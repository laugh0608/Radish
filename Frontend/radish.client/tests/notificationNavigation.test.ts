import assert from 'node:assert/strict';
import test from 'node:test';
import type { NotificationTargetKind, NotificationTargetVo } from '@radish/http';
import { resolveWebNotificationNavigation } from '../src/utils/notificationNavigation.ts';

const postPublicId = 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f';
const notificationsSourceState = {
  app: 'notifications',
  route: { kind: 'index' },
} as const;

function target(kind: NotificationTargetKind, fields: Partial<NotificationTargetVo> = {}): NotificationTargetVo {
  return {
    voKind: kind,
    voPostId: null,
    voPostPublicId: null,
    voCommentId: null,
    voChannelId: null,
    voMessageId: null,
    voUserId: null,
    voUserPublicId: null,
    voOrderId: null,
    voBenefitId: null,
    voDocumentSlug: null,
    voGovernanceCaseId: null,
    voUnavailableReason: null,
    ...fields,
  };
}

test('结构化论坛 target 优先使用公开 ID，并保留评论锚点来源', () => {
  assert.deepEqual(
    resolveWebNotificationNavigation(target('ForumPost', {
      voPostId: '2042219067430928384',
      voPostPublicId: postPublicId,
      voCommentId: '2042219067430928385',
    })),
    {
      surface: 'web',
      href: `/forum/post/${postPublicId}?commentId=2042219067430928385`,
      sourceState: { forumDetailSourceRoute: notificationsSourceState },
    },
  );
});

test('结构化聊天、用户、订单、库存、成长和文档 target 使用正式路由', () => {
  assert.deepEqual(
    resolveWebNotificationNavigation(target('ChatConversation', {
      voChannelId: '2042219067430928390',
      voMessageId: '2042219067430928391',
    })),
    { surface: 'web', href: '/messages?channelId=2042219067430928390&messageId=2042219067430928391' },
  );
  assert.deepEqual(
    resolveWebNotificationNavigation(target('UserProfile', { voUserPublicId: 'usr_public' })),
    {
      surface: 'web',
      href: '/u/usr_public',
      sourceState: { profileSourceRoute: notificationsSourceState },
    },
  );
  assert.deepEqual(
    resolveWebNotificationNavigation(target('ShopOrder', { voOrderId: '2042219067430928392' })),
    { surface: 'web', href: '/shop/order/2042219067430928392' },
  );
  assert.deepEqual(resolveWebNotificationNavigation(target('Inventory')), {
    surface: 'web', href: '/shop/inventory',
  });
  assert.deepEqual(resolveWebNotificationNavigation(target('Experience')), {
    surface: 'web', href: '/me/experience',
  });
  assert.deepEqual(
    resolveWebNotificationNavigation(target('DocsDocument', { voDocumentSlug: 'notification-center' })),
    { surface: 'web', href: '/docs/notification-center' },
  );
});

test('治理 target 只使用结构化 case id 并带通知返回路径', () => {
  assert.deepEqual(
    resolveWebNotificationNavigation(target('GovernanceCase', { voGovernanceCaseId: '2042219067430928393' })),
    {
      surface: 'console',
      href: '/console/moderation?sourceReportId=2042219067430928393&backTo=%2Fnotifications',
    },
  );
});

test('无效 target 不回退到泛化页面，也不从内容猜测', () => {
  assert.equal(resolveWebNotificationNavigation(target('None')), null);
  assert.equal(resolveWebNotificationNavigation(target('ForumPost', { voPostId: '0' })), null);
  assert.equal(resolveWebNotificationNavigation(target('ChatConversation')), null);
  assert.equal(resolveWebNotificationNavigation(target('ShopOrder')), null);
  assert.equal(resolveWebNotificationNavigation(target('DocsDocument', { voDocumentSlug: ' ' })), null);
  assert.equal(resolveWebNotificationNavigation(target('GovernanceCase', { voGovernanceCaseId: 'bad' })), null);
});
