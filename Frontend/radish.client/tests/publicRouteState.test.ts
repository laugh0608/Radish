import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildPublicDocsPath,
  parsePublicDocsRoute,
  resolvePublicDocsRouteFromHref,
  rewritePublicDocsHref,
} from '../src/public/docsRouteState.ts';
import {
  buildPublicProfilePath,
  parsePublicProfileRoute,
} from '../src/public/profileRouteState.ts';
import {
  buildPublicShopPath,
  isPublicShopPathname,
  parsePublicShopRoute,
} from '../src/public/shopRouteState.ts';
import {
  buildPublicLeaderboardPath,
  parsePublicLeaderboardRoute,
} from '../src/public/leaderboardRouteState.ts';
import {
  buildPublicForumPath,
  parsePublicForumRoute,
} from '../src/public/forumRouteState.ts';

test('parsePublicDocsRoute 应解析 docs 搜索与分页参数', () => {
  const route = parsePublicDocsRoute('/docs/search', '?q=radish&page=3');

  assert.deepEqual(route, {
    kind: 'search',
    keyword: 'radish',
    page: 3,
  });
});

test('parsePublicDocsRoute 应把空关键词与非法页码规范化到稳定搜索路由', () => {
  const route = parsePublicDocsRoute('/docs/search', '?q=%20&page=0');

  assert.deepEqual(route, {
    kind: 'search',
    keyword: '',
    page: 1,
  });
});

test('parsePublicDocsRoute 应兼容旧 __documents__ 详情与锚点', () => {
  const route = parsePublicDocsRoute('/__documents__/Getting-Started', '#install');

  assert.deepEqual(route, {
    kind: 'detail',
    slug: 'Getting-Started',
    anchor: 'install',
  });
});

test('buildPublicDocsPath 应为保留字 search 回写 __documents__ 兼容路径', () => {
  const path = buildPublicDocsPath({
    kind: 'detail',
    slug: 'search',
    anchor: 'intro',
  });

  assert.equal(path, '/__documents__/search#intro');
});

test('resolvePublicDocsRouteFromHref 与 rewritePublicDocsHref 应把站内 docs 链接收口到公开壳层', () => {
  const href = 'https://radish.local/__documents__/Guide#overview';

  assert.deepEqual(resolvePublicDocsRouteFromHref(href, 'https://radish.local'), {
    kind: 'detail',
    slug: 'Guide',
    anchor: 'overview',
  });
  assert.equal(rewritePublicDocsHref(href, 'https://radish.local'), '/docs/Guide#overview');
});

test('resolvePublicDocsRouteFromHref 应按当前文档路径解析 Markdown 相对链接与同页锚点', () => {
  assert.deepEqual(
    resolvePublicDocsRouteFromHref('Next-Guide#install', 'https://radish.local', '/docs/Getting-Started#overview'),
    {
      kind: 'detail',
      slug: 'Next-Guide',
      anchor: 'install',
    }
  );
  assert.deepEqual(
    resolvePublicDocsRouteFromHref('#faq', 'https://radish.local', '/docs/Getting-Started'),
    {
      kind: 'detail',
      slug: 'Getting-Started',
      anchor: 'faq',
    }
  );
  assert.equal(
    rewritePublicDocsHref('#faq', 'https://radish.local', '/docs/Getting-Started'),
    '/docs/Getting-Started#faq'
  );
});

test('parsePublicDocsRoute 应忽略非法编码的 slug 与锚点并回落到稳定路由', () => {
  assert.deepEqual(parsePublicDocsRoute('/docs/%E0%A4%A', ''), { kind: 'list' });
  assert.deepEqual(parsePublicDocsRoute('/docs/Guide', '#%E0%A4%A'), {
    kind: 'detail',
    slug: 'Guide',
    anchor: undefined,
  });
});

test('parsePublicProfileRoute 应保留公开个人页 tab 与分页状态', () => {
  const route = parsePublicProfileRoute('/u/2042219067430928384', '?tab=comments&page=4');

  assert.deepEqual(route, {
    kind: 'detail',
    userId: '2042219067430928384',
    tab: 'comments',
    page: 4,
  });
});

test('buildPublicProfilePath 应稳定回写公开个人页参数', () => {
  const path = buildPublicProfilePath({
    kind: 'detail',
    userId: '2042219067430928384',
    tab: 'comments',
    page: 4,
  });

  assert.equal(path, '/u/2042219067430928384?tab=comments&page=4');
});

test('parsePublicShopRoute 应解析公开商城列表上下文', () => {
  const route = parsePublicShopRoute('/shop/products', '?category=digital&q=vip&page=5');

  assert.deepEqual(route, {
    kind: 'products',
    categoryId: 'digital',
    keyword: 'vip',
    page: 5,
  });
});

