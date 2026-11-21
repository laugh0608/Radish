using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Radish.Auth.WebSessions;
using Xunit;

namespace Radish.Api.Tests.Auth;

public sealed class WebSessionKeyRingTest
{
    [Fact]
    public void ProtectedKeysSurviveRestartAndConfigurationOfANewCertificate()
    {
        var scratch = Directory.CreateTempSubdirectory("radish-web-keys-");
        try
        {
            const string password = "test-certificate-password";
            ExportCertificate(Path.Combine(scratch.FullName, "old.pfx"), password);
            ExportCertificate(Path.Combine(scratch.FullName, "new.pfx"), password);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenIddict:WebSession:KeysPath"] = "keys",
                ["OpenIddict:WebSession:KeyCertificate:Path"] = "old.pfx",
                ["OpenIddict:WebSession:KeyCertificate:Password"] = password
            }).Build();
            var session = Guid.NewGuid();
            var issued = WebSessionProtection.FromKeyRing(config, scratch.FullName).ProtectTokens(session, "stored-test-refresh");
            var keyFile = Assert.Single(Directory.GetFiles(Path.Combine(scratch.FullName, "keys"), "key-*.xml"));
            var xml = XDocument.Load(keyFile);
            Assert.Contains(xml.Descendants(), element => element.Name.LocalName == "EncryptedData");
            Assert.DoesNotContain(xml.Descendants(), element => element.Name.LocalName == "masterKey");

            config["OpenIddict:WebSession:KeyCertificate:Path"] = "new.pfx";
            config["OpenIddict:WebSession:PreviousKeyCertificates:0:Path"] = "old.pfx";
            config["OpenIddict:WebSession:PreviousKeyCertificates:0:Password"] = password;
            var restarted = WebSessionProtection.FromKeyRing(config, scratch.FullName);
            Assert.Equal("stored-test-refresh", restarted.ReadTokens<string>(session, issued));
            Assert.Throws<CryptographicException>(() => restarted.ReadTokens<string>(Guid.NewGuid(), issued));
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    private static void ExportCertificate(string path, string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Radish Web temporary key ring", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
    }
}
