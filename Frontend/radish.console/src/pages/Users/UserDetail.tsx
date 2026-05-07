import { useCallback, useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Button,
  Tag,
  Table,
  message,
  type TableColumnsType,
} from '@radish/ui';
import { Card, Descriptions, Empty, Statistic, Tabs } from 'antd';
import {
  UserOutlined,
  TrophyOutlined,
  WalletOutlined,
  LeftOutlined,
} from '@radish/ui';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import { userManagementApi } from '@/api/userManagement';
import { getBalanceByUserId, getTransactionsByUserId, type CoinTransactionVo, type UserBalanceVo } from '@/api/coinAdminApi';
import { getUserExperience, type UserExperienceVo } from '@/api/experienceAdminApi';
import { adminGetOrders, getOrderStatusColor, getOrderStatusDisplay } from '@/api/shopApi';
import type { Order } from '@/api/types';
import type { UserListItem } from '@/types/user';
import './UserDetail.css';

interface UserDetailData {
  uuid: string | number;
  userName: string;
  loginName: string;
  email: string;
  isEnabled: boolean;
  createTime: string;
  lastLoginTime?: string;
}

export const UserDetail = () => {
  const { userId } = useParams<{ userId: string }>();
  const navigate = useNavigate();
  useDocumentTitle('用户详情');
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);
  const canViewCoins = usePermission(CONSOLE_PERMISSIONS.coinsView);
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const canViewExperience = usePermission(CONSOLE_PERMISSIONS.experienceView);

  const [loading, setLoading] = useState(false);
  const [coinLoading, setCoinLoading] = useState(false);
  const [orderLoading, setOrderLoading] = useState(false);
  const [user, setUser] = useState<UserDetailData | null>(null);
  const [balance, setBalance] = useState<UserBalanceVo | null>(null);
  const [experience, setExperience] = useState<UserExperienceVo | null>(null);
  const [coinTransactions, setCoinTransactions] = useState<CoinTransactionVo[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);

  const mapUserDetail = (item: UserListItem): UserDetailData => ({
    uuid: item.uuid,
    userName: item.voUserName || '-',
    loginName: item.voLoginName || '-',
    email: item.voUserEmail || '-',
    isEnabled: item.voIsEnable,
    createTime: item.voCreateTime,
    lastLoginTime: item.voUpdateTime,
  });

  const formatDisplayTime = (time?: string | null) => {
    if (!time) return '-';

    const date = new Date(time);
    if (Number.isNaN(date.getTime())) {
      return time;
    }

    return date.toLocaleString('zh-CN');
  };

  const getSignedCoinAmount = (transaction: CoinTransactionVo) => {
    if (userId && String(transaction.voFromUserId ?? '') === userId) {
      return -transaction.voAmount;
    }

    return transaction.voAmount;
  };

  // 加载用户详情
  const loadUserDetail = useCallback(async () => {
    if (!userId) return;

    try {
      setLoading(true);
      const response = await userManagementApi.getUserById(userId);
      if (!response.ok || !response.data) {
        throw new Error(response.message || '加载用户详情失败');
      }

      setUser(mapUserDetail(response.data));
    } catch (error) {
      log.error('UserDetail', '加载用户详情失败:', error);
      message.error(error instanceof Error ? error.message : '加载用户详情失败');
    } finally {
      setLoading(false);
    }
  }, [userId]);

  const loadBalance = useCallback(async () => {
    if (!userId || !canViewCoins) return;

    try {
      const result = await getBalanceByUserId(userId);
      setBalance(result);
    } catch (error) {
      log.error('UserDetail', '加载萝卜币余额失败:', error);
      setBalance(null);
    }
  }, [userId, canViewCoins]);

  const loadExperience = useCallback(async () => {
    if (!userId || !canViewExperience) return;

    try {
      const result = await getUserExperience(userId);
      setExperience(result);
    } catch (error) {
      log.error('UserDetail', '加载经验信息失败:', error);
      setExperience(null);
    }
  }, [userId, canViewExperience]);

  // 加载萝卜币流水
  const loadCoinTransactions = useCallback(async () => {
    if (!userId || !canViewCoins) return;

    try {
      setCoinLoading(true);
      const result = await getTransactionsByUserId({
        userId,
        pageIndex: 1,
        pageSize: 10,
      });
      setCoinTransactions(result.data);
    } catch (error) {
      log.error('UserDetail', '加载萝卜币流水失败:', error);
      setCoinTransactions([]);
    } finally {
      setCoinLoading(false);
    }
  }, [userId, canViewCoins]);

  // 加载购买记录
  const loadOrders = useCallback(async () => {
    if (!userId || !canViewOrders) return;

    try {
      setOrderLoading(true);
      const result = await adminGetOrders({
        userId,
        pageIndex: 1,
        pageSize: 10,
      });
      setOrders(result.data);
    } catch (error) {
      log.error('UserDetail', '加载购买记录失败:', error);
      setOrders([]);
    } finally {
      setOrderLoading(false);
    }
  }, [userId, canViewOrders]);

  useEffect(() => {
    if (userId && canViewUsers) {
      void loadUserDetail();
    }
  }, [userId, canViewUsers, loadUserDetail]);

  useEffect(() => {
    if (userId && canViewUsers) {
      void loadBalance();
      void loadExperience();
      void loadCoinTransactions();
      void loadOrders();
    }
  }, [userId, canViewUsers, loadBalance, loadExperience, loadCoinTransactions, loadOrders]);
  // 萝卜币流水表格列
  const coinColumns: TableColumnsType<CoinTransactionVo> = [
    {
      title: '金额',
      dataIndex: 'voAmount',
      key: 'voAmount',
      width: 120,
      render: (_amount: number, record) => {
        const signedAmount = getSignedCoinAmount(record);

        return (
          <span style={{ color: signedAmount >= 0 ? '#52c41a' : '#ff4d4f', fontWeight: 'bold' }}>
            {signedAmount >= 0 ? '+' : ''}{signedAmount}
          </span>
        );
      },
    },
    {
      title: '类型',
      dataIndex: 'voTransactionTypeDisplay',
      key: 'voTransactionTypeDisplay',
      width: 120,
    },
    {
      title: '备注',
      dataIndex: 'voRemark',
      key: 'voRemark',
      render: (remark?: string | null) => remark || '-',
    },
    {
      title: '时间',
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (time: string) => formatDisplayTime(time),
    },
  ];

  // 订单表格列
  const orderColumns: TableColumnsType<Order> = [
    {
      title: '订单号',
      dataIndex: 'voOrderNo',
      key: 'voOrderNo',
      width: 180,
    },
    {
      title: '商品',
      dataIndex: 'voProductName',
      key: 'voProductName',
    },
    {
      title: '金额',
      dataIndex: 'voTotalPrice',
      key: 'voTotalPrice',
      width: 120,
      render: (price: number) => `${price} 胡萝卜`,
    },
    {
      title: '状态',
      dataIndex: 'voStatus',
      key: 'voStatus',
      width: 100,
      render: (status: string) => (
        <Tag color={getOrderStatusColor(status)}>
          {getOrderStatusDisplay(status)}
        </Tag>
      ),
    },
    {
      title: '时间',
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (time: string) => formatDisplayTime(time),
    },
  ];

  if (!canViewUsers) {
    return <div style={{ padding: '24px' }}>没有查看用户详情的权限</div>;
  }

  if (loading || !user) {
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
          <Descriptions.Item label="用户 ID">{user.uuid}</Descriptions.Item>
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
            value={experience?.voCurrentLevel ?? '-'}
            suffix={experience?.voCurrentLevelName ?? ''}
            prefix={<TrophyOutlined />}
            styles={{ content: { color: '#722ed1' } }}
          />
        </Card>
        <Card>
          <Statistic
            title="当前经验"
            value={experience?.voCurrentExp ?? '-'}
            suffix={experience ? `/ ${experience.voExpToNextLevel}` : ''}
            prefix={<UserOutlined />}
            styles={{ content: { color: '#1890ff' } }}
          />
        </Card>
        <Card>
          <Statistic
            title="总经验"
            value={experience?.voTotalExp ?? '-'}
            prefix={<TrophyOutlined />}
            styles={{ content: { color: '#52c41a' } }}
          />
        </Card>
        <Card>
          <Statistic
            title="萝卜币余额"
            value={balance?.voBalance ?? '-'}
            prefix={<WalletOutlined />}
            styles={{ content: { color: '#faad14' } }}
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
                canViewCoins ? (
                  <Table
                    columns={coinColumns}
                    dataSource={coinTransactions}
                    rowKey="voId"
                    loading={coinLoading}
                    pagination={{ pageSize: 10 }}
                  />
                ) : (
                  <Empty description="没有查看萝卜币流水的权限" />
                )
              ),
            },
            {
              key: 'orders',
              label: '购买记录',
              children: (
                canViewOrders ? (
                  <Table
                    columns={orderColumns}
                    dataSource={orders}
                    rowKey="voId"
                    loading={orderLoading}
                    pagination={{ pageSize: 10 }}
                  />
                ) : (
                  <Empty description="没有查看购买记录的权限" />
                )
              ),
            },
          ]}
        />
      </Card>
    </div>
  );
};
