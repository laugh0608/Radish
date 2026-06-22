import assert from 'node:assert/strict';
import test from 'node:test';
import {
  BROWSER_PUBLIC_ENTRY_PATH,
  CAPACITOR_PUBLIC_ENTRY_PATH,
  OIDC_CALLBACK_PATH,
  isCirclePathname,
  isDocsAuthorPathname,
  isMePathname,
  isMessagesPathname,
  isNotificationsPathname,
  isPetPathname,
  isPublicContentPathname,
  isShopPathname,
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
  assert.equal(isPublicContentPathname('/docs/mine'), false);
  assert.equal(isPublicContentPathname('/docs/compose'), false);
  assert.equal(isPublicContentPathname('/docs/edit/2042219067430928384'), false);
  assert.equal(isPublicContentPathname('/docs/revisions/2042219067430928384'), false);
  assert.equal(isPublicContentPathname('/u/2042219067430928384'), true);
  assert.equal(isPublicContentPathname('/u/usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f'), true);
  assert.equal(isPublicContentPathname('/leaderboard/post-count'), true);
  assert.equal(isPublicContentPathname('/shop/product/2042219067430928384'), true);
  assert.equal(isPublicContentPathname('/shop/orders'), false);
  assert.equal(isPublicContentPathname('/shop/order/2042219067430928385'), false);
  assert.equal(isPublicContentPathname('/shop/inventory'), false);
  assert.equal(isPublicContentPathname('/circle'), false);
  assert.equal(isPublicContentPathname('/me'), false);
  assert.equal(isPublicContentPathname('/messages'), false);
  assert.equal(isPublicContentPathname('/notifications'), false);
  assert.equal(isPublicContentPathname('/pet'), false);
  assert.equal(isPublicContentPathname('/desktop'), false);
  assert.equal(isPublicContentPathname(OIDC_CALLBACK_PATH), false);
});

test('isCirclePathname 应单独识别登录态圈子入口', () => {
  assert.equal(isCirclePathname('/circle'), true);
  assert.equal(isCirclePathname('/circle/'), true);
  assert.equal(isCirclePathname('/discover'), false);
  assert.equal(isCirclePathname('/forum'), false);
});

test('isNotificationsPathname 应单独识别登录态通知复访入口', () => {
  assert.equal(isNotificationsPathname('/notifications'), true);
  assert.equal(isNotificationsPathname('/notifications/'), true);
  assert.equal(isNotificationsPathname('/discover'), false);
  assert.equal(isNotificationsPathname('/forum'), false);
});

test('isMePathname 应单独识别登录态我的状态入口', () => {
  assert.equal(isMePathname('/me'), true);
  assert.equal(isMePathname('/me/'), true);
  assert.equal(isMePathname('/me/assets'), true);
  assert.equal(isMePathname('/me/assets/transactions'), true);
  assert.equal(isMePathname('/me/content'), true);
  assert.equal(isMePathname('/me/history'), true);
  assert.equal(isMePathname('/me/attachments'), true);
  assert.equal(isMePathname('/me/experience'), true);
  assert.equal(isMePathname('/me/assets/history'), false);
  assert.equal(isMePathname('/discover'), false);
  assert.equal(isMePathname('/u/usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f'), false);
});

test('isMessagesPathname 应单独识别登录态消息复访入口', () => {
  assert.equal(isMessagesPathname('/messages'), true);
  assert.equal(isMessagesPathname('/messages/'), true);
  assert.equal(isMessagesPathname('/notifications'), false);
  assert.equal(isMessagesPathname('/desktop'), false);
});

test('isPetPathname 应单独识别登录态电子宠物入口', () => {
  assert.equal(isPetPathname('/pet'), true);
  assert.equal(isPetPathname('/pet/'), true);
  assert.equal(isPetPathname('/discover'), false);
  assert.equal(isPetPathname('/desktop'), false);
});

test('isShopPathname 应单独识别登录态商城交易入口', () => {
  assert.equal(isShopPathname('/shop/orders'), true);
  assert.equal(isShopPathname('/shop/orders/'), true);
  assert.equal(isShopPathname('/shop/order/2042219067430928385'), true);
  assert.equal(isShopPathname('/shop/inventory'), true);
  assert.equal(isShopPathname('/shop'), false);
  assert.equal(isShopPathname('/shop/product/2042219067430928384'), false);
  assert.equal(isShopPathname('/desktop'), false);
});

test('isDocsAuthorPathname 应单独识别文档作者正式 Web 入口', () => {
  assert.equal(isDocsAuthorPathname('/docs/mine'), true);
  assert.equal(isDocsAuthorPathname('/docs/mine/'), true);
  assert.equal(isDocsAuthorPathname('/docs/compose'), true);
  assert.equal(isDocsAuthorPathname('/docs/edit/2042219067430928384'), true);
  assert.equal(isDocsAuthorPathname('/docs/revisions/2042219067430928384'), true);
  assert.equal(isDocsAuthorPathname('/docs/Guide'), false);
  assert.equal(isDocsAuthorPathname('/__documents__/mine'), false);
});
