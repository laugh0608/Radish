import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import type { ChatMessageReactionStateVo } from '@radish/http';
import {
  buildChatReactionOperationId,
  compareChatReactionRevisions,
  mergeChatReactionState,
} from '../src/utils/chatMessageReactions.ts';

const clientRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

function state(
  revision: string,
  count: number,
  selected: boolean,
  emojiValue: string = '👍'
): ChatMessageReactionStateVo {
  return {
    voMessageId: '900719925474099312341',
    voRevision: revision,
    voItems: [{
      voEmojiType: 'unicode',
      voEmojiValue: emojiValue,
      voCount: count,
      voIsReacted: selected,
      voThumbnailUrl: null,
    }],
  };
}

test('消息回应 revision 应按 LongId 字符串比较，不转换为 Number', () => {
  assert.equal(compareChatReactionRevisions('900719925474099312342', '900719925474099312341'), 1);
  assert.equal(compareChatReactionRevisions('00012', '12'), 0);
  assert.equal(compareChatReactionRevisions('9', '10'), -1);
});

test('Hub 聚合快照只接受更高 revision，并保留当前账号选择', () => {
  const current = state('12', 2, true);
  const higherBroadcast = state('13', 3, false);

  const merged = mergeChatReactionState(current, higherBroadcast, 'broadcast');
  assert.equal(merged.voRevision, '13');
  assert.equal(merged.voItems[0]?.voCount, 3);
  assert.equal(merged.voItems[0]?.voIsReacted, true);
  assert.equal(mergeChatReactionState(merged, state('12', 1, false), 'broadcast'), merged);
  assert.equal(mergeChatReactionState(merged, state('13', 4, false), 'broadcast'), merged);
});

test('首次收到他人 Hub 广播时不得继承广播发起者的选择状态', () => {
  const merged = mergeChatReactionState(undefined, state('8', 1, true), 'broadcast');
  assert.equal(merged.voItems[0]?.voIsReacted, false);
});

test('本人 HTTP 回包可在相同 revision 下校正 Hub 竞态后的选择状态', () => {
  const broadcast = mergeChatReactionState(state('11', 2, false), state('12', 3, true), 'broadcast');
  assert.equal(broadcast.voItems[0]?.voIsReacted, false);

  const authoritative = mergeChatReactionState(broadcast, state('12', 3, true), 'authoritative');
  assert.equal(authoritative.voItems[0]?.voIsReacted, true);
});

test('每次回应动作应生成满足服务端长度约束的独立 operation ID', () => {
  const first = buildChatReactionOperationId('900719925474099312341');
  const second = buildChatReactionOperationId('900719925474099312341');
  assert.ok(first.length >= 8 && first.length <= 100);
  assert.ok(second.length >= 8 && second.length <= 100);
  assert.notEqual(first, second);
});

test('正式消息回应应接入专属 API、Hub、能力字段和显式交互入口', () => {
  const apiSource = readFileSync(resolve(clientRoot, 'src/api/chat.ts'), 'utf8');
  const hubSource = readFileSync(resolve(clientRoot, 'src/services/chatHub.ts'), 'utf8');
  const storeSource = readFileSync(resolve(clientRoot, 'src/stores/chatStore.ts'), 'utf8');
  const listSource = readFileSync(resolve(clientRoot, 'src/apps/chat/ChatMessageList.tsx'), 'utf8');
  const hookSource = readFileSync(resolve(clientRoot, 'src/apps/chat/useChatMessageReactions.ts'), 'utf8');

  assert.match(apiSource, /ChannelMessageReaction\/GetStates/);
  assert.match(apiSource, /ChannelMessageReaction\/Set/);
  assert.match(hubSource, /connection\.on\('MessageReactionsChanged'/);
  assert.match(storeSource, /reactionStateMap: Record<string, ChatMessageReactionStateVo>/);
  assert.match(storeSource, /mergeChatReactionState[\s\S]*'broadcast'/);
  assert.match(listSource, /showAddReactionLabel/);
  assert.match(listSource, /readOnly=\{!canReact\}/);
  assert.match(hookSource, /REACTION_READ_BATCH_SIZE = 100/);
  assert.doesNotMatch(hookSource, /localStorage|sessionStorage|BroadcastChannel/);
});
