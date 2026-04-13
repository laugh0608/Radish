using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Radish.Auth.HealthChecks;

public sealed class OidcIssuerHealthCheck(IConfiguration configuration) : IHealthCheck
{
    private readonly IConfiguration _configuration = configuration;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var configuredIssuer = _configuration.GetValue<string>("OpenIddict:Server:Issuer");
        if (string.IsNullOrWhiteSpace(configuredIssuer))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("OIDC Issuer 配置缺失，请检查 OpenIddict:Server:Issuer。"));
        }

        if (!Uri.TryCreate(configuredIssuer, UriKind.Absolute, out var issuerUri))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"OIDC Issuer 不是合法绝对地址: {configuredIssuer}"));
        }

        if (!string.Equals(issuerUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(issuerUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"OIDC Issuer 协议不受支持: {issuerUri.Scheme}"));
        }

        return Task.FromResult(HealthCheckResult.Healthy($"OIDC Issuer 已对齐: {AuthOidcRuntimeProfile.ResolveIssuer(_configuration)}"));
    }
}
