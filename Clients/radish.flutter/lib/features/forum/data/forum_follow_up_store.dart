import 'dart:convert';

import 'package:flutter/services.dart';

import 'forum_models.dart';

abstract class ForumFollowUpStore {
  Future<ForumDetailHandoffTarget?> takePendingHandoff();

  Future<ForumDetailHandoffTarget?> readRecentBrowseHandoff();

  Future<void> writeRecentBrowseHandoff(ForumDetailHandoffTarget target);

  Future<void> clearRecentBrowseHandoff();
}

class InMemoryForumFollowUpStore implements ForumFollowUpStore {
  InMemoryForumFollowUpStore({
    ForumDetailHandoffTarget? initialPendingHandoff,
    ForumDetailHandoffTarget? initialRecentBrowseHandoff,
  })  : _pendingHandoff = initialPendingHandoff,
        _recentBrowseHandoff = initialRecentBrowseHandoff;

  ForumDetailHandoffTarget? _pendingHandoff;
  ForumDetailHandoffTarget? _recentBrowseHandoff;

  @override
  Future<void> clearRecentBrowseHandoff() async {
    _recentBrowseHandoff = null;
  }

  @override
  Future<ForumDetailHandoffTarget?> readRecentBrowseHandoff() async {
    return _recentBrowseHandoff;
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
  Future<ForumDetailHandoffTarget?> readRecentBrowseHandoff() async {
    final payload =
        await _channel.invokeMethod<String>('readRecentBrowseHandoff');
    return _decodeTarget(payload);
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

  ForumDetailHandoffTarget? _decodeTarget(String? payload) {
    if (payload == null || payload.trim().isEmpty) {
      return null;
    }

    return ForumDetailHandoffTarget.fromJson(jsonDecode(payload));
  }
}
