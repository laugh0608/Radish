export interface PublicDocsListRoute {
  kind: 'list';
}

export interface PublicDocsDetailRoute {
  kind: 'detail';
  slug: string;
  anchor?: string;
}

export type PublicDocsRoute = PublicDocsListRoute | PublicDocsDetailRoute;

export function createDefaultDocsListRoute(): PublicDocsListRoute {
  return {
    kind: 'list'
  };
}

function normalizeSlug(value: string | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  const normalized = decodeURIComponent(value).trim();
  if (!normalized || normalized.includes('/')) {
    return undefined;
  }

  return normalized;
}

function normalizeAnchor(value: string | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  const normalized = decodeURIComponent(value.replace(/^#/, '')).trim();
  return normalized || undefined;
}

function isPublicDocsListPath(pathname: string): boolean {
  return pathname === '/docs'
    || pathname === '/docs/'
    || pathname === '/__documents__'
    || pathname === '/__documents__/';
}

function parsePublicDocsDetailRoute(pathname: string, hash: string): PublicDocsDetailRoute | null {
  const matched = pathname.match(/^\/(?:docs|__documents__)\/([^/?#]+)\/?$/);
  const slug = normalizeSlug(matched?.[1]);
  if (!slug) {
    return null;
  }

  return {
    kind: 'detail',
    slug,
    anchor: normalizeAnchor(hash)
  };
}

export function parsePublicDocsRoute(pathname: string, hash: string): PublicDocsRoute {
  if (isPublicDocsListPath(pathname)) {
    return createDefaultDocsListRoute();
  }

  const detailRoute = parsePublicDocsDetailRoute(pathname, hash);
  if (detailRoute) {
    return detailRoute;
  }

  return createDefaultDocsListRoute();
}

export function resolvePublicDocsRouteFromHref(href: string, currentOrigin: string): PublicDocsRoute | null {
  const trimmedHref = href.trim();
  if (!trimmedHref) {
    return null;
  }

  try {
    const baseUrl = new URL(currentOrigin);
    const resolvedUrl = new URL(trimmedHref, baseUrl);
    if (resolvedUrl.origin !== baseUrl.origin) {
      return null;
    }

    if (isPublicDocsListPath(resolvedUrl.pathname)) {
      return createDefaultDocsListRoute();
    }

    return parsePublicDocsDetailRoute(resolvedUrl.pathname, resolvedUrl.hash);
  } catch {
    return null;
  }
}

export function rewritePublicDocsHref(href: string, currentOrigin: string): string | null {
  const route = resolvePublicDocsRouteFromHref(href, currentOrigin);
  if (!route) {
    return null;
  }

  return buildPublicDocsPath(route);
}

export function buildPublicDocsPath(route: PublicDocsRoute): string {
  if (route.kind === 'list') {
    return '/docs';
  }

  const basePath = `/docs/${encodeURIComponent(route.slug)}`;
  return route.anchor ? `${basePath}#${encodeURIComponent(route.anchor)}` : basePath;
}
