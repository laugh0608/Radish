import 'dart:io';

import '../core/auth/native_auth_gateway.dart';
import '../core/auth/session_store.dart';
import '../core/platform/app_lifecycle_gateway.dart';
import '../core/storage/android_legacy_persistence_migrator.dart';
import '../core/storage/secure_value_store.dart';
import '../core/storage/string_preference_store.dart';
import '../features/docs/data/docs_follow_up_store.dart';
import '../features/forum/data/forum_follow_up_store.dart';

enum RadishPlatformKind {
  android,
  ios,
  unsupported;

  static RadishPlatformKind current() {
    if (Platform.isAndroid) {
      return RadishPlatformKind.android;
    }
    if (Platform.isIOS) {
      return RadishPlatformKind.ios;
    }
    return RadishPlatformKind.unsupported;
  }
}

class RadishPlatformServices {
  RadishPlatformServices._({
    required this.sessionStore,
    required this.authGateway,
    required this.followUpStore,
    required this.docsFollowUpStore,
    required this.appLifecycleGateway,
    required Future<void> Function() initialize,
  }) : _initialize = initialize;

  factory RadishPlatformServices.forPlatform(
    RadishPlatformKind platform, {
    SecureValueStore? secureValues,
    StringPreferenceStore? preferences,
    LegacyAndroidPersistenceGateway? legacyAndroidGateway,
  }) {
    if (platform == RadishPlatformKind.unsupported) {
      return RadishPlatformServices._(
        sessionStore: InMemorySessionStore(),
        authGateway: InMemoryNativeAuthGateway(),
        followUpStore: InMemoryForumFollowUpStore(),
        docsFollowUpStore: InMemoryDocsFollowUpStore(),
        appLifecycleGateway: const EmptyAppLifecycleGateway(),
        initialize: _noInitialization,
      );
    }

    final secureValueStore = secureValues ?? FlutterSecureValueStore();
    final preferenceStore =
        preferences ?? SharedPreferencesStringPreferenceStore();
    final sessionStore = SecureSessionStore(
      secureValues: secureValueStore,
    );
    final authorizationAttemptStore = SecureAuthorizationAttemptStore(
      secureValues: secureValueStore,
    );
    final followUpStore = PersistentForumFollowUpStore(
      preferences: preferenceStore,
    );
    final docsFollowUpStore = PersistentDocsFollowUpStore(
      preferences: preferenceStore,
    );

    final Future<void> Function() initialize;
    if (platform == RadishPlatformKind.android) {
      final migrator = AndroidLegacyPersistenceMigrator(
        sessionStore: sessionStore,
        authorizationAttemptStore: authorizationAttemptStore,
        forumStore: followUpStore,
        docsStore: docsFollowUpStore,
        preferences: preferenceStore,
        legacyGateway: legacyAndroidGateway ??
            MethodChannelLegacyAndroidPersistenceGateway(),
      );
      initialize = migrator.migrate;
    } else {
      initialize = _noInitialization;
    }

    return RadishPlatformServices._(
      sessionStore: sessionStore,
      authGateway: PlatformNativeAuthGateway(
        authorizationAttemptStore: authorizationAttemptStore,
      ),
      followUpStore: followUpStore,
      docsFollowUpStore: docsFollowUpStore,
      appLifecycleGateway: platform == RadishPlatformKind.android
          ? PlatformAppLifecycleGateway()
          : const EmptyAppLifecycleGateway(),
      initialize: initialize,
    );
  }

  final SessionStore sessionStore;
  final NativeAuthGateway authGateway;
  final ForumFollowUpStore followUpStore;
  final DocsFollowUpStore docsFollowUpStore;
  final AppLifecycleGateway appLifecycleGateway;
  final Future<void> Function() _initialize;

  Future<void> initialize() {
    return _initialize();
  }

  static Future<void> _noInitialization() async {}
}
