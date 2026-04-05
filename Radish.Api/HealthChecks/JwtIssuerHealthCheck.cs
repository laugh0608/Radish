using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Radish.Api.HealthChecks;

public sealed class JwtIssuerHealthCheck(IConfiguration configuration) : IHealthCheck
{
    private readonly IConfiguration _configuration = configuration;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var deploymentJwtValidationEnabled = !string.IsNullOrWhiteSpace(_configuration["RADISH_PUBLIC_URL"]);
        var issuer = ResolveJwtIssuer(_configuration);

        if (deploymentJwtValidationEnabled)
        {
            return Task.FromResult(string.IsNullOrWhiteSpace(issuer)
                ? HealthCheckResult.Unhealthy("部署态 JWT Issuer 无法解析，请检查 RADISH_PUBLIC_URL 或 OpenIddict:Server:Issuer。")
                : HealthCheckResult.Healthy($"部署态 JWT Issuer 已对齐: {issuer}"));
        }

        return Task.FromResult(string.IsNullOrWhiteSpace(issuer)
            ? HealthCheckResult.Healthy("当前处于本地 Authority 模式；未解析到公开 Issuer，继续依赖 http://localhost:5200。")
            : HealthCheckResult.Healthy($"当前处于本地 Authority 模式；公开入口解析为 {issuer}。"));
    }

    private static string? ResolveJwtIssuer(IConfiguration configuration)
    {
        var issuer = configuration["OpenIddict:Server:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer))
        {
            issuer = configuration["RADISH_PUBLIC_URL"];
        }

        if (string.IsNullOrWhiteSpace(issuer))
        {
            issuer = configuration["GatewayService:PublicUrl"];
        }

        if (string.IsNullOrWhiteSpace(issuer) || !Uri.TryCreate(issuer, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }
}
