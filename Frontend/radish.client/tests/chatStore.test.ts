import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import test from 'node:test';
import { buildFailedMessageRetryRequest } from '../src/apps/chat/chatApp.helpers.ts';
import { useChatStore } from '../src/stores/chatStore.ts';
import { mergeChannelLastMessage } from '../src/utils/chatChannelProjection.ts';
import type { ChannelMessageVo, ChannelVo } from '../src/types/chat.ts';

function channel(lastMessage?: ChannelMessageVo): ChannelVo {
  return {
    voId: '70001',
    voName: '测试频道',
    voSlug: 'test-channel',
    voType: 3,
    voConversationKind: 'group',
    voCanSend: true,
    voCanReact: true,
    voCanPinMessages: true,
    voCanAccept: false,
    voCanDecline: false,
    voCanBlock: false,
    voCanUnblock: false,
    voIsBlockedByCurrentUser: false,
    voIsArchived: false,
    voIsPeerAvailable: true,
    voSort: 10,
    voUnreadCount: 1,
    voHasMention: false,
    voLastMessage: lastMessage,
  };
}

function message(id: string, content: string, createTime: string): ChannelMessageVo {
  return {
    voId: id,
    voChannelId: '70001',
    voUserId: '20001',
    voUserName: '测试用户',
    voType: 1,
    voContent: content,
    voIsRecalled: false,
    voCreateTime: createTime,
    voLocalStatus: 'sent',
  };
}

const confirmed: ChannelMessageVo = {
  ...message('900719925474099312342', '测试发送', '2026-11-27T12:00:01Z'),
  voClientRequestId: 'original-send',
  voLocalError: null,
};
const pending: ChannelMessageVo = {
  ...confirmed,
  voId: -1,
  voCreateTime: '2026-11-27T12:00:00Z',
  voLocalStatus: 'sending',
};

test('Hub 先确认时，迟到的发送错误保留持久消息且无需提示失败', (t) => {
  const store = useChatStore.getState();
  t.after(store.reset);
  store.addMessage(pending);
  store.addMessage(confirmed);
  assert.equal(store.markSendFailed(pending, '连接中断'), false);
  const rows = useChatStore.getState().messageMap['70001'];
  assert.deepEqual(rows, [confirmed]);
  assert.equal(buildFailedMessageRetryRequest(rows[0]), null);
});

test('回执未到时保留失败原因，手动重试复用原请求，HTTP 与 Hub 确认合为一条', (t) => {
  const store = useChatStore.getState();
  t.after(store.reset);
  store.addMessage(pending);
  assert.equal(store.markSendFailed(pending, '连接中断'), true);
  const [failed] = useChatStore.getState().messageMap['70001'];
  assert.equal(failed.voLocalStatus, 'failed');
  assert.equal(failed.voLocalError, '连接中断');
  assert.deepEqual(buildFailedMessageRetryRequest(failed), {
    channelId: pending.voChannelId,
    clientRequestId: pending.voClientRequestId,
    type: 1,
    content: pending.voContent,
  });
  store.addMessage({ ...failed, voLocalStatus: 'sending', voLocalError: null });
  store.addMessage(confirmed);
  store.addMessage(confirmed);
  assert.deepEqual(useChatStore.getState().messageMap['70001'], [confirmed]);
});

test('历史补拉确认同一请求时，持久消息替换失败项并保留其他待发送项', (t) => {
  const store = useChatStore.getState();
  t.after(store.reset);
  const other = { ...pending, voId: -2, voClientRequestId: 'other-send', voContent: '另一条消息' };
  store.addMessage({ ...pending, voLocalStatus: 'failed', voLocalError: '连接中断' });
  store.addMessage(other);
  store.setChannelMessages(confirmed.voChannelId, [confirmed]);
  assert.deepEqual(useChatStore.getState().messageMap['70001'], [other, confirmed]);
});

test('失败提示出现后才收到回执时，无需手动重试即可清除错误', (t) => {
  const store = useChatStore.getState();
  t.after(store.reset);
  store.addMessage(pending);
  assert.equal(store.markSendFailed(pending, '连接中断'), true);
  store.addMessage(confirmed);
  const rows = useChatStore.getState().messageMap['70001'];
  assert.deepEqual(rows, [confirmed]);
  assert.equal(buildFailedMessageRetryRequest(rows[0]), null);
});

test('其他发送者的回执不能确认本地请求，迟到错误也不能重新插入已移除的消息', (t) => {
  const store = useChatStore.getState();
  t.after(store.reset);
  const fromAnotherUser = { ...confirmed, voUserId: '20002' };
  store.addMessage(pending);
  store.addMessage(fromAnotherUser);
  assert.equal(store.markSendFailed(pending, '连接中断'), true);
  store.removeMessage(pending.voChannelId, pending.voId);
  assert.equal(store.markSendFailed(pending, '连接中断'), false);
  assert.deepEqual(useChatStore.getState().messageMap['70001'], [fromAnotherUser]);
});

test('权威持久消息同步更新频道最后消息且乱序广播不回退预览', () => {
  const older = message('900719925474099312341', '旧消息', '2026-07-19T10:00:00Z');
  const newer = message('900719925474099312342', '新消息', '2026-07-19T10:01:00Z');
  const withNewer = mergeChannelLastMessage(channel(older), newer);
  assert.equal(withNewer.voLastMessage?.voContent, '新消息');
  assert.equal(mergeChannelLastMessage(withNewer, older).voLastMessage?.voContent, '新消息');

  const updated = message(newer.voId, '服务端更新', newer.voCreateTime);
  assert.equal(mergeChannelLastMessage(withNewer, updated).voLastMessage?.voContent, '服务端更新');

  const unrelated = { ...newer, voChannelId: '70002' };
  assert.equal(mergeChannelLastMessage(withNewer, unrelated), withNewer);
});

test('Store 用持久消息更新频道预览且已读更新不重写预览', () => {
  const source = readFileSync(resolve(import.meta.dirname, '../src/stores/chatStore.ts'), 'utf8');
  assert.match(source, /channels: persistedMessage\s+\? state\.channels\.map\(\(channel\) => mergeChannelLastMessage/);
  const updateUnreadBody = source.match(/updateUnread: \(payload:[\s\S]*?\n\s{2}\},\n\n\s{2}setTypingUser:/)?.[0] ?? '';
  assert.match(updateUnreadBody, /voUnreadCount:/);
  assert.doesNotMatch(updateUnreadBody, /voLastMessage:/);
});
