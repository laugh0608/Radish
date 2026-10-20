using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Hubs;
using Radish.Api.Services;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Repository;
using Radish.Service;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class NotificationCreationLoggingTests
{
    private const string Secret = "NOTIFICATION_PRIVATE_SENTINEL";
    private const long RecipientId = 812345671;
    private const long ActorId = 923456782;
    private const long NotificationId = 734567893;
    private static readonly DateTime Now = new(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc);
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Suppression_ShouldCompleteOutboxWithoutWritesPushesOrLogs(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var blocked in new[] { false, true })
        {
            using var f = new Fixture(capture.Logger);
            if (blocked)
                f.Policy.Setup(p => p.ExcludeInteractionBarriersAsync(9, ActorId, It.IsAny<IReadOnlyCollection<long>>())).ReturnsAsync([]);
            else
                f.Inbox.Setup(r => r.GetPreferencesAsync(9, RecipientId)).ReturnsAsync(new Dictionary<string, NotificationSetting>
                {
                    [NotificationCategory.Relationship] = new() { InAppEnabled = false }
                });
            Assert.Equal(NotificationId, await f.Service.CreateNotificationAsync(CreateDto()));
            await f.EnqueueAsync(CreateDto());
            await f.ExecuteAsync();
            Assert.Equal(ReliableOutboxStatuses.Succeeded, (await f.SnapshotAsync())!.Status);
            f.Inbox.Verify(r => r.PersistAsync(It.IsAny<Notification>(), It.IsAny<IReadOnlyList<NotificationInboxRecipient>>(), It.IsAny<DateTime>()), Times.Never);
            f.Client.VerifyNoOtherCalls();
            if (blocked)
                f.Users.Verify(r => r.GetActiveUserIdsAsync(It.IsAny<long>(), It.IsAny<IReadOnlyCollection<long>>()), Times.Never);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Creation_ShouldKeepAuthoritativePayloadRecipientsAndRevisionPushQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        var dto = CreateDto();
        dto.TenantId = 99;
        dto.OccurredAtUtc = Now.AddDays(-1);
        dto.ReceiverUserIds = [RecipientId, -1, ActorId, RecipientId];
        f.Inbox.Setup(r => r.GetPreferencesAsync(9, ActorId)).ReturnsAsync(new Dictionary<string, NotificationSetting>
        {
            [NotificationCategory.Relationship] = new() { InAppEnabled = false }
        });
        f.Inbox.Setup(r => r.GetPreferencesAsync(9, RecipientId)).ReturnsAsync(new Dictionary<string, NotificationSetting>
        {
            [NotificationCategory.Relationship] = new() { InAppEnabled = true, RealtimePreviewEnabled = false }
        });
        await f.EnqueueAsync(dto);
        await f.ExecuteAsync();
        Assert.Equal(ReliableOutboxStatuses.Succeeded, (await f.SnapshotAsync())!.Status);
        f.Inbox.Verify(r => r.PersistAsync(It.Is<Notification>(n =>
            n.Id == NotificationId && n.TenantId == 9 && n.OccurredAtUtc == Now && n.CreateTime == Now &&
            n.BusinessKey == Secret && n.Title == Secret && n.Content == Secret && n.TriggerName == Secret &&
            n.TriggerId == ActorId && n.Type == NotificationType.Followed && n.Category == NotificationCategory.Relationship &&
            n.TemplateKey == "notification.Followed" && n.TemplateArgumentsJson != null && n.TemplateArgumentsJson.Contains(Secret) &&
            n.TargetKind == NotificationTargetKind.UserProfile && n.TargetDataJson == dto.Target!.ToJson()),
            It.Is<IReadOnlyList<NotificationInboxRecipient>>(recipients => recipients.Count == 1 &&
                recipients[0].UserId == RecipientId && !recipients[0].RealtimePreviewAllowed), Now), Times.Once);
        f.Users.Verify(r => r.GetActiveUserIdsAsync(9, It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new[] { RecipientId, ActorId }))), Times.Once);
        f.Client.Verify(c => c.SendCoreAsync("NotificationInboxChanged", It.Is<object?[]>(args =>
            args.Length == 1 && args[0] is NotificationInboxChangedVo &&
            ((NotificationInboxChangedVo)args[0]!).VoRevision == 3 &&
            ((NotificationInboxChangedVo)args[0]!).VoUnreadGroupCount == 2 &&
            ((NotificationInboxChangedVo)args[0]!).VoUnreadOccurrenceCount == 4 &&
            ((NotificationInboxChangedVo)args[0]!).VoReason == "Created" &&
            !((NotificationInboxChangedVo)args[0]!).VoRealtimePreviewAllowed), It.IsAny<CancellationToken>()), Times.Once);
        f.Client.Verify(c => c.SendCoreAsync("UnreadCountChanged", It.Is<object?[]>(args =>
            JsonSerializer.Serialize(args[0], (JsonSerializerOptions?)null) == "{\"unreadCount\":2}"), It.IsAny<CancellationToken>()), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task PushFailure_ShouldStayBestEffortAndNotRetryPersistedNotification(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "group", "NotificationInboxChanged", "UnreadCountChanged" })
        {
            using var f = new Fixture(capture.Logger);
            if (stage == "group")
                f.Clients.Setup(c => c.Group(It.IsAny<string>())).Throws(new IOException(Secret));
            else
                f.Client.Setup(c => c.SendCoreAsync(stage, It.IsAny<object?[]>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException(Secret));
            await f.EnqueueAsync(CreateDto());
            await f.ExecuteAsync();
            var snapshot = (await f.SnapshotAsync())!;
            Assert.Equal(ReliableOutboxStatuses.Succeeded, snapshot.Status);
            Assert.Equal(0, snapshot.AttemptCount); // Outbox 只在失败落库时累加尝试计数。
            capture.AssertSingle("notification.push_failed", "Warning", "io");
            f.Client.Verify(c => c.SendCoreAsync("UnreadCountChanged", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                stage == "UnreadCountChanged" ? Times.Once() : Times.Never());
            capture.Clear();
            // 已完成 Outbox 不再次处理；收件箱返回幂等命中且无 revision 变化时不补推。
            await f.ExecuteAsync();
            await f.Service.CreateNotificationAsync(CreateDto());
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task CreationFailures_ShouldKeepOutboxRetryAndPermanentFailureOwnership(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "users", "preferences", "persist" })
        foreach (var permanent in new[] { false, true })
        {
            using var f = new Fixture(capture.Logger);
            Exception failure = permanent ? new ArgumentException(Secret) : new IOException(Secret);
            switch (stage)
            {
                case "users": f.Users.Setup(r => r.GetActiveUserIdsAsync(9, It.IsAny<IReadOnlyCollection<long>>())).ThrowsAsync(failure); break;
                case "preferences": f.Inbox.Setup(r => r.GetPreferencesAsync(9, RecipientId)).ThrowsAsync(failure); break;
                case "persist": f.Inbox.Setup(r => r.PersistAsync(It.IsAny<Notification>(), It.IsAny<IReadOnlyList<NotificationInboxRecipient>>(), Now)).ThrowsAsync(failure); break;
            }
            Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() => f.Service.CreateNotificationAsync(CreateDto())));
            capture.AssertQuiet();
            await f.EnqueueAsync(CreateDto());
            await f.ExecuteAsync();
            Assert.Equal(permanent ? ReliableOutboxStatuses.DeadLetter : ReliableOutboxStatuses.Pending, (await f.SnapshotAsync())!.Status);
            capture.AssertSingle(permanent ? "outbox.dead_letter" : "outbox.retrying", permanent ? "Error" : "Warning", permanent ? "other" : "io");
            capture.Clear();
            if (!permanent)
            {
                await f.Repository.ClaimDueAsync("main", 10, Secret, DateTime.UtcNow.AddHours(1), TimeSpan.FromMinutes(5));
                await f.ExecuteAsync();
                Assert.Equal(ReliableOutboxStatuses.DeadLetter, (await f.SnapshotAsync())!.Status);
                capture.AssertSingle("outbox.dead_letter", "Error", "io");
                capture.Clear();
            }
            f.Client.VerifyNoOtherCalls();
        }
    }

    private static CreateNotificationDto CreateDto() => new()
    {
        NotificationId = NotificationId, BusinessKey = Secret, Type = NotificationType.Followed,
        TenantId = 9, OccurredAtUtc = Now, Title = Secret, Content = Secret,
        TriggerId = ActorId, TriggerName = Secret, ReceiverUserIds = [RecipientId],
        TemplateArguments = new Dictionary<string, string?> { ["actorName"] = Secret },
        TargetKind = NotificationTargetKind.UserProfile, Target = new() { UserId = ActorId, UserPublicId = Secret }
    };

    private sealed class Fixture : IDisposable
    {
        private readonly SqlSugarScope _db;
        private readonly ILoggerFactory _factory;
        private readonly ReliableOutboxExecutionJob _job;
        private long _outboxId;
        private bool _persisted;
        public Mock<INotificationInboxRepository> Inbox { get; } = new();
        public Mock<IUserRepository> Users { get; } = new();
        public Mock<IUserInteractionPolicyService> Policy { get; } = new();
        public Mock<IHubClients> Clients { get; } = new();
        public Mock<IClientProxy> Client { get; } = new();
        public NotificationService Service { get; }
        public ReliableOutboxRepository Repository { get; }

        public Fixture(Serilog.ILogger logger)
        {
            _factory = LoggerFactory.Create(b => b.AddSerilog(logger, dispose: false));
            Clients.Setup(c => c.Group($"user:{RecipientId}")).Returns(Client.Object);
            Client.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var hub = new Mock<IHubContext<NotificationHub>>();
            hub.SetupGet(h => h.Clients).Returns(Clients.Object);
            var push = new NotificationPushService(hub.Object, Inbox.Object, _factory.CreateLogger<NotificationPushService>());
            Users.Setup(r => r.GetActiveUserIdsAsync(9, It.IsAny<IReadOnlyCollection<long>>()))
                .ReturnsAsync((long _, IReadOnlyCollection<long> ids) => ids.ToList());
            Policy.Setup(p => p.ExcludeInteractionBarriersAsync(9, ActorId, It.IsAny<IReadOnlyCollection<long>>()))
                .ReturnsAsync((long _, long _, IReadOnlyCollection<long> ids) => ids.ToList());
            Inbox.Setup(r => r.GetPreferencesAsync(9, It.IsAny<long>())).ReturnsAsync(new Dictionary<string, NotificationSetting>());
            Inbox.Setup(r => r.PersistAsync(It.IsAny<Notification>(), It.IsAny<IReadOnlyList<NotificationInboxRecipient>>(), Now))
                .ReturnsAsync((Notification n, IReadOnlyList<NotificationInboxRecipient> recipients, DateTime _) =>
                {
                    if (_persisted) return new NotificationInboxPersistResult(n.Id, false, []);
                    _persisted = true;
                    var changes = recipients.Select(r => new NotificationInboxRecipientChange(r.UserId, 8001,
                        r.RealtimePreviewAllowed, new NotificationInboxSummarySnapshot(3, 2, 4, new Dictionary<string, long>(), Now))).ToList();
                    return new NotificationInboxPersistResult(n.Id, true, changes);
                });
            Service = new NotificationService(Inbox.Object, Users.Object, Mock.Of<INotificationTargetResolver>(), push,
                Policy.Object, new FixedClock());
            var processor = new ReliableTaskProcessor(Mock.Of<ICoinRewardService>(), Mock.Of<ICoinService>(),
                Mock.Of<IExperienceService>(), Service, Mock.Of<IChatAttachmentBindingService>(),
                Mock.Of<IBaseRepository<Post>>(), Mock.Of<IBaseRepository<Comment>>());
            _db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main", ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite,
                IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
            });
            _db.CodeFirst.InitTables<ReliableOutboxMessage>();
            Repository = new ReliableOutboxRepository(_db);
            _job = new ReliableOutboxExecutionJob(new ReliableOutboxService(Repository), processor, Mock.Of<IContentModerationCaseRepository>());
        }
        public async Task EnqueueAsync(CreateNotificationDto dto)
        {
            _outboxId = await Repository.AddAsync(new ReliableOutboxDraft("main", 9, ReliableTaskTypes.NotificationRequested,
                1, Secret, "Notification", NotificationId.ToString(), JsonSerializer.Serialize(new NotificationRequestedTaskPayload(dto)), Now, MaxAttempts: 2));
            var claimed = await Repository.ClaimDueAsync("main", 10, Secret, Now, TimeSpan.FromMinutes(5));
            Assert.Equal(_outboxId, Assert.Single(claimed).Id);
        }
        public Task ExecuteAsync() => _job.ExecuteAsync("main", _outboxId, TestContext.Current.CancellationToken);
        public Task<ReliableOutboxSnapshot?> SnapshotAsync() => Repository.QueryByIdAsync("main", _outboxId);
        public void Dispose() { _db.Dispose(); _factory.Dispose(); }
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
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
        public void AssertSingle(string code, string level, string kind)
        {
            var text = _output.ToString();
            Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains(code, text);
            Assert.Contains(level, text);
            Assert.Contains(kind, text);
            foreach (var forbidden in new[] { Secret, RecipientId.ToString(), ActorId.ToString(), NotificationId.ToString(), "runtime.unclassified", "System.IO.IOException", "System.ArgumentException", "user:", "Revision" })
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
