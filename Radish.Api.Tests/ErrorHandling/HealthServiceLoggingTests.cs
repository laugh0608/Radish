using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HealthServiceLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ResultsAndData_ShouldKeepReportIdentityAndSeverity(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        foreach (var status in new[] { HealthStatus.Healthy, HealthStatus.Degraded, HealthStatus.Unhealthy })
        {
            var value = new HealthPoisonValue();
            var data = new Dictionary<string, object> { ["SourceContext"] = HttpBoundaryCapture.Secret, ["EventId"] = 104, ["payload"] = value };
            var exception = new HealthUnreadableException();
            using var fixture = new HealthFixture(capture, builder => builder.AddCheck(HttpBoundaryCapture.Secret,
                new DelegateHealthCheck(_ => Task.FromResult(new HealthCheckResult(status, HttpBoundaryCapture.Secret, exception, data))), tags: new[] { "minimal" }));
            var report = await fixture.Health.CheckHealthAsync(TestContext.Current.CancellationToken);
            Assert.Equal(status, report.Status);
            var entry = Assert.Single(report.Entries).Value;
            Assert.True(ReferenceEquals(exception, entry.Exception));
            Assert.True(ReferenceEquals(data, entry.Data));
            Assert.Equal(HttpBoundaryCapture.Secret, entry.Description);
            Assert.Contains("minimal", entry.Tags);
            Assert.Equal(0, value.FormatCalls);
            AssertCodes(capture, status == HealthStatus.Healthy ? Array.Empty<string>() : new[] { "health.check_completed" },
                "health.run_started", "health.check_started", "health.check_completed", "health.check_data", "health.run_completed");
            var result = capture.Events.SingleOrDefault(e => e.Code == "health.check_completed");
            if (result != null) Assert.Equal(status == HealthStatus.Healthy ? candidate ? "Info" : "Debug" : status == HealthStatus.Degraded ? "Warning" : "Error", result.Level);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task PredicatesEmptySetAndAggregation_ShouldPreserveSelection(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var calls = new List<string>();
        using var fixture = new HealthFixture(capture, builder =>
        {
            foreach (var (name, status) in new[] { ("self", HealthStatus.Healthy), ("console", HealthStatus.Degraded), ("api", HealthStatus.Unhealthy) })
                builder.AddCheck(name, new DelegateHealthCheck(_ =>
                {
                    lock (calls) calls.Add(name);
                    return Task.FromResult(new HealthCheckResult(status));
                }), tags: new[] { name == "self" ? "minimal" : "extended" });
        });
        var minimal = await fixture.Health.CheckHealthAsync(r => r.Tags.Contains("minimal"), TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Healthy, minimal.Status);
        Assert.Equal(new[] { "self" }, calls);
        Assert.Equal(new[] { "self" }, minimal.Entries.Keys);
        capture.Clear(); calls.Clear();
        var empty = await fixture.Health.CheckHealthAsync(_ => false, TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Healthy, empty.Status);
        Assert.Empty(empty.Entries); Assert.Empty(calls);
        AssertCodes(capture, Array.Empty<string>(), "health.run_started", "health.run_completed");
        capture.Clear();
        var all = await fixture.Health.CheckHealthAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, all.Status);
        Assert.Equal(3, calls.Count);
        Assert.Equal(3, all.Entries.Count);
        Assert.Equal(candidate && environment == "Production" ? 2 : 3, capture.Codes.Count(c => c == "health.check_completed"));
        HealthFrameworkEvents.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task ConsumedErrorsAndNonCallerCancellation_ShouldKeepFailureStatus(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var failureStatus in new[] { HealthStatus.Degraded, HealthStatus.Unhealthy })
        foreach (var cancelled in new[] { false, true })
        {
            var failure = cancelled ? (Exception)new OperationCanceledException(HttpBoundaryCapture.Secret) : new IOException(HttpBoundaryCapture.Secret);
            using var fixture = new HealthFixture(capture, b => b.AddCheck("failing", new DelegateHealthCheck(_ => Task.FromException<HealthCheckResult>(failure)), failureStatus));
            var report = await fixture.Health.CheckHealthAsync(TestContext.Current.CancellationToken);
            var entry = Assert.Single(report.Entries).Value;
            Assert.Equal(failureStatus, report.Status);
            Assert.True(ReferenceEquals(failure, entry.Exception));
            Assert.Equal(cancelled ? "A timeout occurred while running check." : HttpBoundaryCapture.Secret, entry.Description);
            AssertCodes(capture, new[] { "health.check_failed" }, "health.run_started", "health.check_started", "health.check_failed", "health.run_completed");
            Assert.Equal("Error", capture.Events.Single(e => e.Code == "health.check_failed").Level);
            Assert.Equal(cancelled ? "cancelled" : "io", capture.Events.Single(e => e.Code == "health.check_failed").FailureKind);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task CallerCancellation_ShouldAbortWithoutCompletionOrFailureSummary(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        foreach (var before in new[] { true, false })
        {
            using var cancellation = new CancellationTokenSource();
            var original = new OperationCanceledException(cancellation.Token);
            var invoked = false;
            using var fixture = new HealthFixture(capture, b => b.AddCheck("cancel", new DelegateHealthCheck(token =>
            {
                invoked = true;
                Assert.Equal(cancellation.Token, token);
                cancellation.Cancel();
                return Task.FromException<HealthCheckResult>(original);
            })));
            if (before) cancellation.Cancel();
            var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Health.CheckHealthAsync(cancellation.Token));
            Assert.Equal(!before, invoked);
            if (!before) Assert.Same(original, thrown);
            Assert.DoesNotContain("health.check_failed", capture.Codes);
            Assert.DoesNotContain("health.run_completed", capture.Codes);
            Assert.DoesNotContain("health.check_completed", capture.Codes);
            HealthFrameworkEvents.AssertSafe(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task FactoryAndScopeDisposalErrors_ShouldPropagateAtOriginalBoundary(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var failure = new IOException(HttpBoundaryCapture.Secret);
        using (var fixture = new HealthFixture(capture, b => b.Add(new HealthCheckRegistration("factory", _ => throw failure, null, null))))
        {
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Health.CheckHealthAsync(TestContext.Current.CancellationToken)));
            Assert.DoesNotContain("health.check_started", capture.Codes);
            Assert.DoesNotContain("health.check_failed", capture.Codes);
            Assert.DoesNotContain("health.run_completed", capture.Codes);
        }
        capture.Clear();
        var disposable = new FailingScope(failure);
        using (var fixture = new HealthFixture(capture, b => b.Add(new HealthCheckRegistration("dispose", sp =>
        {
            _ = sp.GetRequiredService<FailingScope>();
            return new DelegateHealthCheck(_ => Task.FromResult(HealthCheckResult.Healthy()));
        }, null, null)), services => services.AddScoped(_ => disposable)))
        {
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => fixture.Health.CheckHealthAsync(TestContext.Current.CancellationToken)));
            Assert.Equal(1, disposable.Calls);
            Assert.DoesNotContain("health.check_failed", capture.Codes);
            Assert.DoesNotContain("health.run_completed", capture.Codes);
            if (!candidate || environment == "Development") Assert.Contains("health.check_completed", capture.Codes);
            HealthFrameworkEvents.AssertSafe(capture);
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task BeforeLoggerMessageAndDataEnumeration_ShouldKeepExistingFailureBoundary(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var messageFailure = new InvalidOperationException("message getter failed");
        using (var fixture = new HealthFixture(capture, b => b.AddCheck("message", new DelegateHealthCheck(_ => Task.FromException<HealthCheckResult>(new FailingMessageException(messageFailure))))))
        {
            Assert.Same(messageFailure, await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Health.CheckHealthAsync(TestContext.Current.CancellationToken)));
            Assert.DoesNotContain("health.check_failed", capture.Codes);
            Assert.DoesNotContain("health.run_completed", capture.Codes);
        }
        capture.Clear();
        foreach (var count in new[] { true, false })
        {
            var data = new FailingData(count);
            using var fixture = new HealthFixture(capture, b => b.AddCheck("data", new DelegateHealthCheck(_ => Task.FromResult(HealthCheckResult.Healthy(data: data))), HealthStatus.Degraded));
            var report = await fixture.Health.CheckHealthAsync(TestContext.Current.CancellationToken);
            Assert.Equal(HealthStatus.Degraded, report.Status);
            Assert.Same(data.Failure, Assert.Single(report.Entries).Value.Exception);
            Assert.Contains("health.check_failed", capture.Codes);
            Assert.DoesNotContain("health.check_data", capture.Codes);
            HealthFrameworkEvents.AssertSafe(capture);
            capture.Clear();
        }
    }

    [Fact]
    public void DuplicateRegistrationNames_ShouldStillFailBeforeRunningChecks()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks().AddCheck("same", () => HealthCheckResult.Healthy()).AddCheck("SAME", () => HealthCheckResult.Healthy());
        using var provider = services.BuildServiceProvider();
        Assert.Throws<ArgumentException>(() => provider.GetRequiredService<HealthCheckService>());
    }

    internal static void AssertCodes(HttpBoundaryCapture capture, string[] production, params string[] other)
    {
        Assert.Equal(capture.Candidate && capture.Environment == "Production" ? production : other, capture.Codes);
        HealthFrameworkEvents.AssertSafe(capture);
    }

    private sealed class FailingScope(Exception failure) : IDisposable
    {
        internal int Calls;
        public void Dispose() { Calls++; throw failure; }
    }
    private sealed class FailingMessageException(Exception failure) : Exception
    {
        public override string Message => throw failure;
    }
    private sealed class FailingData(bool failCount) : IReadOnlyDictionary<string, object>
    {
        internal readonly IOException Failure = new("Data enumeration failed");
        public int Count => failCount ? throw Failure : 1;
        public IEnumerable<string> Keys => throw Failure;
        public IEnumerable<object> Values => throw Failure;
        public object this[string key] => throw Failure;
        public bool ContainsKey(string key) => throw Failure;
        public bool TryGetValue(string key, out object value) => throw Failure;
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => throw Failure;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
