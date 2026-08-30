import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/auth/native_auth_gateway.dart';
import 'package:radish_flutter/core/auth/session_store.dart';
import 'package:radish_flutter/core/storage/android_legacy_persistence_migrator.dart';
import 'package:radish_flutter/core/storage/secure_value_store.dart';
import 'package:radish_flutter/core/storage/string_preference_store.dart';
import 'package:radish_flutter/features/docs/data/docs_follow_up_store.dart';
import 'package:radish_flutter/features/docs/data/docs_models.dart';
import 'package:radish_flutter/features/forum/data/forum_follow_up_store.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';

void main() {
  test('secure session and OIDC attempt round-trip and clear once', () async {
    final secureValues = InMemorySecureValueStore();
    final sessionStore = SecureSessionStore(secureValues: secureValues);
    final attemptStore = SecureAuthorizationAttemptStore(
      secureValues: secureValues,
    );
    final session = _session();
    final attempt = _attempt();

    await sessionStore.write(session);
    await attemptStore.write(attempt);

    expect((await sessionStore.read())?.accessToken, session.accessToken);
    expect((await attemptStore.take())?.codeVerifier, attempt.codeVerifier);
    expect(await attemptStore.take(), isNull);

    await sessionStore.clear();
    expect(await sessionStore.read(), isNull);
  });

  test('persistent recent targets preserve deduped newest-first limits',
      () async {
    final preferences = InMemoryStringPreferenceStore();
    final forumStore = PersistentForumFollowUpStore(
      preferences: preferences,
    );
    final docsStore = PersistentDocsFollowUpStore(
      preferences: preferences,
    );

    for (var index = 0; index < 6; index += 1) {
      await forumStore.writeRecentBrowseHandoff(
        ForumDetailHandoffTarget(postId: 'post-$index'),
      );
      await docsStore.writeRecentDocumentTarget(
        DocsDetailHandoffTarget(slug: 'doc-$index'),
      );
    }
    await forumStore.writeRecentBrowseHandoff(
      const ForumDetailHandoffTarget(postId: 'post-3'),
    );
    await docsStore.writeRecentDocumentTarget(
      const DocsDetailHandoffTarget(slug: 'doc-3'),
    );

    expect(
      (await forumStore.readRecentBrowseHandoffs()).map((item) => item.postId),
      ['post-3', 'post-5', 'post-4', 'post-2', 'post-1'],
    );
    expect(
      (await docsStore.readRecentDocumentTargets()).map((item) => item.slug),
      ['doc-3', 'doc-5', 'doc-4', 'doc-2', 'doc-1'],
    );
  });

  test('Android legacy migration verifies new owners before clearing legacy',
      () async {
    final secureValues = InMemorySecureValueStore();
    final preferences = InMemoryStringPreferenceStore();
    final sessionStore = SecureSessionStore(secureValues: secureValues);
    final attemptStore = SecureAuthorizationAttemptStore(
      secureValues: secureValues,
    );
    final forumStore = PersistentForumFollowUpStore(
      preferences: preferences,
    );
    final docsStore = PersistentDocsFollowUpStore(
      preferences: preferences,
    );
    final legacy = _MemoryLegacyGateway.seeded();
    final migrator = AndroidLegacyPersistenceMigrator(
      sessionStore: sessionStore,
      authorizationAttemptStore: attemptStore,
      forumStore: forumStore,
      docsStore: docsStore,
      preferences: preferences,
      legacyGateway: legacy,
    );

    await migrator.migrate();

    expect((await sessionStore.read())?.refreshToken, 'refresh-token');
    expect((await attemptStore.read())?.state, 'oidc-state');
    expect(
      (await forumStore.readRecentBrowseHandoffs()).map((item) => item.postId),
      ['post-new', 'post-old'],
    );
    expect(await forumStore.readRecentProfileUserId(), 'user-42');
    expect((await forumStore.readPendingPostLoginTarget())?.tabIndex, 2);
    expect(
      (await docsStore.readRecentDocumentTargets()).map((item) => item.slug),
      ['doc-new', 'doc-old'],
    );
    expect(legacy.allCleared, isTrue);

    await migrator.migrate();
    expect((await sessionStore.read())?.accessToken, 'access-token');
    expect(legacy.allCleared, isTrue);
  });

  test('Android legacy sensitive state remains when secure write fails',
      () async {
    final secureValues = _FailingSecureValueStore();
    final preferences = InMemoryStringPreferenceStore();
    final legacy = _MemoryLegacyGateway.seeded();
    final migrator = AndroidLegacyPersistenceMigrator(
      sessionStore: SecureSessionStore(secureValues: secureValues),
      authorizationAttemptStore: SecureAuthorizationAttemptStore(
        secureValues: secureValues,
      ),
      forumStore: PersistentForumFollowUpStore(preferences: preferences),
      docsStore: PersistentDocsFollowUpStore(preferences: preferences),
      preferences: preferences,
      legacyGateway: legacy,
    );

    await expectLater(migrator.migrate(), throwsStateError);

    expect(legacy.session, isNotNull);
    expect(legacy.sessionCleared, isFalse);
  });
}

