using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Api.Resources;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class UserFollowLoggingTests
{
    private const string Secret = "FOLLOW_PRIVATE_SENTINEL";
    private const long FollowerId = 819234561;
    private const long TargetId = 928345672;
    private const long TenantId = 736451289;
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Follow_ShouldKeepPayloadKeysAndRepeatQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        var before = DateTime.UtcNow;
        var result = await f.Controller.Follow(new() { TargetUserId = TargetId });
        Assert.True(result.IsSuccess);
        Assert.Equal("关注成功", result.MessageInfo);
        Assert.True(Assert.IsType<UserFollowStatusVo>(result.ResponseData).VoIsFollowing);
        var draft = Assert.Single(f.Drafts);
        var notification = JsonSerializer.Deserialize<NotificationRequestedTaskPayload>(draft.PayloadJson)!.Notification;
        Assert.Equal(ReliableOutboxSources.Main, draft.SourceDatabase);
        Assert.Equal(TenantId, draft.TenantId);
        Assert.Equal(ReliableTaskTypes.NotificationRequested, draft.TaskType);
        Assert.Equal(1, draft.SchemaVersion);
        Assert.Equal(6, draft.MaxAttempts);
        Assert.Equal("UserFollow", draft.AggregateType);
        Assert.Equal($"{FollowerId}:{TargetId}", draft.AggregateId);
        Assert.Equal($"task:notification:followed:{notification.NotificationId}", draft.IdempotencyKey);
        Assert.Equal($"notification:followed:follower:{FollowerId}:target:{TargetId}:event:{notification.NotificationId}", notification.BusinessKey);
        Assert.Equal(NotificationType.Followed, notification.Type);
        Assert.Equal((int)NotificationPriority.Normal, notification.Priority);
        Assert.Equal(BusinessType.User, notification.BusinessType);
        Assert.Equal(TargetId, notification.BusinessId);
        Assert.Equal("新增粉丝", notification.Title);
        Assert.Equal($"{Secret} 关注了你", notification.Content);
        Assert.Equal(Secret, notification.TriggerName);
        Assert.Equal(Secret, notification.TemplateArguments!["actorName"]);
        Assert.Equal(FollowerId, notification.TriggerId);
        Assert.Equal(new[] { TargetId }, notification.ReceiverUserIds);
        Assert.Equal(FollowerId, notification.Target!.UserId);
        Assert.Equal(Secret, notification.Target.UserPublicId);
        Assert.Equal(NotificationTargetKind.UserProfile, notification.TargetKind);
        Assert.Equal(TenantId, notification.TenantId);
        Assert.Equal(draft.OccurredAtUtc, notification.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, draft.OccurredAtUtc.Kind);
        Assert.InRange(draft.OccurredAtUtc, before, DateTime.UtcNow);

        var repeat = await f.Controller.Follow(new() { TargetUserId = TargetId });
        Assert.True(repeat.IsSuccess);
        Assert.Equal("已关注该用户", repeat.MessageInfo);
        Assert.Single(f.Drafts);
        f.Outbox.Verify(r => r.AddAsync(It.IsAny<ReliableOutboxDraft>()), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task NotificationFailuresConsumedByController_ShouldKeepResponseAndOneSafeError(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "follower", "avatar", "outbox" })
        foreach (var argument in new[] { false, true })
        {
            using var f = new Fixture(capture.Logger);
            Exception failure = argument ? new ArgumentException(Secret) : new InvalidOperationException(Secret);
            f.FailNotification(stage, failure);
            // Service 保留同一异常实例，且失败不回滚已提交的关注关系。
            Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() => f.Service.FollowAsync(FollowerId, TargetId, TenantId, Secret)));
            Assert.True(f.Followed);
            capture.AssertSingle("user_follow.notification_enqueue_failed", argument ? "argument" : "invalid-operation");
            capture.Clear();

            using var controllerFixture = new Fixture(capture.Logger);
            controllerFixture.FailNotification(stage, failure);
            await InvokeApiAsync(async () =>
            {
                var result = await controllerFixture.Controller.Follow(new() { TargetUserId = TargetId });
                Assert.False(result.IsSuccess);
                Assert.Equal(argument ? 400 : 404, result.StatusCode);
                Assert.Equal(argument ? "UserFollow.FollowRejected" : "UserFollow.TargetUnavailable", result.Code);
                Assert.Equal(argument ? "error.user_follow.follow_rejected" : "error.user_follow.target_unavailable", result.MessageKey);
                Assert.Equal(Secret, result.MessageInfo); // 原响应文案契约保留，本批只约束运行日志。
            }, capture.Logger, 200);
            capture.AssertSingle("user_follow.notification_enqueue_failed", argument ? "argument" : "invalid-operation");
            capture.Clear();
            // 既有关联已存在时不补投通知，也不新增重试。
            Assert.True((await controllerFixture.Controller.Follow(new() { TargetUserId = TargetId })).IsSuccess);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task UnhandledNotificationFailures_ShouldReachOnlyFinalApiBoundary(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "follower", "avatar", "outbox" })
        foreach (var failure in new Exception[] { new IOException(Secret), new TimeoutException(Secret), new OperationCanceledException(Secret), new BusinessException(Secret, 503, "Follow.Test", "error.follow.test") })
        {
            using var f = new Fixture(capture.Logger);
            f.FailNotification(stage, failure);
            Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() => f.Service.FollowAsync(FollowerId, TargetId, TenantId, Secret)));
            capture.AssertQuiet();
            using var controllerFixture = new Fixture(capture.Logger);
            controllerFixture.FailNotification(stage, failure);
            var status = failure is BusinessException ? 503 : 500;
            await InvokeApiAsync(() => controllerFixture.Controller.Follow(new() { TargetUserId = TargetId }), capture.Logger, status);
            capture.AssertSingle("http.failed", failure switch
            {
                IOException => "io", TimeoutException => "timeout", OperationCanceledException => "cancelled", _ => "other"
            });
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ExpectedRejections_ShouldStayQuietAndNotQueue(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        Assert.Equal(400, (await f.Controller.Follow(new() { TargetUserId = 0 })).StatusCode);
        Assert.Equal(400, (await f.Controller.Follow(new() { TargetUserId = FollowerId })).StatusCode);
        f.Users.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync((User?)null);
        Assert.Equal(404, (await f.Controller.Follow(new() { TargetUserId = TargetId })).StatusCode);
        f.Outbox.Verify(r => r.AddAsync(It.IsAny<ReliableOutboxDraft>()), Times.Never);
        using var blocked = new Fixture(capture.Logger);
        blocked.Follows.Setup(r => r.FollowAsync(TenantId, FollowerId, TargetId, Secret, It.IsAny<DateTime>()))
            .ThrowsAsync(new UserFollowInteractionBlockedException());
        await InvokeApiAsync(() => blocked.Controller.Follow(new() { TargetUserId = TargetId }), capture.Logger, 409);
        blocked.Outbox.Verify(r => r.AddAsync(It.IsAny<ReliableOutboxDraft>()), Times.Never);
        using var business = new Fixture(capture.Logger);
        business.FailNotification("outbox", new BusinessException(Secret, 409, "Follow.Test", "error.follow.test"));
        await InvokeApiAsync(() => business.Controller.Follow(new() { TargetUserId = TargetId }), capture.Logger, 409);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task MissingOutbox_ShouldKeepHandledFailureVisible(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger, missingOutbox: true);
        var result = await f.Controller.Follow(new() { TargetUserId = TargetId });
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("可靠 Outbox 服务未注册", result.MessageInfo);
        Assert.True(f.Followed);
        capture.AssertSingle("user_follow.notification_enqueue_failed", "invalid-operation");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ILoggerFactory _factory;
        public Mock<IBaseRepository<User>> Users { get; } = new();
        public Mock<IUserFollowRepository> Follows { get; } = new();
        public Mock<IBaseRepository<Attachment>> Attachments { get; } = new();
        public Mock<IReliableOutboxRepository> Outbox { get; } = new();
        public List<ReliableOutboxDraft> Drafts { get; } = [];
        public bool Followed { get; private set; }
        public UserFollowService Service { get; }
        public UserFollowController Controller { get; }

        public Fixture(Serilog.ILogger logger, bool missingOutbox = false)
        {
            _factory = LoggerFactory.Create(b => b.AddSerilog(logger, dispose: false));
            Users.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync((Expression<Func<User, bool>> p) => new[]
                {
                    new User { Id = FollowerId, TenantId = TenantId, IsEnable = true, UserName = Secret, PublicId = Secret },
                    new User { Id = TargetId, TenantId = TenantId, IsEnable = true, UserName = Secret }
                }.FirstOrDefault(p.Compile()));
            Follows.Setup(r => r.FollowAsync(TenantId, FollowerId, TargetId, Secret, It.IsAny<DateTime>()))
                .ReturnsAsync(() => { var changed = !Followed; Followed = true; return changed; });
            Follows.Setup(r => r.QueryExistsAsync(It.IsAny<Expression<Func<UserFollow, bool>>>())).ReturnsAsync(() => Followed);
            Attachments.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>())).ReturnsAsync([]);
            Outbox.Setup(r => r.AddAsync(It.IsAny<ReliableOutboxDraft>()))
                .Callback<ReliableOutboxDraft>(draft => Drafts.Add(draft)).ReturnsAsync(401L);
            var policy = new Mock<IUserInteractionPolicyService>();
            policy.Setup(p => p.GetSnapshotAsync(TenantId, FollowerId, TargetId))
                .ReturnsAsync(new UserInteractionPolicySnapshot(TargetId, false, false));
            Service = new UserFollowService(Mock.Of<IMapper>(), Follows.Object, Users.Object, Mock.Of<IPostService>(),
                Attachments.Object, Mock.Of<INotificationService>(), _factory.CreateLogger<UserFollowService>(),
                Options.Create(new FeedDistributionOptions()), Mock.Of<IAttachmentUrlResolver>(), policy.Object,
                missingOutbox ? null : new ReliableOutboxService(Outbox.Object));
            var current = new Mock<ICurrentUserAccessor>();
            current.SetupGet(a => a.Current).Returns(new CurrentUser { UserId = FollowerId, TenantId = TenantId, UserName = Secret });
            var localizer = new Mock<IStringLocalizer<Errors>>();
            localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key, resourceNotFound: true));
            Controller = new UserFollowController(Service, current.Object, localizer.Object);
        }

        public void FailNotification(string stage, Exception exception)
        {
            switch (stage)
            {
                case "follower":
                    Users.Setup(r => r.QueryFirstAsync(It.Is<Expression<Func<User, bool>>>(p =>
                        p.Compile()(new User { Id = FollowerId, TenantId = TenantId, IsEnable = true }))))
                        .ThrowsAsync(exception);
                    break;
                case "avatar":
                    Attachments.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>())).ThrowsAsync(exception);
                    break;
                case "outbox":
                    Outbox.Setup(r => r.AddAsync(It.IsAny<ReliableOutboxDraft>())).ThrowsAsync(exception);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }

        public void Dispose() => _factory.Dispose();
    }

    private static async Task InvokeApiAsync(Func<Task> action, Serilog.ILogger logger, int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        app.Run(async _ => await action());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/UserFollow/Follow";
        context.Request.QueryString = new QueryString("?query=" + Secret);
        using var output = new MemoryStream();
        context.Response.Body = output;
        context.RequestServices = app.Services;
        await ((IApplicationBuilder)app).Build()(context);
        Assert.Equal(status, context.Response.StatusCode);
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        private readonly StringWriter _output = new();
        public Serilog.Core.Logger Logger { get; }
        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate)
            {
                var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RadishLogging:Mode"] = environment
                }).Build();
                RuntimeLoggingConfiguration.Configure(config, settings, environment, "api", _output, _output);
            }
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(_output));
            Logger = config.CreateLogger();
            Log.Logger = Logger;
        }
        public void AssertQuiet() => Assert.Equal("", _output.ToString());
        public void AssertSingle(string code, string kind)
        {
            var text = _output.ToString();
            Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(code, text);
            Assert.Contains("Error", text);
            Assert.Contains(kind, text);
            foreach (var forbidden in new[] { Secret, FollowerId.ToString(), TargetId.ToString(), TenantId.ToString(), "runtime.unclassified", "System.IO.IOException", "System.ArgumentException", "System.InvalidOperationException", "notification:followed", "task:notification", "新增粉丝" })
                Assert.DoesNotContain(forbidden, text);
        }
        public void Clear() => _output.GetStringBuilder().Clear();
        public void Dispose() { Log.Logger = _previous; Logger.Dispose(); _output.Dispose(); }
    }

    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
