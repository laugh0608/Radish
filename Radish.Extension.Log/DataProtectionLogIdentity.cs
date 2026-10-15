namespace Radish.Extension.Log;

/// <summary>ASP.NET Core DataProtection 10.0.8 的精确来源与已核对调用点；不按模板或前缀推断身份。</summary>
internal static class DataProtectionLogIdentity
{
    internal const string TypeForwardingActivator = "Microsoft.AspNetCore.DataProtection.TypeForwardingActivator";
    internal const string EphemeralDataProtectionProvider = "Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider";
    internal const string NullXmlEncryptor = "Microsoft.AspNetCore.DataProtection.XmlEncryption.NullXmlEncryptor";
    internal const string DpapiXmlEncryptor = "Microsoft.AspNetCore.DataProtection.XmlEncryption.DpapiXmlEncryptor";
    internal const string DpapiNGXmlDecryptor = "Microsoft.AspNetCore.DataProtection.XmlEncryption.DpapiNGXmlDecryptor";
    internal const string DpapiXmlDecryptor = "Microsoft.AspNetCore.DataProtection.XmlEncryption.DpapiXmlDecryptor";
    internal const string CertificateXmlEncryptor = "Microsoft.AspNetCore.DataProtection.XmlEncryption.CertificateXmlEncryptor";
    internal const string DpapiNGXmlEncryptor = "Microsoft.AspNetCore.DataProtection.XmlEncryption.DpapiNGXmlEncryptor";
    internal const string DefaultKeyResolver = "Microsoft.AspNetCore.DataProtection.KeyManagement.DefaultKeyResolver";
    internal const string XmlKeyManager = "Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager";
    internal const string KeyRingProvider = "Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingProvider";
    internal const string KeyRingBasedDataProtector = "Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector";
    internal const string KeyManagementOptionsPostSetup = "Microsoft.AspNetCore.DataProtection.Internal.KeyManagementOptionsPostSetup";
    internal const string DataProtectionHostedService = "Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService";
    internal const string FileSystemXmlRepository = "Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository";
    internal const string EphemeralXmlRepository = "Microsoft.AspNetCore.DataProtection.Repositories.EphemeralXmlRepository";
    internal const string RegistryXmlRepository = "Microsoft.AspNetCore.DataProtection.Repositories.RegistryXmlRepository";
    internal const string ManagedAuthenticatedEncryptorFactory = "Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ManagedAuthenticatedEncryptorFactory";
    internal const string CngCbcAuthenticatedEncryptorFactory = "Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.CngCbcAuthenticatedEncryptorFactory";
    internal const string CngGcmAuthenticatedEncryptorFactory = "Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.CngGcmAuthenticatedEncryptorFactory";

    internal static bool IsSource(string source) => source is
        TypeForwardingActivator or EphemeralDataProtectionProvider or NullXmlEncryptor or
        DpapiXmlEncryptor or DpapiNGXmlDecryptor or DpapiXmlDecryptor or
        CertificateXmlEncryptor or DpapiNGXmlEncryptor or DefaultKeyResolver or
        XmlKeyManager or KeyRingProvider or KeyRingBasedDataProtector or
        KeyManagementOptionsPostSetup or DataProtectionHostedService or FileSystemXmlRepository or
        EphemeralXmlRepository or RegistryXmlRepository or ManagedAuthenticatedEncryptorFactory or
        CngCbcAuthenticatedEncryptorFactory or CngGcmAuthenticatedEncryptorFactory;

