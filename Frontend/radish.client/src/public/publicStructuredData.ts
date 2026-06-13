import type { PostDetail } from '@/api/forum';
import type { Product } from '@/api/shop';
import type { PublicUserProfile, PublicUserStats } from '@/api/user';
import type { WikiDocumentDetailVo } from '@/apps/wiki/types/wiki';
import {
  buildPublicCanonicalUrl,
  buildPublicRouteHead,
  publicDefaultDescription,
  publicSiteName,
} from './publicHead.ts';
import type { PublicContentRouteDescriptor } from './publicRouteNavigation.ts';

export const publicStructuredDataScriptId = 'radish-public-structured-data';

type JsonLdValue = string | number | boolean | null | JsonLdObject | JsonLdValue[];

export interface JsonLdObject {
  [key: string]: JsonLdValue | undefined;
}

interface PublicStructuredDataOptions {
  canonicalPath: string;
  origin?: string;
}

interface BuildForumPostStructuredDataOptions extends PublicStructuredDataOptions {
  post: PostDetail;
}

interface BuildDocsStructuredDataOptions extends PublicStructuredDataOptions {
  document: WikiDocumentDetailVo;
}

interface BuildShopProductStructuredDataOptions extends PublicStructuredDataOptions {
  product: Product;
  imageUrl?: string | null;
}

interface BuildProfileStructuredDataOptions extends PublicStructuredDataOptions {
  profile: PublicUserProfile;
  stats?: PublicUserStats | null;
  imageUrl?: string | null;
}

function normalizeText(value: string | number | null | undefined): string | undefined {
  const normalized = String(value ?? '').replace(/\s+/g, ' ').trim();
  return normalized || undefined;
}

