export type PublicListSort = 'newest' | 'hottest';
export type PublicSearchTimeRange = 'all' | '24h' | '7d' | '30d' | 'custom';

export interface PublicForumListRoute {
  kind: 'list';
  categoryId: number | null;
  sortBy: PublicListSort;
  page: number;
}

export interface PublicForumSearchRoute {
  kind: 'search';
  keyword: string;
  sortBy: PublicListSort;
  timeRange: PublicSearchTimeRange;
  startDate?: string;
  endDate?: string;
  page: number;
}

export interface PublicForumDetailRoute {
  kind: 'detail';
  postId: string;
}

export type PublicForumBrowseRoute = PublicForumListRoute | PublicForumSearchRoute;
export type PublicForumRoute = PublicForumBrowseRoute | PublicForumDetailRoute;

export function createDefaultListRoute(): PublicForumListRoute {
  return {
    kind: 'list',
    categoryId: null,
    sortBy: 'newest',
    page: 1
  };
}

export function createDefaultSearchRoute(): PublicForumSearchRoute {
  return {
    kind: 'search',
    keyword: '',
    sortBy: 'newest',
    timeRange: 'all',
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

function normalizeKeyword(value: string | null): string {
  return value?.trim() || '';
}

function normalizeTimeRange(value: string | null): PublicSearchTimeRange {
  return value === '24h'
    || value === '7d'
    || value === '30d'
    || value === 'custom'
    ? value
    : 'all';
}

function normalizeDateInput(value: string | null): string | undefined {
  if (!value) {
    return undefined;
  }

  const normalized = value.trim();
  return /^\d{4}-\d{2}-\d{2}$/.test(normalized) ? normalized : undefined;
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

function parseSearchRoute(search: string): PublicForumSearchRoute {
  const params = new URLSearchParams(search);
  const timeRange = normalizeTimeRange(params.get('range'));

  return {
    kind: 'search',
    keyword: normalizeKeyword(params.get('q')),
    sortBy: normalizeSortBy(params.get('sort')),
    timeRange,
    startDate: timeRange === 'custom' ? normalizeDateInput(params.get('start')) : undefined,
    endDate: timeRange === 'custom' ? normalizeDateInput(params.get('end')) : undefined,
    page: normalizePositiveInteger(params.get('page') ?? undefined) ?? 1
  };
}

export function parsePublicForumRoute(pathname: string, search: string): PublicForumRoute {
  if (pathname === '/forum' || pathname === '/forum/') {
    return parseListRoute(search);
  }

  if (pathname === '/forum/search' || pathname === '/forum/search/') {
    return parseSearchRoute(search);
  }

  const categoryMatched = pathname.match(/^\/forum\/category\/(\d+)\/?$/);
  const categoryId = normalizePositiveInteger(categoryMatched?.[1]);
  if (categoryId) {
    const route = parseListRoute(search);
    return {
      ...route,
      categoryId
    };
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

  if (route.kind === 'search') {
    const search = new URLSearchParams();
    if (route.keyword) {
      search.set('q', route.keyword);
    }
    if (route.sortBy !== 'newest') {
      search.set('sort', route.sortBy);
    }
    if (route.timeRange !== 'all') {
      search.set('range', route.timeRange);
    }
    if (route.timeRange === 'custom') {
      if (route.startDate) {
        search.set('start', route.startDate);
      }
      if (route.endDate) {
        search.set('end', route.endDate);
      }
    }
    if (route.page > 1) {
      search.set('page', String(route.page));
    }

    const queryString = search.toString();
    return queryString ? `/forum/search?${queryString}` : '/forum/search';
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
  const basePath = route.categoryId ? `/forum/category/${route.categoryId}` : '/forum';
  return queryString ? `${basePath}?${queryString}` : basePath;
}
