export const CIRCLE_ENTRY_PATH = '/circle';

export type CircleTab = 'feed' | 'following' | 'followers';

export interface CircleRoute {
  tab: CircleTab;
  page: number;
}

export function createDefaultCircleRoute(): CircleRoute {
  return {
    tab: 'feed',
    page: 1
  };
}

export function isCirclePathname(pathname: string): boolean {
  return pathname === CIRCLE_ENTRY_PATH || pathname === `${CIRCLE_ENTRY_PATH}/`;
}

function normalizeCircleTab(value: string | null): CircleTab {
  return value === 'following' || value === 'followers' ? value : 'feed';
}

function normalizePositivePage(value: string | null): number {
  if (!value) {
    return 1;
  }

  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : 1;
}

export function parseCircleRoute(pathname: string, search: string): CircleRoute | null {
  if (!isCirclePathname(pathname)) {
    return null;
  }

  const query = new URLSearchParams(search);
  return {
    tab: normalizeCircleTab(query.get('tab')),
    page: normalizePositivePage(query.get('page'))
  };
}

export function buildCirclePath(route: CircleRoute): string {
  const search = new URLSearchParams();
  if (route.tab !== 'feed') {
    search.set('tab', route.tab);
  }
  if (route.page > 1) {
    search.set('page', String(route.page));
  }

  const query = search.toString();
  return query ? `${CIRCLE_ENTRY_PATH}?${query}` : CIRCLE_ENTRY_PATH;
}
