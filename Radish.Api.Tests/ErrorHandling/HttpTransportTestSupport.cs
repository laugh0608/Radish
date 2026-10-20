using System;
using System.IO;
using System.Linq;

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

// 只解析已安装框架的内部测试入口。无 Host.Start、TestServer、socket 或后台扫描循环。
internal sealed class HttpTransportFixture : IDisposable
{
    private const string Prefix = "Microsoft.AspNetCore.Http.Connections.Internal.";
    internal const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly ServiceProvider _services;
    public readonly object Manager;
    public readonly object Dispatcher;
    public readonly HttpBoundaryCapture Capture;

    public HttpTransportFixture(HttpBoundaryCapture capture)
    {
        Capture = capture;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddConnections();
        services.AddSingleton(capture.Factory);
        services.AddSingleton<IHostApplicationLifetime>(new InactiveLifetime());
        _services = services.BuildServiceProvider();
        Manager = _services.GetRequiredService(Type("HttpConnectionManager"));
        Dispatcher = _services.GetRequiredService(Type("HttpConnectionDispatcher"));
    }

    public static Type Type(string suffix) => Assembly.Load("Microsoft.AspNetCore.Http.Connections").GetType(Prefix + suffix, true)!;
    public static object New(string suffix, params object?[] args) => Activator.CreateInstance(Type(suffix), args)!;
    public object CreateConnection(HttpConnectionDispatcherOptions? options = null) =>
        Call(Manager, "CreateConnection", options ?? new HttpConnectionDispatcherOptions(), 1, false)!;
    public static T Get<T>(object target, string property) => (T)target.GetType().GetProperty(property, Members)!.GetValue(target)!;
    public static void Set(object target, string property, object? value) => target.GetType().GetProperty(property, Members)!.SetValue(target, value);
    public static object? Call(object target, string method, params object?[] args)
    {
        var methodInfo = target.GetType().GetMethods(Members).Single(m => m.Name == method && m.GetParameters().Length == args.Length);
        try { return methodInfo.Invoke(target, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
    public static async Task Run(object target, string method, params object?[] args) =>
        await ((Task)Call(target, method, args)!).WaitAsync(TimeSpan.FromSeconds(5));
    public async Task DisposeConnection(object connection)
    {
        var status = Enum.Parse(Type("HttpConnectionStopStatus"), "NormalClosure");
        await Run(Manager, "DisposeAndRemoveAsync", connection, false, status);
        Assert.False(Contains(Get<string>(connection, "ConnectionToken")));
    }
    public bool Contains(string token)
    {
        object?[] args = [token, null];
        return (bool)Call(Manager, "TryGetConnection", args)!;
    }
    public DefaultHttpContext Context(string method = "GET", string query = "")
    {
        var context = new DefaultHttpContext { RequestServices = _services };
        context.Request.Method = method;
        context.Request.QueryString = new QueryString(query);
        context.Response.Body = new MemoryStream();
        return context;
    }
    public Task Dispatch(DefaultHttpContext context, HttpConnectionDispatcherOptions? options = null) => Run(Dispatcher, "ExecuteAsync", context,
        options ?? new HttpConnectionDispatcherOptions(), (ConnectionDelegate)(_ => Task.CompletedTask));
    public void Dispose()
    {
        Call(Manager, "CloseConnections"); // 同时释放 manager 的 PeriodicTimer，未调用 Start。
        _services.Dispose();
    }
    private sealed class InactiveLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => throw new InvalidOperationException("Tests must not start or stop an application host");
    }
}
