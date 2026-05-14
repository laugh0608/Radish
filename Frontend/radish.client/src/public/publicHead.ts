import { buildPublicDiscoverPath } from './discoverRouteState.ts';
import { buildPublicDocsPath } from './docsRouteState.ts';
import { buildPublicForumPath } from './forumRouteState.ts';
import {
  buildPublicLeaderboardPath,
  getPublicLeaderboardRouteDefinitionBySlug,
} from './leaderboardRouteState.ts';
import { buildPublicProfilePath } from './profileRouteState.ts';
import { buildPublicShopPath } from './shopRouteState.ts';
import type { PublicRouteDescriptor } from './publicRouteNavigation.ts';

export const publicSiteName = 'Radish';
export const publicDefaultOrigin = 'https://radishx.com';
export const publicDefaultDescription = 'Radish 是面向公开阅读、社区互动与桌面工作台的现代社区平台。';

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

function resolveRuntimeOrigin(origin?: string): string {
  if (origin) {
    return normalizeOrigin(origin);
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

export function buildPublicCanonicalUrl(canonicalPath: string, origin?: string): string {
  const baseOrigin = normalizeOrigin(origin);
  return new URL(stripHash(canonicalPath), baseOrigin).toString();
}

function buildDiscoverHead(route: PublicRouteDescriptor & { app: 'discover' }): PublicHeadDescriptor {
  return {
    title: `社区发现 - ${publicSiteName}`,
    description: '浏览 Radish 的公开内容、社区动态、文档入口、榜单与商城推荐。',
    canonicalPath: buildPublicDiscoverPath(route.route),
  };
}

function buildForumHead(route: PublicRouteDescriptor & { app: 'forum' }): PublicHeadDescriptor {
  const canonicalPath = buildPublicForumPath(route.route);
  if (route.route.kind === 'detail') {
    const postLabel = route.route.postPublicId ?? route.route.postId;
    return {
      title: `帖子 ${postLabel} - Radish 论坛`,
      description: `阅读 Radish 公开论坛帖子 ${postLabel}，查看讨论内容与社区互动。`,
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
      title: route.route.categoryId ? `论坛分类 ${route.route.categoryId} - ${publicSiteName}` : `论坛 - ${publicSiteName}`,
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

function buildDocsHead(route: PublicRouteDescriptor & { app: 'docs' }): PublicHeadDescriptor {
  const canonicalPath = buildPublicDocsPath(route.route);
  if (route.route.kind === 'detail') {
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

function buildProfileHead(route: PublicRouteDescriptor & { app: 'profile' }): PublicHeadDescriptor {
  return {
    title: `用户 ${route.route.userId} 的公开主页 - ${publicSiteName}`,
    description: `查看 Radish 用户 ${route.route.userId} 的公开主页、帖子与评论记录。`,
    canonicalPath: buildPublicProfilePath(route.route),
    type: 'profile',
  };
}

function buildLeaderboardHead(route: PublicRouteDescriptor & { app: 'leaderboard' }): PublicHeadDescriptor {
  const definition = getPublicLeaderboardRouteDefinitionBySlug(route.route.typeSlug);
  return {
    title: `${definition.slug} 榜单 - ${publicSiteName}`,
    description: '查看 Radish 公开榜单，了解社区用户、商品与内容的活跃排行。',
    canonicalPath: buildPublicLeaderboardPath(route.route),
  };
}

function buildShopHead(route: PublicRouteDescriptor & { app: 'shop' }): PublicHeadDescriptor {
  const canonicalPath = buildPublicShopPath(route.route);
  if (route.route.kind === 'detail') {
    return {
      title: `商品 ${route.route.productId} - Radish 商城`,
      description: `查看 Radish 商城公开商品 ${route.route.productId} 的详情与兑换信息。`,
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

export function buildPublicRouteHead(route: PublicRouteDescriptor): PublicHeadDescriptor {
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

  return buildShopHead(route);
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
  }
  upsertCanonicalLink(canonicalUrl);
}
