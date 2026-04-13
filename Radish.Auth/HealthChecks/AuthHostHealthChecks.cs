using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Radish.Auth.HealthChecks;

public static class AuthHostHealthChecks
{
    public static readonly IReadOnlyDictionary<string, string[]> Tags =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["self"] = ["host", "self", "minimal"],
            ["oidc-issuer"] = ["host", "oidc", "extended"],
            ["oidc-signing-cert"] = ["host", "oidc", "extended"],
            ["oidc-encryption-cert"] = ["host", "oidc", "extended"]
        };

    public static IHealthChecksBuilder AddAuthHostHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services.AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy("Radish.Auth 进程存活"),
                tags: Tags["self"])
            .AddCheck<OidcIssuerHealthCheck>(
                "oidc-issuer",
                HealthStatus.Unhealthy,
                Tags["oidc-issuer"])
            .AddCheck(
                "oidc-signing-cert",
                new OidcCertificateHealthCheck(configuration, environment, "Signing"),
                HealthStatus.Unhealthy,
                Tags["oidc-signing-cert"])
            .AddCheck(
                "oidc-encryption-cert",
                new OidcCertificateHealthCheck(configuration, environment, "Encryption"),
                HealthStatus.Unhealthy,
                Tags["oidc-encryption-cert"]);
    }

    public static bool IsMinimal(HealthCheckRegistration registration)
    {
        return registration.Tags.Contains("minimal");
    }
}
