import 'dart:convert';

import '../../../core/network/radish_api_client.dart';
import '../../../core/network/radish_api_endpoints.dart';
import '../../forum/data/forum_models.dart';

abstract class NotificationRepository {
  Future<NotificationPage> getNotifications({
    required String accessToken,
    int pageSize = 20,
  }) async {
    final targets = await getForumTargets(
      accessToken: accessToken,
      pageSize: pageSize,
    );
    return NotificationPage.fromForumTargets(targets);
  }

  Future<List<ForumDetailHandoffTarget>> getForumTargets({
    required String accessToken,
    int pageSize = 20,
  });

  Future<ForumDetailHandoffTarget?> getLatestForumTarget({
    required String accessToken,
    int pageSize = 20,
  }) async {
    final targets = await getForumTargets(
      accessToken: accessToken,
      pageSize: pageSize,
    );
    return targets.isEmpty ? null : targets.first;
  }
}

class EmptyNotificationRepository implements NotificationRepository {
  const EmptyNotificationRepository();

  @override
  Future<NotificationPage> getNotifications({
    required String accessToken,
    int pageSize = 20,
  }) async {
    return const NotificationPage(notifications: <NotificationListItem>[]);
  }

  @override
  Future<ForumDetailHandoffTarget?> getLatestForumTarget({
    required String accessToken,
    int pageSize = 20,
  }) async {
    return null;
  }

  @override
  Future<List<ForumDetailHandoffTarget>> getForumTargets({
    required String accessToken,
    int pageSize = 20,
  }) async {
    return const <ForumDetailHandoffTarget>[];
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
  Future<NotificationPage> getNotifications({
    required String accessToken,
    int pageSize = 20,
  }) async {
    final normalizedAccessToken = accessToken.trim();
    if (normalizedAccessToken.isEmpty) {
      return const NotificationPage(notifications: <NotificationListItem>[]);
    }

    final uri = endpoints.resolveApi(
      '/api/v1/Notification/GetNotificationList',
      queryParameters: {
        'pageIndex': '1',
        'pageSize': pageSize.clamp(1, 50).toString(),
      },
    );

    return apiClient.get(
      uri: uri,
      bearerToken: normalizedAccessToken,
      decode: NotificationPage.fromJson,
    );
  }

  @override
  Future<ForumDetailHandoffTarget?> getLatestForumTarget({
    required String accessToken,
    int pageSize = 20,
  }) async {
    final targets = await getForumTargets(
      accessToken: accessToken,
      pageSize: pageSize,
    );
    return targets.isEmpty ? null : targets.first;
  }

  @override
  Future<List<ForumDetailHandoffTarget>> getForumTargets({
    required String accessToken,
    int pageSize = 20,
  }) async {
    final page = await getNotifications(
      accessToken: accessToken,
      pageSize: pageSize,
    );

    return page.forumTargets;
  }
}

class NotificationPage {
  const NotificationPage({
    required this.notifications,
  });

  factory NotificationPage.fromJson(Object? json) {
    final map = _readJsonMap(json);
    final data = map['data'];
    final notifications = data is List
        ? data.map(NotificationListItem.fromJson).toList()
        : const <NotificationListItem>[];

    return NotificationPage(
      notifications: notifications,
    );
  }

  factory NotificationPage.fromForumTargets(
    List<ForumDetailHandoffTarget> targets,
  ) {
    final notifications = <NotificationListItem>[];
    for (var index = 0; index < targets.length; index += 1) {
      notifications.add(
        NotificationListItem.fromForumTarget(
          id: 'forum-target-$index',
          target: targets[index],
        ),
      );
    }

    return NotificationPage(notifications: notifications);
  }

  final List<NotificationListItem> notifications;

  List<ForumDetailHandoffTarget> get forumTargets {
    final targets = <ForumDetailHandoffTarget>[];
    for (final notification in notifications) {
      final target = notification.forumTarget;
      if (target != null) {
        targets.add(target);
      }
    }

    return List<ForumDetailHandoffTarget>.unmodifiable(targets);
  }

  ForumDetailHandoffTarget? get latestForumTarget {
    final targets = forumTargets;
    return targets.isEmpty ? null : targets.first;
  }
}

class NotificationListItem {
  const NotificationListItem({
    required this.id,
    required this.notificationId,
    required this.notification,
    required this.isRead,
    required this.createdAt,
  });

  factory NotificationListItem.fromJson(Object? json) {
    final map = _readJsonMap(json);
    return NotificationListItem(
      id: _readString(map['voId']),
      notificationId: _readString(map['voNotificationId']),
      notification: NotificationPayload.fromJson(map['voNotification']),
      isRead: _readBool(map['voIsRead']),
      createdAt: _readString(map['voCreateTime']),
    );
  }

