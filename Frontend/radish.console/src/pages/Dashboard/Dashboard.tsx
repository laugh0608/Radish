import { useState, useEffect } from 'react';
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
} from '@radish/ui';
import { adminGetOrders, getOrderStatusColor } from '@/api/shopApi';
import { getDashboardStats, type DashboardStatsVo } from '@/api/statisticsApi';
import { buildOrderDetailPath } from '@/pages/Orders/orderListUrlState';
import type { Order } from '@/api/types';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import '../adminFeature.css';
import './Dashboard.css';

export const Dashboard = () => {
  useDocumentTitle('仪表盘');
  const navigate = useNavigate();
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const canViewProducts = usePermission(CONSOLE_PERMISSIONS.productsView);
  const canCreateProduct = usePermission(CONSOLE_PERMISSIONS.productsCreate);
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);
  const canViewApplications = usePermission(CONSOLE_PERMISSIONS.applicationsView);
  const canViewModeration = usePermission(CONSOLE_PERMISSIONS.moderationView);
  const canViewCoins = usePermission(CONSOLE_PERMISSIONS.coinsView);
  const canViewExperience = usePermission(CONSOLE_PERMISSIONS.experienceView);

  const [stats, setStats] = useState<DashboardStatsVo>({
    voTotalUsers: 0,
    voTotalOrders: 0,
    voTotalProducts: 0,
    voTotalRevenue: 0,
  });
  const [recentOrders, setRecentOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [statsLoading, setStatsLoading] = useState(false);

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

  const hasQuickActions = canCreateProduct || canViewOrders || canViewUsers || canViewApplications;
  const visibleQuickActionCount = [
    canCreateProduct,
    canViewOrders,
    canViewUsers,
    canViewApplications,
    !canCreateProduct && canViewProducts,
  ].filter(Boolean).length;
  const dispatchItems = [
    {
      title: '内容治理',
      description: '举报审核与手动治理动作',
      enabled: canViewModeration,
      path: '/moderation',
      icon: <SafetyOutlined />,
    },
    {
      title: '经验等级',
      description: '经验台账、冻结与复核',
      enabled: canViewExperience,
      path: '/experience',
      icon: <TrophyOutlined />,
    },
    {
      title: '订单管理',
      description: '最近订单与支付状态回看',
      enabled: canViewOrders,
      path: '/orders',
      icon: <FileTextOutlined />,
    },
    {
      title: '胡萝卜管理',
      description: '余额台账与人工调整',
      enabled: canViewCoins,
      path: '/coins',
      icon: <WalletOutlined />,
    },
  ];
  const enabledDispatchCount = dispatchItems.filter((item) => item.enabled).length;

  return (
    <div className="admin-feature-page dashboard-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h2>仪表盘</h2>
            <p className="admin-feature-subtle">汇总 Console 当前可处理的运营入口、订单回看和治理调度状态。</p>
          </div>
          <Tag>{enabledDispatchCount > 0 ? `${enabledDispatchCount} 个调度入口` : '暂无调度权限'}</Tag>
        </div>
      </section>

      <section className="admin-feature-metrics dashboard-metrics" aria-label="仪表盘指标">
        <div className="admin-feature-metric dashboard-metric">
          <span className="dashboard-metric__title">
            <TeamOutlined /> 总用户数
          </span>
          <strong>{statsLoading ? '加载中' : stats.voTotalUsers}</strong>
        </div>
        <div className="admin-feature-metric dashboard-metric">
          <span className="dashboard-metric__title">
            <FileTextOutlined /> 总订单数
          </span>
          <strong>{statsLoading ? '加载中' : stats.voTotalOrders}</strong>
        </div>
        <div className="admin-feature-metric dashboard-metric">
          <span className="dashboard-metric__title">
            <ShoppingOutlined /> 商品数量
          </span>
          <strong>{statsLoading ? '加载中' : stats.voTotalProducts}</strong>
        </div>
        <div className="admin-feature-metric dashboard-metric">
          <span className="dashboard-metric__title">
            <AppstoreOutlined /> 总收入
          </span>
          <strong>{statsLoading ? '加载中' : `${stats.voTotalRevenue} 胡萝卜`}</strong>
        </div>
      </section>

      <div className="admin-overview-layout">
        <main className="admin-overview-main">
          <section className="admin-overview-panel">
            <div className="admin-overview-panel__title">
              <div>
                <h3>快速操作</h3>
                <p className="admin-feature-subtle">按当前账号权限展示常用处理入口。</p>
              </div>
              <Tag>{visibleQuickActionCount > 0 ? `${visibleQuickActionCount} 项可用` : '无可用操作'}</Tag>
            </div>
            {hasQuickActions ? (
              <div className="admin-overview-actions">
                {canCreateProduct ? (
                  <Button
                    variant="primary"
                    icon={<PlusOutlined />}
                    onClick={() => navigate('/products')}
                  >
                    新建商品
                  </Button>
                ) : null}
                {canViewOrders ? (
                  <Button
                    icon={<FileTextOutlined />}
                    onClick={() => navigate('/orders')}
                  >
                    查看订单
                  </Button>
                ) : null}
                {canViewUsers ? (
                  <Button
                    icon={<TeamOutlined />}
                    onClick={() => navigate('/users')}
                  >
                    用户管理
                  </Button>
                ) : null}
                {canViewApplications ? (
                  <Button
                    icon={<AppstoreOutlined />}
                    onClick={() => navigate('/applications')}
                  >
                    应用管理
                  </Button>
                ) : null}
                {!canCreateProduct && canViewProducts ? (
                  <Button
                    icon={<ShoppingOutlined />}
                    onClick={() => navigate('/products')}
                  >
                    商品管理
                  </Button>
                ) : null}
              </div>
            ) : (
              <p className="admin-feature-subtle">当前账号暂无可用快捷操作。</p>
            )}
          </section>

          {canViewOrders ? (
            <section className="admin-overview-panel">
              <div className="admin-overview-panel__title">
                <div>
                  <h3>最近订单</h3>
                  <p className="admin-feature-subtle">用于快速回看最近交易状态和用户反馈入口。</p>
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
        </main>

        <aside className="admin-overview-aside">
          <h3>调度总览</h3>
          <p className="admin-feature-subtle">按治理和运营链路聚合当前账号可进入的处理台。</p>
          <div className="admin-dispatch-list">
            {dispatchItems.map((item) => (
              <button
                key={item.path}
                className="admin-dispatch-item"
                type="button"
                disabled={!item.enabled}
                onClick={() => navigate(item.path)}
              >
                <span className="admin-dispatch-item__icon">{item.icon}</span>
                <span className="admin-dispatch-item__content">
                  <span className="admin-dispatch-item__title">{item.title}</span>
                  <span className="admin-dispatch-item__description">{item.description}</span>
                </span>
                <Tag>{item.enabled ? '可进入' : '无权限'}</Tag>
              </button>
            ))}
          </div>
        </aside>
      </div>
    </div>
  );
};
