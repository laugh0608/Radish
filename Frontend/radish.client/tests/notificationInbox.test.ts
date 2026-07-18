import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import type { NotificationInboxGroupVo } from '@radish/http';
import { enCommunity } from '../src/locales/en/community.ts';
import { zhCommunity } from '../src/locales/zh/community.ts';
import {
  buildNotificationPreferenceUpdates,
  canApplyNotificationInboxPage,
  canApplyNotificationSummary,
  compareNotificationRevisions,
  getNotificationTargetUnavailableKey,
  isNotificationCursorExpiredError,
  mergeNotificationGroups,
  notificationCategoryDefinitions,
  normalizeRevision,
  parseNotificationCount,
} from '../src/notifications/notificationInbox.ts';

function group(id: string, title = id): NotificationInboxGroupVo {
  return {
    voGroupId: id,
    voLatestNotificationId: id,
    voCategory: 'System',
    voKind: 'System',
    voTitle: title,
    voContent: '',
    voPriority: 1,
    voOccurrenceCount: '1',
    voUnreadOccurrenceCount: '1',
    voDistinctTriggerCount: '1',
    voIsRead: false,
    voFirstOccurredAtUtc: '2026-07-18T00:00:00Z',
    voLastOccurredAtUtc: '2026-07-18T00:00:00Z',
    voTriggerId: null,
    voTriggerName: null,
    voTriggerAvatar: null,
    voTarget: {
      voKind: 'None',
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
    },
  };
}

test('revision 比较保留长整数精度并抵御乱序与重复', () => {
  assert.equal(normalizeRevision('00042'), '42');
  assert.equal(compareNotificationRevisions('900719925474099312345', '900719925474099312344'), 1);
  assert.equal(compareNotificationRevisions('42', '42'), 0);
  assert.equal(compareNotificationRevisions('41', '42'), -1);
  assert.equal(parseNotificationCount('not-a-count'), 0);
  assert.equal(canApplyNotificationSummary('42', '41'), true);
  assert.equal(canApplyNotificationSummary('41', '42'), false);
});

test('cursor 分页合并按 group id 去重并用权威新项替换', () => {
  assert.deepEqual(
    mergeNotificationGroups([group('1', 'old'), group('2')], [group('1', 'new'), group('3')])
      .map((item) => [item.voGroupId, item.voTitle]),
    [['1', 'new'], ['2', '2'], ['3', '3']],
  );
  assert.equal(canApplyNotificationInboxPage('42', '42', '42', true), true);
  assert.equal(canApplyNotificationInboxPage('43', '42', '42', true), false);
  assert.equal(canApplyNotificationInboxPage('43', '42', '42', false), true);
  assert.equal(isNotificationCursorExpiredError({ code: 'Notification.CursorExpired' }), true);
  assert.equal(isNotificationCursorExpiredError({ messageKey: 'error.notification.cursor_expired' }), true);
  assert.equal(isNotificationCursorExpiredError(new Error('cursor expired')), false);
});

test('偏好更新只提交稳定分类和两个已实现开关', () => {
  assert.deepEqual(buildNotificationPreferenceUpdates([{
    voCategory: 'Reaction',
    voInAppEnabled: true,
    voRealtimePreviewEnabled: false,
    voCanDisableInApp: true,
    voCanDisableRealtimePreview: true,
    voSupportedKinds: ['PostLiked'],
  }]), [{
    category: 'Reaction',
    inAppEnabled: true,
    realtimePreviewEnabled: false,
  }]);
});

test('目标失效原因与所有分类均具备中英文展示', () => {
  const invalid = group('1');
  invalid.voTarget.voUnavailableReason = 'TargetDeleted';
  assert.equal(getNotificationTargetUnavailableKey(invalid), 'notification.targetUnavailable.deleted');

  for (const definition of notificationCategoryDefinitions) {
    assert.equal(typeof zhCommunity[definition.labelKey as keyof typeof zhCommunity], 'string');
    assert.equal(typeof enCommunity[definition.labelKey as keyof typeof enCommunity], 'string');
    assert.equal(typeof zhCommunity[definition.descriptionKey as keyof typeof zhCommunity], 'string');
    assert.equal(typeof enCommunity[definition.descriptionKey as keyof typeof enCommunity], 'string');
  }
});

test('共享状态只接受服务端摘要和 revision，不包含本地计数模拟', () => {
  const source = readFileSync(new URL('../src/stores/notificationStore.ts', import.meta.url), 'utf8');
  const syncSource = readFileSync(new URL('../src/services/notificationInboxSync.ts', import.meta.url), 'utf8');
  const hubSource = readFileSync(new URL('../src/services/notificationHub.ts', import.meta.url), 'utf8');
  assert.doesNotMatch(source, /incrementUnread|decrementUnread|setUnreadCount/);
  assert.match(source, /applySummary/);
  assert.match(source, /noteInboxChanged/);
  assert.match(source, /compareNotificationRevisions/);
  assert.match(syncSource, /message\.userId !== userId/);
  assert.match(syncSource, /requestKey = `\$\{requestUserId\}:/);
  assert.match(syncSource, /currentUserId\(\) !== requestUserId/);
  assert.match(hubSource, /UnreadCountChanged/);
  assert.match(hubSource, /notificationInboxSync\.reconcile/);
  assert.doesNotMatch(hubSource, /setUnreadCount/);
  assert.match(hubSource, /connection was stopped during negotiation/);
  assert.ok(hubSource.indexOf('this.isStarting = true') < hubSource.indexOf('await tokenService.getValidAccessToken()'));
});

test('WebOS 壳层不在卸载清理中停止账号级通知连接', () => {
  const shellSource = readFileSync(new URL('../src/desktop/Shell.tsx', import.meta.url), 'utf8');
  const notificationEffect = shellSource.match(
    /\/\/ 根据认证状态控制 SignalR 连接([\s\S]*?)useEffect\(\(\) => \{([\s\S]*?)\}, \[isAuthenticated\]\);/,
  );
  assert.ok(notificationEffect);
  assert.match(notificationEffect[2], /notificationHub\.start\(\)/);
  assert.match(notificationEffect[2], /notificationHub\.stop\(\)/);
  assert.match(notificationEffect[2], /!tokenService\.getAccessToken\(\)/);
  assert.doesNotMatch(notificationEffect[2], /return \(\) =>/);
  assert.doesNotMatch(notificationEffect[2], /setTimeout/);
});
