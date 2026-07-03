import { useCallback, useEffect, useState } from 'react';
import { useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Button,
  Space,
  Tag,
  Table,
  message,
  type TableColumnsType,
} from '@radish/ui';
import { Descriptions, Empty, Tabs } from 'antd';
import {
  UserOutlined,
  TrophyOutlined,
  WalletOutlined,
  LeftOutlined,
  SafetyOutlined,
} from '@radish/ui';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import { normalizeConsoleReturnTo } from '@/utils/returnTo';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import { userManagementApi } from '@/api/userManagement';
import { getBalanceByUserId, getTransactionsByUserId, type CoinTransactionVo, type UserBalanceVo } from '@/api/coinAdminApi';
import { getUserExperience, type UserExperienceVo } from '@/api/experienceAdminApi';
import { adminGetOrders, getOrderStatusColor, getOrderStatusDisplay } from '@/api/shopApi';
import { buildOrderDetailPath } from '@/pages/Orders/orderListUrlState';
import { buildModerationPath } from '@/pages/Moderation/moderationPageUrlState';
import type { Order } from '@/api/types';
import type { UserListItem } from '@/types/user';
import '../adminFeature.css';
import './UserDetail.css';

interface UserDetailData {
  uuid: string;
  displayName: string;
  displayHandle: string;
  email: string;
  isEnabled: boolean;
  createTime: string;
  lastLoginTime?: string;
}

