import 'dart:convert';

import 'package:flutter/services.dart';

import 'docs_models.dart';

abstract class DocsFollowUpStore {
  Future<DocsDetailHandoffTarget?> readRecentDocumentTarget();

  Future<void> writeRecentDocumentTarget(DocsDetailHandoffTarget target);

  Future<void> clearRecentDocumentTarget();
}

class EmptyDocsFollowUpStore implements DocsFollowUpStore {
  const EmptyDocsFollowUpStore();

  @override
  Future<void> clearRecentDocumentTarget() async {}

  @override
  Future<DocsDetailHandoffTarget?> readRecentDocumentTarget() async {
    return null;
  }

  @override
  Future<void> writeRecentDocumentTarget(
    DocsDetailHandoffTarget target,
  ) async {}
}

class InMemoryDocsFollowUpStore implements DocsFollowUpStore {
  InMemoryDocsFollowUpStore({
    DocsDetailHandoffTarget? initialRecentDocumentTarget,
  }) : _recentDocumentTarget = _normalizeTarget(initialRecentDocumentTarget);

  DocsDetailHandoffTarget? _recentDocumentTarget;

  @override
  Future<void> clearRecentDocumentTarget() async {
    _recentDocumentTarget = null;
  }

  @override
  Future<DocsDetailHandoffTarget?> readRecentDocumentTarget() async {
    return _recentDocumentTarget;
  }

  @override
  Future<void> writeRecentDocumentTarget(
    DocsDetailHandoffTarget target,
  ) async {
    _recentDocumentTarget = _normalizeTarget(
      target.copyWith(source: DocsDetailHandoffSource.browseHistory),
    );
  }
}

class PlatformDocsFollowUpStore implements DocsFollowUpStore {
  PlatformDocsFollowUpStore({
    MethodChannel? channel,
  }) : _channel =
            channel ?? const MethodChannel('radish.flutter/docs_follow_up');

  final MethodChannel _channel;

  @override
  Future<void> clearRecentDocumentTarget() async {
    await _channel.invokeMethod<void>('clearRecentDocumentTarget');
  }

  @override
  Future<DocsDetailHandoffTarget?> readRecentDocumentTarget() async {
    final payload =
        await _channel.invokeMethod<String>('readRecentDocumentTarget');
    return _decodeTarget(payload);
  }

  @override
  Future<void> writeRecentDocumentTarget(
    DocsDetailHandoffTarget target,
  ) async {
    final normalizedTarget = _normalizeTarget(
      target.copyWith(source: DocsDetailHandoffSource.browseHistory),
    );
    if (normalizedTarget == null) {
      await clearRecentDocumentTarget();
      return;
    }

    await _channel.invokeMethod<void>(
      'writeRecentDocumentTarget',
      jsonEncode(normalizedTarget.toJson()),
    );
  }

  DocsDetailHandoffTarget? _decodeTarget(String? payload) {
    if (payload == null || payload.trim().isEmpty) {
      return null;
    }

    return _normalizeTarget(
      DocsDetailHandoffTarget.fromJson(jsonDecode(payload)),
    );
  }
}

DocsDetailHandoffTarget? _normalizeTarget(DocsDetailHandoffTarget? target) {
  if (target == null || !target.hasValidSlug) {
    return null;
  }

  return DocsDetailHandoffTarget(
    slug: target.normalizedSlug,
    source: target.source,
    initialTitle: target.normalizedInitialTitle,
  );
}
