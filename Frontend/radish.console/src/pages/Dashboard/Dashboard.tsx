import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router';
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
  EyeOutlined,
  SafetyOutlined,
  TrophyOutlined,
  WalletOutlined,
  DashboardOutlined,
  SettingOutlined,
  ClockCircleOutlined,
  TagsOutlined,
  ReloadOutlined,
} from '@radish/ui';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
  ConsoleToolbar,
  type ConsoleStatusTone,
} from '@/components/ConsolePage';
import { adminGetOrders } from '@/api/shopApi';
import { getDashboardStats, type DashboardStatsVo } from '@/api/statisticsApi';
import { buildOrderDetailPath } from '@/pages/Orders/orderListUrlState';
import { getOrderStatusColor, getOrderStatusLabel } from '@/pages/Orders/orderPresentation';
import type { Order } from '@/api/types';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import { useUser } from '@/hooks/useUser';
import { getSidebarRouteGroups, type ConsoleRouteIconKey } from '@/router/routeMeta';
import { getLocalizedApiErrorMessage } from '@/utils/apiErrorMessage';
import { log } from '@/utils/logger';
import '../adminFeature.css';
import './Dashboard.css';

type DashboardReadState = 'loading' | 'ready' | 'unavailable' | 'stale';

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

function getReadStateTone(state: DashboardReadState): ConsoleStatusTone {
  if (state === 'ready') return 'success';
  if (state === 'stale' || state === 'loading') return 'warning';
  return 'danger';
}

