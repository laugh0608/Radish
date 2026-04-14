export type PublicListSort = 'newest' | 'hottest';
export type PublicForumRouteSort = 'newest' | 'hottest' | 'pending' | 'answers' | 'votes' | 'deadline';
export type PublicSearchTimeRange = 'all' | '24h' | '7d' | '30d' | 'custom';

export interface PublicForumListRoute {
  kind: 'list';
  categoryId: number | null;
  sortBy: PublicListSort;
  page: number;
}

export interface PublicForumTagRoute {
  kind: 'tag';
  tagSlug: string;
  sortBy: PublicListSort;
  page: number;
}

export interface PublicForumTypeRoute {
  kind: 'question' | 'poll' | 'lottery';
  sortBy: PublicForumRouteSort;
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
  commentId?: string;
}

export type PublicForumBrowseRoute =
  | PublicForumListRoute
  | PublicForumTagRoute
  | PublicForumSearchRoute
  | PublicForumTypeRoute;
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

export function createDefaultTypeRoute(kind: PublicForumTypeRoute['kind']): PublicForumTypeRoute {
  return {
    kind,
    sortBy: 'newest',
    page: 1
  };
}

function normalizeTagSlug(value: string | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  const normalized = value.trim().toLowerCase();
  return normalized.length > 0 ? normalized : undefined;
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

function normalizeTypeSort(kind: PublicForumTypeRoute['kind'], value: string | null): PublicForumRouteSort {
  if (kind === 'question') {
    return value === 'pending' || value === 'answers' ? value : 'newest';
  }

  if (kind === 'poll') {
    return value === 'hottest' || value === 'votes' || value === 'deadline' ? value : 'newest';
  }

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

function parseTypeRoute(kind: PublicForumTypeRoute['kind'], search: string): PublicForumTypeRoute {
  const params = new URLSearchParams(search);
  return {
    kind,
    sortBy: normalizeTypeSort(kind, params.get('sort')),
    page: normalizePositiveInteger(params.get('page') ?? undefined) ?? 1
  };
}

export function parsePublicForumRoute(pathname: string, search: string): PublicForumRoute {
  if (pathname === '/forum' || pathname === '/forum/') {
    return parseListRoute(search);
  }

  if (pathname === '/forum/question' || pathname === '/forum/question/') {
    return parseTypeRoute('question', search);
  }

  if (pathname === '/forum/poll' || pathname === '/forum/poll/') {
    return parseTypeRoute('poll', search);
  }

  if (pathname === '/forum/lottery' || pathname === '/forum/lottery/') {
    return parseTypeRoute('lottery', search);
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

  const tagMatched = pathname.match(/^\/forum\/tag\/([^/?#]+)\/?$/);
  const tagSlug = normalizeTagSlug(tagMatched?.[1] ? decodeURIComponent(tagMatched[1]) : undefined);
  if (tagSlug) {
    const params = new URLSearchParams(search);
    return {
      kind: 'tag',
      tagSlug,
      sortBy: normalizeSortBy(params.get('sort')),
      page: normalizePositiveInteger(params.get('page') ?? undefined) ?? 1
    };
  }

  const matched = pathname.match(/^\/forum\/post\/(\d+)\/?$/);
  const postId = normalizePositiveIntegerString(matched?.[1]);
  if (postId) {
    const params = new URLSearchParams(search);
    const commentId = normalizePositiveIntegerString(params.get('commentId') ?? undefined);
    return commentId
      ? { kind: 'detail', postId, commentId }
      : { kind: 'detail', postId };
  }

  return parseListRoute(search);
}

export function buildPublicForumPath(route: PublicForumRoute): string {
  if (route.kind === 'detail') {
    const search = new URLSearchParams();
    if (route.commentId) {
      search.set('commentId', route.commentId);
    }

    const queryString = search.toString();
    const basePath = `/forum/post/${route.postId}`;
    return queryString ? `${basePath}?${queryString}` : basePath;
  }

  if (route.kind === 'question' || route.kind === 'poll' || route.kind === 'lottery') {
    const search = new URLSearchParams();
    if (route.sortBy !== 'newest') {
      search.set('sort', route.sortBy);
    }
    if (route.page > 1) {
      search.set('page', String(route.page));
    }

    const queryString = search.toString();
    const basePath = `/forum/${route.kind}`;
    return queryString ? `${basePath}?${queryString}` : basePath;
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

  if (route.kind === 'tag') {
    const search = new URLSearchParams();
    if (route.sortBy !== 'newest') {
      search.set('sort', route.sortBy);
    }
    if (route.page > 1) {
      search.set('page', String(route.page));
    }

    const queryString = search.toString();
    const basePath = `/forum/tag/${encodeURIComponent(route.tagSlug)}`;
    return queryString ? `${basePath}?${queryString}` : basePath;
  }

  if (route.kind === 'list') {
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

  return '/forum';
}
