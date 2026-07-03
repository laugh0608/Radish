import { createElement, lazy, type ReactNode } from 'react';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import {
  AuthenticatedLayout,
  GuardedRoute,
  SuspenseRoute,
} from './routerComponents';
import { HangfirePage } from '../pages/SystemTools/HangfirePage';

const applicationsPage = lazy(() => import('../pages/Applications').then(module => ({ default: module.Applications })));
const categoryListPage = lazy(() => import('../pages/Categories').then(module => ({ default: module.CategoryList })));
const coinAdminPage = lazy(() => import('../pages/Coins').then(module => ({ default: module.CoinAdminPage })));
const dashboardPage = lazy(() => import('../pages/Dashboard').then(module => ({ default: module.Dashboard })));
const documentGovernancePage = lazy(() => import('../pages/Documents').then(module => ({ default: module.DocumentGovernancePage })));
const experienceAdminPage = lazy(() => import('../pages/Experience').then(module => ({ default: module.ExperienceAdminPage })));
const loginPage = lazy(() => import('../pages/Login').then(module => ({ default: module.Login })));
const moderationPage = lazy(() => import('../pages/Moderation').then(module => ({ default: module.ModerationPage })));
const notFoundPage = lazy(() => import('../components/NotFound').then(module => ({ default: module.NotFound })));
const oidcCallbackPage = lazy(() => import('../pages/OidcCallback').then(module => ({ default: module.OidcCallback })));
const orderListPage = lazy(() => import('../pages/Orders').then(module => ({ default: module.OrderList })));
const productListPage = lazy(() => import('../pages/Products').then(module => ({ default: module.ProductList })));
const roleListPage = lazy(() => import('../pages/Roles').then(module => ({ default: module.RoleList })));
const rolePermissionPage = lazy(() => import('../pages/Roles').then(module => ({ default: module.RolePermissionPage })));
const settingsPage = lazy(() => import('../pages/Settings').then(module => ({ default: module.Settings })));
const stickerGroupListPage = lazy(() => import('../pages/Stickers').then(module => ({ default: module.StickerGroupList })));
const stickerListPage = lazy(() => import('../pages/Stickers').then(module => ({ default: module.StickerList })));
const systemConfigListPage = lazy(() => import('../pages/SystemConfig').then(module => ({ default: module.SystemConfigList })));
const tagListPage = lazy(() => import('../pages/Tags').then(module => ({ default: module.TagList })));
const themeTestPage = lazy(() => import('../pages/ThemeTest').then(module => ({ default: module.ThemeTest })));
const userDetailPage = lazy(() => import('../pages/Users/UserDetail').then(module => ({ default: module.UserDetail })));
const userListPage = lazy(() => import('../pages/Users').then(module => ({ default: module.UserList })));
const userProfilePage = lazy(() => import('../pages/UserProfile').then(module => ({ default: module.UserProfile })));

function withSuspense(element: ReactNode) {
  return <SuspenseRoute>{element}</SuspenseRoute>;
}

function withRouteGuard(routeKey: string, element: ReactNode) {
  return <GuardedRoute routeKey={routeKey}>{withSuspense(element)}</GuardedRoute>;
}

const router = createBrowserRouter(
  [
    {
      path: '/login',
      element: withSuspense(createElement(loginPage)),
    },
    {
      path: '/callback',
      element: withSuspense(createElement(oidcCallbackPage)),
    },
    {
      path: '/',
      element: createElement(AuthenticatedLayout),
      children: [
        {
          index: true,
          element: withRouteGuard('dashboard', createElement(dashboardPage)),
        },
        {
          path: 'applications',
          element: withRouteGuard('applications', createElement(applicationsPage)),
        },
        {
          path: 'products',
          element: withRouteGuard('products', createElement(productListPage)),
        },
        {
          path: 'orders',
          element: withRouteGuard('orders', createElement(orderListPage)),
        },
        {
          path: 'users',
          element: withRouteGuard('users', createElement(userListPage)),
        },
        {
          path: 'users/:userId',
          element: withRouteGuard('user-detail', createElement(userDetailPage)),
        },
        {
          path: 'roles',
          element: withRouteGuard('roles', createElement(roleListPage)),
        },
        {
          path: 'roles/:roleId/permissions',
          element: withRouteGuard('role-permissions', createElement(rolePermissionPage)),
        },
        {
          path: 'categories',
          element: withRouteGuard('categories', createElement(categoryListPage)),
        },
        {
          path: 'tags',
          element: withRouteGuard('tags', createElement(tagListPage)),
        },
        {
          path: 'documents',
          element: withRouteGuard('documents', createElement(documentGovernancePage)),
        },
        {
          path: 'stickers',
          element: withRouteGuard('stickers', createElement(stickerGroupListPage)),
        },
        {
          path: 'moderation',
          element: withRouteGuard('moderation', createElement(moderationPage)),
        },
        {
          path: 'coins',
          element: withRouteGuard('coins', createElement(coinAdminPage)),
        },
        {
          path: 'experience',
          element: withRouteGuard('experience', createElement(experienceAdminPage)),
        },
        {
          path: 'stickers/:groupId/items',
          element: withRouteGuard('sticker-items', createElement(stickerListPage)),
        },
        {
          path: 'system-config',
          element: withRouteGuard('system-config', createElement(systemConfigListPage)),
        },
        {
          path: 'profile',
          element: withRouteGuard('profile', createElement(userProfilePage)),
        },
        {
          path: 'settings',
          element: withRouteGuard('settings', createElement(settingsPage)),
        },
        {
          path: 'hangfire',
          element: withRouteGuard('hangfire', createElement(HangfirePage)),
        },
        {
          path: 'theme-test',
          element: withRouteGuard('theme-test', createElement(themeTestPage)),
        },
      ],
    },
    {
      path: '*',
      element: withSuspense(createElement(notFoundPage)),
    },
  ],
  {
    basename: '/console/',
  }
);

export function ConsoleRouterProvider() {
  return <RouterProvider router={router} />;
}
