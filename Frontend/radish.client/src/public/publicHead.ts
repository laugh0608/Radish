import type { TFunction } from 'i18next';
import { buildPublicDiscoverPath } from './discoverRouteState.ts';
import { buildPublicDocsPath } from './docsRouteState.ts';
import { buildPublicForumPath } from './forumRouteState.ts';
import {
  buildPublicLeaderboardPath,
  getPublicLeaderboardRouteDefinitionBySlug,
} from './leaderboardRouteState.ts';
import { buildPublicLegalPath } from './legalRouteState.ts';
import { buildPublicProfilePath } from './profileRouteState.ts';
import { buildPublicShopPath } from './shopRouteState.ts';
import type { PublicContentRouteDescriptor } from './publicRouteNavigation.ts';

export const publicSiteName = 'Radish';
export const publicDefaultOrigin = 'https://radishx.com';
export const publicDefaultDescription = 'Radish 是面向公开阅读、社区互动与个人工作台的现代社区平台。';

export type PublicOpenGraphType = 'website' | 'article' | 'profile' | 'product';

export interface PublicHeadDescriptor {
  title: string;
  description: string;
  canonicalPath: string;
  type?: PublicOpenGraphType;
  imageUrl?: string;
}

export interface ApplyPublicHeadOptions {
  origin?: string;
}

function normalizeOrigin(origin: string | undefined): string {
  if (!origin) {
    return publicDefaultOrigin;
  }

  try {
    const url = new URL(origin);
    return url.origin;
  } catch {
    return publicDefaultOrigin;
  }
}

function readRuntimePublicOrigin(): string | undefined {
  if (typeof window === 'undefined') {
    return undefined;
  }

  return window.__RADISH_RUNTIME_CONFIG__?.publicUrl;
}

export function resolvePublicOrigin(origin?: string): string {
  if (origin) {
    return normalizeOrigin(origin);
  }

  return normalizeOrigin(readRuntimePublicOrigin() ?? publicDefaultOrigin);
}

function resolveRuntimeOrigin(origin?: string): string {
  if (origin) {
    return normalizeOrigin(origin);
  }

  const runtimePublicOrigin = readRuntimePublicOrigin();
  if (runtimePublicOrigin) {
    return normalizeOrigin(runtimePublicOrigin);
  }

  if (typeof window !== 'undefined' && window.location.origin) {
    return normalizeOrigin(window.location.origin);
  }

  return publicDefaultOrigin;
}

function stripHash(canonicalPath: string): string {
  const hashIndex = canonicalPath.indexOf('#');
  return hashIndex >= 0 ? canonicalPath.slice(0, hashIndex) : canonicalPath;
}

function normalizeDescription(description: string): string {
  const normalized = description.replace(/\s+/g, ' ').trim();
  return normalized || publicDefaultDescription;
}

function isNumericRouteIdentifier(value: string | undefined): boolean {
  return Boolean(value && /^\d+$/.test(value));
}

export function buildPublicCanonicalUrl(canonicalPath: string, origin?: string): string {
  const baseOrigin = resolvePublicOrigin(origin);
  return new URL(stripHash(canonicalPath), baseOrigin).toString();
}

export function buildPublicShareUrl(publicPath: string, origin?: string): string {
  return new URL(publicPath, resolvePublicOrigin(origin)).toString();
}

function buildDiscoverHead(route: PublicContentRouteDescriptor & { app: 'discover' }): PublicHeadDescriptor {
  return {
    title: `发现 - ${publicSiteName}`,
    description: '浏览 Radish 的公开内容、社区动态、文档入口、榜单与商城推荐。',
    canonicalPath: buildPublicDiscoverPath(route.route),
  };
}

