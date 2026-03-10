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
  SearchOutlined,
  TagsOutlined,
} from '@radish/ui';
import { ROUTES } from '../../router';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { getAuthServerBaseUrl, getPostLogoutRedirectUri, getAvatarUrl } from '@/config/env';
import { tokenService } from '../../services/tokenService';
import { AppBreadcrumb } from '../Breadcrumb';
import { GlobalSearch, useGlobalSearchHotkey } from '../GlobalSearch';
import { usePermission } from '@/hooks/usePermission';
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
  const [searchVisible, setSearchVisible] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { user, loading } = useUser();
  const canViewApplications = usePermission(CONSOLE_PERMISSIONS.applicationsView);
  const canViewProducts = usePermission(CONSOLE_PERMISSIONS.productsView);
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);
  const canViewRoles = usePermission(CONSOLE_PERMISSIONS.rolesView);
  const canViewTags = usePermission(CONSOLE_PERMISSIONS.tagsView);
  const canViewStickers = usePermission(CONSOLE_PERMISSIONS.stickersView);
  const canViewSystemConfig = usePermission(CONSOLE_PERMISSIONS.systemConfigView);
  const canViewHangfire = usePermission(CONSOLE_PERMISSIONS.hangfireView);

  // 全局搜索快捷键
  useGlobalSearchHotkey(() => setSearchVisible(true));

  // 根据当前路径获取选中的菜单 key
  const getSelectedKey = (): string => {
    const path = location.pathname;
    if (path === '/' || path === '') return 'dashboard';
    // 移除开头的 / 获取 key
    return path.slice(1);
  };

  const menuItems = [
    {
      key: 'dashboard',
      icon: <DashboardOutlined />,
      label: '仪表盘',
    },
    canViewApplications ? {
      key: 'applications',
      icon: <AppstoreOutlined />,
      label: '应用管理',
    } : null,
    canViewProducts ? {
      key: 'products',
      icon: <ShoppingOutlined />,
      label: '商品管理',
    } : null,
    canViewOrders ? {
      key: 'orders',
      icon: <FileTextOutlined />,
      label: '订单管理',
    } : null,
    canViewUsers ? {
      key: 'users',
      icon: <TeamOutlined />,
      label: '用户管理',
    } : null,
    canViewRoles ? {
      key: 'roles',
      icon: <SafetyOutlined />,
      label: '角色管理',
    } : null,
    canViewTags ? {
      key: 'tags',
      icon: <TagsOutlined />,
      label: '标签管理',
    } : null,
    canViewStickers ? {
      key: 'stickers',
      icon: <AppstoreOutlined />,
      label: '表情包管理',
    } : null,
    canViewSystemConfig ? {
      key: 'system-config',
      icon: <SettingOutlined />,
      label: '系统配置',
    } : null,
    canViewHangfire ? {
      key: 'hangfire',
      icon: <ClockCircleOutlined />,
      label: '定时任务',
    } : null,
  ].filter((item): item is NonNullable<MenuProps['items']>[number] => item !== null);

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
      tags: ROUTES.TAGS,
      stickers: ROUTES.STICKERS,
      'system-config': ROUTES.SYSTEM_CONFIG,
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

      {/* 全局搜索组件 */}
      <GlobalSearch visible={searchVisible} onClose={() => setSearchVisible(false)} />
    </Layout>
  );
}
