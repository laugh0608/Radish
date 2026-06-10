import assert from 'node:assert/strict';
import test from 'node:test';
import {
  BROWSER_PUBLIC_ENTRY_PATH,
  CAPACITOR_PUBLIC_ENTRY_PATH,
  OIDC_CALLBACK_PATH,
  isCirclePathname,
  isPublicContentPathname,
  resolveInitialEntryPath,
} from '../src/bootstrap/entryRoute.ts';
import { TAURI_DESKTOP_ENTRY_PATH } from '../src/platform/tauriBridge.ts';

test('resolveInitialEntryPath 应把普通浏览器根路径切到公开发现页', () => {
  assert.equal(resolveInitialEntryPath({
    pathname: '/',
    isTauriRuntime: false,
    isCapacitorNativePlatform: false,
  }), BROWSER_PUBLIC_ENTRY_PATH);
});

test('resolveInitialEntryPath 应保留 Tauri 根路径进入桌面工作台', () => {
  assert.equal(resolveInitialEntryPath({
    pathname: '/',
    isTauriRuntime: true,
    isCapacitorNativePlatform: false,
  }), TAURI_DESKTOP_ENTRY_PATH);
});

test('resolveInitialEntryPath 应保留 Capacitor 根路径进入公开文档', () => {
  assert.equal(resolveInitialEntryPath({
    pathname: '/',
    isTauriRuntime: false,
    isCapacitorNativePlatform: true,
  }), CAPACITOR_PUBLIC_ENTRY_PATH);
});

test('resolveInitialEntryPath 不改写非根路径和 OIDC 回调', () => {
  assert.equal(resolveInitialEntryPath({
    pathname: OIDC_CALLBACK_PATH,
    isTauriRuntime: false,
    isCapacitorNativePlatform: false,
  }), null);
  assert.equal(resolveInitialEntryPath({
    pathname: '/desktop',
    isTauriRuntime: false,
    isCapacitorNativePlatform: false,
  }), null);
});

test('isPublicContentPathname 应识别公开内容路由', () => {
  assert.equal(isPublicContentPathname('/discover'), true);
  assert.equal(isPublicContentPathname('/forum/post/2042219067430928384'), true);
  assert.equal(isPublicContentPathname('/docs/Guide'), true);
  assert.equal(isPublicContentPathname('/u/2042219067430928384'), true);
  assert.equal(isPublicContentPathname('/u/usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f'), true);
  assert.equal(isPublicContentPathname('/leaderboard/post-count'), true);
  assert.equal(isPublicContentPathname('/shop/product/2042219067430928384'), true);
  assert.equal(isPublicContentPathname('/circle'), false);
  assert.equal(isPublicContentPathname('/desktop'), false);
  assert.equal(isPublicContentPathname(OIDC_CALLBACK_PATH), false);
});

test('isCirclePathname 应单独识别登录态圈子入口', () => {
  assert.equal(isCirclePathname('/circle'), true);
  assert.equal(isCirclePathname('/circle/'), true);
  assert.equal(isCirclePathname('/discover'), false);
  assert.equal(isCirclePathname('/forum'), false);
});
