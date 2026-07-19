import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import type { ChannelMessageSearchItemVo } from '@radish/http';
import {
  isChatSearchCursorInvalidError,
  mergeChatSearchItems,
  toChatSearchUtcBoundary,
} from '../src/apps/chat/chatMessageSearch.ts';

const clientRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

function item(messageId: string, snippet = messageId): ChannelMessageSearchItemVo {
  return {
    voChannelId: '100',
    voMessageId: messageId,
    voChannelDisplayName: 'Search test',
    voChannelIcon: null,
    voConversationKind: 'group',
    voPeerUserId: null,
    voPeerPublicId: null,
    voPeerAvatarUrl: null,
    voSenderUserId: '200',
    voSenderDisplayName: 'Tester',
    voSenderAvatarUrl: null,
    voSnippet: snippet,
    voCreateTime: '2026-07-19T00:00:00Z',
    voMessageType: 1,
  };
}

test('消息搜索 cursor 失效只识别稳定错误码和 MessageKey', () => {
  assert.equal(isChatSearchCursorInvalidError({ code: 'Chat.SearchCursorInvalid' }), true);
  assert.equal(isChatSearchCursorInvalidError({ messageKey: 'error.chat.search_cursor_invalid' }), true);
  assert.equal(isChatSearchCursorInvalidError(new Error('cursor invalid')), false);
});

test('消息搜索继续加载应按 LongId 字符串去重并保持既有快照顺序', () => {
  const merged = mergeChatSearchItems(
    [item('900719925474099312341'), item('900719925474099312342', 'old')],
    [item('900719925474099312342', 'duplicate'), item('900719925474099312343')]
  );

  assert.deepEqual(merged.map((entry) => entry.voMessageId), [
    '900719925474099312341',
    '900719925474099312342',
    '900719925474099312343',
  ]);
  assert.equal(merged[1]?.voSnippet, 'old');
});

test('消息搜索日期应以浏览器本地自然日生成 UTC 半开区间', () => {
  const fromUtc = toChatSearchUtcBoundary('2026-07-19', false);
  const toUtc = toChatSearchUtcBoundary('2026-07-19', true);
  assert.ok(fromUtc);
  assert.ok(toUtc);

  const from = new Date(fromUtc);
  const to = new Date(toUtc);
  assert.deepEqual(
    [from.getFullYear(), from.getMonth() + 1, from.getDate(), from.getHours()],
    [2026, 7, 19, 0]
  );
  assert.deepEqual(
    [to.getFullYear(), to.getMonth() + 1, to.getDate(), to.getHours()],
    [2026, 7, 20, 0]
  );
  assert.equal(toChatSearchUtcBoundary('2026-02-30', false), undefined);
});

test('正式消息搜索应使用专属 API、内存状态和权威消息窗口定位', () => {
  const apiSource = readFileSync(resolve(clientRoot, 'src/api/chat.ts'), 'utf8');
  const searchSource = readFileSync(resolve(clientRoot, 'src/apps/chat/useChatMessageSearch.ts'), 'utf8');
  const searchPanelSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatMessageSearchPanel.tsx'), 'utf8');
  const chatSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatApp.tsx'), 'utf8');
  const chatStoreSource = readFileSync(resolve(clientRoot, 'src/stores/chatStore.ts'), 'utf8');
  const messagesSource = readFileSync(resolve(clientRoot, 'src/messages/MessagesApp.tsx'), 'utf8');

  assert.match(apiSource, /ChannelMessage\/Search/);
  assert.match(searchSource, /snapshotRef/);
  assert.doesNotMatch(searchSource, /localStorage|sessionStorage|BroadcastChannel|URLSearchParams/);
  assert.doesNotMatch(searchSource, /log\.(?:info|warn|error)[\s\S]{0,180}keyword/);
  assert.match(chatSource, /getChannelMessageWindow\(channelId, target\.messageId\)/);
  assert.match(chatSource, /failMessageWindow\(target\)/);
  assert.doesNotMatch(chatSource, /if \(shouldFallbackToLatest\)/);
  assert.match(messagesSource, /window\.history\.pushState/);
  assert.match(messagesSource, /window\.addEventListener\('popstate'/);
  assert.match(messagesSource, /__radishMessagesSearchRestore/);
  assert.match(messagesSource, /__radishMessagesSearchTarget/);
  assert.match(messagesSource, /window\.history\.back\(\)/);
  assert.match(messagesSource, /searchHideRevision/);
  assert.match(searchPanelSource, /event\.key !== 'Enter' \|\| event\.nativeEvent\.isComposing/);
  assert.match(searchPanelSource, /onKeyDown=\{handleKeywordKeyDown\}/);
  assert.match(searchPanelSource, /search\.items\.filter\(\(item\) => !recalledMessageIds\[item\.voMessageId\]\)/);
  assert.match(chatStoreSource, /recalledMessageIds: Record<string, true>/);
  assert.match(chatStoreSource, /recalledMessageIds: state\.recalledMessageIds\[messageKey\]/);
  assert.match(chatStoreSource, /recalledMessageIds: \{\}/);
  assert.match(chatSource, /searchMounted &&/);
  assert.match(chatSource, /hidden=\{!searchOpen\}/);
  assert.match(chatSource, /searchHideRevision <= handledSearchHideRevisionRef\.current/);
  assert.match(chatSource, /handledSearchHideRevisionRef\.current = searchHideRevision/);
  assert.match(chatSource, /if \(isCompactViewport\) \{\s*setSearchOpen\(false\)/);
  assert.doesNotMatch(chatSource, /setLoadingHistory\(true\);\s*setChannelMessages\(channelId, \[\]\);\s*updateHistoryAvailability\(channelId, false, false\);\s*try \{\s*const windowData/);
});
