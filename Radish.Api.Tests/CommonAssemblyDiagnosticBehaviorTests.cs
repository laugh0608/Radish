using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Radish.Common.LogTool;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class CommonAssemblyDiagnosticBehaviorTests
{
    [Fact]
    public void EffectiveTypes_ShouldRemainLazyAndRepeated_AndDiscardPartialTypesWithoutReadingMetadata()
    {
        using var output = new StringWriter();
        using var fixture = new IsolatedAssemblyDiagnostics(output);
        var failing = new DiagnosticTestAssembly { Fail = true };
        var valid = new DiagnosticTestAssembly();
        fixture.ReplaceAssemblies(failing, valid);
        var types = fixture.EffectiveTypes;
        Assert.Equal(0, failing.Calls + valid.Calls);
        Assert.Empty(CommonAssemblyDiagnosticTests.Events(output));
        for (var repeat = 0; repeat < 2; repeat++)
        {
            Assert.Equal(new[] { typeof(CommonAssemblyDiagnosticBehaviorTests) }, types.ToArray());
            Assert.Equal(repeat + 1, failing.Calls);
            Assert.Equal(repeat + 1, valid.Calls);
            Assert.Equal(repeat + 1, CommonAssemblyDiagnosticTests.Events(output).Length);
        }
        Assert.All(CommonAssemblyDiagnosticTests.Events(output), e => CommonAssemblyDiagnosticTests.AssertSummary(e,
            "api", "runtime.assembly_types_failed", "other"));
        Assert.Equal(0, failing.MetadataReads + valid.MetadataReads);
    }

    [Fact]
    public void TypeFailure_WithBrokenOutput_ShouldStillYieldEmptyAndKeepAppUsable()
    {
        using var output = new FailingAssemblyWriter { FailWrites = true };
        using var fixture = new IsolatedAssemblyDiagnostics(output);
        fixture.ReplaceAssemblies(new DiagnosticTestAssembly { Fail = true });
        Assert.Empty(fixture.EffectiveTypes);
        Assert.Empty(fixture.EffectiveTypes);
        fixture.App.GetProperty("IsBuild")!.SetValue(null, true);
        Assert.Equal(true, fixture.App.GetProperty("IsBuild")!.GetValue(null));
        Assert.Equal(3, output.WriteAttempts); // Two attempts plus one emergency attempt.
    }

    [Fact]
    public void PublicOnlyFilterFailure_ShouldRemainOutsideTheConsumedGetTypesBoundary()
    {
        using var output = new StringWriter();
        using var fixture = new IsolatedAssemblyDiagnostics(output);
        fixture.ReplaceAssemblies(new DiagnosticTestAssembly { ReturnNull = true });
        Assert.Throws<NullReferenceException>(() => fixture.EffectiveTypes.ToArray());
        Assert.Empty(CommonAssemblyDiagnosticTests.Events(output));
    }

    [Fact]
    public void DirectTypeHelpers_ShouldKeepLoadFailurePropagation()
    {
        using var output = new StringWriter();
        using var fixture = new IsolatedAssemblyDiagnostics(output);
        var actual = Assert.Throws<TargetInvocationException>(() => fixture.Runtime.GetMethod("GetTypesByAssembly")!
            .Invoke(null, new object[] { "Missing.Assembly." + CommonAssemblyDiagnosticTests.Secret }));
        Assert.IsType<FileNotFoundException>(actual.InnerException);
        Assert.Empty(CommonAssemblyDiagnosticTests.Events(output));
    }
}

/// <summary>只隔离 Common 的静态状态，框架及第三方依赖仍由默认加载上下文解析。</summary>
internal sealed class IsolatedAssemblyDiagnostics : IDisposable
{
    private readonly AssemblyLoadContext _context = new("common-diagnostic-test-" + Guid.NewGuid().ToString("N"), isCollectible: true);
    private readonly IDisposable _scope;
    public Type App { get; }
    public Type Runtime { get; }
    public Type Reporter { get; }

    public IsolatedAssemblyDiagnostics(TextWriter output, string service = "api", string? assemblyPath = null)
    {
        _context.Resolving += (_, name) => AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        var assembly = _context.LoadFromAssemblyPath(assemblyPath ?? typeof(AssemblyDiagnosticReporter).Assembly.Location);
        App = assembly.GetType("Radish.Common.CoreTool.App", throwOnError: true)!;
        Runtime = assembly.GetType("Radish.Common.CoreTool.RuntimeExtension", throwOnError: true)!;
        Reporter = assembly.GetType("Radish.Common.LogTool.AssemblyDiagnosticReporter", throwOnError: true)!;
        _scope = (IDisposable)Reporter.GetMethod("BeginScope")!.Invoke(null, new object[] { service, output })!;
    }

    public IEnumerable<Type> EffectiveTypes => (IEnumerable<Type>)App.GetField("EffectiveTypes")!.GetValue(null)!;

    public IList<Assembly> LoadAssemblies() => (IList<Assembly>)Runtime.GetMethod("GetAllAssemblies")!.Invoke(null, null)!;

    public void ReplaceAssemblies(params Assembly[] assemblies)
    {
        var current = (IList<Assembly>)App.GetField("Assemblies")!.GetValue(null)!;
        current.Clear();
        foreach (var assembly in assemblies) current.Add(assembly);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _context.Unload();
    }
}

internal sealed class DiagnosticTestAssembly : Assembly
{
    public bool Fail, ReturnNull;
    public int Calls, MetadataReads;
    public override string FullName
    {
        get { MetadataReads++; throw new InvalidOperationException("Assembly FullName must not be read"); }
    }
    public override Type[] GetTypes()
    {
        Calls++;
        if (Fail) throw new ReflectionTypeLoadException(new[] { typeof(string) }, new Exception[] { new UnreadableAssemblyException() });
        if (ReturnNull) return new Type[] { null! };
        return new[] { typeof(CommonAssemblyDiagnosticBehaviorTests), typeof(DiagnosticTestAssembly) };
    }
}
