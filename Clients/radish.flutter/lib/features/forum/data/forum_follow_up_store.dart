import 'dart:convert';

import 'package:flutter/services.dart';

import 'forum_models.dart';

abstract class ForumFollowUpStore {
  Future<ForumDetailHandoffTarget?> takePendingHandoff();

  Future<ForumDetailHandoffTarget?> readRecentBrowseHandoff();

  Future<void> writeRecentBrowseHandoff(ForumDetailHandoffTarget target);

  Future<void> clearRecentBrowseHandoff();

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
    ShellPostLoginTarget? initialPendingPostLoginTarget,
  })  : _pendingHandoff = initialPendingHandoff,
        _recentBrowseHandoff = initialRecentBrowseHandoff,
        _pendingPostLoginTarget = initialPendingPostLoginTarget;

  ForumDetailHandoffTarget? _pendingHandoff;
  ForumDetailHandoffTarget? _recentBrowseHandoff;
  ShellPostLoginTarget? _pendingPostLoginTarget;

  @override
  Future<void> clearRecentBrowseHandoff() async {
    _recentBrowseHandoff = null;
  }

  @override
  Future<void> clearPendingPostLoginTarget() async {
    _pendingPostLoginTarget = null;
  }

  @override
  Future<ForumDetailHandoffTarget?> readRecentBrowseHandoff() async {
    return _recentBrowseHandoff;
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
    _recentBrowseHandoff = ForumDetailHandoffTarget(
      postId: target.normalizedPostId,
      source: ForumDetailHandoffSource.browseHistory,
      initialTitle: target.normalizedInitialTitle,
      commentId: target.normalizedCommentId,
    );
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
  Future<void> clearPendingPostLoginTarget() async {
    await _channel.invokeMethod<void>('clearPendingPostLoginTarget');
  }

  @override
  Future<ForumDetailHandoffTarget?> readRecentBrowseHandoff() async {
    final payload =
        await _channel.invokeMethod<String>('readRecentBrowseHandoff');
    return _decodeTarget(payload);
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

  ShellPostLoginTarget? _decodePostLoginTarget(String? payload) {
    if (payload == null || payload.trim().isEmpty) {
      return null;
    }

    return ShellPostLoginTarget.fromJson(jsonDecode(payload));
  }
}
