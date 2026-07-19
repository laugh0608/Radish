import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import test from 'node:test';
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
