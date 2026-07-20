import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildAvatarText,
  findMentionContext,
  formatChatTime,
  getEntityKey,
  getReplyTargetMessageId,
  normalizeMentionText,
  resolveMediaUrl,
  toNumericId,
} from '../src/apps/chat/chatApp.helpers.ts';
import type { ChannelMessageVo } from '../src/types/chat.ts';

function createMessage(overrides: Partial<ChannelMessageVo> = {}): ChannelMessageVo {
  return {
    voId: '2042219067430928384',
    voChannelId: '2042219067430928385',
    voUserId: '2042219067430928386',
    voUserName: 'alice',
    voType: 1,
    voContent: 'hello',
    voIsRecalled: false,
    voCreateTime: '2026-05-22T08:00:00Z',
    ...overrides,
  };
}

test('findMentionContext 应只在合法 @ 触发位置返回上下文', () => {
  assert.deepEqual(findMentionContext('hello @ali', 'hello @ali'.length), {
    start: 6,
    end: 10,
    keyword: 'ali',
  });
  assert.equal(findMentionContext('email@test', 'email@test'.length), null);
  assert.equal(findMentionContext('hello @ali ce', 'hello @ali ce'.length), null);
});

test('formatChatTime 应按当前语言格式化时间', () => {
  const value = '2026-05-22T08:05:00Z';
  assert.notEqual(formatChatTime(value, 'zh-CN'), formatChatTime(value, 'en-US'));
  assert.equal(formatChatTime('invalid', 'en-US'), '--:--');
});

test('normalizeMentionText 应把存储格式提炼为可读文本', () => {
  assert.equal(
    normalizeMentionText('hi @[Alice](2042219067430928384), see @[Bob](2042219067430928385)'),
    'hi @Alice, see @Bob'
  );
  assert.equal(normalizeMentionText(null), '');
});

test('getReplyTargetMessageId 只允许已发送且未撤回的持久消息', () => {
  assert.equal(getReplyTargetMessageId(createMessage()), '2042219067430928384');
  assert.equal(getReplyTargetMessageId(createMessage({ voId: -1 })), null);
  assert.equal(getReplyTargetMessageId(createMessage({ voLocalStatus: 'failed' })), null);
  assert.equal(getReplyTargetMessageId(createMessage({ voIsRecalled: true })), null);
});

test('ID 与媒体 helper 应保持字符串化契约', () => {
  assert.equal(getEntityKey(' 2042219067430928384 '), '2042219067430928384');
  assert.equal(toNumericId('2042219067430928384'), Number('2042219067430928384'));
  assert.equal(toNumericId('invalid'), 0);
  assert.equal(resolveMediaUrl('http://localhost:5100', '/uploads/a.png'), 'http://localhost:5100/uploads/a.png');
  assert.equal(resolveMediaUrl('http://localhost:5100', 'uploads/a.png'), 'http://localhost:5100/uploads/a.png');
  assert.equal(resolveMediaUrl('http://localhost:5100', 'https://cdn.example/a.png'), 'https://cdn.example/a.png');
  assert.equal(buildAvatarText(' alice '), 'A');
  assert.equal(buildAvatarText(''), '?');
});
