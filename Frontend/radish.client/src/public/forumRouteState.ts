export type PublicListSort = 'newest' | 'hottest';

export interface PublicForumListRoute {
  kind: 'list';
  categoryId: number | null;
  sortBy: PublicListSort;
  page: number;
}

export interface PublicForumDetailRoute {
  kind: 'detail';
  postId: string;
}

export type PublicForumRoute = PublicForumListRoute | PublicForumDetailRoute;

export function createDefaultListRoute(): PublicForumListRoute {
  return {
    kind: 'list',
    categoryId: null,
    sortBy: 'newest',
    page: 1
  };
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

function normalizePositiveIntegerString(value: string | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  const normalized = value.trim();
  if (!/^[1-9]\d*$/.test(normalized)) {
    return undefined;
  }

  return normalized;
}

function normalizeSortBy(value: string | null): PublicListSort {
  return value === 'hottest' ? 'hottest' : 'newest';
}

function parseListRoute(search: string): PublicForumListRoute {
  const params = new URLSearchParams(search);
  return {
    kind: 'list',
    categoryId: normalizePositiveInteger(params.get('category') ?? undefined) ?? null,
    sortBy: normalizeSortBy(params.get('sort')),
    page: normalizePositiveInteger(params.get('page') ?? undefined) ?? 1
  };
}

export function parsePublicForumRoute(pathname: string, search: string): PublicForumRoute {
  if (pathname === '/forum' || pathname === '/forum/') {
    return parseListRoute(search);
  }

  const matched = pathname.match(/^\/forum\/post\/(\d+)\/?$/);
  const postId = normalizePositiveIntegerString(matched?.[1]);
  if (postId) {
    return { kind: 'detail', postId };
  }

  return parseListRoute(search);
}

export function buildPublicForumPath(route: PublicForumRoute): string {
  if (route.kind === 'detail') {
    return `/forum/post/${route.postId}`;
  }

  const search = new URLSearchParams();
  if (route.categoryId) {
    search.set('category', String(route.categoryId));
  }
  if (route.sortBy !== 'newest') {
    search.set('sort', route.sortBy);
  }
  if (route.page > 1) {
    search.set('page', String(route.page));
  }

  const queryString = search.toString();
  return queryString ? `/forum?${queryString}` : '/forum';
}
