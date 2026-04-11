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

export function parsePublicDocsRoute(pathname: string, hash: string): PublicDocsRoute {
  if (pathname === '/docs' || pathname === '/docs/') {
    return createDefaultDocsListRoute();
  }

  const matched = pathname.match(/^\/docs\/([^/?#]+)\/?$/);
  const slug = normalizeSlug(matched?.[1]);
  if (slug) {
    return {
      kind: 'detail',
      slug,
      anchor: normalizeAnchor(hash)
    };
  }

  return createDefaultDocsListRoute();
}

export function buildPublicDocsPath(route: PublicDocsRoute): string {
  if (route.kind === 'list') {
    return '/docs';
  }

  const basePath = `/docs/${encodeURIComponent(route.slug)}`;
  return route.anchor ? `${basePath}#${encodeURIComponent(route.anchor)}` : basePath;
}