function buildForumHead(route: PublicContentRouteDescriptor & { app: 'forum' }): PublicHeadDescriptor {
  const canonicalPath = buildPublicForumPath(route.route);
  if (route.route.kind === 'detail') {
    return {
      title: `论坛帖子 - Radish 论坛`,
      description: '阅读 Radish 公开论坛帖子，查看讨论内容与社区互动。',
      canonicalPath,
      type: 'article',
    };
  }

  if (route.route.kind === 'tag') {
    return {
      title: `#${route.route.tagSlug} - Radish 论坛`,
      description: `浏览 Radish 论坛中带有 ${route.route.tagSlug} 标签的公开讨论。`,
      canonicalPath,
    };
  }

  if (route.route.kind === 'search') {
    const keywordLabel = route.route.keyword || '公开讨论';
    return {
      title: `${keywordLabel} - 论坛搜索 - ${publicSiteName}`,
      description: `在 Radish 论坛搜索 ${keywordLabel}，发现公开帖子、问答、投票与抽奖内容。`,
      canonicalPath,
    };
  }

  if (route.route.kind === 'question') {
    return {
      title: `问答 - Radish 论坛`,
      description: '浏览 Radish 论坛公开问答内容，查看社区问题、回答与互动反馈。',
      canonicalPath,
    };
  }

  if (route.route.kind === 'poll') {
    return {
      title: `投票 - Radish 论坛`,
      description: '浏览 Radish 论坛公开投票内容，查看社区观点与投票结果。',
      canonicalPath,
    };
  }

  if (route.route.kind === 'lottery') {
    return {
      title: `抽奖 - Radish 论坛`,
      description: '浏览 Radish 论坛公开抽奖内容，查看社区活动与参与信息。',
      canonicalPath,
    };
  }

  if (route.route.kind === 'list') {
    return {
      title: route.route.categoryId ? `论坛分类 - ${publicSiteName}` : `论坛 - ${publicSiteName}`,
      description: '浏览 Radish 论坛的公开帖子、问答、投票、抽奖与社区讨论。',
      canonicalPath,
    };
  }

  return {
    title: `论坛 - ${publicSiteName}`,
    description: '浏览 Radish 论坛的公开帖子、问答、投票、抽奖与社区讨论。',
    canonicalPath,
  };
}

function buildDocsHead(route: PublicContentRouteDescriptor & { app: 'docs' }): PublicHeadDescriptor {
  const canonicalPath = buildPublicDocsPath(route.route);
  if (route.route.kind === 'detail') {
    if (isNumericRouteIdentifier(route.route.slug)) {
      return {
        title: `文档详情 - Radish 文档`,
        description: '阅读 Radish 公开文档，了解项目能力、使用方式与协作信息。',
        canonicalPath,
        type: 'article',
      };
    }

    return {
      title: `${route.route.slug} - Radish 文档`,
      description: `阅读 Radish 公开文档 ${route.route.slug}，了解项目能力、使用方式与协作信息。`,
      canonicalPath,
      type: 'article',
    };
  }

  if (route.route.kind === 'search') {
    const keywordLabel = route.route.keyword || '公开文档';
    return {
      title: `${keywordLabel} - 文档搜索 - ${publicSiteName}`,
      description: `在 Radish 文档中搜索 ${keywordLabel}，定位公开说明、指南与项目资料。`,
      canonicalPath,
    };
  }

  return {
    title: `文档 - ${publicSiteName}`,
    description: '阅读 Radish 公开文档，查看项目说明、使用指南、规划与治理资料。',
    canonicalPath,
  };
}

function buildProfileHead(route: PublicContentRouteDescriptor & { app: 'profile' }): PublicHeadDescriptor {
  return {
    title: `用户公开主页 - ${publicSiteName}`,
    description: '查看 Radish 用户的公开主页、帖子与评论记录。',
    canonicalPath: buildPublicProfilePath(route.route),
    type: 'profile',
  };
}

function buildLeaderboardHead(route: PublicContentRouteDescriptor & { app: 'leaderboard' }): PublicHeadDescriptor {
  const definition = getPublicLeaderboardRouteDefinitionBySlug(route.route.typeSlug);
  return {
    title: `${definition.slug} 榜单 - ${publicSiteName}`,
    description: '查看 Radish 公开榜单，了解社区用户、商品与内容的活跃排行。',
    canonicalPath: buildPublicLeaderboardPath(route.route),
  };
}

function buildShopHead(route: PublicContentRouteDescriptor & { app: 'shop' }): PublicHeadDescriptor {
  const canonicalPath = route.route.kind === 'detail'
    ? buildPublicShopPath({ kind: 'detail', productId: route.route.productId })
    : buildPublicShopPath(route.route);
  if (route.route.kind === 'detail') {
    return {
      title: `商城商品 - Radish 商城`,
      description: '查看 Radish 商城公开商品详情与兑换信息。',
      canonicalPath,
      type: 'product',
    };
  }

  if (route.route.kind === 'products') {
    const keywordLabel = route.route.keyword || route.route.categoryId || '商品';
    return {
      title: `${keywordLabel} - Radish 商城`,
      description: `浏览 Radish 商城公开商品列表，按分类与关键词发现可兑换内容。`,
      canonicalPath,
    };
  }

  return {
    title: `商城 - ${publicSiteName}`,
    description: '浏览 Radish 商城公开商品、兑换内容与社区权益。',
    canonicalPath,
  };
}

