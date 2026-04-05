using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Radish.Gateway.HealthChecks;

public static class GatewayHostHealthChecks
{
    public static IReadOnlyDictionary<string, string[]> CreateTags(IConfiguration configuration)
    {
        var tags = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var apiBaseUrl = configuration["DownstreamServices:ApiService:BaseUrl"];
        var apiHealthPath = configuration["DownstreamServices:ApiService:HealthCheckPath"];
        if (!string.IsNullOrWhiteSpace(apiBaseUrl) && !string.IsNullOrWhiteSpace(apiHealthPath))
        {
            tags["api-service"] = ["downstream", "api", "minimal"];
        }

        var authBaseUrl = configuration["DownstreamServices:AuthService:BaseUrl"];
        var authHealthPath = configuration["DownstreamServices:AuthService:HealthCheckPath"];
        if (!string.IsNullOrWhiteSpace(authBaseUrl) && !string.IsNullOrWhiteSpace(authHealthPath))
        {
            tags["auth-service"] = ["downstream", "auth", "minimal"];
        }

        var consoleBaseUrl = configuration["ReverseProxy:Clusters:consoleCluster:Destinations:console:Address"];
        if (!string.IsNullOrWhiteSpace(consoleBaseUrl))
        {
            tags["console-service"] = ["downstream", "console", "extended"];
        }

        return tags;
    }

    public static IHealthChecksBuilder AddGatewayHostHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyDictionary<string, string[]> tags)
    {
        var healthChecksBuilder = services.AddHealthChecks();

        var apiBaseUrl = configuration["DownstreamServices:ApiService:BaseUrl"];
        var apiHealthPath = configuration["DownstreamServices:ApiService:HealthCheckPath"];
        if (!string.IsNullOrWhiteSpace(apiBaseUrl) && !string.IsNullOrWhiteSpace(apiHealthPath))
        {
            var apiHealthUrl = $"{apiBaseUrl.TrimEnd('/')}{apiHealthPath}";
            healthChecksBuilder.AddUrlGroup(
                new Uri(apiHealthUrl),
                name: "api-service",
                tags: tags["api-service"]);
        }

        var authBaseUrl = configuration["DownstreamServices:AuthService:BaseUrl"];
        var authHealthPath = configuration["DownstreamServices:AuthService:HealthCheckPath"];
        if (!string.IsNullOrWhiteSpace(authBaseUrl) && !string.IsNullOrWhiteSpace(authHealthPath))
        {
            var authHealthUrl = $"{authBaseUrl.TrimEnd('/')}{authHealthPath}";
            healthChecksBuilder.AddUrlGroup(
                new Uri(authHealthUrl),
                name: "auth-service",
                tags: tags["auth-service"]);
        }

        var consoleBaseUrl = configuration["ReverseProxy:Clusters:consoleCluster:Destinations:console:Address"];
        if (!string.IsNullOrWhiteSpace(consoleBaseUrl))
        {
            var consoleHealthUrl = $"{consoleBaseUrl.TrimEnd('/')}/healthz";
            healthChecksBuilder.AddUrlGroup(
                new Uri(consoleHealthUrl),
                name: "console-service",
                failureStatus: HealthStatus.Degraded,
                tags: tags["console-service"]);
        }

        return healthChecksBuilder;
    }

    public static bool IsMinimal(HealthCheckRegistration registration)
    {
        return registration.Tags.Contains("minimal");
    }
}
