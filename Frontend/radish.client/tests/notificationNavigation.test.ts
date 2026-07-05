import assert from 'node:assert/strict';
import test from 'node:test';
import { resolveWebNotificationNavigation } from '../src/utils/notificationNavigation.ts';
import {
  getNotificationActionScope,
  resolveNotificationPreview,
  toNotificationStoreItem,
} from '../src/notifications/notificationActionQueue.ts';

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

test('resolveWebNotificationNavigation 应将公开个人页和正式 Web 私有目标分流', () => {
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
      surface: 'web',
      href: `/shop/order/${orderId}`,
    },
  );

  assert.deepEqual(
    resolveWebNotificationNavigation({
      businessType: 'Order',
      businessId: '0',
    }),
    {
      surface: 'web',
      href: '/shop/orders',
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
      surface: 'web',
      href: '/messages?channelId=2042219067430928390&messageId=2042219067430928391',
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

test('notificationActionQueue 应按社区复访场景归类通知', () => {
  const messageTarget = resolveWebNotificationNavigation({
    extData: JSON.stringify({
      app: 'chat',
      channelId: '2042219067430928390',
      messageId: '2042219067430928391',
    }),
  });
  assert.equal(
    getNotificationActionScope({
      id: '1',
      type: 'system',
      title: '新的频道消息',
      content: '你收到一条聊天消息',
      businessType: 'ChannelMessage',
      businessId: '2042219067430928391',
      extData: null,
      isRead: false,
      createdAt: '2026-07-05T12:00:00Z',
    }, messageTarget),
    'messages',
  );

  assert.equal(
    getNotificationActionScope({
      id: '2',
      type: 'system',
      title: '宠物状态变化',
      content: '宠物照护已完成',
      businessType: 'PetCare',
      businessId: '2042219067430928392',
      extData: null,
      isRead: false,
      createdAt: '2026-07-05T12:00:00Z',
    }, null),
    'pet',
  );

  assert.equal(
    getNotificationActionScope({
      id: '3',
      type: 'system',
      title: '举报审核结果',
      content: '你的举报已完成治理审核',
      businessType: 'ModerationReport',
      businessId: '2042219067430928393',
      extData: null,
      isRead: false,
      createdAt: '2026-07-05T12:00:00Z',
    }, null),
    'governance',
  );
});

test('notificationActionQueue 应把后端通知 VO 映射为可回跳预览', () => {
  const storeItem = toNotificationStoreItem({
    voId: '2042219067430928400',
    voUserId: userId,
    voNotificationId: '2042219067430928401',
    voIsRead: false,
    voReadAt: null,
    voDeliveryStatus: 'Delivered',
    voDeliveredAt: '2026-07-05T12:00:00Z',
    voCreateTime: '2026-07-05T12:00:00Z',
    voNotification: {
      voId: '2042219067430928401',
      voType: 'Followed',
      voPriority: 1,
      voTitle: '新的关注',
      voContent: '有人关注了你',
      voBusinessType: 'User',
      voBusinessId: userId,
      voTriggerId: userId,
      voTriggerName: 'Radish User',
      voTriggerAvatar: null,
      voExtData: null,
      voCreateTime: '2026-07-05T12:00:00Z',
    },
  });

  assert.ok(storeItem);
  assert.equal(storeItem.type, 'follow');

  const preview = resolveNotificationPreview(storeItem);
  assert.equal(preview.target?.href, `/u/${userId}`);
  assert.equal(getNotificationActionScope(preview, preview.target), 'follow');
});
