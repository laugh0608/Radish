import { useEffect } from 'react';
import { UserProvider } from './contexts/UserContext';
import { LoadingProvider } from './contexts/LoadingContext';
import { ErrorBoundary } from './components/ErrorBoundary';
import { GlobalLoading } from './components/GlobalLoading';
import { setupApiInterceptors } from './services/apiInterceptor';
import { tokenService } from './services/tokenService';
import { env } from './config/env';
import { log } from './utils/logger';
import { ConsoleRouterProvider } from './router';

setupApiInterceptors();

function App() {
  useEffect(() => {
    const stopActivityTracking = tokenService.startActivityTracking(() => {
      log.warn('App', 'Console 会话因长时间未使用已过期');
      tokenService.clearTokens('idle_session_expired');

      const isLoginPage = window.location.pathname.endsWith('/login');
      if (!isLoginPage) {
        window.location.href = '/console/login?auto=1&reason=idle';
      }
    });

    return stopActivityTracking;
  }, []);

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
          <ConsoleRouterProvider />
          <GlobalLoading />
        </UserProvider>
      </LoadingProvider>
    </ErrorBoundary>
  );
}

export default App;
