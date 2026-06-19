import assert from 'node:assert/strict';
import test from 'node:test';
import {
  BROWSER_PUBLIC_ENTRY_PATH,
  isPublicContentPathname,
  resolveInitialEntryPath,
} from '../src/bootstrap/entryRoute.ts';
import {
  buildPublicCanonicalUrl,
  buildPublicRouteHead,
  buildPublicShareUrl,
  publicDefaultOrigin,
} from '../src/public/publicHead.ts';
import type { PublicRouteDescriptor } from '../src/public/publicRouteNavigation.ts';
import {
  buildCircleReturnPath,
  buildDesktopForumPostReturnPath,
  buildDesktopShopOrderReturnPath,
  buildDesktopShopPrivateViewReturnPath,
  buildDesktopShopProductReturnPath,
  buildMeReturnPath,
  buildMessagesReturnPath,
  buildNotificationsReturnPath,
  buildPublicForumPostReturnPath,
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

test('P3-9 公开详情 head 与分享链接应保持发布候选预览契约', () => {
  const routes: Array<{
    route: PublicRouteDescriptor;
    canonicalPath: string;
    sharePath: string;
    type: 'article' | 'product' | 'profile';
  }> = [
    {
      route: {
        app: 'forum',
        route: {
          kind: 'detail',
          postId: '2042219067430920000',
          postPublicId,
          commentId,
        },
      },
      canonicalPath: `/forum/post/${postPublicId}?commentId=${commentId}`,
      sharePath: `/forum/post/${postPublicId}?commentId=${commentId}`,
      type: 'article',
    },
    {
      route: {
        app: 'docs',
        route: {
          kind: 'detail',
          slug: 'release-candidate-guide',
          anchor: 'mobile-path',
        },
      },
      canonicalPath: '/docs/release-candidate-guide#mobile-path',
      sharePath: '/docs/release-candidate-guide#mobile-path',
      type: 'article',
    },
    {
      route: {
        app: 'shop',
        route: {
          kind: 'detail',
          productId,
        },
      },
      canonicalPath: `/shop/product/${productId}`,
      sharePath: `/shop/product/${productId}`,
      type: 'product',
    },
    {
      route: {
        app: 'profile',
        route: {
          kind: 'detail',
          userId: '2042219067430928390',
          tab: 'posts',
          page: 1,
        },
      },
      canonicalPath: '/u/2042219067430928390',
      sharePath: '/u/2042219067430928390',
      type: 'profile',
    },
  ];

  for (const { route, canonicalPath, sharePath, type } of routes) {
    const head = buildPublicRouteHead(route);

    assert.equal(head.canonicalPath, canonicalPath);
    assert.equal(head.type, type);
    assert.equal(buildPublicCanonicalUrl(head.canonicalPath), `${publicDefaultOrigin}${canonicalPath.split('#')[0]}`);
    assert.equal(buildPublicShareUrl(sharePath), `${publicDefaultOrigin}${sharePath}`);
  }
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

test('P3-10 公开论坛详情登录回流只接受参与意图', () => {
  const returnPath = buildPublicForumPostReturnPath({
    postPublicId,
    commentId,
    intent: 'comment',
  });

  assert.equal(
    returnPath,
    `/forum/post/${postPublicId}?commentId=${commentId}&intent=comment`,
  );
  assert.equal(normalizeAuthReturnPath(returnPath), returnPath);
  assert.equal(normalizeAuthReturnPath(`/forum/post/${postPublicId}`), null);
  assert.equal(normalizeAuthReturnPath(`/forum/post/${postPublicId}?intent=edit`), null);
  assert.equal(normalizeAuthReturnPath(`/forum/post/${postPublicId}?intent=comment&from=discover`), null);
});

test('P3-9 登录回流不接受普通公开路径、外部 URL、协议相对 URL 或反斜杠路径', () => {
  assert.equal(normalizeAuthReturnPath(`/shop/product/${productId}`), null);
  assert.equal(normalizeAuthReturnPath(`/forum/post/${postPublicId}`), null);
  assert.equal(normalizeAuthReturnPath('https://radishx.com/desktop?app=shop'), null);
  assert.equal(normalizeAuthReturnPath('//radishx.com/desktop?app=shop'), null);
  assert.equal(normalizeAuthReturnPath('/desktop\\?app=shop'), null);
});

test('P3-10 圈子入口应是登录态私域回流路径而不是公开内容路由', () => {
  const circleReturnPath = buildCircleReturnPath({ tab: 'following', page: 2 });
  assert.equal(circleReturnPath, '/circle?tab=following&page=2');
  assert.equal(normalizeAuthReturnPath(circleReturnPath), circleReturnPath);
  assert.equal(isPublicContentPathname('/circle'), false);
  assert.equal(isPublicContentPathname('/circle/'), false);
  assert.equal(normalizeAuthReturnPath('/circle?tab=hot'), null);
});

test('P3-10 通知复访入口应是登录态私域回流路径而不是公开内容路由', () => {
  const notificationsReturnPath = buildNotificationsReturnPath();
  assert.equal(notificationsReturnPath, '/notifications');
  assert.equal(normalizeAuthReturnPath(notificationsReturnPath), notificationsReturnPath);
  assert.equal(isPublicContentPathname('/notifications'), false);
  assert.equal(isPublicContentPathname('/notifications/'), false);
  assert.equal(normalizeAuthReturnPath('/notifications?filter=unread'), null);
});

test('P3-10 消息复访入口应是登录态私域回流路径而不是公开内容路由', () => {
  const messagesReturnPath = buildMessagesReturnPath({
    channelId: '2042219067430928390',
    messageId: '2042219067430928391',
  });
  assert.equal(messagesReturnPath, '/messages?channelId=2042219067430928390&messageId=2042219067430928391');
  assert.equal(normalizeAuthReturnPath(messagesReturnPath), messagesReturnPath);
  assert.equal(isPublicContentPathname('/messages'), false);
  assert.equal(isPublicContentPathname('/messages/'), false);
  assert.equal(normalizeAuthReturnPath('/messages?messageId=2042219067430928391'), null);
});

test('P3-10 我的状态入口应是登录态私域回流路径而不是公开内容路由', () => {
  const meReturnPath = buildMeReturnPath();
  assert.equal(meReturnPath, '/me');
  assert.equal(normalizeAuthReturnPath(meReturnPath), meReturnPath);
  assert.equal(isPublicContentPathname('/me'), false);
  assert.equal(isPublicContentPathname('/me/'), false);
  assert.equal(normalizeAuthReturnPath('/me?tab=assets'), null);
});
