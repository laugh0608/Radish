import { Suspense, type ReactNode } from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { AdminLayout } from '../components/AdminLayout';
import { ClientBackLink } from '../components/ClientBackLink';
import { RouteGuard } from '../components/PermissionGuard';
import { useUser } from '../hooks/useUser';
import { tokenService } from '../services/tokenService';
import { canEnterConsole, consoleRouteMetaMap } from './routeMeta';
import './routerComponents.css';

export function AuthenticatedLayout() {
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
        正在校验 Console 访问权限...
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login?auto=1" replace />;
  }

  if (!canEnterConsole(user)) {
    return (
      <div className="console-route-state console-route-state--permission">
        <h2>当前账号未开通 Console 访问权限</h2>
        <p>
          请联系管理员为当前角色分配至少一个 Console 页面权限；入口权限会随授权自动收口。
        </p>
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
  return (
    <div className="console-route-state console-route-state--loading" role="status">
      正在加载页面...
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
