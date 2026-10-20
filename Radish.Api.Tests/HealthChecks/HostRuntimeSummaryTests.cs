#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Radish.Api.HealthChecks;
using Radish.Auth.HealthChecks;
using Radish.Gateway.HealthChecks;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.HealthChecks;

public sealed class HostRuntimeSummaryTests
{
    [Fact(DisplayName = "Api 本地模式应保留健康检查使用的 Issuer 与验签选择")]
    public void ApiJwtRuntimeProfile_ShouldKeepAuthorityModeResolution()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();

        ApiJwtRuntimeProfile.IsDeploymentJwtValidationEnabled(configuration).ShouldBeFalse();
        ApiJwtRuntimeProfile.ResolveJwtIssuer(configuration).ShouldBeNull();
    }

    [Fact(DisplayName = "Api 证书路径解析应保留已有文件优先规则")]
    public void ApiJwtRuntimeProfile_ShouldKeepCertificatePathResolution()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"radish-api-runtime-{Guid.NewGuid():N}");
        var basePath = Path.Combine(tempRoot, "base");
        var contentRootPath = Path.Combine(tempRoot, "content");
        Directory.CreateDirectory(basePath);
        Directory.CreateDirectory(contentRootPath);

        try
        {
            var certificatePath = Path.Combine(basePath, "certs", "api-signing.pfx");
            Directory.CreateDirectory(Path.GetDirectoryName(certificatePath)!);
            File.WriteAllText(certificatePath, "test");

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RADISH_PUBLIC_URL"] = "https://radish.example.com/",
                    ["OpenIddict:Encryption:SigningCertificatePath"] = "certs/api-signing.pfx"
                })
                .Build();

            ApiJwtRuntimeProfile.IsDeploymentJwtValidationEnabled(configuration).ShouldBeTrue();
            ApiJwtRuntimeProfile.ResolveJwtIssuer(configuration).ShouldBe("https://radish.example.com");
            ApiJwtRuntimeProfile.ResolveSigningCertificatePath(configuration, basePath, contentRootPath).ShouldBe(certificatePath);
            File.Delete(certificatePath);
            ApiJwtRuntimeProfile.ResolveSigningCertificatePath(configuration, basePath, contentRootPath)
                .ShouldBe(Path.Combine(contentRootPath, "certs", "api-signing.pfx"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact(DisplayName = "Auth 健康检查应保留开发密钥与 Issuer 解析")]
    public void AuthOidcRuntimeProfile_ShouldKeepHealthCheckResolution()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenIddict:Server:Issuer"] = "https://auth.radish.example.com/",
                ["OpenIddict:Encryption:UseDevelopmentKeys"] = "true"
            })
            .Build();

        AuthOidcRuntimeProfile.UsesDevelopmentKeys(configuration).ShouldBeTrue();
        AuthOidcRuntimeProfile.ResolveIssuer(configuration).ShouldBe("https://auth.radish.example.com");
        AuthOidcRuntimeProfile.ResolveCertificatePath(configuration, new TestHostEnvironment(), "Signing").ShouldBeEmpty();
    }

    [Fact(DisplayName = "Gateway 运行目标应把 console 维持为扩展观测且探活 /healthz")]
    public void GatewayHostHealthChecks_ShouldDescribeConsoleAsExtendedHealthTarget()
    {
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

        var targets = GatewayHostHealthChecks.CreateHealthTargets(configuration);

        targets.Select(target => target.Name).ShouldBe(["api-service", "auth-service", "console-service"], ignoreOrder: true);

        var consoleTarget = targets.Single(target => target.Name == "console-service");
        consoleTarget.Url.ShouldBe("http://localhost:3100/healthz");
        consoleTarget.FailureStatus.ShouldBe(HealthStatus.Degraded);
        consoleTarget.Tags.ShouldContain("extended");
        consoleTarget.Tags.ShouldNotContain("minimal");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Radish.Auth.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
