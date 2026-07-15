import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useUser } from '../../hooks/useUser';
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
  CloseOutlined,
  AppstoreOutlined,
  TeamOutlined,
  SafetyOutlined,
  ClockCircleOutlined,
  DashboardOutlined,
  ShoppingOutlined,
  FileTextOutlined,
  SearchOutlined,
  TagsOutlined,
  TrophyOutlined,
  WalletOutlined,
} from '@radish/ui';
import { ROUTES } from '../../router/routes';
import { getAuthServerBaseUrl, getPostLogoutRedirectUri, getAvatarUrl } from '@/config/env';
import { tokenService } from '../../services/tokenService';
import { AppBreadcrumb } from '../Breadcrumb';
import { GlobalSearch, useGlobalSearchHotkey } from '../GlobalSearch';
import { getActiveMenuKey, getSidebarRouteGroups, type ConsoleRouteIconKey } from '@/router/routeMeta';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import { ClientBackLink } from '../ClientBackLink';
import './AdminLayout.css';
import { useTranslation } from 'react-i18next';
import { LanguageSwitcher } from '@/i18n/LanguageSwitcher';

const { Header, Sider, Content } = Layout;

const MOBILE_SIDEBAR_MEDIA_QUERY = '(max-width: 768px)';
const DESKTOP_SIDEBAR_WIDTH = 300;
const DESKTOP_COLLAPSED_WIDTH = 88;
const highFrequencyMobileRouteKeys = [
  'dashboard',
  'moderation',
  'experience',
  'orders',
  'products',
  'coins',
  'users',
  'roles',
];

function isMobileSidebarLayout(): boolean {
  if (typeof window === 'undefined') {
    return false;
  }

  return window.matchMedia(MOBILE_SIDEBAR_MEDIA_QUERY).matches;
}

const menuIconMap: Record<ConsoleRouteIconKey, ReactNode> = {
  dashboard: <DashboardOutlined />,
  application: <AppstoreOutlined />,
  product: <ShoppingOutlined />,
  order: <FileTextOutlined />,
  user: <TeamOutlined />,
  role: <SafetyOutlined />,
  taxonomy: <TagsOutlined />,
  document: <FileTextOutlined />,
  sticker: <AppstoreOutlined />,
  moderation: <SafetyOutlined />,
  coin: <WalletOutlined />,
  experience: <TrophyOutlined />,
  setting: <SettingOutlined />,
  task: <ClockCircleOutlined />,
};

export interface AdminLayoutProps {
  children: ReactNode;
}