  factory NotificationListItem.fromForumTarget({
    required String id,
    required ForumDetailHandoffTarget target,
  }) {
    return NotificationListItem(
      id: id,
      notificationId: null,
      notification: NotificationPayload.fromForumTarget(target),
      isRead: false,
      createdAt: null,
    );
  }

  final String? id;
  final String? notificationId;
  final NotificationPayload? notification;
  final bool isRead;
  final String? createdAt;

  String get title => notification?.title ?? '通知';

  String? get content => notification?.content;

  String get typeLabel => notification?.typeLabel ?? '站内通知';

  ForumDetailHandoffTarget? get forumTarget => notification?.forumTarget;
}

class ForumNotificationItem {
  const ForumNotificationItem({
    required this.notification,
  });

  factory ForumNotificationItem.fromJson(Object? json) {
    final map = _readJsonMap(json);
    return ForumNotificationItem(
      notification: NotificationPayload.fromJson(map['voNotification']),
    );
  }

  final NotificationPayload? notification;

  ForumDetailHandoffTarget? get forumTarget => notification?.forumTarget;
}

class NotificationPayload {
  const NotificationPayload({
    required this.title,
    required this.content,
    required this.type,
    required this.businessType,
    required this.createdAt,
    required this.extData,
  });

  factory NotificationPayload.fromJson(Object? json) {
    final map = _tryReadJsonMap(json);
    if (map == null) {
      return const NotificationPayload(
        title: null,
        content: null,
        type: null,
        businessType: null,
        createdAt: null,
        extData: null,
      );
    }

    return NotificationPayload(
      title: _readString(map['voTitle']),
      content: _readString(map['voContent']),
      type: _readString(map['voType']),
      businessType: _readString(map['voBusinessType']),
      createdAt: _readString(map['voCreateTime']),
      extData: _readExtData(map['voExtData']),
    );
  }

  factory NotificationPayload.fromForumTarget(ForumDetailHandoffTarget target) {
    return NotificationPayload(
      title: target.normalizedInitialTitle ?? '论坛通知',
      content: _buildForumTargetContent(target),
      type: 'Forum',
      businessType: 'Post',
      createdAt: null,
      extData: {
        'app': 'forum',
        'postId': target.postId,
        if (target.normalizedCommentId != null)
          'commentId': target.normalizedCommentId,
      },
    );
  }

  final String? title;
  final String? content;
  final String? type;
  final String? businessType;
  final String? createdAt;
  final Map<String, Object?>? extData;

  String? get typeLabel {
    final normalizedBusinessType = businessType?.trim();
    if (normalizedBusinessType != null && normalizedBusinessType.isNotEmpty) {
      return _translateNotificationType(normalizedBusinessType);
    }

    final normalizedType = type?.trim();
    if (normalizedType != null && normalizedType.isNotEmpty) {
      return _translateNotificationType(normalizedType);
    }

    return null;
  }

  ForumDetailHandoffTarget? get forumTarget {
    final ext = extData;
    if (ext == null) {
      return null;
    }

    final app = _readString(ext['app']);
    final postPublicId = _readString(ext['postPublicId']);
    final postId = _readString(ext['postId']);
    final routePostId = postPublicId ?? postId;
    if (app != 'forum' || routePostId == null || routePostId.isEmpty) {
      return null;
    }

    return ForumDetailHandoffTarget(
      postId: routePostId,
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

typedef ForumNotificationPage = NotificationPage;
typedef ForumNotificationPayload = NotificationPayload;

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

bool _readBool(Object? value) {
  if (value is bool) {
    return value;
  }

  final text = _readString(value)?.toLowerCase();
  return text == 'true' || text == '1';
}

String _buildForumTargetContent(ForumDetailHandoffTarget target) {
  final commentId = target.normalizedCommentId;
  if (commentId == null) {
    return '可返回相关帖子';
  }

  return '可返回相关评论';
}

String _translateNotificationType(String type) {
  switch (type) {
    case 'Post':
    case 'Forum':
      return '论坛';
    case 'Comment':
      return '评论';
    case 'User':
      return '用户';
    case 'System':
      return '系统';
    case 'CommentReplied':
      return '评论回复';
    case 'PostLiked':
      return '帖子获赞';
    case 'CommentLiked':
      return '评论获赞';
    case 'Mentioned':
      return '提及';
    case 'GodComment':
      return '神评';
    case 'Sofa':
      return '抢沙发';
    default:
      return type;
  }
}