export const UserDetail = () => {
  const { userId } = useParams<{ userId: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const returnTo = normalizeConsoleReturnTo(searchParams.get('returnTo'));
  useDocumentTitle('用户详情');
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);
  const canViewCoins = usePermission(CONSOLE_PERMISSIONS.coinsView);
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const canViewExperience = usePermission(CONSOLE_PERMISSIONS.experienceView);
  const canViewModeration = usePermission(CONSOLE_PERMISSIONS.moderationView);
  const canReviewModeration = usePermission(CONSOLE_PERMISSIONS.moderationReview);

  const [loading, setLoading] = useState(false);
  const [coinLoading, setCoinLoading] = useState(false);
  const [orderLoading, setOrderLoading] = useState(false);
  const [user, setUser] = useState<UserDetailData | null>(null);
  const [balance, setBalance] = useState<UserBalanceVo | null>(null);
  const [experience, setExperience] = useState<UserExperienceVo | null>(null);
  const [coinTransactions, setCoinTransactions] = useState<CoinTransactionVo[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);

  const mapUserDetail = (item: UserListItem): UserDetailData => {
    const displayName = resolveVisibleUserDisplayName(item, item.uuid ? `用户 ${item.uuid}` : '-');

    return {
      uuid: item.uuid,
      displayName,
      displayHandle: resolveVisibleUserHandle(item, displayName) || '-',
      email: item.voUserEmail || '-',
      isEnabled: item.voIsEnable,
      createTime: item.voCreateTime,
      lastLoginTime: item.voUpdateTime,
    };
  };

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

  const getSignedAmountClassName = (amount: number) => (
    amount >= 0
      ? 'user-detail-signed-amount user-detail-signed-amount--positive'
      : 'user-detail-signed-amount user-detail-signed-amount--negative'
  );

  const handleBack = () => {
    if (returnTo) {
      navigate(returnTo);
      return;
    }

    navigate('/users');
  };

  const getCurrentReturnTo = () => `${location.pathname}${location.search}`;

  const handleViewOrderFromTransaction = (transaction: CoinTransactionVo) => {
    if (transaction.voBusinessType !== 'Order' || !transaction.voBusinessId) {
      return;
    }

    navigate(buildOrderDetailPath({
      orderId: String(transaction.voBusinessId),
      returnTo: getCurrentReturnTo(),
    }));
  };

  const handleViewCoinTransactionFromOrder = (order: Order) => {
    const searchParams = new URLSearchParams({
      userId: String(order.voUserId),
      transactionType: 'CONSUME',
      businessType: 'Order',
      businessId: String(order.voId),
      returnTo: getCurrentReturnTo(),
    });

    navigate(`/coins?${searchParams.toString()}`);
  };

  const handleViewModerationLogs = () => {
    if (!userId) {
      return;
    }

    navigate(buildModerationPath({
      section: 'logs',
      targetUserId: userId,
      returnTo: getCurrentReturnTo(),
    }));
  };

  const handleOpenManualModeration = () => {
    if (!userId) {
      return;
    }

    navigate(buildModerationPath({
      section: 'manual',
      targetUserId: userId,
      returnTo: getCurrentReturnTo(),
    }));
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
          <span className={getSignedAmountClassName(signedAmount)}>
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
    {
      title: '操作',
      key: 'action',
      width: 120,
      render: (_: unknown, record) => (
        record.voBusinessType === 'Order' && record.voBusinessId ? (
          <Button onClick={() => handleViewOrderFromTransaction(record)}>
            查看订单
          </Button>
        ) : '-'
      ),
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
    {
      title: '操作',
      key: 'action',
      width: 200,
      render: (_: unknown, record: Order) => (
        <Space wrap>
          <Button onClick={() => {
            navigate(buildOrderDetailPath({
              orderId: String(record.voId),
              returnTo: getCurrentReturnTo(),
            }));
          }}>
            治理详情
          </Button>
          {record.voCoinTransactionId ? (
            <Button onClick={() => handleViewCoinTransactionFromOrder(record)}>
              扣款流水
            </Button>
          ) : null}
        </Space>
      ),
    },
  ];

  if (!canViewUsers) {
    return (
      <div className="admin-feature-page user-detail-page">
        <section className="admin-feature-card">
          <div className="admin-feature-header">
            <div>
              <h2>
                <UserOutlined /> 用户详情
              </h2>
              <p className="admin-feature-subtle">没有查看用户详情的权限。</p>
            </div>
            <Button icon={<LeftOutlined />} onClick={handleBack}>
              返回
            </Button>
          </div>
        </section>
      </div>
    );
  }

  if (loading || !user) {
    return (
      <div className="admin-feature-page user-detail-page">
        <section className="admin-feature-card">
          <div className="admin-feature-header">
            <div>
              <h2>
                <UserOutlined /> 用户详情
              </h2>
              <p className="admin-feature-subtle">加载用户档案、资产和最近行为记录。</p>
            </div>
          </div>
        </section>
      </div>
    );
  }

  return (
    <div className="admin-feature-page user-detail-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div className="user-detail-heading">
            <Button icon={<LeftOutlined />} onClick={handleBack}>
              返回
            </Button>
            <div>
              <h2>
                <UserOutlined /> 用户详情
              </h2>
              <p className="admin-feature-subtle">聚合用户基础资料、经验、胡萝卜余额和最近订单。</p>
            </div>
          </div>
          <Tag color={user.isEnabled ? 'success' : 'error'}>
            {user.isEnabled ? '启用' : '禁用'}
          </Tag>
        </div>
      </section>

      <section className="admin-feature-metrics" aria-label="用户详情指标">
        <div className="admin-feature-metric">
          <span><TrophyOutlined /> 等级</span>
          <strong>{experience ? `${experience.voCurrentLevel} ${experience.voCurrentLevelName}` : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span><UserOutlined /> 当前经验</span>
          <strong>{experience ? `${experience.voCurrentExp} / ${experience.voExpToNextLevel}` : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span><TrophyOutlined /> 总经验</span>
          <strong>{experience?.voTotalExp ?? '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span><WalletOutlined /> 胡萝卜余额</span>
          <strong>{balance?.voBalance ?? '--'}</strong>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <section className="admin-table-panel">
            <div className="user-detail-section-title">
              <div>
                <h3>基本信息</h3>
                <p className="admin-feature-subtle">用户账号基础档案和最近登录时间。</p>
              </div>
            </div>
            <Descriptions column={2}>
              <Descriptions.Item label="展示名称">{user.displayName}</Descriptions.Item>
              <Descriptions.Item label="公开句柄">{user.displayHandle}</Descriptions.Item>
              <Descriptions.Item label="邮箱">{user.email}</Descriptions.Item>
              <Descriptions.Item label="用户 ID">{user.uuid}</Descriptions.Item>
              <Descriptions.Item label="状态">
                <Tag color={user.isEnabled ? 'success' : 'error'}>
                  {user.isEnabled ? '启用' : '禁用'}
                </Tag>
              </Descriptions.Item>
              <Descriptions.Item label="注册时间">{formatDisplayTime(user.createTime)}</Descriptions.Item>
              <Descriptions.Item label="最后登录">{formatDisplayTime(user.lastLoginTime)}</Descriptions.Item>
            </Descriptions>
          </section>

          <section className="admin-table-panel">
            <div className="user-detail-section-title">
              <div>
                <h3>最近记录</h3>
                <p className="admin-feature-subtle">展示最近 10 条胡萝卜流水和购买记录。</p>
              </div>
            </div>
            <Tabs
              items={[
                {
                  key: 'coins',
                  label: '萝卜币流水',
                  children: (
                    canViewCoins ? (
                      <div className="admin-table-scroll-region">
                        <Table
                          columns={coinColumns}
                          dataSource={coinTransactions}
                          rowKey="voId"
                          loading={coinLoading}
                          pagination={{ pageSize: 10 }}
                          scroll={{ x: 760 }}
                        />
                      </div>
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
                      <div className="admin-table-scroll-region">
                        <Table
                          columns={orderColumns}
                          dataSource={orders}
                          rowKey="voId"
                          loading={orderLoading}
                          pagination={{ pageSize: 10 }}
                          scroll={{ x: 900 }}
                        />
                      </div>
                    ) : (
                      <Empty description="没有查看购买记录的权限" />
                    )
                  ),
                },
              ]}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>用户摘要</h3>
          <p className="admin-feature-subtle">用于核对当前用户跨模块数据可见性和返回来源。</p>
          <div className="user-detail-case-actions">
            {canViewModeration ? (
              <Button icon={<SafetyOutlined />} onClick={handleViewModerationLogs}>
                查看治理记录
              </Button>
            ) : null}
            {canReviewModeration ? (
              <Button variant="primary" icon={<SafetyOutlined />} onClick={handleOpenManualModeration}>
                手动治理
              </Button>
            ) : null}
          </div>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">用户 ID</span>
              <span className="admin-table-summary__value">{user.uuid}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">资产权限</span>
              <span className="admin-table-summary__value">{canViewCoins ? '可查看胡萝卜资产' : '无资产查看权限'}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">经验权限</span>
              <span className="admin-table-summary__value">{canViewExperience ? '可查看经验数据' : '无经验查看权限'}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">订单权限</span>
              <span className="admin-table-summary__value">{canViewOrders ? '可查看购买记录' : '无订单查看权限'}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">治理权限</span>
              <span className="admin-table-summary__value">
                {canReviewModeration ? '可执行手动治理' : canViewModeration ? '可查看治理记录' : '无治理查看权限'}
              </span>
            </div>
          </div>
        </aside>
      </div>
    </div>
  );
};
