export interface PublicLegalRoute {
  kind: 'index';
}

export function isPublicLegalPathname(pathname: string): boolean {
  return pathname === '/legal' || pathname === '/legal/';
}

export function parsePublicLegalRoute(pathname: string): PublicLegalRoute | null {
  return isPublicLegalPathname(pathname) ? { kind: 'index' } : null;
}

export function buildPublicLegalPath(route: PublicLegalRoute = { kind: 'index' }): string {
  switch (route.kind) {
    case 'index':
      return '/legal';
  }
}
