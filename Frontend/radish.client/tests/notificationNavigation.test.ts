import assert from 'node:assert/strict';
import test from 'node:test';
import { resolveWebNotificationNavigation } from '../src/utils/notificationNavigation.ts';

const postPublicId = 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f';
const postId = '2042219067430928384';
const commentId = '2042219067430928385';
const userId = '2042219067430928386';
const orderId = '2042219067430928387';
const notificationsSourceState = {
  app: 'notifications',
  route: { kind: 'index' },
} as const;

test('resolveWebNotificationNavigation 应将论坛通知优先导向公开帖子详情', () => {
  assert.deepEqual(
    resolveWebNotificationNavigation({
      extData: JSON.stringify({
        app: 'forum',
        postPublicId,
        commentId,
      }),
    }),
    {
      surface: 'web',
      href: `/forum/post/${postPublicId}?commentId=${commentId}`,
      sourceState: {
        forumDetailSourceRoute: notificationsSourceState,
      },
    },
  );

  assert.deepEqual(
    resolveWebNotificationNavigation({
      businessType: 'Post',
      businessId: postId,
    }),
    {
      surface: 'web',
      href: `/forum/post/${postId}`,
      sourceState: {
        forumDetailSourceRoute: notificationsSourceState,
      },
    },
  );
});

test('resolveWebNotificationNavigation 应将公开个人页和工作台私有目标分流', () => {
  assert.deepEqual(
    resolveWebNotificationNavigation({
      type: 'follow',
      triggerId: userId,
    }),
    {
      surface: 'web',
      href: `/u/${userId}`,
      sourceState: {
        profileSourceRoute: notificationsSourceState,
      },
    },
  );

  assert.deepEqual(
    resolveWebNotificationNavigation({
      businessType: 'Order',
      businessId: orderId,
    }),
    {
      surface: 'desktop',
      href: `/desktop?app=shop&orderId=${orderId}`,
    },
  );

  assert.deepEqual(
    resolveWebNotificationNavigation({
      extData: JSON.stringify({
        app: 'chat',
        channelId: '2042219067430928390',
        messageId: '2042219067430928391',
      }),
    }),
    {
      surface: 'desktop',
      href: '/desktop?app=chat&channelId=2042219067430928390&messageId=2042219067430928391',
    },
  );
});

test('resolveWebNotificationNavigation 应为弱上下文互动通知回到论坛，并拒绝无效目标', () => {
  assert.deepEqual(
    resolveWebNotificationNavigation({
      businessType: 'Comment',
      businessId: commentId,
    }),
    {
      surface: 'web',
      href: '/forum',
    },
  );

  assert.deepEqual(
    resolveWebNotificationNavigation({
      type: 'mention',
    }),
    {
      surface: 'web',
      href: '/forum',
    },
  );

  assert.equal(
    resolveWebNotificationNavigation({
      businessType: 'Post',
      businessId: '0',
    }),
    null,
  );
});
