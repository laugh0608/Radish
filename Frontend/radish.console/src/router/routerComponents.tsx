import { Suspense, type ReactNode } from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { AdminLayout } from '../components/AdminLayout';
import { ClientBackLink } from '../components/ClientBackLink';
import { RouteGuard } from '../components/PermissionGuard';
import { useUser } from '../hooks/useUser';
import { tokenService } from '../services/tokenService';
import { canEnterConsole, consoleRouteMetaMap } from './routeMeta';
import './routerComponents.css';
import { useTranslation } from 'react-i18next';

export function AuthenticatedLayout() {
  const { t } = useTranslation();
  const token = tokenService.getAccessToken();
  const { user, loading } = useUser();

  if (!token) {
    return <Navigate to="/login?auto=1" replace />;
  }

  if (tokenService.isIdleSessionExpired()) {
    tokenService.clearTokens('idle_session_expired');
    return <Navigate to="/login?auto=1&reason=idle" replace />;
  }

  if (tokenService.isTokenExpired()) {
    tokenService.clearTokens();
    return <Navigate to="/login?auto=1" replace />;
  }

  if (loading) {
    return (
      <div className="console-route-state console-route-state--loading" role="status">
        {t('console.auth.checking')}
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login?auto=1" replace />;
  }

  if (!canEnterConsole(user)) {
    return (
      <div className="console-route-state console-route-state--permission">
        <h2>{t('console.auth.deniedTitle')}</h2>
        <p>{t('console.auth.deniedDescription')}</p>
        <ClientBackLink />
      </div>
    );
  }

  return (
    <AdminLayout>
      <Outlet />
    </AdminLayout>
  );
}

export function RouteLoading() {
  const { t } = useTranslation();
  return (
    <div className="console-route-state console-route-state--loading" role="status">
      {t('console.route.loading')}
    </div>
  );
}

export function SuspenseRoute({ children }: { children: ReactNode }) {
  return <Suspense fallback={<RouteLoading />}>{children}</Suspense>;
}

export function GuardedRoute({ routeKey, children }: { routeKey: string; children: ReactNode }) {
  const route = consoleRouteMetaMap[routeKey];
  return <RouteGuard route={route}>{children}</RouteGuard>;
}
