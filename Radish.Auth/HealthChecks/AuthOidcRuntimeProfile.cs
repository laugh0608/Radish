namespace Radish.Auth.HealthChecks;

public static class AuthOidcRuntimeProfile
{
    public static bool UsesDevelopmentKeys(IConfiguration configuration)
    {
        return configuration.GetSection("OpenIddict:Encryption").GetValue<bool?>("UseDevelopmentKeys") ?? false;
    }

    public static string? ResolveIssuer(IConfiguration configuration)
    {
        var issuer = configuration.GetValue<string>("OpenIddict:Server:Issuer");
        if (string.IsNullOrWhiteSpace(issuer) || !Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri))
        {
            return null;
        }

        return issuerUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    public static string ResolveCertificatePath(
        IConfiguration configuration,
        IHostEnvironment environment,
        string certificateType)
    {
        var configuredPath = configuration.GetValue<string>($"OpenIddict:Encryption:{certificateType}CertificatePath");
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
    }

    public static AuthOidcRuntimeSummary BuildStartupSummary(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (UsesDevelopmentKeys(configuration))
        {
            return new AuthOidcRuntimeSummary(
                IssuerSummary: ResolveIssuer(configuration) ?? "未解析",
                KeyMode: "development-keys",
                SigningCertificateSummary: "开发密钥",
                EncryptionCertificateSummary: "开发密钥");
        }

        return new AuthOidcRuntimeSummary(
            IssuerSummary: ResolveIssuer(configuration) ?? "未解析",
            KeyMode: "file-certificates",
            SigningCertificateSummary: ResolveCertificatePath(configuration, environment, "Signing"),
            EncryptionCertificateSummary: ResolveCertificatePath(configuration, environment, "Encryption"));
    }
}

public sealed record AuthOidcRuntimeSummary(
    string IssuerSummary,
    string KeyMode,
    string SigningCertificateSummary,
    string EncryptionCertificateSummary);
