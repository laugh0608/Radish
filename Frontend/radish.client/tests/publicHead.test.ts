import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildPublicCanonicalUrl,
  buildPublicShareUrl,
  buildPublicRouteHead,
  publicDefaultOrigin,
  type PublicHeadDescriptor,
} from '../src/public/publicHead.ts';
import {
  buildForumPostPublicHead,
  resolvePublicProfileUserId,
} from '../src/public/forum/publicForumUtils.ts';
import type { PublicRouteDescriptor } from '../src/public/publicRouteNavigation.ts';

test('buildPublicCanonicalUrl 应使用默认公开域名并移除锚点', () => {
  assert.equal(
    buildPublicCanonicalUrl('/docs/Guide#intro'),
    `${publicDefaultOrigin}/docs/Guide`
  );
});

test('resolvePublicProfileUserId 应优先使用 User PublicId 并兼容 LongId', () => {
  assert.equal(
    resolvePublicProfileUserId('2042219067430928384', ' usr_019ea76872bf787981ad3e9d3c6a3417 '),
    'usr_019ea76872bf787981ad3e9d3c6a3417'
  );
  assert.equal(resolvePublicProfileUserId('2042219067430928384', null), '2042219067430928384');
  assert.equal(resolvePublicProfileUserId('2042219067430928384', '   '), '2042219067430928384');
});

test('buildPublicCanonicalUrl 应保留规范化查询参数', () => {
  assert.equal(
    buildPublicCanonicalUrl('/forum/search?q=radish&page=2', 'https://example.test/app'),
    'https://example.test/forum/search?q=radish&page=2'
  );
});

test('buildPublicShareUrl 应使用运行时公开域名配置并保留锚点', () => {
  const globalWithWindow = globalThis as typeof globalThis & { window?: Window };
  const originalWindow = globalWithWindow.window;

  Object.defineProperty(globalWithWindow, 'window', {
    value: {
      __RADISH_RUNTIME_CONFIG__: {
        publicUrl: 'https://configured.example/base',
      },
    } as unknown as Window,
    configurable: true,
  });

  try {
    assert.equal(
      buildPublicCanonicalUrl('/docs/Guide#intro'),
      'https://configured.example/docs/Guide'
    );
    assert.equal(
      buildPublicShareUrl('/docs/Guide#intro'),
      'https://configured.example/docs/Guide#intro'
    );
  } finally {
    if (originalWindow === undefined) {
      delete globalWithWindow.window;
    } else {
      Object.defineProperty(globalWithWindow, 'window', {
        value: originalWindow,
        configurable: true,
      });
    }
  }
});

test('buildPublicShareUrl 应允许显式来源覆盖运行时配置', () => {
  assert.equal(
    buildPublicShareUrl('/docs/Guide#intro', 'https://share.example/app'),
    'https://share.example/docs/Guide#intro'
  );
});

test('buildPublicShareUrl 应保留公开榜单类型与分页分享路径', () => {
  assert.equal(
    buildPublicShareUrl('/leaderboard/post-count?page=3', 'https://share.example/app'),
    'https://share.example/leaderboard/post-count?page=3'
  );
});

test('buildPublicShareUrl 应保留公开分发页区块分享路径', () => {
  assert.equal(
    buildPublicShareUrl('/discover?section=shop', 'https://share.example/app'),
    'https://share.example/discover?section=shop'
  );
});

test('buildPublicRouteHead 应为论坛帖子详情生成 article head', () => {
  const route: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'detail', postId: '2042219067430928384', commentId: '8' },
  };

  const head = buildPublicRouteHead(route);

  assert.deepEqual<PublicHeadDescriptor>(head, {
    title: '论坛帖子 - Radish 论坛',
    description: '阅读 Radish 公开论坛帖子，查看讨论内容与社区互动。',
    canonicalPath: '/forum/post/2042219067430928384?commentId=8',
    type: 'article',
  });
});

test('buildPublicRouteHead 应优先使用论坛帖子 PublicId 生成 canonical path', () => {
  const route: PublicRouteDescriptor = {
    app: 'forum',
    route: {
      kind: 'detail',
      postId: '2042219067430928384',
      postPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      commentId: '8',
    },
  };

  const head = buildPublicRouteHead(route);

  assert.deepEqual<PublicHeadDescriptor>(head, {
    title: '论坛帖子 - Radish 论坛',
    description: '阅读 Radish 公开论坛帖子，查看讨论内容与社区互动。',
    canonicalPath: '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f?commentId=8',
    type: 'article',
  });
});

test('buildForumPostPublicHead 应在详情加载后用 PublicId 刷新 canonical 与分享预览文案', () => {
  const head = buildForumPostPublicHead(
    {
      voId: '2042219067430928384',
      voPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      voTitle: '  公开帖子标题  ',
      voSummary: '  公开摘要\n内容  ',
      voContent: '# 标题\n\n正文内容',
    },
    '2042219067430928385',
    'https://cdn.example.test/post.png'
  );

  assert.deepEqual<PublicHeadDescriptor>(head, {
    title: '公开帖子标题 - Radish 论坛',
    description: '公开摘要 内容',
    canonicalPath: '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f?commentId=2042219067430928385',
    type: 'article',
    imageUrl: 'https://cdn.example.test/post.png',
  });
});

test('buildPublicRouteHead 不应在公开 head 中外露数字兼容标识', () => {
  const forumCategoryHead = buildPublicRouteHead({
    app: 'forum',
    route: { kind: 'list', categoryId: '2042219067430928384', sortBy: 'newest', page: 1 },
  });
  const docsHead = buildPublicRouteHead({
    app: 'docs',
    route: { kind: 'detail', slug: '2042219067430928384' },
  });
  const profileHead = buildPublicRouteHead({
    app: 'profile',
    route: { kind: 'detail', userId: '2042219067430928384', tab: 'posts', page: 1 },
  });

  assert.equal(forumCategoryHead.title.includes('2042219067430928384'), false);
  assert.equal(forumCategoryHead.description.includes('2042219067430928384'), false);
  assert.equal(docsHead.title, '文档详情 - Radish 文档');
  assert.equal(docsHead.description.includes('2042219067430928384'), false);
  assert.equal(profileHead.title, '用户公开主页 - Radish');
  assert.equal(profileHead.description.includes('2042219067430928384'), false);
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
  assert.equal(shopHead.title, '商城商品 - Radish 商城');
  assert.equal(shopHead.description.includes('2042219067430928384'), false);
  assert.equal(shopHead.canonicalPath, '/shop/product/2042219067430928384');
  assert.equal(profileHead.type, 'profile');
  assert.equal(profileHead.title, '用户公开主页 - Radish');
  assert.equal(profileHead.canonicalPath, '/u/7?tab=comments&page=2');
});
