import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Button,
  Space,
  Tag,
  Table,
  message,
  type TableColumnsType,
} from '@radish/ui';
import { Card, Descriptions, Statistic, Tabs } from 'antd';
import {
  UserOutlined,
  TrophyOutlined,
  WalletOutlined,
  ShoppingOutlined,
  LeftOutlined,
} from '@radish/ui';
import { log } from '@/utils/logger';
import './UserDetail.css';

interface UserDetailData {
  uuid: number;
  userName: string;
  loginName: string;
  email: string;
  phone?: string;
  isEnabled: boolean;
  level: number;
  levelName: string;
  currentExp: number;
  totalExp: number;
  nextLevelExp: number;
  coinBalance: number;
  createTime: string;
  lastLoginTime?: string;
}

interface CoinTransaction {
  id: number;
  amount: number;
  type: string;
  reason: string;
  createTime: string;
}

interface Order {
  id: number;
  orderNo: string;
  productName: string;
  totalPrice: number;
  status: string;
  createTime: string;
}

export const UserDetail = () => {
  const { userId } = useParams<{ userId: string }>();
  const navigate = useNavigate();
  useDocumentTitle('用户详情');

  const [loading, setLoading] = useState(false);
  const [user, setUser] = useState<UserDetailData | null>(null);
  const [coinTransactions, setCoinTransactions] = useState<CoinTransaction[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);

  // 加载用户详情
  const loadUserDetail = async () => {
    try {
      setLoading(true);
      // TODO: 调用用户详情 API
      // 暂时使用模拟数据
      setUser({
        uuid: Number(userId),
        userName: '测试用户',
        loginName: 'testuser',
        email: 'test@example.com',
        phone: '13800138000',
        isEnabled: true,
        level: 5,
        levelName: '筑基期',
        currentExp: 1500,
        totalExp: 5500,
        nextLevelExp: 2000,
        coinBalance: 12345,
        createTime: '2024-01-01 10:00:00',
        lastLoginTime: '2024-01-19 15:30:00',
      });
    } catch (error) {
      log.error('UserDetail', '加载用户详情失败:', error);
      message.error('加载用户详情失败');
    } finally {
      setLoading(false);
    }
  };

  // 加载萝卜币流水
  const loadCoinTransactions = async () => {
    try {
      // TODO: 调用萝卜币流水 API
      setCoinTransactions([
        { id: 1, amount: 100, type: '收入', reason: '发帖奖励', createTime: '2024-01-19 10:00:00' },
        { id: 2, amount: -50, type: '支出', reason: '购买商品', createTime: '2024-01-18 15:30:00' },
      ]);
    } catch (error) {
      log.error('UserDetail', '加载萝卜币流水失败:', error);
    }
  };

  // 加载购买记录
  const loadOrders = async () => {
    try {
      // TODO: 调用订单列表 API
      setOrders([
        { id: 1, orderNo: 'ORD20240119001', productName: '测试商品', totalPrice: 100, status: 'Completed', createTime: '2024-01-19 10:00:00' },
      ]);
    } catch (error) {
      log.error('UserDetail', '加载购买记录失败:', error);
    }
  };

  useEffect(() => {
    if (userId) {
      loadUserDetail();
      loadCoinTransactions();
      loadOrders();
    }
  }, [userId]);

  // 萝卜币流水表格列
  const coinColumns: TableColumnsType<CoinTransaction> = [
    {
      title: '金额',
      dataIndex: 'amount',
      key: 'amount',
      width: 120,
      render: (amount: number) => (
        <span style={{ color: amount > 0 ? '#52c41a' : '#ff4d4f', fontWeight: 'bold' }}>
          {amount > 0 ? '+' : ''}{amount}
        </span>
      ),
    },
    {
      title: '类型',
      dataIndex: 'type',
      key: 'type',
      width: 80,
    },
    {
      title: '原因',
      dataIndex: 'reason',
      key: 'reason',
    },
    {
      title: '时间',
      dataIndex: 'createTime',
      key: 'createTime',
      width: 180,
    },
  ];

  // 订单表格列
  const orderColumns: TableColumnsType<Order> = [
    {
      title: '订单号',
      dataIndex: 'orderNo',
      key: 'orderNo',
      width: 180,
    },
    {
      title: '商品',
      dataIndex: 'productName',
      key: 'productName',
    },
    {
      title: '金额',
      dataIndex: 'totalPrice',
      key: 'totalPrice',
      width: 120,
      render: (price: number) => `${price} 胡萝卜`,
    },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (status: string) => (
        <Tag color={status === 'Completed' ? 'success' : 'default'}>
          {status === 'Completed' ? '已完成' : status}
        </Tag>
      ),
    },
    {
      title: '时间',
      dataIndex: 'createTime',
      key: 'createTime',
      width: 180,
    },
  ];

  if (!user) {
    return <div style={{ padding: '24px' }}>加载中...</div>;
  }

  return (
    <div className="user-detail-page">
      <div className="user-detail-header">
        <Button
          icon={<LeftOutlined />}
          onClick={() => navigate('/users')}
        >
          返回
        </Button>
        <h2>用户详情</h2>
      </div>

      {/* 用户基本信息 */}
      <Card title="基本信息" style={{ marginBottom: '16px' }}>
        <Descriptions column={2}>
          <Descriptions.Item label="用户名">{user.userName}</Descriptions.Item>
          <Descriptions.Item label="登录名">{user.loginName}</Descriptions.Item>
          <Descriptions.Item label="邮箱">{user.email}</Descriptions.Item>
          <Descriptions.Item label="手机">{user.phone || '-'}</Descriptions.Item>
          <Descriptions.Item label="状态">
            <Tag color={user.isEnabled ? 'success' : 'error'}>
              {user.isEnabled ? '启用' : '禁用'}
            </Tag>
          </Descriptions.Item>
          <Descriptions.Item label="注册时间">{user.createTime}</Descriptions.Item>
          <Descriptions.Item label="最后登录">{user.lastLoginTime || '-'}</Descriptions.Item>
        </Descriptions>
      </Card>

      {/* 统计数据 */}
      <div className="user-stats">
        <Card>
          <Statistic
            title="等级"
            value={user.level}
            suffix={user.levelName}
            prefix={<TrophyOutlined />}
            valueStyle={{ color: '#722ed1' }}
          />
        </Card>
        <Card>
          <Statistic
            title="当前经验"
            value={user.currentExp}
            suffix={`/ ${user.nextLevelExp}`}
            prefix={<UserOutlined />}
            valueStyle={{ color: '#1890ff' }}
          />
        </Card>
        <Card>
          <Statistic
            title="总经验"
            value={user.totalExp}
            prefix={<TrophyOutlined />}
            valueStyle={{ color: '#52c41a' }}
          />
        </Card>
        <Card>
          <Statistic
            title="萝卜币余额"
            value={user.coinBalance}
            prefix={<WalletOutlined />}
            valueStyle={{ color: '#faad14' }}
          />
        </Card>
      </div>

      {/* 详细信息标签页 */}
      <Card style={{ marginTop: '16px' }}>
        <Tabs
          items={[
            {
              key: 'coins',
              label: '萝卜币流水',
              children: (
                <Table
                  columns={coinColumns}
                  dataSource={coinTransactions}
                  rowKey="id"
                  pagination={{ pageSize: 10 }}
                />
              ),
            },
            {
              key: 'orders',
              label: '购买记录',
              children: (
                <Table
                  columns={orderColumns}
                  dataSource={orders}
                  rowKey="id"
                  pagination={{ pageSize: 10 }}
                />
              ),
            },
          ]}
        />
      </Card>
    </div>
  );
};
