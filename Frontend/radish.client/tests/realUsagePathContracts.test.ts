import assert from 'node:assert/strict';
import test from 'node:test';
import {
  BROWSER_PUBLIC_ENTRY_PATH,
  isPublicContentPathname,
  resolveInitialEntryPath,
} from '../src/bootstrap/entryRoute.ts';
import {
  buildDesktopForumPostReturnPath,
  buildDesktopShopOrderReturnPath,
  buildDesktopShopPrivateViewReturnPath,
  buildDesktopShopProductReturnPath,
  normalizeAuthReturnPath,
} from '../src/services/authReturnPath.ts';
import { parseDesktopExternalEntry } from '../src/utils/desktopEntryNavigation.ts';

const productId = '2042219067430928384';
const orderId = '2042219067430928385';
const postPublicId = 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f';
const commentId = '2042219067430928386';

function splitDesktopPath(returnPath: string): { pathname: string; search: string } {
  const url = new URL(returnPath, 'https://radish.local');
  return {
    pathname: url.pathname,
    search: url.search,
  };
}

test('P3-9 访客根入口应进入纯 Web 公开发现页', () => {
  assert.equal(resolveInitialEntryPath({
    pathname: '/',
    isTauriRuntime: false,
    isCapacitorNativePlatform: false,
  }), BROWSER_PUBLIC_ENTRY_PATH);
  assert.equal(isPublicContentPathname(BROWSER_PUBLIC_ENTRY_PATH), true);
  assert.equal(isPublicContentPathname('/desktop'), false);
});

test('P3-9 公开商品购买入口应保持同一商品上下文并可被登录回流保存', () => {
  const returnPath = buildDesktopShopProductReturnPath(productId, { intent: 'purchase' });
  assert.equal(returnPath, `/desktop?app=shop&productId=${productId}&intent=purchase`);
  assert.equal(normalizeAuthReturnPath(returnPath), returnPath);

  const { pathname, search } = splitDesktopPath(returnPath);
  assert.deepEqual(parseDesktopExternalEntry(pathname, search), {
    appId: 'shop',
    appParams: {
      productId,
      intent: 'purchase',
    },
    requiresAuthenticatedSession: false,
    signature: `shop:product:${productId}:purchase`,
  });
});

test('P3-9 订单与背包私有入口应要求登录并保留字符串 ID 契约', () => {
  const orderReturnPath = buildDesktopShopOrderReturnPath(orderId);
  assert.equal(orderReturnPath, `/desktop?app=shop&orderId=${orderId}`);
  assert.equal(normalizeAuthReturnPath(orderReturnPath), orderReturnPath);

  const orderTarget = splitDesktopPath(orderReturnPath);
  assert.deepEqual(parseDesktopExternalEntry(orderTarget.pathname, orderTarget.search), {
    appId: 'shop',
    appParams: {
      orderId,
    },
    requiresAuthenticatedSession: true,
    signature: `shop:order:${orderId}`,
  });

  const inventoryReturnPath = buildDesktopShopPrivateViewReturnPath('inventory');
  assert.equal(normalizeAuthReturnPath(inventoryReturnPath), inventoryReturnPath);
  const inventoryTarget = splitDesktopPath(inventoryReturnPath);
  assert.deepEqual(parseDesktopExternalEntry(inventoryTarget.pathname, inventoryTarget.search), {
    appId: 'shop',
    appParams: {
      initialView: 'inventory',
    },
    requiresAuthenticatedSession: true,
    signature: 'shop:inventory',
  });
});

test('P3-9 公开论坛参与入口应优先使用 PublicId 并保留评论意图', () => {
  const returnPath = buildDesktopForumPostReturnPath({
    postPublicId,
    commentId,
    intent: 'comment',
  });
  assert.equal(
    returnPath,
    `/desktop?app=forum&postPublicId=${postPublicId}&commentId=${commentId}&intent=comment`,
  );
  assert.equal(normalizeAuthReturnPath(returnPath), returnPath);

  const { pathname, search } = splitDesktopPath(returnPath);
  assert.deepEqual(parseDesktopExternalEntry(pathname, search), {
    appId: 'forum',
    appParams: {
      postPublicId,
      commentId,
      intent: 'comment',
    },
    requiresAuthenticatedSession: false,
    signature: `forum:${postPublicId}:${commentId}:comment`,
  });
});

test('P3-9 登录回流不接受公开路径、外部 URL、协议相对 URL 或反斜杠路径', () => {
  assert.equal(normalizeAuthReturnPath(`/shop/product/${productId}`), null);
  assert.equal(normalizeAuthReturnPath(`/forum/post/${postPublicId}`), null);
  assert.equal(normalizeAuthReturnPath('https://radishx.com/desktop?app=shop'), null);
  assert.equal(normalizeAuthReturnPath('//radishx.com/desktop?app=shop'), null);
  assert.equal(normalizeAuthReturnPath('/desktop\\?app=shop'), null);
});
