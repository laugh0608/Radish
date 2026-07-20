import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Navigate, useLocation } from 'react-router-dom';
import { useUser } from '@/hooks/useUser';
import {
  canAccessConsoleRoute,
  getDefaultAuthorizedPath,
  type ConsoleRouteMeta,
} from '@/router/routeMeta';

interface RouteGuardProps {
  children: ReactNode;
  route: ConsoleRouteMeta;
}

export function RouteGuard({ children, route }: RouteGuardProps) {
  const { t } = useTranslation();
  const location = useLocation();
  const { user, loading } = useUser();

  if (loading) {
    return <div style={{ padding: '24px' }}>{t('console.guard.checking')}</div>;
  }

  if (!user) {
    return <Navigate to="/login?auto=1" replace />;
  }

  if (!canAccessConsoleRoute(route, user)) {
    const fallbackPath = getDefaultAuthorizedPath(user);

    if (fallbackPath !== location.pathname) {
      return <Navigate to={fallbackPath} replace />;
    }

    const routeTitle = t(`console.route.${route.key}`, { defaultValue: route.title });
    return <div style={{ padding: '24px' }}>{t('console.guard.denied', { title: routeTitle })}</div>;
  }

  return <>{children}</>;
}
