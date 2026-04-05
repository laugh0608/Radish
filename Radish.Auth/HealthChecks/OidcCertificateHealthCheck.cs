using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Radish.Auth.HealthChecks;

public sealed class OidcCertificateHealthCheck(
    IConfiguration configuration,
    IHostEnvironment environment,
    string certificateType) : IHealthCheck
{
    private readonly IConfiguration _configuration = configuration;
    private readonly IHostEnvironment _environment = environment;
    private readonly string _certificateType = certificateType;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var certificateSection = _configuration.GetSection("OpenIddict:Encryption");
        var useDevelopmentKeys = certificateSection.GetValue<bool?>("UseDevelopmentKeys") ?? false;
        if (useDevelopmentKeys)
        {
            return Task.FromResult(HealthCheckResult.Healthy($"OIDC {_certificateType} 已切到开发密钥模式。"));
        }

        var configuredPath = certificateSection.GetValue<string>($"{_certificateType}CertificatePath");
        var configuredPassword = certificateSection.GetValue<string>($"{_certificateType}CertificatePassword");
        if (string.IsNullOrWhiteSpace(configuredPath) || string.IsNullOrWhiteSpace(configuredPassword))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"OIDC {_certificateType} 证书配置缺失，请检查 OpenIddict:Encryption:{_certificateType}CertificatePath / {_certificateType}CertificatePassword。"));
        }

        var resolvedPath = ResolveCertificatePath(configuredPath);
        if (!File.Exists(resolvedPath))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"OIDC {_certificateType} 证书文件不存在: {resolvedPath}"));
        }

        try
        {
            using var certificate = X509CertificateLoader.LoadPkcs12FromFile(resolvedPath, configuredPassword);
            return Task.FromResult(HealthCheckResult.Healthy($"OIDC {_certificateType} 证书可读: {certificate.Subject}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"OIDC {_certificateType} 证书无法读取: {resolvedPath}", ex));
        }
    }

    private string ResolveCertificatePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, configuredPath));
    }
}
