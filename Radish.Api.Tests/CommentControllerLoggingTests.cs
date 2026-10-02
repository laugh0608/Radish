using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Castle.DynamicProxy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Api.Filters;
using Radish.Api.Hubs;
using Radish.Api.Services;
using Radish.Common.CacheTool;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.Extension.AopExtension;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Repository.UnitOfWorks;
using Radish.Service;
using Radish.Shared.Constants;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class CommentControllerLoggingTests
{
    private const string Secret = "COMMENT_WRITE_PRIVATE_SENTINEL";
    private const long CommentId = 812345679;
    private const long PostId = 923456781;
    private const long UserId = 623456789;
    private const long TenantId = 734567891;
    private static readonly string[] Operations = ["create", "like", "edit"];
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task NormalValidationAndBusinessRejections_ShouldKeepQuietResponses(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var scenario in new[] { "short-create", "long-create", "long-edit", "edit-limit", "edit-window", "missing-like", "missing-edit", "forbidden-edit", "empty-create", "denied-create" })
        {
            using var f = new Fixture();
            var operation = scenario.EndsWith("create", StringComparison.Ordinal) ? "create" : scenario == "missing-like" ? "like" : "edit";
            var status = 400;
            switch (scenario)
            {
                case "short-create": f.MinLength = 80; break;
                case "long-create" or "long-edit": f.MaxLength = 3; break;
                case "edit-limit": f.Stored.EditCount = 3; break;
                case "edit-window": f.Stored.CreateTime = DateTime.Now.AddHours(-1); break;
                case "missing-like": f.FailedStage = "like"; f.Failure = new CommentOperationRejectedException("评论不存在或已被删除"); break;
                case "missing-edit": f.Stored.IsDeleted = true; status = 404; break;
                case "forbidden-edit": f.Stored.AuthorId = UserId + 1; status = 403; break;
                case "empty-create": f.Content = "  "; break;
                case "denied-create": f.CanPublish = false; status = 403; break;
            }
            var result = await InvokeApiAsync(() => f.CallAsync(operation), status);
            Assert.False(result!.IsSuccess);
            Assert.DoesNotContain("insert", f.Steps);
            Assert.DoesNotContain("columns", f.Steps);
            Assert.Empty(f.Sends);
            capture.AssertQuiet();
        }
        foreach (var beginStatus in new[] { ContentSubmissionBeginStatus.InvalidKey, ContentSubmissionBeginStatus.Conflict, ContentSubmissionBeginStatus.FrequencyLimited })
        {
            using var f = new Fixture { BeginStatus = beginStatus };
            await InvokeApiAsync(() => f.CallAsync("create"), beginStatus == ContentSubmissionBeginStatus.InvalidKey ? 400 : beginStatus == ContentSubmissionBeginStatus.Conflict ? 409 : 429);
            f.AssertTransaction("rollback");
            Assert.Empty(f.Drafts);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ArgumentFailures_ShouldHaveOneSafeOwnerAndKeep400(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in new[] { "create", "edit" })
        foreach (var stage in operation == "create"
            ? new[] { "snapshot", "submission", "settings", "insert", "post-count", "outbox", "revision", "completion" }
            : new[] { "snapshot", "submission", "settings", "columns", "revision", "completion" })
        {
            using var f = new Fixture { FailedStage = stage, Failure = new ArgumentException(Secret) };
            var result = await InvokeApiAsync(() => f.CallAsync(operation), 400);
            Assert.False(result!.IsSuccess);
            Assert.Equal(Secret, result.MessageInfo);
            f.AssertTransaction("rollback");
            Assert.Empty(f.Sends);
            capture.AssertSingle(operation == "create" ? "comment.create_failed" : "comment.edit_failed", "argument");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task OperationAndUnhandledFailures_ShouldPreserveFinalApiOwnership(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        foreach (var failure in new Exception[]
        {
            new InvalidOperationException(Secret), new IOException(Secret), new TimeoutException(Secret),
            new OperationCanceledException(Secret), new BusinessException(Secret, 503, "Comment.Test", "error.comment.test")
        })
        {
            using var f = new Fixture { FailedStage = operation == "like" ? "like" : "revision", Failure = failure };
            var handled = failure is InvalidOperationException && operation != "create";
            var status = handled ? 400 : failure is BusinessException ? 503 : 500;
            var result = await InvokeApiAsync(() => f.CallAsync(operation), status);
            if (handled) Assert.Equal(Secret, result!.MessageInfo);
            capture.AssertSingle(handled ? operation == "like" ? "comment.like_failed" : "comment.edit_failed" : "http.failed",
                RuntimeFailureSummary.Classify(failure), handled ? null : status);
            if (operation != "like") f.AssertTransaction("rollback");
            Assert.Empty(f.Sends);
            capture.Clear();
        }
        // 上层只记录最终处理；下层继续抛出原来的异常实例。
        foreach (var operation in new[] { "create", "edit" })
        {
            using var f = new Fixture { FailedStage = "revision", Failure = new IOException(Secret) };
            Assert.Same(f.Failure, await Assert.ThrowsAsync<IOException>(() => f.CallWriteAsync(operation)));
            f.AssertTransaction("rollback");
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task AfterWriteDetailFailure_ShouldKeepCommittedMutationAndOriginalCatchBoundary(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        {
            using var f = new Fixture { FailedStage = "detail", Failure = new InvalidOperationException(Secret) };
            var status = operation == "like" ? 400 : 500;
            await InvokeApiAsync(() => f.CallAsync(operation), status);
            capture.AssertSingle(operation == "like" ? "comment.like_failed" : "http.failed", "invalid-operation", operation == "like" ? null : status);
            if (operation != "like") f.AssertTransaction("commit");
            Assert.Empty(f.Sends);
            Assert.Contains(operation == "create" ? "insert" : operation == "edit" ? "columns" : "like", f.Steps);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SuccessReplayAndNoChange_ShouldKeepLedgerRevisionOutboxAndPushDecisions(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in Operations)
        {
            using var f = new Fixture();
            var result = await InvokeApiAsync(() => f.CallAsync(operation), 200);
            Assert.True(result!.IsSuccess);
            var pushed = Assert.Single(f.Sends);
            Assert.Equal(operation == "create" ? "CommentCreated" : operation == "edit" ? "CommentUpdated" : "CommentLikeChanged", pushed.Name);
            Assert.Equal(CommentId, pushed.Payload.VoCommentId);
            Assert.Equal(PostId, pushed.Payload.VoPostId);
            if (operation != "like")
            {
                f.AssertTransaction("commit");
                var completion = Assert.Single(f.Completions);
                Assert.Equal(41, completion.RecordId);
                Assert.Equal(operation == "create" ? ContentSubmissionResultTypes.Comment : ContentSubmissionResultTypes.CommentContentRevision, completion.ResultType);
                Assert.Equal(operation == "create" ? CommentId : 42, completion.ResultId);
                Assert.Equal(operation == "create" ? ForumContentRevisionSourceTypes.Baseline : ForumContentRevisionSourceTypes.Edit, Assert.Single(f.RevisionSources));
                Assert.True(f.Steps.IndexOf("revision") < f.Steps.IndexOf("completion"));
                Assert.True(f.Steps.IndexOf("commit") < f.Steps.IndexOf("detail"));
            }
            if (operation == "create")
            {
                var draft = Assert.Single(f.Drafts);
                Assert.Equal($"task:comment-published:{CommentId}", draft.IdempotencyKey);
                Assert.Equal(ReliableTaskTypes.CommentPublished, draft.TaskType);
                Assert.Equal(TenantId, draft.TenantId);
                var payload = JsonSerializer.Deserialize<CommentPublishedTaskPayload>(draft.PayloadJson)!;
                Assert.Equal(CommentId, payload.CommentId);
                Assert.Equal(PostId, payload.PostId);
                Assert.Equal(Secret, payload.Content);
                Assert.True(f.Steps.IndexOf("outbox") < f.Steps.IndexOf("revision"));
            }
            if (operation == "edit")
            {
                Assert.Equal(Secret, f.Stored.Content);
                Assert.Equal(2, f.Stored.ContentRevision);
                Assert.Equal(1, f.Stored.EditCount);
                Assert.Equal(UserId, f.Stored.ModifyId);
                Assert.Equal(Secret, f.Stored.ModifyBy);
            }
            capture.AssertQuiet();
        }
        foreach (var operation in new[] { "create", "edit" })
        foreach (var beginStatus in new[] { ContentSubmissionBeginStatus.Succeeded, ContentSubmissionBeginStatus.DuplicateContent })
        {
            using var f = new Fixture { BeginStatus = beginStatus };
            Assert.True((await InvokeApiAsync(() => f.CallAsync(operation), 200))!.IsSuccess);
            f.AssertTransaction("commit");
            Assert.Empty(f.Completions);
            Assert.Empty(f.RevisionSources);
            Assert.Empty(f.Drafts);
            Assert.Empty(f.Sends);
            capture.AssertQuiet();
        }
        using var unchanged = new Fixture();
        unchanged.Content = unchanged.Stored.Content;
        var noChange = await InvokeApiAsync(() => unchanged.CallAsync("edit"), 200);
        Assert.Equal("内容没有变化，无需保存", noChange!.MessageInfo);
        Assert.Single(unchanged.Completions);
        Assert.Empty(unchanged.RevisionSources);
        Assert.Empty(unchanged.Sends);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task EditValidationConsumption_ShouldKeepTupleAndOnlyLogDependencyFailures(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using (var f = new Fixture { MaxLength = 3 })
        {
            var result = await f.Service.UpdateCommentAsync(CommentId, Secret, UserId, Secret);
            Assert.False(result.success);
            Assert.Equal("评论内容不能超过 3 个字符", result.message);
            capture.AssertQuiet();
            var exception = await Assert.ThrowsAsync<CommentContentValidationException>(() => f.Service.AddCommentAsync(new Comment(Secret)));
            Assert.IsAssignableFrom<ArgumentException>(exception);
            capture.AssertQuiet();
        }
        using (var f = new Fixture { FailedStage = "settings", Failure = new ArgumentException(Secret) })
        {
            var result = await f.Service.UpdateCommentAsync(CommentId, Secret, UserId, Secret);
            Assert.False(result.success);
            Assert.Equal(Secret, result.message);
            Assert.DoesNotContain("columns", f.Steps);
            capture.AssertSingle("comment.edit_failed", "argument");
            capture.Clear();
        }
        using var invalid = new Fixture { MinLength = 101, MaxLength = 100 };
        await InvokeApiAsync(() => invalid.CallAsync("edit"), 400);
        capture.AssertSingle("comment.edit_failed", "invalid-operation");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ILoggerFactory _factory;
        public List<string> Steps { get; } = [];
        public List<ReliableOutboxDraft> Drafts { get; } = [];
        public List<ContentSubmissionCompletionRequest> Completions { get; } = [];
        public List<string> RevisionSources { get; } = [];
        public List<(string Name, CommentRealtimeEventVo Payload)> Sends { get; } = [];
        public string? FailedStage { get; set; }
        public Exception Failure { get; set; } = new IOException(Secret);
        public string Content { get; set; } = Secret;
        public bool CanPublish { get; set; } = true;
        public int MinLength { get; set; } = 1;
        public int MaxLength { get; set; } = 100;
        public ContentSubmissionBeginStatus BeginStatus { get; set; } = ContentSubmissionBeginStatus.Started;
        public Comment Stored { get; private set; } = new("old " + Secret)
        {
            Id = CommentId, PostId = PostId, AuthorId = UserId, AuthorName = Secret, TenantId = TenantId,
            CreateTime = DateTime.Now, ContentRevision = 1
        };
        public CommentService Service { get; }
        public IForumContentWriteService Writes { get; }
        public CommentController Controller { get; }

        public Fixture()
        {
            var repository = new Mock<IBaseRepository<Comment>>();
            repository.Setup(r => r.QueryByIdAsync(CommentId)).ReturnsAsync(() => Stored);
            repository.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
                .ReturnsAsync((Expression<Func<Comment, bool>>? predicate) => predicate == null || predicate.Compile()(Stored) ? Stored : null);
            repository.Setup(r => r.AddAsync(It.IsAny<Comment>())).ReturnsAsync((Comment comment) =>
            { Step("insert"); comment.Id = CommentId; Stored = comment; return CommentId; });
            repository.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<Comment, Comment>>>(), It.IsAny<Expression<Func<Comment, bool>>>()))
                .ReturnsAsync(() => { Step("columns"); return 1; });
            var mapper = new Mock<IMapper>();
            mapper.Setup(m => m.Map<CommentVo>(It.IsAny<Comment>())).Returns((Comment? comment) => comment == null ? null! : new CommentVo
            {
                VoId = comment.Id, VoPostId = comment.PostId, VoAuthorId = comment.AuthorId, VoAuthorName = comment.AuthorName,
                VoContent = comment.Content, VoContentRevision = comment.ContentRevision
            });
            var post = new Mock<IPostService>();
            post.Setup(p => p.UpdateCommentCountAsync(PostId, 1)).Callback(() => Step("post-count")).Returns(Task.CompletedTask);
            var likes = new Mock<IBaseRepository<UserCommentLike>>();
            likes.Setup(l => l.QueryAsync(It.IsAny<Expression<Func<UserCommentLike, bool>>?>())).ReturnsAsync([]);
            var highlights = new Mock<IBaseRepository<CommentHighlight>>();
            highlights.Setup(h => h.QueryFirstAsync(It.IsAny<Expression<Func<CommentHighlight, bool>>?>()))
                .Callback(() => Step("detail")).ReturnsAsync((CommentHighlight?)null);
            var settings = new Mock<ISystemSettingProvider>();
            settings.Setup(s => s.GetInt32Async(It.IsAny<string>())).ReturnsAsync((string key) =>
            { Step("settings"); return key == SystemConfigDefaults.CommentBodyMinLengthKey ? MinLength : MaxLength; });
            var outbox = new Mock<IReliableOutboxRepository>();
            outbox.Setup(o => o.AddAsync(It.IsAny<ReliableOutboxDraft>())).ReturnsAsync((ReliableOutboxDraft draft) =>
            { Step("outbox"); Drafts.Add(draft); return 1; });
            var custom = new Mock<ICommentRepository>();
            custom.Setup(c => c.ToggleCommentLikeAsync(UserId, Secret, CommentId)).ReturnsAsync(() =>
            { Step("like"); return new CommentLikePersistenceResult(CommentId, TenantId, PostId, null, UserId, Secret, true, 3, 0); });
            Service = new CommentService(mapper.Object, repository.Object, likes.Object, highlights.Object, post.Object,
                Mock.Of<ICaching>(), Mock.Of<ICoinRewardService>(), Mock.Of<INotificationService>(), Mock.Of<INotificationDedupService>(),
                Mock.Of<IExperienceService>(), Mock.Of<IAttachmentUrlResolver>(), Microsoft.Extensions.Options.Options.Create(new CommentHighlightOptions { RealtimeUpdate = false }),
                Mock.Of<IBaseRepository<CommentEditHistory>>(), Microsoft.Extensions.Options.Options.Create(new ForumEditHistoryOptions
                { Comment = new ForumCommentEditHistoryOptions { EditWindowMinutes = 5, MaxEditCount = 3 } }), settings.Object,
                commentCustomRepository: custom.Object, reliableOutboxService: new ReliableOutboxService(outbox.Object));
            var submission = new Mock<IContentSubmissionService>();
            submission.Setup(s => s.CreateRequestSnapshot(It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<IReadOnlyDictionary<string, object?>>()))
                .Returns(() => { Step("snapshot"); return new ContentSubmissionRequestSnapshot { RequestDigest = Secret, RequestSummary = Secret, ContentFingerprint = Secret }; });
            submission.Setup(s => s.BeginAsync(It.IsAny<ContentSubmissionBeginRequest>())).ReturnsAsync(() =>
            {
                Step("submission");
                return new ContentSubmissionBeginResult { Status = BeginStatus, RecordId = 41, ResultType = ContentSubmissionResultTypes.Comment, ResultId = CommentId, RetryAfterSeconds = 10 };
            });
            submission.Setup(s => s.CompleteSuccessAsync(It.IsAny<ContentSubmissionCompletionRequest>()))
                .Callback<ContentSubmissionCompletionRequest>(request => { Step("completion"); Completions.Add(request); }).Returns(Task.CompletedTask);
            var revision = new Mock<IForumContentRevisionService>();
            revision.Setup(r => r.AppendCommentRevisionAsync(CommentId, It.IsAny<string>(), null, UserId, Secret))
                .ReturnsAsync((long _, string source, long? _, long _, string _) =>
                { Step("revision"); RevisionSources.Add(source); return new ForumContentRevisionWriteResult { VoRevisionId = 42, VoContentRevision = Stored.ContentRevision }; });
            revision.Setup(r => r.GetCurrentCommentRevisionAsync(CommentId)).ReturnsAsync(new ForumContentRevisionWriteResult { VoRevisionId = 42, VoContentRevision = 1 });
            var unit = new Mock<IUnitOfWorkManage>();
            unit.Setup(u => u.BeginTran(It.IsAny<MethodInfo>())).Callback(() => Step("begin"));
            unit.Setup(u => u.CommitTran(It.IsAny<MethodInfo>())).Callback(() => Step("commit"));
            unit.Setup(u => u.RollbackTran(It.IsAny<MethodInfo>())).Callback(() => Step("rollback"));
            Writes = new ProxyGenerator().CreateInterfaceProxyWithTarget<IForumContentWriteService>(
                new ForumContentWriteService(submission.Object, post.Object, Service, revision.Object), new TranAop(unit.Object));
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(new CurrentUser { UserId = UserId, UserName = Secret, TenantId = TenantId, IsAuthenticated = true });
            var permission = new Mock<IContentModerationService>();
            permission.Setup(p => p.GetPublishPermissionAsync(UserId)).ReturnsAsync(() => new ContentModerationPermissionVo { VoCanPublish = CanPublish, VoDenyReason = Secret });
            _factory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Trace).AddSerilog(Log.Logger, dispose: false));
            var proxy = new Mock<IClientProxy>();
            proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((name, args, _) => Sends.Add((name, Assert.IsType<CommentRealtimeEventVo>(Assert.Single(args)))))
                .Returns(Task.CompletedTask);
            var clients = new Mock<IHubClients>();
            clients.Setup(c => c.Group($"post-comments:{PostId}")).Returns(proxy.Object);
            var hub = new Mock<IHubContext<CommentHub>>();
            hub.SetupGet(h => h.Clients).Returns(clients.Object);
            Controller = new CommentController(Service, post.Object, Mock.Of<IUserService>(), permission.Object, current.Object,
                new CommentRealtimePushService(hub.Object, _factory.CreateLogger<CommentRealtimePushService>()), Writes);
        }
        private void Step(string stage)
        {
            Steps.Add(stage);
            if (stage == FailedStage) throw Failure;
        }
        public Task<MessageModel> CallAsync(string operation) => operation switch
        {
            "create" => Controller.Create(new CreateCommentDto { PostId = PostId, Content = Content, ClientSubmissionId = Secret }),
            "like" => Controller.ToggleLike(CommentId),
            _ => Controller.Update(new UpdateCommentDto { CommentId = CommentId, Content = Content, ClientSubmissionId = Secret, ExpectedContentRevision = 1 })
        };
        public async Task CallWriteAsync(string operation)
        {
            if (operation == "create") await Writes.CreateCommentAsync(new Comment(Content) { PostId = PostId, AuthorId = UserId, AuthorName = Secret, TenantId = TenantId }, Secret);
            else await Writes.UpdateCommentAsync(TenantId, CommentId, Content, UserId, Secret, false, Secret, 1);
        }
        public void AssertTransaction(string result)
        {
            Assert.Single(Steps, s => s == "begin");
            Assert.Single(Steps, s => s == result);
            Assert.DoesNotContain(result == "commit" ? "rollback" : "commit", Steps);
        }
        public void Dispose() => _factory.Dispose();
    }

    private static async Task<MessageModel?> InvokeApiAsync(Func<Task<MessageModel>> action, int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(Log.Logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        MessageModel? result = null;
        app.Run(async context => { result = await action(); await ApplyResultAsync(context, result); });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/Comment/Create";
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
            foreach (var forbidden in new[] { Secret, CommentId.ToString(), PostId.ToString(), UserId.ToString(), TenantId.ToString(), "runtime.unclassified", "System.IO.IOException", "System.ArgumentException", "System.InvalidOperationException", "post-comments:", "task:comment-published", "RequestDigest", "RequestSummary" })
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
