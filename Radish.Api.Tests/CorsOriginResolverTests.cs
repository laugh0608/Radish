#nullable enable

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Radish.Common.CoreTool;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests;

public sealed class CorsOriginResolverTests
{
    [Fact(DisplayName = "CORS 来源解析应在部署公开入口外保留附加来源")]
    public void ResolveAllowedOrigins_ShouldKeepAdditionalOriginsWithPublicUrl()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RADISH_PUBLIC_URL"] = "https://radish.example.com/app",
                ["Cors:AllowedOrigins:0"] = "https://localhost:5000",
                ["Cors:AdditionalAllowedOrigins:0"] = "http://tauri.localhost",
                ["Cors:AdditionalAllowedOrigins:1"] = "https://tauri.localhost",
            })
            .Build();

        var origins = CorsOriginResolver.ResolveAllowedOrigins(configuration);

        origins.ShouldBe([
            "https://radish.example.com",
            "http://tauri.localhost",
            "https://tauri.localhost",
        ]);
    }

    [Fact(DisplayName = "CORS 来源解析应在开发配置中合并并去重附加来源")]
    public void ResolveAllowedOrigins_ShouldMergeConfiguredAndAdditionalOrigins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://localhost:5000/",
                ["Cors:AllowedOrigins:1"] = "https://tauri.localhost",
                ["Cors:AdditionalAllowedOrigins:0"] = "https://tauri.localhost/",
                ["Cors:AdditionalAllowedOrigins:1"] = "http://tauri.localhost",
            })
            .Build();

        var origins = CorsOriginResolver.ResolveAllowedOrigins(configuration);

        origins.ShouldBe([
            "https://localhost:5000",
            "https://tauri.localhost",
            "http://tauri.localhost",
        ]);
    }
}
