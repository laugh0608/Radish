import 'package:flutter_test/flutter_test.dart';
import 'package:radish_flutter/features/forum/data/forum_models.dart';
import 'package:radish_flutter/features/notifications/data/notification_repository.dart';

void main() {
  test('parses latest forum notification target with string ids', () {
    final page = ForumNotificationPage.fromJson({
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

  test('skips non-forum notifications before selecting target', () {
    final page = ForumNotificationPage.fromJson({
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
    final page = ForumNotificationPage.fromJson({
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
}
