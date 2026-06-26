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
  assert.deepEqual(parseMeRoute('/me/content', '?tab=comments&page=3'), {
    kind: 'content',
    tab: 'comments',
    page: 3,
  });
  assert.deepEqual(parseMeRoute('/me/content', '?tab=unknown&page=0'), {
    kind: 'content',
    tab: 'posts',
    page: 1,
  });
  assert.deepEqual(parseMeRoute('/me/history', '?page=2'), {
    kind: 'history',
    page: 2,
  });
  assert.deepEqual(parseMeRoute('/me/attachments', '?businessType=Post&keyword=avatar&page=4'), {
    kind: 'attachments',
    businessType: 'Post',
    keyword: 'avatar',
    page: 4,
  });
  assert.deepEqual(parseMeRoute('/me/attachments', '?businessType=Unknown&page=0'), {
    kind: 'attachments',
    businessType: 'All',
    keyword: '',
    page: 1,
  });
  assert.deepEqual(parseMeRoute('/me/experience', '?page=5'), {
    kind: 'experience',
    page: 5,
  });
});

test('parseMeRoute 应拒绝未知我的状态子路径', () => {
  assert.equal(parseMeRoute('/me/profile'), null);
  assert.equal(parseMeRoute('/me/assets/history'), null);
  assert.equal(parseMeRoute('/me/content/posts'), null);
  assert.equal(parseMeRoute('/discover'), null);
});

test('buildMePath 应稳定回写我的状态和资产正式 Web 路径', () => {
  assert.equal(buildMePath(), '/me');
  assert.equal(buildMePath(createDefaultMeRoute()), '/me');
  assert.equal(buildMePath({ kind: 'assets' }), '/me/assets');
  assert.equal(buildMePath({ kind: 'assets-transactions' }), '/me/assets/transactions');
  assert.equal(buildMePath({ kind: 'content', tab: 'posts', page: 1 }), '/me/content');
  assert.equal(buildMePath({ kind: 'content', tab: 'quick-replies', page: 2 }), '/me/content?tab=quick-replies&page=2');
  assert.equal(buildMePath({ kind: 'history', page: 1 }), '/me/history');
  assert.equal(buildMePath({ kind: 'history', page: 3 }), '/me/history?page=3');
  assert.equal(
    buildMePath({ kind: 'attachments', businessType: 'Post', keyword: 'cover', page: 2 }),
    '/me/attachments?businessType=Post&keyword=cover&page=2',
  );
  assert.equal(buildMePath({ kind: 'experience', page: 1 }), '/me/experience');
  assert.equal(buildMePath({ kind: 'experience', page: 6 }), '/me/experience?page=6');
});

test('isMePathname 应识别我的状态和资产正式 Web 路径', () => {
  assert.equal(isMePathname('/me'), true);
  assert.equal(isMePathname('/me/assets'), true);
  assert.equal(isMePathname('/me/assets/transactions'), true);
  assert.equal(isMePathname('/me/content'), true);
  assert.equal(isMePathname('/me/history'), true);
  assert.equal(isMePathname('/me/attachments'), true);
  assert.equal(isMePathname('/me/experience'), true);
  assert.equal(isMePathname('/me/assets/history'), false);
  assert.equal(isMePathname('/desktop'), false);
});
