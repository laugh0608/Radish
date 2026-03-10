import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useUser } from '@/contexts/UserContext';
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
  const location = useLocation();
  const { user, loading } = useUser();

  if (loading) {
    return <div style={{ padding: '24px' }}>正在校验页面权限...</div>;
  }

  if (!user) {
    return <Navigate to="/login?auto=1" replace />;
  }

  if (!canAccessConsoleRoute(route, user)) {
    const fallbackPath = getDefaultAuthorizedPath(user);

    if (fallbackPath !== location.pathname) {
      return <Navigate to={fallbackPath} replace />;
    }

    return <div style={{ padding: '24px' }}>当前账号暂无 {route.title} 访问权限。</div>;
  }

  return <>{children}</>;
}
