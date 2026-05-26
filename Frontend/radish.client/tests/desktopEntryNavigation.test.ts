import assert from 'node:assert/strict';
import test from 'node:test';
import { parseDesktopExternalEntry, stripDesktopExternalEntrySearch } from '../src/utils/desktopEntryNavigation.ts';

test('parseDesktopExternalEntry 应解析 desktop 聊天消息深链参数', () => {
  const target = parseDesktopExternalEntry(
    '/desktop',
    '?app=chat&channelId=2042219067430928384&messageId=2042219067430928385',
  );

  assert.deepEqual(target, {
    appId: 'chat',
    appParams: {
      channelId: '2042219067430928384',
      messageId: '2042219067430928385',
    },
    requiresAuthenticatedSession: true,
    signature: 'chat:2042219067430928384:2042219067430928385',
  });
});

test('parseDesktopExternalEntry 应拒绝非 desktop 路径或非法频道参数', () => {
  assert.equal(parseDesktopExternalEntry('/forum', '?app=chat&channelId=123&messageId=456'), null);
  assert.equal(parseDesktopExternalEntry('/desktop', '?app=chat&channelId=0&messageId=456'), null);
});

test('parseDesktopExternalEntry 应允许仅按频道打开聊天窗口', () => {
  const target = parseDesktopExternalEntry('/desktop/', '?app=chat&channelId=123');

  assert.deepEqual(target, {
    appId: 'chat',
    appParams: {
      channelId: '123',
    },
    requiresAuthenticatedSession: true,
    signature: 'chat:123:none',
  });
});

test('parseDesktopExternalEntry 应解析 desktop 商城商品深链参数', () => {
  const target = parseDesktopExternalEntry(
    '/desktop',
    '?app=shop&productId=2042219067430928384',
  );

  assert.deepEqual(target, {
    appId: 'shop',
    appParams: {
      productId: '2042219067430928384',
    },
    requiresAuthenticatedSession: false,
    signature: 'shop:product:2042219067430928384',
  });
});

test('parseDesktopExternalEntry 应解析 desktop 商城订单深链参数', () => {
  const target = parseDesktopExternalEntry(
    '/desktop',
    '?app=shop&orderId=2042219067430928384',
  );

  assert.deepEqual(target, {
    appId: 'shop',
    appParams: {
      orderId: '2042219067430928384',
    },
    requiresAuthenticatedSession: true,
    signature: 'shop:order:2042219067430928384',
  });
});

test('parseDesktopExternalEntry 应解析 desktop 商城订单列表和背包入口', () => {
  assert.deepEqual(parseDesktopExternalEntry('/desktop', '?app=shop&view=orders'), {
    appId: 'shop',
    appParams: {
      initialView: 'orders',
    },
    requiresAuthenticatedSession: true,
    signature: 'shop:orders',
  });

  assert.deepEqual(parseDesktopExternalEntry('/desktop', '?app=shop&view=inventory'), {
    appId: 'shop',
    appParams: {
      initialView: 'inventory',
    },
    requiresAuthenticatedSession: true,
    signature: 'shop:inventory',
  });
});

test('parseDesktopExternalEntry 应拒绝非法商城参数', () => {
  assert.equal(parseDesktopExternalEntry('/desktop', '?app=shop&productId=0'), null);
  assert.equal(parseDesktopExternalEntry('/desktop', '?app=shop&productId=abc'), null);
  assert.equal(parseDesktopExternalEntry('/desktop', '?app=shop&orderId=0'), null);
  assert.equal(parseDesktopExternalEntry('/desktop', '?app=shop&view=profile'), null);
  assert.equal(parseDesktopExternalEntry('/desktop', '?app=shop'), null);
});

test('stripDesktopExternalEntrySearch 应仅移除已处理的 desktop 跳转参数', () => {
  assert.equal(
    stripDesktopExternalEntrySearch('?app=chat&channelId=123&messageId=456&culture=zh'),
    '?culture=zh',
  );
  assert.equal(stripDesktopExternalEntrySearch('?app=chat&channelId=123'), '');
  assert.equal(
    stripDesktopExternalEntrySearch('?app=shop&productId=2042219067430928384&culture=zh'),
    '?culture=zh',
  );
  assert.equal(
    stripDesktopExternalEntrySearch('?app=shop&orderId=2042219067430928384&view=orders&culture=zh'),
    '?culture=zh',
  );
});
