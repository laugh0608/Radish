import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildMePath,
  createDefaultMeRoute,
  isMePathname,
  parseMeRoute,
} from '../src/me/meRouteState.ts';

test('parseMeRoute 应解析我的状态和资产正式 Web 路由', () => {
  assert.deepEqual(parseMeRoute('/me'), {
    kind: 'dashboard',
  });
  assert.deepEqual(parseMeRoute('/me/'), {
    kind: 'dashboard',
  });
  assert.deepEqual(parseMeRoute('/me/assets'), {
    kind: 'assets',
  });
  assert.deepEqual(parseMeRoute('/me/assets/transactions'), {
    kind: 'assets-transactions',
  });
});

test('parseMeRoute 应拒绝未知我的状态子路径', () => {
  assert.equal(parseMeRoute('/me/profile'), null);
  assert.equal(parseMeRoute('/me/assets/history'), null);
  assert.equal(parseMeRoute('/discover'), null);
});

test('buildMePath 应稳定回写我的状态和资产正式 Web 路径', () => {
  assert.equal(buildMePath(), '/me');
  assert.equal(buildMePath(createDefaultMeRoute()), '/me');
  assert.equal(buildMePath({ kind: 'assets' }), '/me/assets');
  assert.equal(buildMePath({ kind: 'assets-transactions' }), '/me/assets/transactions');
});

test('isMePathname 应识别我的状态和资产正式 Web 路径', () => {
  assert.equal(isMePathname('/me'), true);
  assert.equal(isMePathname('/me/assets'), true);
  assert.equal(isMePathname('/me/assets/transactions'), true);
  assert.equal(isMePathname('/me/assets/history'), false);
  assert.equal(isMePathname('/desktop'), false);
});
