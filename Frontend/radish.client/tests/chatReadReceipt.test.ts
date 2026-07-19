import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import test from 'node:test';
import type { ChatReadReceiptSummariesVo } from '@radish/http';
import type { ChannelMessageVo } from '../src/types/chat.ts';
import {
  canAdvanceChatReadState,
  resolveDirectReadBoundaryMessageId,
  selectHigherReadTarget,
  selectHighestVisiblePersistedMessageId,
  selectOwnReceiptMessageIds,
} from '../src/apps/chat/chatReadReceipts.ts';

const clientRoot = resolve(import.meta.dirname, '..');

function message(id: string | number, userId: string, recalled = false): ChannelMessageVo {
  return {
    voId: id,
    voChannelId: '70001',
    voUserId: userId,
    voUserName: `user-${userId}`,
    voType: 1,
    voContent: `message-${id}`,
    voIsRecalled: recalled,
    voCreateTime: '2026-07-19T10:00:00Z',
    voLocalStatus: typeof id === 'number' && id < 0 ? 'sending' : 'sent',
  };
}

test('活跃阅读面必须同时满足可见、聚焦、前台、尾部和无更新页', () => {
  const active = {
    documentVisible: true,
    documentFocused: true,
    windowForeground: true,
    atConversationTail: true,
    hasMoreNewerHistory: false,
    loadingHistory: false,
    navigatingHistory: false,
  };
  assert.equal(canAdvanceChatReadState(active), true);
  for (const key of [
    'documentVisible',
    'documentFocused',
    'windowForeground',
    'atConversationTail',
  ] as const) {
    assert.equal(canAdvanceChatReadState({ ...active, [key]: false }), false);
  }
  assert.equal(canAdvanceChatReadState({ ...active, hasMoreNewerHistory: true }), false);
  assert.equal(canAdvanceChatReadState({ ...active, loadingHistory: true }), false);
  assert.equal(canAdvanceChatReadState({ ...active, navigatingHistory: true }), false);
});

test('回执摘要只选择最近 20 条自己发送且未撤回的持久消息', () => {
  const ownMessages = Array.from({ length: 24 }, (_, index) => message(String(100 + index), '20001'));
  const selected = selectOwnReceiptMessageIds([
    message('80', '20002'),
    message('90', '20001', true),
    message(-1, '20001'),
    ...ownMessages,
  ], '20001');

  assert.equal(selected.length, 20);
  assert.equal(selected[0], '104');
  assert.equal(selected.at(-1), '123');
});

test('Direct 仅显示当前加载消息中的最后已读边界', () => {
  const messages = [message('100', '20001'), message('200', '20001'), message('300', '20001')];
  const summaries: ChatReadReceiptSummariesVo = {
    voChannelId: '70001',
    voMode: 'direct',
    voItems: [
      { voMessageId: '100', voPeerHasRead: true },
      { voMessageId: '200', voPeerHasRead: true },
      { voMessageId: '300', voPeerHasRead: false },
    ],
  };

  assert.equal(resolveDirectReadBoundaryMessageId(messages, '20001', summaries), '200');
  assert.equal(resolveDirectReadBoundaryMessageId(messages, '20002', summaries), null);
});

test('精确游标只选择真实可见的最高持久消息并按 LongId 单调合并', () => {
  const messages = [message('900719925474099312341', '20002'), message('900719925474099312342', '20002')];
  assert.equal(
    selectHighestVisiblePersistedMessageId(messages, new Set(['900719925474099312341'])),
    '900719925474099312341'
  );
  assert.equal(
    selectHigherReadTarget('900719925474099312341', '900719925474099312342'),
    '900719925474099312342'
  );
  assert.equal(
    selectHigherReadTarget('900719925474099312342', '900719925474099312341'),
    '900719925474099312342'
  );
});

test('正式页面应使用 REST、无个人数据 Hub 失效、权威 Store 和 PC/mobile 详情', () => {
  const apiSource = readFileSync(resolve(clientRoot, 'src/api/chat.ts'), 'utf8');
  const hubSource = readFileSync(resolve(clientRoot, 'src/services/chatHub.ts'), 'utf8');
  const storeSource = readFileSync(resolve(clientRoot, 'src/stores/chatStore.ts'), 'utf8');
  const appSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatApp.tsx'), 'utf8');
  const indicatorSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatReadReceiptIndicator.tsx'), 'utf8');
  const surfaceSource = readFileSync(resolve(clientRoot, 'src/apps/chat/useActiveChatReadSurface.ts'), 'utf8');
  const bottomSheetSource = readFileSync(
    resolve(clientRoot, '../radish.ui/src/components/BottomSheet/BottomSheet.tsx'),
    'utf8'
  );

  assert.match(apiSource, /ChannelReadState\/Advance/);
  assert.match(apiSource, /ChannelReadReceipt\/GetSummaries/);
  assert.match(apiSource, /ChannelReadReceipt\/GetReaders/);
  assert.match(hubSource, /connection\.on\('ReadReceiptsChanged'/);
  assert.doesNotMatch(hubSource, /invoke\('MarkChannelAsRead'/);
  assert.match(storeSource, /readReceiptSummaryMap: Record<string, ChatReadReceiptSummariesVo>/);
  assert.match(storeSource, /readReceiptInvalidationMap: Record<string, number>/);
  assert.match(storeSource, /readReceiptSummaryMap: \{\}/);
  assert.match(appSource, /useActiveChatReadSurface/);
  assert.match(appSource, /readSurfaceMode = 'webos-window'/);
  assert.match(appSource, /useChatReadReceipts/);
  assert.match(indicatorSource, /<BottomSheet/);
  assert.match(indicatorSource, /role="dialog"/);
  assert.match(indicatorSource, /event\.key === 'Escape'/);
  assert.match(indicatorSource, /return \(\) => \{\s+requestRevisionRef\.current \+= 1;/);
  assert.match(bottomSheetSource, /aria-modal="true"/);
  assert.match(bottomSheetSource, /e\.key !== 'Tab'/);
  assert.match(surfaceSource, /document\.visibilityState === 'visible'/);
  assert.match(surfaceSource, /document\.hasFocus\(\)/);
  assert.match(surfaceSource, /windowForeground/);
  assert.match(surfaceSource, /readSurfaceMode === 'page'/);
  assert.match(surfaceSource, /if \(!trackedWindow\) \{\s+return false;/);
  assert.match(surfaceSource, /requestAccountKey === accountKeyRef\.current/);
  assert.match(surfaceSource, /accountKeyRef\.current = ''/);
  assert.doesNotMatch(surfaceSource, /localStorage|sessionStorage|BroadcastChannel/);
});
