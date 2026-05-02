import 'dart:convert';

import 'package:flutter/services.dart';

import 'docs_models.dart';

abstract class DocsFollowUpStore {
  Future<DocsDetailHandoffTarget?> readRecentDocumentTarget();

  Future<List<DocsDetailHandoffTarget>> readRecentDocumentTargets();

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
  Future<List<DocsDetailHandoffTarget>> readRecentDocumentTargets() async {
    return const <DocsDetailHandoffTarget>[];
  }

  @override
  Future<void> writeRecentDocumentTarget(
    DocsDetailHandoffTarget target,
  ) async {}
}

class InMemoryDocsFollowUpStore implements DocsFollowUpStore {
  InMemoryDocsFollowUpStore({
    DocsDetailHandoffTarget? initialRecentDocumentTarget,
    List<DocsDetailHandoffTarget> initialRecentDocumentTargets =
        const <DocsDetailHandoffTarget>[],
  }) : _recentDocumentTargets = _normalizeTargets([
          ...initialRecentDocumentTargets,
          if (initialRecentDocumentTarget != null) initialRecentDocumentTarget,
        ]);

  List<DocsDetailHandoffTarget> _recentDocumentTargets;

  @override
  Future<void> clearRecentDocumentTarget() async {
    _recentDocumentTargets = const <DocsDetailHandoffTarget>[];
  }

  @override
  Future<DocsDetailHandoffTarget?> readRecentDocumentTarget() async {
    return _recentDocumentTargets.isEmpty ? null : _recentDocumentTargets.first;
  }

  @override
  Future<List<DocsDetailHandoffTarget>> readRecentDocumentTargets() async {
    return List<DocsDetailHandoffTarget>.unmodifiable(_recentDocumentTargets);
  }

  @override
  Future<void> writeRecentDocumentTarget(
    DocsDetailHandoffTarget target,
  ) async {
    _recentDocumentTargets = _upsertRecentDocumentTarget(
      _recentDocumentTargets,
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
    final targets = await readRecentDocumentTargets();
    return targets.isEmpty ? null : targets.first;
  }

  @override
  Future<List<DocsDetailHandoffTarget>> readRecentDocumentTargets() async {
    final payload =
        await _channel.invokeMethod<String>('readRecentDocumentTargets');
    return _decodeTargetList(payload);
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

  List<DocsDetailHandoffTarget> _decodeTargetList(String? payload) {
    if (payload == null || payload.trim().isEmpty) {
      return const <DocsDetailHandoffTarget>[];
    }

    final decoded = jsonDecode(payload);
    if (decoded is! List) {
      return const <DocsDetailHandoffTarget>[];
    }

    return _normalizeTargets(
      decoded
          .map(DocsDetailHandoffTarget.fromJson)
          .whereType<DocsDetailHandoffTarget>(),
    );
  }
}

const int _maxRecentDocumentTargetCount = 5;

List<DocsDetailHandoffTarget> _upsertRecentDocumentTarget(
  Iterable<DocsDetailHandoffTarget> targets,
  DocsDetailHandoffTarget target,
) {
  final recentTarget = _normalizeTarget(target);
  if (recentTarget == null) {
    return _normalizeTargets(targets);
  }

  return _normalizeTargets([
    recentTarget,
    ...targets.where(
      (item) => item.normalizedSlug != recentTarget.normalizedSlug,
    ),
  ]);
}

List<DocsDetailHandoffTarget> _normalizeTargets(
  Iterable<DocsDetailHandoffTarget?> targets,
) {
  final normalizedTargets = <DocsDetailHandoffTarget>[];
  for (final target in targets) {
    final normalizedTarget = _normalizeTarget(target);
    if (normalizedTarget == null) {
      continue;
    }

    if (normalizedTargets.any(
      (item) => item.normalizedSlug == normalizedTarget.normalizedSlug,
    )) {
      continue;
    }

    normalizedTargets.add(normalizedTarget);
    if (normalizedTargets.length >= _maxRecentDocumentTargetCount) {
      break;
    }
  }

  return List<DocsDetailHandoffTarget>.unmodifiable(normalizedTargets);
}

DocsDetailHandoffTarget? _normalizeTarget(DocsDetailHandoffTarget? target) {
  if (target == null || !target.hasValidSlug) {
    return null;
  }

  return DocsDetailHandoffTarget(
    slug: target.normalizedSlug,
    source: DocsDetailHandoffSource.browseHistory,
    initialTitle: target.normalizedInitialTitle,
  );
}
