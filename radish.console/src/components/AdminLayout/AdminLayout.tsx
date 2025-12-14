import { useState, type ReactNode } from 'react';
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
} from '@radish/ui';
import './AdminLayout.css';

const { Header, Sider, Content } = Layout;

export interface AdminLayoutProps {
  /**
   * 当前选中的菜单 key
   */
  selectedKey?: string;
  /**
   * 菜单项点击回调
   */
  onMenuClick?: (key: string) => void;
  /**
   * 用户信息
   */
  user?: {
    name: string;
    avatar?: string;
  };
  /**
   * 用户菜单点击回调
   */
  onUserMenuClick?: (key: string) => void;
  /**
   * 内容区域
   */
  children: ReactNode;
}

/**
 * AdminLayout - Radish Console 后台管理布局
 */
export const AdminLayout = ({
  selectedKey,
  onMenuClick,
  user,
  onUserMenuClick,
  children,
}: AdminLayoutProps) => {
  const [collapsed, setCollapsed] = useState(false);

  const menuItems: MenuProps['items'] = [
    {
      key: 'dashboard',
      icon: <AppstoreOutlined />,
      label: '仪表盘',
    },
    {
      key: 'applications',
      icon: <AppstoreOutlined />,
      label: '应用管理',
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

  const handleUserMenuClick: MenuProps['onClick'] = ({ key }) => {
    onUserMenuClick?.(key);
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
          {collapsed ? '🌿' : 'Radish Console'}
        </div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={selectedKey ? [selectedKey] : []}
          items={menuItems}
          onClick={({ key }) => onMenuClick?.(key)}
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
            {user && (
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
                    src={user.avatar}
                  />
                  <span className="admin-username">{user.name}</span>
                </div>
              </Dropdown>
            )}
          </div>
        </Header>
        <Content className="admin-content">
          {children}
        </Content>
      </Layout>
    </Layout>
  );
};
