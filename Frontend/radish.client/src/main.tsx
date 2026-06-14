import { StrictMode, Suspense, lazy } from 'react';
import { createRoot } from 'react-dom/client';
import { configureApiClient } from '@radish/http';
import { tokenService } from '@/services/tokenService';
import { getApiBaseUrl } from '@/config/env';
import { applySiteBranding } from '@/services/siteBranding';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { initializeTheme } from '@/theme/theme';
import {
  initializeTauriBridge,
  isTauriRuntime,
  rewriteDesktopOidcReturnToBrowserPath,
} from '@/platform/tauriBridge';
import {
  OIDC_CALLBACK_PATH,
  isCirclePathname,
  isMePathname,
  isNotificationsPathname,
  isPublicContentPathname,
  resolveInitialEntryPath,
} from '@/bootstrap/entryRoute';
import './theme/theme-tokens.css';
import './index.css';
import './i18n';
import 'highlight.js/styles/github-dark.css';

const OidcCallbackPage = lazy(() => import('./auth/OidcCallbackPage.tsx').then((module) => ({ default: module.OidcCallbackPage })));
const CircleEntry = lazy(() => import('./circle/CircleEntry.tsx').then((module) => ({ default: module.CircleEntry })));
const MeEntry = lazy(() => import('./me/MeEntry.tsx').then((module) => ({ default: module.MeEntry })));
const NotificationsEntry = lazy(() => import('./notifications/NotificationsEntry.tsx').then((module) => ({ default: module.NotificationsEntry })));
const RootEntry = lazy(() => import('./desktop/RootEntry.tsx').then((module) => ({ default: module.RootEntry })));
const PublicEntry = lazy(() => import('./public/PublicEntry.tsx').then((module) => ({ default: module.PublicEntry })));

const isBrowser = typeof window !== 'undefined';

function isCapacitorNativePlatform(): boolean {
  return window.Capacitor?.isNativePlatform?.() === true;
}

function handleTauriDeepLink(url: string): void {
  if (!isBrowser) {
    return;
  }

  const nextPath = rewriteDesktopOidcReturnToBrowserPath(url);
  if (!nextPath) {
    return;
  }

  window.location.replace(nextPath);
}

if (isBrowser) {
  initializeTauriBridge({
    onDeepLink: handleTauriDeepLink,
  });
}

const initialEntryPath = isBrowser
  ? resolveInitialEntryPath({
      pathname: window.location.pathname,
      isTauriRuntime: isTauriRuntime(),
      isCapacitorNativePlatform: isCapacitorNativePlatform(),
    })
  : null;

if (initialEntryPath) {
  window.history.replaceState({}, '', initialEntryPath);
}

const isOidcCallback = isBrowser && window.location.pathname === OIDC_CALLBACK_PATH;
const isCircleRoute = isBrowser && isCirclePathname(window.location.pathname);
const isMeRoute = isBrowser && isMePathname(window.location.pathname);
const isNotificationsRoute = isBrowser && isNotificationsPathname(window.location.pathname);
const isPublicContentRoute = isBrowser && isPublicContentPathname(window.location.pathname);

const Page = isOidcCallback
  ? OidcCallbackPage
  : isNotificationsRoute
    ? NotificationsEntry
    : isMeRoute
      ? MeEntry
      : isCircleRoute
        ? CircleEntry
        : isPublicContentRoute
          ? PublicEntry
          : RootEntry;

initializeTheme();
void applySiteBranding(getApiBaseUrl());

configureApiClient({
  getToken: () => tokenService.getAccessToken(),
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <Suspense fallback={<div className="appLoading">应用加载中...</div>}>
        <Page />
      </Suspense>
    </ThemeProvider>
  </StrictMode>
);
