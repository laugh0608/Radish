import 'dart:convert';

import 'package:flutter/services.dart';

import 'forum_models.dart';

abstract class ForumFollowUpStore {
  Future<ForumDetailHandoffTarget?> takePendingHandoff();

  Future<ForumDetailHandoffTarget?> readRecentBrowseHandoff();

  Future<List<ForumDetailHandoffTarget>> readRecentBrowseHandoffs();

  Future<void> writeRecentBrowseHandoff(ForumDetailHandoffTarget target);

  Future<void> clearRecentBrowseHandoff();

  Future<String?> readRecentProfileUserId();

  Future<void> writeRecentProfileUserId(String userId);

  Future<void> clearRecentProfileUserId();

  Future<ShellPostLoginTarget?> readPendingPostLoginTarget();

  Future<void> writePendingPostLoginTarget(ShellPostLoginTarget target);

  Future<void> clearPendingPostLoginTarget();
}

class ShellPostLoginTarget {
  const ShellPostLoginTarget({
    required this.tabIndex,
    this.forumTarget,
  });

  final int tabIndex;
  final ForumDetailHandoffTarget? forumTarget;

  Map<String, Object?> toJson() {
    return {
      'tabIndex': tabIndex,
      'forumTarget': forumTarget?.toJson(),
    };
  }

  static ShellPostLoginTarget? fromJson(Object? json) {
    if (json is! Map) {
      return null;
    }

    final map = Map<String, Object?>.from(json.cast<Object?, Object?>());
    final tabIndex = int.tryParse(map['tabIndex']?.toString() ?? '');
    if (tabIndex == null || tabIndex < 0) {
      return null;
    }

    final forumTarget = ForumDetailHandoffTarget.fromJson(map['forumTarget']);
    return ShellPostLoginTarget(
      tabIndex: tabIndex,
      forumTarget: forumTarget == null || !forumTarget.hasValidPostId
          ? null
          : ForumDetailHandoffTarget(
              postId: forumTarget.normalizedPostId,
              source: forumTarget.source,
              initialTitle: forumTarget.normalizedInitialTitle,
              commentId: forumTarget.normalizedCommentId,
            ),
    );
  }
}

class InMemoryForumFollowUpStore implements ForumFollowUpStore {
  InMemoryForumFollowUpStore({
    ForumDetailHandoffTarget? initialPendingHandoff,
    ForumDetailHandoffTarget? initialRecentBrowseHandoff,
    List<ForumDetailHandoffTarget> initialRecentBrowseHandoffTargets =
        const <ForumDetailHandoffTarget>[],
    String? initialRecentProfileUserId,
    ShellPostLoginTarget? initialPendingPostLoginTarget,
  })  : _pendingHandoff = initialPendingHandoff,
        _recentBrowseHandoffTargets = _normalizeRecentBrowseTargets(
          [
            ...initialRecentBrowseHandoffTargets,
            if (initialRecentBrowseHandoff != null) initialRecentBrowseHandoff,
          ],
        ),
        _recentProfileUserId = _normalizeUserId(initialRecentProfileUserId),
        _pendingPostLoginTarget = initialPendingPostLoginTarget;

  ForumDetailHandoffTarget? _pendingHandoff;
  List<ForumDetailHandoffTarget> _recentBrowseHandoffTargets;
  String? _recentProfileUserId;
  ShellPostLoginTarget? _pendingPostLoginTarget;

  @override
  Future<void> clearRecentBrowseHandoff() async {
    _recentBrowseHandoffTargets = const <ForumDetailHandoffTarget>[];
  }

  @override
  Future<void> clearRecentProfileUserId() async {
    _recentProfileUserId = null;
  }

  @override
  Future<void> clearPendingPostLoginTarget() async {
    _pendingPostLoginTarget = null;
  }

  @override
  Future<ForumDetailHandoffTarget?> readRecentBrowseHandoff() async {
    return _recentBrowseHandoffTargets.isEmpty
        ? null
        : _recentBrowseHandoffTargets.first;
  }