test('parsePublicShopRoute 应规范化空查询参数与非法页码', () => {
  const route = parsePublicShopRoute('/shop/products', '?category=%20&q=%20&page=0');

  assert.deepEqual(route, {
    kind: 'products',
    categoryId: undefined,
    keyword: undefined,
    page: 1,
  });
});

test('parsePublicShopRoute 应保留商品详情的大整数字符串 ID', () => {
  const route = parsePublicShopRoute('/shop/product/2042219067430928384', '');

  assert.deepEqual(route, {
    kind: 'detail',
    productId: '2042219067430928384',
  });
});

test('parsePublicShopRoute 应保留正式 Web 购买意图并拒绝其他意图', () => {
  assert.deepEqual(parsePublicShopRoute('/shop/product/2042219067430928384', '?intent=purchase'), {
    kind: 'detail',
    productId: '2042219067430928384',
    intent: 'purchase',
  });
  assert.deepEqual(parsePublicShopRoute('/shop/product/2042219067430928384', '?intent=read'), {
    kind: 'detail',
    productId: '2042219067430928384',
  });
  assert.deepEqual(parsePublicShopRoute('/shop/product/2042219067430928384', '?intent=purchase&intent=purchase'), {
    kind: 'detail',
    productId: '2042219067430928384',
  });
});

test('parsePublicShopRoute 应拒绝非法商品详情 ID 并回落到商城首页', () => {
  assert.deepEqual(parsePublicShopRoute('/shop/product/0', ''), { kind: 'home' });
  assert.deepEqual(parsePublicShopRoute('/shop/product/abc', ''), { kind: 'home' });
});

test('buildPublicShopPath 应回写公开商城列表和详情路径', () => {
  const productsPath = buildPublicShopPath({
    kind: 'products',
    categoryId: 'digital',
    keyword: 'vip',
    page: 5,
  });
  const detailPath = buildPublicShopPath({
    kind: 'detail',
    productId: '2042219067430928384',
  });

  assert.equal(productsPath, '/shop/products?category=digital&q=vip&page=5');
  assert.equal(detailPath, '/shop/product/2042219067430928384');
  assert.equal(
    buildPublicShopPath({
      kind: 'detail',
      productId: '2042219067430928384',
      intent: 'purchase',
    }),
    '/shop/product/2042219067430928384?intent=purchase'
  );
});

test('isPublicShopPathname 应只识别公开商城浏览路径', () => {
  assert.equal(isPublicShopPathname('/shop'), true);
  assert.equal(isPublicShopPathname('/shop/'), true);
  assert.equal(isPublicShopPathname('/shop/products'), true);
  assert.equal(isPublicShopPathname('/shop/product/2042219067430928384'), true);
  assert.equal(isPublicShopPathname('/shop/orders'), false);
  assert.equal(isPublicShopPathname('/shop/order/2042219067430928385'), false);
  assert.equal(isPublicShopPathname('/shop/inventory'), false);
});

test('parsePublicLeaderboardRoute 应解析类型与分页状态', () => {
  const route = parsePublicLeaderboardRoute('/leaderboard/post-count', '?page=3');

  assert.deepEqual(route, {
    kind: 'list',
    typeSlug: 'post-count',
    page: 3,
  });
});

test('parsePublicLeaderboardRoute 应把非法榜单类型与页码回落到默认值', () => {
  const route = parsePublicLeaderboardRoute('/leaderboard/unknown', '?page=0');

  assert.deepEqual(route, {
    kind: 'list',
    typeSlug: 'experience',
    page: 1,
  });
});

test('buildPublicLeaderboardPath 应稳定回写默认与非默认榜单路径', () => {
  const defaultPath = buildPublicLeaderboardPath({
    kind: 'list',
    typeSlug: 'experience',
    page: 1,
  });
  const typedPath = buildPublicLeaderboardPath({
    kind: 'list',
    typeSlug: 'post-count',
    page: 3,
  });

  assert.equal(defaultPath, '/leaderboard');
  assert.equal(typedPath, '/leaderboard/post-count?page=3');
});

test('parsePublicForumRoute 应解析公开帖子详情评论定位与参与意图', () => {
  const route = parsePublicForumRoute(
    '/forum/post/PST_018F6B6F7C7D70008F8F8F8F8F8F8F8F',
    '?intent=quickReply&commentId=2042219067430928385'
  );

  assert.deepEqual(route, {
    kind: 'detail',
    postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928385',
    intent: 'quickReply',
  });
});

test('buildPublicForumPath 应稳定回写公开帖子详情 intent 参数', () => {
  const path = buildPublicForumPath({
    kind: 'detail',
    postId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    commentId: '2042219067430928385',
    intent: 'comment',
  });

  assert.equal(
    path,
    '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f?commentId=2042219067430928385&intent=comment'
  );
});
