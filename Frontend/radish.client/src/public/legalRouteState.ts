export interface PublicLegalRoute {
  kind: 'index';
  anchor?: string;
}

function normalizeLegalAnchor(hash: string): string | undefined {
  const rawAnchor = hash.startsWith('#') ? hash.slice(1) : hash;
  if (!rawAnchor) {
    return undefined;
  }

  try {
    const decodedAnchor = decodeURIComponent(rawAnchor).trim();
    return /^[a-z0-9-]{1,64}$/i.test(decodedAnchor) ? decodedAnchor : undefined;
  } catch {
    return undefined;
  }
}

export function isPublicLegalPathname(pathname: string): boolean {
  return pathname === '/legal' || pathname === '/legal/';
}

export function parsePublicLegalRoute(pathname: string, hash = ''): PublicLegalRoute | null {
  if (!isPublicLegalPathname(pathname)) {
    return null;
  }

  return {
    kind: 'index',
    anchor: normalizeLegalAnchor(hash),
  };
}

export function buildPublicLegalPath(route: PublicLegalRoute = { kind: 'index' }): string {
  switch (route.kind) {
    case 'index':
      return route.anchor ? `/legal#${encodeURIComponent(route.anchor)}` : '/legal';
  }
}