  @override
  Future<List<ForumDetailHandoffTarget>> readRecentBrowseHandoffs() async {
    return List<ForumDetailHandoffTarget>.unmodifiable(
      _recentBrowseHandoffTargets,
    );
  }

  @override
  Future<String?> readRecentProfileUserId() async {
    return _recentProfileUserId;
  }

  @override
  Future<ShellPostLoginTarget?> readPendingPostLoginTarget() async {
    return _pendingPostLoginTarget;
  }

  @override
  Future<ForumDetailHandoffTarget?> takePendingHandoff() async {
    final handoff = _pendingHandoff;
    _pendingHandoff = null;
    return handoff;
  }

  @override
  Future<void> writeRecentBrowseHandoff(ForumDetailHandoffTarget target) async {
    _recentBrowseHandoffTargets = _upsertRecentBrowseTarget(
      _recentBrowseHandoffTargets,
      target,
    );
  }

  @override
  Future<void> writeRecentProfileUserId(String userId) async {
    _recentProfileUserId = _normalizeUserId(userId);
  }

  @override
  Future<void> writePendingPostLoginTarget(ShellPostLoginTarget target) async {
    _pendingPostLoginTarget = ShellPostLoginTarget(
      tabIndex: target.tabIndex,
      forumTarget: target.forumTarget == null
          ? null
          : ForumDetailHandoffTarget(
              postId: target.forumTarget!.normalizedPostId,
              source: target.forumTarget!.source,
              initialTitle: target.forumTarget!.normalizedInitialTitle,
              commentId: target.forumTarget!.normalizedCommentId,
            ),
    );
  }
}

class PlatformForumFollowUpStore implements ForumFollowUpStore {
  PlatformForumFollowUpStore({
    MethodChannel? channel,
  }) : _channel =
            channel ?? const MethodChannel('radish.flutter/forum_follow_up');

  final MethodChannel _channel;

  @override
  Future<void> clearRecentBrowseHandoff() async {
    await _channel.invokeMethod<void>('clearRecentBrowseHandoff');
  }

  @override
  Future<void> clearRecentProfileUserId() async {
    await _channel.invokeMethod<void>('clearRecentProfileUserId');
  }

  @override
  Future<void> clearPendingPostLoginTarget() async {
    await _channel.invokeMethod<void>('clearPendingPostLoginTarget');
  }

  @override
  Future<ForumDetailHandoffTarget?> readRecentBrowseHandoff() async {
    final targets = await readRecentBrowseHandoffs();
    return targets.isEmpty ? null : targets.first;
  }

  @override
  Future<List<ForumDetailHandoffTarget>> readRecentBrowseHandoffs() async {
    final payload =
        await _channel.invokeMethod<String>('readRecentBrowseHandoffs');
    return _decodeTargetList(payload);
  }

  @override
  Future<String?> readRecentProfileUserId() async {
    final userId = await _channel.invokeMethod<String>(
      'readRecentProfileUserId',
    );
    return _normalizeUserId(userId);
  }

  @override
  Future<ShellPostLoginTarget?> readPendingPostLoginTarget() async {
    final payload =
        await _channel.invokeMethod<String>('readPendingPostLoginTarget');
    return _decodePostLoginTarget(payload);
  }

  @override
  Future<ForumDetailHandoffTarget?> takePendingHandoff() async {
    final payload = await _channel.invokeMethod<String>('takePendingHandoff');
    return _decodeTarget(payload);
  }

  @override
  Future<void> writeRecentBrowseHandoff(ForumDetailHandoffTarget target) async {
    await _channel.invokeMethod<void>(
      'writeRecentBrowseHandoff',
      jsonEncode(
        ForumDetailHandoffTarget(
          postId: target.normalizedPostId,
          source: ForumDetailHandoffSource.browseHistory,
          initialTitle: target.normalizedInitialTitle,
          commentId: target.normalizedCommentId,
        ).toJson(),
      ),
    );
  }