export function AdminLayout({ children }: AdminLayoutProps) {
  const { t } = useTranslation();
  const [collapsed, setCollapsed] = useState(() => isMobileSidebarLayout());
  const [isMobileLayout, setIsMobileLayout] = useState(() => isMobileSidebarLayout());
  const [searchVisible, setSearchVisible] = useState(false);
  const [mobileFunctionsOpen, setMobileFunctionsOpen] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { user, loading } = useUser();
  const unknownUserLabel = t('common.unknownUser');
  const displayUserName = user
    ? resolveVisibleUserHandle(user, resolveVisibleUserDisplayName(user, unknownUserLabel))
      || resolveVisibleUserDisplayName(user, unknownUserLabel)
    : unknownUserLabel;

  useGlobalSearchHotkey(() => setSearchVisible(true));

  const sidebarGroups = useMemo(
    () => getSidebarRouteGroups(user, t),
    [t, user]
  );
  const sidebarRoutes = useMemo(() => sidebarGroups.flatMap((group) => group.routes), [sidebarGroups]);
  const activeMenuKey = getActiveMenuKey(location.pathname);
  const menuItems = useMemo<NonNullable<MenuProps['items']>>(
    () => sidebarGroups.map((group) => ({
      key: `group:${group.key}`,
      label: collapsed ? undefined : group.label,
      type: 'group',
      children: group.routes.map((route) => ({
        key: route.key,
        icon: route.iconKey ? menuIconMap[route.iconKey] : undefined,
        label: route.badgeText ? (
          <span className="admin-menu-label">
            <span>{route.title}</span>
            <span className={`admin-menu-badge admin-menu-badge--${route.badgeTone ?? 'neutral'}`}>
              {route.badgeText}
            </span>
          </span>
        ) : route.title,
      })),
    })),
    [collapsed, sidebarGroups]
  );
  const mobileNavItems = useMemo(() => {
    const findRoute = (routeKeys: string[]) =>
      routeKeys.map((key) => sidebarRoutes.find((route) => route.key === key)).find(Boolean);

    const definitions = [
      {
        key: 'overview',
        label: t('console.mobile.overview'),
        icon: <DashboardOutlined />,
        routeKeys: ['dashboard'],
      },
      {
        key: 'governance',
        label: t('console.mobile.governance'),
        icon: <SafetyOutlined />,
        routeKeys: ['moderation', 'experience'],
      },
      {
        key: 'commerce',
        label: t('console.mobile.commerce'),
        icon: <ShoppingOutlined />,
        routeKeys: ['orders', 'products', 'coins'],
      },
      {
        key: 'access',
        label: t('console.mobile.access'),
        icon: <TeamOutlined />,
        routeKeys: ['users', 'roles'],
      },
    ];

    return [
      ...definitions.map((definition) => {
        const route = findRoute(definition.routeKeys);
        return {
          ...definition,
          path: route?.path,
          active: definition.routeKeys.includes(activeMenuKey),
          disabled: !route,
          isMore: false,
        };
      }),
      {
        key: 'more',
        label: t('console.mobile.more'),
        icon: <AppstoreOutlined />,
        path: undefined,
        active: Boolean(activeMenuKey) && !highFrequencyMobileRouteKeys.includes(activeMenuKey),
        disabled: sidebarRoutes.length === 0,
        isMore: true,
      },
    ];
  }, [activeMenuKey, sidebarRoutes, t]);

  const userMenuItems: MenuProps['items'] = [
    {
      key: 'profile',
      icon: <UserOutlined />,
      label: t('console.user.profile'),
    },
    {
      key: 'settings',
      icon: <SettingOutlined />,
      label: t('console.user.settings'),
    },
    {
      type: 'divider',
    },
    {
      key: 'logout',
      icon: <LogoutOutlined />,
      label: t('console.user.logout'),
      danger: true,
    },
  ];

  const handleToggle = () => {
    if (isMobileLayout) {
      setMobileFunctionsOpen(true);
      return;
    }

    setCollapsed(!collapsed);
  };

  const handleNavigate = (path: string) => {
    navigate(path);
    setMobileFunctionsOpen(false);
  };

  const handleMobileNavClick = (item: (typeof mobileNavItems)[number]) => {
    if (item.disabled) {
      return;
    }

    if (item.isMore) {
      setMobileFunctionsOpen(true);
      return;
    }

    if (item.path) {
      handleNavigate(item.path);
    }
  };

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    const mediaQuery = window.matchMedia(MOBILE_SIDEBAR_MEDIA_QUERY);
    const syncMobileLayout = () => {
      const matches = mediaQuery.matches;
      setIsMobileLayout(matches);
      if (matches) {
        setCollapsed(true);
      }
    };

    syncMobileLayout();
    mediaQuery.addEventListener('change', syncMobileLayout);

    return () => {
      mediaQuery.removeEventListener('change', syncMobileLayout);
    };
  }, []);

  useEffect(() => {
    setMobileFunctionsOpen(false);
  }, [location.pathname]);

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
        width={DESKTOP_SIDEBAR_WIDTH}
        collapsedWidth={DESKTOP_COLLAPSED_WIDTH}
        className="admin-sider"
      >
        <div className="admin-logo">
          <span className="admin-logo-mark">R</span>
          {!collapsed ? (
              <span className="admin-logo-copy">
                <span className="admin-logo-title">Radish Console</span>
                <span className="admin-logo-subtitle">{t('console.brand.subtitle')}</span>
              </span>
          ) : null}
        </div>
        <Menu
          theme="light"
          mode="inline"
          selectedKeys={[getActiveMenuKey(location.pathname)]}
          items={menuItems}
          onClick={handleMenuClick}
        />
      </Sider>
      <Layout className={collapsed ? 'collapsed' : ''}>
        <Header className="admin-header">
          <div className="admin-header-left">
            {isMobileLayout ? (
              <AppstoreOutlined
                className="admin-trigger"
                onClick={handleToggle}
              />
            ) : (
              collapsed ? (
                <MenuUnfoldOutlined
                  className="admin-trigger"
                  onClick={handleToggle}
                />
              ) : (
                <MenuFoldOutlined
                  className="admin-trigger"
                  onClick={handleToggle}
                />
              )
            )}
            <ClientBackLink />
          </div>
          <div className="admin-header-right">
            <LanguageSwitcher />
            <SearchOutlined
              className="admin-search-icon"
              onClick={() => setSearchVisible(true)}
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
                  {loading ? t('common.loading') : displayUserName}
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

      {isMobileLayout ? (
        <nav className="admin-mobile-nav" aria-label={t('console.mobile.navLabel')}>
          {mobileNavItems.map((item) => (
            <button
              key={item.key}
              className={[
                'admin-mobile-nav__item',
                item.active || (item.isMore && mobileFunctionsOpen) ? 'admin-mobile-nav__item--active' : '',
              ].filter(Boolean).join(' ')}
              type="button"
              disabled={item.disabled}
              onClick={() => handleMobileNavClick(item)}
            >
              <span className="admin-mobile-nav__icon">{item.icon}</span>
              <span className="admin-mobile-nav__label">{item.label}</span>
            </button>
          ))}
        </nav>
      ) : null}

      {isMobileLayout && mobileFunctionsOpen ? (
        <div
          className={[
            'admin-mobile-functions',
            'admin-mobile-functions--open',
          ].filter(Boolean).join(' ')}
        >
          <button
            className="admin-mobile-functions__backdrop"
            type="button"
            aria-label={t('console.mobile.closeAll')}
            onClick={() => setMobileFunctionsOpen(false)}
          />
          <section
            className="admin-mobile-functions__panel"
            role="dialog"
            aria-modal="true"
            aria-label={t('console.mobile.allTitle')}
          >
            <div className="admin-mobile-functions__header">
              <div>
                <h2>{t('console.mobile.allTitle')}</h2>
                <p>{t('console.mobile.allDescription')}</p>
              </div>
              <button
                className="admin-mobile-functions__close"
                type="button"
                aria-label={t('console.mobile.closeAll')}
                onClick={() => setMobileFunctionsOpen(false)}
              >
                <CloseOutlined />
              </button>
            </div>
            <div className="admin-mobile-functions__groups">
              {sidebarGroups.map((group) => (
                <section className="admin-mobile-functions__group" key={group.key}>
                  <h3>{group.label}</h3>
                  <div className="admin-mobile-functions__routes">
                    {group.routes.map((route) => (
                      <button
                        key={route.key}
                        className={[
                          'admin-mobile-functions__route',
                          route.key === activeMenuKey ? 'admin-mobile-functions__route--active' : '',
                        ].filter(Boolean).join(' ')}
                        type="button"
                        onClick={() => handleNavigate(route.path)}
                      >
                        <span className="admin-mobile-functions__route-icon">
                          {route.iconKey ? menuIconMap[route.iconKey] : <AppstoreOutlined />}
                        </span>
                        <span className="admin-mobile-functions__route-copy">
                          <span>{route.title}</span>
                          {route.badgeText ? <small>{route.badgeText}</small> : null}
                        </span>
                      </button>
                    ))}
                  </div>
                </section>
              ))}
            </div>
          </section>
        </div>
      ) : null}

      <GlobalSearch visible={searchVisible} onClose={() => setSearchVisible(false)} />
    </Layout>
  );
}
