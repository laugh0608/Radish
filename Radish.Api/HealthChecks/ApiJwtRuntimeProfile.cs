namespace Radish.Api.HealthChecks;

public static class ApiJwtRuntimeProfile
{
    public const string LocalAuthority = "http://localhost:5200";

    public static bool IsDeploymentJwtValidationEnabled(IConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration["RADISH_PUBLIC_URL"]);
    }

    public static string? ResolveJwtIssuer(IConfiguration configuration)
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

    public static string ResolveSigningCertificatePath(
        IConfiguration configuration,
        string basePath,
        string contentRootPath)
    {
        var configuredPath = configuration["OpenIddict:Encryption:SigningCertificatePath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return string.Empty;
        }

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

    public static ApiJwtRuntimeSummary BuildStartupSummary(
        IConfiguration configuration,
        string basePath,
        string contentRootPath)
    {
        var issuer = ResolveJwtIssuer(configuration);
        if (!IsDeploymentJwtValidationEnabled(configuration))
        {
            return new ApiJwtRuntimeSummary(
                ValidationMode: "authority",
                ValidationTarget: LocalAuthority,
                IssuerSummary: string.IsNullOrWhiteSpace(issuer) ? $"未解析，沿用 {LocalAuthority}" : issuer,
                SigningCertificateSummary: "未启用（Authority 模式）");
        }

        return new ApiJwtRuntimeSummary(
            ValidationMode: "local-certificate",
            ValidationTarget: issuer ?? "未解析",
            IssuerSummary: issuer ?? "未解析",
            SigningCertificateSummary: ResolveSigningCertificatePath(configuration, basePath, contentRootPath));
    }
}

public sealed record ApiJwtRuntimeSummary(
    string ValidationMode,
    string ValidationTarget,
    string IssuerSummary,
    string SigningCertificateSummary);
