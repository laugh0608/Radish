import assert from 'node:assert/strict';
import test from 'node:test';
import {
  clearRememberedClientBackTo,
  getRememberedClientBackTo,
  normalizeClientBackTo,
  rememberClientBackTo,
  resolveClientBackLabelKey,
} from '../src/utils/clientNavigation.ts';

function createMemoryStorage() {
  const values = new Map<string, string>();
  return {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => values.set(key, value),
    removeItem: (key: string) => values.delete(key),
  };
}

test('normalizeClientBackTo 仅接受 client 产品路由', () => {
  assert.equal(normalizeClientBackTo('/forum/post/2042219067430928384?replyId=2#reply'), '/forum/post/2042219067430928384?replyId=2#reply');
  assert.equal(normalizeClientBackTo('/messages?channelId=2042219067430928384'), '/messages?channelId=2042219067430928384');
  assert.equal(normalizeClientBackTo('/console/users'), undefined);
  assert.equal(normalizeClientBackTo('/api/users'), undefined);
  assert.equal(normalizeClientBackTo('/forumevil'), undefined);
  assert.equal(normalizeClientBackTo('//example.com/forum'), undefined);
  assert.equal(normalizeClientBackTo('https://example.com/forum'), undefined);
  assert.equal(normalizeClientBackTo('/forum?access_token=secret'), undefined);
  assert.equal(normalizeClientBackTo('/forum?CODE=secret'), undefined);
});

test('backTo 可跨 Console 认证往返保存在当前标签页会话中', () => {
  const storage = createMemoryStorage();
  const source = '/forum/post/2042219067430928384?replyId=2042219067430928385';

  assert.equal(rememberClientBackTo(`?backTo=${encodeURIComponent(source)}`, storage), source);
  assert.equal(rememberClientBackTo('?code=oidc-code&state=oidc-state', storage), source);
  assert.equal(getRememberedClientBackTo(storage), source);

  clearRememberedClientBackTo(storage);
  assert.equal(getRememberedClientBackTo(storage), undefined);
});

test('显式非法 backTo 会清除旧来源，避免继承过期跳转', () => {
  const storage = createMemoryStorage();
  rememberClientBackTo('?backTo=%2Fshop', storage);

  assert.equal(rememberClientBackTo('?backTo=https%3A%2F%2Fexample.com', storage), undefined);
  assert.equal(getRememberedClientBackTo(storage), undefined);
});

test('sessionStorage 不可用时不应阻断 Console 导航', () => {
  const unavailableStorage = {
    getItem: () => { throw new Error('storage disabled'); },
    setItem: () => { throw new Error('storage disabled'); },
    removeItem: () => { throw new Error('storage disabled'); },
  };

  assert.equal(rememberClientBackTo('?backTo=%2Fforum', unavailableStorage), '/forum');
  assert.equal(getRememberedClientBackTo(unavailableStorage), undefined);
  assert.doesNotThrow(() => clearRememberedClientBackTo(unavailableStorage));
});

test('返回入口根据产品来源提供稳定文案', () => {
  assert.equal(resolveClientBackLabelKey('/forum/post/1'), 'console.clientBack.forum');
  assert.equal(resolveClientBackLabelKey('/shop/orders'), 'console.clientBack.shop');
  assert.equal(resolveClientBackLabelKey('/messages?channelId=1'), 'console.clientBack.messages');
  assert.equal(resolveClientBackLabelKey('/workbench'), 'console.clientBack.workbench');
  assert.equal(resolveClientBackLabelKey('/me'), 'console.clientBack.community');
});
