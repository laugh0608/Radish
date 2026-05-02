import 'dart:convert';

import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import '../../forum/data/forum_models.dart';

abstract class NotificationRepository {
  Future<ForumDetailHandoffTarget?> getLatestForumTarget({
    required String accessToken,
    int pageSize = 20,
  });
}

class EmptyNotificationRepository implements NotificationRepository {
  const EmptyNotificationRepository();

  @override
  Future<ForumDetailHandoffTarget?> getLatestForumTarget({
    required String accessToken,
    int pageSize = 20,
  }) async {
    return null;
  }
}

class HttpNotificationRepository implements NotificationRepository {
  const HttpNotificationRepository({
    required this.apiClient,
    required this.endpoints,
  });

  final RadishApiClient apiClient;
  final RadishApiEndpoints endpoints;

  @override
  Future<ForumDetailHandoffTarget?> getLatestForumTarget({
    required String accessToken,
    int pageSize = 20,
  }) async {
    final normalizedAccessToken = accessToken.trim();
    if (normalizedAccessToken.isEmpty) {
      return null;
    }

    final uri = endpoints.resolveApi(
      '/api/v1/Notification/GetNotificationList',
      queryParameters: {
        'pageIndex': '1',
        'pageSize': pageSize.clamp(1, 50).toString(),
      },
    );

    final page = await apiClient.get(
      uri: uri,
      bearerToken: normalizedAccessToken,
      decode: ForumNotificationPage.fromJson,
    );

    return page.latestForumTarget;
  }
}

class ForumNotificationPage {
  const ForumNotificationPage({
    required this.notifications,
  });

  factory ForumNotificationPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['data'];
    final notifications = data is List
        ? data.map(ForumNotificationItem.fromJson).toList()
        : const <ForumNotificationItem>[];

    return ForumNotificationPage(
      notifications: notifications,
    );
  }

  final List<ForumNotificationItem> notifications;

  ForumDetailHandoffTarget? get latestForumTarget {
    for (final notification in notifications) {
      final target = notification.forumTarget;
      if (target != null) {
        return target;
      }
    }

    return null;
  }
}

class ForumNotificationItem {
  const ForumNotificationItem({
    required this.notification,
  });

  factory ForumNotificationItem.fromJson(Object? json) {
    final map = _readJsonMap(json);
    return ForumNotificationItem(
      notification: ForumNotificationPayload.fromJson(map['voNotification']),
    );
  }

  final ForumNotificationPayload? notification;

  ForumDetailHandoffTarget? get forumTarget => notification?.forumTarget;
}

class ForumNotificationPayload {
  const ForumNotificationPayload({
    required this.title,
    required this.extData,
  });

  factory ForumNotificationPayload.fromJson(Object? json) {
    final map = _tryReadJsonMap(json);
    if (map == null) {
      return const ForumNotificationPayload(
        title: null,
        extData: null,
      );
    }

    return ForumNotificationPayload(
      title: _readString(map['voTitle']),
      extData: _readExtData(map['voExtData']),
    );
  }

  final String? title;
  final Map<String, Object?>? extData;

  ForumDetailHandoffTarget? get forumTarget {
    final ext = extData;
    if (ext == null) {
      return null;
    }

    final app = _readString(ext['app']);
    final postId = _readString(ext['postId']);
    if (app != 'forum' || postId == null || postId.isEmpty) {
      return null;
    }

    return ForumDetailHandoffTarget(
      postId: postId,
      source: ForumDetailHandoffSource.notification,
      initialTitle: title,
      commentId: _readString(ext['commentId']),
    );
  }

  static Map<String, Object?>? _readExtData(Object? value) {
    if (value is Map) {
      return Map<String, Object?>.from(value.cast<Object?, Object?>());
    }

    final text = _readString(value);
    if (text == null) {
      return null;
    }

    try {
      final decoded = jsonDecode(text);
      return _tryReadJsonMap(decoded);
    } on FormatException {
      return null;
    }
  }
}

Map<String, Object?> _readJsonMap(Object? json) {
  if (json is Map) {
    return Map<String, Object?>.from(json.cast<Object?, Object?>());
  }

  throw const FormatException('Expected a JSON object.');
}

Map<String, Object?>? _tryReadJsonMap(Object? json) {
  if (json is! Map) {
    return null;
  }

  return Map<String, Object?>.from(json.cast<Object?, Object?>());
}

String? _readString(Object? value) {
  if (value == null) {
    return null;
  }

  final text = value.toString().trim();
  return text.isEmpty ? null : text;
}
