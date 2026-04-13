export interface PublicDiscoverRoute {
  kind: 'home';
}

export function createDefaultPublicDiscoverRoute(): PublicDiscoverRoute {
  return {
    kind: 'home'
  };
}

export function parsePublicDiscoverRoute(pathname: string): PublicDiscoverRoute | null {
  if (pathname === '/discover' || pathname === '/discover/') {
    return createDefaultPublicDiscoverRoute();
  }

  return null;
}

export function buildPublicDiscoverPath(route: PublicDiscoverRoute): string {
  void route;
  return '/discover';
}
