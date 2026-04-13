#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Radish.Api.HealthChecks;
using Radish.Auth.HealthChecks;
using Radish.Common.HealthTool;
using Radish.Gateway.HealthChecks;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.HealthChecks;

public class HostHealthCheckRegistrationTests
{
    [Fact(DisplayName = "Api 宿主健康检查应只把 self 暴露给最小探活")]
    public void ApiHostHealthChecks_ShouldKeepOnlySelfInMinimalSet()
    {
        var services = new ServiceCollection();
        services.AddApiHostHealthChecks();

        var registrations = BuildRegistrations(services);

        registrations.Select(registration => registration.Name).ShouldBe(
            ["self", "jwt-issuer", "jwt-signing-cert"],
            ignoreOrder: true);
        registrations.Where(ApiHostHealthChecks.IsMinimal).Select(registration => registration.Name).ShouldBe(["self"]);
        registrations.Single(registration => registration.Name == "jwt-issuer").Tags.ShouldContain("extended");
        registrations.Single(registration => registration.Name == "jwt-signing-cert").Tags.ShouldContain("extended");
    }

    [Fact(DisplayName = "Auth 宿主健康检查应只把 self 暴露给最小探活")]
    public void AuthHostHealthChecks_ShouldKeepOnlySelfInMinimalSet()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddAuthHostHealthChecks(configuration, new TestHostEnvironment());

        var registrations = BuildRegistrations(services);

        registrations.Select(registration => registration.Name).ShouldBe(
            ["self", "oidc-issuer", "oidc-signing-cert", "oidc-encryption-cert"],
            ignoreOrder: true);
        registrations.Where(AuthHostHealthChecks.IsMinimal).Select(registration => registration.Name).ShouldBe(["self"]);
        registrations.Single(registration => registration.Name == "oidc-issuer").Tags.ShouldContain("extended");
        registrations.Single(registration => registration.Name == "oidc-signing-cert").Tags.ShouldContain("extended");
        registrations.Single(registration => registration.Name == "oidc-encryption-cert").Tags.ShouldContain("extended");
    }

    [Fact(DisplayName = "Gateway 宿主健康检查应把 console 保持在扩展观测层")]
    public void GatewayHostHealthChecks_ShouldKeepConsoleOutsideMinimalSet()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DownstreamServices:ApiService:BaseUrl"] = "http://localhost:5100",
                ["DownstreamServices:ApiService:HealthCheckPath"] = "/health",
                ["DownstreamServices:AuthService:BaseUrl"] = "http://localhost:5200",
                ["DownstreamServices:AuthService:HealthCheckPath"] = "/health",
                ["ReverseProxy:Clusters:consoleCluster:Destinations:console:Address"] = "http://localhost:3100"
            })
            .Build();

        var tags = GatewayHostHealthChecks.CreateTags(configuration);
        services.AddGatewayHostHealthChecks(configuration, tags);

        var registrations = BuildRegistrations(services);

        registrations.Select(registration => registration.Name).ShouldBe(
            ["api-service", "auth-service", "console-service"],
            ignoreOrder: true);
        registrations.Where(GatewayHostHealthChecks.IsMinimal).Select(registration => registration.Name).ShouldBe(
            ["api-service", "auth-service"],
            ignoreOrder: true);
        registrations.Single(registration => registration.Name == "console-service").FailureStatus.ShouldBe(HealthStatus.Degraded);
        registrations.Single(registration => registration.Name == "console-service").Tags.ShouldContain("extended");
    }

    [Fact(DisplayName = "结构化健康响应应输出条目标签")]
    public async Task StructuredHealthCheckResponseWriter_ShouldEmitEntryTags()
    {
        var context = new DefaultHttpContext();
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["self"] = new(
                    HealthStatus.Healthy,
                    "Radish.Api 进程存活",
                    TimeSpan.FromMilliseconds(1),
                    null,
                    data: new Dictionary<string, object>()),
                ["jwt-issuer"] = new(
                    HealthStatus.Unhealthy,
                    "部署态 JWT Issuer 无法解析",
                    TimeSpan.FromMilliseconds(2),
                    new InvalidOperationException("issuer-invalid"),
                    data: new Dictionary<string, object>())
            },
            TimeSpan.FromMilliseconds(3));

        await StructuredHealthCheckResponseWriter.WriteJsonAsync(context, report, ApiHostHealthChecks.Tags);

        responseBody.Position = 0;
        using var json = await JsonDocument.ParseAsync(responseBody, cancellationToken: TestContext.Current.CancellationToken);

        context.Response.ContentType.ShouldBe("application/json; charset=utf-8");
        json.RootElement.GetProperty("status").GetString().ShouldBe("Unhealthy");

        var entries = json.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        var selfEntry = entries.Single(entry => entry.GetProperty("name").GetString() == "self");
        var jwtIssuerEntry = entries.Single(entry => entry.GetProperty("name").GetString() == "jwt-issuer");

        selfEntry.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).ShouldBe(["host", "self", "minimal"]);
        jwtIssuerEntry.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).ShouldBe(["host", "jwt", "extended"]);
        jwtIssuerEntry.GetProperty("exception").GetString().ShouldBe("issuer-invalid");
    }

    private static HealthCheckRegistration[] BuildRegistrations(ServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ToArray();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Radish.Auth.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
