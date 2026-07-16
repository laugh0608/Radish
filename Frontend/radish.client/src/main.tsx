import { StrictMode, Suspense } from 'react';
import { createRoot } from 'react-dom/client';
import { configureApiClient } from '@radish/http';
import { ToastContainer } from '@radish/ui/toast';
import { tokenService } from '@/services/tokenService';
import { getApiBaseUrl } from '@/config/env';
import { applySiteBranding } from '@/services/siteBranding';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { initializeTheme } from '@/theme/theme';
import { BrowserAppRouter } from '@/bootstrap/BrowserAppRouter';
import { LanguageProvider } from '@/i18n/LanguageProvider';
import { getIntlLocale } from '@/locales/language';
import i18n from './i18n';
import {
  initializeTauriBridge,
  isTauriRuntime,
  rewriteDesktopOidcReturnToBrowserPath,
} from '@/platform/tauriBridge';
import { resolveInitialEntryPath } from '@/bootstrap/entryRoute';
import './theme/theme-tokens.css';
import './index.css';
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
  getLanguage: () => getIntlLocale(i18n.resolvedLanguage ?? i18n.language),
  translateMessage: (key, messageArguments) => i18n.exists(key)
    ? i18n.t(key, Object.fromEntries((messageArguments ?? []).map((value, index) => [index, value])))
    : undefined,
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <LanguageProvider>
      <ThemeProvider>
        <Suspense fallback={<div className="appLoading">{i18n.t('desktop.appLoading')}</div>}>
          <BrowserAppRouter />
        </Suspense>
        <ToastContainer />
      </ThemeProvider>
    </LanguageProvider>
  </StrictMode>
);
