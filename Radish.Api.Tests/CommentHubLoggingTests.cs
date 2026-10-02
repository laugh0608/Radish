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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Hubs;
using Radish.Common.HttpContextTool;
using Radish.Extension.Log;
using Radish.Model.ViewModels;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class CommentHubLoggingTests
{
    private const string Secret = "COMMENT_HUB_PRIVATE_SENTINEL";
    private const long PostId = 812345679;
    private const long UserId = 923456781;
    private const long CommentId = 734567891;
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task Subscriptions_ShouldAwaitGroupOperationsWithoutIdentityLookupOrLogs(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        foreach (var joining in new[] { true, false })
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (joining) f.Groups.Setup(g => g.AddToGroupAsync(Secret, f.PostGroup, default)).Returns(completion.Task);
            else f.Groups.Setup(g => g.RemoveFromGroupAsync(Secret, f.PostGroup, default)).Returns(completion.Task);
            var pending = joining ? f.Hub.JoinPost(PostId) : f.Hub.LeavePost(PostId);
            Assert.False(pending.IsCompleted);
            completion.SetResult();
            await pending;
            capture.AssertQuiet();
        }
        f.Groups.Verify(g => g.AddToGroupAsync(Secret, f.PostGroup, default), Times.Once);
        f.Groups.Verify(g => g.RemoveFromGroupAsync(Secret, f.PostGroup, default), Times.Once);
        f.Groups.VerifyNoOtherCalls();
        f.Normalizer.VerifyNoOtherCalls();
        f.Clients.VerifyNoOtherCalls();
        Assert.Equal($"post-comments:{PostId}", f.PostGroup);
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task InvalidPostsAndUnauthenticatedTyping_ShouldKeepExistingShortCircuits(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        using var f = new Fixture(capture.Logger);
        foreach (var post in new long[] { 0, -1, long.MinValue })
        {
            Assert.Equal("帖子 Id 无效", (await Assert.ThrowsAsync<HubException>(() => f.Hub.JoinPost(post))).Message);
            await f.Hub.LeavePost(post);
            await f.Hub.StartTyping(post, CommentId);
        }
        f.Normalizer.VerifyNoOtherCalls();
        foreach (var (authenticated, userId) in new[] { (false, 0L), (false, UserId), (true, 0L), (true, -1L) })
        {
            f.User = new CurrentUser { IsAuthenticated = authenticated, UserId = userId, UserName = Secret };
            await f.Hub.StartTyping(PostId);
        }
        f.Normalizer.Verify(n => n.Normalize(f.Principal, null), Times.Exactly(4));
        f.Groups.VerifyNoOtherCalls();
        f.Clients.VerifyNoOtherCalls();
        f.Proxy.VerifyNoOtherCalls();
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task AuthenticatedTyping_ShouldKeepRecipientPayloadAndNameFallbackQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var name in new[] { Secret, "", "  " })
        foreach (var commentId in new long?[] { null, CommentId, 0, -1 })
        {
            using var f = new Fixture(capture.Logger) { User = new CurrentUser { IsAuthenticated = true, UserId = UserId, UserName = name } };
            var before = DateTime.UtcNow;
            await f.Hub.StartTyping(PostId, commentId);
            f.Normalizer.Verify(n => n.Normalize(f.Principal, null), Times.Once);
            f.Clients.Verify(c => c.OthersInGroup(f.PostGroup), Times.Once);
            f.Proxy.Verify(p => p.SendCoreAsync("CommentTyping", It.IsAny<object?[]>(), default), Times.Once);
            f.Clients.VerifyNoOtherCalls();
            f.Groups.VerifyNoOtherCalls();
            var payload = Assert.Single(f.Payloads);
            Assert.Equal(PostId, payload.VoPostId);
            Assert.Equal(commentId, payload.VoCommentId);
            Assert.Equal(UserId, payload.VoUserId);
            Assert.Equal(string.IsNullOrWhiteSpace(name) ? "Unknown" : name, payload.VoUserName);
            Assert.Equal(DateTimeKind.Utc, payload.VoEventTime.Kind);
            Assert.InRange(payload.VoEventTime, before, DateTime.UtcNow);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task CredentialSelection_ShouldKeepQueryPrecedenceAndHeaderSemanticsWithoutLogs(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var (query, header, expected) in new (string?, string?, string?)[]
        {
            (Secret + "_QUERY", "Bearer " + Secret + "_HEADER", Secret + "_QUERY"),
            ("  " + Secret + "  ", null, "  " + Secret + "  "),
            (null, "bEaReR  " + Secret + "_HEADER  ", Secret + "_HEADER"),
            (" ", Secret + "_RAW", Secret + "_RAW"),
            (null, "Bearer   ", ""), (null, " ", null), (null, null, null)
        })
        {
            using var f = new Fixture(capture.Logger);
            var http = new DefaultHttpContext();
            if (query != null) http.Request.QueryString = QueryString.Create("access_token", query);
            if (header != null) http.Request.Headers.Authorization = header;
            var feature = new Mock<IHttpContextFeature>();
            feature.SetupGet(h => h.HttpContext).Returns(http);
            f.Features.Set(feature.Object);
            await f.Hub.StartTyping(PostId);
            f.Normalizer.Verify(n => n.Normalize(f.Principal, expected), Times.Once);
            Assert.Single(f.Payloads);
            capture.AssertQuiet();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task DependencyFailures_ShouldPropagateSameExceptionWithoutLocalLoggingOrRetry(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var failure in new Exception[] { new IOException(Secret), new OperationCanceledException(Secret) })
        foreach (var stage in new[] { "join", "leave", "identity", "recipients", "send" })
        {
            using var f = new Fixture(capture.Logger);
            Func<Task> call = () => f.Hub.StartTyping(PostId);
            switch (stage)
            {
                case "join": f.Groups.Setup(g => g.AddToGroupAsync(Secret, f.PostGroup, default)).ThrowsAsync(failure); call = () => f.Hub.JoinPost(PostId); break;
                case "leave": f.Groups.Setup(g => g.RemoveFromGroupAsync(Secret, f.PostGroup, default)).ThrowsAsync(failure); call = () => f.Hub.LeavePost(PostId); break;
                case "identity": f.Normalizer.Setup(n => n.Normalize(f.Principal, null)).Throws(failure); break;
                case "recipients": f.Clients.Setup(c => c.OthersInGroup(f.PostGroup)).Throws(failure); break;
                default: f.Proxy.Setup(p => p.SendCoreAsync("CommentTyping", It.IsAny<object?[]>(), default)).ThrowsAsync(failure); break;
            }
            Assert.Same(failure, await Record.ExceptionAsync(call));
            Assert.Empty(f.Payloads);
            if (stage == "join") f.Groups.Verify(g => g.AddToGroupAsync(Secret, f.PostGroup, default), Times.Once);
            if (stage == "leave") f.Groups.Verify(g => g.RemoveFromGroupAsync(Secret, f.PostGroup, default), Times.Once);
            if (stage is "join" or "leave") f.Normalizer.VerifyNoOtherCalls();
            else f.Normalizer.Verify(n => n.Normalize(f.Principal, null), Times.Once);
            if (stage is "join" or "leave" or "identity") f.Clients.VerifyNoOtherCalls();
            else f.Clients.Verify(c => c.OthersInGroup(f.PostGroup), Times.Once);
            if (stage == "send") f.Proxy.Verify(p => p.SendCoreAsync("CommentTyping", It.IsAny<object?[]>(), default), Times.Once);
            else f.Proxy.VerifyNoOtherCalls();
            capture.AssertQuiet();
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _provider;
        public ClaimsPrincipal Principal { get; } = new(new ClaimsIdentity());
        public FeatureCollection Features { get; } = new();
        public CurrentUser User { get; set; } = new() { IsAuthenticated = true, UserId = UserId, UserName = Secret };
        public Mock<IClaimsPrincipalNormalizer> Normalizer { get; } = new(MockBehavior.Strict);
        public Mock<IGroupManager> Groups { get; } = new(MockBehavior.Strict);
        public Mock<IHubCallerClients> Clients { get; } = new(MockBehavior.Strict);
        public Mock<IClientProxy> Proxy { get; } = new(MockBehavior.Strict);
        public List<CommentTypingEventVo> Payloads { get; } = [];
        public string PostGroup => CommentHub.BuildPostGroup(PostId);
        public CommentHub Hub { get; }
        public Fixture(Serilog.ILogger logger)
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddSerilog(logger, dispose: false));
            services.AddSingleton(Normalizer.Object);
            _provider = services.BuildServiceProvider();
            // Resolve constructor dependencies as the host does; a reintroduced ILogger is captured too.
            Hub = ActivatorUtilities.CreateInstance<CommentHub>(_provider);
            var context = new Mock<HubCallerContext>();
            context.SetupGet(c => c.User).Returns(Principal);
            context.SetupGet(c => c.ConnectionId).Returns(Secret);
            context.SetupGet(c => c.Features).Returns(Features);
            Hub.Context = context.Object;
            Hub.Groups = Groups.Object;
            Hub.Clients = Clients.Object;
            Normalizer.Setup(n => n.Normalize(Principal, It.IsAny<string?>())).Returns(() => User);
            Groups.Setup(g => g.AddToGroupAsync(Secret, PostGroup, default)).Returns(Task.CompletedTask);
            Groups.Setup(g => g.RemoveFromGroupAsync(Secret, PostGroup, default)).Returns(Task.CompletedTask);
            Clients.Setup(c => c.OthersInGroup(PostGroup)).Returns(Proxy.Object);
            Proxy.Setup(p => p.SendCoreAsync("CommentTyping", It.IsAny<object?[]>(), default))
                .Callback<string, object?[], CancellationToken>((_, args, token) =>
                {
                    Assert.Equal(CancellationToken.None, token);
                    Payloads.Add(Assert.IsType<CommentTypingEventVo>(Assert.Single(args)));
                }).Returns(Task.CompletedTask);
        }
        public void Dispose() { Hub.Dispose(); _provider.Dispose(); }
    }

    private sealed class Capture : IDisposable
    {
        private readonly StringWriter _output = new();
        public Serilog.Core.Logger Logger { get; }
        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate)
            {
                var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RadishLogging:Mode"] = environment,
                    ["RadishLogging:Diagnostics"] = environment == "Development" ? "true" : "false"
                }).Build();
                RuntimeLoggingConfiguration.Configure(config, settings, environment, "api", _output, _output);
            }
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(_output));
            Logger = config.CreateLogger();
            Logger.ForContext("EventCode", "runtime.started").Information("Capture readiness probe");
            Assert.NotEmpty(_output.ToString());
            if (candidate)
            {
                using var json = JsonDocument.Parse(_output.ToString());
                Assert.Equal(environment, json.RootElement.GetProperty("mode").GetString());
            }
            _output.GetStringBuilder().Clear();
        }
        public void AssertQuiet() => Assert.Equal("", _output.ToString());
        public void Dispose() { Logger.Dispose(); _output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