  @override
  Future<void> writeRecentProfileUserId(String userId) async {
    final normalizedUserId = _normalizeUserId(userId);
    if (normalizedUserId == null) {
      await clearRecentProfileUserId();
      return;
    }

    await _channel.invokeMethod<void>(
      'writeRecentProfileUserId',
      normalizedUserId,
    );
  }

  @override
  Future<void> writePendingPostLoginTarget(ShellPostLoginTarget target) async {
    await _channel.invokeMethod<void>(
      'writePendingPostLoginTarget',
      jsonEncode(target.toJson()),
    );
  }

  ForumDetailHandoffTarget? _decodeTarget(String? payload) {
    if (payload == null || payload.trim().isEmpty) {
      return null;
    }

    return ForumDetailHandoffTarget.fromJson(jsonDecode(payload));
  }

  List<ForumDetailHandoffTarget> _decodeTargetList(String? payload) {
    if (payload == null || payload.trim().isEmpty) {
      return const <ForumDetailHandoffTarget>[];
    }

    final decoded = jsonDecode(payload);
    if (decoded is! List) {
      return const <ForumDetailHandoffTarget>[];
    }

    return _normalizeRecentBrowseTargets(
      decoded
          .map(ForumDetailHandoffTarget.fromJson)
          .whereType<ForumDetailHandoffTarget>(),
    );
  }

  ShellPostLoginTarget? _decodePostLoginTarget(String? payload) {
    if (payload == null || payload.trim().isEmpty) {
      return null;
    }

    return ShellPostLoginTarget.fromJson(jsonDecode(payload));
  }
}

String? _normalizeUserId(String? userId) {
  final normalizedUserId = userId?.trim();
  if (normalizedUserId == null || normalizedUserId.isEmpty) {
    return null;
  }

  return normalizedUserId;
}

const int _maxRecentBrowseTargetCount = 5;

List<ForumDetailHandoffTarget> _upsertRecentBrowseTarget(
  Iterable<ForumDetailHandoffTarget> targets,
  ForumDetailHandoffTarget target,
) {
  final recentTarget = _normalizeRecentBrowseTarget(target);
  if (recentTarget == null) {
    return _normalizeRecentBrowseTargets(targets);
  }

  return _normalizeRecentBrowseTargets([
    recentTarget,
    ...targets.where(
      (item) => !_isSameRecentBrowseTarget(item, recentTarget),
    ),
  ]);
}

List<ForumDetailHandoffTarget> _normalizeRecentBrowseTargets(
  Iterable<ForumDetailHandoffTarget?> targets,
) {
  final normalizedTargets = <ForumDetailHandoffTarget>[];
  for (final target in targets) {
    final normalizedTarget = _normalizeRecentBrowseTarget(target);
    if (normalizedTarget == null) {
      continue;
    }

    if (normalizedTargets.any(
      (item) => _isSameRecentBrowseTarget(item, normalizedTarget),
    )) {
      continue;
    }

    normalizedTargets.add(normalizedTarget);
    if (normalizedTargets.length >= _maxRecentBrowseTargetCount) {
      break;
    }
  }

  return List<ForumDetailHandoffTarget>.unmodifiable(normalizedTargets);
}

ForumDetailHandoffTarget? _normalizeRecentBrowseTarget(
  ForumDetailHandoffTarget? target,
) {
  if (target == null || !target.hasValidPostId) {
    return null;
  }

  return ForumDetailHandoffTarget(
    postId: target.normalizedPostId,
    source: ForumDetailHandoffSource.browseHistory,
    initialTitle: target.normalizedInitialTitle,
    commentId: target.normalizedCommentId,
  );
}

bool _isSameRecentBrowseTarget(
  ForumDetailHandoffTarget left,
  ForumDetailHandoffTarget right,
) {
  return left.normalizedPostId == right.normalizedPostId &&
      left.normalizedCommentId == right.normalizedCommentId;
}
