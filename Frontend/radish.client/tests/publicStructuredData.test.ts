import assert from 'node:assert/strict';
import test from 'node:test';
import {
  applyPublicStructuredData,
  buildDocsArticleStructuredData,
  buildForumPostStructuredData,
  buildProfilePageStructuredData,
  buildPublicRouteStructuredData,
  buildShopProductStructuredData,
  publicStructuredDataScriptId,
  removePublicStructuredData,
} from '../src/public/publicStructuredData.ts';
import { publicDefaultOrigin } from '../src/public/publicHead.ts';

class FakeScriptElement {
  id = '';
  type = '';
  textContent: string | null = null;
  private readonly owner: FakeHeadElement;

  constructor(owner: FakeHeadElement) {
    this.owner = owner;
  }

  remove(): void {
    this.owner.remove(this);
  }
}

class FakeHeadElement {
  readonly children: FakeScriptElement[] = [];

  querySelector(selector: string): FakeScriptElement | null {
    if (selector !== `#${publicStructuredDataScriptId}`) {
      return null;
    }

    return this.children.find((child) => child.id === publicStructuredDataScriptId) ?? null;
  }

  appendChild(element: FakeScriptElement): FakeScriptElement {
    this.children.push(element);
    return element;
  }

  remove(element: FakeScriptElement): void {
    const index = this.children.indexOf(element);
    if (index >= 0) {
      this.children.splice(index, 1);
    }
  }
}

function installFakeDocument(): FakeHeadElement {
  const head = new FakeHeadElement();
  Object.defineProperty(globalThis, 'document', {
    configurable: true,
    value: {
      head,
      createElement: (tagName: string) => {
        assert.equal(tagName, 'script');
        return new FakeScriptElement(head);
      },
    },
  });
  return head;
}

test.afterEach(() => {
  Reflect.deleteProperty(globalThis, 'document');
});

test('buildForumPostStructuredData 应生成 BlogPosting JSON-LD 并使用 PublicId canonical', () => {
  const data = buildForumPostStructuredData({
    canonicalPath: '/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
    post: {
      voId: '2042219067430928384',
      voPublicId: 'pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f',
      voTitle: '公开帖子标题',
      voSummary: '  公开摘要  ',
      voContent: '# 标题\n\n正文内容',
      voCategoryId: 1,
      voAuthorId: '9',
      voAuthorName: '作者名',
      voCoverImage: 'https://cdn.example.test/post.png',
      voTagNames: ['radish', '公开'],
      voViewCount: 12,
      voCommentCount: 3,
      voCreateTime: '2026-05-17T10:00:00+08:00',
    },
  });

  assert.equal(data['@context'], 'https://schema.org');
  assert.equal(data['@type'], 'BlogPosting');
  assert.equal(data.headline, '公开帖子标题');
  assert.equal(data.description, '公开摘要');
  assert.equal(data.url, `${publicDefaultOrigin}/forum/post/pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f`);
  assert.deepEqual(data.author, {
    '@type': 'Person',
    name: '作者名',
  });
});

test('buildDocsArticleStructuredData 应生成 Article JSON-LD 并清理 markdown 摘要', () => {
  const data = buildDocsArticleStructuredData({
    canonicalPath: '/docs/getting-started#intro',
    document: {
      voId: '101',
      voTitle: '入门文档',
      voSlug: 'getting-started',
      voSummary: null,
      voMarkdownContent: '# 入门\n\n[链接](https://example.test) 和正文',
      voSort: 1,
      voStatus: 1,
      voVisibility: 1,
      voAllowedRoles: [],
      voAllowedPermissions: [],
      voSourceType: 'Markdown',
      voVersion: 2,
      voIsDeleted: false,
      voCreateTime: '2026-05-16T10:00:00+08:00',
      voPublishedAt: '2026-05-17T10:00:00+08:00',
    },
  });

  assert.equal(data['@type'], 'Article');
  assert.equal(data.headline, '入门文档');
  assert.equal(data.url, `${publicDefaultOrigin}/docs/getting-started`);
  assert.equal(data.description, '入门 链接 和正文');
});

test('buildShopProductStructuredData 应生成 Product JSON-LD', () => {
  const data = buildShopProductStructuredData({
    canonicalPath: '/shop/product/2042219067430928384',
    imageUrl: 'https://cdn.example.test/product.png',
    product: {
      voId: '2042219067430928384',
      voName: '商品名',
      voDescription: '商品描述',
      voCoverImage: '/uploads/product.png',
      voCategoryId: '7',
      voCategoryName: '权益',
      voProductType: 'Benefit',
      voPrice: 99,
      voStockType: 'Unlimited',
      voDurationType: 'Forever',
    },
  });

  assert.equal(data['@type'], 'Product');
  assert.equal(data.name, '商品名');
  assert.equal(data.image, 'https://cdn.example.test/product.png');
  assert.equal(data.category, '权益');
});

