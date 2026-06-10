import { TAURI_DESKTOP_ENTRY_PATH } from '../platform/tauriBridge.ts';
import { isCirclePathname } from '../circle/circleRouteState.ts';
import { isPublicDiscoverPathname } from '../public/discoverRouteState.ts';

export const BROWSER_PUBLIC_ENTRY_PATH = '/discover';
export const CAPACITOR_PUBLIC_ENTRY_PATH = '/docs';
export const OIDC_CALLBACK_PATH = '/oidc/callback';

interface ResolveInitialEntryPathOptions {
  isCapacitorNativePlatform: boolean;
  isTauriRuntime: boolean;
  pathname: string;
}

export function resolveInitialEntryPath({
  isCapacitorNativePlatform,
  isTauriRuntime,
  pathname,
}: ResolveInitialEntryPathOptions): string | null {
  if (pathname !== '/') {
    return null;
  }

  if (isTauriRuntime) {
    return TAURI_DESKTOP_ENTRY_PATH;
  }

  return isCapacitorNativePlatform ? CAPACITOR_PUBLIC_ENTRY_PATH : BROWSER_PUBLIC_ENTRY_PATH;
}

export function isPublicContentPathname(pathname: string): boolean {
  return (
    isPublicDiscoverPathname(pathname)
    || pathname === '/forum'
    || pathname.startsWith('/forum/')
    || pathname === '/shop'
    || pathname.startsWith('/shop/')
    || pathname === '/leaderboard'
    || pathname.startsWith('/leaderboard/')
    || pathname === '/docs'
    || pathname.startsWith('/docs/')
    || /^\/u\/(?:[1-9]\d*|usr_[0-9a-f]{32})\/?$/i.test(pathname)
    || pathname === '/__documents__'
    || pathname.startsWith('/__documents__/')
  );
}

export { isCirclePathname };
