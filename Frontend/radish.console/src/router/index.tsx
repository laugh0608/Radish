import type { ReactNode } from 'react';
import { createBrowserRouter, Navigate, Outlet } from 'react-router-dom';
import { AdminLayout } from '../components/AdminLayout';
import { RouteGuard } from '../components/PermissionGuard';
import { Dashboard } from '../pages/Dashboard';
import { Applications } from '../pages/Applications';
import { ProductList } from '../pages/Products';
import { OrderList } from '../pages/Orders';
import { UserList } from '../pages/Users';
import { UserDetail } from '../pages/Users/UserDetail';
import { RoleList } from '../pages/Roles';
import { TagList } from '../pages/Tags';
import { StickerGroupList, StickerList } from '../pages/Stickers';
import { SystemConfigList } from '../pages/SystemConfig';
import { UserProfile } from '../pages/UserProfile';
import { Settings } from '../pages/Settings';
import { Login } from '../pages/Login';
import { OidcCallback } from '../pages/OidcCallback';
import { ThemeTest } from '../pages/ThemeTest';
import { NotFound } from '../components/NotFound';
import { getApiBaseUrl } from '../config/env';
import { tokenService } from '../services/tokenService';
import { consoleRouteMetaMap } from './routeMeta';

function AuthenticatedLayout() {
  const token = tokenService.getAccessToken();

  if (!token) {
    return <Navigate to="/login?auto=1" replace />;
  }

  if (tokenService.isTokenExpired()) {
    tokenService.clearTokens();
    return <Navigate to="/login?auto=1" replace />;
  }

  return (
    <AdminLayout>
      <Outlet />
    </AdminLayout>
  );
}

function HangfirePage() {
  return (
    <div
      style={{
        height: 'calc(100vh - 200px)',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
      }}
    >
      <h2 style={{ margin: '0 0 16px 0', flexShrink: 0 }}>定时任务管理</h2>
      <iframe
        src={`${getApiBaseUrl()}/hangfire`}
        style={{
          flex: 1,
          border: '1px solid #d9d9d9',
          borderRadius: '4px',
          width: '100%',
          height: '100%',
        }}
        title="Hangfire Dashboard"
      />
    </div>
  );
}

function withRouteGuard(routeKey: string, element: ReactNode) {
  const route = consoleRouteMetaMap[routeKey];
  return <RouteGuard route={route}>{element}</RouteGuard>;
}

export const router = createBrowserRouter(
  [
    {
      path: '/login',
      element: <Login />,
    },
    {
      path: '/callback',
      element: <OidcCallback />,
    },
    {
      path: '/',
      element: <AuthenticatedLayout />,
      children: [
        {
          index: true,
          element: withRouteGuard('dashboard', <Dashboard />),
        },
        {
          path: 'applications',
          element: withRouteGuard('applications', <Applications />),
        },
        {
          path: 'products',
          element: withRouteGuard('products', <ProductList />),
        },
        {
          path: 'orders',
          element: withRouteGuard('orders', <OrderList />),
        },
        {
          path: 'users',
          element: withRouteGuard('users', <UserList />),
        },
        {
          path: 'users/:userId',
          element: withRouteGuard('user-detail', <UserDetail />),
        },
        {
          path: 'roles',
          element: withRouteGuard('roles', <RoleList />),
        },
        {
          path: 'tags',
          element: withRouteGuard('tags', <TagList />),
        },
        {
          path: 'stickers',
          element: withRouteGuard('stickers', <StickerGroupList />),
        },
        {
          path: 'stickers/:groupId/items',
          element: withRouteGuard('sticker-items', <StickerList />),
        },
        {
          path: 'system-config',
          element: withRouteGuard('system-config', <SystemConfigList />),
        },
        {
          path: 'profile',
          element: withRouteGuard('profile', <UserProfile />),
        },
        {
          path: 'settings',
          element: withRouteGuard('settings', <Settings />),
        },
        {
          path: 'hangfire',
          element: withRouteGuard('hangfire', <HangfirePage />),
        },
        {
          path: 'theme-test',
          element: withRouteGuard('theme-test', <ThemeTest />),
        },
      ],
    },
    {
      path: '*',
      element: <NotFound />,
    },
  ],
  {
    basename: '/console/',
  }
);

export const ROUTES = {
  HOME: '/',
  LOGIN: '/login',
  CALLBACK: '/callback',
  APPLICATIONS: '/applications',
  PRODUCTS: '/products',
  ORDERS: '/orders',
  USERS: '/users',
  USER_DETAIL: '/users/:userId',
  ROLES: '/roles',
  TAGS: '/tags',
  STICKERS: '/stickers',
  STICKER_ITEMS: '/stickers/:groupId/items',
  SYSTEM_CONFIG: '/system-config',
  PROFILE: '/profile',
  SETTINGS: '/settings',
  HANGFIRE: '/hangfire',
  THEME_TEST: '/theme-test',
} as const;
