import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Space,
  Button,
  Table,
  Tag,
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
import type { Order } from '@/api/types';
import { log } from '@/utils/logger';
import './Dashboard.css';

interface DashboardStats {
  totalUsers: number;
  totalOrders: number;
  totalProducts: number;
  totalRevenue: number;
}

export const Dashboard = () => {
  useDocumentTitle('仪表盘');
  const navigate = useNavigate();
  const [stats, setStats] = useState<DashboardStats>({
    totalUsers: 0,
    totalOrders: 0,
    totalProducts: 0,
    totalRevenue: 0,
  });
  const [recentOrders, setRecentOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);

  // 加载统计数据
  const loadStats = async () => {
    try {
      // TODO: 调用统计 API
      // 暂时使用模拟数据
      setStats({
        totalUsers: 1234,
        totalOrders: 567,
        totalProducts: 89,
        totalRevenue: 12345,
      });
    } catch (error) {
      log.error('Dashboard', '加载统计数据失败:', error);
    }
  };

  // 加载最近订单
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
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadStats();
    loadRecentOrders();
  }, []);

  // 订单表格列定义
  const orderColumns: TableColumnsType<Order> = [
    {
      title: '订单号',
      dataIndex: 'orderNo',
      key: 'orderNo',
      width: 180,
    },
    {
      title: '用户',
      dataIndex: 'userName',
      key: 'userName',
      width: 120,
    },
    {
      title: '商品',
      dataIndex: 'productName',
      key: 'productName',
      width: 150,
    },
    {
      title: '金额',
      dataIndex: 'totalPrice',
      key: 'totalPrice',
      width: 100,
      render: (price: number) => `${price} 胡萝卜`,
    },
    {
      title: '状态',
      key: 'status',
      width: 100,
      render: (_, record) => (
        <Tag color={getOrderStatusColor(record.status)}>
          {record.statusDisplay}
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
          onClick={() => navigate(`/orders?orderNo=${record.orderNo}`)}
        >
          查看
        </Button>
      ),
    },
  ];

  return (
    <div className="dashboard-page">
      <h2 style={{ marginBottom: '24px' }}>仪表盘</h2>

      {/* 关键指标卡片 */}
      <div className="dashboard-stats">
        <Card>
          <Statistic
            title="总用户数"
            value={stats.totalUsers}
            prefix={<TeamOutlined />}
            valueStyle={{ color: '#3f8600' }}
          />
        </Card>
        <Card>
          <Statistic
            title="总订单数"
            value={stats.totalOrders}
            prefix={<FileTextOutlined />}
            valueStyle={{ color: '#1890ff' }}
          />
        </Card>
        <Card>
          <Statistic
            title="商品数量"
            value={stats.totalProducts}
            prefix={<ShoppingOutlined />}
            valueStyle={{ color: '#722ed1' }}
          />
        </Card>
        <Card>
          <Statistic
            title="总收入"
            value={stats.totalRevenue}
            suffix="胡萝卜"
            prefix={<AppstoreOutlined />}
            valueStyle={{ color: '#cf1322' }}
          />
        </Card>
      </div>

      {/* 快速操作 */}
      <Card
        title="快速操作"
        style={{ marginTop: '24px' }}
      >
        <Space wrap>
          <Button
            variant="primary"
            icon={<PlusOutlined />}
            onClick={() => navigate('/products')}
          >
            新建商品
          </Button>
          <Button
            icon={<FileTextOutlined />}
            onClick={() => navigate('/orders')}
          >
            查看订单
          </Button>
          <Button
            icon={<TeamOutlined />}
            onClick={() => navigate('/users')}
          >
            用户管理
          </Button>
          <Button
            icon={<AppstoreOutlined />}
            onClick={() => navigate('/applications')}
          >
            应用管理
          </Button>
        </Space>
      </Card>

      {/* 最近订单 */}
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
          rowKey="id"
          loading={loading}
          pagination={false}
          size="small"
        />
      </Card>
    </div>
  );
};
