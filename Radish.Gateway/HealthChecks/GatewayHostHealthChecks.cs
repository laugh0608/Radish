using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Radish.Gateway.HealthChecks;

public static class GatewayHostHealthChecks
{
    public static IReadOnlyList<GatewayHealthTarget> CreateHealthTargets(IConfiguration configuration)
    {
        var targets = new List<GatewayHealthTarget>();

        var apiBaseUrl = configuration["DownstreamServices:ApiService:BaseUrl"];
        var apiHealthPath = configuration["DownstreamServices:ApiService:HealthCheckPath"];
        if (!string.IsNullOrWhiteSpace(apiBaseUrl) && !string.IsNullOrWhiteSpace(apiHealthPath))
        {
            targets.Add(new GatewayHealthTarget(
                "api-service",
                $"{apiBaseUrl.TrimEnd('/')}{apiHealthPath}",
                ["downstream", "api", "minimal"],
                HealthStatus.Unhealthy));
        }

        var authBaseUrl = configuration["DownstreamServices:AuthService:BaseUrl"];
        var authHealthPath = configuration["DownstreamServices:AuthService:HealthCheckPath"];
        if (!string.IsNullOrWhiteSpace(authBaseUrl) && !string.IsNullOrWhiteSpace(authHealthPath))
        {
            targets.Add(new GatewayHealthTarget(
                "auth-service",
                $"{authBaseUrl.TrimEnd('/')}{authHealthPath}",
                ["downstream", "auth", "minimal"],
                HealthStatus.Unhealthy));
        }

        var consoleBaseUrl = configuration["ReverseProxy:Clusters:consoleCluster:Destinations:console:Address"];
        if (!string.IsNullOrWhiteSpace(consoleBaseUrl))
        {
            targets.Add(new GatewayHealthTarget(
                "console-service",
                $"{consoleBaseUrl.TrimEnd('/')}/healthz",
                ["downstream", "console", "extended"],
                HealthStatus.Degraded));
        }

        return targets;
    }

    public static IReadOnlyDictionary<string, string[]> CreateTags(IConfiguration configuration)
    {
        return CreateHealthTargets(configuration)
            .ToDictionary(target => target.Name, target => target.Tags, StringComparer.OrdinalIgnoreCase);
    }

    public static IHealthChecksBuilder AddGatewayHostHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyDictionary<string, string[]> tags)
    {
        var healthChecksBuilder = services.AddHealthChecks();

        foreach (var target in CreateHealthTargets(configuration))
        {
            healthChecksBuilder.AddUrlGroup(
                new Uri(target.Url),
                name: target.Name,
                failureStatus: target.FailureStatus,
                tags: tags[target.Name]);
        }

        return healthChecksBuilder;
    }

    public static bool IsMinimal(HealthCheckRegistration registration)
    {
        return registration.Tags.Contains("minimal");
    }
}

public sealed record GatewayHealthTarget(
    string Name,
    string Url,
    string[] Tags,
    HealthStatus FailureStatus);
