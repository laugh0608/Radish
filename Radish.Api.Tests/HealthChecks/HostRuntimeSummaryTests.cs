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
    [Fact(DisplayName = "Api 启动摘要应在本地 Authority 模式下回退到默认说明")]
    public void ApiJwtRuntimeProfile_ShouldDescribeAuthorityModeSummary()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();

        var summary = ApiJwtRuntimeProfile.BuildStartupSummary(
            configuration,
            basePath: AppContext.BaseDirectory,
            contentRootPath: AppContext.BaseDirectory);

        summary.ValidationMode.ShouldBe("authority");
        summary.ValidationTarget.ShouldBe(ApiJwtRuntimeProfile.LocalAuthority);
        summary.IssuerSummary.ShouldBe($"未解析，沿用 {ApiJwtRuntimeProfile.LocalAuthority}");
        summary.SigningCertificateSummary.ShouldBe("未启用（Authority 模式）");
    }

    [Fact(DisplayName = "Api 启动摘要应在部署态指向本地证书验签")]
    public void ApiJwtRuntimeProfile_ShouldDescribeLocalCertificateModeSummary()
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

            var summary = ApiJwtRuntimeProfile.BuildStartupSummary(configuration, basePath, contentRootPath);

            summary.ValidationMode.ShouldBe("local-certificate");
            summary.ValidationTarget.ShouldBe("https://radish.example.com");
            summary.IssuerSummary.ShouldBe("https://radish.example.com");
            summary.SigningCertificateSummary.ShouldBe(certificatePath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact(DisplayName = "Auth 启动摘要应在开发密钥模式下输出固定说明")]
    public void AuthOidcRuntimeProfile_ShouldDescribeDevelopmentKeysSummary()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenIddict:Server:Issuer"] = "https://auth.radish.example.com/",
                ["OpenIddict:Encryption:UseDevelopmentKeys"] = "true"
            })
            .Build();

        var summary = AuthOidcRuntimeProfile.BuildStartupSummary(configuration, new TestHostEnvironment());

        summary.IssuerSummary.ShouldBe("https://auth.radish.example.com");
        summary.KeyMode.ShouldBe("development-keys");
        summary.SigningCertificateSummary.ShouldBe("开发密钥");
        summary.EncryptionCertificateSummary.ShouldBe("开发密钥");
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
