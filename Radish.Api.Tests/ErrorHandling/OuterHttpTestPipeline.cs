using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radish.Api.ErrorHandling;
using Serilog;

namespace Radish.Api.Tests.ErrorHandling;

/// <summary>通过当前框架的宿主组装器包含自动中间件，不调用 WebApplication.Start / Run。</summary>
internal sealed class OuterHttpTestPipeline : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly DiagnosticListener _diagnostic = new("Radish.Tests.OuterHttp");
    private readonly ActivitySource _activity = new("Radish.Tests.OuterHttp");
    public readonly RequestDelegate Pipeline;
    public readonly HostingApplicationBridge Application;
    public IServiceProvider Services => _app.Services;

    public OuterHttpTestPipeline(HttpBoundaryCapture capture, RequestDelegate? action,
        bool api = false, bool explicitDeveloperPage = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = capture.Environment });
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(capture.Logger, dispose: false);
        builder.Services.AddSingleton<ApiExceptionHandler>();
        _app = builder.Build();
        if (explicitDeveloperPage) _app.UseDeveloperExceptionPage();
        if (api) _app.UseApiExceptionHandler();
        if (action != null) _app.Run(action);
        var outer = new ApplicationBuilder(_app.Services);
        var context = new WebHostBuilderContext { HostingEnvironment = builder.Environment, Configuration = builder.Configuration };
        // 绑定框架版本；直接 Build 应用会漏掉 Development 的自动开发异常页。
        typeof(WebApplicationBuilder).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(m => m.Name == "ConfigureApplication" && m.GetParameters().Length == 2)
            .Invoke(builder, new object[] { context, outer });
        Pipeline = outer.Build();
        Application = new HostingApplicationBridge(Pipeline, _app.Services, capture.Factory, _diagnostic, _activity);
    }

    public async Task<Exception?> Invoke(DefaultHttpContext request)
    {
        request.RequestServices = Services;
        var context = Application.CreateContext(request.Features);
        Exception? failure = null;
        try { await Application.ProcessRequestAsync(context); }
        catch (Exception exception) { failure = exception; }
        finally { Application.DisposeContext(context, failure); }
        return failure;
    }

    public static DefaultHttpContext Request(Stream body, MutableResponseFeature? response = null)
    {
        var request = new DefaultHttpContext();
        if (response != null) request.Features.Set<IHttpResponseFeature>(response);
        request.Request.Method = "GET";
        request.Request.Protocol = "HTTP/1.1";
        request.Request.Scheme = "http";
        request.Request.Host = new HostString("example.test");
        request.Request.Path = "/api/" + HttpBoundaryCapture.Secret;
        request.Request.QueryString = new QueryString("?credential=" + HttpBoundaryCapture.Secret);
        request.Request.Headers["Authorization"] = "Bearer " + HttpBoundaryCapture.Secret;
        request.Response.Body = body;
        return request;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        _diagnostic.Dispose();
        _activity.Dispose();
    }
}

/// <summary>桥接真实 HostingApplication 的内部 Context 类型，保留原异常而不是反射包装。</summary>
internal sealed class HostingApplicationBridge : IHttpApplication<object>
{
    private readonly object _inner;
    private readonly MethodInfo _create;
    private readonly MethodInfo _process;
    private readonly MethodInfo _dispose;
    public Exception? DisposedException { get; private set; }
    public int DisposeCount { get; private set; }

    public HostingApplicationBridge(RequestDelegate pipeline, IServiceProvider services, ILoggerFactory factory,
        DiagnosticListener diagnostic, ActivitySource activity)
    {
        var assembly = Assembly.Load("Microsoft.AspNetCore.Hosting");
        var type = assembly.GetType("Microsoft.AspNetCore.Hosting.HostingApplication", true)!;
        var eventSource = assembly.GetType("Microsoft.AspNetCore.Hosting.HostingEventSource", true)!
            .GetField("Log", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
        var metricsType = assembly.GetType("Microsoft.AspNetCore.Hosting.HostingMetrics", true)!;
        _inner = Activator.CreateInstance(type, new object[]
        {
            pipeline, factory.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics"), diagnostic, activity,
            DistributedContextPropagator.Current, services.GetRequiredService<IHttpContextFactory>(), eventSource,
            services.GetRequiredService(metricsType)
        })!;
        _create = type.GetMethod("CreateContext")!;
        _process = type.GetMethod("ProcessRequestAsync")!;
        _dispose = type.GetMethod("DisposeContext")!;
    }

    public object CreateContext(IFeatureCollection features) => Invoke(_create, features)!;
    public Task ProcessRequestAsync(object context) => (Task)Invoke(_process, context)!;
    public void DisposeContext(object context, Exception? exception)
    {
        DisposedException = exception;
        DisposeCount++;
        Invoke(_dispose, context, exception);
    }
    private object? Invoke(MethodInfo method, params object?[] arguments)
    {
        try { return method.Invoke(_inner, arguments); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