function buildLegalHead(route: PublicContentRouteDescriptor & { app: 'legal' }): PublicHeadDescriptor {
  return {
    title: `用户承诺与社区边界 - ${publicSiteName}`,
    description: '查看 Radish 的社区公约、隐私边界、账号安全、虚拟商品与资产说明、敏感内容和反馈诊断口径。',
    canonicalPath: buildPublicLegalPath(route.route),
  };
}

export function buildPublicRouteHead(route: PublicContentRouteDescriptor): PublicHeadDescriptor {
  if (route.app === 'discover') {
    return buildDiscoverHead(route);
  }

  if (route.app === 'forum') {
    return buildForumHead(route);
  }

  if (route.app === 'docs') {
    return buildDocsHead(route);
  }

  if (route.app === 'profile') {
    return buildProfileHead(route);
  }

  if (route.app === 'leaderboard') {
    return buildLeaderboardHead(route);
  }

  if (route.app === 'legal') {
    return buildLegalHead(route);
  }

  return buildShopHead(route);
}

function withLocalizedAppTitle(pageTitle: string, appName: string): string {
  return `${pageTitle} · ${appName}`;
}

/**
 * 构造当前界面语言的公开路由 head 基线。路由中的搜索词、标签等原始值只做插值，不交给 i18n 当作翻译键。
 */
export function buildLocalizedPublicRouteHead(
  route: PublicContentRouteDescriptor,
  t: TFunction,
): PublicHeadDescriptor {
  const routeHead = buildPublicRouteHead(route);

  if (route.app === 'discover') {
    return {
      ...routeHead,
      title: withLocalizedAppTitle(t('discover.public.pageTitle'), publicSiteName),
      description: t('discover.public.pageIntro'),
    };
  }

  if (route.app === 'forum') {
    const appName = t('desktop.apps.forum.name');
    const forumRoute = route.route;
    const pageTitle = forumRoute.kind === 'detail'
      ? t('forum.postDetail.title')
      : forumRoute.kind === 'compose'
        ? t('forum.public.composeTitle')
        : forumRoute.kind === 'search'
          ? forumRoute.keyword
            ? t('forum.public.searchResultTitle', { keyword: forumRoute.keyword })
            : t('forum.public.searchTitle')
          : forumRoute.kind === 'tag'
            ? `#${forumRoute.tagSlug}`
            : forumRoute.kind === 'question'
              ? t('forum.public.questionTitle')
              : forumRoute.kind === 'poll'
                ? t('forum.public.pollTitle')
                : forumRoute.kind === 'lottery'
                  ? t('forum.public.lotteryTitle')
                  : t('forum.allPosts');
    const description = forumRoute.kind === 'compose'
      ? t('forum.public.composeDescription')
      : forumRoute.kind === 'search'
        ? t('forum.public.searchIdleDescription')
        : t('forum.public.readOnlyDescription');

    return {
      ...routeHead,
      title: withLocalizedAppTitle(pageTitle, appName),
      description,
    };
  }

  if (route.app === 'docs') {
    const appName = t('desktop.apps.document.name');
    const docsRoute = route.route;
    const pageTitle = docsRoute.kind === 'detail'
      ? isNumericRouteIdentifier(docsRoute.slug)
        ? t('wiki.public.detailTitle')
        : docsRoute.slug
      : docsRoute.kind === 'search'
        ? docsRoute.keyword
          ? t('wiki.public.searchResultTitle', { keyword: docsRoute.keyword })
          : t('wiki.public.searchTitle')
        : t('wiki.public.pageTitle');
    const description = docsRoute.kind === 'detail'
      ? t('wiki.public.detailGuideDescription')
      : docsRoute.kind === 'search'
        ? t('wiki.public.searchGuideDescription')
        : t('wiki.public.listGuideDescription');

    return {
      ...routeHead,
      title: withLocalizedAppTitle(pageTitle, appName),
      description,
    };
  }

  if (route.app === 'profile') {
    return {
      ...routeHead,
      title: withLocalizedAppTitle(t('profile.public.title'), publicSiteName),
      description: t('profile.public.contentDescription'),
    };
  }

  if (route.app === 'leaderboard') {
    return {
      ...routeHead,
      title: withLocalizedAppTitle(
        t('leaderboard.public.title'),
        t('desktop.apps.leaderboard.name'),
      ),
      description: t('leaderboard.public.pageIntro'),
    };
  }

  if (route.app === 'legal') {
    return {
      ...routeHead,
      title: t('legal.header.brandName'),
      description: t('workbench.item.legal.description'),
    };
  }

  const appName = t('desktop.apps.shop.name');
  const shopRoute = route.route;
  const pageTitle = shopRoute.kind === 'detail'
    ? t('shop.public.detailTitle')
    : shopRoute.kind === 'products'
      ? shopRoute.keyword?.trim() || t('shop.public.productsTitle')
      : t('shop.public.homeTitle');
  const description = shopRoute.kind === 'detail'
    ? t('shop.public.detailGuideDescription')
    : shopRoute.kind === 'products'
      ? t('shop.public.productsToolbarDescription')
      : t('shop.public.guideDescription');

  return {
    ...routeHead,
    title: withLocalizedAppTitle(pageTitle, appName),
    description,
  };
}

