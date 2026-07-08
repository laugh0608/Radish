import { TAURI_DESKTOP_ENTRY_PATH } from '../platform/tauriBridge.ts';
import { isCirclePathname } from '../circle/circleRouteState.ts';
import { isMePathname } from '../me/meRouteState.ts';
import { isMessagesPathname } from '../messages/messagesRouteState.ts';
import { isNotificationsPathname } from '../notifications/notificationRouteState.ts';
import { isPetPathname } from '../pet/petRouteState.ts';
import { isShopPathname } from '../shop/shopRouteState.ts';
import { isPublicDiscoverPathname } from '../public/discoverRouteState.ts';
import { isPublicShopPathname } from '../public/shopRouteState.ts';
import { isPublicLegalPathname } from '../public/legalRouteState.ts';
import { isDocsAuthorPathname } from '../docs/docsAuthorRouteState.ts';
import { isWorkbenchPathname } from '../workbench/workbenchRouteState.ts';

export const BROWSER_PUBLIC_ENTRY_PATH = '/discover';
export const CAPACITOR_PUBLIC_ENTRY_PATH = '/docs';
export const OIDC_CALLBACK_PATH = '/oidc/callback';

export type BrowserEntryKind =
  | 'oidc'
  | 'messages'
  | 'notifications'
  | 'pet'
  | 'me'
  | 'circle'
  | 'shop'
  | 'docs-author'
  | 'workbench'
  | 'public'
  | 'root';

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
    || isPublicShopPathname(pathname)
    || pathname === '/leaderboard'
    || pathname.startsWith('/leaderboard/')
    || pathname === '/docs'
    || (pathname.startsWith('/docs/') && !isDocsAuthorPathname(pathname))
    || isPublicLegalPathname(pathname)
    || /^\/u\/(?:[1-9]\d*|usr_[0-9a-f]{32})\/?$/i.test(pathname)
    || pathname === '/__documents__'
    || pathname.startsWith('/__documents__/')
  );
}

export function resolveBrowserEntryKind(pathname: string): BrowserEntryKind {
  if (pathname === OIDC_CALLBACK_PATH) {
    return 'oidc';
  }

  if (isMessagesPathname(pathname)) {
    return 'messages';
  }

  if (isNotificationsPathname(pathname)) {
    return 'notifications';
  }

  if (isPetPathname(pathname)) {
    return 'pet';
  }

  if (isMePathname(pathname)) {
    return 'me';
  }

  if (isCirclePathname(pathname)) {
    return 'circle';
  }

  if (isShopPathname(pathname)) {
    return 'shop';
  }

  if (isDocsAuthorPathname(pathname)) {
    return 'docs-author';
  }

  if (isWorkbenchPathname(pathname)) {
    return 'workbench';
  }

  if (isPublicContentPathname(pathname)) {
    return 'public';
  }

  return 'root';
}

export { isCirclePathname, isMessagesPathname, isNotificationsPathname };
export { isMePathname };
export { isPetPathname };
export { isShopPathname };
export { isDocsAuthorPathname };
export { isWorkbenchPathname };
