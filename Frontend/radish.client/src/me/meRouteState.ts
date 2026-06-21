export const ME_ENTRY_PATH = '/me';
export const ME_ASSETS_PATH = '/me/assets';
export const ME_ASSET_TRANSACTIONS_PATH = '/me/assets/transactions';

export interface MeDashboardRoute {
  kind: 'dashboard';
}

export interface MeAssetsRoute {
  kind: 'assets';
}

export interface MeAssetTransactionsRoute {
  kind: 'assets-transactions';
}

export type MeRoute =
  | MeDashboardRoute
  | MeAssetsRoute
  | MeAssetTransactionsRoute;

export function createDefaultMeRoute(): MeDashboardRoute {
  return {
    kind: 'dashboard'
  };
}

export function parseMeRoute(pathname: string): MeRoute | null {
  if (pathname === ME_ENTRY_PATH || pathname === `${ME_ENTRY_PATH}/`) {
    return createDefaultMeRoute();
  }

  if (pathname === ME_ASSETS_PATH || pathname === `${ME_ASSETS_PATH}/`) {
    return {
      kind: 'assets'
    };
  }

  if (pathname === ME_ASSET_TRANSACTIONS_PATH || pathname === `${ME_ASSET_TRANSACTIONS_PATH}/`) {
    return {
      kind: 'assets-transactions'
    };
  }

  return null;
}

export function buildMePath(route: MeRoute = createDefaultMeRoute()): string {
  if (route.kind === 'assets') {
    return ME_ASSETS_PATH;
  }

  if (route.kind === 'assets-transactions') {
    return ME_ASSET_TRANSACTIONS_PATH;
  }

  return ME_ENTRY_PATH;
}

export function isMePathname(pathname: string): boolean {
  return parseMeRoute(pathname) !== null;
}
