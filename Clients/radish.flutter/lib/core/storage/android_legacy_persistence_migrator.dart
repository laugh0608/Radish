import 'dart:convert';

import 'package:flutter/services.dart';

import '../../features/docs/data/docs_follow_up_store.dart';
import '../../features/docs/data/docs_models.dart';
import '../../features/forum/data/forum_follow_up_store.dart';
import '../../features/forum/data/forum_models.dart';
import '../auth/native_auth_gateway.dart';
import '../auth/session_store.dart';
import 'string_preference_store.dart';

abstract interface class LegacyAndroidPersistenceGateway {
  Future<AuthSession?> readSession();

  Future<void> clearSession();

  Future<NativeOidcAuthorizationAttempt?> readAuthorizationAttempt();

  Future<void> clearAuthorizationAttempt();

  Future<List<ForumDetailHandoffTarget>> readRecentBrowseHandoffs();

  Future<void> clearRecentBrowseHandoffs();

  Future<String?> readRecentProfileUserId();

  Future<void> clearRecentProfileUserId();

  Future<ShellPostLoginTarget?> readPendingPostLoginTarget();

  Future<void> clearPendingPostLoginTarget();

  Future<List<DocsDetailHandoffTarget>> readRecentDocumentTargets();

  Future<void> clearRecentDocumentTargets();
}

class MethodChannelLegacyAndroidPersistenceGateway
    implements LegacyAndroidPersistenceGateway {
  MethodChannelLegacyAndroidPersistenceGateway({
    MethodChannel? sessionChannel,
    MethodChannel? authChannel,
    PlatformForumFollowUpStore? forumStore,
    PlatformDocsFollowUpStore? docsStore,
  })  : _sessionChannel = sessionChannel ??
            const MethodChannel('radish.flutter/session_store'),
        _authChannel =
            authChannel ?? const MethodChannel('radish.flutter/native_auth'),
        _forumStore = forumStore ?? PlatformForumFollowUpStore(),
        _docsStore = docsStore ?? PlatformDocsFollowUpStore();

  final MethodChannel _sessionChannel;
  final MethodChannel _authChannel;
  final PlatformForumFollowUpStore _forumStore;
  final PlatformDocsFollowUpStore _docsStore;

  @override
  Future<void> clearAuthorizationAttempt() {
    return _authChannel.invokeMethod<void>('clearAuthorizationAttempt');
  }

  @override
  Future<void> clearPendingPostLoginTarget() {
    return _forumStore.clearPendingPostLoginTarget();
  }

  @override
  Future<void> clearRecentBrowseHandoffs() {
    return _forumStore.clearRecentBrowseHandoff();
  }

  @override
  Future<void> clearRecentDocumentTargets() {
    return _docsStore.clearRecentDocumentTarget();
  }

  @override
  Future<void> clearRecentProfileUserId() {
    return _forumStore.clearRecentProfileUserId();
  }

  @override
  Future<void> clearSession() {
    return _sessionChannel.invokeMethod<void>('clear');
  }

  @override
  Future<NativeOidcAuthorizationAttempt?> readAuthorizationAttempt() async {
    final payload =
        await _authChannel.invokeMethod<String>('readAuthorizationAttempt');
    if (payload == null || payload.trim().isEmpty) {
      return null;
    }

    return NativeOidcAuthorizationAttempt.fromJson(jsonDecode(payload));
  }

  @override
  Future<ShellPostLoginTarget?> readPendingPostLoginTarget() {
    return _forumStore.readPendingPostLoginTarget();
  }

  @override
  Future<List<ForumDetailHandoffTarget>> readRecentBrowseHandoffs() {
    return _forumStore.readRecentBrowseHandoffs();
  }

  @override
  Future<List<DocsDetailHandoffTarget>> readRecentDocumentTargets() {
    return _docsStore.readRecentDocumentTargets();
  }

  @override
  Future<String?> readRecentProfileUserId() {
    return _forumStore.readRecentProfileUserId();
  }

  @override
  Future<AuthSession?> readSession() async {
    final payload = await _sessionChannel.invokeMethod<String>('read');
    if (payload == null || payload.trim().isEmpty) {
      return null;
    }

    return AuthSession.fromJson(jsonDecode(payload));
  }
}

class AndroidLegacyPersistenceMigrator {
  AndroidLegacyPersistenceMigrator({
    required SessionStore sessionStore,
    required AuthorizationAttemptStore authorizationAttemptStore,
    required PersistentForumFollowUpStore forumStore,
    required PersistentDocsFollowUpStore docsStore,
    required StringPreferenceStore preferences,
    required LegacyAndroidPersistenceGateway legacyGateway,
  })  : _sessionStore = sessionStore,
        _authorizationAttemptStore = authorizationAttemptStore,
        _forumStore = forumStore,
        _docsStore = docsStore,
        _preferences = preferences,
        _legacyGateway = legacyGateway;

  final SessionStore _sessionStore;
  final AuthorizationAttemptStore _authorizationAttemptStore;
  final PersistentForumFollowUpStore _forumStore;
  final PersistentDocsFollowUpStore _docsStore;
  final StringPreferenceStore _preferences;
  final LegacyAndroidPersistenceGateway _legacyGateway;

