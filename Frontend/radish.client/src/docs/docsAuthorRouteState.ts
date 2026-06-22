import type { LongId } from '@/api/user';

export const DOCS_AUTHOR_MINE_PATH = '/docs/mine';
export const DOCS_AUTHOR_COMPOSE_PATH = '/docs/compose';
export const DOCS_AUTHOR_EDIT_PREFIX = '/docs/edit';
export const DOCS_AUTHOR_REVISIONS_PREFIX = '/docs/revisions';

export interface DocsAuthorMineRoute {
  kind: 'mine';
}

export interface DocsAuthorComposeRoute {
  kind: 'compose';
}

export interface DocsAuthorEditRoute {
  kind: 'edit';
  documentId: LongId;
}

export interface DocsAuthorRevisionsRoute {
  kind: 'revisions';
  documentId: LongId;
}

export type DocsAuthorRoute =
  | DocsAuthorMineRoute
  | DocsAuthorComposeRoute
  | DocsAuthorEditRoute
  | DocsAuthorRevisionsRoute;

const POSITIVE_LONG_ID_PATTERN = /^[1-9]\d*$/;

function normalizePathname(pathname: string): string {
  return pathname.endsWith('/') && pathname !== '/'
    ? pathname.slice(0, -1)
    : pathname;
}

function normalizeDocumentId(value: string | undefined): LongId | null {
  if (!value) {
    return null;
  }

  let decoded = '';
  try {
    decoded = decodeURIComponent(value).trim();
  } catch {
    return null;
  }

  return POSITIVE_LONG_ID_PATTERN.test(decoded) ? decoded : null;
}

export function createDefaultDocsAuthorRoute(): DocsAuthorMineRoute {
  return { kind: 'mine' };
}

export function parseDocsAuthorRoute(pathname: string): DocsAuthorRoute | null {
  const normalizedPathname = normalizePathname(pathname);

  if (normalizedPathname === DOCS_AUTHOR_MINE_PATH) {
    return createDefaultDocsAuthorRoute();
  }

  if (normalizedPathname === DOCS_AUTHOR_COMPOSE_PATH) {
    return { kind: 'compose' };
  }

  const editMatched = normalizedPathname.match(/^\/docs\/edit\/([^/]+)$/);
  if (editMatched) {
    const documentId = normalizeDocumentId(editMatched[1]);
    return documentId ? { kind: 'edit', documentId } : createDefaultDocsAuthorRoute();
  }

  const revisionsMatched = normalizedPathname.match(/^\/docs\/revisions\/([^/]+)$/);
  if (revisionsMatched) {
    const documentId = normalizeDocumentId(revisionsMatched[1]);
    return documentId ? { kind: 'revisions', documentId } : createDefaultDocsAuthorRoute();
  }

  if (
    normalizedPathname === DOCS_AUTHOR_EDIT_PREFIX
    || normalizedPathname.startsWith(`${DOCS_AUTHOR_EDIT_PREFIX}/`)
    || normalizedPathname === DOCS_AUTHOR_REVISIONS_PREFIX
    || normalizedPathname.startsWith(`${DOCS_AUTHOR_REVISIONS_PREFIX}/`)
  ) {
    return createDefaultDocsAuthorRoute();
  }

  return null;
}

export function isDocsAuthorPathname(pathname: string): boolean {
  return parseDocsAuthorRoute(pathname) !== null;
}

export function buildDocsAuthorPath(route: DocsAuthorRoute): string {
  if (route.kind === 'compose') {
    return DOCS_AUTHOR_COMPOSE_PATH;
  }

  if (route.kind === 'edit') {
    return `${DOCS_AUTHOR_EDIT_PREFIX}/${encodeURIComponent(String(route.documentId))}`;
  }

  if (route.kind === 'revisions') {
    return `${DOCS_AUTHOR_REVISIONS_PREFIX}/${encodeURIComponent(String(route.documentId))}`;
  }

  return DOCS_AUTHOR_MINE_PATH;
}