    internal static string Code(string source, int? id, string? name) => source switch
    {
        TypeForwardingActivator => "data_protection.type_activation.unknown",
        EphemeralDataProtectionProvider => (id, name) switch
        {
            (45, "UsingEphemeralDataProtectionProvider") => "data_protection.ephemeral_provider.selected",
            _ => "data_protection.ephemeral_provider.unknown"
        },
        NullXmlEncryptor => (id, name) switch
        {
            (44, "EncryptingUsingNullEncryptor") => "data_protection.null_encryptor.encryption_unprotected",
            _ => "data_protection.null_encryptor.unknown"
        },
        DpapiXmlEncryptor => (id, name) switch
        {
            (26, "EncryptingToWindowsDPAPIForCurrentUserAccount") => "data_protection.dpapi_encryptor.user_encryption_started",
            (54, "ErrorOccurredWhileEncryptingToWindowsDPAPI") => "data_protection.dpapi_encryptor.encryption_failed",
            (55, "EncryptingToWindowsDPAPIForLocalMachineAccount") => "data_protection.dpapi_encryptor.machine_encryption_started",
            _ => "data_protection.dpapi_encryptor.unknown"
        },
        DpapiNGXmlDecryptor => (id, name) switch
        {
            (42, "DecryptingSecretElementUsingWindowsDPAPING") => "data_protection.dpaping_decryptor.decryption_started",
            (43, "ExceptionOccurredTryingToDecryptElement") => "data_protection.dpaping_decryptor.decryption_failed",
            _ => "data_protection.dpaping_decryptor.unknown"
        },
        DpapiXmlDecryptor => (id, name) switch
        {
            (43, "ExceptionOccurredTryingToDecryptElement") => "data_protection.dpapi_decryptor.decryption_failed",
            (51, "DecryptingSecretElementUsingWindowsDPAPI") => "data_protection.dpapi_decryptor.decryption_started",
            _ => "data_protection.dpapi_decryptor.unknown"
        },
        CertificateXmlEncryptor => (id, name) switch
        {
            (28, "ErrorOccurredWhileEncryptingToX509CertificateWithThumbprint") => "data_protection.certificate_encryptor.encryption_failed",
            (29, "EncryptingToX509CertificateWithThumbprint") => "data_protection.certificate_encryptor.encryption_started",
            (30, "ExceptionOccurredWhileTryingToResolveCertificateWithThumbprint") => "data_protection.certificate_encryptor.certificate_resolution_failed",
            _ => "data_protection.certificate_encryptor.unknown"
        },
        DpapiNGXmlEncryptor => (id, name) switch
        {
            (27, "EncryptingToWindowsDPAPINGUsingProtectionDescriptorRule") => "data_protection.dpaping_encryptor.encryption_started",
            (56, "ErrorOccurredWhileEncryptingToWindowsDPAPING") => "data_protection.dpaping_encryptor.encryption_failed",
            _ => "data_protection.dpaping_encryptor.unknown"
        },
        DefaultKeyResolver => (id, name) switch
        {
            (12, "KeyIsIneligibleToBeTheDefaultKeyBecauseItsMethodFailed") => "data_protection.key_resolver.candidate_failed",
            (13, "ConsideringKeyWithExpirationDateAsDefaultKey") => "data_protection.key_resolver.candidate_considered",
            (14, "KeyIsNoLongerUnderConsiderationAsDefault") => "data_protection.key_resolver.candidate_rejected",
            (53, "RepositoryContainsNoViableDefaultKey") => "data_protection.key_resolver.default_unavailable",
            (73, "RetryingMethodOfKeyAfterFailure") => "data_protection.key_resolver.candidate_retry",
            _ => "data_protection.key_resolver.unknown"
        },
        XmlKeyManager => (id, name) switch
        {
            (15, "UnknownElementWithNameFoundInKeyringSkipping") => "data_protection.key_manager.unknown_element_skipped",
            (16, "MarkedKeyAsRevokedInTheKeyring") => "data_protection.key_manager.key_marked_revoked",
            (17, "TriedToProcessRevocationOfKeyButNoSuchKeyWasFound") => "data_protection.key_manager.revocation_key_missing",
            (18, "FoundKey") => "data_protection.key_manager.key_found",
            (19, "FoundRevocationOfAllKeysCreatedPriorTo") => "data_protection.key_manager.date_revocation_found",
            (20, "FoundRevocationOfKey") => "data_protection.key_manager.key_revocation_found",
            (21, "ExceptionWhileProcessingRevocationElement") => "data_protection.key_manager.revocation_processing_failed",
            (22, "RevokingAllKeysAsOfForReason") => "data_protection.key_manager.date_revocation_started",
            (23, "KeyCacheExpirationTokenTriggeredByOperation") => "data_protection.key_manager.cache_expiration_triggered",
            (24, "ExceptionOccurredWhileProcessingTheKeyElement") => "data_protection.key_manager.key_processing_failed",
            (25, "ExceptionOccurredWhileProcessingTheKeyElementDebug") => "data_protection.key_manager.key_processing_failure_detail",
            (32, "DescriptorDeserializerTypeForKeyIs") => "data_protection.key_manager.deserializer_selected",
            (33, "KeyEscrowSinkFoundWritingKeyToEscrow") => "data_protection.key_manager.escrow_write_started",
            (34, "NoKeyEscrowSinkFoundNotWritingKeyToEscrow") => "data_protection.key_manager.escrow_not_configured",
            (35, "NoXMLEncryptorConfiguredKeyMayBePersistedToStorageInUnencryptedForm") => "data_protection.key_manager.xml_encryptor_missing",
            (36, "RevokingKeyForReason") => "data_protection.key_manager.key_revocation_started",
            (58, "CreatingKey") => "data_protection.key_manager.key_creation_started",
            (59, "UsingEphemeralKeyRepository") => "data_protection.key_manager.ephemeral_repository_selected",
            (61, "UsingRegistryAsKeyRepositoryWithDPAPI") => "data_protection.key_manager.registry_repository_selected",
            (62, "UsingProfileAsKeyRepository") => "data_protection.key_manager.profile_repository_selected",
            (63, "UsingProfileAsKeyRepositoryWithDPAPI") => "data_protection.key_manager.protected_profile_repository_selected",
            (64, "UsingAzureAsKeyRepository") => "data_protection.key_manager.azure_repository_selected",
            (78, "KeyRevokedMultipleTimes") => "data_protection.key_manager.repeated_revocation",
            (80, "DeletingKey") => "data_protection.key_manager.key_deletion_started",
            _ => "data_protection.key_manager.unknown"
        },
        KeyRingProvider => (id, name) switch
        {
            (1, "UsingFallbackKeyWithExpirationAsDefaultKey") => "data_protection.key_ring.fallback_selected",
            (2, "UsingKeyAsDefaultKey") => "data_protection.key_ring.default_selected",
            (46, "ExistingCachedKeyRingIsExpiredRefreshing") => "data_protection.key_ring.refresh_started",
            (47, "ErrorOccurredWhileRefreshingKeyRing") => "data_protection.key_ring.refresh_failed",
            (48, "ErrorOccurredWhileReadingKeyRing") => "data_protection.key_ring.read_failed",
            (49, "KeyRingDoesNotContainValidDefaultKey") => "data_protection.key_ring.default_unavailable",
            (57, "PolicyResolutionStatesThatANewKeyShouldBeAddedToTheKeyRing") => "data_protection.key_ring.new_key_required",
            (72, "KeyRingDefaultKeyIsRevoked") => "data_protection.key_ring.default_revoked",
            _ => "data_protection.key_ring.unknown"
        },
        KeyRingBasedDataProtector => (id, name) switch
        {
            (5, "PerformingUnprotectOperationToKeyWithPurposes") => "data_protection.protector.unprotect_started",
            (6, "KeyWasNotFoundInTheKeyRingUnprotectOperationCannotProceed") => "data_protection.protector.unprotect_key_missing",
            (7, "KeyWasRevokedCallerRequestedUnprotectOperationProceedRegardless") => "data_protection.protector.unprotect_revoked_key_allowed",
            (8, "KeyWasRevokedUnprotectOperationCannotProceed") => "data_protection.protector.unprotect_revoked_key_rejected",
            (31, "PerformingProtectOperationToKeyWithPurposes") => "data_protection.protector.protect_started",
            _ => "data_protection.protector.unknown"
        },
        KeyManagementOptionsPostSetup => (id, name) switch
        {
            (61, "IgnoringReadOnlyConfigurationForNonDefaultOptions") => "data_protection.options.nondefault_options_ignored",
            (62, "UsingReadOnlyKeyConfiguration") => "data_protection.options.read_only_selected",
            (63, "NotUsingReadOnlyKeyConfigurationBecauseOfRepository") => "data_protection.options.read_only_repository_rejected",
            (64, "NotUsingReadOnlyKeyConfigurationBecauseOfEncryptor") => "data_protection.options.read_only_encryptor_rejected",
            _ => "data_protection.options.unknown"
        },
        DataProtectionHostedService => (id, name) switch
        {
            (65, "KeyRingWasLoadedOnStartup") => "data_protection.preload.completed",
            (66, "KeyRingFailedToLoadOnStartup") => "data_protection.preload.failed",
            _ => "data_protection.preload.unknown"
        },
        FileSystemXmlRepository => (id, name) switch
        {
            (37, "ReadingDataFromFile") => "data_protection.file_repository.read_started",
            (38, "NameIsNotSafeFileName") => "data_protection.file_repository.unsafe_name_replaced",
            (39, "WritingDataToFile") => "data_protection.file_repository.write_started",
            (60, "UsingEphemeralFileSystemLocationInContainer") => "data_protection.file_repository.ephemeral_container_location",
            (74, "DeletingFile") => "data_protection.file_repository.delete_started",
            (75, "FailedToDeleteFile") => "data_protection.file_repository.delete_failed",
            _ => "data_protection.file_repository.unknown"
        },
        EphemeralXmlRepository => (id, name) switch
        {
            (50, "UsingInMemoryRepository") => "data_protection.memory_repository.selected",
            _ => "data_protection.memory_repository.unknown"
        },
        RegistryXmlRepository => (id, name) switch
        {
            (40, "ReadingDataFromRegistryKeyValue") => "data_protection.registry_repository.read_started",
            (41, "NameIsNotSafeRegistryValueName") => "data_protection.registry_repository.unsafe_name_replaced",
            (76, "RemovingDataFromRegistryKeyValue") => "data_protection.registry_repository.delete_started",
            (77, "FailedToRemoveDataFromRegistryKeyValue") => "data_protection.registry_repository.delete_failed",
            _ => "data_protection.registry_repository.unknown"
        },
        ManagedAuthenticatedEncryptorFactory => (id, name) switch
        {
            (10, "UsingManagedKeyedHashAlgorithm") => "data_protection.managed_encryptor.hash_algorithm_selected",
            (11, "UsingManagedSymmetricAlgorithm") => "data_protection.managed_encryptor.symmetric_algorithm_selected",
            _ => "data_protection.managed_encryptor.unknown"
        },
        CngCbcAuthenticatedEncryptorFactory => (id, name) switch
        {
            (3, "OpeningCNGAlgorithmFromProviderWithHMAC") => "data_protection.cng_cbc_encryptor.hash_algorithm_opening",
            (4, "OpeningCNGAlgorithmFromProviderWithChainingModeCBC") => "data_protection.cng_cbc_encryptor.symmetric_algorithm_opening",
            _ => "data_protection.cng_cbc_encryptor.unknown"
        },
        CngGcmAuthenticatedEncryptorFactory => (id, name) switch
        {
            (9, "OpeningCNGAlgorithmFromProviderWithChainingModeGCM") => "data_protection.cng_gcm_encryptor.symmetric_algorithm_opening",
            _ => "data_protection.cng_gcm_encryptor.unknown"
        },
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };
}
