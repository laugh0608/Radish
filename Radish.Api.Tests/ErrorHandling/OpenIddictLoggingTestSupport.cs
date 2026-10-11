using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Radish.Extension.Log;
using Serilog;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

internal sealed class OpenIddictLoggingFixture : IDisposable
{
    internal readonly ServiceProvider Services;
    internal readonly ILoggerFactory Factory;
    internal OpenIddictLoggingFixture(HttpBoundaryCapture capture, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSerilog(capture.Logger, dispose: false);
        services.ProtectOpenIddictServerLogging();
        services.ProtectHealthCheckLogging();
        configure?.Invoke(services);
        Services = services.BuildServiceProvider();
        Factory = Services.GetRequiredService<ILoggerFactory>();
    }
    internal Microsoft.Extensions.Logging.ILogger ServerLogger => Factory.CreateLogger(OpenIddictEvents.Server);
    internal OpenIddictServerTransaction Transaction(OpenIddictRequest? request = null, OpenIddictServerOptions? options = null) => new()
    {
        Logger = ServerLogger, Options = options ?? new OpenIddictServerOptions(), Request = request ?? new OpenIddictRequest(),
        Response = new OpenIddictResponse(), BaseUri = new Uri("https://server.example.invalid/"),
        RequestUri = new Uri("https://server.example.invalid/connect/token")
    };
    internal static DefaultHttpContext AttachHttp(OpenIddictServerTransaction transaction)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("server.example.invalid");
        context.Response.Body = new System.IO.MemoryStream();
        transaction.Properties[typeof(HttpRequest).FullName!] = new WeakReference<HttpRequest>(context.Request);
        return context;
    }
    public void Dispose() => Services.Dispose();
    internal static void AssertSafe(HttpBoundaryCapture capture)
    {
        capture.AssertSafe();
        foreach (var entry in capture.Sink.Events)
            Assert.All(entry.Properties.Keys, key => Assert.Contains(key,
                new[] { "EventCode", "SourceContext", "SourceCategory", "Diagnostic", "failureKind" }));
    }
}

internal sealed class OpenIddictOptions<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue => value;
    public T Get(string? name) => value;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

/// <summary>读取已安装程序集的数字 EventId 调用，防止遗漏升级新增事件；不把资源编号或模板当作身份。</summary>
internal static class OpenIddictCompiledEvents
{
    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public)
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Value);

    internal static HashSet<int> Read(string assemblyName)
    {
        var ids = new HashSet<int>();
        foreach (var type in Assembly.Load(assemblyName).GetTypes())
        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            var bytes = method.GetMethodBody()?.GetILAsByteArray();
            if (bytes == null) continue;
            int? previousInteger = null;
            for (var position = 0; position < bytes.Length;)
            {
                short value = bytes[position++];
                if (value == 254) value = (short)(-512 + bytes[position++]);
                var operation = OpCodesByValue[value];
                var operand = position;
                position += operation.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, operand),
                    _ => 4
                };
                if (operation == OpCodes.Ldc_I4) previousInteger = BitConverter.ToInt32(bytes, operand);
                else if (operation == OpCodes.Ldc_I4_S) previousInteger = (sbyte)bytes[operand];
                else
                {
                    if (operation.OperandType == OperandType.InlineMethod)
                    {
                        var target = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, operand),
                            type.IsGenericType ? type.GetGenericArguments() : null, method.IsGenericMethod ? method.GetGenericArguments() : null);
                        if (target?.DeclaringType == typeof(EventId) && target.Name == "op_Implicit" && previousInteger is int id)
                            ids.Add(id);
                    }
                    previousInteger = null;
                }
            }
        }
        return ids;
    }
}
