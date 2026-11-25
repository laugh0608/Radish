using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Moq;
using Radish.Common.Exceptions;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Shared.Constants;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

using static ContentStickerOutputLoggingTests;

public sealed partial class CommentHighlightLoggingTests
{
    internal static async Task VerifyHighlightsAsync(AssertEvents assertEvents)
    {
        foreach (var sofa in new[] { false, true })
        {
            using var f = new Fixture(sofa) { FailedStage = "cache" };
            var result = await f.Proxy.TriggerHighlightRecheckAsync(PostId, f.Parent);
            Assert.False(result.VoChanged);
            Assert.Equal(2, f.Highlights.Count);
            Assert.Single(f.Drafts);
            f.Unit.Verify(u => u.CommitTran(It.IsAny<MethodInfo>()), Times.Once);
            assertEvents(new Expected(typeof(CommentService), f.RecheckEvent, "Error", "io"));
        }
        using var fill = new Fixture(false) { HighlightQueryStage = "fill", FailedStage = "fill-2" };
        var response = await fill.Controller.GetRootComments(PostId);
        Assert.True(response.IsSuccess);
        Assert.Single(Assert.IsType<VoPagedResult<CommentVo>>(response.ResponseData).VoItems);
        Assert.Contains("profiles", fill.Steps);
        assertEvents(new Expected(typeof(CommentService), "comment.highlight_fill_failed", "Error", "io"));
    }

    internal static async Task VerifyOuterAsync(AssertEvents assertEvents)
    {
        using var f = new Fixture(false) { FailedStage = "count" };
        Assert.False((await f.Service.TriggerHighlightRecheckAsync(PostId)).VoChanged);
        assertEvents(new Expected(typeof(CommentService), "comment.god_recheck_failed", "Error", "io"),
            new Expected(typeof(CommentService), "comment.highlight_recheck_failed", "Error", "aggregate"));
    }

    internal static async Task VerifyHighlightFaultAsync(Func<Func<Task>, Task>? whenAuditFails)
    {
        using var f = new Fixture(false) { FailedStage = "cache" };
        if (whenAuditFails is null)
        {
            Assert.False((await f.Proxy.TriggerHighlightRecheckAsync(PostId)).VoChanged);
            f.Unit.Verify(u => u.CommitTran(It.IsAny<MethodInfo>()), Times.Once);
        }
        else
        {
            await whenAuditFails(() => f.Proxy.TriggerHighlightRecheckAsync(PostId));
            f.Unit.Verify(u => u.RollbackTran(It.IsAny<MethodInfo>()), Times.Once);
        }
        Assert.Single(f.Drafts);
    }
}

public sealed partial class CommentControllerLoggingTests
{
    internal static async Task VerifyEditAsync(AssertEvents assertEvents)
    {
        using var f = new Fixture { FailedStage = "settings", Failure = new ArgumentException(Secret) };
        var result = await f.Service.UpdateCommentAsync(CommentId, Secret, UserId, Secret);
        Assert.False(result.success);
        Assert.Equal(Secret, result.message);
        Assert.DoesNotContain("columns", f.Steps);
        assertEvents(new Expected(typeof(CommentService), "comment.edit_failed", "Error", "argument"));
    }
}

public sealed partial class ContentSubmissionLoggingTests
{
    internal static async Task VerifySubmissionAsync(AssertEvents assertEvents)
    {
        await VerifySubmissionFaultAsync(null);
        assertEvents(new Expected(typeof(ContentSubmissionService), "content_submission.conflict_resolved", "Warning", "database"));
    }

    internal static async Task VerifySubmissionFaultAsync(Func<Func<Task>, Task>? whenAuditFails)
    {
        var f = new Fixture();
        if (whenAuditFails is null)
            Assert.Equal(ContentSubmissionBeginStatus.Processing, (await f.Service.BeginAsync(Request())).Status);
        else await whenAuditFails(() => f.Service.BeginAsync(Request()));
        Assert.Equal(new[] { "lookup", "savepoint", "insert", "rollback-savepoint", "lookup" }, f.Steps.Take(5));
    }
}

