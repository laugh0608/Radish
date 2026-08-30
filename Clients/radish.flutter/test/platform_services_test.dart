import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/app/platform_services.dart';
import 'package:radish_flutter/core/auth/native_auth_gateway.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/platform/app_lifecycle_gateway.dart';
import 'package:radish_flutter/core/storage/secure_value_store.dart';
import 'package:radish_flutter/core/storage/string_preference_store.dart';
import 'package:radish_flutter/features/docs/data/docs_follow_up_store.dart';
import 'package:radish_flutter/features/forum/data/forum_follow_up_store.dart';

void main() {
  test('Android selects secure persistence and Android lifecycle bridge', () {
    final services = RadishPlatformServices.forPlatform(
      RadishPlatformKind.android,
      secureValues: InMemorySecureValueStore(),
      preferences: InMemoryStringPreferenceStore(),
    );

    expect(services.sessionStore, isA<SecureSessionStore>());
    expect(services.authGateway, isA<PlatformNativeAuthGateway>());
    expect(services.followUpStore, isA<PersistentForumFollowUpStore>());
    expect(services.docsFollowUpStore, isA<PersistentDocsFollowUpStore>());
    expect(services.appLifecycleGateway, isA<PlatformAppLifecycleGateway>());
  });

  test('iOS selects secure persistence without Android task semantics', () {
    final services = RadishPlatformServices.forPlatform(
      RadishPlatformKind.ios,
      secureValues: InMemorySecureValueStore(),
      preferences: InMemoryStringPreferenceStore(),
    );

    expect(services.sessionStore, isA<SecureSessionStore>());
    expect(services.authGateway, isA<PlatformNativeAuthGateway>());
    expect(services.followUpStore, isA<PersistentForumFollowUpStore>());
    expect(services.docsFollowUpStore, isA<PersistentDocsFollowUpStore>());
    expect(services.appLifecycleGateway, isA<EmptyAppLifecycleGateway>());
  });

  test('unsupported platforms remain explicit in-memory development shells',
      () {
    final services = RadishPlatformServices.forPlatform(
      RadishPlatformKind.unsupported,
    );

    expect(services.sessionStore, isA<InMemorySessionStore>());
    expect(services.authGateway, isA<InMemoryNativeAuthGateway>());
    expect(services.followUpStore, isA<InMemoryForumFollowUpStore>());
    expect(services.docsFollowUpStore, isA<InMemoryDocsFollowUpStore>());
    expect(services.appLifecycleGateway, isA<EmptyAppLifecycleGateway>());
  });
}
