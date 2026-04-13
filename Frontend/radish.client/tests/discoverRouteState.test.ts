import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildPublicDiscoverPath,
  createDefaultPublicDiscoverRoute,
  isPublicDiscoverPathname,
  parsePublicDiscoverRoute,
} from '../src/public/discoverRouteState.ts';

test('parsePublicDiscoverRoute 应识别公开社区分发页入口', () => {
  const route = parsePublicDiscoverRoute('/discover');

  assert.deepEqual(route, {
    kind: 'home'
  });
});

test('parsePublicDiscoverRoute 应兼容尾随斜杠', () => {
  const route = parsePublicDiscoverRoute('/discover/');

  assert.deepEqual(route, {
    kind: 'home'
  });
});

test('parsePublicDiscoverRoute 对非 discover 路径返回 null', () => {
  assert.equal(parsePublicDiscoverRoute('/discover/forum'), null);
});

test('isPublicDiscoverPathname 仅识别公开社区分发页根路径', () => {
  assert.equal(isPublicDiscoverPathname('/discover'), true);
  assert.equal(isPublicDiscoverPathname('/discover/'), true);
  assert.equal(isPublicDiscoverPathname('/discover/forum'), false);
});

test('buildPublicDiscoverPath 应稳定回写公开社区分发页路径', () => {
  const path = buildPublicDiscoverPath(createDefaultPublicDiscoverRoute());

  assert.equal(path, '/discover');
});
