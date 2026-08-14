import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  buildUserDetailPath,
  parseUserListQuery,
  serializeUserListQuery,
} from '../src/pages/Users/userListUrlState.ts';

const readConsoleSource = (relativePath: string) => readFileSync(
  new URL(`../${relativePath}`, import.meta.url),
  'utf8',
);

test('R3-C04-B Users 查询状态应可回访并保留详情来源', () => {
  const query = parseUserListQuery(new URLSearchParams('page=3&pageSize=50&keyword=alice&status=disabled&role=Admin'));
  assert.deepEqual(query, {
    pageIndex: 3,
    pageSize: 50,
    keyword: 'alice',
    status: 'disabled',
    roleName: 'Admin',
  });
  assert.equal(serializeUserListQuery(query).toString(), 'page=3&pageSize=50&keyword=alice&status=disabled&role=Admin');
  assert.equal(
    buildUserDetailPath('9007199254740993', '/users?page=3&role=Admin'),
    '/users/9007199254740993?returnTo=%2Fusers%3Fpage%3D3%26role%3DAdmin',
  );
});

test('R3-C04-B Users 列表应使用权威筛选、查询快照与共享响应式表面', () => {
  const api = readConsoleSource('src/api/userManagement.ts');
  const list = readConsoleSource('src/pages/Users/UserList.tsx');

  assert.match(api, /searchParams\.set\('isEnabled'/);
  assert.match(api, /searchParams\.set\('roleName'/);
  assert.doesNotMatch(api, /searchParams\.set\('status'/);
  assert.match(list, /snapshotQueryKey\.current === queryKey/);
  assert.match(list, /requestSequence\.current !== requestId/);
  assert.match(list, /setReadState\(hasCurrentSnapshot \? 'stale' : 'unavailable'\)/);
  assert.match(list, /<ConsoleResourceList/);
  assert.match(list, /<BottomSheet/);
  assert.match(list, /console-resource-mobile-card/);
  assert.doesNotMatch(list, /Locked/);
});

test('R3-C04-B Users 详情应分离聚合状态并使用服务端分页', () => {
  const detail = readConsoleSource('src/pages/Users/UserDetail.tsx');

  for (const source of ['profile', 'authorization', 'balance', 'experience', 'coins', 'orders', 'operations', 'benefits']) {
    assert.match(detail, new RegExp(`${source}(?:ReadState|:)`));
  }
  assert.match(detail, /pageIndex: coinPage\.pageIndex/);
  assert.match(detail, /pageIndex: orderPage\.pageIndex/);
  assert.match(detail, /pageIndex: operationPage\.pageIndex/);
  assert.match(detail, /basic\.updatedAt/);
  assert.doesNotMatch(detail, /basic\.lastLogin/);
  assert.doesNotMatch(detail, /set(?:Balance|Experience|CoinTransactions|Orders|EntitlementOperations|Benefits)\((?:null|\[\])\);\s*\}\s*finally/);
});
