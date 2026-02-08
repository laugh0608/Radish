import { StrictMode, Suspense, lazy } from 'react';
import { createRoot } from 'react-dom/client';
import './index.css';
import './i18n';
import 'highlight.js/styles/github-dark.css';

const App = lazy(() => import('./App.tsx'));
const Shell = lazy(() => import('./desktop/Shell.tsx').then((module) => ({ default: module.Shell })));

const isBrowser = typeof window !== 'undefined';
const isOidcCallback = isBrowser && window.location.pathname === '/oidc/callback';

const params = new URLSearchParams(window.location.search);
const isDemo = params.has('demo');

const Page = isOidcCallback || isDemo ? App : Shell;

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Suspense fallback={<div style={{ padding: '1rem', textAlign: 'center' }}>应用加载中...</div>}>
      <Page />
    </Suspense>
  </StrictMode>
);
