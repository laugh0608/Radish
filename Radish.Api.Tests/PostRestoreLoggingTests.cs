using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Castle.DynamicProxy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Api.Filters;
using Radish.Api.Resources;
using Radish.Common.CoreTool;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.Extension.AopExtension;
using Radish.Extension.Log;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using Radish.Service;
using Radish.Shared.Constants;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class PostRestoreLoggingTests
{
    private const string Secret = "POST_RESTORE_PRIVATE_SENTINEL";
    private const long PostId = 912345678;
    private const long UserId = 623456789;
    private const long TenantId = 734567891;
    private const long RevisionId = 823456789;
    private static readonly DateTimeOffset SnapshotTime = new(2026, 10, 2, 8, 30, 0, TimeSpan.Zero);
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task RestoreRulesAndTypedRejections_ShouldKeepCodesAndStayQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var scenario in new[] { "post-missing", "denied", "expected", "revision-missing", "revision-wrong-post", "incomplete", "current", "same", "category", "tag", "attachment", "empty-title", "empty-body", "length", "edit-limit", "post-disappeared", "cas" })
        {
            var f = new Fixture();
            var code = ForumContentRevisionErrorCodes.Conflict;
            var status = 409;
            switch (scenario)
            {
                case "post-missing": f.MissingPostRead = 1; code = ForumContentRevisionErrorCodes.NotFound; status = 404; break;
                case "denied": f.Stored.AuthorId++; code = ForumContentRevisionErrorCodes.AccessDenied; status = 403; break;
                case "expected": f.ExpectedRevision = 9; break;
                case "revision-missing": f.Revisions.Clear(); code = ForumContentRevisionErrorCodes.NotFound; status = 404; break;
                case "revision-wrong-post": f.Target.PostId++; code = ForumContentRevisionErrorCodes.NotFound; status = 404; break;
                case "incomplete": f.Target.IntegrityStatus = ForumContentRevisionIntegrityStatuses.LegacyIncomplete; code = ForumContentRevisionErrorCodes.Incomplete; break;
                case "current": f.Target.RevisionNumber = 2; break;
                case "same": f.Target.Title = f.Stored.Title; f.Target.Content = f.Stored.Content; f.Target.CoverAttachmentId = null; break;
                case "category": f.Category.IsEnabled = false; code = ForumContentRevisionErrorCodes.CategoryUnavailable; break;
                case "tag": f.Tag.IsDeleted = true; code = ForumContentRevisionErrorCodes.TagUnavailable; break;
                case "attachment": f.Attachments[0].BusinessId++; code = ForumContentRevisionErrorCodes.AttachmentUnavailable; break;
                case "empty-title": f.Target.Title = " "; code = ForumContentRevisionErrorCodes.ContentRejected; break;
                case "empty-body": f.Target.Content = " "; code = ForumContentRevisionErrorCodes.ContentRejected; break;
                case "length": f.MaxTitle = 3; code = ForumContentRevisionErrorCodes.ContentRejected; break;
                case "edit-limit": f.Stored.EditCount = 3; code = ForumContentRevisionErrorCodes.EditLimitReached; break;
                case "post-disappeared": f.MissingPostRead = 2; code = ForumContentRevisionErrorCodes.ContentRejected; break;
                case "cas": f.CasRows = 0; break;
            }
            var error = await Assert.ThrowsAsync<BusinessException>(() => f.CallWriteAsync());
            Assert.Equal(status, error.StatusCode);
            Assert.Equal(code, error.ErrorCode);
            Assert.Equal(ForumContentRevisionErrorCodes.ResolveMessageKey(code), error.MessageKey);
            f.AssertTransaction("rollback");
            Assert.DoesNotContain("revision-add", f.Steps);
            capture.AssertQuiet();
        }
        foreach (var state in new[] { ContentSubmissionBeginStatus.InvalidKey, ContentSubmissionBeginStatus.Processing, ContentSubmissionBeginStatus.Conflict, ContentSubmissionBeginStatus.FrequencyLimited })
        {
            var f = new Fixture { BeginStatus = state };
            var error = await Assert.ThrowsAsync<BusinessException>(() => f.CallWriteAsync());
            Assert.Equal(state == ContentSubmissionBeginStatus.InvalidKey ? 400 : state == ContentSubmissionBeginStatus.FrequencyLimited ? 429 : 409, error.StatusCode);
            if (state == ContentSubmissionBeginStatus.Conflict) Assert.Equal(ForumContentRevisionErrorCodes.RestoreKeyConflict, error.ErrorCode);
            Assert.DoesNotContain("post-read", f.Steps);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task UpdateFailuresConvertedTo409_ShouldHaveOneSafeRestoreOwner(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var failure in new Exception[] { new UnsafeArgument(), new InvalidOperationException(Secret), new InvalidOperationException(Secret + "次数") })
        foreach (var stage in new[] { "settings", "columns", "tag-read" })
        {
            var f = new Fixture { FailedStage = stage, FailedOccurrence = stage == "tag-read" ? 2 : 1, Failure = failure };
            var error = await Assert.ThrowsAsync<BusinessException>(() => f.CallWriteAsync());
            Assert.Equal(failure.Message, error.Message);
            Assert.Equal(409, error.StatusCode);
            Assert.Equal(failure.Message.Contains("次数", StringComparison.Ordinal) ? ForumContentRevisionErrorCodes.EditLimitReached : ForumContentRevisionErrorCodes.ContentRejected, error.ErrorCode);
            Assert.Null(error.InnerException);
            f.AssertTransaction("rollback");
            capture.AssertSingle("post.restore_failed", RuntimeFailureSummary.Classify(failure));
            capture.Clear();
            var api = new Fixture { FailedStage = stage, FailedOccurrence = f.FailedOccurrence, Failure = failure };
            await InvokeApiAsync(api.Controller.RestoreRevision, api.Request(), 409);
            api.AssertTransaction("rollback");
            capture.AssertSingle("post.restore_failed", RuntimeFailureSummary.Classify(failure));
            capture.Clear();
        }
        var invalidConfig = new Fixture { MinTitle = 2000 };
        await InvokeApiAsync(invalidConfig.Controller.RestoreRevision, invalidConfig.Request(), 409);
        capture.AssertSingle("post.restore_failed", "invalid-operation");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task FailuresOutsideConversion_ShouldKeepApiOwnershipAndOriginalException(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "snapshot", "submission", "post-read", "revision-read", "category-read", "revision-tags-read", "post-tags", "references-read", "attachment-read", "revision-add", "revision-tags-add", "references-add", "completion" })
        {
            var failure = new ArgumentException(Secret);
            var f = new Fixture { FailedStage = stage, Failure = failure };
            Assert.Same(failure, await Assert.ThrowsAsync<ArgumentException>(() => f.CallWriteAsync()));
            f.AssertTransaction("rollback");
            capture.AssertQuiet();
            var api = new Fixture { FailedStage = stage, Failure = failure };
            await InvokeApiAsync(api.Controller.RestoreRevision, api.Request(), 500);
            capture.AssertSingle("http.failed", "argument", 500);
            capture.Clear();
        }
        foreach (var failure in new Exception[] { new IOException(Secret), new TimeoutException(Secret), new OperationCanceledException(Secret), new BusinessException(Secret, 503), new BusinessException(Secret, 409), new HostileFailure() })
        {
            var f = new Fixture { FailedStage = "columns", Failure = failure };
            Exception? observed = null;
            try { await f.CallWriteAsync(); }
            catch (Exception ex) { observed = ex; }
            Assert.Same(failure, observed);
            capture.AssertQuiet();
            var api = new Fixture { FailedStage = "columns", Failure = failure };
            var status = failure is BusinessException business ? business.StatusCode : 500;
            await InvokeApiAsync(api.Controller.RestoreRevision, api.Request(), status);
            if (status < 500) capture.AssertQuiet();
            else capture.AssertSingle("http.failed", RuntimeFailureSummary.Classify(failure), status);
            capture.Clear();
        }
        // 转换本身未能取得 Message 时，并没有成功消费原异常；只由 API 记录新的失败。
        var unreadable = new Fixture { FailedStage = "columns", Failure = new UnreadableArgument() };
        await InvokeApiAsync(unreadable.Controller.RestoreRevision, unreadable.Request(), 500);
        capture.AssertSingle("http.failed", "io", 500);
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SuccessfulRestoreAndReplay_ShouldKeepImmutableSnapshotsAndAudit(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        var response = await InvokeApiAsync(f.Controller.RestoreRevision, f.Request(), 200);
        var result = Assert.IsType<PostEditResult>(response!.ResponseData);
        Assert.Equal(PostId, result.PostId);
        Assert.Equal(3, result.ContentRevision);
        Assert.Equal(f.NewRevisionId, result.RevisionId);
        Assert.Equal(f.Target.Title, f.Stored.Title);
        Assert.Equal(f.Target.Content, f.Stored.Content);
        Assert.Equal(f.Target.CoverAttachmentId, f.Stored.CoverAttachmentId);
        Assert.Equal(1, f.Stored.EditCount);
        var snapshot = Assert.Single(f.Revisions, r => r.Id != RevisionId);
        Assert.Equal(ForumContentRevisionSourceTypes.Restore, snapshot.SourceType);
        Assert.Equal(RevisionId, snapshot.RestoredFromRevisionId);
        Assert.Equal(3, snapshot.RevisionNumber);
        Assert.Equal(ForumContentRevisionIntegrityStatuses.Complete, snapshot.IntegrityStatus);
        Assert.Equal(UserId, snapshot.EditorId);
        Assert.Equal(Secret, snapshot.EditorName);
        Assert.Equal(SnapshotTime.UtcDateTime, snapshot.CreateTime);
        Assert.Equal(DateTimeKind.Utc, snapshot.CreateTime.Kind);
        Assert.Equal(1, f.Target.RevisionNumber);
        Assert.Equal(ForumContentRevisionSourceTypes.Baseline, f.Target.SourceType);
        var tag = Assert.Single(f.RevisionTags, t => t.RevisionId == f.NewRevisionId);
        Assert.Equal("tag", tag.TagNameSnapshot);
        Assert.Equal(SnapshotTime.UtcDateTime, tag.CreateTime);
        var references = f.References.Where(r => r.RevisionId == f.NewRevisionId).ToList();
        Assert.Equal(2, references.Count);
        Assert.Contains(references, r => r.AttachmentId == 401 && r.ReferenceKind == ForumContentRevisionReferenceKinds.Content);
        Assert.Contains(references, r => r.AttachmentId == 402 && r.ReferenceKind == ForumContentRevisionReferenceKinds.Cover);
        Assert.All(references, r => { Assert.Equal(SnapshotTime.UtcDateTime, r.CreateTime); Assert.Equal(UserId, r.CreateId); });
        Assert.True(f.Steps.IndexOf("columns") < f.Steps.IndexOf("revision-add"));
        Assert.True(f.Steps.IndexOf("references-add") < f.Steps.IndexOf("completion"));
        Assert.True(f.Steps.IndexOf("completion") < f.Steps.IndexOf("commit"));
        var completion = Assert.Single(f.Completions);
        Assert.Equal(ContentSubmissionResultTypes.PostContentRevision, completion.ResultType);
        Assert.Equal(f.NewRevisionId, completion.ResultId);
        f.AssertTransaction("commit");
        capture.AssertQuiet();
        foreach (var state in new[] { ContentSubmissionBeginStatus.Succeeded, ContentSubmissionBeginStatus.DuplicateContent })
        {
            var replay = new Fixture { BeginStatus = state };
            var replayResponse = await InvokeApiAsync(replay.Controller.RestoreRevision, replay.Request(), 200);
            Assert.Equal(RevisionId, Assert.IsType<PostEditResult>(replayResponse!.ResponseData).RevisionId);
            Assert.DoesNotContain("columns", replay.Steps);
            Assert.DoesNotContain("revision-add", replay.Steps);
            Assert.Empty(replay.Completions);
            replay.AssertTransaction("commit");
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SqliteRestore_ShouldCommitOrRollbackPostAndSnapshotRowsTogether(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        new ServiceCollection().ConfigureApplication();
        foreach (var stage in new[] { "success", "columns", "references-add", "completion" })
        {
            using var db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main", DbType = DbType.Sqlite, ConnectionString = "Data Source=:memory:",
                IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
            });
            db.CodeFirst.InitTables<Post, PostContentRevision, PostContentRevisionTag, ForumContentRevisionAttachment>();
            var unit = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
            var f = new Fixture(unit) { FailedStage = stage, Failure = new InvalidOperationException(Secret) };
            await db.Insertable(f.Stored).ExecuteCommandAsync(TestContext.Current.CancellationToken);
            await db.Insertable(f.Target).ExecuteCommandAsync(TestContext.Current.CancellationToken);
            await db.Insertable(f.RevisionTags).ExecuteCommandAsync(TestContext.Current.CancellationToken);
            await db.Insertable(f.References).ExecuteCommandAsync(TestContext.Current.CancellationToken);
            await InvokeApiAsync(f.Controller.RestoreRevision, f.Request(), stage == "success" ? 200 : stage == "columns" ? 409 : 500);
            var post = await db.Queryable<Post>().InSingleAsync(PostId);
            Assert.Equal(stage == "success" ? 3 : 2, post.ContentRevision);
            Assert.Equal(stage == "success" ? f.Target.Title : "current " + Secret, post.Title);
            Assert.Equal(stage == "success" ? 402L : (long?)null, post.CoverAttachmentId);
            Assert.Equal(stage == "success" ? 2 : 1, db.Queryable<PostContentRevision>().Count());
            Assert.Equal(stage == "success" ? 2 : 1, db.Queryable<PostContentRevisionTag>().Count());
            Assert.Equal(stage == "success" ? 4 : 2, db.Queryable<ForumContentRevisionAttachment>().Count());
            Assert.Equal(0, unit.TranCount);
            if (stage == "success") capture.AssertQuiet();
            else capture.AssertSingle(stage == "columns" ? "post.restore_failed" : "http.failed", "invalid-operation", stage == "columns" ? null : 500);
            capture.Clear();
        }
    }

    private sealed class UnsafeArgument() : ArgumentException(Secret)
    {
        public override string ToString() => throw new InvalidOperationException("日志不得渲染异常");
    }
    private sealed class UnreadableArgument : ArgumentException
    {
        public override string Message => throw new IOException(Secret);
    }
    private sealed class HostileFailure : Exception
    {
        public override string Message => throw new InvalidOperationException("不可读取异常原文");
        public override string ToString() => throw new InvalidOperationException("不可渲染异常");
    }
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SnapshotTime;
    }

    private sealed class Fixture
    {
        public List<string> Steps { get; } = [];
        public string? FailedStage { get; set; }
        public int FailedOccurrence { get; set; } = 1;
        public Exception Failure { get; set; } = new IOException(Secret);
        public int ExpectedRevision { get; set; } = 2;
        public int MinTitle { get; set; } = 1;
        public int MaxTitle { get; set; } = 1000;
        public int CasRows { get; set; } = 1;
        public int MissingPostRead { get; set; }
        public long NewRevisionId { get; private set; } = 43;
        public ContentSubmissionBeginStatus BeginStatus { get; set; } = ContentSubmissionBeginStatus.Started;
        public Post Stored { get; } = new("current " + Secret, "current body " + Secret)
        {
            Id = PostId, PublicId = Secret, AuthorId = UserId, AuthorName = Secret, TenantId = 0,
            CategoryId = 101, ContentRevision = 2, IsPublished = true, PublishTime = DateTime.Now
        };
        public PostContentRevision Target { get; } = new()
        {
            Id = RevisionId, TenantId = 0, PostId = PostId, RevisionNumber = 1, Title = "restored " + Secret,
            Content = Secret + " ![image](attachment://401)", CategoryId = 101, CategoryNameSnapshot = "category", CoverAttachmentId = 402
        };
        public Category Category { get; } = new("category") { Id = 101, IsEnabled = true };
        public Tag Tag { get; } = new("tag") { Id = 201, IsEnabled = true };
        public List<PostContentRevision> Revisions { get; } = [];
        public List<PostContentRevisionTag> RevisionTags { get; } = [new() { Id = 501, RevisionId = RevisionId, TagId = 201, TagNameSnapshot = "tag", TenantId = 0 }];
        public List<ForumContentRevisionAttachment> References { get; } =
        [
            new() { Id = 601, TargetId = PostId, RevisionId = RevisionId, AttachmentId = 401, TenantId = 0 },
            new() { Id = 602, TargetId = PostId, RevisionId = RevisionId, AttachmentId = 402, ReferenceKind = ForumContentRevisionReferenceKinds.Cover, TenantId = 0 }
        ];
        public List<Attachment> Attachments { get; } =
        [
            new() { Id = 401, TenantId = 0, BusinessType = "Post", BusinessId = PostId },
            new() { Id = 402, TenantId = 0, BusinessType = "Post", BusinessId = PostId }
        ];
        public List<ContentSubmissionCompletionRequest> Completions { get; } = [];
        public IForumContentWriteService Writes { get; }
        public PostController Controller { get; }

        public Fixture(IUnitOfWorkManage? realUnit = null)
        {
            Revisions.Add(Target);
            var realPosts = realUnit == null ? null : new BaseRepository<Post>(realUnit);
            var realRevisions = realUnit == null ? null : new BaseRepository<PostContentRevision>(realUnit);
            var realTags = realUnit == null ? null : new BaseRepository<PostContentRevisionTag>(realUnit);
            var realReferences = realUnit == null ? null : new BaseRepository<ForumContentRevisionAttachment>(realUnit);
            var posts = new Mock<IBaseRepository<Post>>();
            var reads = 0;
            posts.Setup(p => p.QueryByIdAsync(PostId)).Returns(async () =>
            { Step("post-read"); return ++reads == MissingPostRead ? null : realPosts == null ? Stored : await realPosts.QueryByIdAsync(PostId); });
            posts.Setup(p => p.UpdateColumnsAsync(It.IsAny<Expression<Func<Post, Post>>>(), It.IsAny<Expression<Func<Post, bool>>>()))
                .Returns(async (Expression<Func<Post, Post>> columns, Expression<Func<Post, bool>> predicate) =>
                { Step("columns"); return realPosts == null ? CasRows : await realPosts.UpdateColumnsAsync(columns, predicate); });
            var revisions = new Mock<IBaseRepository<PostContentRevision>>();
            revisions.Setup(r => r.QueryByIdAsync(It.IsAny<long>())).Returns(async (long id) =>
            { Step("revision-read"); return realRevisions == null ? Revisions.SingleOrDefault(r => r.Id == id) : await realRevisions.QueryByIdAsync(id); });
            revisions.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<PostContentRevision, bool>>?>())).Returns(async (Expression<Func<PostContentRevision, bool>>? predicate) =>
                realRevisions == null ? Revisions.FirstOrDefault(r => predicate == null || predicate.Compile()(r)) : await realRevisions.QueryFirstAsync(predicate));
            revisions.Setup(r => r.AddAsync(It.IsAny<PostContentRevision>())).Returns(async (PostContentRevision revision) =>
            { Step("revision-add"); NewRevisionId = realRevisions == null ? 43 : await realRevisions.AddAsync(revision); revision.Id = NewRevisionId; Revisions.Add(revision); return NewRevisionId; });
            var revisionTags = new Mock<IBaseRepository<PostContentRevisionTag>>();
            revisionTags.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<PostContentRevisionTag, bool>>?>())).Returns(async (Expression<Func<PostContentRevisionTag, bool>>? predicate) =>
            { Step("revision-tags-read"); return realTags == null ? RevisionTags.Where(t => predicate == null || predicate.Compile()(t)).ToList() : await realTags.QueryAsync(predicate); });
            revisionTags.Setup(r => r.AddRangeAsync(It.IsAny<List<PostContentRevisionTag>>())).Returns(async (List<PostContentRevisionTag> tags) =>
            { Step("revision-tags-add"); RevisionTags.AddRange(tags); return realTags == null ? tags.Count : await realTags.AddRangeAsync(tags); });
            var references = new Mock<IBaseRepository<ForumContentRevisionAttachment>>();
            references.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<ForumContentRevisionAttachment, bool>>?>())).Returns(async (Expression<Func<ForumContentRevisionAttachment, bool>>? predicate) =>
            { Step("references-read"); return realReferences == null ? References.Where(r => predicate == null || predicate.Compile()(r)).ToList() : await realReferences.QueryAsync(predicate); });
            references.Setup(r => r.AddRangeAsync(It.IsAny<List<ForumContentRevisionAttachment>>())).Returns(async (List<ForumContentRevisionAttachment> values) =>
            { Step("references-add"); References.AddRange(values); return realReferences == null ? values.Count : await realReferences.AddRangeAsync(values); });
            var categories = new Mock<IBaseRepository<Category>>();
            categories.Setup(c => c.QueryByIdAsync(It.IsAny<long>())).ReturnsAsync(() => { Step("category-read"); return Category; });
            var tagsRepository = new Mock<IBaseRepository<Tag>>();
            tagsRepository.Setup(t => t.QueryAsync(It.IsAny<Expression<Func<Tag, bool>>?>())).ReturnsAsync((Expression<Func<Tag, bool>>? predicate) =>
            { Step("tag-read"); return predicate == null || predicate.Compile()(Tag) ? [Tag] : []; });
            var postTags = new Mock<IBaseRepository<PostTag>>();
            postTags.Setup(t => t.QueryAsync(It.IsAny<Expression<Func<PostTag, bool>>?>())).ReturnsAsync(() =>
            { Step("post-tags"); return [new PostTag(PostId, Tag.Id)]; });
            var attachments = new Mock<IBaseRepository<Attachment>>();
            attachments.Setup(a => a.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>?>())).ReturnsAsync((Expression<Func<Attachment, bool>>? predicate) =>
            { Step("attachment-read"); return Attachments.Where(a => predicate == null || predicate.Compile()(a)).ToList(); });
            var settings = new Mock<ISystemSettingProvider>();
            settings.Setup(s => s.GetInt32Async(It.IsAny<string>())).ReturnsAsync((string key) =>
            {
                Step("settings"); return key switch
                {
                    SystemConfigDefaults.PostTitleMinLengthKey => MinTitle, SystemConfigDefaults.PostTitleMaxLengthKey => MaxTitle,
                    SystemConfigDefaults.PostBodyMinLengthKey => 1, _ => 1000
                };
            });
            var service = new PostService(Mock.Of<IMapper>(), posts.Object, Mock.Of<IBaseRepository<UserPostLike>>(), postTags.Object,
                categories.Object, tagsRepository.Object, Mock.Of<IBaseRepository<PostPoll>>(), Mock.Of<IBaseRepository<PostPollOption>>(),
                Mock.Of<IBaseRepository<PostPollVote>>(), Mock.Of<IBaseRepository<PostQuestion>>(), Mock.Of<IBaseRepository<PostAnswer>>(),
                Mock.Of<ITagService>(), Mock.Of<ICoinRewardService>(), Mock.Of<INotificationService>(), Mock.Of<INotificationDedupService>(),
                Mock.Of<IExperienceService>(), Mock.Of<IBaseRepository<PostEditHistory>>(), Mock.Of<IAttachmentService>(),
                Options.Create(new ForumEditHistoryOptions { Post = new ForumPostEditHistoryOptions { MaxEditCount = 3 } }), settings.Object);
            var revisionService = new ForumContentRevisionService(posts.Object, Mock.Of<IBaseRepository<Comment>>(), revisions.Object,
                revisionTags.Object, Mock.Of<IBaseRepository<CommentContentRevision>>(), references.Object, postTags.Object,
                tagsRepository.Object, categories.Object, attachments.Object, service, Mock.Of<ICommentService>(), new FixedClock());
            var submission = new Mock<IContentSubmissionService>();
            submission.Setup(s => s.CreateRequestSnapshot(It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<IReadOnlyDictionary<string, object?>>()))
                .Returns(() => { Step("snapshot"); return new ContentSubmissionRequestSnapshot { RequestDigest = Secret, RequestSummary = Secret, ContentFingerprint = Secret }; });
            submission.Setup(s => s.BeginAsync(It.IsAny<ContentSubmissionBeginRequest>())).ReturnsAsync(() =>
            {
                Step("submission"); return new ContentSubmissionBeginResult
                { Status = BeginStatus, RecordId = 41, ResultType = ContentSubmissionResultTypes.PostContentRevision, ResultId = RevisionId, RetryAfterSeconds = 10 };
            });
            submission.Setup(s => s.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>())).Callback<ContentSubmissionCompletionRequest>(request =>
            { Step("completion"); Completions.Add(request); }).Returns(Task.CompletedTask);
            var unit = new Mock<IUnitOfWorkManage>();
            unit.Setup(u => u.BeginTran(It.IsAny<MethodInfo>())).Callback(() => Step("begin"));
            unit.Setup(u => u.CommitTran(It.IsAny<MethodInfo>())).Callback(() => Step("commit"));
            unit.Setup(u => u.RollbackTran(It.IsAny<MethodInfo>())).Callback(() => Step("rollback"));
            Writes = new ProxyGenerator().CreateInterfaceProxyWithTarget<IForumContentWriteService>(
                new ForumContentWriteService(submission.Object, service, Mock.Of<ICommentService>(), revisionService), new TranAop(realUnit ?? unit.Object));
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(new CurrentUser { UserId = UserId, UserName = Secret, TenantId = 0, IsAuthenticated = true });
            Controller = new PostController(service, Mock.Of<IUserService>(), Mock.Of<IContentModerationService>(), null, null,
                Mock.Of<IUserBrowseHistoryService>(), current.Object, Writes, Mock.Of<IStringLocalizer<Errors>>());
        }
        private void Step(string stage)
        {
            Steps.Add(stage);
            if (stage == FailedStage && Steps.Count(s => s == stage) == FailedOccurrence) throw Failure;
        }
        public RestoreForumContentRevisionDto Request() => new() { TargetId = PostId, RevisionId = RevisionId, ExpectedContentRevision = ExpectedRevision, ClientSubmissionId = Secret };
        public Task<ContentWriteResult<PostEditResult>> CallWriteAsync() => Writes.RestorePostRevisionAsync(0, PostId, RevisionId, ExpectedRevision, UserId, Secret, false, Secret);
        public void AssertTransaction(string outcome)
        {
            Assert.Single(Steps, s => s == "begin");
            Assert.Single(Steps, s => s == outcome);
            Assert.DoesNotContain(outcome == "commit" ? "rollback" : "commit", Steps);
        }
    }

    private static async Task<MessageModel?> InvokeApiAsync(Func<RestoreForumContentRevisionDto, Task<MessageModel>> action, RestoreForumContentRevisionDto request, int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(Log.Logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        MessageModel? result = null;
        app.Run(async context => { result = await action(request); await ApplyResultAsync(context, result); });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/Post/RestoreRevision";
        context.Request.QueryString = new QueryString("?query=" + Secret);
        using var output = new MemoryStream();
        context.Response.Body = output;
        context.RequestServices = app.Services;
        await ((IApplicationBuilder)app).Build()(context);
        Assert.Equal(status, context.Response.StatusCode);
        return result;
    }

    private static Task ApplyResultAsync(HttpContext context, MessageModel result)
    {
        var actionContext = new ActionContext(context, new RouteData(), new ActionDescriptor());
        var objectResult = new ObjectResult(result);
        var filters = new List<IFilterMetadata>();
        var controller = new object();
        return new ApiErrorContractAttribute().OnResultExecutionAsync(
            new ResultExecutingContext(actionContext, filters, objectResult, controller), () =>
            {
                context.Response.StatusCode = objectResult.StatusCode!.Value;
                return Task.FromResult(new ResultExecutedContext(actionContext, filters, objectResult, controller));
            });
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        private readonly StringWriter _output = new();
        private readonly Serilog.Core.Logger _logger;
        private readonly string? _mode;
        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate)
            {
                _mode = environment;
                var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RadishLogging:Mode"] = environment,
                    ["RadishLogging:Diagnostics"] = (environment == "Development").ToString()
                }).Build();
                RuntimeLoggingConfiguration.Configure(config, settings, environment, "api", _output, _output);
            }
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(_output));
            _logger = config.CreateLogger();
            Log.Logger = _logger;
        }
        public void AssertQuiet() => Assert.Equal("", _output.ToString());
        public void AssertSingle(string code, string kind, int? status = null)
        {
            var text = _output.ToString();
            Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(code, text);
            Assert.Contains("Error", text);
            Assert.Contains(kind, text);
            foreach (var forbidden in new[] { Secret, PostId.ToString(), UserId.ToString(), TenantId.ToString(), "runtime.unclassified", "System.IO.IOException", "System.ArgumentException", "System.InvalidOperationException", "PostContentRevision", "ClientSubmissionId", "RequestDigest", "RequestSummary" })
                Assert.DoesNotContain(forbidden, text);
            if (_mode != null)
            {
                using var json = JsonDocument.Parse(text);
                var root = json.RootElement;
                Assert.Equal(_mode, root.GetProperty("mode").GetString());
                Assert.Equal(code, root.GetProperty("eventCode").GetString());
                var properties = root.GetProperty("properties");
                Assert.Equal(status.HasValue ? 2 : 1, properties.EnumerateObject().Count());
                Assert.Equal(kind, properties.GetProperty("failureKind").GetString());
                if (status.HasValue) Assert.Equal(status.Value, properties.GetProperty("statusCode").GetInt32());
            }
        }
        public void Clear() => _output.GetStringBuilder().Clear();
        public void Dispose() { Log.Logger = _previous; _logger.Dispose(); _output.Dispose(); }
    }

    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
