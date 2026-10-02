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
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Api.Filters;
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
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class PostQuickReplyLoggingTests
{
    private const string Secret = "QUICK_REPLY_PRIVATE_SENTINEL";
    private const long PostId = 812345679;
    private const long ReplyId = 923456781;
    private const long UserId = 623456789;
    private const long ReceiverId = 512345678;
    private const long TenantId = 734567891;
    private static string CooldownKey => $"post_quick_reply:cooldown:{UserId}:{PostId}";
    private static CreatePostQuickReplyDto Request => new() { PostId = PostId, Content = $"  {Secret}\n reply  " };
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Create_ShouldKeepNotificationPayloadTransactionAndRateLimits(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture();
        var before = DateTime.UtcNow;
        var result = await InvokeApiAsync(() => f.Controller.Create(Request), 200);
        Assert.True(result!.IsSuccess);
        var reply = Assert.IsType<PostQuickReplyVo>(result.ResponseData);
        Assert.Equal(ReplyId, reply.VoId);
        Assert.Equal($"{Secret} reply", reply.VoContent);
        var draft = Assert.Single(f.Drafts);
        var notification = JsonSerializer.Deserialize<NotificationRequestedTaskPayload>(draft.PayloadJson)!.Notification;
        Assert.Equal(ReliableOutboxSources.Main, draft.SourceDatabase);
        Assert.Equal(TenantId, draft.TenantId);
        Assert.Equal(ReliableTaskTypes.NotificationRequested, draft.TaskType);
        Assert.Equal(1, draft.SchemaVersion);
        Assert.Equal(6, draft.MaxAttempts);
        Assert.Equal("PostQuickReply", draft.AggregateType);
        Assert.Equal(ReplyId.ToString(), draft.AggregateId);
        Assert.Equal($"task:notification:post-quick-reply:{ReplyId}", draft.IdempotencyKey);
        Assert.Equal($"notification:post-quick-reply:{ReplyId}:receiver:{ReceiverId}", notification.BusinessKey);
        Assert.True(notification.NotificationId > 0);
        Assert.Equal(NotificationType.PostQuickReplied, notification.Type);
        Assert.Equal("帖子收到轻回应", notification.Title);
        Assert.Equal(reply.VoContent, notification.Content);
        Assert.Equal((int)NotificationPriority.Normal, notification.Priority);
        Assert.Equal(BusinessType.Post, notification.BusinessType);
        Assert.Equal(PostId, notification.BusinessId);
        Assert.Equal(UserId, notification.TriggerId);
        Assert.Equal(Secret, notification.TriggerName);
        Assert.Equal(Secret + "/avatar", notification.TriggerAvatar);
        Assert.Equal(new[] { ReceiverId }, notification.ReceiverUserIds);
        Assert.Equal(TenantId, notification.TenantId);
        Assert.Equal(Secret, notification.TemplateArguments!["actorName"]);
        Assert.Equal(Secret, notification.TemplateArguments["targetTitle"]);
        Assert.Equal(NotificationTargetKind.ForumPost, notification.TargetKind);
        Assert.Equal(PostId, notification.Target!.PostId);
        Assert.Equal(Secret, notification.Target.PostPublicId);
        Assert.Equal(reply.VoCreateTime, draft.OccurredAtUtc);
        Assert.Equal(draft.OccurredAtUtc, notification.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, draft.OccurredAtUtc.Kind);
        Assert.InRange(draft.OccurredAtUtc, before, DateTime.UtcNow);
        Assert.Equal(new[] { "begin", "insert", "cooldown", "dedup", "profiles", "outbox", "commit" }, f.Steps);
        Assert.Equal(TimeSpan.FromSeconds(30), f.CacheMarks[CooldownKey]);
        Assert.Equal(TimeSpan.FromSeconds(300), f.CacheMarks.Single(p => p.Key != CooldownKey).Value);

        var repeat = await InvokeApiAsync(() => f.Controller.Create(Request), 429);
        Assert.Equal("QuickReply.RateLimitExceeded", repeat!.Code);
        f.CacheMarks.Remove(CooldownKey);
        repeat = await InvokeApiAsync(() => f.Controller.Create(Request), 409);
        Assert.Equal("QuickReply.DuplicateContent", repeat!.Code);
        Assert.Single(f.Drafts);
        f.Replies.Verify(r => r.AddAsync(It.IsAny<PostQuickReply>()), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task NotificationFailure_ShouldRollbackAndHaveOnlyOneFinalOwner(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var failure in new Exception[]
        {
            new ArgumentException(Secret), new InvalidOperationException(Secret), new IOException(Secret),
            new TimeoutException(Secret), new OperationCanceledException(Secret), new HostileException(),
            new BusinessException(Secret, 503, "QuickReply.Test", "error.quick_reply.test"),
            new BusinessException(Secret, 409, "QuickReply.Test", "error.quick_reply.test")
        })
        {
            using (var direct = new Fixture())
            {
                direct.Fail("outbox", failure);
                Exception? observed = null;
                try { await direct.Proxy.CreateAsync(Request, UserId, Secret, TenantId); }
                catch (Exception exception) { observed = exception; }
                // 避免断言库格式化恶意异常；只核对原实例传播。
                Assert.True(ReferenceEquals(failure, observed));
                direct.AssertRolledBack();
                if (failure is ArgumentException) capture.AssertSingle("quick_reply.notification_enqueue_failed", "argument");
                else capture.AssertQuiet();
                capture.Clear();
            }

            using var f = new Fixture();
            f.Fail("outbox", failure);
            var status = failure is ArgumentException ? 400 : failure is BusinessException business ? business.StatusCode : 500;
            var result = await InvokeApiAsync(() => f.Controller.Create(Request), status);
            f.AssertRolledBack();
            Assert.Equal(2, f.CacheMarks.Count); // 事务 AOP 不撤销已写入的缓存标记。
            if (failure is ArgumentException)
            {
                Assert.Equal(Secret, result!.MessageInfo);
                capture.AssertSingle("quick_reply.notification_enqueue_failed", "argument");
            }
            else if (status >= 500) capture.AssertSingle("http.failed", RuntimeFailureSummary.Classify(failure), status: status);
            else capture.AssertQuiet();
            if (failure is BusinessException)
            {
                Assert.Equal("QuickReply.Test", result!.Code);
                Assert.Equal("error.quick_reply.test", result.MessageKey);
                Assert.Equal(Secret, result.MessageInfo);
            }
            capture.Clear();
            var repeat = await InvokeApiAsync(() => f.Controller.Create(Request), 429);
            Assert.Equal("QuickReply.RateLimitExceeded", repeat!.Code);
            f.Outbox.Verify(r => r.AddAsync(It.IsAny<ReliableOutboxDraft>()), Times.Once);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task PreNotificationAndMissingOutboxFailures_ShouldUseApiBoundary(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "insert", "cache", "profiles", "missing-outbox", "permission" })
        {
            using var f = new Fixture(missingOutbox: stage == "missing-outbox");
            if (stage != "missing-outbox") f.Fail(stage, new IOException(Secret));
            await InvokeApiAsync(() => f.Controller.Create(Request), 500);
            capture.AssertSingle("http.failed", stage == "missing-outbox" ? "invalid-operation" : "io", status: 500);
            if (stage == "permission") Assert.Empty(f.Steps);
            else f.AssertRolledBack();
            f.Outbox.Verify(r => r.AddAsync(It.IsAny<ReliableOutboxDraft>()), Times.Never);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DisabledAndQueryConfigurationFailures_ShouldKeepResponseAndSafeConsumedEvents(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var operation in new[] { "create", "delete", "query" })
        {
            using var f = new Fixture();
            f.Options.Enable = false;
            var result = await InvokeApiAsync(() => operation switch
            {
                "create" => f.Controller.Create(Request), "delete" => f.Controller.Delete(ReplyId),
                _ => f.Controller.GetRecentByPostId(PostId)
            }, 503);
            if (operation != "query")
            {
                Assert.Equal("QuickReply.Disabled", result!.Code);
                Assert.Equal("error.quick_reply.disabled", result.MessageKey);
                f.AssertRolledBack();
            }
            capture.AssertSingle("http.failed", "other", status: 503);
            capture.Clear();
        }
        using var invalid = new Fixture();
        invalid.Settings.Setup(s => s.GetInt32Async(SystemConfigDefaults.QuickReplyDefaultTakeKey)).ReturnsAsync(61);
        var query = await InvokeApiAsync(() => invalid.Controller.GetRecentByPostId(PostId), 404);
        Assert.Contains("默认返回条数不能大于最大返回条数", query!.MessageInfo);
        capture.AssertSingle("quick_reply.query_rejected", "invalid-operation", "Warning");
        capture.Clear();
        await InvokeApiAsync(() => invalid.Controller.GetRecentByPostId(0), 400);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ValidationAndBusinessRejections_ShouldStayQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "anonymous", "denied", "invalid-post", "empty", "missing-post", "locked", "missing-reply", "not-owner" })
        {
            using var f = new Fixture();
            var request = Request;
            var status = 400;
            switch (stage)
            {
                case "anonymous": f.Current = CurrentUser.Anonymous; status = 401; break;
                case "denied": f.Permission.Setup(p => p.GetPublishPermissionAsync(UserId)).ReturnsAsync(new ContentModerationPermissionVo { VoCanPublish = false, VoDenyReason = Secret }); status = 403; break;
                case "invalid-post": request.PostId = 0; break;
                case "empty": request.Content = " \n "; break;
                case "missing-post": f.Posts.Setup(p => p.QueryByIdAsync(PostId)).ReturnsAsync((Post?)null); status = 404; break;
                case "locked": f.Post.IsLocked = true; status = 409; break;
                case "missing-reply": status = 404; break;
                case "not-owner": f.Stored = new PostQuickReply { Id = ReplyId, AuthorId = ReceiverId }; status = 403; break;
            }
            await InvokeApiAsync(() => stage is "missing-reply" or "not-owner" ? f.Controller.Delete(ReplyId) : f.Controller.Create(request), status);
            f.Replies.Verify(r => r.AddAsync(It.IsAny<PostQuickReply>()), Times.Never);
            f.Outbox.Verify(r => r.AddAsync(It.IsAny<ReliableOutboxDraft>()), Times.Never);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SkippedNotificationsAndDelete_ShouldKeepQuietSuccessAndAuditColumns(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var receiver in new[] { 0, UserId })
        {
            using var f = new Fixture();
            f.Post.AuthorId = receiver;
            Assert.True((await InvokeApiAsync(() => f.Controller.Create(Request), 200))!.IsSuccess);
            f.Outbox.Verify(r => r.AddAsync(It.IsAny<ReliableOutboxDraft>()), Times.Never);
            var before = DateTime.UtcNow;
            Assert.True((await InvokeApiAsync(() => f.Controller.Delete(ReplyId), 200))!.IsSuccess);
            var audit = Assert.IsType<PostQuickReply>(f.DeleteColumns);
            Assert.True(audit.IsDeleted);
            Assert.Equal(Secret, audit.DeletedBy);
            Assert.Equal(Secret, audit.ModifyBy);
            Assert.Equal(UserId, audit.ModifyId);
            Assert.InRange(audit.DeletedAt!.Value, before, DateTime.UtcNow);
            Assert.InRange(audit.ModifyTime!.Value, before, DateTime.UtcNow);
            Assert.Equal(2, f.Steps.Count(s => s == "commit"));
            Assert.DoesNotContain("rollback", f.Steps);
            capture.AssertQuiet();
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ILoggerFactory _factory;
        public Mock<IBaseRepository<PostQuickReply>> Replies { get; } = new();
        public Mock<IBaseRepository<Post>> Posts { get; } = new();
        public Mock<IBaseRepository<User>> Users { get; } = new();
        public Mock<ICaching> Cache { get; } = new();
        public Mock<ISystemSettingProvider> Settings { get; } = new();
        public Mock<IReliableOutboxRepository> Outbox { get; } = new();
        public Mock<IContentModerationService> Permission { get; } = new();
        public ForumQuickReplyOptions Options { get; } = new() { Enable = true };
        public Post Post { get; } = new(new PostInitializationOptions(Secret, Secret)) { Id = PostId, AuthorId = ReceiverId, PublicId = Secret };
        public CurrentUser Current { get; set; } = new() { UserId = UserId, UserName = " " + Secret + " ", TenantId = TenantId, IsAuthenticated = true };
        public PostQuickReply? Stored { get; set; }
        public PostQuickReply? DeleteColumns { get; private set; }
        public List<string> Steps { get; } = [];
        public Dictionary<string, TimeSpan> CacheMarks { get; } = [];
        public List<ReliableOutboxDraft> Drafts { get; } = [];
        public IPostQuickReplyService Proxy { get; }
        public PostQuickReplyController Controller { get; }

        public Fixture(bool missingOutbox = false)
        {
            Posts.Setup(p => p.QueryByIdAsync(PostId)).ReturnsAsync(Post);
            Replies.Setup(r => r.AddAsync(It.IsAny<PostQuickReply>())).ReturnsAsync((PostQuickReply reply) =>
            { Steps.Add("insert"); Stored = reply; return ReplyId; });
            Replies.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<PostQuickReply, bool>>>()))
                .ReturnsAsync((Expression<Func<PostQuickReply, bool>> predicate) => Stored != null && predicate.Compile()(Stored) ? Stored : null);
            Replies.Setup(r => r.UpdateColumnsAsync(It.IsAny<Expression<Func<PostQuickReply, PostQuickReply>>>(), It.IsAny<Expression<Func<PostQuickReply, bool>>>()))
                .ReturnsAsync((Expression<Func<PostQuickReply, PostQuickReply>> columns, Expression<Func<PostQuickReply, bool>> predicate) =>
                { Assert.True(predicate.Compile()(Stored!)); DeleteColumns = columns.Compile()(Stored!); return 1; });
            Cache.Setup(c => c.ExistsAsync(It.IsAny<string>())).ReturnsAsync((string key) => CacheMarks.ContainsKey(key));
            Cache.Setup(c => c.SetStringAsync(It.IsAny<string>(), "1", It.IsAny<TimeSpan>()))
                .Callback<string, string, TimeSpan>((key, _, ttl) => { Steps.Add(key == CooldownKey ? "cooldown" : "dedup"); CacheMarks[key] = ttl; })
                .Returns(Task.CompletedTask);
            Settings.Setup(s => s.GetInt32Async(It.IsAny<string>())).ReturnsAsync((string key) => key switch
            {
                SystemConfigDefaults.QuickReplyMaxContentLengthKey => 100,
                SystemConfigDefaults.QuickReplyDefaultTakeKey => 30,
                SystemConfigDefaults.QuickReplyMaxTakeKey => 60,
                SystemConfigDefaults.QuickReplyPerPostCooldownSecondsKey => 30,
                SystemConfigDefaults.QuickReplyDuplicateWindowSecondsKey => 300,
                _ => throw new ArgumentOutOfRangeException(nameof(key))
            });
            Users.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .Callback(() => Steps.Add("profiles")).ReturnsAsync([]);
            Outbox.Setup(r => r.AddAsync(It.IsAny<ReliableOutboxDraft>())).ReturnsAsync((ReliableOutboxDraft draft) =>
            { Steps.Add("outbox"); Drafts.Add(draft); return 1; });
            Permission.Setup(p => p.GetPublishPermissionAsync(UserId)).ReturnsAsync(new ContentModerationPermissionVo { VoCanPublish = true });
            var mapper = new Mock<IMapper>();
            mapper.Setup(m => m.Map<PostQuickReplyVo>(It.IsAny<PostQuickReply>())).Returns((PostQuickReply reply) => new PostQuickReplyVo
            {
                VoId = reply.Id, VoPostId = reply.PostId, VoAuthorId = reply.AuthorId, VoAuthorName = reply.AuthorName,
                VoAuthorAvatarUrl = Secret + "/avatar", VoContent = reply.Content, VoCreateTime = reply.CreateTime
            });
            _factory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Trace).AddSerilog(Log.Logger, dispose: false));
            var service = new PostQuickReplyService(mapper.Object, Replies.Object, Posts.Object, Cache.Object, Settings.Object,
                Microsoft.Extensions.Options.Options.Create(Options), logger: _factory.CreateLogger<PostQuickReplyService>(),
                userRepository: Users.Object, reliableOutboxService: missingOutbox ? null : new ReliableOutboxService(Outbox.Object));
            var unit = new Mock<IUnitOfWorkManage>();
            unit.Setup(u => u.BeginTran(It.IsAny<MethodInfo>())).Callback(() => Steps.Add("begin"));
            unit.Setup(u => u.CommitTran(It.IsAny<MethodInfo>())).Callback(() => Steps.Add("commit"));
            unit.Setup(u => u.RollbackTran(It.IsAny<MethodInfo>())).Callback(() => Steps.Add("rollback"));
            Proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget<IPostQuickReplyService>(service, new TranAop(unit.Object));
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(c => c.Current).Returns(() => Current);
            Controller = new PostQuickReplyController(Proxy, Permission.Object, current.Object);
        }

        public void Fail(string stage, Exception failure)
        {
            switch (stage)
            {
                case "outbox": Outbox.Setup(r => r.AddAsync(It.IsAny<ReliableOutboxDraft>())).ThrowsAsync(failure); break;
                case "insert": Replies.Setup(r => r.AddAsync(It.IsAny<PostQuickReply>())).ThrowsAsync(failure); break;
                case "cache": Cache.Setup(c => c.SetStringAsync(It.IsAny<string>(), "1", It.IsAny<TimeSpan>())).ThrowsAsync(failure); break;
                case "profiles": Users.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<User, bool>>>())).ThrowsAsync(failure); break;
                case "permission": Permission.Setup(p => p.GetPublishPermissionAsync(UserId)).ThrowsAsync(failure); break;
                default: throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }
        public void AssertRolledBack()
        {
            Assert.Equal("begin", Steps[0]);
            Assert.Equal("rollback", Steps[^1]);
            Assert.Single(Steps, s => s == "rollback");
            Assert.DoesNotContain("commit", Steps);
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
        app.Run(async context =>
        {
            result = await action();
            await ApplyResultAsync(context, result);
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/PostQuickReply/Create";
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

    private sealed class HostileException : Exception
    {
        public override string Message => throw new InvalidOperationException("日志不得读取异常 Message");
        public override string ToString() => throw new InvalidOperationException("日志不得调用异常 ToString");
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        private readonly StringWriter _output = new();
        private readonly Serilog.Core.Logger _logger;
        private readonly bool _candidate;
        private readonly string _mode;
        public Capture(bool candidate, string environment)
        {
            _candidate = candidate;
            _mode = environment;
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate)
            {
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
        public void AssertSingle(string code, string kind, string level = "Error", int? status = null)
        {
            var text = _output.ToString();
            Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(code, text);
            Assert.Contains(level, text);
            Assert.Contains(kind, text);
            foreach (var forbidden in new[] { Secret, PostId.ToString(), ReplyId.ToString(), UserId.ToString(), ReceiverId.ToString(), TenantId.ToString(), "runtime.unclassified", "System.IO.IOException", "System.ArgumentException", "System.InvalidOperationException", "post_quick_reply:", "notification:post-quick-reply", "task:notification", "帖子收到轻回应" })
                Assert.DoesNotContain(forbidden, text);
            if (_candidate)
            {
                using var json = JsonDocument.Parse(text);
                Assert.Equal(_mode, json.RootElement.GetProperty("mode").GetString());
                var properties = json.RootElement.GetProperty("properties");
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
