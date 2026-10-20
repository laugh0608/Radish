using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Radish.Extension.Log;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

internal sealed record DataProtectionFrameworkEvent(string Source, int Id, string Name, LogLevel Level, string Code, string Method);

internal static class DataProtectionFrameworkEvents
{
    internal static readonly Type LogType = Assembly.Load("Microsoft.AspNetCore.DataProtection").GetType("Microsoft.Extensions.Logging.LoggingExtensions", true)!;
    internal static readonly DataProtectionFrameworkEvent[] All =
    [
        new(DataProtectionLogIdentity.EphemeralDataProtectionProvider, 45, "UsingEphemeralDataProtectionProvider", LogLevel.Information, "data_protection.ephemeral_provider.selected", "UsingEphemeralDataProtectionProvider"),
        new(DataProtectionLogIdentity.NullXmlEncryptor, 44, "EncryptingUsingNullEncryptor", LogLevel.Warning, "data_protection.null_encryptor.encryption_unprotected", "EncryptingUsingNullEncryptor"),
        new(DataProtectionLogIdentity.DpapiXmlEncryptor, 26, "EncryptingToWindowsDPAPIForCurrentUserAccount", LogLevel.Debug, "data_protection.dpapi_encryptor.user_encryption_started", "EncryptingToWindowsDPAPIForCurrentUserAccount"),
        new(DataProtectionLogIdentity.DpapiXmlEncryptor, 54, "ErrorOccurredWhileEncryptingToWindowsDPAPI", LogLevel.Error, "data_protection.dpapi_encryptor.encryption_failed", "ErrorOccurredWhileEncryptingToWindowsDPAPI"),
        new(DataProtectionLogIdentity.DpapiXmlEncryptor, 55, "EncryptingToWindowsDPAPIForLocalMachineAccount", LogLevel.Debug, "data_protection.dpapi_encryptor.machine_encryption_started", "EncryptingToWindowsDPAPIForLocalMachineAccount"),
        new(DataProtectionLogIdentity.DpapiNGXmlDecryptor, 42, "DecryptingSecretElementUsingWindowsDPAPING", LogLevel.Debug, "data_protection.dpaping_decryptor.decryption_started", "DecryptingSecretElementUsingWindowsDPAPING"),
        new(DataProtectionLogIdentity.DpapiNGXmlDecryptor, 43, "ExceptionOccurredTryingToDecryptElement", LogLevel.Error, "data_protection.dpaping_decryptor.decryption_failed", "ExceptionOccurredTryingToDecryptElement"),
        new(DataProtectionLogIdentity.DpapiXmlDecryptor, 43, "ExceptionOccurredTryingToDecryptElement", LogLevel.Error, "data_protection.dpapi_decryptor.decryption_failed", "ExceptionOccurredTryingToDecryptElement"),
        new(DataProtectionLogIdentity.DpapiXmlDecryptor, 51, "DecryptingSecretElementUsingWindowsDPAPI", LogLevel.Debug, "data_protection.dpapi_decryptor.decryption_started", "DecryptingSecretElementUsingWindowsDPAPI"),
        new(DataProtectionLogIdentity.CertificateXmlEncryptor, 28, "ErrorOccurredWhileEncryptingToX509CertificateWithThumbprint", LogLevel.Error, "data_protection.certificate_encryptor.encryption_failed", "AnErrorOccurredWhileEncryptingToX509CertificateWithThumbprint"),
        new(DataProtectionLogIdentity.CertificateXmlEncryptor, 29, "EncryptingToX509CertificateWithThumbprint", LogLevel.Debug, "data_protection.certificate_encryptor.encryption_started", "EncryptingToX509CertificateWithThumbprint"),
        new(DataProtectionLogIdentity.CertificateXmlEncryptor, 30, "ExceptionOccurredWhileTryingToResolveCertificateWithThumbprint", LogLevel.Error, "data_protection.certificate_encryptor.certificate_resolution_failed", "ExceptionWhileTryingToResolveCertificateWithThumbprint"),
        new(DataProtectionLogIdentity.DpapiNGXmlEncryptor, 27, "EncryptingToWindowsDPAPINGUsingProtectionDescriptorRule", LogLevel.Debug, "data_protection.dpaping_encryptor.encryption_started", "EncryptingToWindowsDPAPINGUsingProtectionDescriptorRule"),
        new(DataProtectionLogIdentity.DpapiNGXmlEncryptor, 56, "ErrorOccurredWhileEncryptingToWindowsDPAPING", LogLevel.Error, "data_protection.dpaping_encryptor.encryption_failed", "ErrorOccurredWhileEncryptingToWindowsDPAPING"),
        new(DataProtectionLogIdentity.DefaultKeyResolver, 12, "KeyIsIneligibleToBeTheDefaultKeyBecauseItsMethodFailed", LogLevel.Warning, "data_protection.key_resolver.candidate_failed", "KeyIsIneligibleToBeTheDefaultKeyBecauseItsMethodFailed"),
        new(DataProtectionLogIdentity.DefaultKeyResolver, 13, "ConsideringKeyWithExpirationDateAsDefaultKey", LogLevel.Debug, "data_protection.key_resolver.candidate_considered", "ConsideringKeyWithExpirationDateAsDefaultKey"),
        new(DataProtectionLogIdentity.DefaultKeyResolver, 14, "KeyIsNoLongerUnderConsiderationAsDefault", LogLevel.Debug, "data_protection.key_resolver.candidate_rejected", "KeyIsNoLongerUnderConsiderationAsDefault"),
        new(DataProtectionLogIdentity.DefaultKeyResolver, 53, "RepositoryContainsNoViableDefaultKey", LogLevel.Debug, "data_protection.key_resolver.default_unavailable", "RepositoryContainsNoViableDefaultKey"),
        new(DataProtectionLogIdentity.DefaultKeyResolver, 73, "RetryingMethodOfKeyAfterFailure", LogLevel.Debug, "data_protection.key_resolver.candidate_retry", "RetryingMethodOfKeyAfterFailure"),
        new(DataProtectionLogIdentity.XmlKeyManager, 15, "UnknownElementWithNameFoundInKeyringSkipping", LogLevel.Warning, "data_protection.key_manager.unknown_element_skipped", "UnknownElementWithNameFoundInKeyringSkipping"),
        new(DataProtectionLogIdentity.XmlKeyManager, 16, "MarkedKeyAsRevokedInTheKeyring", LogLevel.Debug, "data_protection.key_manager.key_marked_revoked", "MarkedKeyAsRevokedInTheKeyring"),
        new(DataProtectionLogIdentity.XmlKeyManager, 17, "TriedToProcessRevocationOfKeyButNoSuchKeyWasFound", LogLevel.Warning, "data_protection.key_manager.revocation_key_missing", "TriedToProcessRevocationOfKeyButNoSuchKeyWasFound"),
        new(DataProtectionLogIdentity.XmlKeyManager, 18, "FoundKey", LogLevel.Debug, "data_protection.key_manager.key_found", "FoundKey"),
        new(DataProtectionLogIdentity.XmlKeyManager, 19, "FoundRevocationOfAllKeysCreatedPriorTo", LogLevel.Debug, "data_protection.key_manager.date_revocation_found", "FoundRevocationOfAllKeysCreatedPriorTo"),
        new(DataProtectionLogIdentity.XmlKeyManager, 20, "FoundRevocationOfKey", LogLevel.Debug, "data_protection.key_manager.key_revocation_found", "FoundRevocationOfKey"),
        new(DataProtectionLogIdentity.XmlKeyManager, 21, "ExceptionWhileProcessingRevocationElement", LogLevel.Error, "data_protection.key_manager.revocation_processing_failed", "ExceptionWhileProcessingRevocationElement"),
        new(DataProtectionLogIdentity.XmlKeyManager, 22, "RevokingAllKeysAsOfForReason", LogLevel.Information, "data_protection.key_manager.date_revocation_started", "RevokingAllKeysAsOfForReason"),
        new(DataProtectionLogIdentity.XmlKeyManager, 23, "KeyCacheExpirationTokenTriggeredByOperation", LogLevel.Debug, "data_protection.key_manager.cache_expiration_triggered", "KeyCacheExpirationTokenTriggeredByOperation"),
        new(DataProtectionLogIdentity.XmlKeyManager, 24, "ExceptionOccurredWhileProcessingTheKeyElement", LogLevel.Error, "data_protection.key_manager.key_processing_failed", "ExceptionWhileProcessingKeyElement"),
        new(DataProtectionLogIdentity.XmlKeyManager, 25, "ExceptionOccurredWhileProcessingTheKeyElementDebug", LogLevel.Trace, "data_protection.key_manager.key_processing_failure_detail", "AnExceptionOccurredWhileProcessingElementDebug"),
        new(DataProtectionLogIdentity.XmlKeyManager, 32, "DescriptorDeserializerTypeForKeyIs", LogLevel.Debug, "data_protection.key_manager.deserializer_selected", "DescriptorDeserializerTypeForKeyIs"),
        new(DataProtectionLogIdentity.XmlKeyManager, 33, "KeyEscrowSinkFoundWritingKeyToEscrow", LogLevel.Debug, "data_protection.key_manager.escrow_write_started", "KeyEscrowSinkFoundWritingKeyToEscrow"),
        new(DataProtectionLogIdentity.XmlKeyManager, 34, "NoKeyEscrowSinkFoundNotWritingKeyToEscrow", LogLevel.Debug, "data_protection.key_manager.escrow_not_configured", "NoKeyEscrowSinkFoundNotWritingKeyToEscrow"),
        new(DataProtectionLogIdentity.XmlKeyManager, 35, "NoXMLEncryptorConfiguredKeyMayBePersistedToStorageInUnencryptedForm", LogLevel.Warning, "data_protection.key_manager.xml_encryptor_missing", "NoXMLEncryptorConfiguredKeyMayBePersistedToStorageInUnencryptedForm"),
        new(DataProtectionLogIdentity.XmlKeyManager, 36, "RevokingKeyForReason", LogLevel.Information, "data_protection.key_manager.key_revocation_started", "RevokingKeyForReason"),
        new(DataProtectionLogIdentity.XmlKeyManager, 58, "CreatingKey", LogLevel.Information, "data_protection.key_manager.key_creation_started", "CreatingKey"),
        new(DataProtectionLogIdentity.XmlKeyManager, 59, "UsingEphemeralKeyRepository", LogLevel.Warning, "data_protection.key_manager.ephemeral_repository_selected", "UsingEphemeralKeyRepository"),
        new(DataProtectionLogIdentity.XmlKeyManager, 61, "UsingRegistryAsKeyRepositoryWithDPAPI", LogLevel.Information, "data_protection.key_manager.registry_repository_selected", "UsingRegistryAsKeyRepositoryWithDPAPI"),
        new(DataProtectionLogIdentity.XmlKeyManager, 62, "UsingProfileAsKeyRepository", LogLevel.Information, "data_protection.key_manager.profile_repository_selected", "UsingProfileAsKeyRepository"),
        new(DataProtectionLogIdentity.XmlKeyManager, 63, "UsingProfileAsKeyRepositoryWithDPAPI", LogLevel.Information, "data_protection.key_manager.protected_profile_repository_selected", "UsingProfileAsKeyRepositoryWithDPAPI"),
        new(DataProtectionLogIdentity.XmlKeyManager, 64, "UsingAzureAsKeyRepository", LogLevel.Information, "data_protection.key_manager.azure_repository_selected", "UsingAzureAsKeyRepository"),
        new(DataProtectionLogIdentity.XmlKeyManager, 78, "KeyRevokedMultipleTimes", LogLevel.Trace, "data_protection.key_manager.repeated_revocation", "KeyRevokedMultipleTimes"),
        new(DataProtectionLogIdentity.XmlKeyManager, 80, "DeletingKey", LogLevel.Debug, "data_protection.key_manager.key_deletion_started", "DeletingKey"),
        new(DataProtectionLogIdentity.KeyRingProvider, 1, "UsingFallbackKeyWithExpirationAsDefaultKey", LogLevel.Warning, "data_protection.key_ring.fallback_selected", "UsingFallbackKeyWithExpirationAsDefaultKey"),
        new(DataProtectionLogIdentity.KeyRingProvider, 2, "UsingKeyAsDefaultKey", LogLevel.Debug, "data_protection.key_ring.default_selected", "UsingKeyAsDefaultKey"),
        new(DataProtectionLogIdentity.KeyRingProvider, 46, "ExistingCachedKeyRingIsExpiredRefreshing", LogLevel.Debug, "data_protection.key_ring.refresh_started", "ExistingCachedKeyRingIsExpired"),
        new(DataProtectionLogIdentity.KeyRingProvider, 47, "ErrorOccurredWhileRefreshingKeyRing", LogLevel.Error, "data_protection.key_ring.refresh_failed", "ErrorOccurredWhileRefreshingKeyRing"),
        new(DataProtectionLogIdentity.KeyRingProvider, 48, "ErrorOccurredWhileReadingKeyRing", LogLevel.Error, "data_protection.key_ring.read_failed", "ErrorOccurredWhileReadingKeyRing"),
        new(DataProtectionLogIdentity.KeyRingProvider, 49, "KeyRingDoesNotContainValidDefaultKey", LogLevel.Error, "data_protection.key_ring.default_unavailable", "KeyRingDoesNotContainValidDefaultKey"),
        new(DataProtectionLogIdentity.KeyRingProvider, 57, "PolicyResolutionStatesThatANewKeyShouldBeAddedToTheKeyRing", LogLevel.Debug, "data_protection.key_ring.new_key_required", "PolicyResolutionStatesThatANewKeyShouldBeAddedToTheKeyRing"),
        new(DataProtectionLogIdentity.KeyRingProvider, 72, "KeyRingDefaultKeyIsRevoked", LogLevel.Error, "data_protection.key_ring.default_revoked", "KeyRingDefaultKeyIsRevoked"),
        new(DataProtectionLogIdentity.KeyRingBasedDataProtector, 5, "PerformingUnprotectOperationToKeyWithPurposes", LogLevel.Trace, "data_protection.protector.unprotect_started", "PerformingUnprotectOperationToKeyWithPurposes"),
        new(DataProtectionLogIdentity.KeyRingBasedDataProtector, 6, "KeyWasNotFoundInTheKeyRingUnprotectOperationCannotProceed", LogLevel.Trace, "data_protection.protector.unprotect_key_missing", "KeyWasNotFoundInTheKeyRingUnprotectOperationCannotProceed"),
        new(DataProtectionLogIdentity.KeyRingBasedDataProtector, 7, "KeyWasRevokedCallerRequestedUnprotectOperationProceedRegardless", LogLevel.Debug, "data_protection.protector.unprotect_revoked_key_allowed", "KeyWasRevokedCallerRequestedUnprotectOperationProceedRegardless"),
        new(DataProtectionLogIdentity.KeyRingBasedDataProtector, 8, "KeyWasRevokedUnprotectOperationCannotProceed", LogLevel.Debug, "data_protection.protector.unprotect_revoked_key_rejected", "KeyWasRevokedUnprotectOperationCannotProceed"),
        new(DataProtectionLogIdentity.KeyRingBasedDataProtector, 31, "PerformingProtectOperationToKeyWithPurposes", LogLevel.Trace, "data_protection.protector.protect_started", "PerformingProtectOperationToKeyWithPurposes"),
        new(DataProtectionLogIdentity.KeyManagementOptionsPostSetup, 61, "IgnoringReadOnlyConfigurationForNonDefaultOptions", LogLevel.Trace, "data_protection.options.nondefault_options_ignored", "IgnoringReadOnlyConfigurationForNonDefaultOptions"),
        new(DataProtectionLogIdentity.KeyManagementOptionsPostSetup, 62, "UsingReadOnlyKeyConfiguration", LogLevel.Information, "data_protection.options.read_only_selected", "UsingReadOnlyKeyConfiguration"),
        new(DataProtectionLogIdentity.KeyManagementOptionsPostSetup, 63, "NotUsingReadOnlyKeyConfigurationBecauseOfRepository", LogLevel.Debug, "data_protection.options.read_only_repository_rejected", "NotUsingReadOnlyKeyConfigurationBecauseOfRepository"),
        new(DataProtectionLogIdentity.KeyManagementOptionsPostSetup, 64, "NotUsingReadOnlyKeyConfigurationBecauseOfEncryptor", LogLevel.Debug, "data_protection.options.read_only_encryptor_rejected", "NotUsingReadOnlyKeyConfigurationBecauseOfEncryptor"),
        new(DataProtectionLogIdentity.DataProtectionHostedService, 65, "KeyRingWasLoadedOnStartup", LogLevel.Debug, "data_protection.preload.completed", "KeyRingWasLoadedOnStartup"),
        new(DataProtectionLogIdentity.DataProtectionHostedService, 66, "KeyRingFailedToLoadOnStartup", LogLevel.Information, "data_protection.preload.failed", "KeyRingFailedToLoadOnStartup"),
        new(DataProtectionLogIdentity.FileSystemXmlRepository, 37, "ReadingDataFromFile", LogLevel.Debug, "data_protection.file_repository.read_started", "ReadingDataFromFile"),
        new(DataProtectionLogIdentity.FileSystemXmlRepository, 38, "NameIsNotSafeFileName", LogLevel.Debug, "data_protection.file_repository.unsafe_name_replaced", "NameIsNotSafeFileName"),
        new(DataProtectionLogIdentity.FileSystemXmlRepository, 39, "WritingDataToFile", LogLevel.Information, "data_protection.file_repository.write_started", "WritingDataToFile"),
        new(DataProtectionLogIdentity.FileSystemXmlRepository, 60, "UsingEphemeralFileSystemLocationInContainer", LogLevel.Warning, "data_protection.file_repository.ephemeral_container_location", "UsingEphemeralFileSystemLocationInContainer"),
        new(DataProtectionLogIdentity.FileSystemXmlRepository, 74, "DeletingFile", LogLevel.Debug, "data_protection.file_repository.delete_started", "DeletingFile"),
        new(DataProtectionLogIdentity.FileSystemXmlRepository, 75, "FailedToDeleteFile", LogLevel.Error, "data_protection.file_repository.delete_failed", "FailedToDeleteFile"),
        new(DataProtectionLogIdentity.EphemeralXmlRepository, 50, "UsingInMemoryRepository", LogLevel.Warning, "data_protection.memory_repository.selected", "UsingInmemoryRepository"),
        new(DataProtectionLogIdentity.RegistryXmlRepository, 40, "ReadingDataFromRegistryKeyValue", LogLevel.Debug, "data_protection.registry_repository.read_started", "ReadingDataFromRegistryKeyValue"),
        new(DataProtectionLogIdentity.RegistryXmlRepository, 41, "NameIsNotSafeRegistryValueName", LogLevel.Debug, "data_protection.registry_repository.unsafe_name_replaced", "NameIsNotSafeRegistryValueName"),
        new(DataProtectionLogIdentity.RegistryXmlRepository, 76, "RemovingDataFromRegistryKeyValue", LogLevel.Debug, "data_protection.registry_repository.delete_started", "RemovingDataFromRegistryKeyValue"),
        new(DataProtectionLogIdentity.RegistryXmlRepository, 77, "FailedToRemoveDataFromRegistryKeyValue", LogLevel.Error, "data_protection.registry_repository.delete_failed", "FailedToRemoveDataFromRegistryKeyValue"),
        new(DataProtectionLogIdentity.ManagedAuthenticatedEncryptorFactory, 10, "UsingManagedKeyedHashAlgorithm", LogLevel.Debug, "data_protection.managed_encryptor.hash_algorithm_selected", "UsingManagedKeyedHashAlgorithm"),
        new(DataProtectionLogIdentity.ManagedAuthenticatedEncryptorFactory, 11, "UsingManagedSymmetricAlgorithm", LogLevel.Debug, "data_protection.managed_encryptor.symmetric_algorithm_selected", "UsingManagedSymmetricAlgorithm"),
        new(DataProtectionLogIdentity.CngCbcAuthenticatedEncryptorFactory, 3, "OpeningCNGAlgorithmFromProviderWithHMAC", LogLevel.Debug, "data_protection.cng_cbc_encryptor.hash_algorithm_opening", "OpeningCNGAlgorithmFromProviderWithHMAC"),
        new(DataProtectionLogIdentity.CngCbcAuthenticatedEncryptorFactory, 4, "OpeningCNGAlgorithmFromProviderWithChainingModeCBC", LogLevel.Debug, "data_protection.cng_cbc_encryptor.symmetric_algorithm_opening", "OpeningCNGAlgorithmFromProviderWithChainingModeCBC"),
        new(DataProtectionLogIdentity.CngGcmAuthenticatedEncryptorFactory, 9, "OpeningCNGAlgorithmFromProviderWithChainingModeGCM", LogLevel.Debug, "data_protection.cng_gcm_encryptor.symmetric_algorithm_opening", "OpeningCNGAlgorithmFromProviderWithChainingModeGCM"),
    ];

