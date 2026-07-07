import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Button,
  Table,
  Tag,
  message,
  type TableColumnsType,
} from '@radish/ui';
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
  useDocumentTitle('Console 调度台');
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

  const routeGroups = useMemo(() => getSidebarRouteGroups(user), [user]);
  const visibleRouteCount = routeGroups.reduce((total, group) => total + group.routes.length, 0);

  const loadStats = async () => {
    try {
      setStatsLoading(true);
      const data = await getDashboardStats();
      setStats(data);
    } catch (error) {
      log.error('Dashboard', '加载统计数据失败:', error);
      message.error('加载统计数据失败');
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
      message.error('加载最近订单失败');
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
      title: '订单号',
      dataIndex: 'voOrderNo',
      key: 'voOrderNo',
      width: 180,
    },
    {
      title: '用户',
      dataIndex: 'voUserName',
      key: 'voUserName',
      width: 120,
    },
    {
      title: '商品',
      dataIndex: 'voProductName',
      key: 'voProductName',
      width: 150,
    },
    {
      title: '金额',
      dataIndex: 'voTotalPrice',
      key: 'voTotalPrice',
      width: 100,
      render: (price: number) => `${price} 胡萝卜`,
    },
    {
      title: '状态',
      key: 'status',
      width: 100,
      render: (_, record) => (
        <Tag color={getOrderStatusColor(record.voStatus)}>
          {record.voStatusDisplay}
        </Tag>
      ),
    },
    {
      title: '操作',
      key: 'action',
      width: 80,
      render: (_, record) => (
        <Button
          variant="ghost"
          size="small"
          icon={<EyeOutlined />}
          onClick={() => handleOpenOrderDetail(record)}
        >
          查看
        </Button>
      ),
    },
  ];

  const dispatchItems = [
    {
      title: '内容治理',
      description: '处理举报、审核和内容回看',
      status: '社区信任',
      enabled: canViewModeration,
      path: '/moderation',
      icon: <SafetyOutlined />,
    },
    {
      title: '经验等级',
      description: '复核经验台账、冻结和人工调整',
      status: '贡献激励',
      enabled: canViewExperience,
      path: '/experience',
      icon: <TrophyOutlined />,
    },
    {
      title: '订单复核',
      description: '回看最近交易、履约和权益状态',
      status: '交易保障',
      enabled: canViewOrders,
      path: '/orders',
      icon: <FileTextOutlined />,
    },
    {
      title: '文档治理',
      description: '检查文档状态、权限和发布边界',
      status: '内容资产',
      enabled: canViewDocs,
      path: '/documents',
      icon: <FileTextOutlined />,
    },
    {
      title: '用户风险',
      description: '定位用户、角色和治理上下文',
      status: '账号治理',
      enabled: canViewUsers,
      path: '/users',
      icon: <TeamOutlined />,
    },
    {
      title: '权限配置',
      description: '复核角色权限和高风险开关',
      status: '访问边界',
      enabled: canViewRoles,
      path: '/roles',
      icon: <SafetyOutlined />,
    },
  ];
  const enabledDispatchCount = dispatchItems.filter((item) => item.enabled).length;

  const commandItems = [
    {
      title: '新建商品',
      enabled: canCreateProduct,
      path: '/products',
      icon: <PlusOutlined />,
    },
    {
      title: '商品管理',
      enabled: canViewProducts,
      path: '/products',
      icon: <ShoppingOutlined />,
    },
    {
      title: '胡萝卜台账',
      enabled: canViewCoins,
      path: '/coins',
      icon: <WalletOutlined />,
    },
    {
      title: '应用管理',
      enabled: canViewApplications,
      path: '/applications',
      icon: <AppstoreOutlined />,
    },
    {
      title: '系统设置',
      enabled: canViewSystemConfig,
      path: '/system-config',
      icon: <SettingOutlined />,
    },
  ];
  const enabledCommandItems = commandItems.filter((item) => item.enabled);

  return (
    <div className="admin-feature-page dashboard-page">
      <ConsolePageHeader
        eyebrow="治理总览"
        icon={<DashboardOutlined />}
        title="Console 调度台"
        description="聚合社区治理、交易复核、权限边界和全部功能入口，按当前账号权限组织可处理任务。"
        status={<ConsoleStatusChip tone={enabledDispatchCount > 0 ? 'success' : 'warning'}>{enabledDispatchCount} 个高频入口</ConsoleStatusChip>}
        actions={(
          <>
            {canViewModeration ? (
              <Button icon={<SafetyOutlined />} onClick={() => navigate('/moderation')}>
                内容治理
              </Button>
            ) : null}
            {canViewOrders ? (
              <Button icon={<FileTextOutlined />} onClick={() => navigate('/orders')}>
                订单复核
              </Button>
            ) : null}
          </>
        )}
      />

      <ConsoleMetricGrid label="Console 调度指标">
        <ConsoleMetricCard
          label="用户"
          value={statsLoading ? '加载中' : stats.voTotalUsers}
          description="账号与用户风险排查基数"
          tone="info"
        />
        <ConsoleMetricCard
          label="订单"
          value={statsLoading ? '加载中' : stats.voTotalOrders}
          description="交易复核与权益回看基数"
          tone="warning"
        />
        <ConsoleMetricCard
          label="商品"
          value={statsLoading ? '加载中' : stats.voTotalProducts}
          description="商城运营与上架状态基数"
        />
        <ConsoleMetricCard
          label="收入"
          value={statsLoading ? '加载中' : `${stats.voTotalRevenue} 胡萝卜`}
          description="当前统计口径下的交易收入"
          tone="success"
        />
      </ConsoleMetricGrid>

      <div className="dashboard-dispatch-layout">
        <section className="dashboard-dispatch-board">
          <div className="dashboard-section-header">
            <div>
              <h2>优先处理队列</h2>
              <p>把社区治理、交易保障和权限边界放在首页首屏，减少后台高频操作跳转成本。</p>
            </div>
            <Tag>{enabledDispatchCount > 0 ? `${enabledDispatchCount} 项可进入` : '暂无权限'}</Tag>
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
                <Tag>{item.enabled ? item.status : '无权限'}</Tag>
              </button>
            ))}
          </div>
        </section>

        <aside className="dashboard-command-panel">
          <div className="dashboard-section-header dashboard-section-header--compact">
            <div>
              <h2>命令组</h2>
              <p>保留少量高频动作，不再让快速操作占据首页主体。</p>
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
            <p className="dashboard-empty-copy">当前账号暂无可用命令。</p>
          )}
        </aside>
      </div>

      <ConsoleToolbar
        title="全部功能"
        description="移动端总览和更多入口都会承接这组完整功能面板；桌面端仍保留左侧全量导航。"
        meta={<Tag>{visibleRouteCount} 个入口</Tag>}
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
          <p className="dashboard-empty-copy">当前账号暂无可访问的 Console 功能。</p>
        )}
      </ConsoleToolbar>

      {canViewOrders ? (
        <section className="dashboard-orders-panel">
          <div className="dashboard-section-header">
            <div>
              <h2>最近订单</h2>
              <p>保留交易状态回看能力，辅助处理支付反馈、权益到账和用户咨询。</p>
            </div>
            <Button
              variant="ghost"
              size="small"
              onClick={() => navigate('/orders')}
            >
              查看全部
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
