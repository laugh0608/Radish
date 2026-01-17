import { createBrowserRouter, Navigate, Outlet } from 'react-router-dom';
import { AdminLayout } from '../components/AdminLayout';
import { Dashboard } from '../pages/Dashboard';
import { Applications } from '../pages/Applications';
import { ProductList } from '../pages/Products';
import { OrderList } from '../pages/Orders';
import { UserList } from '../pages/Users';
import { Login } from '../pages/Login';
import { OidcCallback } from '../pages/OidcCallback';
import { ThemeTest } from '../pages/ThemeTest';
import { NotFound } from '../components/NotFound';

/**
 * 需要认证的布局包装器
 */
function AuthenticatedLayout() {
  const token = localStorage.getItem('access_token');

  if (!token) {
    return <Navigate to="/login" replace />;
  }

  return (
    <AdminLayout>
      <Outlet />
    </AdminLayout>
  );
}

/**
 * 占位页面组件
 */
function PlaceholderPage({ title }: { title: string }) {
  return (
    <div>
      <h2>{title}</h2>
      <p>{title}功能待实现</p>
    </div>
  );
}

/**
 * Hangfire 嵌入页面
 */
function HangfirePage() {
  return (
    <div style={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
      <h2 style={{ margin: '0 0 16px 0' }}>定时任务管理</h2>
      <iframe
        src="/hangfire"
        style={{
          flex: 1,
          border: '1px solid #d9d9d9',
          borderRadius: '4px',
          width: '100%',
          minHeight: '600px'
        }}
        title="Hangfire Dashboard"
      />
    </div>
  );
}

/**
 * 路由配置
 * 注意：由于 Vite 配置了 base: '/console/'，所以路由路径不需要包含 /console 前缀
 */
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
          element: <Dashboard />,
        },
        {
          path: 'applications',
          element: <Applications />,
        },
        {
          path: 'products',
          element: <ProductList />,
        },
        {
          path: 'orders',
          element: <OrderList />,
        },
        {
          path: 'users',
          element: <UserList />,
        },
        {
          path: 'roles',
          element: <PlaceholderPage title="角色管理" />,
        },
        {
          path: 'hangfire',
          element: <HangfirePage />,
        },
        {
          path: 'theme-test',
          element: <ThemeTest />,
        },
      ],
    },
    {
      path: '*',
      element: <NotFound />,
    },
  ],
  {
    basename: '/console',
  }
);

/**
 * 路由路径常量，方便其他组件引用
 */
export const ROUTES = {
  HOME: '/',
  LOGIN: '/login',
  CALLBACK: '/callback',
  APPLICATIONS: '/applications',
  PRODUCTS: '/products',
  ORDERS: '/orders',
  USERS: '/users',
  ROLES: '/roles',
  HANGFIRE: '/hangfire',
  THEME_TEST: '/theme-test',
} as const;
