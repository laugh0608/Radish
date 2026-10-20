using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Hubs;
using Radish.Common.HttpContextTool;
using Radish.Extension.Log;
using Radish.IService;
using Radish.Model.ViewModels;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class NotificationHubLoggingTests
{
    private const string Secret = "HUB_PRIVATE_SENTINEL";
    private const long UserId = 812345679;
    private const long TenantId = 923456781;
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task NormalLifecycle_ShouldKeepGroupAndAuthoritativeEventsQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        await f.Hub.OnConnectedAsync();
        await f.Hub.OnDisconnectedAsync(null);
        Assert.Equal(new[] { "add", "summary", "NotificationInboxChanged", "UnreadCountChanged", "remove" }, f.Steps);
        f.Groups.Verify(g => g.AddToGroupAsync(Secret, $"user:{UserId}", default), Times.Once);
        f.Groups.Verify(g => g.RemoveFromGroupAsync(Secret, $"user:{UserId}", default), Times.Once);
        f.Caller.Verify(c => c.SendCoreAsync("NotificationInboxChanged", It.Is<object?[]>(args =>
            args.Length == 1 && args[0] is NotificationInboxChangedVo &&
            ((NotificationInboxChangedVo)args[0]!).VoRevision == 7 &&
            ((NotificationInboxChangedVo)args[0]!).VoUnreadGroupCount == 2 &&
            ((NotificationInboxChangedVo)args[0]!).VoUnreadOccurrenceCount == 3 &&
            ((NotificationInboxChangedVo)args[0]!).VoReason == "Connected" &&
            !((NotificationInboxChangedVo)args[0]!).VoRealtimePreviewAllowed), It.IsAny<CancellationToken>()), Times.Once);
        f.Caller.Verify(c => c.SendCoreAsync("UnreadCountChanged", It.Is<object?[]>(args =>
            JsonSerializer.Serialize(args[0], (JsonSerializerOptions?)null) == "{\"unreadCount\":2}"), It.IsAny<CancellationToken>()), Times.Once);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DisconnectFailure_ShouldRemoveGroupThenWriteOneSafeWarning(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var (failure, kind) in new (Exception, string)[]
        {
            (new IOException(Secret), "io"), (new TimeoutException(Secret), "timeout"),
            (new OperationCanceledException(Secret), "cancelled"), (new HubException(Secret), "other")
        })
        {
            using var f = new Fixture(capture.Logger);
            failure.Data[Secret] = Secret;
            f.Groups.Setup(g => g.RemoveFromGroupAsync(Secret, $"user:{UserId}", default))
                .Callback(capture.AssertQuiet).Returns(Task.CompletedTask);
            await f.Hub.OnDisconnectedAsync(failure);
            f.Groups.Verify(g => g.RemoveFromGroupAsync(Secret, $"user:{UserId}", default), Times.Once);
            f.Notifications.VerifyNoOtherCalls();
            f.Caller.VerifyNoOtherCalls();
            capture.AssertWarning(kind);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task LifecycleFailures_ShouldPropagateWithoutAddingHubFailureLog(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "identity-connect", "add", "summary", "NotificationInboxChanged", "UnreadCountChanged", "identity-disconnect", "remove" })
        {
            using var f = new Fixture(capture.Logger);
            var failure = new IOException(Secret);
            switch (stage)
            {
                case "identity-connect":
                case "identity-disconnect":
                    f.Normalizer.Setup(n => n.Normalize(f.Principal, It.IsAny<string?>())).Throws(failure); break;
                case "add": f.Groups.Setup(g => g.AddToGroupAsync(Secret, $"user:{UserId}", default)).ThrowsAsync(failure); break;
                case "summary": f.Notifications.Setup(n => n.GetInboxSummaryAsync(TenantId, UserId)).ThrowsAsync(failure); break;
                case "remove": f.Groups.Setup(g => g.RemoveFromGroupAsync(Secret, $"user:{UserId}", default)).ThrowsAsync(failure); break;
                default: f.Caller.Setup(c => c.SendCoreAsync(stage, It.IsAny<object?[]>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure); break;
            }
            Func<Task> action = stage is "identity-disconnect" or "remove"
                ? () => f.Hub.OnDisconnectedAsync(new TimeoutException(Secret))
                : f.Hub.OnConnectedAsync;
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(action));
            capture.AssertQuiet();
            if (stage is "identity-connect" or "identity-disconnect") f.Groups.VerifyNoOtherCalls();
            if (stage is "identity-connect" or "add") f.Notifications.VerifyNoOtherCalls();
            if (stage is not "UnreadCountChanged")
                f.Caller.Verify(c => c.SendCoreAsync("UnreadCountChanged", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task IdentityRejection_ShouldKeepExistingConnectAndDisconnectRules(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using (var missing = new Fixture(capture.Logger))
        {
            missing.Context.SetupGet(c => c.User).Returns((ClaimsPrincipal?)null);
            Assert.Equal("用户未认证", (await Assert.ThrowsAsync<HubException>(missing.Hub.OnConnectedAsync)).Message);
            Assert.Equal("用户未认证", (await Assert.ThrowsAsync<HubException>(() => missing.Hub.OnDisconnectedAsync(new IOException(Secret)))).Message);
            missing.Normalizer.VerifyNoOtherCalls();
            missing.Groups.VerifyNoOtherCalls();
        }
        using var invalid = new Fixture(capture.Logger);
        invalid.Normalizer.Setup(n => n.Normalize(invalid.Principal, It.IsAny<string?>())).Returns(new CurrentUser { UserId = 0 });
        Assert.Equal("无法获取用户 ID", (await Assert.ThrowsAsync<HubException>(invalid.Hub.OnConnectedAsync)).Message);
        await invalid.Hub.OnDisconnectedAsync(null);
        capture.AssertQuiet();
        await invalid.Hub.OnDisconnectedAsync(new IOException(Secret));
        invalid.Groups.VerifyNoOtherCalls();
        capture.AssertWarning("io");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task CredentialSelection_ShouldStayInsideNormalizerAndOutOfLogs(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var (query, header, expected) in new (string?, string?, string?)[]
        {
            (Secret + "_QUERY", "Bearer " + Secret + "_HEADER", Secret + "_QUERY"),
            (null, "bEaReR  " + Secret + "_HEADER  ", Secret + "_HEADER"),
            (" ", Secret + "_RAW", Secret + "_RAW"),
            (null, null, "")
        })
        {
            using var f = new Fixture(capture.Logger);
            var http = new DefaultHttpContext();
            if (query != null) http.Request.QueryString = QueryString.Create("access_token", query);
            if (header != null) http.Request.Headers.Authorization = header;
            var feature = new Mock<IHttpContextFeature>();
            feature.SetupGet(v => v.HttpContext).Returns(http);
            f.Features.Set(feature.Object);
            await f.Hub.OnConnectedAsync();
            await f.Hub.OnDisconnectedAsync(new IOException(Secret));
            f.Normalizer.Verify(n => n.Normalize(f.Principal, expected), Times.Exactly(2));
            capture.AssertWarning("io");
            capture.Clear();
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ILoggerFactory _factory;
        public ClaimsPrincipal Principal { get; } = new(new ClaimsIdentity([], "Test"));
        public FeatureCollection Features { get; } = new();
        public Mock<HubCallerContext> Context { get; } = new();
        public Mock<IGroupManager> Groups { get; } = new(MockBehavior.Strict);
        public Mock<ISingleClientProxy> Caller { get; } = new(MockBehavior.Strict);
        public Mock<INotificationService> Notifications { get; } = new(MockBehavior.Strict);
        public Mock<IClaimsPrincipalNormalizer> Normalizer { get; } = new(MockBehavior.Strict);
        public List<string> Steps { get; } = [];
        public NotificationHub Hub { get; }
        public Fixture(Serilog.ILogger logger)
        {
            _factory = LoggerFactory.Create(b => b.AddSerilog(logger, dispose: false));
            Context.SetupGet(c => c.ConnectionId).Returns(Secret);
            Context.SetupGet(c => c.User).Returns(Principal);
            Context.SetupGet(c => c.Features).Returns(Features);
            Normalizer.Setup(n => n.Normalize(Principal, It.IsAny<string?>()))
                .Returns(new CurrentUser { UserId = UserId, TenantId = TenantId, IsAuthenticated = true, UserName = Secret });
            Groups.Setup(g => g.AddToGroupAsync(Secret, $"user:{UserId}", default)).Callback(() => Steps.Add("add")).Returns(Task.CompletedTask);
            Groups.Setup(g => g.RemoveFromGroupAsync(Secret, $"user:{UserId}", default)).Callback(() => Steps.Add("remove")).Returns(Task.CompletedTask);
            Notifications.Setup(n => n.GetInboxSummaryAsync(TenantId, UserId)).Callback(() => Steps.Add("summary"))
                .ReturnsAsync(new NotificationInboxSummaryVo { VoRevision = 7, VoUnreadGroupCount = 2, VoUnreadOccurrenceCount = 3 });
            Caller.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((method, _, _) => Steps.Add(method)).Returns(Task.CompletedTask);
            var clients = new Mock<IHubCallerClients>();
            clients.SetupGet(c => c.Caller).Returns(Caller.Object);
            Hub = new NotificationHub(Mock.Of<INotificationPushService>(), Notifications.Object, Normalizer.Object, _factory.CreateLogger<NotificationHub>())
            {
                Context = Context.Object, Groups = Groups.Object, Clients = clients.Object
            };
        }
        public void Dispose() { Hub.Dispose(); _factory.Dispose(); }
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
        public void AssertWarning(string kind)
        {
            var text = _output.ToString();
            Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains("notification.connection_closed", text);
            Assert.Contains("Warning", text);
            Assert.Contains(kind, text);
            foreach (var forbidden in new[] { Secret, UserId.ToString(), TenantId.ToString(), "runtime.unclassified", "System.IO.IOException", "ConnectionId", "access_token", "Bearer" })
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