AuthSession _session() {
  return AuthSession(
    accessToken: 'access-token',
    refreshToken: 'refresh-token',
    userId: 'user-42',
    expiresAt: DateTime.utc(2026, 8, 30, 12),
  );
}

NativeOidcAuthorizationAttempt _attempt() {
  return NativeOidcAuthorizationAttempt(
    state: 'oidc-state',
    codeVerifier: List.filled(43, 'v').join(),
    redirectUri: 'radish://oidc/callback',
    startedAt: DateTime.utc(2026, 8, 30, 8),
  );
}

class _FailingSecureValueStore implements SecureValueStore {
  @override
  Future<void> delete(String key) async {}

  @override
  Future<String?> read(String key) async => null;

  @override
  Future<void> write(String key, String value) {
    throw StateError('secure storage unavailable');
  }
}

class _MemoryLegacyGateway implements LegacyAndroidPersistenceGateway {
  _MemoryLegacyGateway.seeded()
      : session = _session(),
        authorizationAttempt = _attempt(),
        recentBrowseHandoffs = const [
          ForumDetailHandoffTarget(postId: 'post-new'),
          ForumDetailHandoffTarget(postId: 'post-old'),
        ],
        recentProfileUserId = 'user-42',
        pendingPostLoginTarget = const ShellPostLoginTarget(tabIndex: 2),
        recentDocumentTargets = const [
          DocsDetailHandoffTarget(slug: 'doc-new'),
          DocsDetailHandoffTarget(slug: 'doc-old'),
        ];

  AuthSession? session;
  NativeOidcAuthorizationAttempt? authorizationAttempt;
  List<ForumDetailHandoffTarget> recentBrowseHandoffs;
  String? recentProfileUserId;
  ShellPostLoginTarget? pendingPostLoginTarget;
  List<DocsDetailHandoffTarget> recentDocumentTargets;

  bool sessionCleared = false;
  bool authorizationAttemptCleared = false;
  bool recentBrowseHandoffsCleared = false;
  bool recentProfileUserIdCleared = false;
  bool pendingPostLoginTargetCleared = false;
  bool recentDocumentTargetsCleared = false;

  bool get allCleared =>
      sessionCleared &&
      authorizationAttemptCleared &&
      recentBrowseHandoffsCleared &&
      recentProfileUserIdCleared &&
      pendingPostLoginTargetCleared &&
      recentDocumentTargetsCleared;

  @override
  Future<void> clearAuthorizationAttempt() async {
    authorizationAttempt = null;
    authorizationAttemptCleared = true;
  }

  @override
  Future<void> clearPendingPostLoginTarget() async {
    pendingPostLoginTarget = null;
    pendingPostLoginTargetCleared = true;
  }

  @override
  Future<void> clearRecentBrowseHandoffs() async {
    recentBrowseHandoffs = const [];
    recentBrowseHandoffsCleared = true;
  }

  @override
  Future<void> clearRecentDocumentTargets() async {
    recentDocumentTargets = const [];
    recentDocumentTargetsCleared = true;
  }

  @override
  Future<void> clearRecentProfileUserId() async {
    recentProfileUserId = null;
    recentProfileUserIdCleared = true;
  }

  @override
  Future<void> clearSession() async {
    session = null;
    sessionCleared = true;
  }

  @override
  Future<NativeOidcAuthorizationAttempt?> readAuthorizationAttempt() async {
    return authorizationAttempt;
  }

  @override
  Future<ShellPostLoginTarget?> readPendingPostLoginTarget() async {
    return pendingPostLoginTarget;
  }

  @override
  Future<List<ForumDetailHandoffTarget>> readRecentBrowseHandoffs() async {
    return recentBrowseHandoffs;
  }

  @override
  Future<List<DocsDetailHandoffTarget>> readRecentDocumentTargets() async {
    return recentDocumentTargets;
  }

  @override
  Future<String?> readRecentProfileUserId() async {
    return recentProfileUserId;
  }

  @override
  Future<AuthSession?> readSession() async {
    return session;
  }
}
