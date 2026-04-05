using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Radish.Api.HealthChecks;

public sealed class JwtSigningCertificateHealthCheck(IConfiguration configuration, IHostEnvironment environment) : IHealthCheck
{
    private readonly IConfiguration _configuration = configuration;
    private readonly IHostEnvironment _environment = environment;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var deploymentJwtValidationEnabled = !string.IsNullOrWhiteSpace(_configuration["RADISH_PUBLIC_URL"]);
        if (!deploymentJwtValidationEnabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy("当前处于本地 Authority 模式，不要求本地 JWT signing 证书。"));
        }

        var configuredPath = _configuration["OpenIddict:Encryption:SigningCertificatePath"];
        var configuredPassword = _configuration["OpenIddict:Encryption:SigningCertificatePassword"];
        if (string.IsNullOrWhiteSpace(configuredPath) || string.IsNullOrWhiteSpace(configuredPassword))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("部署态 JWT signing 证书配置缺失，请检查 OpenIddict:Encryption:SigningCertificatePath / SigningCertificatePassword。"));
        }

        var resolvedPath = ResolveCertificatePath(configuredPath, AppContext.BaseDirectory, _environment.ContentRootPath);
        if (!File.Exists(resolvedPath))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"部署态 JWT signing 证书文件不存在: {resolvedPath}"));
        }

        try
        {
            using var certificate = X509CertificateLoader.LoadPkcs12FromFile(resolvedPath, configuredPassword);
            return Task.FromResult(HealthCheckResult.Healthy($"部署态 JWT signing 证书可读: {certificate.Subject}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"部署态 JWT signing 证书无法读取: {resolvedPath}", ex));
        }
    }

    private static string ResolveCertificatePath(string configuredPath, string basePath, string contentRootPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var baseDirectoryCandidate = Path.GetFullPath(Path.Combine(basePath, configuredPath));
        if (File.Exists(baseDirectoryCandidate))
        {
            return baseDirectoryCandidate;
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }
}
