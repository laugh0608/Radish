using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Radish.Common.LogTool;
using Xunit;

namespace Radish.Api.Tests;

[Collection("Runtime logging global state")]
public sealed class CommonAssemblyDiagnosticProcessTests
{
    private const string ChildMode = "RADISH_ASSEMBLY_DIAGNOSTIC_TEST_CHILD";

    [Fact]
    public async Task FreshProcess_ShouldKeepRealScanningAndStaticInitializationSafe()
    {
        var mode = Environment.GetEnvironmentVariable(ChildMode);
        if (mode != null)
        {
            RunChild(mode);
            return;
        }
        foreach (var scenario in new[] { "normal", "write-failure", "flush-failure", "reentrant", "policy-failure" })
        {
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add(typeof(CommonAssemblyDiagnosticProcessTests).Assembly.Location);
            foreach (var arg in new[] { "-noLogo", "-noColor", "-class", typeof(CommonAssemblyDiagnosticProcessTests).FullName! }) start.ArgumentList.Add(arg);
            start.Environment[ChildMode] = scenario;
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            try { await process.WaitForExitAsync(timeout.Token); }
            finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
            var text = await stdout + await stderr;
            Assert.True(process.ExitCode == 0, $"Isolated {scenario} test failed: {text}");
            Assert.DoesNotContain(CommonAssemblyDiagnosticTests.Secret, text);
        }
    }

    private static void RunChild(string mode)
    {
        var root = Path.Combine(Path.GetTempPath(), "radish-common-diagnostic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previousBase = AppContext.GetData("APP_CONTEXT_BASE_DIRECTORY");
        var previousOut = Console.Out;
        using var stdout = new StringWriter();
        using var safeOutput = new StringWriter();
        using var failing = new FailingAssemblyWriter { FailWrites = mode == "write-failure", FailFlushes = mode == "flush-failure" };
        // Reentry targets this isolated reporter, not another copy in the default context.
        using var reentrant = new IsolatedReentrantWriter();
        TextWriter writer = mode is "normal" or "policy-failure" ? safeOutput : mode == "reentrant" ? reentrant : failing;
        try
        {
            File.WriteAllText(Path.Combine(root, "Radish.Api." + CommonAssemblyDiagnosticTests.Secret + ".dll"), "synthetic invalid image");
            File.WriteAllText(Path.Combine(root, "Ignored." + CommonAssemblyDiagnosticTests.Secret + ".dll"), "ignored invalid image");
            File.Copy(typeof(AssemblyDiagnosticReporter).Assembly.Location, Path.Combine(root, "Radish.Api.Valid.dll"));
            AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", root + Path.DirectorySeparatorChar);
            Assert.Equal(root + Path.DirectorySeparatorChar, AppDomain.CurrentDomain.BaseDirectory);
            Console.SetOut(stdout);
            var policyFailureAssembly = mode == "policy-failure" ? CreateBrokenPolicyAssembly(root) : null;
            using var fixture = new IsolatedAssemblyDiagnostics(writer, assemblyPath: policyFailureAssembly);
            reentrant.Report = () => fixture.Reporter.GetMethod("AssemblyTypesFailed")!.Invoke(null, new object[] { new UnreadableAssemblyException() });
            var loaded = fixture.LoadAssemblies();
            Assert.Single(loaded); // Matching broken file is skipped; valid matching file still loads; other prefix is ignored.
            Assert.Equal("Radish.Common", loaded[0].GetName().Name);
            Assert.Equal(false, fixture.App.GetProperty("IsBuild")!.GetValue(null)); // Real static initializer scans again.
            fixture.App.GetProperty("IsBuild")!.SetValue(null, true);
            Assert.Equal(true, fixture.App.GetProperty("IsBuild")!.GetValue(null));
            Assert.Equal("", stdout.ToString());
            if (mode is "normal" or "reentrant")
            {
                var events = CommonAssemblyDiagnosticTests.Events(mode == "normal" ? safeOutput : reentrant);
                Assert.Equal(2, events.Length);
                Assert.All(events, e => CommonAssemblyDiagnosticTests.AssertSummary(e, "api", "runtime.assembly_load_failed", "other"));
            }
            else if (mode == "policy-failure") Assert.Empty(CommonAssemblyDiagnosticTests.Events(safeOutput));
            else Assert.Equal(3, failing.WriteAttempts); // Both real load failures are consumed even if stderr fails.
            if (mode == "reentrant") Assert.Equal(2, reentrant.Attempts);
            safeOutput.GetStringBuilder().Clear();
            AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", Path.Combine(root, "missing-directory"));
            var missing = Assert.Throws<TargetInvocationException>(() => fixture.LoadAssemblies());
            Assert.IsType<DirectoryNotFoundException>(missing.InnerException);
            Assert.Equal("", safeOutput.ToString()); // Directory enumeration remains outside the consumed catch.
        }
        finally
        {
            Console.SetOut(previousOut);
            AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", previousBase);
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateBrokenPolicyAssembly(string root)
    {
        var common = typeof(AssemblyDiagnosticReporter).Assembly;
        using var resource = common.GetManifestResourceStream("Radish.Common.LogTool.Contracts.runtime-log-policy.v1.json")!;
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        var resourceBytes = buffer.ToArray();
        var image = File.ReadAllBytes(common.Location);
        var offset = image.AsSpan().IndexOf(resourceBytes);
        Assert.True(offset >= 0);
        image[offset] = (byte)'!'; // Corrupt only the embedded JSON in a disposable copy; CLR metadata stays valid.
        var path = Path.Combine(root, "Common.PolicyFailure.dll");
        File.WriteAllBytes(path, image);
        return path;
    }

    private sealed class IsolatedReentrantWriter : StringWriter
    {
        public Action? Report;
        public int Attempts;
        public override void WriteLine(string? value)
        {
            Attempts++;
            Report!();
            base.WriteLine(value);
        }
    }
}
