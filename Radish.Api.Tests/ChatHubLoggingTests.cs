using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Hubs;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Extension.Log;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class ChatHubLoggingTests
{
    private const string Secret = "CHAT_HUB_PRIVATE_SENTINEL";
    private const long UserId = 812345679;
    private const long TenantId = 923456781;
    private const long ChannelId = 734567891;
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task NormalLifecycle_ShouldKeepOrderingTenantGroupsAndTypingPayloadQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        await f.Hub.OnConnectedAsync();
        await f.Hub.JoinChannel(ChannelId);
        await f.Hub.StartTyping(ChannelId);
        await f.Hub.LeaveChannel(ChannelId);
        await f.Hub.OnDisconnectedAsync(null);
        Assert.Equal(new[] { "add-user", "join", "add-channel", "presence-join", "access", "typing", "leave", "remove-channel", "presence-leave", "presence-remove", "remove-user" }, f.Steps);
        Assert.Equal($"channel:{TenantId}:{ChannelId}", ChatHub.BuildChannelGroup(TenantId, ChannelId));
        using var payload = JsonDocument.Parse(Assert.Single(f.Payloads));
        Assert.Equal(ChannelId, payload.RootElement.GetProperty("channelId").GetInt64());
        Assert.Equal(UserId, payload.RootElement.GetProperty("userId").GetInt64());
        Assert.Equal(Secret, payload.RootElement.GetProperty("userName").GetString());
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Presence_ShouldRetainOtherConnectionUntilItsOwnDisconnect(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var presence = new ChatPresenceService();
        using var first = new Fixture(capture.Logger, presence);
        using var second = new Fixture(capture.Logger, presence);
        try
        {
            await first.Hub.JoinChannel(ChannelId);
            await second.Hub.JoinChannel(ChannelId);
            Assert.Equal(new[] { UserId }, presence.GetOnlineUserIds(TenantId, ChannelId));
            await first.Hub.LeaveChannel(ChannelId);
            Assert.Equal(new[] { UserId }, presence.GetOnlineUserIds(TenantId, ChannelId));
            await second.Hub.OnDisconnectedAsync(null);
            Assert.Empty(presence.GetOnlineUserIds(TenantId, ChannelId));
            capture.AssertQuiet();
        }
        finally
        {
            presence.RemoveConnection(first.Connection, UserId);
            presence.RemoveConnection(second.Connection, UserId);
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DisconnectFailure_ShouldLogOnlyAfterPresenceAndGroupCleanup(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var (failure, kind) in new (Exception, string)[]
        {
            (new IOException(Secret), "io"), (new TimeoutException(Secret), "timeout"),
            (new OperationCanceledException(Secret), "cancelled"), (new HubException(Secret), "other")
        })
        {
            using var f = new Fixture(capture.Logger);
            f.Groups.Setup(g => g.RemoveFromGroupAsync(f.Connection, $"user:{UserId}", default))
                .Callback(() => { Assert.Equal(new[] { "presence-remove" }, f.Steps); capture.AssertQuiet(); }).Returns(Task.CompletedTask);
            await f.Hub.OnDisconnectedAsync(failure);
            f.Groups.Verify(g => g.RemoveFromGroupAsync(f.Connection, $"user:{UserId}", default), Times.Once);
            capture.AssertWarning(kind);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DependencyFailures_ShouldPropagateSameExceptionAndStopLaterSteps(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var stage in new[] { "identity", "connect-group", "join", "join-group", "join-presence", "leave", "leave-group", "leave-presence", "disconnect-presence", "disconnect-group", "typing-access", "typing-send" })
        {
            using var f = new Fixture(capture.Logger);
            var failure = new IOException(Secret);
            Func<Task> call;
            switch (stage)
            {
                case "identity": f.Normalizer.Setup(n => n.Normalize(f.Principal, It.IsAny<string?>())).Throws(failure); call = f.Hub.OnConnectedAsync; break;
                case "connect-group": f.Groups.Setup(g => g.AddToGroupAsync(f.Connection, $"user:{UserId}", default)).ThrowsAsync(failure); call = f.Hub.OnConnectedAsync; break;
                case "join": f.Chat.Setup(c => c.JoinChannelAsync(TenantId, UserId, ChannelId)).ThrowsAsync(failure); call = () => f.Hub.JoinChannel(ChannelId); break;
                case "join-group": f.Groups.Setup(g => g.AddToGroupAsync(f.Connection, f.ChannelGroup, default)).ThrowsAsync(failure); call = () => f.Hub.JoinChannel(ChannelId); break;
                case "join-presence": f.Presence.Setup(p => p.JoinChannel(f.Connection, TenantId, ChannelId, UserId)).Throws(failure); call = () => f.Hub.JoinChannel(ChannelId); break;
                case "leave": f.Chat.Setup(c => c.LeaveChannelAsync(TenantId, UserId, ChannelId)).ThrowsAsync(failure); call = () => f.Hub.LeaveChannel(ChannelId); break;
                case "leave-group": f.Groups.Setup(g => g.RemoveFromGroupAsync(f.Connection, f.ChannelGroup, default)).ThrowsAsync(failure); call = () => f.Hub.LeaveChannel(ChannelId); break;
                case "leave-presence": f.Presence.Setup(p => p.LeaveChannel(f.Connection, TenantId, ChannelId, UserId)).Throws(failure); call = () => f.Hub.LeaveChannel(ChannelId); break;
                case "disconnect-presence": f.Presence.Setup(p => p.RemoveConnection(f.Connection, UserId)).Throws(failure); call = () => f.Hub.OnDisconnectedAsync(new TimeoutException(Secret)); break;
                case "disconnect-group": f.Groups.Setup(g => g.RemoveFromGroupAsync(f.Connection, $"user:{UserId}", default)).ThrowsAsync(failure); call = () => f.Hub.OnDisconnectedAsync(new TimeoutException(Secret)); break;
                case "typing-access": f.Access.Setup(a => a.GetAccessAsync(TenantId, UserId, ChannelId, false)).ThrowsAsync(failure); call = () => f.Hub.StartTyping(ChannelId); break;
                default: f.Proxy.Setup(p => p.SendCoreAsync("UserTyping", It.IsAny<object?[]>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure); call = () => f.Hub.StartTyping(ChannelId); break;
            }
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(call));
            capture.AssertQuiet();
            if (stage is "join" or "join-group") f.Presence.VerifyNoOtherCalls();
            if (stage is "leave" or "leave-group") f.Presence.VerifyNoOtherCalls();
            if (stage is "identity" or "disconnect-presence") f.Groups.VerifyNoOtherCalls();
            if (stage == "typing-access") f.Proxy.VerifyNoOtherCalls();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Rejections_ShouldKeepExistingSemanticsWithoutLogsOrPresenceChanges(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        Assert.Equal("频道 Id 无效", (await Assert.ThrowsAsync<HubException>(() => f.Hub.JoinChannel(0))).Message);
        await f.Hub.LeaveChannel(0);
        await f.Hub.StartTyping(0);
        f.Normalizer.VerifyNoOtherCalls();
        var denied = new BusinessException(Secret, 404, "Chat.ChannelUnavailable", "error.chat.channel_unavailable");
        f.Chat.Setup(c => c.JoinChannelAsync(TenantId, UserId, ChannelId)).ThrowsAsync(denied);
        Assert.Same(denied, await Assert.ThrowsAsync<BusinessException>(() => f.Hub.JoinChannel(ChannelId)));
        f.Access.Setup(a => a.GetAccessAsync(TenantId, UserId, ChannelId, false)).ReturnsAsync(ChatChannelAccessResult.Unavailable);
        Assert.Equal("频道不存在或无权发言", (await Assert.ThrowsAsync<HubException>(() => f.Hub.StartTyping(ChannelId))).Message);
        f.Normalizer.Setup(n => n.Normalize(f.Principal, It.IsAny<string?>())).Returns(CurrentUser.Anonymous);
        Assert.Equal("无法获取用户 Id", (await Assert.ThrowsAsync<HubException>(f.Hub.OnConnectedAsync)).Message);
        Assert.Equal("无法获取用户 Id", (await Assert.ThrowsAsync<HubException>(() => f.Hub.OnDisconnectedAsync(new IOException(Secret)))).Message);
        f.Presence.VerifyNoOtherCalls();
        f.Groups.VerifyNoOtherCalls();
        f.Proxy.VerifyNoOtherCalls();
        capture.AssertQuiet();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ILoggerFactory _factory;
        public string Connection { get; } = Secret + Guid.NewGuid().ToString("N");
        public string ChannelGroup => $"channel:{TenantId}:{ChannelId}";
        public ClaimsPrincipal Principal { get; } = new(new ClaimsIdentity([], "Test"));
        public Mock<IClaimsPrincipalNormalizer> Normalizer { get; } = new(MockBehavior.Strict);
        public Mock<IChatService> Chat { get; } = new(MockBehavior.Strict);
        public Mock<IChatChannelAccessService> Access { get; } = new(MockBehavior.Strict);
        public Mock<IChatPresenceService> Presence { get; } = new(MockBehavior.Strict);
        public Mock<IGroupManager> Groups { get; } = new(MockBehavior.Strict);
        public Mock<IClientProxy> Proxy { get; } = new(MockBehavior.Strict);
        public List<string> Steps { get; } = [];
        public List<string> Payloads { get; } = [];
        public ChatHub Hub { get; }
        public Fixture(Serilog.ILogger logger, IChatPresenceService? actualPresence = null)
        {
            _factory = LoggerFactory.Create(b => b.AddSerilog(logger, dispose: false));
            Normalizer.Setup(n => n.Normalize(Principal, It.IsAny<string?>())).Returns(new CurrentUser
            {
                UserId = UserId, TenantId = TenantId, UserName = Secret, IsAuthenticated = true
            });
            var context = new Mock<HubCallerContext>();
            context.SetupGet(c => c.User).Returns(Principal);
            context.SetupGet(c => c.ConnectionId).Returns(Connection);
            context.SetupGet(c => c.Features).Returns(new FeatureCollection());
            Groups.Setup(g => g.AddToGroupAsync(Connection, $"user:{UserId}", default)).Callback(() => Steps.Add("add-user")).Returns(Task.CompletedTask);
            Groups.Setup(g => g.RemoveFromGroupAsync(Connection, $"user:{UserId}", default)).Callback(() => Steps.Add("remove-user")).Returns(Task.CompletedTask);
            Groups.Setup(g => g.AddToGroupAsync(Connection, ChannelGroup, default)).Callback(() => Steps.Add("add-channel")).Returns(Task.CompletedTask);
            Groups.Setup(g => g.RemoveFromGroupAsync(Connection, ChannelGroup, default)).Callback(() => Steps.Add("remove-channel")).Returns(Task.CompletedTask);
            Chat.Setup(c => c.JoinChannelAsync(TenantId, UserId, ChannelId)).Callback(() => Steps.Add("join")).Returns(Task.CompletedTask);
            Chat.Setup(c => c.LeaveChannelAsync(TenantId, UserId, ChannelId)).Callback(() => Steps.Add("leave")).Returns(Task.CompletedTask);
            Presence.Setup(p => p.JoinChannel(Connection, TenantId, ChannelId, UserId)).Callback(() => Steps.Add("presence-join"));
            Presence.Setup(p => p.LeaveChannel(Connection, TenantId, ChannelId, UserId)).Callback(() => Steps.Add("presence-leave"));
            Presence.Setup(p => p.RemoveConnection(Connection, UserId)).Callback(() => Steps.Add("presence-remove"));
            Access.Setup(a => a.GetAccessAsync(TenantId, UserId, ChannelId, false)).Callback(() => Steps.Add("access"))
                .ReturnsAsync(new ChatChannelAccessResult(true, ChannelType.Public, true, true, true, true, false));
            Proxy.Setup(p => p.SendCoreAsync("UserTyping", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((_, args, _) => { Steps.Add("typing"); Payloads.Add(JsonSerializer.Serialize(Assert.Single(args))); })
                .Returns(Task.CompletedTask);
            var clients = new Mock<IHubCallerClients>(MockBehavior.Strict);
            clients.Setup(c => c.OthersInGroup(ChannelGroup)).Returns(Proxy.Object);
            Hub = new ChatHub(Chat.Object, actualPresence ?? Presence.Object, Access.Object, Normalizer.Object, _factory.CreateLogger<ChatHub>())
            {
                Context = context.Object, Groups = Groups.Object, Clients = clients.Object
            };
        }
        public void Dispose() { Hub.Dispose(); _factory.Dispose(); }
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
                    ["RadishLogging:Mode"] = environment,
                    ["RadishLogging:Diagnostics"] = environment == "Development" ? "true" : "false"
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
            Assert.Contains("chat.connection_closed", text);
            Assert.Contains("Warning", text);
            Assert.Contains(kind, text);
            if (_candidateMode != null)
            {
                using var json = JsonDocument.Parse(text);
                Assert.Equal(_candidateMode, json.RootElement.GetProperty("mode").GetString());
            }
            foreach (var forbidden in new[] { Secret, UserId.ToString(), TenantId.ToString(), ChannelId.ToString(), "ConnectionId", "runtime.unclassified", "System.IO.IOException" })
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
