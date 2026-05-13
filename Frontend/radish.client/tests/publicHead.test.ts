import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildPublicCanonicalUrl,
  buildPublicRouteHead,
  publicDefaultOrigin,
  type PublicHeadDescriptor,
} from '../src/public/publicHead.ts';
import type { PublicRouteDescriptor } from '../src/public/publicRouteNavigation.ts';

test('buildPublicCanonicalUrl 应使用默认公开域名并移除锚点', () => {
  assert.equal(
    buildPublicCanonicalUrl('/docs/Guide#intro'),
    `${publicDefaultOrigin}/docs/Guide`
  );
});

test('buildPublicCanonicalUrl 应保留规范化查询参数', () => {
  assert.equal(
    buildPublicCanonicalUrl('/forum/search?q=radish&page=2', 'https://example.test/app'),
    'https://example.test/forum/search?q=radish&page=2'
  );
});

test('buildPublicRouteHead 应为论坛帖子详情生成 article head', () => {
  const route: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'detail', postId: '2042219067430928384', commentId: '8' },
  };

  const head = buildPublicRouteHead(route);

  assert.deepEqual<PublicHeadDescriptor>(head, {
    title: '帖子 2042219067430928384 - Radish 论坛',
    description: '阅读 Radish 公开论坛帖子 2042219067430928384，查看讨论内容与社区互动。',
    canonicalPath: '/forum/post/2042219067430928384?commentId=8',
    type: 'article',
  });
});

test('buildPublicRouteHead 应为 docs 详情生成去重前的路由 canonical path', () => {
  const route: PublicRouteDescriptor = {
    app: 'docs',
    route: { kind: 'detail', slug: 'Guide', anchor: 'overview' },
  };

  const head = buildPublicRouteHead(route);

  assert.equal(head.title, 'Guide - Radish 文档');
  assert.equal(head.canonicalPath, '/docs/Guide#overview');
  assert.equal(buildPublicCanonicalUrl(head.canonicalPath), `${publicDefaultOrigin}/docs/Guide`);
});

test('buildPublicRouteHead 应覆盖商城商品和用户公开页类型', () => {
  const shopHead = buildPublicRouteHead({
    app: 'shop',
    route: { kind: 'detail', productId: '2042219067430928384' },
  });
  const profileHead = buildPublicRouteHead({
    app: 'profile',
    route: { kind: 'detail', userId: '7', tab: 'comments', page: 2 },
  });

  assert.equal(shopHead.type, 'product');
  assert.equal(shopHead.canonicalPath, '/shop/product/2042219067430928384');
  assert.equal(profileHead.type, 'profile');
  assert.equal(profileHead.canonicalPath, '/u/7?tab=comments&page=2');
});
