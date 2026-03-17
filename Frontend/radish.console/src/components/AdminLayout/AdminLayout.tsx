import { useMemo, useState, type ReactNode } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useUser } from '../../contexts/UserContext';
import {
  Layout,
  Menu,
  Dropdown,
  Avatar,
  type MenuProps,
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
  SearchOutlined,
  TagsOutlined,
} from '@radish/ui';
import { ROUTES } from '../../router';
import { getAuthServerBaseUrl, getPostLogoutRedirectUri, getAvatarUrl } from '@/config/env';
import { tokenService } from '../../services/tokenService';
import { AppBreadcrumb } from '../Breadcrumb';
import { GlobalSearch, useGlobalSearchHotkey } from '../GlobalSearch';
import { getActiveMenuKey, getSidebarRoutes } from '@/router/routeMeta';
import './AdminLayout.css';

const { Header, Sider, Content } = Layout;

const menuIconMap: Record<string, ReactNode> = {
  dashboard: <DashboardOutlined />,
  applications: <AppstoreOutlined />,
  products: <ShoppingOutlined />,
  orders: <FileTextOutlined />,
  users: <TeamOutlined />,
  roles: <SafetyOutlined />,
  tags: <TagsOutlined />,
  stickers: <AppstoreOutlined />,
  'system-config': <SettingOutlined />,
  hangfire: <ClockCircleOutlined />,
};

export interface AdminLayoutProps {
  children: ReactNode;
}

export function AdminLayout({ children }: AdminLayoutProps) {
  const [collapsed, setCollapsed] = useState(false);
  const [searchVisible, setSearchVisible] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { user, loading } = useUser();

  useGlobalSearchHotkey(() => setSearchVisible(true));

  const sidebarRoutes = useMemo(() => getSidebarRoutes(user), [user]);
  const menuItems = useMemo<NonNullable<MenuProps['items']>>(
    () => sidebarRoutes.map((route) => ({
      key: route.key,
      icon: menuIconMap[route.key],
      label: route.title,
    })),
    [sidebarRoutes]
  );

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
    const path = sidebarRoutes.find((route) => route.key === key)?.path;
    if (path) {
      navigate(path);
    }
  };

  const handleLogout = () => {
    tokenService.clearTokens();

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
        navigate(ROUTES.PROFILE);
        break;
      case 'settings':
        navigate(ROUTES.SETTINGS);
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
          selectedKeys={[getActiveMenuKey(location.pathname)]}
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
            <SearchOutlined
              className="admin-search-icon"
              onClick={() => setSearchVisible(true)}
              style={{ fontSize: '18px', cursor: 'pointer', marginRight: '24px' }}
            />
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
                  src={getAvatarUrl(user?.voAvatarUrl)}
                />
                <span className="admin-username">
                  {loading ? '加载中...' : (user?.voUserName || '未知用户')}
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

      <GlobalSearch visible={searchVisible} onClose={() => setSearchVisible(false)} />
    </Layout>
  );
}