test('buildProfilePageStructuredData 应生成 ProfilePage JSON-LD 且不使用数字 ID 作为名称', () => {
  const data = buildProfilePageStructuredData({
    canonicalPath: '/u/2042219067430928384',
    imageUrl: 'https://cdn.example.test/avatar.png',
    profile: {
      voUserId: '2042219067430928384',
      voUserName: 'radish-user',
      voDisplayName: '萝卜用户',
      voCreateTime: '2026-05-01T10:00:00+08:00',
      voPet: {
        voPublicId: 'pet_019ea76872bf787981ad3e9d3c6a3417',
        voName: '不会进入结构化数据的小萝卜',
        voSpeciesKey: 'radish',
        voShapeKey: 'sprout',
        voGrowthStage: 2,
        voMood: 'happy',
      },
    },
    stats: {
      voPostCount: 8,
      voCommentCount: 16,
      voTotalLikeCount: 20,
      voPostLikeCount: 12,
      voCommentLikeCount: 8,
    },
  });

  assert.equal(data['@type'], 'ProfilePage');
  assert.equal(data.name, '萝卜用户 - Radish 用户公开主页');
  assert.equal(JSON.stringify(data).includes('"name":"2042219067430928384"'), false);
  assert.equal(JSON.stringify(data).includes('不会进入结构化数据的小萝卜'), false);
  assert.equal(JSON.stringify(data).includes('pet_019ea76872bf787981ad3e9d3c6a3417'), false);
});

test('buildPublicRouteStructuredData 应为公开发现和榜单生成聚合页 JSON-LD', () => {
  const discoverData = buildPublicRouteStructuredData({
    app: 'discover',
    route: { kind: 'home', section: 'shop' },
  });
  const leaderboardData = buildPublicRouteStructuredData({
    app: 'leaderboard',
    route: { kind: 'list', typeSlug: 'post-count', page: 2 },
  });

  assert.equal(discoverData['@type'], 'CollectionPage');
  assert.equal(discoverData.url, `${publicDefaultOrigin}/discover?section=shop`);
  assert.equal(leaderboardData['@type'], 'CollectionPage');
  assert.equal(leaderboardData.url, `${publicDefaultOrigin}/leaderboard/post-count?page=2`);
});

test('buildPublicRouteStructuredData 应把公开浏览页识别为聚合页', () => {
  const routes = [
    {
      app: 'forum',
      route: { kind: 'search', keyword: 'radish', sortBy: 'newest', timeRange: 'all', page: 1 },
    },
    {
      app: 'docs',
      route: { kind: 'search', keyword: 'release', page: 1 },
    },
    {
      app: 'shop',
      route: { kind: 'products', categoryId: 'digital', keyword: 'vip', page: 2 },
    },
  ] as const;

  for (const route of routes) {
    const data = buildPublicRouteStructuredData(route);
    assert.equal(data['@type'], 'CollectionPage');
    assert.equal(typeof data.url, 'string');
  }
});

test('buildPublicRouteStructuredData 不应把数字公开 ID 写成页面名称', () => {
  const data = buildPublicRouteStructuredData({
    app: 'profile',
    route: {
      kind: 'detail',
      userId: '2042219067430928384',
      tab: 'posts',
      page: 1,
    },
  });

  assert.equal(data['@type'], 'WebPage');
  assert.equal(data.url, `${publicDefaultOrigin}/u/2042219067430928384`);
  assert.equal(data.name, '用户公开主页 - Radish');
  assert.equal(JSON.stringify(data).includes('"name":"2042219067430928384"'), false);
});

test('applyPublicStructuredData 应复用单个 script 并支持清理', () => {
  const head = installFakeDocument();

  applyPublicStructuredData({
    '@context': 'https://schema.org',
    '@type': 'Article',
    headline: '第一篇',
  });
  applyPublicStructuredData({
    '@context': 'https://schema.org',
    '@type': 'Article',
    headline: '第二篇',
  });

  assert.equal(head.children.length, 1);
  assert.equal(head.children[0].id, publicStructuredDataScriptId);
  assert.equal(head.children[0].type, 'application/ld+json');
  assert.deepEqual(JSON.parse(head.children[0].textContent ?? '{}'), {
    '@context': 'https://schema.org',
    '@type': 'Article',
    headline: '第二篇',
  });

  removePublicStructuredData();
  assert.equal(head.children.length, 0);
});
