import { useEffect } from 'react';
import { RouterProvider } from 'react-router-dom';
import { UserProvider } from './contexts/UserContext';
import { LoadingProvider } from './contexts/LoadingContext';
import { ErrorBoundary } from './components/ErrorBoundary';
import { GlobalLoading } from './components/GlobalLoading';
import { setupApiInterceptors } from './services/apiInterceptor';
import { tokenService } from './services/tokenService';
import { env } from './config/env';
import { log } from './utils/logger';
import { router } from './router';
import './App.css';

setupApiInterceptors();

function App() {
  useEffect(() => {
    if (!env.tokenAutoRefreshDebug) {
      return;
    }

    log.info('App', '已启用 Token 自动刷新调试定时器');
    tokenService.startAutoRefresh();

    const handleTokenUpdated = () => {
      tokenService.startAutoRefresh();
    };
    window.addEventListener('auth:token-updated', handleTokenUpdated);

    return () => {
      window.removeEventListener('auth:token-updated', handleTokenUpdated);
      tokenService.stopAutoRefresh();
    };
  }, []);

  return (
    <ErrorBoundary>
      <LoadingProvider>
        <UserProvider>
          <RouterProvider router={router} />
          <GlobalLoading />
        </UserProvider>
      </LoadingProvider>
    </ErrorBoundary>
  );
}

export default App;
