export type PublicDiscoverSection = 'forum' | 'docs' | 'leaderboard' | 'shop';

export interface PublicDiscoverRoute {
  kind: 'home';
  section?: PublicDiscoverSection;
}

export function createDefaultPublicDiscoverRoute(): PublicDiscoverRoute {
  return {
    kind: 'home'
  };
}

function normalizeDiscoverSection(value: string | null): PublicDiscoverSection | undefined {
  if (value === 'forum' || value === 'docs' || value === 'leaderboard' || value === 'shop') {
    return value;
  }

  return undefined;
}

export function isPublicDiscoverPathname(pathname: string): boolean {
  return pathname === '/discover' || pathname === '/discover/';
}

export function parsePublicDiscoverRoute(pathname: string, search = ''): PublicDiscoverRoute | null {
  if (isPublicDiscoverPathname(pathname)) {
    const params = new URLSearchParams(search);
    const section = normalizeDiscoverSection(params.get('section'));
    return section
      ? {
          kind: 'home',
          section
        }
      : {
          kind: 'home'
        };
  }

  return null;
}

export function buildPublicDiscoverPath(route: PublicDiscoverRoute): string {
  const params = new URLSearchParams();
  if (route.section) {
    params.set('section', route.section);
  }

  const queryString = params.toString();
  return queryString ? `/discover?${queryString}` : '/discover';
}
