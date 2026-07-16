import assert from 'node:assert/strict';
import test from 'node:test';
import type { TFunction } from 'i18next';
import {
  applyPublicHead,
  buildPublicCanonicalUrl,
  buildLocalizedPublicRouteHead,
  buildPublicShareUrl,
  buildPublicRouteHead,
  publicDefaultOrigin,
  resetPublicHead,
  type PublicHeadDescriptor,
} from '../src/public/publicHead.ts';
import {
  buildForumPostPublicHead,
  getForumPostRouteIdentifier,
  resolvePublicProfileUserId,
} from '../src/public/forum/publicForumUtils.ts';
import { buildPublicDocsHeadSnapshot } from '../src/public/docs/publicDocsHead.ts';
import type { PublicRouteDescriptor } from '../src/public/publicRouteNavigation.ts';
import {
  resolveActivePublicHeadSnapshot,
  updatePublicHeadRegistration,
  type PublicHeadSnapshot,
} from '../src/public/publicHeadLifecycleContext.ts';
import {
  isCurrentDocsHeadSource,
  isCurrentForumPostHeadSource,
  isCurrentProfileHeadSource,
  isCurrentShopProductHeadSource,
} from '../src/public/publicHeadSourceIdentity.ts';
import { buildPublicRouteStructuredData } from '../src/public/publicStructuredData.ts';
import { enAccount } from '../src/locales/en/account.ts';
import { enCommerce } from '../src/locales/en/commerce.ts';
import { enCommunity } from '../src/locales/en/community.ts';
import { enDiscover } from '../src/locales/en/discover.ts';
import { enDocs } from '../src/locales/en/docs.ts';
import { enShell } from '../src/locales/en/shell.ts';
import { zhAccount } from '../src/locales/zh/account.ts';
import { zhCommerce } from '../src/locales/zh/commerce.ts';
import { zhCommunity } from '../src/locales/zh/community.ts';
import { zhDiscover } from '../src/locales/zh/discover.ts';
import { zhDocs } from '../src/locales/zh/docs.ts';
import { zhShell } from '../src/locales/zh/shell.ts';

function createTestTranslator(resources: Record<string, string>): TFunction {
  return ((key: string, options?: Record<string, unknown>) => {
    const template = resources[key] ?? key;
    return template.replace(/\{\{([^}]+)\}\}/g, (_match, name: string) => (
      String(options?.[name] ?? '')
    ));
  }) as TFunction;
}

const enHeadTranslator = createTestTranslator({
  ...enAccount,
  ...enCommerce,
  ...enCommunity,
  ...enDiscover,
  ...enDocs,
  ...enShell,
});
const zhHeadTranslator = createTestTranslator({
  ...zhAccount,
  ...zhCommerce,
  ...zhCommunity,
  ...zhDiscover,
  ...zhDocs,
  ...zhShell,
});

test('buildPublicCanonicalUrl 应使用默认公开域名并移除锚点', () => {
  assert.equal(
    buildPublicCanonicalUrl('/docs/Guide#intro'),
    `${publicDefaultOrigin}/docs/Guide`
  );
});

test('resolvePublicProfileUserId 应优先使用 User PublicId 并兼容 LongId', () => {
  assert.equal(
    resolvePublicProfileUserId('2042219067430928384', ' USR_019EA76872BF787981AD3E9D3C6A3417 '),
    'usr_019ea76872bf787981ad3e9d3c6a3417'
  );
  assert.equal(resolvePublicProfileUserId('2042219067430928384', null), '2042219067430928384');
  assert.equal(resolvePublicProfileUserId('2042219067430928384', '   '), '2042219067430928384');
  assert.equal(resolvePublicProfileUserId('2042219067430928384', 'usr_not-a-route-id'), '2042219067430928384');
});

