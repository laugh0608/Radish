import { StrictMode, Suspense, lazy } from 'react';
import { createRoot } from 'react-dom/client';
import { configureApiClient } from '@radish/http';
import { tokenService } from '@/services/tokenService';
import { getApiBaseUrl } from '@/config/env';
import { applySiteBranding } from '@/services/siteBranding';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { initializeTheme } from '@/theme/theme';
import { isPublicDiscoverPathname } from './public/discoverRouteState';
import './theme/theme-tokens.css';
import './index.css';
import './i18n';
import 'highlight.js/styles/github-dark.css';

const App = lazy(() => import('./App.tsx'));
const Shell = lazy(() => import('./desktop/Shell.tsx').then((module) => ({ default: module.Shell })));
const PublicEntry = lazy(() => import('./public/PublicEntry.tsx').then((module) => ({ default: module.PublicEntry })));

const isBrowser = typeof window !== 'undefined';
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

const params = new URLSearchParams(window.location.search);
const isDemo = params.has('demo');

const Page = isOidcCallback || isDemo ? App : isPublicContentRoute ? PublicEntry : Shell;

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