function upsertMetaByName(name: string, content: string): void {
  const normalizedContent = normalizeDescription(content);
  const existing = document.head.querySelector<HTMLMetaElement>(`meta[name="${name}"]`);
  const element = existing ?? document.createElement('meta');
  element.setAttribute('name', name);
  element.setAttribute('content', normalizedContent);
  if (!existing) {
    document.head.appendChild(element);
  }
}

function removeMetaByName(name: string): void {
  document.head.querySelector<HTMLMetaElement>(`meta[name="${name}"]`)?.remove();
}

function upsertMetaByProperty(property: string, content: string): void {
  const normalizedContent = normalizeDescription(content);
  const existing = document.head.querySelector<HTMLMetaElement>(`meta[property="${property}"]`);
  const element = existing ?? document.createElement('meta');
  element.setAttribute('property', property);
  element.setAttribute('content', normalizedContent);
  if (!existing) {
    document.head.appendChild(element);
  }
}

function removeMetaByProperty(property: string): void {
  document.head.querySelector<HTMLMetaElement>(`meta[property="${property}"]`)?.remove();
}

function upsertCanonicalLink(href: string): void {
  const existing = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
  const element = existing ?? document.createElement('link');
  element.setAttribute('rel', 'canonical');
  element.setAttribute('href', href);
  if (!existing) {
    document.head.appendChild(element);
  }
}

export function applyPublicHead(head: PublicHeadDescriptor, options?: ApplyPublicHeadOptions): void {
  if (typeof document === 'undefined') {
    return;
  }

  const title = head.title.trim() || publicSiteName;
  const description = normalizeDescription(head.description);
  const canonicalUrl = buildPublicCanonicalUrl(head.canonicalPath, resolveRuntimeOrigin(options?.origin));
  document.title = title;
  upsertMetaByName('description', description);
  upsertMetaByProperty('og:site_name', publicSiteName);
  upsertMetaByProperty('og:title', title);
  upsertMetaByProperty('og:description', description);
  upsertMetaByProperty('og:url', canonicalUrl);
  upsertMetaByProperty('og:type', head.type ?? 'website');
  if (head.imageUrl) {
    upsertMetaByProperty('og:image', head.imageUrl);
  } else {
    removeMetaByProperty('og:image');
  }
  upsertMetaByName('twitter:card', head.imageUrl ? 'summary_large_image' : 'summary');
  upsertMetaByName('twitter:title', title);
  upsertMetaByName('twitter:description', description);
  if (head.imageUrl) {
    upsertMetaByName('twitter:image', head.imageUrl);
  } else {
    removeMetaByName('twitter:image');
  }
  upsertCanonicalLink(canonicalUrl);
}

/**
 * PublicEntry 离开时恢复站点级基线，避免公开路由的 canonical 和分享信息残留到私域页。
 */
export function resetPublicHead(): void {
  if (typeof document === 'undefined') {
    return;
  }

  document.title = publicSiteName;
  upsertMetaByName('description', publicDefaultDescription);
  upsertMetaByProperty('og:site_name', publicSiteName);
  upsertMetaByProperty('og:title', publicSiteName);
  upsertMetaByProperty('og:description', publicDefaultDescription);
  upsertMetaByProperty('og:type', 'website');
  removeMetaByProperty('og:url');
  removeMetaByProperty('og:image');
  removeMetaByName('twitter:card');
  removeMetaByName('twitter:title');
  removeMetaByName('twitter:description');
  removeMetaByName('twitter:image');
  document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]')?.remove();
}
