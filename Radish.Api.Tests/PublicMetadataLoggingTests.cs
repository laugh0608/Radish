using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.ErrorHandling;
using Radish.Common.CacheTool;
using Radish.Extension.Log;
using Radish.Gateway.PublicHead;
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
public sealed class PublicMetadataLoggingTests
{
    private const string Secret = "PUBLIC_METADATA_PRIVATE_SENTINEL";
    public static TheoryData<bool, string> OutputModes => new()
    {
        { false, "Production" }, { true, "Production" },
        { false, "Development" }, { true, "Development" }
    };

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task CacheHitsAndMissingRoutes_ShouldStayQuiet(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        f.Cache.Setup(c => c.GetStringAsync(It.IsAny<string>())).ReturnsAsync("{\"voTitle\":\"" + Secret + "\"}");
        Assert.Equal(Secret, (await f.Head.GetDocsSnapshotAsync(Secret, f.BaseUrl))!.VoTitle);
        f.Posts.Verify(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Post, bool>>>()), Times.Never);
        f.Cache.Setup(c => c.GetStringAsync(It.IsAny<string>())).ReturnsAsync("<urlset />");
        Assert.Equal("<urlset />", (await f.SitemapController.GetSection("tags-1.xml") as ContentResult)!.Content);
        Assert.IsType<NotFoundResult>(await f.SitemapController.GetSection(Secret));
        f.Cache.Setup(c => c.GetStringAsync(It.IsAny<string>())).ReturnsAsync((string)null!);
        Assert.Null(await f.Head.GetStaticRouteSnapshotAsync(Secret, f.BaseUrl));
        Assert.IsType<NotFoundResult>(await f.HeadController.GetForumPost(Secret));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task HeadCacheFailures_ShouldKeepGeneratedSnapshotAndTtl(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        foreach (var failure in new[] { "read", "json", "write" })
        {
            f.Cache.Setup(c => c.GetStringAsync(It.IsAny<string>())).ReturnsAsync(failure == "json" ? Secret : null!);
            f.Cache.Setup(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);
            if (failure == "read") f.Cache.Setup(c => c.GetStringAsync(It.IsAny<string>())).ThrowsAsync(new IOException(Secret));
            if (failure == "write") f.Cache.Setup(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>())).ThrowsAsync(new IOException(Secret));
            var snapshot = Assert.IsType<PublicHeadSnapshotVo>(Assert.IsType<OkObjectResult>(await f.HeadController.GetStaticRoute("forum")).Value);
            Assert.Equal(f.BaseUrl + "/forum", snapshot.VoCanonicalUrl);
            capture.AssertSingle(failure == "write" ? "public_head.cache_write_failed" : "public_head.cache_read_failed", "Warning");
            capture.Clear();
        }
        f.Cache.Verify(c => c.SetStringAsync(It.Is<string>(k => k.StartsWith("public-head:static-route:")), It.IsAny<string>(), TimeSpan.FromMinutes(20)), Times.Exactly(3));
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task HeadQueryFailure_ShouldReachApiExactlyOnce(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        f.Posts.Setup(r => r.QueryFirstAsync(It.IsAny<Expression<Func<Post, bool>>>())).ThrowsAsync(new IOException(Secret));
        await InvokeApiAsync(() => f.HeadController.GetForumPost(Secret), capture.Logger, 500);
        capture.AssertSingle("http.failed", "Error");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SitemapCacheFailures_ShouldKeepXmlAndTtl(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        foreach (var read in new[] { true, false })
        {
            var f = new Fixture();
            if (read) f.Cache.Setup(c => c.GetStringAsync(It.IsAny<string>())).ThrowsAsync(new IOException(Secret));
            else f.Cache.Setup(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>())).ThrowsAsync(new IOException(Secret));
            var response = Assert.IsType<ContentResult>(await f.SitemapController.GetSection("static.xml"));
            Assert.Contains(f.BaseUrl + "/discover", response.Content);
            Assert.Equal("application/xml; charset=utf-8", response.ContentType);
            f.Cache.Verify(c => c.SetStringAsync(It.Is<string>(k => k.StartsWith("public-sitemap:section:static:")), It.IsAny<string>(), TimeSpan.FromMinutes(30)), Times.Once);
            capture.AssertSingle(read ? "sitemap.cache_read_failed" : "sitemap.cache_write_failed", "Warning");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SitemapGenerationFailure_ShouldKeepLastSuccessOrEmptyFallback(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        f.Tags.Setup(t => t.QueryIndexableTagPageAsync(1, PublicSitemapService.SectionPageSize)).ReturnsAsync([new Tag(Secret) { Slug = Secret }]);
        var previous = await f.Sitemap.GetSectionXmlAsync("tags", 1, f.BaseUrl);
        Assert.Contains(Secret, previous);
        capture.AssertQuiet();
        f.Tags.Setup(t => t.QueryIndexableTagPageAsync(1, PublicSitemapService.SectionPageSize)).ThrowsAsync(new IOException(Secret));
        Assert.Equal(previous, await f.Sitemap.GetSectionXmlAsync("tags", 1, f.BaseUrl));
        capture.AssertSingle("sitemap.generation_failed", "Error");
        capture.Clear();
        var empty = await f.Sitemap.GetSectionXmlAsync("tags", 1, f.BaseUrl + "/cold");
        Assert.Contains("<urlset", empty);
        Assert.DoesNotContain("<url>", empty);
        capture.AssertSingle("sitemap.generation_failed", "Error");
        f.Cache.Verify(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task SitemapCountFailure_ShouldKeepOtherSectionsAndOnlyWarnAtConsumer(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment);
        var f = new Fixture();
        f.Posts.Setup(r => r.QueryCountAsync(It.IsAny<Expression<Func<Post, bool>>>())).ThrowsAsync(new IOException(Secret));
        f.Tags.Setup(t => t.QueryIndexableTagCountAsync()).ReturnsAsync(PublicSitemapService.SectionPageSize + 1);
        var xml = await f.Sitemap.GetIndexXmlAsync(f.BaseUrl);
        Assert.Contains("/sitemaps/static.xml", xml);
        Assert.Contains("/sitemaps/tags-2.xml", xml);
        Assert.DoesNotContain("/sitemaps/forum-1.xml", xml);
        capture.AssertSingle("sitemap.section_count_failed", "Warning");
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task GatewayUnavailableResponses_ShouldKeepFallbackAndSafeStatus(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment, "gateway");
        using var f = new GatewayFixture(capture.Logger);
        f.Handler.Response = _ => new(HttpStatusCode.NotFound);
        Assert.Null(await f.Client.GetSnapshotAsync("/" + Secret, TestContext.Current.CancellationToken));
        capture.AssertQuiet();
        foreach (var status in new[] { HttpStatusCode.Forbidden, HttpStatusCode.ServiceUnavailable })
        {
            f.Handler.Response = _ => new(status);
            await f.Middleware.InvokeAsync(f.Context(), f.Client);
            Assert.True(f.NextCalled);
            capture.AssertSingle("public_head.snapshot_unavailable", "Warning");
            Assert.Contains(((int)status).ToString(), capture.Output.ToString());
            capture.Clear();
            Assert.Null(await f.Client.GetFrontendIndexHtmlAsync(TestContext.Current.CancellationToken));
            capture.AssertSingle("public_head.html_unavailable", "Warning");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task GatewayTransportFailures_ShouldKeepNullAndSafeClassification(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment, "gateway");
        using var f = new GatewayFixture(capture.Logger);
        foreach (var failure in new Exception[] { new HttpRequestException(Secret), new TaskCanceledException(Secret) })
        {
            f.Handler.Response = _ => throw failure;
            Assert.Null(await f.Client.GetSnapshotAsync("/" + Secret, TestContext.Current.CancellationToken));
            capture.AssertSingle("public_head.snapshot_request_failed", "Warning");
            capture.Clear();
            Assert.Null(await f.Client.GetFrontendIndexHtmlAsync(TestContext.Current.CancellationToken));
            capture.AssertSingle("public_head.html_request_failed", "Warning");
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task GatewaySuccessAndMalformedJson_ShouldPreserveExistingBoundaries(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment, "gateway");
        using var f = new GatewayFixture(capture.Logger);
        var context = f.Context();
        await f.Middleware.InvokeAsync(context, f.Client);
        Assert.False(f.NextCalled);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        Assert.Contains("<title>" + Secret + "</title>", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, f.Handler.Calls);
        await f.Middleware.InvokeAsync(f.Context(), f.Client);
        Assert.Equal(2, f.Handler.Calls);
        f.Handler.Response = _ => new(HttpStatusCode.OK) { Content = new StringContent(Secret) };
        await Assert.ThrowsAsync<JsonException>(() => f.Client.GetSnapshotAsync("/" + Secret, TestContext.Current.CancellationToken));
        capture.AssertQuiet();
    }

    [Theory]
    [MemberData(nameof(OutputModes))]
    public async Task GatewayResponseFailure_ShouldKeepNextFallbackAndSingleWarning(bool candidate, string environment)
    {
        using var capture = new Capture(candidate, environment, "gateway");
        using var f = new GatewayFixture(capture.Logger);
        var context = f.Context();
        context.Response.Body = new FailingStream();
        await f.Middleware.InvokeAsync(context, f.Client);
        Assert.True(f.NextCalled);
        capture.AssertSingle("public_head.injection_failed", "Warning");
    }

    private sealed class Fixture
    {
        public string BaseUrl { get; } = "https://public.test/" + Secret + "/" + Guid.NewGuid().ToString("N");
        public Mock<ICaching> Cache { get; } = new();
        public Mock<IBaseRepository<Post>> Posts { get; } = new();
        public Mock<ITagDiscoveryRepository> Tags { get; } = new();
        public PublicHeadSnapshotService Head { get; }
        public PublicSitemapService Sitemap { get; }
        public PublicHeadSnapshotController HeadController { get; }
        public PublicSitemapController SitemapController { get; }
        public Fixture()
        {
            Cache.Setup(c => c.GetStringAsync(It.IsAny<string>())).ReturnsAsync((string)null!);
            Cache.Setup(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);
            Head = new(Cache.Object, Posts.Object, Mock.Of<IBaseRepository<WikiDocument>>(), Mock.Of<IBaseRepository<Product>>(), Mock.Of<IAttachmentUrlResolver>(), Mock.Of<ITagService>());
            Sitemap = new(Cache.Object, Posts.Object, Mock.Of<IBaseRepository<WikiDocument>>(), Mock.Of<IBaseRepository<Product>>(), Tags.Object);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayService:PublicUrl"] = BaseUrl }).Build();
            HeadController = new(Head, config);
            SitemapController = new(Sitemap, config);
        }
    }

    private sealed class GatewayFixture : IDisposable
    {
        public Handler Handler { get; } = new();
        private readonly HttpClient _http;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly Serilog.Extensions.Logging.SerilogLoggerFactory _factory;
        public PublicHeadSnapshotClient Client { get; }
        public PublicHeadSnapshotMiddleware Middleware { get; }
        public bool NextCalled { get; private set; }
        public GatewayFixture(Serilog.ILogger logger)
        {
            _http = new(Handler);
            _factory = new(logger, dispose: false);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DownstreamServices:ApiService:BaseUrl"] = "https://api.test/" + Secret,
                ["FrontendService:BaseUrl"] = "https://frontend.test/" + Secret
            }).Build();
            Client = new(_http, config, _cache, _factory.CreateLogger<PublicHeadSnapshotClient>());
            Middleware = new(_ => { NextCalled = true; return Task.CompletedTask; }, _cache, _factory.CreateLogger<PublicHeadSnapshotMiddleware>());
        }
        public DefaultHttpContext Context()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = "GET";
            context.Request.Path = "/docs/" + Secret;
            context.Request.Host = new HostString("public.test");
            context.Response.Body = new MemoryStream();
            return context;
        }
        public void Dispose() { _http.Dispose(); _cache.Dispose(); _factory.Dispose(); }
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> Response { get; set; } = request => new(HttpStatusCode.OK)
        {
            Content = new StringContent(request.RequestUri!.Host == "api.test"
                ? "{\"voTitle\":\"" + Secret + "\"}" : "<html><head><title>old</title></head><body></body></html>")
        };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Response(request));
        }
    }
    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException(new IOException(Secret));
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
        context.Request.Path = "/api/public-head/forum/post/" + Secret;
        context.Response.Body = new MemoryStream();
        context.RequestServices = app.Services;
        await ((IApplicationBuilder)app).Build()(context);
        Assert.Equal(status, context.Response.StatusCode);
    }

    private sealed class Capture : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        public StringWriter Output { get; } = new();
        public Serilog.Core.Logger Logger { get; }
        public string[] Lines => Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        public Capture(bool candidate, string environment, string service = "api")
        {
            var config = new LoggerConfiguration().MinimumLevel.Verbose();
            if (candidate) RuntimeLoggingConfiguration.Configure(config, new ConfigurationBuilder().Build(), environment, service, Output, Output);
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
            Assert.DoesNotContain("public-head:", Output.ToString());
            Assert.DoesNotContain("public-sitemap:", Output.ToString());
            Assert.DoesNotContain("https://", Output.ToString());
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
