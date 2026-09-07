import 'package:flutter_secure_storage/flutter_secure_storage.dart';

const MacOsOptions radishMacOsSecureStorageOptions = MacOsOptions(
  accountName: 'flutter_secure_storage_service',
  accessibility: KeychainAccessibility.first_unlock_this_device,
  synchronizable: false,
  // Data Protection Keychain requires a signed application identifier/access
  // group. Keep no-membership ad-hoc macOS builds on the encrypted login
  // Keychain instead of falling back to preferences or an in-memory store.
  usesDataProtectionKeychain: false,
);

abstract interface class SecureValueStore {
  Future<String?> read(String key);

  Future<void> write(String key, String value);

  Future<void> delete(String key);
}

class FlutterSecureValueStore implements SecureValueStore {
  FlutterSecureValueStore({
    FlutterSecureStorage? storage,
  }) : _storage = storage ??
            const FlutterSecureStorage(
              aOptions: AndroidOptions(
                resetOnError: false,
                migrateWithBackup: true,
                storageNamespace: 'radish_auth_v1',
              ),
              iOptions: IOSOptions(
                accessibility: KeychainAccessibility.first_unlock_this_device,
                synchronizable: false,
              ),
              mOptions: radishMacOsSecureStorageOptions,
            );

  final FlutterSecureStorage _storage;

  @override
  Future<void> delete(String key) {
    return _storage.delete(key: key);
  }

  @override
  Future<String?> read(String key) {
    return _storage.read(key: key);
  }

  @override
  Future<void> write(String key, String value) {
    return _storage.write(key: key, value: value);
  }
}

class InMemorySecureValueStore implements SecureValueStore {
  InMemorySecureValueStore({
    Map<String, String> initialValues = const <String, String>{},
  }) : _values = Map<String, String>.from(initialValues);

  final Map<String, String> _values;

  @override
  Future<void> delete(String key) async {
    _values.remove(key);
  }

  @override
  Future<String?> read(String key) async {
    return _values[key];
  }

  @override
  Future<void> write(String key, String value) async {
    _values[key] = value;
  }
}
