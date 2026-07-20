import assert from 'node:assert/strict';
import test from 'node:test';
import { matchesRoutePattern } from '../src/router/routePath.ts';

test('Console 路由匹配应识别角色授权与表情详情动态路径', () => {
  assert.equal(matchesRoutePattern('/roles/:roleId/permissions', '/roles/9223372036854775807/permissions'), true);
  assert.equal(matchesRoutePattern('/stickers/:groupId/items', '/stickers/2028085755741470720/items'), true);
  assert.equal(matchesRoutePattern('/roles/:roleId/permissions', '/roles/1'), false);
  assert.equal(matchesRoutePattern('/stickers/:groupId/items', '/roles/1/items'), false);
});

test('Console 静态路由匹配不应将子路径误判为列表页', () => {
  assert.equal(matchesRoutePattern('/categories', '/categories'), true);
  assert.equal(matchesRoutePattern('/categories', '/categories/1'), false);
  assert.equal(matchesRoutePattern('/coins', '/coins'), true);
  assert.equal(matchesRoutePattern('/experience', '/experience'), true);
});