public sealed partial class PostRestoreLoggingTests
{
    internal static async Task VerifyRestoreAsync(AssertEvents assertEvents)
    {
        foreach (var (exception, kind) in new (Exception, string)[]
                 { (new ArgumentException(Secret), "argument"), (new InvalidOperationException(Secret + "次数"), "invalid-operation") })
        {
            var f = new Fixture { FailedStage = "columns", Failure = exception };
            var error = await Assert.ThrowsAsync<BusinessException>(f.CallWriteAsync);
            Assert.Equal(409, error.StatusCode);
            Assert.Equal(kind == "argument" ? ForumContentRevisionErrorCodes.ContentRejected : ForumContentRevisionErrorCodes.EditLimitReached, error.ErrorCode);
            f.AssertTransaction("rollback");
            assertEvents(new Expected(typeof(ForumContentRevisionService), "post.restore_failed", "Error", kind));
        }
    }

    internal static async Task VerifyRestoreFaultAsync(Func<Func<Task>, Task>? whenAuditFails)
    {
        var f = new Fixture { FailedStage = "columns", Failure = new ArgumentException(Secret) };
        if (whenAuditFails is null) Assert.Equal(409, (await Assert.ThrowsAsync<BusinessException>(f.CallWriteAsync)).StatusCode);
        else await whenAuditFails(f.CallWriteAsync);
        f.AssertTransaction("rollback");
    }
}

public sealed partial class ReactionControllerLoggingTests
{
    internal static async Task VerifyReactionAsync(AssertEvents assertEvents)
    {
        await VerifyReactionFaultAsync(null);
        assertEvents(new Expected(typeof(ReactionService), "reaction.retrying", "Warning", "other"));
    }

    internal static async Task VerifyReactionFaultAsync(Func<Func<Task>, Task>? whenAuditFails)
    {
        using var f = new Fixture("toggle");
        var first = new CountingConflict();
        f.AddFailures.Enqueue(first);
        if (whenAuditFails is null) Assert.True((await f.CallAsync()).IsSuccess);
        else await whenAuditFails(f.CallAsync);
        Assert.Equal(1, first.RenderCount);
        Assert.Equal(whenAuditFails is null ? 2 : 1, f.Steps.Count(s => s == "reactions.AddAsync"));
        Assert.Equal(whenAuditFails is null ? 1 : 0, f.Db.Queryable<Reaction>().Count());
    }
}

public sealed partial class StickerControllerLoggingTests
{
    internal static async Task VerifyStickersAsync(AssertEvents assertEvents)
    {
        using (var batch = new Fixture("batch") { FailedStage = "attachment.QueryByIdAsync", FailOccurrence = 2, Failure = new UnsafeOperation() })
        {
            batch.EnableThumbnails();
            var response = await batch.CallAsync();
            Assert.Equal("ImageProcessFailed", response.Code);
            Assert.Single(Assert.IsType<StickerBatchAddResultVo>(response.ResponseData).VoFailedItems);
            Assert.Equal(2, batch.Db.Queryable<Sticker>().Count());
            Assert.Single(Directory.GetFiles(batch.DirectoryPath, "*", SearchOption.AllDirectories));
            assertEvents(new Expected(typeof(StickerService), "sticker.batch_item_failed", "Error", "invalid-operation"));
        }
        using (var thumbnail = new Fixture("single") { ThumbnailSuccess = false, ThrowOnDispose = true })
        {
            thumbnail.EnableThumbnails();
            Assert.True((await thumbnail.CallAsync()).IsSuccess);
            Assert.Equal(3, thumbnail.Db.Queryable<Sticker>().Count());
            assertEvents(new Expected(typeof(StickerService), "sticker.thumbnail_failed", "Warning", "io"));
        }
        foreach (var (stage, code) in new[]
                 { ("cache.read", "sticker.cache_read_failed"), ("cache.write", "sticker.cache_write_failed"), ("cache.remove", "sticker.cache_invalidation_failed") })
        {
            using var f = new Fixture("single") { FailedStage = stage, Failure = new HostileFailure() };
            if (stage == "cache.remove") Assert.True((await f.CallAsync()).IsSuccess);
            else Assert.Single(await f.Service.GetGroupsAsync(0));
            assertEvents(new Expected(typeof(StickerService), code, "Warning", "other"));
        }
    }

    internal static async Task VerifyStickerFaultAsync(Func<Func<Task>, Task>? whenAuditFails)
    {
        using var f = new Fixture("single") { FailedStage = "cache.read", Failure = new HostileFailure() };
        if (whenAuditFails is null)
            Assert.Single(await f.Service.GetGroupsAsync(0));
        else await whenAuditFails(() => f.Service.GetGroupsAsync(0));
        Assert.Equal(whenAuditFails is null, f.Steps.Contains("cache.write"));
    }
}