export const Dashboard = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('dashboard.title'));
  const navigate = useNavigate();
  const { user } = useUser();
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);
  const canViewModeration = usePermission(CONSOLE_PERMISSIONS.moderationView);
  const canViewExperience = usePermission(CONSOLE_PERMISSIONS.experienceView);
  const canViewDocs = usePermission(CONSOLE_PERMISSIONS.docsView);
  const canViewRoles = usePermission(CONSOLE_PERMISSIONS.rolesView);

  const [stats, setStats] = useState<DashboardStatsVo | null>(null);
  const [statsState, setStatsState] = useState<DashboardReadState>('loading');
  const [recentOrders, setRecentOrders] = useState<Order[] | null>(null);
  const [ordersState, setOrdersState] = useState<DashboardReadState>('loading');
  const statsSnapshot = useRef<DashboardStatsVo | null>(null);
  const recentOrdersSnapshot = useRef<Order[] | null>(null);
  const statsRequestGeneration = useRef(0);
  const ordersRequestGeneration = useRef(0);

  const routeGroups = useMemo(
    () => getSidebarRouteGroups(user, t),
    [t, user]
  );
  const visibleRouteCount = routeGroups.reduce((total, group) => total + group.routes.length, 0);

  const loadStats = useCallback(async () => {
    const requestGeneration = statsRequestGeneration.current + 1;
    const hasCurrentSnapshot = statsSnapshot.current !== null;
    statsRequestGeneration.current = requestGeneration;
    setStatsState('loading');

    try {
      const data = await getDashboardStats(t);
      if (requestGeneration !== statsRequestGeneration.current) return;

      statsSnapshot.current = data;
      setStats(data);
      setStatsState('ready');
    } catch (error) {
      if (requestGeneration !== statsRequestGeneration.current) return;

      log.error('Dashboard', '加载统计数据失败:', error);
      setStatsState(hasCurrentSnapshot ? 'stale' : 'unavailable');
      message.error(getLocalizedApiErrorMessage(error, t, 'dashboard.loadStatsFailed'));
    }
  }, [t]);

  const loadRecentOrders = useCallback(async () => {
    const requestGeneration = ordersRequestGeneration.current + 1;
    const hasCurrentSnapshot = recentOrdersSnapshot.current !== null;
    ordersRequestGeneration.current = requestGeneration;
    setOrdersState('loading');

    try {
      const response = await adminGetOrders({
        pageIndex: 1,
        pageSize: 5,
      }, t);
      if (requestGeneration !== ordersRequestGeneration.current) return;

      recentOrdersSnapshot.current = response.data;
      setRecentOrders(response.data);
      setOrdersState('ready');
    } catch (error) {
      if (requestGeneration !== ordersRequestGeneration.current) return;

      log.error('Dashboard', '加载最近订单失败:', error);
      setOrdersState(hasCurrentSnapshot ? 'stale' : 'unavailable');
      message.error(getLocalizedApiErrorMessage(error, t, 'dashboard.loadOrdersFailed'));
    }
  }, [t]);

  useEffect(() => {
    void loadStats();
  }, [loadStats]);

  useEffect(() => {
    if (!canViewOrders) {
      ordersRequestGeneration.current += 1;
      recentOrdersSnapshot.current = null;
      setRecentOrders(null);
      setOrdersState('ready');
      return;
    }

    void loadRecentOrders();
  }, [canViewOrders, loadRecentOrders]);

  useEffect(() => () => {
    statsRequestGeneration.current += 1;
    ordersRequestGeneration.current += 1;
  }, []);

  const handleRefresh = () => {
    void loadStats();
    if (canViewOrders) void loadRecentOrders();
  };

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
      render: (_: unknown, record) => record.voUserName || `#${record.voUserId}`,
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
          {getOrderStatusLabel(record, t)}
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

  const taskPathItems = [
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
  const enabledTaskPathCount = taskPathItems.filter((item) => item.enabled).length;
  const isRefreshing = statsState === 'loading' || (canViewOrders && ordersState === 'loading');
  const metricValue = (value: number | undefined) => (
    value === undefined ? t(statsState === 'loading' ? 'common.loading' : 'dashboard.read.noSnapshot') : formatLocalizedNumber(value, language)
  );

  const renderReadNotice = (
    resource: 'stats' | 'orders',
    state: DashboardReadState,
    retry: () => void,
  ) => {
    if (state !== 'stale' && state !== 'unavailable') return null;

    return (
      <div className={`console-resource-list-notice console-resource-list-notice--${state} dashboard-read-notice`} role="alert">
        <div>
          <strong>{t(`dashboard.${resource}.${state}Title`)}</strong>
          <span>{t(`dashboard.${resource}.${state}Description`)}</span>
        </div>
        <Button size="small" onClick={retry}>{t('dashboard.retry')}</Button>
      </div>
    );
  };

  return (
    <div className="admin-feature-page dashboard-page">
      <ConsolePageHeader
        eyebrow={t('dashboard.eyebrow')}
        icon={<DashboardOutlined />}
        title={t('dashboard.title')}
        description={t('dashboard.description')}
        status={<ConsoleStatusChip tone={enabledTaskPathCount > 0 ? 'success' : 'warning'}>{t('dashboard.highFrequencyCount', { count: enabledTaskPathCount })}</ConsoleStatusChip>}
        actions={(
          <Button icon={<ReloadOutlined />} disabled={isRefreshing} onClick={handleRefresh}>
            {t(isRefreshing ? 'dashboard.refreshing' : 'dashboard.refresh')}
          </Button>
        )}
      />

      <section className="dashboard-dispatch-board">
        <div className="dashboard-section-header">
          <div>
            <h2>{t('dashboard.taskPaths.title')}</h2>
            <p>{t('dashboard.taskPaths.description')}</p>
          </div>
          <Tag>{enabledTaskPathCount > 0 ? t('dashboard.availableCount', { count: enabledTaskPathCount }) : t('dashboard.noPermission')}</Tag>
        </div>
        <div className="dashboard-dispatch-grid">
          {taskPathItems.map((item) => (
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

      <section className="dashboard-metrics-section" aria-labelledby="dashboard-metrics-title">
        <div className="dashboard-section-header">
          <div>
            <h2 id="dashboard-metrics-title">{t('dashboard.metrics.label')}</h2>
            <p>{t('dashboard.metrics.description')}</p>
          </div>
          <ConsoleStatusChip tone={getReadStateTone(statsState)}>{t(`dashboard.read.${statsState}`)}</ConsoleStatusChip>
        </div>
        {renderReadNotice('stats', statsState, () => void loadStats())}
        <ConsoleMetricGrid label={t('dashboard.metrics.label')}>
          <ConsoleMetricCard
            label={t('dashboard.metrics.users')}
            value={metricValue(stats?.voTotalUsers)}
            description={t('dashboard.metrics.usersDescription')}
            tone="info"
          />
          <ConsoleMetricCard
            label={t('dashboard.metrics.orders')}
            value={metricValue(stats?.voTotalOrders)}
            description={t('dashboard.metrics.ordersDescription')}
            tone="warning"
          />
          <ConsoleMetricCard
            label={t('dashboard.metrics.products')}
            value={metricValue(stats?.voTotalProducts)}
            description={t('dashboard.metrics.productsDescription')}
          />
          <ConsoleMetricCard
            label={t('dashboard.metrics.revenue')}
            value={stats ? `${formatLocalizedNumber(stats.voTotalRevenue, language)} ${t('console.unit.carrot')}` : metricValue(undefined)}
            description={t('dashboard.metrics.revenueDescription')}
            tone="success"
          />
        </ConsoleMetricGrid>
      </section>

      {canViewOrders ? (
        <section className="dashboard-orders-panel">
          <div className="dashboard-section-header">
            <div>
              <h2>{t('dashboard.recentOrders.title')}</h2>
              <p>{t('dashboard.recentOrders.description')}</p>
            </div>
            <div className="dashboard-section-actions">
              <ConsoleStatusChip tone={getReadStateTone(ordersState)}>{t(`dashboard.read.${ordersState}`)}</ConsoleStatusChip>
              <Button
                variant="ghost"
                size="small"
                onClick={() => navigate('/orders')}
              >
                {t('dashboard.recentOrders.viewAll')}
              </Button>
            </div>
          </div>
          {renderReadNotice('orders', ordersState, () => void loadRecentOrders())}
          {recentOrders !== null || ordersState === 'loading' ? (
            <div className="dashboard-orders-desktop admin-table-scroll-region">
              <Table
                columns={orderColumns}
                dataSource={recentOrders ?? []}
                rowKey="voId"
                loading={ordersState === 'loading'}
                pagination={false}
                size="small"
                scroll={{ x: 760 }}
              />
            </div>
          ) : null}
          <div className="dashboard-orders-mobile" aria-busy={ordersState === 'loading'}>
            {recentOrders && recentOrders.length > 0 ? recentOrders.map((order) => (
              <article className="console-resource-mobile-card dashboard-order-mobile-card" key={order.voId}>
                <div className="console-resource-mobile-card__header">
                  <div className="console-resource-mobile-card__identity">
                    <strong>{order.voOrderNo}</strong>
                    <span>{order.voUserName || `#${order.voUserId}`}</span>
                  </div>
                  <Tag color={getOrderStatusColor(order.voStatus)}>{getOrderStatusLabel(order, t)}</Tag>
                </div>
                <div className="console-resource-mobile-card__facts">
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('dashboard.table.product')}</span>
                    <strong>{order.voProductName}</strong>
                  </div>
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('dashboard.table.quantity')}</span>
                    <strong>{formatLocalizedNumber(order.voQuantity, language)}</strong>
                  </div>
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('dashboard.table.amount')}</span>
                    <strong>{formatLocalizedNumber(order.voTotalPrice, language)} {t('console.unit.carrot')}</strong>
                  </div>
                </div>
                <div className="console-resource-mobile-card__footer">
                  <span className="dashboard-order-mobile-card__hint">{t('dashboard.recentOrders.sameSnapshot')}</span>
                  <Button size="small" icon={<EyeOutlined />} onClick={() => handleOpenOrderDetail(order)}>
                    {t('dashboard.table.view')}
                  </Button>
                </div>
              </article>
            )) : ordersState === 'loading' ? (
              <div className="console-resource-mobile-loading">{t('dashboard.recentOrders.loading')}</div>
            ) : ordersState === 'ready' || ordersState === 'stale' ? (
              <div className="console-resource-mobile-empty">
                <strong>{t('dashboard.recentOrders.emptyTitle')}</strong>
                <span>{t('dashboard.recentOrders.emptyDescription')}</span>
              </div>
            ) : null}
          </div>
        </section>
      ) : null}

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
    </div>
  );
};
