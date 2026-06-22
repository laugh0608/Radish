export interface PublicDocsListRoute {
  kind: 'list';
}

export interface PublicDocsSearchRoute {
  kind: 'search';
  keyword: string;
  page: number;
}

export interface PublicDocsDetailRoute {
  kind: 'detail';
  slug: string;
  anchor?: string;
}

export type PublicDocsBrowseRoute = PublicDocsListRoute | PublicDocsSearchRoute;
export type PublicDocsRoute = PublicDocsBrowseRoute | PublicDocsDetailRoute;

const PUBLIC_DOCS_RESERVED_SLUGS = new Set(['search', 'mine', 'compose', 'edit', 'revisions']);

export function createDefaultDocsListRoute(): PublicDocsListRoute {
  return {
    kind: 'list'
  };
}

export function createDefaultDocsSearchRoute(): PublicDocsSearchRoute {
  return {
    kind: 'search',
    keyword: '',
    page: 1
  };
}

function safeDecodeURIComponent(value: string): string | undefined {
  try {
    return decodeURIComponent(value);
  } catch {
    return undefined;
  }
}

function normalizeSlug(value: string | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  const normalized = safeDecodeURIComponent(value)?.trim();
  if (!normalized || normalized.includes('/')) {
    return undefined;
  }

  return normalized;
}

function normalizeAnchor(value: string | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  const normalized = safeDecodeURIComponent(value.replace(/^#/, ''))?.trim();
  return normalized || undefined;
}

function normalizeKeyword(value: string | null): string {
  return value?.trim() || '';
}

function normalizePositiveInteger(value: string | undefined): number | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return undefined;
  }

  return parsed;
}

function isPublicDocsListPath(pathname: string): boolean {
  return pathname === '/docs'
    || pathname === '/docs/'
    || pathname === '/__documents__'
    || pathname === '/__documents__/';
}

function isPublicDocsSearchPath(pathname: string): boolean {
  return pathname === '/docs/search' || pathname === '/docs/search/';
}

function parsePublicDocsSearchRoute(search: string): PublicDocsSearchRoute {
  const params = new URLSearchParams(search);
  const keyword = normalizeKeyword(params.get('q'));

  return {
    kind: 'search',
    keyword,
    page: keyword
      ? (normalizePositiveInteger(params.get('page') ?? undefined) ?? 1)
      : 1
  };
}

function normalizeDocsRouteArgs(searchOrHash = '', hash = ''): { search: string; hash: string } {
  if (!hash && searchOrHash.startsWith('#')) {
    return {
      search: '',
      hash: searchOrHash
    };
  }

  return {
    search: searchOrHash,
    hash
  };
}

function parsePublicDocsDetailRoute(pathname: string, hash: string): PublicDocsDetailRoute | null {
  const matched = pathname.match(/^\/(?:docs|__documents__)\/([^/?#]+)\/?$/);
  const slug = normalizeSlug(matched?.[1]);
  if (!slug) {
    return null;
  }

  if (pathname.startsWith('/docs/') && PUBLIC_DOCS_RESERVED_SLUGS.has(slug.trim().toLowerCase())) {
    return null;
  }

  return {
    kind: 'detail',
    slug,
    anchor: normalizeAnchor(hash)
  };
}

export function parsePublicDocsRoute(pathname: string, searchOrHash = '', hash = ''): PublicDocsRoute {
  const normalizedArgs = normalizeDocsRouteArgs(searchOrHash, hash);

  if (isPublicDocsListPath(pathname)) {
    return createDefaultDocsListRoute();
  }

  if (isPublicDocsSearchPath(pathname)) {
    return parsePublicDocsSearchRoute(normalizedArgs.search);
  }

  const detailRoute = parsePublicDocsDetailRoute(pathname, normalizedArgs.hash);
  if (detailRoute) {
    return detailRoute;
  }

  return createDefaultDocsListRoute();
}

export function resolvePublicDocsRouteFromHref(
  href: string,
  currentOrigin: string,
  currentPublicPath = '/'
): PublicDocsRoute | null {
  const trimmedHref = href.trim();
  if (!trimmedHref) {
    return null;
  }

  try {
    const baseUrl = new URL(currentOrigin);
    const currentUrl = new URL(currentPublicPath, baseUrl);
    const resolvedUrl = new URL(trimmedHref, currentUrl);
    if (resolvedUrl.origin !== baseUrl.origin) {
      return null;
    }

    if (isPublicDocsListPath(resolvedUrl.pathname)) {
      return createDefaultDocsListRoute();
    }

    if (isPublicDocsSearchPath(resolvedUrl.pathname)) {
      return parsePublicDocsSearchRoute(resolvedUrl.search);
    }

    return parsePublicDocsDetailRoute(resolvedUrl.pathname, resolvedUrl.hash);
  } catch {
    return null;
  }
}

export function rewritePublicDocsHref(
  href: string,
  currentOrigin: string,
  currentPublicPath = '/'
): string | null {
  const route = resolvePublicDocsRouteFromHref(href, currentOrigin, currentPublicPath);
  if (!route) {
    return null;
  }

  return buildPublicDocsPath(route);
}

export function buildPublicDocsPath(route: PublicDocsRoute): string {
  if (route.kind === 'list') {
    return '/docs';
  }

  if (route.kind === 'search') {
    const params = new URLSearchParams();
    if (route.keyword) {
      params.set('q', route.keyword);
    }
    if (route.keyword && route.page > 1) {
      params.set('page', String(route.page));
    }

    const queryString = params.toString();
    return queryString ? `/docs/search?${queryString}` : '/docs/search';
  }

  const basePath = PUBLIC_DOCS_RESERVED_SLUGS.has(route.slug.trim().toLowerCase())
    ? `/__documents__/${encodeURIComponent(route.slug)}`
    : `/docs/${encodeURIComponent(route.slug)}`;
  return route.anchor ? `${basePath}#${encodeURIComponent(route.anchor)}` : basePath;
}
