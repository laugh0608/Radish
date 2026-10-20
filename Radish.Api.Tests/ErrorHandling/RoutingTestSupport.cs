using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

/// <summary>只构造 DI、ApplicationBuilder 和内存 context；不创建或启动项目宿主。</summary>
internal sealed class RoutingFixture : IDisposable
{
    internal readonly ServiceProvider Services;
    internal readonly RequestDelegate Pipeline;
    private readonly HttpBoundaryCapture _capture;

    internal RoutingFixture(HttpBoundaryCapture capture, Action<IEndpointRouteBuilder> endpoints,
        Action<RouteOptions>? options = null, Action<IApplicationBuilder>? between = null)
    {
        _capture = capture;
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders().SetMinimumLevel(LogLevel.Trace)
            .AddFilter((category, _) => RoutingFrameworkEvents.Sources.Any(s => s.Source == category))
            .AddSerilog(capture.Logger, dispose: false));
        services.AddRouting();
        if (options != null) services.Configure(options);
        services.AddSingleton(new DiagnosticListener("Microsoft.AspNetCore"));
        services.AddSingleton<DiagnosticSource>(provider => provider.GetRequiredService<DiagnosticListener>());
        Services = services.BuildServiceProvider();
        var app = new ApplicationBuilder(Services);
        app.UseRouting();
        between?.Invoke(app);
        app.UseEndpoints(endpoints);
        Pipeline = app.Build();
    }

    internal DefaultHttpContext Context(string path)
    {
        var context = new DefaultHttpContext { RequestServices = Services };
        context.Request.Path = path;
        context.Request.Method = "GET";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("routing.example.invalid");
        context.Request.QueryString = new QueryString("?email=" + HttpBoundaryCapture.Secret);
        context.Response.Body = new MemoryStream();
        return context;
    }

    internal void Contains(params string[] codes)
    {
        if (_capture.Candidate && _capture.Environment == "Production")
            codes = codes.Where(c => c is "routing.pipeline_size_feature_missing" or "routing.pipeline_size_feature_read_only").ToArray();
        foreach (var code in codes) Assert.Contains(code, _capture.Codes);
        if (_capture.Candidate && _capture.Environment == "Production")
            Assert.All(_capture.Codes, code => Assert.Contains(code, codes));
        RoutingFrameworkLoggingTests.AssertSafe(_capture);
    }

    public void Dispose() => Services.Dispose();
}

internal sealed class UnreadableRoutingException : Exception
{
    public override string Message => throw new InvalidOperationException("Unexpected exception message access");
    public override string ToString() => throw new InvalidOperationException("Unexpected exception formatting");
}
