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
    signature: 'chat:123:none',
  });
});

test('stripDesktopExternalEntrySearch 应仅移除已处理的 desktop 跳转参数', () => {
  assert.equal(
    stripDesktopExternalEntrySearch('?app=chat&channelId=123&messageId=456&culture=zh'),
    '?culture=zh',
  );
  assert.equal(stripDesktopExternalEntrySearch('?app=chat&channelId=123'), '');
});
