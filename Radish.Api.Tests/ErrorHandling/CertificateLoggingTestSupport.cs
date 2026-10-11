using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Radish.Api.Tests.ErrorHandling;

/// <summary>仅反射已安装框架的测试入口；不加载宿主配置、不创建物理监视器或访问证书库。</summary>
internal static class CertificateLoggingFramework
{
    public const string Watcher = "Microsoft.AspNetCore.Server.Kestrel.Core.Internal.CertificatePathWatcher";
    public const string Config = "Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServer";
    public const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static Type Type(string name) => Assembly.Load("Microsoft.AspNetCore.Server.Kestrel.Core").GetType(name, true)!;
    public static object Logger(string category, ILoggerFactory factory) =>
        Activator.CreateInstance(typeof(Logger<>).MakeGenericType(Type(category)), factory)!;

    public static object CertificateConfig(string path, string? keyPath = null, string? password = null)
    {
        var value = Activator.CreateInstance(Type("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.CertificateConfig"), nonPublic: true)!;
        Set(value, "Path", path);
        Set(value, "KeyPath", keyPath);
        Set(value, "Password", password);
        return value;
    }

    public static void Set(object target, string property, object? value) => target.GetType().GetProperty(property, Members)!.SetValue(target, value);
    public static T Get<T>(object target, string property) => (T)target.GetType().GetProperty(property, Members)!.GetValue(target)!;
    public static object? Call(object target, string method, params object?[] args) =>
        Invoke(target.GetType().GetMethod(method, Members)!, target, args);

    public static object? Invoke(MethodInfo method, object? target, object?[] args)
    {
        try { return method.Invoke(target, args); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    public static object ConfigList(params object[] configs)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(
            Type("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.CertificateConfig")))!;
        foreach (var config in configs) list.Add(config);
        return list;
    }

    public static bool DiagnosticVisible(HttpBoundaryCapture capture) => !capture.Candidate || capture.Environment == "Development";

    public static void AssertSafe(HttpBoundaryCapture capture)
    {
        capture.AssertSafe();
        foreach (var entry in capture.Sink.Events)
            Xunit.Assert.All(entry.Properties.Keys, key => Xunit.Assert.Contains(key,
                new[] { "EventCode", "SourceCategory", "SourceContext", "Diagnostic", "failureKind" }));
    }
}

internal sealed class CertificateTestEnvironment(string root) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Production;
    public string ApplicationName { get; set; } = "radish-certificate-isolation-test";
    public string ContentRootPath { get; set; } = root;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class UnreadableCertificateException : Exception
{
    public override string Message => throw new InvalidOperationException("Must not read certificate exception message");
    public override string ToString() => throw new InvalidOperationException("Must not render certificate exception");
}

internal sealed class UnreadableCertificateValue
{
    public override string ToString() => throw new InvalidOperationException("Must not render certificate payload");
}
