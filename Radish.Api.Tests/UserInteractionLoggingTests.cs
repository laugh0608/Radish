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
using Radish.Model.ViewModels;
using Radish.Repository;
using Radish.Service;
using Serilog;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class UserInteractionLoggingTests
{
    private const string Secret = "INTERACTION_PRIVATE_SENTINEL";
    private const long FirstUser = 812345679;
    private const long SecondUser = 923456781;
    private const long Version = 9007199254740993;
    private static readonly DateTime OccurredAt = new(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc);
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ValidDistinctRecipients_ShouldReceiveOnlyStringVersionQuietly(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var (first, second, expected) in new (long, long, long[])[]
        {
            (-1, 0, []), (FirstUser, FirstUser, [FirstUser]),
            (FirstUser, SecondUser, [FirstUser, SecondUser]), (0, SecondUser, [SecondUser])
        })
        {
            using var f = new Fixture(capture.Logger);
            await f.Notifier.NotifyRelationshipChangedAsync(first, second, Version);
            Assert.Equal(expected.SelectMany(id => new[] { $"chat:user:{id}", $"notification:user:{id}" }), f.Attempts);
            Assert.Equal(expected.Length * 2, f.Payloads.Count);
            Assert.All(f.Payloads, payload => Assert.Equal("{\"VoRelationshipVersion\":\"9007199254740993\"}", payload));
            capture.AssertEvents();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SingleDeliveryFailure_ShouldKeepOtherHubAndRecipientAttempts(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var hub in new[] { "chat", "notification" })
        foreach (var user in new[] { FirstUser, SecondUser })
        foreach (var groupFailure in new[] { false, true })
        {
            using var f = new Fixture(capture.Logger);
            f.FailedHub = hub;
            f.FailedUser = user;
            f.FailGroup = groupFailure;
            await f.Notifier.NotifyRelationshipChangedAsync(FirstUser, SecondUser, Version);
            Assert.Equal(new[] { $"chat:user:{FirstUser}", $"notification:user:{FirstUser}", $"chat:user:{SecondUser}", $"notification:user:{SecondUser}" }, f.Attempts);
            Assert.Equal(groupFailure ? 3 : 4, f.Payloads.Count);
            capture.AssertEvents(($"user_interaction.{hub}_push_failed", "Warning", "io"));
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task AllPushesFailing_ShouldStillCompleteBlockedAndUnblockedTasks(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var eventType in new[] { UserBlockRelationshipEventTypes.Blocked, UserBlockRelationshipEventTypes.Unblocked })
        {
            using var f = new Fixture(capture.Logger) { FailAll = true };
            var result = await f.RunTaskAsync(eventType);
            Assert.Equal(ReliableOutboxStatuses.Succeeded, result.Status);
            Assert.Equal(0, result.AttemptCount);
            Assert.Equal(4, f.Attempts.Count);
            Assert.Equal(eventType == UserBlockRelationshipEventTypes.Blocked ? 2 : 0, f.SuppressionCalls.Count);
            if (eventType == UserBlockRelationshipEventTypes.Blocked)
                Assert.Equal(new[] { (FirstUser, SecondUser), (SecondUser, FirstUser) }, f.SuppressionCalls);
            capture.AssertEvents(
                ("user_interaction.chat_push_failed", "Warning", "io"),
                ("user_interaction.notification_push_failed", "Warning", "io"),
                ("user_interaction.chat_push_failed", "Warning", "io"),
                ("user_interaction.notification_push_failed", "Warning", "io"));
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task PrecedingTaskFailures_ShouldRetainOutboxOwnershipWithoutPushEvents(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var invalidEvent in new[] { false, true })
        {
            using var f = new Fixture(capture.Logger);
            if (!invalidEvent)
                f.Inbox.Setup(r => r.SuppressBlockedActorsAsync(9, FirstUser, It.IsAny<IReadOnlyCollection<long>>(), OccurredAt))
                    .ThrowsAsync(new IOException(Secret));
            var result = await f.RunTaskAsync(invalidEvent ? Secret : UserBlockRelationshipEventTypes.Blocked);
            Assert.Equal(invalidEvent ? ReliableOutboxStatuses.DeadLetter : ReliableOutboxStatuses.Pending, result.Status);
            Assert.Equal(1, result.AttemptCount);
            Assert.Empty(f.Attempts);
            capture.AssertEvents((invalidEvent ? "outbox.dead_letter" : "outbox.retrying", invalidEvent ? "Error" : "Warning", invalidEvent ? "other" : "io"));
            capture.Clear();
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ILoggerFactory _factory;
        public UserInteractionRealtimeNotifier Notifier { get; }
        public Mock<INotificationInboxRepository> Inbox { get; } = new();
        public List<(long Recipient, long Actor)> SuppressionCalls { get; } = [];
        public List<string> Attempts { get; } = [];
        public List<string> Payloads { get; } = [];
        public string? FailedHub { get; set; }
        public long FailedUser { get; set; }
        public bool FailGroup { get; set; }
        public bool FailAll { get; set; }
        public Fixture(Serilog.ILogger logger)
        {
            _factory = LoggerFactory.Create(b => b.AddSerilog(logger, dispose: false));
            var chat = new Mock<IHubContext<ChatHub>>();
            chat.SetupGet(h => h.Clients).Returns(CreateClients("chat"));
            var notification = new Mock<IHubContext<NotificationHub>>();
            notification.SetupGet(h => h.Clients).Returns(CreateClients("notification"));
            Notifier = new UserInteractionRealtimeNotifier(chat.Object, notification.Object, _factory.CreateLogger<UserInteractionRealtimeNotifier>());
            Inbox.Setup(r => r.SuppressBlockedActorsAsync(9, It.IsAny<long>(), It.IsAny<IReadOnlyCollection<long>>(), OccurredAt))
                .Callback<long, long, IReadOnlyCollection<long>, DateTime>((_, recipient, actors, _) => SuppressionCalls.Add((recipient, Assert.Single(actors))))
                .ReturnsAsync(new NotificationInboxSuppressionResult(0, []));
        }
        private IHubClients CreateClients(string hub)
        {
            var clients = new Mock<IHubClients>(MockBehavior.Strict);
            foreach (var user in new[] { FirstUser, SecondUser })
            {
                var proxy = new Mock<IClientProxy>(MockBehavior.Strict);
                proxy.Setup(c => c.SendCoreAsync("UserInteractionChanged", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                    .Returns((string _, object?[] args, CancellationToken _) =>
                    {
                        Payloads.Add(JsonSerializer.Serialize(Assert.IsType<UserInteractionChangedVo>(Assert.Single(args))));
                        return FailAll || (FailedHub == hub && FailedUser == user && !FailGroup)
                            ? Task.FromException(new IOException(Secret)) : Task.CompletedTask;
                    });
                clients.Setup(c => c.Group($"user:{user}"))
                    .Returns(() =>
                    {
                        Attempts.Add($"{hub}:user:{user}");
                        if (FailedHub == hub && FailedUser == user && FailGroup) throw new IOException(Secret);
                        return proxy.Object;
                    });
            }
            return clients.Object;
        }
        public async Task<ReliableOutboxSnapshot> RunTaskAsync(string eventType)
        {
            using var db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main", ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite,
                IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute
            });
            db.CodeFirst.InitTables<ReliableOutboxMessage>();
            var repository = new ReliableOutboxRepository(db);
            var processor = new ReliableTaskProcessor(Mock.Of<ICoinRewardService>(), Mock.Of<ICoinService>(), Mock.Of<IExperienceService>(),
                Mock.Of<INotificationService>(), Mock.Of<IChatAttachmentBindingService>(), Mock.Of<IBaseRepository<Post>>(), Mock.Of<IBaseRepository<Comment>>(),
                notificationInboxRepository: Inbox.Object, notificationPushService: Mock.Of<INotificationPushService>(), userInteractionRealtimeNotifier: Notifier);
            var payload = new UserBlockRelationshipChangedTaskPayload(9, 7001, eventType, FirstUser, SecondUser, Version, OccurredAt);
            var id = await repository.AddAsync(new ReliableOutboxDraft("main", 9, ReliableTaskTypes.UserBlockRelationshipChanged, 1,
                Secret, "UserBlock", Secret, JsonSerializer.Serialize(payload), OccurredAt));
            Assert.Equal(id, Assert.Single(await repository.ClaimDueAsync("main", 10, Secret, OccurredAt, TimeSpan.FromMinutes(5))).Id);
            var job = new ReliableOutboxExecutionJob(new ReliableOutboxService(repository), processor, Mock.Of<IContentModerationCaseRepository>());
            await job.ExecuteAsync("main", id, TestContext.Current.CancellationToken);
            return (await repository.QueryByIdAsync("main", id))!;
        }
        public void Dispose() => _factory.Dispose();
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        private readonly StringWriter _output = new();
        private readonly string? _candidateMode;
        public Serilog.Core.Logger Logger { get; }
        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate)
            {
                _candidateMode = environment;
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
        public void AssertEvents(params (string Code, string Level, string Kind)[] events)
        {
            var text = _output.ToString();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(events.Length, lines.Length);
            for (var i = 0; i < events.Length; i++)
            {
                Assert.Contains(events[i].Code, lines[i]);
                Assert.Contains(events[i].Level, lines[i]);
                Assert.Contains(events[i].Kind, lines[i]);
                if (_candidateMode != null)
                {
                    using var json = JsonDocument.Parse(lines[i]);
                    Assert.Equal(_candidateMode, json.RootElement.GetProperty("mode").GetString());
                }
            }
            foreach (var forbidden in new[] { Secret, FirstUser.ToString(), SecondUser.ToString(), Version.ToString(), "RelationshipVersion", "user:", "System.IO.IOException", "runtime.unclassified" })
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
