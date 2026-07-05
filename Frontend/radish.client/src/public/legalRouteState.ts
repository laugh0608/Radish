export interface PublicLegalRoute {
  kind: 'index';
}

export function isPublicLegalPathname(pathname: string): boolean {
  return pathname === '/legal' || pathname === '/legal/';
}

export function parsePublicLegalRoute(pathname: string): PublicLegalRoute | null {
  return isPublicLegalPathname(pathname) ? { kind: 'index' } : null;
}

export function buildPublicLegalPath(_route: PublicLegalRoute = { kind: 'index' }): string {
  return '/legal';
}
