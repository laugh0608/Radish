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
  TAURI_DESKTOP_ENTRY_PATH,
  rewriteDesktopOidcReturnToBrowserPath,
} from '@/platform/tauriBridge';
import { isPublicDiscoverPathname } from './public/discoverRouteState';
import './theme/theme-tokens.css';
import './index.css';
import './i18n';
import 'highlight.js/styles/github-dark.css';

const App = lazy(() => import('./App.tsx'));
const RootEntry = lazy(() => import('./desktop/RootEntry.tsx').then((module) => ({ default: module.RootEntry })));
const PublicEntry = lazy(() => import('./public/PublicEntry.tsx').then((module) => ({ default: module.PublicEntry })));

const isBrowser = typeof window !== 'undefined';
const BROWSER_PUBLIC_ENTRY_PATH = '/discover';
const CAPACITOR_PUBLIC_ENTRY_PATH = '/docs';

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

const params = isBrowser ? new URLSearchParams(window.location.search) : new URLSearchParams();
const isDemo = params.has('demo');

if (isBrowser && window.location.pathname === '/' && !isDemo) {
  if (isTauriRuntime()) {
    window.history.replaceState({}, '', TAURI_DESKTOP_ENTRY_PATH);
  } else if (isCapacitorNativePlatform()) {
    window.history.replaceState({}, '', CAPACITOR_PUBLIC_ENTRY_PATH);
  } else {
    window.history.replaceState({}, '', BROWSER_PUBLIC_ENTRY_PATH);
  }
}

const isOidcCallback = isBrowser && window.location.pathname === '/oidc/callback';
const isPublicContentRoute = isBrowser && (
  isPublicDiscoverPathname(window.location.pathname)
  || window.location.pathname === '/forum'
  || window.location.pathname.startsWith('/forum/')
  || window.location.pathname === '/shop'
  || window.location.pathname.startsWith('/shop/')
  || window.location.pathname === '/leaderboard'
  || window.location.pathname.startsWith('/leaderboard/')
  || window.location.pathname === '/docs'
  || window.location.pathname.startsWith('/docs/')
  || /^\/u\/[1-9]\d*\/?$/.test(window.location.pathname)
  || window.location.pathname === '/__documents__'
  || window.location.pathname.startsWith('/__documents__/')
);

const Page = isOidcCallback || isDemo ? App : isPublicContentRoute ? PublicEntry : RootEntry;

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
