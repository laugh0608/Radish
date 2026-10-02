using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Hubs;
using Radish.Api.Services;
using Radish.Common.HttpContextTool;
using Radish.Extension.Log;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class CommentRealtimeLoggingTests
{
    private const string Secret = "COMMENT_PUSH_PRIVATE_SENTINEL";
    private const long PostId = 812345679;
    private const long CommentId = 923456781;
    private const long UserId = 734567891;
    private const long ParentId = 623456789;
    private const long RootId = 512345678;
    private static readonly string[] Events = ["CommentCreated", "CommentUpdated", "CommentDeleted", "CommentLikeChanged", "CommentHighlightsChanged"];
    private static readonly string[] Operations = ["create", "like", "delete", "update", "restore"];
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SuccessfulPushes_ShouldKeepAllEventNamesGroupsAndPayloadsQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        var before = DateTime.UtcNow;
        await f.PushAllAsync();
        Assert.Equal(Events, f.Sends.Select(s => s.Event));
        Assert.Equal(Enumerable.Repeat($"post-comments:{PostId}", 5), f.Groups);
        for (var i = 0; i < 4; i++)
        {
            var payload = Assert.IsType<CommentRealtimeEventVo>(f.Sends[i].Payload);
            Assert.Equal(PostId, payload.VoPostId);
            Assert.Equal(CommentId, payload.VoCommentId);
            Assert.Equal(ParentId, payload.VoParentCommentId);
            Assert.Equal(RootId, payload.VoRootCommentId);
            Assert.Equal(DateTimeKind.Utc, payload.VoEventTime.Kind);
            Assert.InRange(payload.VoEventTime, before, DateTime.UtcNow);
            if (i < 2) Assert.Same(f.Detail, payload.VoComment);
            else Assert.Null(payload.VoComment);
            Assert.Equal(i == 2 ? (int?)null : 7, payload.VoLikeCount);
        }
        Assert.Same(f.Highlight, f.Sends[4].Payload);
        capture.AssertWarnings(0);
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task InvalidAndUnchangedInputs_ShouldRetainExistingShortCircuitBoundaries(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        foreach (var invalid in new long[] { 0, -1 })
        {
            await f.Push.PushCreatedAsync(new CommentVo { VoPostId = invalid, VoId = CommentId });
            await f.Push.PushUpdatedAsync(new CommentVo { VoPostId = PostId, VoId = invalid });
            await f.Push.PushCreatedAsync(new CommentVo { VoPostId = PostId, VoId = invalid });
            await f.Push.PushUpdatedAsync(new CommentVo { VoPostId = invalid, VoId = CommentId });
            await f.Push.PushHighlightChangedAsync(new CommentHighlightRecheckResultVo { VoPostId = invalid, VoChanged = true });
        }
        await f.Push.PushHighlightChangedAsync(CommentHighlightRecheckResultVo.NoChange(PostId, ParentId, 2));
        f.Context.VerifyNoOtherCalls();
        Assert.Empty(f.Sends);
        // Delete / like have never validated IDs here; do not silently broaden this logging change.
        await f.Push.PushDeletedAsync(0, 0, null, null);
        await f.Push.PushLikeChangedAsync(-1, -1, null, null, -1);
        Assert.Equal(new[] { "post-comments:0", "post-comments:-1" }, f.Groups);
        Assert.Equal(2, f.Sends.Count);
        capture.AssertWarnings(0);
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task PushFailures_ShouldBeConsumedOnceWithoutEvaluatingExceptionOrPayload(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "clients", "group", "send" })
        foreach (var (failure, kind) in new (Exception, string)[]
        {
            (new IOException(Secret), "io"), (new TimeoutException(Secret), "timeout"),
            (new OperationCanceledException(Secret), "cancelled"), (new InvalidOperationException(Secret), "invalid-operation"),
            (new OpaqueException(), "other")
        })
        {
            using var f = new Fixture(capture.Logger) { PushFailure = failure, FailedEvent = "all" };
            if (stage == "clients") f.Context.SetupGet(c => c.Clients).Throws(failure);
            if (stage == "group") f.Clients.Setup(c => c.Group(It.IsAny<string>())).Throws(failure);
            await f.PushAllAsync();
            f.Context.VerifyGet(c => c.Clients, Times.Exactly(5));
            if (stage == "send") Assert.Equal(Events, f.Sends.Select(s => s.Event));
            else Assert.Empty(f.Sends);
            capture.AssertWarnings(5, kind);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ControllerWrites_ShouldRetainResultsAndContinueHighlightPushAfterDeliveryFailure(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var failure in new[] { "none", "first", "highlight", "all" })
        {
            using var f = new Fixture(capture.Logger);
            var firstEvent = operation switch
            {
                "create" => "CommentCreated", "like" => "CommentLikeChanged", "delete" => "CommentDeleted", _ => "CommentUpdated"
            };
            f.FailedEvent = failure switch { "first" => firstEvent, "highlight" => "CommentHighlightsChanged", "all" => "all", _ => null };
            var response = await f.CallControllerAsync(operation);
            Assert.True(response.IsSuccess);
            Assert.Equal(200, response.StatusCode);
            if (operation == "create") Assert.Equal(CommentId, response.ResponseData);
            if (operation == "like") Assert.Same(f.LikeResult, response.ResponseData);
            if (operation is "update" or "restore") Assert.Same(f.EditResult, response.ResponseData);
            Assert.Equal(new[] { firstEvent, "CommentHighlightsChanged" }, f.Sends.Select(s => s.Event));
            var expected = operation switch
            {
                "create" => new[] { "permission", "create", "detail" },
                "like" => new[] { "like", "detail" },
                "delete" => new[] { "query", "delete", "highlight" },
                "update" => new[] { "query", "update", "detail", "highlight" },
                _ => new[] { "restore", "detail", "highlight" }
            };
            Assert.Equal(expected.Concat(new[] { firstEvent, "CommentHighlightsChanged" }), f.Steps);
            capture.AssertWarnings(failure == "none" ? 0 : failure == "all" ? 2 : 1);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ControllerReplaysAndMissingDetails_ShouldKeepExistingPushDecisions(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in new[] { "create", "update", "restore" })
        foreach (var status in new[] { ContentWriteStatus.Replayed, ContentWriteStatus.DuplicateContent, ContentWriteStatus.NoChange })
        {
            using var f = new Fixture(capture.Logger) { WriteStatus = status };
            var result = await f.CallControllerAsync(operation);
            Assert.True(result.IsSuccess);
            Assert.Empty(f.Sends);
            Assert.DoesNotContain("detail", f.Steps);
            Assert.DoesNotContain("highlight", f.Steps);
        }
        foreach (var operation in new[] { "create", "like", "update", "restore" })
        {
            using var f = new Fixture(capture.Logger) { MissingDetail = true };
            Assert.True((await f.CallControllerAsync(operation)).IsSuccess);
            Assert.Equal(operation is "create" or "like" ? new[] { "CommentHighlightsChanged" } : Array.Empty<string>(), f.Sends.Select(s => s.Event));
        }
        using (var f = new Fixture(capture.Logger))
        {
            f.LikeResult.HighlightRecheckResult = null;
            await f.CallControllerAsync("like");
            Assert.Equal("CommentLikeChanged", Assert.Single(f.Sends).Event);
        }
        capture.AssertWarnings(0);
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task PrecedingFailures_ShouldPreserveExceptionOwnershipAndResponseMappings(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var stage in operation switch
        {
            "delete" => new[] { "delete", "highlight" },
            "update" or "restore" => new[] { operation, "detail", "highlight" },
            _ => new[] { operation, "detail" }
        })
        {
            using var f = new Fixture(capture.Logger) { FailedStage = stage, BusinessFailure = new IOException(Secret) };
            Assert.Same(f.BusinessFailure, await Assert.ThrowsAsync<IOException>(() => f.CallControllerAsync(operation)));
            Assert.Empty(f.Sends);
        }
        foreach (var (operation, failure) in new (string, Exception)[]
        {
            ("create", new ArgumentException(Secret)), ("like", new InvalidOperationException(Secret)),
            ("update", new ArgumentException(Secret)), ("update", new InvalidOperationException(Secret))
        })
        {
            using var f = new Fixture(capture.Logger) { FailedStage = operation, BusinessFailure = failure };
            var result = await f.CallControllerAsync(operation);
            Assert.False(result.IsSuccess);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal(Secret, result.MessageInfo);
            Assert.Empty(f.Sends);
        }
        // ToggleLike's existing catch also covers detail lookup; restore has no such catch.
        using (var f = new Fixture(capture.Logger) { FailedStage = "detail", BusinessFailure = new InvalidOperationException(Secret) })
            Assert.Equal(400, (await f.CallControllerAsync("like")).StatusCode);
        using (var f = new Fixture(capture.Logger) { FailedStage = "restore", BusinessFailure = new InvalidOperationException(Secret) })
            Assert.Same(f.BusinessFailure, await Assert.ThrowsAsync<InvalidOperationException>(() => f.CallControllerAsync("restore")));
        capture.AssertWarnings(0);
    }

    private sealed class OpaqueException : Exception
    {
        public override string Message => throw new InvalidOperationException("Exception text must not be evaluated");
        public override string ToString() => throw new InvalidOperationException("Exception text must not be evaluated");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ILoggerFactory _factory;
        public Mock<IHubContext<CommentHub>> Context { get; } = new(MockBehavior.Strict);
        public Mock<IHubClients> Clients { get; } = new(MockBehavior.Strict);
        public Mock<IClientProxy> Proxy { get; } = new(MockBehavior.Strict);
        public Mock<ICommentService> Comments { get; } = new(MockBehavior.Strict);
        public List<string> Groups { get; } = [];
        public List<string> Steps { get; } = [];
        public List<(string Event, object Payload)> Sends { get; } = [];
        public string? FailedEvent { get; set; }
        public Exception PushFailure { get; set; } = new IOException(Secret);
        public string? FailedStage { get; set; }
        public Exception? BusinessFailure { get; set; }
        public ContentWriteStatus WriteStatus { get; set; } = ContentWriteStatus.Created;
        public bool MissingDetail { get; set; }
        public CommentVo Detail { get; } = new()
        {
            VoPostId = PostId, VoId = CommentId, VoParentId = ParentId, VoRootId = RootId,
            VoAuthorId = UserId, VoAuthorName = Secret, VoContent = Secret, VoLikeCount = 7
        };
        public CommentHighlightRecheckResultVo Highlight { get; } = new()
        {
            VoPostId = PostId, VoParentCommentId = ParentId, VoChanged = true, VoHighlightType = 2, VoCurrentCommentIds = [CommentId]
        };
        public CommentLikeResultDto LikeResult { get; }
        public CommentEditResult EditResult { get; } = new() { CommentId = CommentId, PostId = PostId, ParentId = ParentId, ContentRevision = 2, RevisionId = 42 };
        public CommentRealtimePushService Push { get; }
        public CommentController Controller { get; }

        public Fixture(Serilog.ILogger logger)
        {
            _factory = LoggerFactory.Create(b => b.AddSerilog(logger, dispose: false));
            LikeResult = new CommentLikeResultDto { IsLiked = true, LikeCount = 7, HighlightRecheckResult = Highlight };
            Context.SetupGet(c => c.Clients).Returns(Clients.Object);
            Clients.Setup(c => c.Group(It.IsAny<string>())).Callback<string>(Groups.Add).Returns(Proxy.Object);
            Proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Returns<string, object?[], CancellationToken>((name, args, token) =>
                {
                    Assert.Equal(CancellationToken.None, token);
                    Sends.Add((name, Assert.IsAssignableFrom<object>(Assert.Single(args))));
                    Steps.Add(name);
                    return FailedEvent == "all" || FailedEvent == name ? Task.FromException(PushFailure) : Task.CompletedTask;
                });
            Push = new CommentRealtimePushService(Context.Object, _factory.CreateLogger<CommentRealtimePushService>());
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(new CurrentUser { UserId = UserId, UserName = Secret, TenantId = 9, IsAuthenticated = true });
            var moderation = new Mock<IContentModerationService>(MockBehavior.Strict);
            moderation.Setup(m => m.GetPublishPermissionAsync(UserId)).Callback(() => Step("permission"))
                .ReturnsAsync(new ContentModerationPermissionVo { VoCanPublish = true });
            var writes = new Mock<IForumContentWriteService>(MockBehavior.Strict);
            writes.Setup(w => w.CreateCommentAsync(It.Is<Comment>(c => c.PostId == PostId && c.Content == Secret && c.AuthorId == UserId && c.TenantId == 9), Secret))
                .Callback(() => Step("create")).ReturnsAsync(() => new ContentWriteResult<CommentCreateResult>
                {
                    Status = WriteStatus, Result = new CommentCreateResult { CommentId = CommentId, HighlightRecheckResult = Highlight }
                });
            writes.Setup(w => w.UpdateCommentAsync(9, CommentId, Secret, UserId, Secret, false, Secret, 1))
                .Callback(() => Step("update")).ReturnsAsync(() => new ContentWriteResult<CommentEditResult> { Status = WriteStatus, Result = EditResult });
            writes.Setup(w => w.RestoreCommentRevisionAsync(9, CommentId, 42, 1, UserId, Secret, false, Secret))
                .Callback(() => Step("restore")).ReturnsAsync(() => new ContentWriteResult<CommentEditResult> { Status = WriteStatus, Result = EditResult });
            Comments.Setup(c => c.GetCommentDetailAsync(CommentId, UserId)).Callback(() => Step("detail")).ReturnsAsync(() => MissingDetail ? null : Detail);
            Comments.Setup(c => c.QueryFirstAsync(It.IsAny<Expression<Func<Comment, bool>>?>())).Callback(() => Step("query")).ReturnsAsync(Detail);
            Comments.Setup(c => c.ToggleLikeAsync(UserId, Secret, CommentId)).Callback(() => Step("like")).ReturnsAsync(LikeResult);
            Comments.Setup(c => c.TriggerHighlightRecheckAsync(PostId, ParentId)).Callback(() => Step("highlight")).ReturnsAsync(Highlight);
            Comments.Setup(c => c.UpdateColumnsAsync(It.IsAny<Expression<Func<Comment, Comment>>>(), It.IsAny<Expression<Func<Comment, bool>>>()))
                .Callback<Expression<Func<Comment, Comment>>, Expression<Func<Comment, bool>>>((columns, where) =>
                {
                    Step("delete");
                    var value = columns.Compile()(new Comment());
                    Assert.True(value.IsDeleted);
                    Assert.Equal(UserId, value.ModifyId);
                    Assert.Equal(Secret, value.ModifyBy);
                    Assert.True(where.Compile()(new Comment { Id = CommentId }));
                    Assert.False(where.Compile()(new Comment { Id = CommentId + 1 }));
                }).ReturnsAsync(1);
            Controller = new CommentController(Comments.Object, Mock.Of<IPostService>(), Mock.Of<IUserService>(), moderation.Object, current.Object, Push, writes.Object);
        }

        private void Step(string stage)
        {
            Steps.Add(stage);
            if (FailedStage == stage) throw BusinessFailure!;
        }
        public async Task PushAllAsync()
        {
            await Push.PushCreatedAsync(Detail);
            await Push.PushUpdatedAsync(Detail);
            await Push.PushDeletedAsync(PostId, CommentId, ParentId, RootId);
            await Push.PushLikeChangedAsync(PostId, CommentId, ParentId, RootId, 7);
            await Push.PushHighlightChangedAsync(Highlight);
        }
        public Task<MessageModel> CallControllerAsync(string operation) => operation switch
        {
            "create" => Controller.Create(new CreateCommentDto { PostId = PostId, Content = Secret, ClientSubmissionId = Secret }),
            "like" => Controller.ToggleLike(CommentId),
            "delete" => Controller.Delete(CommentId),
            "update" => Controller.Update(new UpdateCommentDto { CommentId = CommentId, Content = Secret, ClientSubmissionId = Secret, ExpectedContentRevision = 1 }),
            "restore" => Controller.RestoreRevision(new RestoreForumContentRevisionDto { TargetId = CommentId, RevisionId = 42, ExpectedContentRevision = 1, ClientSubmissionId = Secret }),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        public void Dispose() => _factory.Dispose();
    }

    private sealed class Capture : IDisposable
    {
        private readonly StringWriter _output = new();
        private readonly string? _candidateMode;
        public Serilog.Core.Logger Logger { get; }
        public Capture(bool candidate, string environment)
        {
            var config = new Serilog.LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate)
            {
                _candidateMode = environment;
                var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RadishLogging:Mode"] = environment,
                    ["RadishLogging:Diagnostics"] = environment == "Development" ? "true" : "false"
                }).Build();
                RuntimeLoggingConfiguration.Configure(config, settings, environment, "api", _output, _output);
            }
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(_output));
            Logger = config.CreateLogger();
        }
        public void AssertWarnings(int count, string kind = "io")
        {
            var text = _output.ToString();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(count, lines.Length);
            foreach (var line in lines)
            {
                Assert.Contains("comment.push_failed", line);
                Assert.Contains("Warning", line);
                if (_candidateMode != null)
                {
                    using var json = JsonDocument.Parse(line);
                    var root = json.RootElement;
                    Assert.Equal(_candidateMode, root.GetProperty("mode").GetString());
                    Assert.Equal("comment.push_failed", root.GetProperty("eventCode").GetString());
                    Assert.Equal("Warning", root.GetProperty("level").GetString());
                    var properties = root.GetProperty("properties");
                    Assert.Equal(kind, properties.GetProperty("failureKind").GetString());
                    Assert.Single(properties.EnumerateObject());
                }
                else Assert.Contains($"kind={kind}", line);
            }
            foreach (var forbidden in new[] { Secret, PostId.ToString(), CommentId.ToString(), UserId.ToString(), ParentId.ToString(), RootId.ToString(), "post-comments:", "EventName", "System.IO.IOException", "runtime.unclassified" }.Concat(Events))
                Assert.DoesNotContain(forbidden, text);
        }
        public void Clear() => _output.GetStringBuilder().Clear();
        public void Dispose() { Logger.Dispose(); _output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
