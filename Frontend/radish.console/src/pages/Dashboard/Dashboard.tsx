import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Space,
  Button,
  Table,
  Tag,
  message,
  type TableColumnsType,
} from '@radish/ui';
import { Card, Statistic } from 'antd';
import {
  ShoppingOutlined,
  TeamOutlined,
  AppstoreOutlined,
  FileTextOutlined,
  PlusOutlined,
  EyeOutlined,
} from '@radish/ui';
import { adminGetOrders, getOrderStatusColor } from '@/api/shopApi';
import { getDashboardStats, type DashboardStatsVo } from '@/api/statisticsApi';
import type { Order } from '@/api/types';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import './Dashboard.css';

export const Dashboard = () => {
  useDocumentTitle('仪表盘');
  const navigate = useNavigate();
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const canViewProducts = usePermission(CONSOLE_PERMISSIONS.productsView);
  const canCreateProduct = usePermission(CONSOLE_PERMISSIONS.productsCreate);
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);
  const canViewApplications = usePermission(CONSOLE_PERMISSIONS.applicationsView);

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
      setStats({
        voTotalUsers: 1234,
        voTotalOrders: 567,
        voTotalProducts: 89,
        voTotalRevenue: 12345,
      });
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
          onClick={() => navigate(`/orders?orderNo=${record.voOrderNo}`)}
        >
          查看
        </Button>
      ),
    },
  ];

  const hasQuickActions = canCreateProduct || canViewOrders || canViewUsers || canViewApplications;

  return (
    <div className="dashboard-page">
      <h2 style={{ marginBottom: '24px' }}>仪表盘</h2>

      <div className="dashboard-stats">
        <Card loading={statsLoading}>
          <Statistic
            title="总用户数"
            value={stats.voTotalUsers}
            prefix={<TeamOutlined />}
            styles={{ content: { color: '#3f8600' } }}
          />
        </Card>
        <Card loading={statsLoading}>
          <Statistic
            title="总订单数"
            value={stats.voTotalOrders}
            prefix={<FileTextOutlined />}
            styles={{ content: { color: '#1890ff' } }}
          />
        </Card>
        <Card loading={statsLoading}>
          <Statistic
            title="商品数量"
            value={stats.voTotalProducts}
            prefix={<ShoppingOutlined />}
            styles={{ content: { color: '#722ed1' } }}
          />
        </Card>
        <Card loading={statsLoading}>
          <Statistic
            title="总收入"
            value={stats.voTotalRevenue}
            suffix="胡萝卜"
            prefix={<AppstoreOutlined />}
            styles={{ content: { color: '#cf1322' } }}
          />
        </Card>
      </div>

      <Card title="快速操作" style={{ marginTop: '24px' }}>
        {hasQuickActions ? (
          <Space wrap>
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
          </Space>
        ) : (
          <div style={{ color: '#8c8c8c' }}>当前账号暂无可用快捷操作。</div>
        )}
      </Card>

      {canViewOrders ? (
        <Card
          title="最近订单"
          extra={
            <Button
              variant="ghost"
              size="small"
              onClick={() => navigate('/orders')}
            >
              查看全部
            </Button>
          }
          style={{ marginTop: '24px' }}
        >
          <Table
            columns={orderColumns}
            dataSource={recentOrders}
            rowKey="voId"
            loading={loading}
            pagination={false}
            size="small"
          />
        </Card>
      ) : null}
    </div>
  );
};
