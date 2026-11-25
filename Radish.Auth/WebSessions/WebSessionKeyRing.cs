using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Radish.Auth.OpenIddict;

namespace Radish.Auth.WebSessions;

internal static class WebSessionKeyRing
{
    public static IDataProtectionProvider Create(IConfiguration configuration, string contentRoot)
    {
        var settings = configuration.GetSection(WebAuthClient.SettingsSection.TrimEnd(':'));
        var path = settings["KeysPath"];
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("radish-web requires a persistent OpenIddict:WebSession:KeysPath.");
        }

        var directory = Path.GetFullPath(path, contentRoot);
        if (configuration["DataProtection:KeysPath"] is { Length: > 0 } authKeys &&
            string.Equals(Path.TrimEndingDirectorySeparator(directory),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(authKeys, contentRoot)), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("radish-web requires a key ring separate from the Auth cookie key ring.");
        }

        var current = LoadCertificate(settings.GetSection("KeyCertificate"), contentRoot);
        var previous = settings.GetSection("PreviousKeyCertificates").GetChildren()
            .Select(section => LoadCertificate(section, contentRoot)).Prepend(current).ToArray();

        return DataProtectionProvider.Create(new DirectoryInfo(directory), builder => builder
            .SetApplicationName("Radish.WebSession")
            .ProtectKeysWithCertificate(current)
            .UnprotectKeysWithAnyCertificate(previous));
    }

    private static X509Certificate2 LoadCertificate(IConfiguration settings, string contentRoot)
    {
        var path = settings["Path"];
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("radish-web requires a key-ring encryption certificate.");
        }

        var keyStorage = OperatingSystem.IsMacOS() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet;
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(Path.GetFullPath(path, contentRoot), settings["Password"], keyStorage);
        using var key = certificate.GetRSAPrivateKey();
        if (key is null)
        {
            certificate.Dispose();
            throw new InvalidOperationException("The radish-web key-ring certificate requires an RSA private key.");
        }

        return certificate;
    }
}