test('getForumPostRouteIdentifier 应只使用有效帖子 PublicId', () => {
  assert.equal(
    getForumPostRouteIdentifier({
      voId: '2042219067430928384',
      voPublicId: ' PST_018F6B6F7C7D70008F8F8F8F8F8F8F8F ',
    }),
    'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f'
  );
  assert.equal(
    getForumPostRouteIdentifier({
      voId: '2042219067430928384',
      voPublicId: 'pst_not-a-route-id',
    }),
    '2042219067430928384'
  );
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

test('Docs 详情快照应复用中英文路由基线并保留默认中文契约', () => {
  const document = {
    voId: '101',
    voTitle: '运行时文档原题',
    voSlug: 'runtime-guide',
    voSummary: null,
    voMarkdownContent: '',
    voSort: 1,
    voStatus: 1,
    voVisibility: 1,
    voAllowedRoles: [],
    voAllowedPermissions: [],
    voSourceType: 'Markdown',
    voVersion: 1,
    voIsDeleted: false,
    voCreateTime: '2026-07-16T00:00:00Z',
  };
  const route: PublicRouteDescriptor = {
    app: 'docs',
    route: { kind: 'detail', slug: document.voSlug },
  };
  const enRouteHead = buildLocalizedPublicRouteHead(route, enHeadTranslator);
  const zhRouteHead = buildLocalizedPublicRouteHead(route, zhHeadTranslator);
  const enSnapshot = buildPublicDocsHeadSnapshot(document, undefined, {
    appName: 'Docs',
    routeHead: enRouteHead,
  });
  const zhSnapshot = buildPublicDocsHeadSnapshot(document, undefined, {
    appName: '文档',
    routeHead: zhRouteHead,
  });
  const defaultSnapshot = buildPublicDocsHeadSnapshot(document, undefined);

  assert.equal(enSnapshot.head.title, '运行时文档原题 · Docs');
  assert.equal(
    enSnapshot.head.description,
    'Document detail reading focuses on the body, metadata, internal links, and stable return to the original public source. Editing and publishing stay in the author workspace.',
  );
  assert.equal(enSnapshot.structuredData?.description, enSnapshot.head.description);
  assert.equal(zhSnapshot.head.title, '运行时文档原题 · 文档');
  assert.equal(
    zhSnapshot.head.description,
    '公开文档详情会把阅读重点收口在正文、元信息、文档内链，以及稳定回到原公开来源上，不会从公开阅读页重新带出编辑或发布流程。',
  );
  assert.equal(zhSnapshot.structuredData?.description, zhSnapshot.head.description);
  assert.equal(defaultSnapshot.head.title, '运行时文档原题 · Radish 文档');
  assert.equal(
    defaultSnapshot.head.description,
    '阅读 Radish 公开文档 runtime-guide，了解项目能力、使用方式与协作信息。',
  );
});

test('Forum 详情 head 应复用中英文路由基线、应用名与空内容 fallback', () => {
  const post = {
    voId: '2042219067430928384',
    voPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    voTitle: '运行时帖子原题',
    voSummary: null,
    voContent: '',
  };
  const route: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'detail', postId: post.voPublicId },
  };
  const enRouteHead = buildLocalizedPublicRouteHead(route, enHeadTranslator);
  const zhRouteHead = buildLocalizedPublicRouteHead(route, zhHeadTranslator);
  const enHead = buildForumPostPublicHead(post, undefined, null, {
    appName: 'Forum',
    routeHead: enRouteHead,
  });
  const zhHead = buildForumPostPublicHead(post, undefined, null, {
    appName: '论坛',
    routeHead: zhRouteHead,
  });
  const enUntitledHead = buildForumPostPublicHead({ ...post, voTitle: '   ' }, undefined, null, {
    appName: 'Forum',
    routeHead: enRouteHead,
  });
  const defaultHead = buildForumPostPublicHead({ ...post, voTitle: '   ' });

  assert.equal(enHead.title, '运行时帖子原题 · Forum');
  assert.equal(
    enHead.description,
    'This public entry keeps reading first. Signed-in users can add quick replies and root discussion comments from the post detail page.',
  );
  assert.equal(zhHead.title, '运行时帖子原题 · 论坛');
  assert.equal(
    zhHead.description,
    '这个公开页面优先承载阅读；登录用户可在帖子详情页直接发布轻回应和根评论。',
  );
  assert.equal(enUntitledHead.title, 'Post details · Forum');
  assert.equal(defaultHead.title, '论坛帖子 - Radish 论坛');
  assert.equal(defaultHead.description, '阅读 Radish 公开论坛帖子，查看讨论内容与社区互动。');
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
    route: { kind: 'detail', productId: '2042219067430928384', intent: 'purchase' },
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

test('本地化公开 head 应覆盖全部集合路由并保留运营与用户原文', () => {
  const routes: PublicRouteDescriptor[] = [
    { app: 'discover', route: { kind: 'home' } },
    { app: 'forum', route: { kind: 'search', keyword: '用户原词', sortBy: 'newest', timeRange: 'all', page: 1 } },
    { app: 'docs', route: { kind: 'search', keyword: '运营原词', page: 1 } },
    { app: 'leaderboard', route: { typeSlug: 'experience', page: 1 } },
    { app: 'shop', route: { kind: 'products', keyword: '商品原词', page: 1 } },
    { app: 'legal', route: { kind: 'commitments' } },
  ];

  assert.deepEqual(
    routes.map((route) => buildLocalizedPublicRouteHead(route, enHeadTranslator).title),
    [
      'Discover · Radish',
      'Results for "用户原词" · Forum',
      'Search "运营原词" · Docs',
      'Public leaderboard · Leaderboard',
      '商品原词 · Shop',
      'Radish Commitments',
    ],
  );
  assert.deepEqual(
    routes.map((route) => buildLocalizedPublicRouteHead(route, zhHeadTranslator).title),
    [
      '发现 · Radish',
      '“用户原词” 的搜索结果 · 论坛',
      '搜索“运营原词” · 文档',
      '公开榜单 · 排行榜',
      '商品原词 · 萝卜商城',
      'Radish 承诺',
    ],
  );

  for (const route of routes) {
    assert.notEqual(
      buildLocalizedPublicRouteHead(route, enHeadTranslator).description,
      buildPublicRouteHead(route).description,
    );
  }
});

test('集合页 JSON-LD 应复用已解析的中英文公开 head', () => {
  const route: PublicRouteDescriptor = {
    app: 'forum',
    route: { kind: 'search', keyword: '用户原词', sortBy: 'newest', timeRange: 'all', page: 1 },
  };
  const enHead = buildLocalizedPublicRouteHead(route, enHeadTranslator);
  const zhHead = buildLocalizedPublicRouteHead(route, zhHeadTranslator);
  const enData = buildPublicRouteStructuredData(route, { head: enHead });
  const zhData = buildPublicRouteStructuredData(route, { head: zhHead });

  assert.equal(enData['@type'], 'CollectionPage');
  assert.equal(enData.name, enHead.title);
  assert.equal(enData.description, enHead.description);
  assert.equal(zhData['@type'], 'CollectionPage');
  assert.equal(zhData.name, zhHead.title);
  assert.equal(zhData.description, zhHead.description);
  assert.notEqual(enData.name, zhData.name);
  assert.equal(String(enData.name).includes('用户原词'), true);
  assert.equal(String(zhData.name).includes('用户原词'), true);
});

test('详情 head 数据源必须与当前 docs/forum/shop/profile 路由身份一致', () => {
  const document = {
    voId: '100',
    voSlug: 'getting-started',
  };
  assert.equal(
    isCurrentDocsHeadSource({ kind: 'detail', slug: 'getting-started' }, document),
    true,
  );
  assert.equal(
    isCurrentDocsHeadSource({ kind: 'detail', slug: '100' }, document),
    true,
  );
  assert.equal(
    isCurrentDocsHeadSource({ kind: 'detail', slug: 'release-notes' }, document),
    false,
  );

  const post = {
    voId: '200',
    voPublicId: 'pst_0123456789abcdef0123456789abcdef',
  };
  assert.equal(isCurrentForumPostHeadSource('200', post), true);
  assert.equal(
    isCurrentForumPostHeadSource('pst_0123456789abcdef0123456789abcdef', post),
    true,
  );
  assert.equal(isCurrentForumPostHeadSource('201', post), false);

  assert.equal(
    isCurrentShopProductHeadSource({ kind: 'detail', productId: '200' }, '200'),
    true,
  );
  assert.equal(
    isCurrentShopProductHeadSource({ kind: 'detail', productId: '201' }, '200'),
    false,
  );

  const profile = {
    voUserId: '300',
    voPublicId: 'usr_0123456789abcdef0123456789abcdef',
    voUserName: '原始用户名',
    voCreateTime: '2026-07-16T00:00:00Z',
  };
  assert.equal(
    isCurrentProfileHeadSource({ kind: 'detail', userId: '300', tab: 'posts', page: 1 }, profile),
    true,
  );
  assert.equal(
    isCurrentProfileHeadSource({
      kind: 'detail',
      userId: 'usr_0123456789abcdef0123456789abcdef',
      tab: 'posts',
      page: 1,
    }, profile),
    true,
  );
  assert.equal(
    isCurrentProfileHeadSource({ kind: 'detail', userId: '301', tab: 'posts', page: 1 }, profile),
    false,
  );
});

test('head 快照应兼容 StrictMode 重放、按 route key 隔离且阻止旧 owner 清理新 owner', () => {
  const firstToken = Symbol('first');
  const secondToken = Symbol('second');
  const firstSnapshot: PublicHeadSnapshot = {
    head: {
      title: '第一页 - Radish',
      description: '第一页',
      canonicalPath: '/forum/post/first',
    },
    structuredData: { '@type': 'BlogPosting', name: '第一页' },
  };
  const secondSnapshot: PublicHeadSnapshot = {
    head: {
      title: '第二页 - Radish',
      description: '第二页',
      canonicalPath: '/forum/post/second',
    },
    structuredData: { '@type': 'BlogPosting', name: '第二页' },
  };

  let registration = updatePublicHeadRegistration(null, firstToken, '/forum/post/first', firstSnapshot);
  assert.equal(resolveActivePublicHeadSnapshot(registration, '/forum/post/first'), firstSnapshot);
  assert.equal(resolveActivePublicHeadSnapshot(registration, '/forum/post/second'), null);
  assert.equal(
    updatePublicHeadRegistration(registration, firstToken, '/forum/post/first', firstSnapshot),
    registration,
  );

  registration = updatePublicHeadRegistration(registration, firstToken, '/forum/post/first', null);
  registration = updatePublicHeadRegistration(registration, firstToken, '/forum/post/first', firstSnapshot);
  assert.equal(resolveActivePublicHeadSnapshot(registration, '/forum/post/first'), firstSnapshot);

  registration = updatePublicHeadRegistration(registration, secondToken, '/forum/post/second', secondSnapshot);
  const newOwnerRegistration = registration;
  registration = updatePublicHeadRegistration(registration, firstToken, '/forum/post/first', null);

  assert.equal(registration, newOwnerRegistration);
  assert.equal(resolveActivePublicHeadSnapshot(registration, '/forum/post/second'), secondSnapshot);
  assert.equal(updatePublicHeadRegistration(registration, secondToken, '/forum/post/second', null), null);
});

class FakeHeadNode {
  readonly attributes = new Map<string, string>();
  readonly tagName: string;
  textContent: string | null = null;
  private readonly owner: FakeHead;

  constructor(tagName: string, owner: FakeHead) {
    this.tagName = tagName;
    this.owner = owner;
  }

  setAttribute(name: string, value: string): void {
    this.attributes.set(name, value);
  }

  getAttribute(name: string): string | null {
    return this.attributes.get(name) ?? null;
  }

  remove(): void {
    this.owner.remove(this);
  }
}

class FakeHead {
  readonly children: FakeHeadNode[] = [];

  querySelector(selector: string): FakeHeadNode | null {
    const match = /^(meta|link)\[(name|property|rel)="([^"]+)"\]$/.exec(selector);
    if (!match) {
      return null;
    }

    const [, tagName, attributeName, attributeValue] = match;
    return this.children.find((child) => (
      child.tagName === tagName
      && child.getAttribute(attributeName) === attributeValue
    )) ?? null;
  }

  appendChild(node: FakeHeadNode): FakeHeadNode {
    this.children.push(node);
    return node;
  }

  remove(node: FakeHeadNode): void {
    const index = this.children.indexOf(node);
    if (index >= 0) {
      this.children.splice(index, 1);
    }
  }
}

test('applyPublicHead 应统一更新分享元信息并在离开时清理公开路由状态', () => {
  const head = new FakeHead();
  const fakeDocument = {
    title: '',
    head,
    createElement: (tagName: string) => new FakeHeadNode(tagName, head),
  };

  Object.defineProperty(globalThis, 'document', {
    configurable: true,
    value: fakeDocument,
  });

  try {
    applyPublicHead({
      title: '详情页 - Radish',
      description: '详情摘要',
      canonicalPath: '/forum/post/pst_example',
      type: 'article',
      imageUrl: 'https://cdn.example.test/cover.png',
    });

    assert.equal(fakeDocument.title, '详情页 - Radish');
    assert.equal(head.querySelector('link[rel="canonical"]')?.getAttribute('href'), `${publicDefaultOrigin}/forum/post/pst_example`);
    assert.equal(head.querySelector('meta[property="og:image"]')?.getAttribute('content'), 'https://cdn.example.test/cover.png');
    assert.equal(head.querySelector('meta[name="twitter:card"]')?.getAttribute('content'), 'summary_large_image');

    applyPublicHead({
      title: '聚合页 - Radish',
      description: '聚合页摘要',
      canonicalPath: '/discover',
    });

    assert.equal(head.querySelector('meta[property="og:image"]'), null);
    assert.equal(head.querySelector('meta[name="twitter:image"]'), null);
    assert.equal(head.querySelector('meta[name="twitter:card"]')?.getAttribute('content'), 'summary');

    resetPublicHead();

    assert.equal(fakeDocument.title, 'Radish');
    assert.equal(head.querySelector('link[rel="canonical"]'), null);
    assert.equal(head.querySelector('meta[property="og:url"]'), null);
    assert.equal(head.querySelector('meta[name="twitter:card"]'), null);
  } finally {
    Reflect.deleteProperty(globalThis, 'document');
  }
});
