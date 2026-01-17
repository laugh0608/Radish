import { useState, type ReactNode } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useUser } from '../../contexts/UserContext';
import {
  Layout,
  Menu,
  Dropdown,
  Avatar,
  type MenuProps,
  message,
} from '@radish/ui';
import {
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  UserOutlined,
  LogoutOutlined,
  SettingOutlined,
  AppstoreOutlined,
  TeamOutlined,
  SafetyOutlined,
  ClockCircleOutlined,
  DashboardOutlined,
  ShoppingOutlined,
  FileTextOutlined,
} from '@radish/ui';
import { ROUTES } from '../../router';
import { getAuthServerBaseUrl, getPostLogoutRedirectUri } from '@/config/env';
import { tokenService } from '../../services/tokenService';
import { AppBreadcrumb } from '../Breadcrumb';
import './AdminLayout.css';

const { Header, Sider, Content } = Layout;

export interface AdminLayoutProps {
  /**
   * 内容区域
   */
  children: ReactNode;
}

/**
 * AdminLayout - Radish Console 后台管理布局
 */
export function AdminLayout({ children }: AdminLayoutProps) {
  const [collapsed, setCollapsed] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { user, loading } = useUser();

  // 根据当前路径获取选中的菜单 key
  const getSelectedKey = (): string => {
    const path = location.pathname;
    if (path === '/' || path === '') return 'dashboard';
    // 移除开头的 / 获取 key
    return path.slice(1);
  };

  const menuItems: MenuProps['items'] = [
    {
      key: 'dashboard',
      icon: <DashboardOutlined />,
      label: '仪表盘',
    },
    {
      key: 'applications',
      icon: <AppstoreOutlined />,
      label: '应用管理',
    },
    {
      key: 'products',
      icon: <ShoppingOutlined />,
      label: '商品管理',
    },
    {
      key: 'orders',
      icon: <FileTextOutlined />,
      label: '订单管理',
    },
    {
      key: 'users',
      icon: <TeamOutlined />,
      label: '用户管理',
    },
    {
      key: 'roles',
      icon: <SafetyOutlined />,
      label: '角色管理',
    },
    {
      key: 'hangfire',
      icon: <ClockCircleOutlined />,
      label: '定时任务',
    },
  ];

  const userMenuItems: MenuProps['items'] = [
    {
      key: 'profile',
      icon: <UserOutlined />,
      label: '个人信息',
    },
    {
      key: 'settings',
      icon: <SettingOutlined />,
      label: '设置',
    },
    {
      type: 'divider',
    },
    {
      key: 'logout',
      icon: <LogoutOutlined />,
      label: '退出登录',
      danger: true,
    },
  ];

  const handleToggle = () => {
    setCollapsed(!collapsed);
  };

  const handleMenuClick: MenuProps['onClick'] = ({ key }) => {
    // 根据 key 导航到对应路由
    const routeMap: Record<string, string> = {
      dashboard: ROUTES.HOME,
      applications: ROUTES.APPLICATIONS,
      products: ROUTES.PRODUCTS,
      orders: ROUTES.ORDERS,
      users: ROUTES.USERS,
      roles: ROUTES.ROLES,
      hangfire: ROUTES.HANGFIRE,
    };
    const path = routeMap[key];
    if (path) {
      navigate(path);
    }
  };

  const handleLogout = () => {
    // 使用 TokenService 清理 Token
    tokenService.clearTokens();

    // 使用 OIDC 标准的 endsession endpoint 实现 Single Sign-Out
    const postLogoutRedirectUri = getPostLogoutRedirectUri();
    const authServerBaseUrl = getAuthServerBaseUrl();

    const logoutUrl = new URL(`${authServerBaseUrl}/connect/endsession`);
    logoutUrl.searchParams.set('post_logout_redirect_uri', postLogoutRedirectUri);
    logoutUrl.searchParams.set('client_id', 'radish-console');

    window.location.href = logoutUrl.toString();
  };

  const handleUserMenuClick: MenuProps['onClick'] = ({ key }) => {
    switch (key) {
      case 'logout':
        handleLogout();
        break;
      case 'profile':
        message.info('个人信息功能待实现');
        break;
      case 'settings':
        message.info('设置功能待实现');
        break;
    }
  };

  return (
    <Layout className="admin-layout">
      <Sider
        trigger={null}
        collapsible
        collapsed={collapsed}
        className="admin-sider"
      >
        <div className="admin-logo">
          {collapsed ? 'R' : 'Radish Console'}
        </div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[getSelectedKey()]}
          items={menuItems}
          onClick={handleMenuClick}
        />
      </Sider>
      <Layout className={collapsed ? 'collapsed' : ''}>
        <Header className="admin-header">
          <div className="admin-header-left">
            {collapsed ? (
              <MenuUnfoldOutlined
                className="admin-trigger"
                onClick={handleToggle}
              />
            ) : (
              <MenuFoldOutlined
                className="admin-trigger"
                onClick={handleToggle}
              />
            )}
          </div>
          <div className="admin-header-right">
            <Dropdown
              menu={{
                items: userMenuItems,
                onClick: handleUserMenuClick,
              }}
              placement="bottomRight"
            >
              <div className="admin-user">
                <Avatar
                  size="small"
                  icon={<UserOutlined />}
                  src={user?.avatarUrl}
                />
                <span className="admin-username">
                  {loading ? '加载中...' : (user?.userName || 'Unknown')}
                </span>
              </div>
            </Dropdown>
          </div>
        </Header>
        <Content className="admin-content">
          <AppBreadcrumb />
          {children}
        </Content>
      </Layout>
    </Layout>
  );
}
