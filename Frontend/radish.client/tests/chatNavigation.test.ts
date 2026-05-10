import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildChatAppParams,
  parseChatNotificationNavigation,
  parseChatWindowParams,
  resolveChatMessageNavigationAction,
} from '../src/utils/chatNavigation.ts';
import type { ChannelMessageVo } from '../src/types/chat.ts';

function createMessage(id: string): ChannelMessageVo {
  return {
    voId: id,
    voChannelId: '100',
    voUserId: '200',
    voUserName: 'tester',
    voType: 1,
    voContent: `message-${id}`,
    voIsRecalled: false,
    voCreateTime: '2026-05-10T00:00:00.000Z',
  };
}

test('parseChatNotificationNavigation 应拒绝已丢精度的 number 型频道 ID', () => {
  const navigation = parseChatNotificationNavigation(JSON.stringify({
    app: 'chat',
    channelId: 2042219067430928384,
    messageId: 2042219067430928385,
  }));

  assert.equal(navigation, null);
});

test('parseChatNotificationNavigation 应接受字符串化的 chat extData', () => {
  const navigation = parseChatNotificationNavigation(JSON.stringify({
    app: 'chat',
    channelId: '2042219067430928384',
    messageId: '2042219067430928385',
  }));

  assert.deepEqual(navigation, {
    channelId: '2042219067430928384',
    messageId: '2042219067430928385',
  });
});

test('parseChatNotificationNavigation 应兼容旧 payload 的安全整数 ID', () => {
  const navigation = parseChatNotificationNavigation(JSON.stringify({
    channelId: 123,
    messageId: 456,
  }));

  assert.deepEqual(navigation, {
    channelId: '123',
    messageId: '456',
  });
});

test('parseChatWindowParams 应保留窗口参数中的大整数 ID 字符串', () => {
  const params = parseChatWindowParams({
    channelId: '2042219067430928384',
    messageId: '2042219067430928385',
    __navigationKey: 'notification:1',
  });

  assert.deepEqual(params, {
    channelId: '2042219067430928384',
    messageId: '2042219067430928385',
    navigationKey: 'notification:1',
  });
});

test('buildChatAppParams 应把合法 ID 统一序列化为字符串参数', () => {
  const params = buildChatAppParams({
    channelId: 123,
    messageId: '456',
  });

  assert.deepEqual(params, {
    channelId: '123',
    messageId: '456',
  });
});

test('resolveChatMessageNavigationAction 应在目标消息已加载时返回 found', () => {
  const action = resolveChatMessageNavigationAction(
    [createMessage('2042219067430928384'), createMessage('2042219067430928385')],
    '2042219067430928385',
    true
  );

  assert.equal(action, 'found');
});

test('resolveChatMessageNavigationAction 应在目标消息比当前最旧消息更早且仍有历史时返回 load-more', () => {
  const action = resolveChatMessageNavigationAction(
    [createMessage('2042219067430928384'), createMessage('2042219067430928385')],
    '2042219067430928383',
    true
  );

  assert.equal(action, 'load-more');
});

test('resolveChatMessageNavigationAction 应在目标消息已落入当前已加载区间但未命中时返回 not-found', () => {
  const action = resolveChatMessageNavigationAction(
    [createMessage('2042219067430928384'), createMessage('2042219067430928386')],
    '2042219067430928385',
    true
  );

  assert.equal(action, 'not-found');
});
