export interface PublicDiscoverRoute {
  kind: 'home';
}

export function createDefaultPublicDiscoverRoute(): PublicDiscoverRoute {
  return {
    kind: 'home'
  };
}

export function isPublicDiscoverPathname(pathname: string): boolean {
  return pathname === '/discover' || pathname === '/discover/';
}

export function parsePublicDiscoverRoute(pathname: string): PublicDiscoverRoute | null {
  if (isPublicDiscoverPathname(pathname)) {
    return createDefaultPublicDiscoverRoute();
  }

  return null;
}

export function buildPublicDiscoverPath(route: PublicDiscoverRoute): string {
  void route;
  return '/discover';
}
