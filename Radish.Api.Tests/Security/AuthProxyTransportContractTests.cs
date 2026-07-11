using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Radish.Auth.OpenIddict;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Security;

public class AuthProxyTransportContractTests
{
    [Fact]
    public async Task ForwardedProtoFromGateway_ShouldRestoreHttpsScheme()
    {
        var options = new ForwardedHeadersOptions();
        AuthForwardedHeadersPolicy.Configure(options);
        var observedScheme = string.Empty;
        var middleware = new ForwardedHeadersMiddleware(
            context =>
            {
                observedScheme = context.Request.Scheme;
                return Task.CompletedTask;
            },
            NullLoggerFactory.Instance,
            Options.Create(options));
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        await middleware.Invoke(context);

        observedScheme.ShouldBe("https");
        options.ForwardLimit.ShouldBe(1);
    }

    [Fact]
    public void GatewayAuthRoutes_ShouldForwardOriginalScheme()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(GetRepositoryPath("Radish.Gateway", "appsettings.json")),
            new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        var routes = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Routes");

        AssertRouteForwardsScheme(routes.GetProperty("auth-account-route"));
        AssertRouteForwardsScheme(routes.GetProperty("auth-connect-route"));
    }

    [Fact]
    public void DevelopmentHttpLaunchProfile_ShouldExplicitlyAllowInsecureTransport()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(GetRepositoryPath("Radish.Auth", "Properties", "launchSettings.json")));
        var environmentVariables = document.RootElement
            .GetProperty("profiles")
            .GetProperty("http")
            .GetProperty("environmentVariables");

        environmentVariables.GetProperty("ASPNETCORE_ENVIRONMENT").GetString().ShouldBe("Development");
        environmentVariables.GetProperty("OpenIddict__Server__AllowInsecureHttp").GetString().ShouldBe("true");
    }

    [Theory]
    [InlineData("docker-compose.local.yaml")]
    [InlineData("docker-compose.yaml")]
    public void AuthComposeService_ShouldRequireSecureTransportAndAvoidHostPort(string composeFile)
    {
        var content = File.ReadAllText(GetRepositoryPath("Deploy", composeFile));
        var authSection = GetYamlServiceSection(content, "auth");

        authSection.ShouldContain("OpenIddict__Server__AllowInsecureHttp: \"false\"");
        authSection.ShouldContain("expose:");
        authSection.ShouldNotContain("ports:");
    }

    private static void AssertRouteForwardsScheme(JsonElement route)
    {
        var forwardsScheme = route.GetProperty("Transforms").EnumerateArray().Any(transform =>
            transform.TryGetProperty("RequestHeader", out var header) &&
            header.GetString() == "X-Forwarded-Proto" &&
            transform.TryGetProperty("Set", out var value) &&
            value.GetString() == "{scheme}");
        forwardsScheme.ShouldBeTrue();
    }

    private static string GetYamlServiceSection(string content, string serviceName)
    {
        var marker = $"  {serviceName}:";
        var start = content.IndexOf(marker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var nextService = content.IndexOf("\n  ", start + marker.Length, StringComparison.Ordinal);
        while (nextService >= 0 &&
               (nextService + 3 >= content.Length || char.IsWhiteSpace(content[nextService + 3])))
        {
            nextService = content.IndexOf("\n  ", nextService + 3, StringComparison.Ordinal);
        }

        return nextService >= 0 ? content[start..nextService] : content[start..];
    }

    private static string GetRepositoryPath(params string[] segments)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Radish.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("无法定位 Radish 仓库根目录");
        return Path.Combine([directory.FullName, .. segments]);
    }
}
