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
  parsePublicShopRoute,
} from '../src/public/shopRouteState.ts';
import {
  buildPublicLeaderboardPath,
  parsePublicLeaderboardRoute,
} from '../src/public/leaderboardRouteState.ts';

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
