import { lazy, Suspense, type ReactNode } from 'react';
import { createBrowserRouter, Navigate, Outlet } from 'react-router-dom';
import { AdminLayout } from '../components/AdminLayout';
import { RouteGuard } from '../components/PermissionGuard';
import { getApiBaseUrl } from '../config/env';
import { tokenService } from '../services/tokenService';
import { canEnterConsole, consoleRouteMetaMap } from './routeMeta';
import { useUser } from '../contexts/UserContext';

const Applications = lazy(() => import('../pages/Applications').then(module => ({ default: module.Applications })));
const CategoryList = lazy(() => import('../pages/Categories').then(module => ({ default: module.CategoryList })));
const CoinAdminPage = lazy(() => import('../pages/Coins').then(module => ({ default: module.CoinAdminPage })));
const Dashboard = lazy(() => import('../pages/Dashboard').then(module => ({ default: module.Dashboard })));
const ExperienceAdminPage = lazy(() => import('../pages/Experience').then(module => ({ default: module.ExperienceAdminPage })));
const Login = lazy(() => import('../pages/Login').then(module => ({ default: module.Login })));
const ModerationPage = lazy(() => import('../pages/Moderation').then(module => ({ default: module.ModerationPage })));
const NotFound = lazy(() => import('../components/NotFound').then(module => ({ default: module.NotFound })));
const OidcCallback = lazy(() => import('../pages/OidcCallback').then(module => ({ default: module.OidcCallback })));
const OrderList = lazy(() => import('../pages/Orders').then(module => ({ default: module.OrderList })));
const ProductList = lazy(() => import('../pages/Products').then(module => ({ default: module.ProductList })));
const RoleList = lazy(() => import('../pages/Roles').then(module => ({ default: module.RoleList })));
const RolePermissionPage = lazy(() => import('../pages/Roles').then(module => ({ default: module.RolePermissionPage })));
const Settings = lazy(() => import('../pages/Settings').then(module => ({ default: module.Settings })));
const StickerGroupList = lazy(() => import('../pages/Stickers').then(module => ({ default: module.StickerGroupList })));
const StickerList = lazy(() => import('../pages/Stickers').then(module => ({ default: module.StickerList })));
const SystemConfigList = lazy(() => import('../pages/SystemConfig').then(module => ({ default: module.SystemConfigList })));
const TagList = lazy(() => import('../pages/Tags').then(module => ({ default: module.TagList })));
const ThemeTest = lazy(() => import('../pages/ThemeTest').then(module => ({ default: module.ThemeTest })));
const UserDetail = lazy(() => import('../pages/Users/UserDetail').then(module => ({ default: module.UserDetail })));
const UserList = lazy(() => import('../pages/Users').then(module => ({ default: module.UserList })));
const UserProfile = lazy(() => import('../pages/UserProfile').then(module => ({ default: module.UserProfile })));

function AuthenticatedLayout() {
  const token = tokenService.getAccessToken();
  const { user, loading } = useUser();

  if (!token) {
    return <Navigate to="/login?auto=1" replace />;
  }

  if (tokenService.isTokenExpired()) {
    tokenService.clearTokens();
    return <Navigate to="/login?auto=1" replace />;
  }

  if (loading) {
    return <div style={{ padding: '24px' }}>正在校验 Console 访问权限...</div>;
  }

  if (!user) {
    return <Navigate to="/login?auto=1" replace />;
  }

  if (!canEnterConsole(user)) {
    return (
      <div style={{ padding: '48px 24px', maxWidth: '720px', margin: '0 auto' }}>
        <h2 style={{ marginBottom: '12px' }}>当前账号未开通 Console 访问权限</h2>
        <p style={{ margin: 0, color: '#666' }}>
          请联系管理员为当前角色分配至少一个 Console 页面权限；入口权限会随授权自动收口。
        </p>
      </div>
    );
  }

  return (
    <AdminLayout>
      <Outlet />
    </AdminLayout>
  );
}

function RouteLoading() {
  return <div style={{ padding: '24px' }}>正在加载页面...</div>;
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

function withSuspense(element: ReactNode) {
  return <Suspense fallback={<RouteLoading />}>{element}</Suspense>;
}

function withRouteGuard(routeKey: string, element: ReactNode) {
  const route = consoleRouteMetaMap[routeKey];
  return <RouteGuard route={route}>{withSuspense(element)}</RouteGuard>;
}

export const router = createBrowserRouter(
  [
    {
      path: '/login',
      element: withSuspense(<Login />),
    },
    {
      path: '/callback',
      element: withSuspense(<OidcCallback />),
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
          path: 'roles/:roleId/permissions',
          element: withRouteGuard('role-permissions', <RolePermissionPage />),
        },
        {
          path: 'categories',
          element: withRouteGuard('categories', <CategoryList />),
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
          path: 'moderation',
          element: withRouteGuard('moderation', <ModerationPage />),
        },
        {
          path: 'coins',
          element: withRouteGuard('coins', <CoinAdminPage />),
        },
        {
          path: 'experience',
          element: withRouteGuard('experience', <ExperienceAdminPage />),
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
      element: withSuspense(<NotFound />),
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
  ROLE_PERMISSIONS: '/roles/:roleId/permissions',
  CATEGORIES: '/categories',
  TAGS: '/tags',
  STICKERS: '/stickers',
  MODERATION: '/moderation',
  COINS: '/coins',
  EXPERIENCE: '/experience',
  STICKER_ITEMS: '/stickers/:groupId/items',
  SYSTEM_CONFIG: '/system-config',
  PROFILE: '/profile',
  SETTINGS: '/settings',
  HANGFIRE: '/hangfire',
  THEME_TEST: '/theme-test',
} as const;
