import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import test from 'node:test';
import type { ChatMessagePinStateVo } from '@radish/http';
import {
  compareChatPinRevisions,
  mergeChatPinState,
} from '../src/utils/chatMessagePins.ts';

const clientRoot = resolve(import.meta.dirname, '..');

function state(revision: string, messageId: string): ChatMessagePinStateVo {
  return {
    voChannelId: '70001',
    voRevision: revision,
    voItems: messageId ? [{
      voId: `91${messageId}`,
      voMessageId: messageId,
      voMessage: {
        voId: messageId,
        voChannelId: '70001',
        voUserId: '20001',
        voUserName: 'Tester',
        voType: 1,
        voContent: `message-${messageId}`,
        voIsRecalled: false,
        voCreateTime: '2026-07-19T10:00:00Z',
      },
      voPinnedByUserId: '20002',
      voPinnedByName: 'Moderator',
      voPinnedAt: '2026-07-19T10:01:00Z',
    }] : [],
  };
}

test('消息置顶 revision 应按 LongId 字符串比较', () => {
  assert.equal(compareChatPinRevisions('900719925474099312342', '900719925474099312341'), 1);
  assert.equal(compareChatPinRevisions('00018', '18'), 0);
  assert.equal(compareChatPinRevisions('9', '10'), -1);
});

test('Hub 置顶快照只接受更高 revision', () => {
  const current = state('18', '90001');
  const newer = state('19', '90002');
  assert.equal(mergeChatPinState(current, newer, 'broadcast'), newer);
  assert.equal(mergeChatPinState(newer, state('18', '90001'), 'broadcast'), newer);
  assert.equal(mergeChatPinState(newer, state('19', '90003'), 'broadcast'), newer);
});

test('HTTP 权威快照可在相同 revision 下修正初始状态', () => {
  const current = state('18', '90001');
  const authoritative = state('18', '90002');
  assert.equal(mergeChatPinState(current, authoritative, 'authoritative'), authoritative);
});

test('正式消息置顶应接入专属 API、Hub、Store 与定位入口', () => {
  const apiSource = readFileSync(resolve(clientRoot, 'src/api/chat.ts'), 'utf8');
  const hubSource = readFileSync(resolve(clientRoot, 'src/services/chatHub.ts'), 'utf8');
  const storeSource = readFileSync(resolve(clientRoot, 'src/stores/chatStore.ts'), 'utf8');
  const appSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatApp.tsx'), 'utf8');
  const listSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatMessageList.tsx'), 'utf8');
  const panelSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatPinnedMessages.tsx'), 'utf8');
  const hookSource = readFileSync(resolve(clientRoot, 'src/apps/chat/useChatMessagePins.ts'), 'utf8');

  assert.match(apiSource, /ChannelMessagePin\/GetState/);
  assert.match(apiSource, /ChannelMessagePin\/Set/);
  assert.match(hubSource, /connection\.on\('MessagePinsChanged'/);
  assert.match(storeSource, /pinStateMap: Record<string, ChatMessagePinStateVo>/);
  assert.match(storeSource, /mergeChatPinState[\s\S]*'broadcast'/);
  assert.match(storeSource, /pinStateMap: \{\}/);
  assert.match(appSource, /navigateToMessage/);
  assert.match(listSource, /canPinMessages[\s\S]*chat\.pin\.unpin[\s\S]*chat\.pin\.pin/);
  assert.match(panelSource, /id="chat-pinned-message-panel"/);
  assert.match(panelSource, /onNavigate\(messageId\)/);
  assert.match(panelSource, /onClick=\{\(\) => navigate\(item\.voMessageId\)\}/);
  assert.match(hookSource, /refreshAfterTargetUnavailable[\s\S]*setRefreshRevision/);
});