    internal static void Generate(DataProtectionFrameworkEvent row, ILogger logger)
    {
        var method = LogType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(m => m.Name == row.Method && m.GetCustomAttribute<LoggerMessageAttribute>() != null);
        var metadata = method.GetCustomAttribute<LoggerMessageAttribute>()!;
        Assert.Equal(row.Id, metadata.EventId);
        Assert.Equal(row.Name, metadata.EventName);
        Assert.Equal(row.Level, metadata.Level);
        method.Invoke(null, method.GetParameters().Select(p => Argument(p.ParameterType, logger)).ToArray());
    }

    private static object? Argument(Type type, ILogger logger)
    {
        if (type == typeof(ILogger)) return logger;
        if (type == typeof(string)) return HttpBoundaryCapture.Secret;
        if (type == typeof(Guid)) return Guid.Parse("d8e48197-2bd4-46e9-815a-45ced35d5905");
        if (type == typeof(DateTimeOffset)) return DateTimeOffset.UnixEpoch;
        if (type == typeof(Exception)) return new DataProtectionUnreadableException();
        if (type == typeof(XElement)) return new XElement("key", new XAttribute("id", HttpBoundaryCapture.Secret), new XElement("secret", HttpBoundaryCapture.Secret));
        if (type == typeof(XName)) return XName.Get(HttpBoundaryCapture.Secret);
        // Generated logging method only: never opens a Windows registry handle on this platform.
        if (type.FullName == "Microsoft.Win32.RegistryKey") return null;
        throw new InvalidOperationException(type.Name);
    }

    internal static void AssertSafe(HttpBoundaryCapture capture)
    {
        capture.AssertSafe();
        Assert.DoesNotContain("d8e48197-2bd4-46e9-815a-45ced35d5905", capture.Output.ToString());
        foreach (var entry in capture.Sink.Events)
            Assert.All(entry.Properties.Keys, key => Assert.Contains(key,
                new[] { "EventCode", "SourceContext", "SourceCategory", "Diagnostic", "failureKind" }));
    }
}

internal sealed class DataProtectionUnreadableException : Exception
{
    public override string Message => throw new InvalidOperationException("Raw DataProtection exception message accessed");
    public override string ToString() => throw new InvalidOperationException("Raw DataProtection exception formatted");
}

internal sealed class DataProtectionPoisonState : IReadOnlyList<KeyValuePair<string, object?>>
{
    public int Count => throw new InvalidOperationException("Raw state count accessed");
    public KeyValuePair<string, object?> this[int index] => throw new InvalidOperationException("Raw state item accessed");
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => throw new InvalidOperationException("Raw state enumerated");
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public override string ToString() => throw new InvalidOperationException("Raw state formatted");
}
