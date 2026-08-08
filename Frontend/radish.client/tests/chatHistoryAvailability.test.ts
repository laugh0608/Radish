import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import test from 'node:test';
import {
  isChatChannelUnavailableError,
  retainLocalChatMessages,
} from '../src/utils/chatHistoryAvailability.ts';
import type { ChannelMessageVo } from '../src/types/chat.ts';

const channelId = '2042219067430928385';

function createMessage(overrides: Partial<ChannelMessageVo>): ChannelMessageVo {
  return {
    voId: '2042219067430928386',
    voChannelId: channelId,
    voUserId: '2042219067430928387',
    voUserName: 'reader',
    voType: 1,
    voContent: 'cached server content',
    voIsRecalled: false,
    voCreateTime: '2026-08-08T08:00:00Z',
    voLocalStatus: 'sent',
    ...overrides,
  };
}

test('只有权威 ChannelUnavailable 合同才触发历史失效清理', () => {
  assert.equal(isChatChannelUnavailableError({ code: 'Chat.ChannelUnavailable' }), true);
  assert.equal(isChatChannelUnavailableError({ messageKey: 'error.chat.channel_unavailable' }), true);
  assert.equal(isChatChannelUnavailableError({ code: 'Chat.SearchCursorInvalid' }), false);
  assert.equal(isChatChannelUnavailableError(new Error('network disconnected')), false);
});

test('频道失效时只保留本地失败项，不继续回显服务端正文', () => {
  const remaining = retainLocalChatMessages([
    createMessage({}),
    createMessage({
      voId: -1,
      voClientRequestId: 'chat-local-failed-request',
      voContent: 'local failed draft',
      voLocalStatus: 'failed',
      voLocalError: 'timeout',
    }),
  ]);

  assert.equal(remaining.length, 1);
  assert.equal(remaining[0]?.voId, -1);
  assert.equal(remaining[0]?.voContent, 'local failed draft');
});

test('历史或目标不可用时页面应先隐藏旧消息、Pin 与成员上下文', () => {
  const clientRoot = resolve(import.meta.dirname, '..');
  const chatSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatApp.tsx'), 'utf8');
  const listSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatMessageList.tsx'), 'utf8');
  const unavailableGuardIndex = listSource.indexOf('if (historyUnavailable || messageTargetUnavailable)');
  const pinRegionIndex = listSource.indexOf('<div className={styles.pinRegion}>');

  assert.ok(unavailableGuardIndex >= 0 && pinRegionIndex > unavailableGuardIndex);
  assert.match(listSource, /if \(navigatingToMessage\)[\s\S]*chat\.loadingHistory/);
  assert.match(chatSource, /setLoadingHistory\(true\);\s*clearServerMessages\(channelId\);\s*try \{/);
  assert.match(chatSource, /replyTarget=\{conversationContextUnavailable \? null : replyTarget\}/);
  assert.match(chatSource, /typingUsers=\{conversationContextUnavailable \? \[\] : typingUsers\}/);
  assert.match(chatSource, /!conversationContextUnavailable && \(\s*<ChatMemberPanel/);
  assert.match(chatSource, /useChatHistoryAvailability\(activeChannelId\)/);
  const availabilityHookSource = readFileSync(
    resolve(clientRoot, 'src/apps/chat/useChatHistoryAvailability.ts'),
    'utf8'
  );
  assert.match(availabilityHookSource, /ReadonlySet<string>/);
  assert.match(availabilityHookSource, /unavailableChannelKeys\.has\(activeChannelKey\)/);
});