  Future<void> migrate() async {
    await _migrateSession();
    await _migrateAuthorizationAttempt();
    await _migrateRecentBrowseHandoffs();
    await _migrateRecentProfileUserId();
    await _migratePendingPostLoginTarget();
    await _migrateRecentDocumentTargets();
  }

  Future<void> _migrateSession() async {
    final current = await _sessionStore.read();
    if (current != null) {
      await _legacyGateway.clearSession();
      return;
    }

    final legacy = await _legacyGateway.readSession();
    if (legacy == null) {
      return;
    }

    await _sessionStore.write(legacy);
    final persisted = await _sessionStore.read();
    if (!_sameJson(persisted?.toJson(), legacy.toJson())) {
      throw StateError('Unable to verify migrated auth session.');
    }
    await _legacyGateway.clearSession();
  }

  Future<void> _migrateAuthorizationAttempt() async {
    final current = await _authorizationAttemptStore.read();
    if (current != null) {
      await _legacyGateway.clearAuthorizationAttempt();
      return;
    }

    final legacy = await _legacyGateway.readAuthorizationAttempt();
    if (legacy == null) {
      return;
    }

    await _authorizationAttemptStore.write(legacy);
    final persisted = await _authorizationAttemptStore.read();
    if (!_sameJson(persisted?.toJson(), legacy.toJson())) {
      throw StateError('Unable to verify migrated OIDC authorization attempt.');
    }
    await _legacyGateway.clearAuthorizationAttempt();
  }

  Future<void> _migrateRecentBrowseHandoffs() async {
    if (await _preferences.containsKey(
      PersistentForumFollowUpStore.recentBrowseHandoffsKey,
    )) {
      await _legacyGateway.clearRecentBrowseHandoffs();
      return;
    }

    final legacy = await _legacyGateway.readRecentBrowseHandoffs();
    for (final target in legacy.reversed) {
      await _forumStore.writeRecentBrowseHandoff(target);
    }
    final persisted = await _forumStore.readRecentBrowseHandoffs();
    if (!_sameJson(
      persisted.map(_forumTargetIdentity).toList(),
      legacy.map(_forumTargetIdentity).toList(),
    )) {
      throw StateError('Unable to verify migrated forum browse history.');
    }
    await _legacyGateway.clearRecentBrowseHandoffs();
  }

  Future<void> _migrateRecentProfileUserId() async {
    if (await _preferences.containsKey(
      PersistentForumFollowUpStore.recentProfileUserIdKey,
    )) {
      await _legacyGateway.clearRecentProfileUserId();
      return;
    }

    final legacy = (await _legacyGateway.readRecentProfileUserId())?.trim();
    if (legacy == null || legacy.isEmpty) {
      await _legacyGateway.clearRecentProfileUserId();
      return;
    }

    await _forumStore.writeRecentProfileUserId(legacy);
    if (await _forumStore.readRecentProfileUserId() != legacy) {
      throw StateError('Unable to verify migrated recent profile target.');
    }
    await _legacyGateway.clearRecentProfileUserId();
  }

  Future<void> _migratePendingPostLoginTarget() async {
    if (await _preferences.containsKey(
      PersistentForumFollowUpStore.pendingPostLoginTargetKey,
    )) {
      await _legacyGateway.clearPendingPostLoginTarget();
      return;
    }

    final legacy = await _legacyGateway.readPendingPostLoginTarget();
    if (legacy == null) {
      await _legacyGateway.clearPendingPostLoginTarget();
      return;
    }

    await _forumStore.writePendingPostLoginTarget(legacy);
    final persisted = await _forumStore.readPendingPostLoginTarget();
    if (!_sameJson(persisted?.toJson(), legacy.toJson())) {
      throw StateError('Unable to verify migrated post-login target.');
    }
    await _legacyGateway.clearPendingPostLoginTarget();
  }

  Future<void> _migrateRecentDocumentTargets() async {
    if (await _preferences.containsKey(
      PersistentDocsFollowUpStore.recentDocumentTargetsKey,
    )) {
      await _legacyGateway.clearRecentDocumentTargets();
      return;
    }

    final legacy = await _legacyGateway.readRecentDocumentTargets();
    for (final target in legacy.reversed) {
      await _docsStore.writeRecentDocumentTarget(target);
    }
    final persisted = await _docsStore.readRecentDocumentTargets();
    if (!_sameJson(
      persisted.map(_docsTargetIdentity).toList(),
      legacy.map(_docsTargetIdentity).toList(),
    )) {
      throw StateError('Unable to verify migrated docs browse history.');
    }
    await _legacyGateway.clearRecentDocumentTargets();
  }
}

bool _sameJson(Object? left, Object? right) {
  return jsonEncode(left) == jsonEncode(right);
}

Map<String, Object?> _forumTargetIdentity(ForumDetailHandoffTarget target) {
  return {
    'postId': target.normalizedPostId,
    'commentId': target.normalizedCommentId,
    'initialTitle': target.normalizedInitialTitle,
  };
}

Map<String, Object?> _docsTargetIdentity(DocsDetailHandoffTarget target) {
  return {
    'slug': target.normalizedSlug,
    'initialTitle': target.normalizedInitialTitle,
  };
}
