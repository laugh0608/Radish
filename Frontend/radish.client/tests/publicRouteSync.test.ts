import assert from 'node:assert/strict';
import test from 'node:test';
import { shouldReplacePublicRouteSync } from '../src/public/usePublicReplaceRouteSync.ts';

test('shouldReplacePublicRouteSync 在 route key 未变化时返回 false', () => {
  assert.equal(shouldReplacePublicRouteSync('search::all::1', 'search::all::1'), false);
});

test('shouldReplacePublicRouteSync 在 route key 变化时返回 true', () => {
  assert.equal(shouldReplacePublicRouteSync('search::all::1', 'search:radish:all:1'), true);
});
