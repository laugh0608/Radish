import { StrictMode, Suspense } from 'react';
import { createRoot } from 'react-dom/client';
import { configureApiClient } from '@radish/http';
import { tokenService } from '@/services/tokenService';
import { getApiBaseUrl } from '@/config/env';
import { applySiteBranding } from '@/services/siteBranding';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { initializeTheme } from '@/theme/theme';
import { BrowserAppRouter } from '@/bootstrap/BrowserAppRouter';
import {
  initializeTauriBridge,
  isTauriRuntime,
  rewriteDesktopOidcReturnToBrowserPath,
} from '@/platform/tauriBridge';
import { resolveInitialEntryPath } from '@/bootstrap/entryRoute';
import './theme/theme-tokens.css';
import './index.css';
import './i18n';
import 'highlight.js/styles/github-dark.css';

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

initializeTheme();
void applySiteBranding(getApiBaseUrl());

configureApiClient({
  getToken: () => tokenService.getAccessToken(),
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <Suspense fallback={<div className="appLoading">应用加载中...</div>}>
        <BrowserAppRouter />
      </Suspense>
    </ThemeProvider>
  </StrictMode>
);
