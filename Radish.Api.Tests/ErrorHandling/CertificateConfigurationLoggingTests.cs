using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using Radish.Extension.Log;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class CertificateConfigurationLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void RealFileLoader_ShouldLoadPublicPemAndPropagateMissingMalformedAndConflictingSources(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var files = new CertificateTestFiles();
        var loader = CreateLoader(capture, files.Root);
        var result = Load(loader, CertificateLoggingFramework.CertificateConfig(files.CertificatePath));
        using var loaded = result.Item1;
        try
        {
            Assert.NotNull(loaded);
            Assert.False(loaded.HasPrivateKey);
            Assert.Equal(files.Certificate.Thumbprint, loaded.Thumbprint);
            Assert.NotNull(result.Item2);
            Assert.Equal(files.Certificate.Thumbprint, Assert.Single(result.Item2.Cast<X509Certificate2>()).Thumbprint);
        }
        finally { DisposeChain(result.Item2); }

        Assert.ThrowsAny<IOException>(() => Load(loader, CertificateLoggingFramework.CertificateConfig(files.CertificatePath + ".missing")));
        Assert.ThrowsAny<CryptographicException>(() => Load(loader, CertificateLoggingFramework.CertificateConfig(files.InvalidPath)));
        var conflict = CertificateLoggingFramework.CertificateConfig(files.CertificatePath);
        CertificateLoggingFramework.Set(conflict, "Subject", HttpBoundaryCapture.Secret);
        Assert.Throws<InvalidOperationException>(() => Load(loader, conflict));
        Assert.Equal((null, null), Load(loader, null));
        // 这些分支直接传播；不得用手工 LogError 冒充框架已产生 6 / 7 事件。
        Assert.Empty(capture.Events);
        CertificateLoggingFramework.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void RealKeyLoader_ShouldAttachOnlyTestKeysAndKeepOriginalFailures(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var files = new CertificateTestFiles();
        using var publicCertificate = X509CertificateLoader.LoadCertificateFromFile(files.CertificatePath);
        var method = CertificateLoggingFramework.Type("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Certificates.CertificateConfigLoader")
            .GetMethod("LoadCertificateKey", CertificateLoggingFramework.Members)!;
        foreach (var encrypted in new[] { false, true })
        {
            using var combined = (X509Certificate2)CertificateLoggingFramework.Invoke(method, null, new object?[]
                { publicCertificate, encrypted ? files.EncryptedKeyPath : files.KeyPath, encrypted ? CertificateTestFiles.Password : null })!;
            Assert.True(combined.HasPrivateKey);
            using var privateKey = combined.GetRSAPrivateKey();
            using var publicKey = publicCertificate.GetRSAPublicKey();
            var bytes = new byte[] { 1, 2, 3 };
            Assert.True(publicKey!.VerifyData(bytes, privateKey!.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
        Assert.ThrowsAny<CryptographicException>(() => CertificateLoggingFramework.Invoke(method, null,
            new object?[] { publicCertificate, files.EncryptedKeyPath, "incorrect-test-password" }));
        Assert.ThrowsAny<ArgumentException>(() => CertificateLoggingFramework.Invoke(method, null,
            new object?[] { publicCertificate, files.InvalidPath, null }));
        var mismatch = Assert.Throws<InvalidOperationException>(() => CertificateLoggingFramework.Invoke(method, null,
            new object?[] { publicCertificate, files.OtherKeyPath, null }));
        Assert.NotNull(mismatch.InnerException);
        Assert.ThrowsAny<IOException>(() => CertificateLoggingFramework.Invoke(method, null,
            new object?[] { publicCertificate, files.KeyPath + ".missing", null }));
        Assert.Empty(capture.Events);
        CertificateLoggingFramework.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task FileLoadFailure_ShouldReachExistingProcessOwnerWithoutLeakingPath(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var files = new CertificateTestFiles();
        using var output = new StringWriter();
        var exit = await RuntimeProcess.RunAsync("api", () =>
        {
            Load(CreateLoader(capture, files.Root), CertificateLoggingFramework.CertificateConfig(files.CertificatePath + ".missing"));
            return Task.CompletedTask;
        }, output);
        Assert.Equal(1, exit);
        Assert.Empty(capture.Events);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("runtime.failed", json.RootElement.GetProperty("eventCode").GetString());
        Assert.Equal("Error", json.RootElement.GetProperty("level").GetString());
        Assert.Equal("io", json.RootElement.GetProperty("properties").GetProperty("failureKind").GetString());
        Assert.DoesNotContain(HttpBoundaryCapture.Secret, output.ToString());
        CertificateLoggingFramework.AssertSafe(capture);
    }

    internal static object CreateLoader(HttpBoundaryCapture capture, string root) => Activator.CreateInstance(
        CertificateLoggingFramework.Type("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Certificates.CertificateConfigLoader"),
        new CertificateTestEnvironment(root), CertificateLoggingFramework.Logger(CertificateLoggingFramework.Config, capture.Factory))!;

    private static (X509Certificate2?, X509Certificate2Collection?) Load(object loader, object? config) =>
        ((X509Certificate2?, X509Certificate2Collection?))CertificateLoggingFramework.Call(loader, "LoadCertificate", config, HttpBoundaryCapture.Secret)!;

    internal static void DisposeChain(X509Certificate2Collection? chain)
    {
        if (chain != null) foreach (var certificate in chain) certificate.Dispose();
    }
}

/// <summary>生成测试专属 PEM；不导入 PFX、系统密钥容器或用户信任存储。</summary>
internal sealed class CertificateTestFiles : IDisposable
{
    public const string Password = "test-only-" + HttpBoundaryCapture.Secret;
    public readonly string Root = Path.Combine(Path.GetTempPath(), "radish-cert-" + HttpBoundaryCapture.Secret + "-" + Guid.NewGuid().ToString("N"));
    public string CertificatePath => Path.Combine(Root, "certificate.pem");
    public string KeyPath => Path.Combine(Root, "key.pem");
    public string EncryptedKeyPath => Path.Combine(Root, "encrypted-key.pem");
    public string OtherKeyPath => Path.Combine(Root, "other-key.pem");
    public string InvalidPath => Path.Combine(Root, "invalid.pem");
    public readonly X509Certificate2 Certificate;

    public CertificateTestFiles()
    {
        Directory.CreateDirectory(Root);
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=" + HttpBoundaryCapture.Secret, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        try
        {
            File.WriteAllText(CertificatePath, Certificate.ExportCertificatePem());
            File.WriteAllText(KeyPath, key.ExportPkcs8PrivateKeyPem());
            File.WriteAllText(EncryptedKeyPath, key.ExportEncryptedPkcs8PrivateKeyPem(Password,
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)));
            using var other = RSA.Create(2048);
            File.WriteAllText(OtherKeyPath, other.ExportPkcs8PrivateKeyPem());
            File.WriteAllText(InvalidPath, HttpBoundaryCapture.Secret);
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        Certificate.Dispose();
        Directory.Delete(Root, recursive: true);
    }
}
