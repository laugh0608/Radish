import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildAvatarText,
  buildFailedMessageRetryRequest,
  findMentionContext,
  formatChatTime,
  getEntityKey,
  getReplyTargetMessageId,
  normalizeMentionText,
  resolveMediaUrl,
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
  assert.equal(resolveMediaUrl('http://localhost:5100', '/uploads/a.png'), 'http://localhost:5100/uploads/a.png');
  assert.equal(resolveMediaUrl('http://localhost:5100', 'uploads/a.png'), 'http://localhost:5100/uploads/a.png');
  assert.equal(resolveMediaUrl('http://localhost:5100', 'https://cdn.example/a.png'), 'https://cdn.example/a.png');
  assert.equal(buildAvatarText(' alice '), 'A');
  assert.equal(buildAvatarText(''), '?');
});

test('失败文字消息重试应复用原幂等键与完整发送指纹', () => {
  const request = buildFailedMessageRetryRequest(createMessage({
    voId: -1,
    voClientRequestId: 'chat-original-text-request',
    voLocalStatus: 'failed',
    voContent: 'hello again',
    voReplyToId: '2042219067430928390',
  }));

  assert.deepEqual(request, {
    clientRequestId: 'chat-original-text-request',
    channelId: '2042219067430928385',
    type: 1,
    content: 'hello again',
    replyToId: '2042219067430928390',
  });
});

test('失败图片消息重试应复用原附件与原幂等键', () => {
  const request = buildFailedMessageRetryRequest(createMessage({
    voId: -2,
    voClientRequestId: 'chat-original-image-request',
    voLocalStatus: 'failed',
    voType: 2,
    voContent: 'image caption',
    voAttachmentId: '2042219067430928391',
  }));

  assert.deepEqual(request, {
    clientRequestId: 'chat-original-image-request',
    channelId: '2042219067430928385',
    type: 2,
    content: 'image caption',
    attachmentId: '2042219067430928391',
  });
});

test('Pending Direct 首消息重试应保持原纯文字请求且不得生成替代键', () => {
  const request = buildFailedMessageRetryRequest(createMessage({
    voId: -3,
    voClientRequestId: 'chat-original-direct-request',
    voLocalStatus: 'failed',
    voContent: '你好，可以认识一下吗？',
    voReplyToId: null,
    voAttachmentId: null,
  }));

  assert.deepEqual(request, {
    clientRequestId: 'chat-original-direct-request',
    channelId: '2042219067430928385',
    type: 1,
    content: '你好，可以认识一下吗？',
  });
});

test('失败消息缺少原幂等键或完整附件指纹时应拒绝静默重试', () => {
  assert.equal(buildFailedMessageRetryRequest(createMessage({
    voId: -4,
    voClientRequestId: null,
    voLocalStatus: 'failed',
  })), null);
  assert.equal(buildFailedMessageRetryRequest(createMessage({
    voId: -5,
    voClientRequestId: 'chat-incomplete-image-request',
    voLocalStatus: 'failed',
    voType: 2,
    voAttachmentId: null,
  })), null);
});