function stripMarkdown(value: string | null | undefined): string | undefined {
  const normalized = normalizeText(
    value
      ?.replace(/```[\s\S]*?```/g, ' ')
      .replace(/`([^`]+)`/g, '$1')
      .replace(/!\[[^\]]*]\([^)]*\)/g, ' ')
      .replace(/\[([^\]]+)]\([^)]*\)/g, '$1')
      .replace(/[#>*_~-]+/g, ' ')
  );
  return normalized;
}

function truncateText(value: string | undefined, maxLength: number): string | undefined {
  if (!value || value.length <= maxLength) {
    return value;
  }

  return `${value.slice(0, maxLength - 1).trimEnd()}…`;
}

function toCanonicalUrl(options: PublicStructuredDataOptions): string {
  return buildPublicCanonicalUrl(options.canonicalPath, options.origin);
}

function buildOrganization(): JsonLdObject {
  return {
    '@type': 'Organization',
    name: publicSiteName,
  };
}

function removeUndefinedValues(value: JsonLdValue | undefined): JsonLdValue | undefined {
  if (value === undefined) {
    return undefined;
  }

  if (Array.isArray(value)) {
    const items = value
      .map(removeUndefinedValues)
      .filter((item): item is JsonLdValue => item !== undefined);
    return items.length > 0 ? items : undefined;
  }

  if (typeof value === 'object' && value !== null) {
    const entries = Object.entries(value)
      .map(([key, entryValue]) => [key, removeUndefinedValues(entryValue)] as const)
      .filter(([, entryValue]) => entryValue !== undefined);
    return entries.length > 0 ? Object.fromEntries(entries) as JsonLdObject : undefined;
  }

  return value;
}

function withContext(data: JsonLdObject): JsonLdObject {
  const normalized = removeUndefinedValues({
    '@context': 'https://schema.org',
    ...data,
  });
  return normalized && typeof normalized === 'object' && !Array.isArray(normalized)
    ? normalized
    : { '@context': 'https://schema.org' };
}

export function buildForumPostStructuredData(options: BuildForumPostStructuredDataOptions): JsonLdObject {
  const description = truncateText(
    normalizeText(options.post.voSummary) ?? stripMarkdown(options.post.voContent) ?? publicDefaultDescription,
    240
  );
  const canonicalUrl = toCanonicalUrl(options);

  return withContext({
    '@type': 'BlogPosting',
    headline: normalizeText(options.post.voTitle),
    description,
    articleBody: truncateText(stripMarkdown(options.post.voContent), 1200),
    url: canonicalUrl,
    mainEntityOfPage: canonicalUrl,
    datePublished: normalizeText(options.post.voCreateTime),
    dateModified: normalizeText(options.post.voUpdateTime ?? options.post.voCreateTime),
    author: {
      '@type': 'Person',
      name: normalizeText(options.post.voAuthorName) ?? 'Radish 用户',
    },
    publisher: buildOrganization(),
    image: normalizeText(options.post.voCoverImage),
    keywords: options.post.voTagNames,
    interactionStatistic: [
      {
        '@type': 'InteractionCounter',
        interactionType: { '@type': 'ViewAction' },
        userInteractionCount: options.post.voViewCount,
      },
      {
        '@type': 'InteractionCounter',
        interactionType: { '@type': 'CommentAction' },
        userInteractionCount: options.post.voCommentCount,
      },
    ],
  });
}

export function buildDocsArticleStructuredData(options: BuildDocsStructuredDataOptions): JsonLdObject {
  const description = truncateText(
    normalizeText(options.document.voSummary) ?? stripMarkdown(options.document.voMarkdownContent) ?? publicDefaultDescription,
    240
  );
  const canonicalUrl = toCanonicalUrl(options);

  return withContext({
    '@type': 'Article',
    headline: normalizeText(options.document.voTitle),
    description,
    articleBody: truncateText(stripMarkdown(options.document.voMarkdownContent), 1200),
    url: canonicalUrl,
    mainEntityOfPage: canonicalUrl,
    datePublished: normalizeText(options.document.voPublishedAt ?? options.document.voCreateTime),
    dateModified: normalizeText(options.document.voModifyTime ?? options.document.voPublishedAt ?? options.document.voCreateTime),
    publisher: buildOrganization(),
  });
}

export function buildShopProductStructuredData(options: BuildShopProductStructuredDataOptions): JsonLdObject {
  const description = truncateText(
    normalizeText(options.product.voDescription) ?? `${options.product.voName} - Radish 商城商品`,
    240
  );
  const canonicalUrl = toCanonicalUrl(options);

  return withContext({
    '@type': 'Product',
    name: normalizeText(options.product.voName),
    description,
    image: normalizeText(options.imageUrl ?? options.product.voCoverImage ?? options.product.voIcon),
    url: canonicalUrl,
    category: normalizeText(options.product.voCategoryName),
    brand: buildOrganization(),
  });
}

export function buildProfilePageStructuredData(options: BuildProfileStructuredDataOptions): JsonLdObject {
  const displayName = normalizeText(options.profile.voDisplayName) ?? normalizeText(options.profile.voUserName) ?? 'Radish 用户';
  const canonicalUrl = toCanonicalUrl(options);

  return withContext({
    '@type': 'ProfilePage',
    name: `${displayName} - Radish 用户公开主页`,
    url: canonicalUrl,
    dateCreated: normalizeText(options.profile.voCreateTime),
    mainEntity: {
      '@type': 'Person',
      name: displayName,
      alternateName: normalizeText(options.profile.voUserName),
      image: normalizeText(options.imageUrl),
      interactionStatistic: [
        {
          '@type': 'InteractionCounter',
          interactionType: { '@type': 'WriteAction' },
          userInteractionCount: options.stats?.voPostCount,
        },
        {
          '@type': 'InteractionCounter',
          interactionType: { '@type': 'CommentAction' },
          userInteractionCount: options.stats?.voCommentCount,
        },
      ],
    },
  });
}

function isPublicRouteCollectionPage(route: PublicContentRouteDescriptor): boolean {
  if (route.app === 'discover' || route.app === 'leaderboard') {
    return true;
  }

  if (route.app === 'forum' || route.app === 'docs' || route.app === 'shop') {
    return route.route.kind !== 'detail';
  }

  return false;
}

export function buildPublicRouteStructuredData(route: PublicContentRouteDescriptor, origin?: string): JsonLdObject {
  const head = buildPublicRouteHead(route);
  const canonicalUrl = buildPublicCanonicalUrl(head.canonicalPath, origin);
  const pageType = isPublicRouteCollectionPage(route) ? 'CollectionPage' : 'WebPage';

  return withContext({
    '@type': pageType,
    name: normalizeText(head.title),
    description: normalizeText(head.description),
    url: canonicalUrl,
    mainEntityOfPage: canonicalUrl,
    isPartOf: {
      '@type': 'WebSite',
      name: publicSiteName,
      url: buildPublicCanonicalUrl('/', origin),
    },
    publisher: buildOrganization(),
  });
}

export function applyPublicStructuredData(data: JsonLdObject): void {
  if (typeof document === 'undefined') {
    return;
  }

  const existing = document.head.querySelector<HTMLScriptElement>(`#${publicStructuredDataScriptId}`);
  const element = existing ?? document.createElement('script');
  element.id = publicStructuredDataScriptId;
  element.type = 'application/ld+json';
  element.textContent = JSON.stringify(removeUndefinedValues(data) ?? data);
  if (!existing) {
    document.head.appendChild(element);
  }
}

export function removePublicStructuredData(): void {
  if (typeof document === 'undefined') {
    return;
  }

  document.head.querySelector(`#${publicStructuredDataScriptId}`)?.remove();
}
