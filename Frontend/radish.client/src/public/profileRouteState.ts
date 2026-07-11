export type PublicProfileTab = 'posts' | 'comments';
export type PublicProfileIntent = 'follow';

export interface PublicProfileRoute {
  kind: 'detail';
  userId: string;
  tab: PublicProfileTab;
  page: number;
  intent?: PublicProfileIntent;
}

export function createDefaultPublicProfileRoute(userId: string): PublicProfileRoute {
  return {
    kind: 'detail',
    userId,
    tab: 'posts',
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

function normalizeProfileIdentifier(value: string | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  let normalized: string;
  try {
    normalized = decodeURIComponent(value).trim();
  } catch {
    return undefined;
  }
  if (/^[1-9]\d*$/.test(normalized)) {
    return normalized;
  }

  if (/^usr_[0-9a-f]{32}$/i.test(normalized)) {
    return normalized.toLowerCase();
  }

  return undefined;
}

function normalizeTab(value: string | null): PublicProfileTab {
  return value === 'comments' ? 'comments' : 'posts';
}

function normalizeIntent(params: URLSearchParams): PublicProfileIntent | undefined {
  return params.getAll('intent').length === 1 && params.get('intent') === 'follow'
    ? 'follow'
    : undefined;
}

export function parsePublicProfileRoute(pathname: string, search: string): PublicProfileRoute | null {
  const matched = pathname.match(/^\/u\/([^/?#]+)\/?$/);
  const userId = normalizeProfileIdentifier(matched?.[1]);
  if (!userId) {
    return null;
  }

  const params = new URLSearchParams(search);
  const intent = normalizeIntent(params);
  return {
    kind: 'detail',
    userId,
    tab: normalizeTab(params.get('tab')),
    page: normalizePositiveInteger(params.get('page') ?? undefined) ?? 1,
    ...(intent ? { intent } : {}),
  };
}

export function buildPublicProfilePath(route: PublicProfileRoute): string {
  const search = new URLSearchParams();
  if (route.tab !== 'posts') {
    search.set('tab', route.tab);
  }
  if (route.page > 1) {
    search.set('page', String(route.page));
  }
  if (route.intent === 'follow') {
    search.set('intent', route.intent);
  }

  const query = search.toString();
  const encodedUserId = encodeURIComponent(route.userId);
  return query ? `/u/${encodedUserId}?${query}` : `/u/${encodedUserId}`;
}
