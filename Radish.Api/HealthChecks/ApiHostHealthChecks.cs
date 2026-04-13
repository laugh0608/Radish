using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Radish.Api.HealthChecks;

public static class ApiHostHealthChecks
{
    public static readonly IReadOnlyDictionary<string, string[]> Tags =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["self"] = ["host", "self", "minimal"],
            ["jwt-issuer"] = ["host", "jwt", "extended"],
            ["jwt-signing-cert"] = ["host", "jwt", "extended"]
        };

    public static IHealthChecksBuilder AddApiHostHealthChecks(this IServiceCollection services)
    {
        return services.AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy("Radish.Api 进程存活"),
                tags: Tags["self"])
            .AddCheck<JwtIssuerHealthCheck>(
                "jwt-issuer",
                HealthStatus.Unhealthy,
                Tags["jwt-issuer"])
            .AddCheck<JwtSigningCertificateHealthCheck>(
                "jwt-signing-cert",
                HealthStatus.Unhealthy,
                Tags["jwt-signing-cert"]);
    }

    public static bool IsMinimal(HealthCheckRegistration registration)
    {
        return registration.Tags.Contains("minimal");
    }
}
