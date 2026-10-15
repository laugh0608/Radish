using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Radish.Extension.Log;
using Serilog;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class DataProtectionBehaviorTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void RealKeyRing_ShouldPreserveRoundTripIsolationTamperMissingKeyAndRevocation(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var fixture = new DataProtectionFixture(capture);
        var protector = fixture.Provider.CreateProtector(HttpBoundaryCapture.Secret);
        var plaintext = new byte[] { 1, 2, 3, 4 };
        var protectedBytes = protector.Protect(plaintext);
        Assert.Equal(plaintext, protector.Unprotect(protectedBytes));
        Assert.Throws<CryptographicException>(() => fixture.Provider.CreateProtector("different-purpose").Unprotect(protectedBytes));
        var tampered = protectedBytes.ToArray(); tampered[^1] ^= 1;
        Assert.Throws<CryptographicException>(() => protector.Unprotect(tampered));
        var unknownKey = protectedBytes.ToArray(); Guid.NewGuid().ToByteArray().CopyTo(unknownKey, 4);
        Assert.Throws<CryptographicException>(() => protector.Unprotect(unknownKey));
        if (!candidate || environment == "Development") Assert.Contains("data_protection.protector.unprotect_key_missing", capture.Codes);
        var key = Assert.Single(fixture.Manager.GetAllKeys());
        fixture.Manager.RevokeKey(key.KeyId, HttpBoundaryCapture.Secret);
        Assert.True(fixture.Manager.GetAllKeys().Single(k => k.KeyId == key.KeyId).IsRevoked);
        // .NET 10 can serve the stale ring while refreshing asynchronously; wait for an explicit refresh.
        fixture.Refresh();
        Assert.Throws<CryptographicException>(() => protector.Unprotect(protectedBytes));
        var persisted = (IPersistedDataProtector)protector;
        Assert.Equal(plaintext, persisted.DangerousUnprotect(protectedBytes, true, out _, out var revoked));
        Assert.True(revoked);
        if (!candidate || environment == "Development")
        {
            Assert.Contains("data_protection.protector.unprotect_revoked_key_rejected", capture.Codes);
            Assert.Contains("data_protection.protector.unprotect_revoked_key_allowed", capture.Codes);
            Assert.Contains("data_protection.key_manager.key_creation_started", capture.Codes);
        }
        DataProtectionFrameworkEvents.AssertSafe(capture);
        foreach (var persistedKey in fixture.Repository.Elements.Where(e => e.Name == "key"))
            Assert.DoesNotContain(persistedKey.ToString(SaveOptions.DisableFormatting), capture.Output.ToString());
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void MalformedKeys_ShouldBeSkippedButMalformedRevocationShouldAbort(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var fixture = new DataProtectionFixture(capture);
        fixture.Repository.Elements.Add(new XElement("key", new XAttribute("id", HttpBoundaryCapture.Secret), new XElement("secret", HttpBoundaryCapture.Secret)));
        fixture.Repository.Elements.Add(new XElement(HttpBoundaryCapture.Secret));
        Assert.Empty(fixture.Manager.GetAllKeys());
        Assert.Contains("data_protection.key_manager.key_processing_failed", capture.Codes);
        Assert.Contains("data_protection.key_manager.unknown_element_skipped", capture.Codes);
        if (!candidate || environment == "Development") Assert.Contains("data_protection.key_manager.key_processing_failure_detail", capture.Codes);
        fixture.Repository.Elements.Clear(); capture.Clear();
        fixture.Repository.Elements.Add(new XElement("revocation", new XElement("secret", HttpBoundaryCapture.Secret)));
        Assert.ThrowsAny<Exception>(() => fixture.Manager.GetAllKeys());
        Assert.Equal("data_protection.key_manager.revocation_processing_failed", Assert.Single(capture.Events).Code);
        DataProtectionFrameworkEvents.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void PersistenceFailures_ShouldPropagateWithoutClaimingCreationOrRevocationCompleted(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var fixture = new DataProtectionFixture(capture);
        var failure = new IOException(HttpBoundaryCapture.Secret);
        fixture.Repository.Store = _ => throw failure;
        var now = DateTimeOffset.UtcNow;
        Assert.Same(failure, Assert.Throws<IOException>(() => fixture.Manager.CreateNewKey(now, now.AddDays(30))));
        Assert.Empty(fixture.Repository.Elements);
        if (!candidate || environment == "Development") Assert.Contains("data_protection.key_manager.key_creation_started", capture.Codes);
        capture.Clear();
        var token = fixture.Manager.GetCacheExpirationToken();
        Assert.Same(failure, Assert.Throws<IOException>(() => fixture.Manager.RevokeKey(Guid.NewGuid(), HttpBoundaryCapture.Secret)));
        Assert.False(token.IsCancellationRequested);
        Assert.Empty(fixture.Repository.Elements);
        if (!candidate || environment == "Development") Assert.Contains("data_protection.key_manager.key_revocation_started", capture.Codes);
        Assert.DoesNotContain(capture.Codes, c => c.EndsWith("completed", StringComparison.Ordinal));
        DataProtectionFrameworkEvents.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task Preload_ShouldConsumeFailureAndAllowLaterRecoveryWithoutStartingHost(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var fixture = new DataProtectionFixture(capture);
        var failure = new IOException(HttpBoundaryCapture.Secret);
        fixture.Repository.ReadFailure = failure;
        var preload = fixture.Services.GetServices<IHostedService>().Single(s => s.GetType().FullName == DataProtectionLogIdentity.DataProtectionHostedService);
        await preload.StartAsync(CancellationToken.None);
        Assert.Contains("data_protection.key_ring.read_failed", capture.Codes);
        if (!candidate || environment == "Development")
        {
            var entry = Assert.Single(capture.Events, e => e.Code == "data_protection.preload.failed");
            Assert.Equal(candidate ? "Info" : "Information", entry.Level);
            Assert.True(entry.Diagnostic);
        }
        fixture.Repository.ReadFailure = null;
        capture.Clear();
        await preload.StartAsync(CancellationToken.None);
        Assert.DoesNotContain("data_protection.preload.failed", capture.Codes);
        if (!candidate || environment == "Development") Assert.Contains("data_protection.preload.completed", capture.Codes);
        await preload.StopAsync(CancellationToken.None);
        DataProtectionFrameworkEvents.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void DisabledGeneration_ShouldFailWithoutKeysAndUseExpiredFallbackWhenAvailable(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var fixture = new DataProtectionFixture(capture, autoGenerate: false);
        var protector = fixture.Provider.CreateProtector(HttpBoundaryCapture.Secret);
        Assert.Throws<CryptographicException>(() => protector.Protect(new byte[] { 1 }));
        Assert.Contains("data_protection.key_ring.default_unavailable", capture.Codes);
        Assert.Empty(fixture.Repository.Elements);
        capture.Clear();
        var now = DateTimeOffset.UtcNow;
        fixture.Manager.CreateNewKey(now.AddDays(-60), now.AddDays(-30));
        // The preferred expired candidate is excluded from fallback selection.
        fixture.Manager.CreateNewKey(now.AddDays(-90), now.AddDays(-60));
        var payload = protector.Protect(new byte[] { 2 });
        Assert.Equal(new byte[] { 2 }, protector.Unprotect(payload));
        Assert.Contains("data_protection.key_ring.fallback_selected", capture.Codes);
        Assert.Equal(2, fixture.Manager.GetAllKeys().Count);
        DataProtectionFrameworkEvents.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void CertificateResolverFailure_ShouldPropagateWithoutAccessingCertificateStore(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var factory = new DataProtectionLoggerFactory(capture.Factory);
        var resolver = new FailingCertificateResolver();
        var encryptor = new CertificateXmlEncryptor(HttpBoundaryCapture.Secret, resolver, factory);
        Assert.Same(resolver.Failure, Assert.Throws<IOException>(() => encryptor.Encrypt(new XElement("secret", HttpBoundaryCapture.Secret))));
        Assert.Equal(1, resolver.Calls);
        Assert.Equal("data_protection.certificate_encryptor.certificate_resolution_failed", Assert.Single(capture.Events).Code);
        DataProtectionFrameworkEvents.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void FileRepository_ShouldReportWriteAttemptBeforeFinalMoveAndConsumeDeleteFailure(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var path = Path.Combine(Path.GetTempPath(), "radish-dp-" + Guid.NewGuid().ToString("N"));
        var directory = Directory.CreateDirectory(path);
        try
        {
            var factory = new DataProtectionLoggerFactory(capture.Factory);
            var repository = new FileSystemXmlRepository(directory, factory);
            Directory.CreateDirectory(Path.Combine(path, "blocked.xml"));
            Assert.ThrowsAny<IOException>(() => repository.StoreElement(new XElement("secret", HttpBoundaryCapture.Secret), "blocked"));
            if (!candidate || environment == "Development") Assert.Contains("data_protection.file_repository.write_started", capture.Codes);
            Assert.Empty(Directory.GetFiles(path));
            Directory.Delete(Path.Combine(path, "blocked.xml"));
            repository.StoreElement(new XElement("secret", HttpBoundaryCapture.Secret), "delete");
            capture.Clear();
            Assert.False(repository.DeleteElements(elements =>
            {
                foreach (var element in elements)
                {
                    File.Delete(Path.Combine(path, "delete.xml"));
                    Directory.CreateDirectory(Path.Combine(path, "delete.xml"));
                    element.DeletionOrder = 0;
                }
            }));
            Assert.Contains("data_protection.file_repository.delete_failed", capture.Codes);
            DataProtectionFrameworkEvents.AssertSafe(capture);
        }
        finally { Directory.Delete(path, recursive: true); }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void RolloverAndRefreshFailure_ShouldRetainOriginalKeyAndCachedFallback(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var fixture = new DataProtectionFixture(capture);
        var now = DateTimeOffset.UtcNow;
        var original = fixture.Manager.CreateNewKey(now.AddDays(-1), now.AddDays(1));
        capture.Clear();
        var protector = fixture.Provider.CreateProtector(HttpBoundaryCapture.Secret);
        var payload = protector.Protect(new byte[] { 5 });
        var keys = fixture.Manager.GetAllKeys();
        Assert.Equal(2, keys.Count);
        var successor = Assert.Single(keys, k => k.KeyId != original.KeyId);
        Assert.Equal(original.ExpirationDate, successor.ActivationDate);
        Assert.Equal(new byte[] { 5 }, protector.Unprotect(payload));
        if (!candidate || environment == "Development")
        {
            Assert.Contains("data_protection.key_ring.new_key_required", capture.Codes);
            Assert.Contains("data_protection.key_manager.key_creation_started", capture.Codes);
        }
        var failure = new IOException(HttpBoundaryCapture.Secret);
        fixture.Repository.ReadFailure = failure;
        capture.Clear();
        // Drive the real cache clock past expiration without sleeping or changing the process clock.
        var future = DateTime.UtcNow.AddDays(91);
        var thrown = Assert.Throws<TargetInvocationException>(() => fixture.RingAt(future, true));
        Assert.Same(failure, thrown.InnerException);
        Assert.Contains("data_protection.key_ring.refresh_failed", capture.Codes);
        Assert.Equal("io", Assert.Single(capture.Events, e => e.Code == "data_protection.key_ring.refresh_failed").FailureKind);
        capture.Clear();
        Assert.NotNull(fixture.RingAt(future, false));
        Assert.Empty(capture.Events); // The temporarily extended cache avoids a second store read.
        Assert.Equal(new byte[] { 5 }, protector.Unprotect(payload));
        DataProtectionFrameworkEvents.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void DeferredDescriptorFailure_ShouldLogSafeXmlSummaryAndPreserveException(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        var repository = new MemoryDataProtectionRepository();
        using (var writer = new DataProtectionFixture(capture, repository: repository))
        {
            var now = DateTimeOffset.UtcNow;
            writer.Manager.CreateNewKey(now, now.AddDays(30));
        }
        var descriptor = Assert.Single(repository.Elements).Element("descriptor")!;
        descriptor.SetAttributeValue("deserializerType", HttpBoundaryCapture.Secret);
        using var reader = new DataProtectionFixture(capture, repository: repository);
        var key = Assert.Single(reader.Manager.GetAllKeys()); // Descriptor is deferred until CreateEncryptor.
        capture.Clear();
        Assert.ThrowsAny<Exception>(() => key.CreateEncryptor());
        Assert.Contains("data_protection.key_manager.key_processing_failed", capture.Codes);
        if (!candidate || environment == "Development") Assert.Contains("data_protection.key_manager.key_processing_failure_detail", capture.Codes);
        DataProtectionFrameworkEvents.AssertSafe(capture);
    }

    private sealed class FailingCertificateResolver : ICertificateResolver
    {
        internal readonly IOException Failure = new(HttpBoundaryCapture.Secret);
        internal int Calls;
        public X509Certificate2? ResolveCertificate(string thumbprint) { Calls++; throw Failure; }
    }
}

internal sealed class DataProtectionFixture : IDisposable
{
    internal readonly ServiceProvider Services;
    internal readonly MemoryDataProtectionRepository Repository;
    internal IKeyManager Manager => Services.GetRequiredService<IKeyManager>();
    internal IDataProtectionProvider Provider => Services.GetRequiredService<IDataProtectionProvider>();

    internal DataProtectionFixture(HttpBoundaryCapture capture, bool autoGenerate = true, MemoryDataProtectionRepository? repository = null)
    {
        Repository = repository ?? new MemoryDataProtectionRepository();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSerilog(capture.Logger, dispose: false);
        services.ProtectDataProtectionLogging();
        services.AddDataProtection();
        services.Configure<KeyManagementOptions>(options =>
        {
            options.XmlRepository = Repository;
            options.AutoGenerateKeys = autoGenerate;
        });
        Services = services.BuildServiceProvider();
    }
    internal object Refresh()
    {
        var type = typeof(IKeyManager).Assembly.GetType("Microsoft.AspNetCore.DataProtection.KeyManagement.Internal.IKeyRingProvider", true)!;
        var ring = Services.GetRequiredService(type);
        return ring.GetType().GetMethod("RefreshCurrentKeyRing", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ring, null)!;
    }
    internal object RingAt(DateTime utcNow, bool forceRefresh)
    {
        var type = typeof(IKeyManager).Assembly.GetType("Microsoft.AspNetCore.DataProtection.KeyManagement.Internal.IKeyRingProvider", true)!;
        var ring = Services.GetRequiredService(type);
        return ring.GetType().GetMethod("GetCurrentKeyRingCore", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ring, new object[] { utcNow, forceRefresh })!;
    }
    public void Dispose() => Services.Dispose();
}

internal sealed class MemoryDataProtectionRepository : IXmlRepository
{
    internal readonly List<XElement> Elements = [];
    internal Action<XElement>? Store;
    internal Exception? ReadFailure;
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        if (ReadFailure != null) throw ReadFailure;
        return Elements.Select(e => new XElement(e)).ToArray();
    }
    public void StoreElement(XElement element, string friendlyName)
    {
        Store?.Invoke(element);
        Elements.Add(new XElement(element));
    }
}
