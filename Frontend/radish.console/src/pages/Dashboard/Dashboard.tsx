import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Button,
  Table,
  Tag,
  message,
  type TableColumnsType,
  formatLocalizedNumber,
} from '@radish/ui';
import { useTranslation } from 'react-i18next';
import {
  ShoppingOutlined,
  TeamOutlined,
  AppstoreOutlined,
  FileTextOutlined,
  PlusOutlined,
  EyeOutlined,
  SafetyOutlined,
  TrophyOutlined,
  WalletOutlined,
  DashboardOutlined,
  SettingOutlined,
  ClockCircleOutlined,
  TagsOutlined,
} from '@radish/ui';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import { adminGetOrders, getOrderStatusColor } from '@/api/shopApi';
import { getDashboardStats, type DashboardStatsVo } from '@/api/statisticsApi';
import { buildOrderDetailPath } from '@/pages/Orders/orderListUrlState';
import type { Order } from '@/api/types';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import { useUser } from '@/hooks/useUser';
import { getSidebarRouteGroups, type ConsoleRouteIconKey } from '@/router/routeMeta';
import { log } from '@/utils/logger';
import '../adminFeature.css';
import './Dashboard.css';

const routeIconMap: Record<ConsoleRouteIconKey, ReactNode> = {
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

export const Dashboard = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('dashboard.title'));
  const navigate = useNavigate();
  const { user } = useUser();
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const canViewProducts = usePermission(CONSOLE_PERMISSIONS.productsView);
  const canCreateProduct = usePermission(CONSOLE_PERMISSIONS.productsCreate);
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);
  const canViewApplications = usePermission(CONSOLE_PERMISSIONS.applicationsView);
  const canViewModeration = usePermission(CONSOLE_PERMISSIONS.moderationView);
  const canViewCoins = usePermission(CONSOLE_PERMISSIONS.coinsView);
  const canViewExperience = usePermission(CONSOLE_PERMISSIONS.experienceView);
  const canViewDocs = usePermission(CONSOLE_PERMISSIONS.docsView);
  const canViewRoles = usePermission(CONSOLE_PERMISSIONS.rolesView);
  const canViewSystemConfig = usePermission(CONSOLE_PERMISSIONS.systemConfigView);

  const [stats, setStats] = useState<DashboardStatsVo>({
    voTotalUsers: 0,
    voTotalOrders: 0,
    voTotalProducts: 0,
    voTotalRevenue: 0,
  });
  const [recentOrders, setRecentOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [statsLoading, setStatsLoading] = useState(false);

  const routeGroups = useMemo(
    () => getSidebarRouteGroups(user),
    [user, i18n.resolvedLanguage]
  );
  const visibleRouteCount = routeGroups.reduce((total, group) => total + group.routes.length, 0);

  const loadStats = async () => {
    try {
      setStatsLoading(true);
      const data = await getDashboardStats();
      setStats(data);
    } catch (error) {
      log.error('Dashboard', '加载统计数据失败:', error);
      message.error(t('dashboard.loadStatsFailed'));
    } finally {
      setStatsLoading(false);
    }
  };

  const loadRecentOrders = async () => {
    try {
      setLoading(true);
      const response = await adminGetOrders({
        pageIndex: 1,
        pageSize: 5,
      });
      setRecentOrders(response.data);
    } catch (error) {
      log.error('Dashboard', '加载最近订单失败:', error);
      message.error(t('dashboard.loadOrdersFailed'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadStats();
  }, []);

  useEffect(() => {
    if (!canViewOrders) {
      setRecentOrders([]);
      setLoading(false);
      return;
    }

    void loadRecentOrders();
  }, [canViewOrders]);

  const handleOpenOrderDetail = (order: Order) => {
    navigate(buildOrderDetailPath({
      orderId: String(order.voId),
      returnTo: '/',
    }));
  };

  const orderColumns: TableColumnsType<Order> = [
    {
      title: t('dashboard.table.orderNo'),
      dataIndex: 'voOrderNo',
      key: 'voOrderNo',
      width: 180,
    },
    {
      title: t('dashboard.table.user'),
      dataIndex: 'voUserName',
      key: 'voUserName',
      width: 120,
    },
    {
      title: t('dashboard.table.product'),
      dataIndex: 'voProductName',
      key: 'voProductName',
      width: 150,
    },
    {
      title: t('dashboard.table.amount'),
      dataIndex: 'voTotalPrice',
      key: 'voTotalPrice',
      width: 100,
      render: (price: number) => `${formatLocalizedNumber(price, language)} ${t('console.unit.carrot')}`,
    },
    {
      title: t('dashboard.table.status'),
      key: 'status',
      width: 100,
      render: (_, record) => (
        <Tag color={getOrderStatusColor(record.voStatus)}>
          {record.voStatusDisplay}
        </Tag>
      ),
    },
    {
      title: t('dashboard.table.action'),
      key: 'action',
      width: 80,
      render: (_, record) => (
        <Button
          variant="ghost"
          size="small"
          icon={<EyeOutlined />}
          onClick={() => handleOpenOrderDetail(record)}
        >
          {t('dashboard.table.view')}
        </Button>
      ),
    },
  ];

  const dispatchItems = [
    {
      title: t('dashboard.dispatch.moderation.title'),
      description: t('dashboard.dispatch.moderation.description'),
      status: t('dashboard.dispatch.moderation.status'),
      enabled: canViewModeration,
      path: '/moderation',
      icon: <SafetyOutlined />,
    },
    {
      title: t('dashboard.dispatch.experience.title'),
      description: t('dashboard.dispatch.experience.description'),
      status: t('dashboard.dispatch.experience.status'),
      enabled: canViewExperience,
      path: '/experience',
      icon: <TrophyOutlined />,
    },
    {
      title: t('dashboard.dispatch.orders.title'),
      description: t('dashboard.dispatch.orders.description'),
      status: t('dashboard.dispatch.orders.status'),
      enabled: canViewOrders,
      path: '/orders',
      icon: <FileTextOutlined />,
    },
    {
      title: t('dashboard.dispatch.documents.title'),
      description: t('dashboard.dispatch.documents.description'),
      status: t('dashboard.dispatch.documents.status'),
      enabled: canViewDocs,
      path: '/documents',
      icon: <FileTextOutlined />,
    },
    {
      title: t('dashboard.dispatch.users.title'),
      description: t('dashboard.dispatch.users.description'),
      status: t('dashboard.dispatch.users.status'),
      enabled: canViewUsers,
      path: '/users',
      icon: <TeamOutlined />,
    },
    {
      title: t('dashboard.dispatch.roles.title'),
      description: t('dashboard.dispatch.roles.description'),
      status: t('dashboard.dispatch.roles.status'),
      enabled: canViewRoles,
      path: '/roles',
      icon: <SafetyOutlined />,
    },
  ];
  const enabledDispatchCount = dispatchItems.filter((item) => item.enabled).length;

  const commandItems = [
    {
      title: t('dashboard.command.createProduct'),
      enabled: canCreateProduct,
      path: '/products',
      icon: <PlusOutlined />,
    },
    {
      title: t('dashboard.command.products'),
      enabled: canViewProducts,
      path: '/products',
      icon: <ShoppingOutlined />,
    },
    {
      title: t('dashboard.command.coins'),
      enabled: canViewCoins,
      path: '/coins',
      icon: <WalletOutlined />,
    },
    {
      title: t('dashboard.command.applications'),
      enabled: canViewApplications,
      path: '/applications',
      icon: <AppstoreOutlined />,
    },
    {
      title: t('dashboard.command.system'),
      enabled: canViewSystemConfig,
      path: '/system-config',
      icon: <SettingOutlined />,
    },
  ];
  const enabledCommandItems = commandItems.filter((item) => item.enabled);

  return (
    <div className="admin-feature-page dashboard-page">
      <ConsolePageHeader
        eyebrow={t('dashboard.eyebrow')}
        icon={<DashboardOutlined />}
        title={t('dashboard.title')}
        description={t('dashboard.description')}
        status={<ConsoleStatusChip tone={enabledDispatchCount > 0 ? 'success' : 'warning'}>{t('dashboard.highFrequencyCount', { count: enabledDispatchCount })}</ConsoleStatusChip>}
        actions={(
          <>
            {canViewModeration ? (
              <Button icon={<SafetyOutlined />} onClick={() => navigate('/moderation')}>
                {t('dashboard.dispatch.moderation.title')}
              </Button>
            ) : null}
            {canViewOrders ? (
              <Button icon={<FileTextOutlined />} onClick={() => navigate('/orders')}>
                {t('dashboard.dispatch.orders.title')}
              </Button>
            ) : null}
          </>
        )}
      />

      <ConsoleMetricGrid label={t('dashboard.metrics.label')}>
        <ConsoleMetricCard
          label={t('dashboard.metrics.users')}
          value={statsLoading ? t('common.loading') : formatLocalizedNumber(stats.voTotalUsers, language)}
          description={t('dashboard.metrics.usersDescription')}
          tone="info"
        />
        <ConsoleMetricCard
          label={t('dashboard.metrics.orders')}
          value={statsLoading ? t('common.loading') : formatLocalizedNumber(stats.voTotalOrders, language)}
          description={t('dashboard.metrics.ordersDescription')}
          tone="warning"
        />
        <ConsoleMetricCard
          label={t('dashboard.metrics.products')}
          value={statsLoading ? t('common.loading') : formatLocalizedNumber(stats.voTotalProducts, language)}
          description={t('dashboard.metrics.productsDescription')}
        />
        <ConsoleMetricCard
          label={t('dashboard.metrics.revenue')}
          value={statsLoading ? t('common.loading') : `${formatLocalizedNumber(stats.voTotalRevenue, language)} ${t('console.unit.carrot')}`}
          description={t('dashboard.metrics.revenueDescription')}
          tone="success"
        />
      </ConsoleMetricGrid>

      <div className="dashboard-dispatch-layout">
        <section className="dashboard-dispatch-board">
          <div className="dashboard-section-header">
            <div>
              <h2>{t('dashboard.priority.title')}</h2>
              <p>{t('dashboard.priority.description')}</p>
            </div>
            <Tag>{enabledDispatchCount > 0 ? t('dashboard.availableCount', { count: enabledDispatchCount }) : t('dashboard.noPermission')}</Tag>
          </div>
          <div className="dashboard-dispatch-grid">
            {dispatchItems.map((item) => (
              <button
                key={item.title}
                className="dashboard-dispatch-card"
                type="button"
                disabled={!item.enabled}
                onClick={() => navigate(item.path)}
              >
                <span className="dashboard-dispatch-card__icon">{item.icon}</span>
                <span className="dashboard-dispatch-card__copy">
                  <strong>{item.title}</strong>
                  <span>{item.description}</span>
                </span>
                <Tag>{item.enabled ? item.status : t('dashboard.noPermission')}</Tag>
              </button>
            ))}
          </div>
        </section>

        <aside className="dashboard-command-panel">
          <div className="dashboard-section-header dashboard-section-header--compact">
            <div>
              <h2>{t('dashboard.commands.title')}</h2>
              <p>{t('dashboard.commands.description')}</p>
            </div>
          </div>
          {enabledCommandItems.length > 0 ? (
            <div className="dashboard-command-list">
              {enabledCommandItems.map((item) => (
                <Button
                  key={item.title}
                  icon={item.icon}
                  onClick={() => navigate(item.path)}
                >
                  {item.title}
                </Button>
              ))}
            </div>
          ) : (
            <p className="dashboard-empty-copy">{t('dashboard.commands.empty')}</p>
          )}
        </aside>
      </div>

      <ConsoleToolbar
        title={t('dashboard.functions.title')}
        description={t('dashboard.functions.description')}
        meta={<Tag>{t('dashboard.entryCount', { count: visibleRouteCount })}</Tag>}
      >
        {routeGroups.length > 0 ? (
          <div className="dashboard-function-grid">
            {routeGroups.map((group) => (
              <section className="dashboard-function-group" key={group.key}>
                <h3>{group.label}</h3>
                <div className="dashboard-function-routes">
                  {group.routes.map((route) => (
                    <button
                      key={route.key}
                      className="dashboard-function-route"
                      type="button"
                      onClick={() => navigate(route.path)}
                    >
                      <span className="dashboard-function-route__icon">
                        {route.iconKey ? routeIconMap[route.iconKey] : <AppstoreOutlined />}
                      </span>
                      <span>{route.title}</span>
                    </button>
                  ))}
                </div>
              </section>
            ))}
          </div>
        ) : (
          <p className="dashboard-empty-copy">{t('dashboard.functions.empty')}</p>
        )}
      </ConsoleToolbar>

      {canViewOrders ? (
        <section className="dashboard-orders-panel">
          <div className="dashboard-section-header">
            <div>
              <h2>{t('dashboard.recentOrders.title')}</h2>
              <p>{t('dashboard.recentOrders.description')}</p>
            </div>
            <Button
              variant="ghost"
              size="small"
              onClick={() => navigate('/orders')}
            >
              {t('dashboard.recentOrders.viewAll')}
            </Button>
          </div>
          <div className="admin-table-scroll-region">
            <Table
              columns={orderColumns}
              dataSource={recentOrders}
              rowKey="voId"
              loading={loading}
              pagination={false}
              size="small"
              scroll={{ x: 760 }}
            />
          </div>
        </section>
      ) : null}
    </div>
  );
};
