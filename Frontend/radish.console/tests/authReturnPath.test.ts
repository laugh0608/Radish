import assert from 'node:assert/strict';
import test from 'node:test';
import {
  consumeConsoleAuthReturnPath,
  normalizeConsoleAuthReturnPath,
  rememberConsoleAuthReturnPath,
} from '../src/services/authReturnPath.ts';

function createMemoryStorage() {
  const values = new Map<string, string>();
  return {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => values.set(key, value),
    removeItem: (key: string) => values.delete(key),
  };
}

test('Console 登录来源只接受当前 base 内的业务路由', () => {
  assert.equal(normalizeConsoleAuthReturnPath('/orders?page=2#recent'), '/orders?page=2#recent');
  assert.equal(normalizeConsoleAuthReturnPath('/users/2042219067430928384'), '/users/2042219067430928384');
  assert.equal(normalizeConsoleAuthReturnPath('/console/orders?page=2'), '/orders?page=2');
  assert.equal(normalizeConsoleAuthReturnPath('/login'), undefined);
  assert.equal(normalizeConsoleAuthReturnPath('/Console/Login'), undefined);
  assert.equal(normalizeConsoleAuthReturnPath('/callback?code=secret'), undefined);
  assert.equal(normalizeConsoleAuthReturnPath('//example.com/orders'), undefined);
  assert.equal(normalizeConsoleAuthReturnPath('https://example.com/orders'), undefined);
  assert.equal(normalizeConsoleAuthReturnPath('/orders?access_token=secret'), undefined);
  assert.equal(normalizeConsoleAuthReturnPath('/orders?STATE=secret'), undefined);
  assert.equal(normalizeConsoleAuthReturnPath('/orders#access_token=secret'), undefined);
});

test('Console 登录来源按当前标签页保存并只消费一次', () => {
  const storage = createMemoryStorage();
  assert.equal(rememberConsoleAuthReturnPath({
    pathname: '/orders',
    search: '?status=Pending&page=2',
  }, storage), true);

  assert.equal(consumeConsoleAuthReturnPath(storage), '/orders?status=Pending&page=2');
  assert.equal(consumeConsoleAuthReturnPath(storage), '/');
});

test('Console 登录来源存储不可用时安全回落首页', () => {
  const unavailableStorage = {
    getItem: () => { throw new Error('storage disabled'); },
    setItem: () => { throw new Error('storage disabled'); },
    removeItem: () => { throw new Error('storage disabled'); },
  };

  assert.equal(rememberConsoleAuthReturnPath({ pathname: '/orders' }, unavailableStorage), false);
  assert.equal(consumeConsoleAuthReturnPath(unavailableStorage), '/');
});
