import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/core/config/app_environment.dart';
import 'package:radish_flutter/core/network/radish_api_client.dart';
import 'package:radish_flutter/core/network/radish_api_endpoints.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/notifications/data/notification_repository.dart';

void main() {
  test('parses latest forum notification target with string ids', () {
    final page = NotificationPage.fromJson({
      'page': 1,
      'dataCount': 1,
      'pageCount': 1,
      'data': [
        {
          'voId': 'user-notification-1',
          'voNotification': {
            'voTitle': '帖子被评论',
            'voExtData':
                '{"app":"forum","postId":"2042219067430928384","commentId":"2042219067430928999"}',
          },
        },
      ],
    });

    final target = page.latestForumTarget;

    expect(target, isNotNull);
    expect(target!.postId, '2042219067430928384');
    expect(target.commentId, '2042219067430928999');
    expect(target.initialTitle, '帖子被评论');
    expect(target.source, ForumDetailHandoffSource.notification);
  });

  test('prefers forum notification public id when present', () {
    final page = NotificationPage.fromJson({
      'data': [
        {
          'voNotification': {
            'voTitle': '帖子收到轻回应',
            'voExtData':
                '{"app":"forum","postId":"2042219067430928384","postPublicId":"pst_01HZEXAMPLEPUBLICID","commentId":"2042219067430928999"}',
          },
        },
      ],
    });

    final target = page.latestForumTarget;

    expect(target, isNotNull);
    expect(target!.postId, 'pst_01HZEXAMPLEPUBLICID');
    expect(target.commentId, '2042219067430928999');
    expect(target.initialTitle, '帖子收到轻回应');
    expect(target.source, ForumDetailHandoffSource.notification);
  });

  test('accepts public-id-only forum notification payloads', () {
    final page = NotificationPage.fromJson({
      'data': [
        {
          'voNotification': {
            'voTitle': '公开帖子回流',
            'voExtData': {
              'app': 'forum',
              'postPublicId': 'pst_01HZONLYPUBLICID',
            },
          },
        },
      ],
    });

    final target = page.latestForumTarget;

    expect(target, isNotNull);
    expect(target!.postId, 'pst_01HZONLYPUBLICID');
    expect(target.commentId, isNull);
  });

  test('skips non-forum notifications before selecting target', () {
    final page = NotificationPage.fromJson({
      'data': [
        {
          'voNotification': {
            'voTitle': '系统通知',
            'voExtData': '{"app":"system"}',
          },
        },
        {
          'voNotification': {
            'voTitle': '评论回复',
            'voExtData': {
              'app': 'forum',
              'postId': 'post-42',
              'commentId': 'reply-1',
            },
          },
        },
      ],
    });

    final target = page.latestForumTarget;

    expect(target, isNotNull);
    expect(target!.postId, 'post-42');
    expect(target.commentId, 'reply-1');
    expect(target.initialTitle, '评论回复');
  });

  test('ignores malformed forum notification payloads', () {
    final page = NotificationPage.fromJson({
      'data': [
        {
          'voNotification': {
            'voTitle': 'Broken',
            'voExtData': '{"app":"forum"}',
          },
        },
        {
          'voNotification': {
            'voTitle': 'Also broken',
            'voExtData': 'not-json',
          },
        },
      ],
    });

    expect(page.latestForumTarget, isNull);
  });

  test('returns recent forum notification targets in page order', () {
    final page = NotificationPage.fromJson({
      'data': [
        {
          'voNotification': {
            'voTitle': '系统通知',
            'voExtData': '{"app":"system"}',
          },
        },
        {
          'voNotification': {
            'voTitle': '第一条论坛通知',
            'voExtData': {
              'app': 'forum',
              'postPublicId': 'pst_first',
              'commentId': 'comment-1',
            },
          },
        },
        {
          'voNotification': {
            'voTitle': '第二条论坛通知',
            'voExtData': {
              'app': 'forum',
              'postId': 'post-second',
            },
          },
        },
      ],
    });

    final targets = page.forumTargets;

    expect(targets, hasLength(2));
    expect(targets[0].postId, 'pst_first');
    expect(targets[0].commentId, 'comment-1');
    expect(targets[0].initialTitle, '第一条论坛通知');
    expect(targets[1].postId, 'post-second');
    expect(targets[1].commentId, isNull);
    expect(page.latestForumTarget?.postId, targets.first.postId);
    expect(page.latestForumTarget?.commentId, targets.first.commentId);
  });

  test('parses readonly notification metadata alongside forum targets', () {
    final page = NotificationPage.fromJson({
      'page': 1,
      'dataCount': 2,
      'pageCount': 1,
      'data': [
        {
          'voId': 1001,
          'voNotificationId': 2001,
          'voIsRead': false,
          'voCreateTime': '2026-05-31T08:30:00',
          'voNotification': {
            'voType': 'System',
            'voTitle': '系统维护',
            'voContent': '今晚 23:00 进行维护。',
            'voBusinessType': 'System',
            'voCreateTime': '2026-05-31T08:00:00',
            'voExtData': '{"app":"system"}',
          },
        },
        {
          'voId': 1002,
          'voNotificationId': 2002,
          'voIsRead': 'true',
          'voNotification': {
            'voType': 'CommentReplied',
            'voTitle': '评论被回复',
            'voContent': '有人回复了你的评论。',
            'voBusinessType': 'Comment',
            'voExtData': {
              'app': 'forum',
              'postPublicId': 'pst_reply',
              'commentId': 'reply-1',
            },
          },
        },
      ],
    });

    expect(page.notifications, hasLength(2));
    expect(page.notifications.first.id, '1001');
    expect(page.notifications.first.notificationId, '2001');
    expect(page.notifications.first.title, '系统维护');
    expect(page.notifications.first.content, '今晚 23:00 进行维护。');
    expect(page.notifications.first.typeLabel, '系统');
    expect(page.notifications.first.isRead, isFalse);
    expect(page.notifications.first.createdAt, '2026-05-31T08:30:00');
    expect(page.notifications.first.forumTarget, isNull);

    expect(page.notifications.last.typeLabel, '评论');
    expect(page.notifications.last.isRead, isTrue);
    expect(page.notifications.last.forumTarget?.postId, 'pst_reply');
    expect(page.notifications.last.forumTarget?.commentId, 'reply-1');
  });

  test('marks a single notification as read with backend put contract',
      () async {
    final apiClient = _RecordingNotificationApiClient();
    final repository = HttpNotificationRepository(
      apiClient: apiClient,
      endpoints: const RadishApiEndpoints(AppEnvironment.development()),
    );

    final affectedRows = await repository.markAsRead(
      accessToken: 'access-token',
      notificationId: '2001',
    );

    expect(affectedRows, 1);
    expect(apiClient.lastMethod, 'PUT');
    expect(apiClient.lastUri?.path, '/api/v1/Notification/MarkAsRead');
    expect(apiClient.lastBearerToken, 'access-token');
    expect(apiClient.lastBody, {
      'notificationIds': ['2001'],
    });
  });
}

class _RecordingNotificationApiClient implements RadishApiClient {
  String? lastMethod;
  Uri? lastUri;
  Object? lastBody;
  String? lastBearerToken;

  @override
  Future<T> get<T>({
    required Uri uri,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<T> post<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<T> put<T>({
    required Uri uri,
    required Object? body,
    required JsonFactory<T> decode,
    String? bearerToken,
  }) async {
    lastMethod = 'PUT';
    lastUri = uri;
    lastBody = body;
    lastBearerToken = bearerToken;
    return decode(1);
  }
}
