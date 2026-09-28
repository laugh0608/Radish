using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Common.Exceptions;
using Radish.Extension.Log;
using Radish.IRepository;
using Radish.Model.ViewModels;
using Radish.Service;
using Serilog;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class PublicDiscoverLoggingTests
{
    private const string Secret = "DISCOVER_PRIVATE_SENTINEL";
    private static readonly DateTime Now = new(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task FeedAndCursor_ShouldKeepOrderingPulseAndNoStoreQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        f.Items[0] = [Item(10, PublicDiscoverItemKind.ChannelSummary)];
        f.Items[1] = [Item(20, PublicDiscoverItemKind.MemberActivity), Item(30, PublicDiscoverItemKind.MemberActivity)];
        var first = (await f.Controller.GetFeed(null, 2)).ResponseData!;
        Assert.Equal(new[] { "channelsummary:10", "memberactivity:30" }, first.VoItems.Select(i => i.VoKey));
        Assert.All(first.VoItems, i => Assert.Equal("DISCOVER PRIVATE SENTINEL", i.VoTitle));
        Assert.True(first.VoHasMore);
        Assert.NotNull(first.VoNextCursor);
        Assert.Equal(9, first.VoPulse.VoEligibleItemCount);
        Assert.Equal(4, first.VoPulse.VoDiscoverableChannelCount);
        Assert.Equal(3, first.VoPulse.VoKnowledgeContributionCount);
        Assert.Equal(Now.AddHours(-24), first.VoPulse.VoWindowStartedAtUtc);
        Assert.Equal(Now, first.VoGeneratedAtUtc);
        Assert.Equal("no-store", f.Controller.Response.Headers.CacheControl);
        Assert.All(f.Windows, w => { Assert.Equal(0, w.TenantId); Assert.Equal(3, w.Take); Assert.Equal(Now, w.SnapshotCutoffUtc); });
        f.Windows.Clear();
        var second = (await f.Controller.GetFeed(first.VoNextCursor, 2)).ResponseData!;
        Assert.Equal("memberactivity:20", Assert.Single(second.VoItems).VoKey);
        Assert.False(second.VoHasMore);
        Assert.Null(second.VoNextCursor);
        Assert.All(f.Windows, w => { Assert.Equal(30, w.LastSourceId); Assert.Equal(Now.AddHours(-1), w.LastOccurredAtUtc); });
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task InvalidArguments_ShouldStayQuietAndNeverQuerySources(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        foreach (var (cursor, size, code) in new (string?, int, string)[]
        {
            (null, 0, "PublicDiscover.PageSizeInvalid"), (null, 51, "PublicDiscover.PageSizeInvalid"),
            (Secret, 20, "PublicDiscover.CursorInvalid"), (new string('x', 2049), 20, "PublicDiscover.CursorInvalid")
        })
        {
            using var body = await InvokeApiAsync(() => f.Controller.GetFeed(cursor, size), capture.Logger, 400);
            Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
            capture.AssertQuiet();
        }
        Assert.Equal(0, f.Started);
        f.Repository.VerifyNoOtherCalls();
        f.Channels.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task EverySourceFailure_ShouldKeep503AndExactlyOneFinalEvent(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        for (var source = 0; source < 7; source++)
        {
            var f = new Fixture { FailedSource = source, Failure = new IOException(Secret) };
            using var body = await InvokeApiAsync(() => f.Controller.GetFeed(), capture.Logger, 503);
            Assert.Equal("PublicDiscover.SourceUnavailable", body.RootElement.GetProperty("code").GetString());
            Assert.Equal("error.public_discover.source_unavailable", body.RootElement.GetProperty("messageKey").GetString());
            Assert.DoesNotContain(Secret, body.RootElement.GetRawText());
            Assert.Equal(7, f.Started);
            capture.AssertSingle("http.failed", "Error");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task BusinessExceptions_ShouldPreserveIdentityAndFinalStatusPolicy(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var status in new[] { 400, 409, 503 })
        {
            var failure = new BusinessException(Secret, status, "Discover.Test", "error.discover.test");
            var f = new Fixture { FailedSource = 1, Failure = failure };
            Assert.Same(failure, await Assert.ThrowsAsync<BusinessException>(() => f.Service.GetFeedAsync(null, 20)));
            capture.AssertQuiet();
            using var body = await InvokeApiAsync(() => f.Controller.GetFeed(), capture.Logger, status);
            Assert.Equal("Discover.Test", body.RootElement.GetProperty("code").GetString());
            if (status >= 500) capture.AssertSingle("http.failed", "Error");
            else capture.AssertQuiet();
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task ConcurrentQueries_ShouldAllStartAndWaitBeforeWrappingFailure(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var f = new Fixture { Gate = gate.Task, FailedSource = 0, Failure = new IOException(Secret) };
        var pending = f.Service.GetFeedAsync(null, 20);
        Assert.Equal(7, f.Started);
        Assert.False(pending.IsCompleted);
        gate.SetResult(true);
        var failure = await Assert.ThrowsAsync<BusinessException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal(503, failure.StatusCode);
        Assert.Null(failure.InnerException);
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task EmptyOrIneligibleCandidates_ShouldKeepEmptySuccessQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        f.Items[0] = [Item(0, PublicDiscoverItemKind.ChannelSummary)];
        var future = Item(2, PublicDiscoverItemKind.MemberActivity);
        future.OccurredAtUtc = Now.AddMinutes(1);
        f.Items[1] = [future];
        var first = (await f.Controller.GetFeed()).ResponseData!;
        Assert.Empty(first.VoItems);
        Assert.False(first.VoHasMore);
        Assert.Null(first.VoNextCursor);
        capture.AssertQuiet();
    }

    private static PublicDiscoverSourceProjection Item(long id, PublicDiscoverItemKind kind) => new()
    {
        SourceId = id, Kind = kind, OccurredAtUtc = Now.AddHours(-1), Title = "<b>" + Secret + "</b>",
        TargetKind = kind == PublicDiscoverItemKind.ChannelSummary ? PublicDiscoverTargetKind.Messages : PublicDiscoverTargetKind.Docs,
        ChannelId = 7, DocumentSlug = Secret, RequiresAuthentication = kind == PublicDiscoverItemKind.ChannelSummary
    };
    private sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed class Fixture
    {
        public Mock<IPublicDiscoverRepository> Repository { get; } = new(MockBehavior.Strict);
        public Mock<IPublicDiscoverChannelRepository> Channels { get; } = new(MockBehavior.Strict);
        public PublicDiscoverService Service { get; }
        public PublicDiscoverController Controller { get; }
        public IReadOnlyList<PublicDiscoverSourceProjection>[] Items { get; } = [[], [], [], [], []];
        public List<PublicDiscoverSourceWindow> Windows { get; } = [];
        public int Started { get; private set; }
        public int FailedSource { get; init; } = -1;
        public Exception Failure { get; init; } = new IOException(Secret);
        public Task Gate { get; init; } = Task.CompletedTask;
        public Fixture()
        {
            Channels.Setup(r => r.QueryChannelSummariesAsync(It.IsAny<PublicDiscoverSourceWindow>(), Now.AddHours(-24)))
                .Returns((PublicDiscoverSourceWindow w, DateTime _) => QueryItems(0, w));
            Repository.Setup(r => r.QueryMemberActivitiesAsync(It.IsAny<PublicDiscoverSourceWindow>())).Returns((PublicDiscoverSourceWindow w) => QueryItems(1, w));
            Repository.Setup(r => r.QueryHighlightedCommentsAsync(It.IsAny<PublicDiscoverSourceWindow>())).Returns((PublicDiscoverSourceWindow w) => QueryItems(2, w));
            Repository.Setup(r => r.QueryPostsAsync(It.IsAny<PublicDiscoverSourceWindow>())).Returns((PublicDiscoverSourceWindow w) => QueryItems(3, w));
            Repository.Setup(r => r.QueryQuestionsAsync(It.IsAny<PublicDiscoverSourceWindow>())).Returns((PublicDiscoverSourceWindow w) => QueryItems(4, w));
            Channels.Setup(r => r.QueryPulseAsync(0, Now.AddHours(-24), Now)).Returns(() => Query(5, new PublicDiscoverChannelPulseCounts(4, 2)));
            Repository.Setup(r => r.QueryPulseAsync(0, Now.AddHours(-24), Now)).Returns(() => Query(6, new PublicDiscoverMainPulseCounts(7, 3)));
            Service = new(Repository.Object, Channels.Object, new FixedTime());
            Controller = new(Service) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        }
        private Task<IReadOnlyList<PublicDiscoverSourceProjection>> QueryItems(int source, PublicDiscoverSourceWindow window)
        {
            Windows.Add(window);
            return Query(source, Items[source]);
        }
        private async Task<T> Query<T>(int source, T result)
        {
            Started++;
            await Gate;
            if (source == FailedSource) throw Failure;
            return result;
        }
    }

    private static async Task<JsonDocument> InvokeApiAsync(Func<Task> action, Serilog.ILogger logger, int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var app = builder.Build();
        app.UseApiExceptionHandler();
        app.Run(async _ => await action());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/PublicDiscover/GetFeed";
        context.Request.QueryString = new QueryString("?cursor=" + Secret);
        using var output = new MemoryStream();
        context.Response.Body = output;
        context.RequestServices = app.Services;
        await ((IApplicationBuilder)app).Build()(context);
        Assert.Equal(status, context.Response.StatusCode);
        output.Position = 0;
        return await JsonDocument.ParseAsync(output, cancellationToken: TestContext.Current.CancellationToken);
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        public StringWriter Output { get; } = new();
        public Serilog.Core.Logger Logger { get; }
        public string[] Lines => Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        public Capture(bool candidate, string environment)
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate) RuntimeLoggingConfiguration.Configure(config, new ConfigurationBuilder().Build(), environment, "api", Output, Output);
            else config.Enrich.FromLogContext().WriteTo.Sink(new LegacySink(Output));
            Logger = config.CreateLogger(); Log.Logger = Logger;
        }
        public void AssertQuiet() => Assert.Equal("", Output.ToString());
        public void AssertSingle(string code, string level)
        {
            Assert.Single(Lines);
            Assert.Contains(code, Output.ToString());
            Assert.Contains(level, Output.ToString());
            AssertSafe();
        }
        public void AssertSafe()
        {
            Assert.DoesNotContain(Secret, Output.ToString());
            Assert.DoesNotContain("DISCOVER PRIVATE SENTINEL", Output.ToString());
            Assert.DoesNotContain("runtime.unclassified", Output.ToString());
        }
        public void Clear() => Output.GetStringBuilder().Clear();
        public void Dispose() { Log.Logger = _previous; Logger.Dispose(); Output.Dispose(); }
    }
    private sealed class LegacySink(TextWriter output) : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.Display.MessageTemplateTextFormatter _formatter = new("{Level} {Message:lj} {Properties:j} {Exception}{NewLine}");
        public void Emit(Serilog.Events.LogEvent value) => _formatter.Format(value, output);
    }
}
