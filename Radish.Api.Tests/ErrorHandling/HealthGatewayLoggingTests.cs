using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Radish.Gateway.HealthChecks;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HealthGatewayLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task UrlChecks_ShouldKeepSelectionAndFailureStatuses(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        using var scope = capture.SensitiveScope();
        var calls = new ConcurrentBag<string>();
        foreach (var failure in new[] { "status", "exception" })
        {
            using var fixture = Create(capture, (name, _) =>
            {
                calls.Add(name);
                return name == "auth-service" ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)) :
                    failure == "status" ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)) :
                    Task.FromException<HttpResponseMessage>(new IOException(HttpBoundaryCapture.Secret));
            });
            var minimal = await fixture.Health.CheckHealthAsync(GatewayHostHealthChecks.IsMinimal, TestContext.Current.CancellationToken);
            Assert.Equal(HealthStatus.Unhealthy, minimal.Status);
            Assert.Equal(2, minimal.Entries.Count);
            Assert.DoesNotContain("console-service", calls);
            Assert.Equal(HealthStatus.Unhealthy, minimal.Entries["api-service"].Status);
            Assert.Equal(HealthStatus.Healthy, minimal.Entries["auth-service"].Status);
            capture.AssertSafe();
            capture.Clear(); calls.Clear();
            var all = await fixture.Health.CheckHealthAsync(TestContext.Current.CancellationToken);
            Assert.Equal(HealthStatus.Unhealthy, all.Status);
            Assert.Equal(3, calls.Count);
            Assert.Equal(HealthStatus.Degraded, all.Entries["console-service"].Status);
            Assert.Contains("extended", all.Entries["console-service"].Tags);
            if (failure == "exception") Assert.IsType<IOException>(all.Entries["console-service"].Exception);
            // UriHealthCheck 将状态码 / IO 失败转换为结果，健康服务走 103，而不是抛出异常的 104。
            Assert.DoesNotContain("health.check_failed", capture.Codes);
            Assert.Contains("health.check_completed", capture.Codes);
            capture.AssertSafe();
            capture.Clear(); calls.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task UriCancellation_ShouldKeepExistingResultFolding(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "gateway");
        foreach (var callerCancellation in new[] { false, true })
        {
            using var cancellation = new CancellationTokenSource();
            using var fixture = Create(capture, (_, token) =>
            {
                if (callerCancellation) cancellation.Cancel();
                return Task.FromException<HttpResponseMessage>(new OperationCanceledException(HttpBoundaryCapture.Secret, token));
            });
            var report = await fixture.Health.CheckHealthAsync(r => r.Name == "api-service", cancellation.Token);
            Assert.Equal(HealthStatus.Unhealthy, report.Status);
            Assert.IsAssignableFrom<OperationCanceledException>(Assert.Single(report.Entries).Value.Exception);
            Assert.DoesNotContain("health.check_failed", capture.Codes);
            Assert.Contains("health.check_completed", capture.Codes);
            if (!candidate || environment == "Development") Assert.Contains("health.run_completed", capture.Codes);
            capture.AssertSafe();
            capture.Clear();
        }
    }

    private static HealthFixture Create(HttpBoundaryCapture capture, Func<string, CancellationToken, Task<HttpResponseMessage>> send)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DownstreamServices:ApiService:BaseUrl"] = "https://api.invalid",
            ["DownstreamServices:ApiService:HealthCheckPath"] = "/" + HttpBoundaryCapture.Secret,
            ["DownstreamServices:AuthService:BaseUrl"] = "https://auth.invalid",
            ["DownstreamServices:AuthService:HealthCheckPath"] = "/" + HttpBoundaryCapture.Secret,
            ["ReverseProxy:Clusters:consoleCluster:Destinations:console:Address"] = "https://console.invalid/" + HttpBoundaryCapture.Secret
        }).Build();
        return new HealthFixture(capture, services: services =>
        {
            services.AddGatewayHostHealthChecks(configuration, GatewayHostHealthChecks.CreateTags(configuration));
            foreach (var target in GatewayHostHealthChecks.CreateHealthTargets(configuration))
                services.AddHttpClient(target.Name).ConfigurePrimaryHttpMessageHandler(() => new InMemoryHandler(token => send(target.Name, token)));
        });
    }

    private sealed class InMemoryHandler(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(cancellationToken);
    }
}
