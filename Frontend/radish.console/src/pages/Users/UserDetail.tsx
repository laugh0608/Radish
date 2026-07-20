import { useCallback, useEffect, useState } from 'react';
import { useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Button,
  Space,
  Tag,
  Table,
  message,
  AntModal as Modal,
  AntInput as Input,
  type TableColumnsType,
  formatLocalizedDateTime,
  formatLocalizedNumber,
} from '@radish/ui';
import { useTranslation } from 'react-i18next';
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
import { formatConsoleInteger, formatConsoleSignedInteger } from '@/utils/localeFormatters';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import { userManagementApi } from '@/api/userManagement';
import { getBalanceByUserId, getTransactionsByUserId, type CoinTransactionVo, type UserBalanceVo } from '@/api/coinAdminApi';
import { getUserExperience, type UserExperienceVo } from '@/api/experienceAdminApi';
import {
  adminGetEntitlementOperations,
  adminGetUserBenefits,
  adminGetOrders,
  adminRevokeBenefit,
} from '@/api/shopApi';
import { buildOrderDetailPath } from '@/pages/Orders/orderListUrlState';
import {
  getBenefitDurationLabel,
  getBenefitSourceLabel,
  getBenefitStatusLabel,
  getBenefitTypeLabel,
  getCoinTransactionTypeLabel,
  getConsumableTypeLabel,
  getOrderStatusColor,
  getOrderStatusLabel,
} from '@/pages/Orders/orderPresentation';
import { buildModerationPath } from '@/pages/Moderation/moderationPageUrlState';
import type { Order, ShopEntitlementOperation, UserBenefit } from '@/api/types';
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
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const { userId } = useParams<{ userId: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const returnTo = normalizeConsoleReturnTo(searchParams.get('returnTo'));
  useDocumentTitle(t('console.route.user-detail'));
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);
  const canViewCoins = usePermission(CONSOLE_PERMISSIONS.coinsView);
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const canViewBenefits = usePermission(CONSOLE_PERMISSIONS.benefitsView);
  const canRevokeBenefits = usePermission(CONSOLE_PERMISSIONS.benefitsRevoke);
  const canViewExperience = usePermission(CONSOLE_PERMISSIONS.experienceView);
  const canViewModeration = usePermission(CONSOLE_PERMISSIONS.moderationView);
  const canReviewModeration = usePermission(CONSOLE_PERMISSIONS.moderationReview);

  const [loading, setLoading] = useState(false);
  const [coinLoading, setCoinLoading] = useState(false);
  const [orderLoading, setOrderLoading] = useState(false);
  const [operationLoading, setOperationLoading] = useState(false);
  const [benefitLoading, setBenefitLoading] = useState(false);
  const [revokeLoading, setRevokeLoading] = useState(false);
  const [user, setUser] = useState<UserDetailData | null>(null);
  const [balance, setBalance] = useState<UserBalanceVo | null>(null);
  const [experience, setExperience] = useState<UserExperienceVo | null>(null);
  const [coinTransactions, setCoinTransactions] = useState<CoinTransactionVo[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);
  const [entitlementOperations, setEntitlementOperations] = useState<ShopEntitlementOperation[]>([]);
  const [benefits, setBenefits] = useState<UserBenefit[]>([]);
  const [revokeTarget, setRevokeTarget] = useState<UserBenefit | null>(null);
  const [revokeReason, setRevokeReason] = useState('');

  const mapUserDetail = useCallback((item: UserListItem): UserDetailData => {
    const displayName = resolveVisibleUserDisplayName(
      item,
      item.uuid ? t('users.common.userFallback', { id: item.uuid }) : '-',
    );

    return {
      uuid: item.uuid,
      displayName,
      displayHandle: resolveVisibleUserHandle(item, displayName) || '-',
      email: item.voUserEmail || '-',
      isEnabled: item.voIsEnable,
      createTime: item.voCreateTime,
      lastLoginTime: item.voUpdateTime,
    };
  }, [t]);

  const formatDisplayTime = (time?: string | null) => {
    if (!time) return '-';

    return formatLocalizedDateTime(time, language);
  };

  const getSignedCoinAmount = (transaction: CoinTransactionVo) => {
    const amount = BigInt(transaction.voAmount);
    if (userId && String(transaction.voFromUserId ?? '') === userId) {
      return -amount;
    }

    return amount;
  };

  const getSignedAmountClassName = (amount: bigint) => (
    amount >= 0n
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
        throw new Error(response.message || t('users.detail.loadFailed'));
      }

      setUser(mapUserDetail(response.data));
    } catch (error) {
      log.error('UserDetail', '加载用户详情失败:', error);
      message.error(error instanceof Error ? error.message : t('users.detail.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [mapUserDetail, t, userId]);

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

  const loadEntitlementOperations = useCallback(async () => {
    if (!userId || !canViewBenefits) return;

    try {
      setOperationLoading(true);
      const result = await adminGetEntitlementOperations({
        userId,
        pageIndex: 1,
        pageSize: 10,
      });
      setEntitlementOperations(result.data);
    } catch (error) {
      log.error('UserDetail', '加载商城权益流水失败:', error);
      setEntitlementOperations([]);
    } finally {
      setOperationLoading(false);
    }
  }, [userId, canViewBenefits]);

  const loadBenefits = useCallback(async () => {
    if (!userId || !canViewBenefits) return;

    try {
      setBenefitLoading(true);
      setBenefits(await adminGetUserBenefits(userId));
    } catch (error) {
      log.error('UserDetail', '加载持续权益失败:', error);
      setBenefits([]);
    } finally {
      setBenefitLoading(false);
    }
  }, [userId, canViewBenefits]);

  const handleRevokeBenefit = async () => {
    if (!revokeTarget) return;

    const normalizedReason = revokeReason.trim();
    if (normalizedReason.length < 2) {
      message.warning(t('users.detail.revokeReasonInvalid'));
      return;
    }

    try {
      setRevokeLoading(true);
      const result = await adminRevokeBenefit(revokeTarget.voId, normalizedReason);
      message.success(t(result.voChanged ? 'users.detail.revokeSuccess' : 'users.detail.alreadyRevoked'));
      setRevokeTarget(null);
      setRevokeReason('');
      await Promise.all([loadBenefits(), loadEntitlementOperations()]);
    } catch (error) {
      log.error('UserDetail', '撤销持续权益失败:', error);
      message.error(error instanceof Error ? error.message : t('users.detail.revokeFailed'));
    } finally {
      setRevokeLoading(false);
    }
  };

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
      void loadEntitlementOperations();
      void loadBenefits();
    }
  }, [
    userId,
    canViewUsers,
    loadBalance,
    loadExperience,
    loadCoinTransactions,
    loadOrders,
    loadEntitlementOperations,
    loadBenefits,
  ]);
  // 萝卜币流水表格列
  const coinColumns: TableColumnsType<CoinTransactionVo> = [
    {
      title: t('users.detail.column.amount'),
      dataIndex: 'voAmount',
      key: 'voAmount',
      width: 120,
      render: (_amount: string, record) => {
        const signedAmount = getSignedCoinAmount(record);

        return (
          <span className={getSignedAmountClassName(signedAmount)}>
            {formatConsoleSignedInteger(signedAmount, language)}
          </span>
        );
      },
    },
    {
      title: t('users.detail.column.type'),
      dataIndex: 'voTransactionType',
      key: 'voTransactionType',
      width: 120,
      render: (transactionType: string) => getCoinTransactionTypeLabel(transactionType, t),
    },
    {
      title: t('users.detail.column.remark'),
      dataIndex: 'voRemark',
      key: 'voRemark',
      render: (remark?: string | null) => remark || '-',
    },
    {
      title: t('users.detail.column.time'),
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (time: string) => formatDisplayTime(time),
    },
    {
      title: t('users.detail.column.action'),
      key: 'action',
      width: 120,
      render: (_: unknown, record) => (
        record.voBusinessType === 'Order' && record.voBusinessId ? (
          <Button onClick={() => handleViewOrderFromTransaction(record)}>
            {t('users.detail.action.viewOrder')}
          </Button>
        ) : '-'
      ),
    },
  ];

  // 订单表格列
  const orderColumns: TableColumnsType<Order> = [
    {
      title: t('users.detail.column.orderNo'),
      dataIndex: 'voOrderNo',
      key: 'voOrderNo',
      width: 180,
    },
    {
      title: t('users.detail.column.product'),
      dataIndex: 'voProductName',
      key: 'voProductName',
    },
    {
      title: t('users.detail.column.amount'),
      dataIndex: 'voTotalPrice',
      key: 'voTotalPrice',
      width: 120,
      render: (price: number) => `${formatLocalizedNumber(price, language)} ${t('console.unit.carrot')}`,
    },
    {
      title: t('users.detail.column.status'),
      dataIndex: 'voStatus',
      key: 'voStatus',
      width: 100,
      render: (_status: string, record) => (
        <Tag color={getOrderStatusColor(record.voStatus)}>
          {getOrderStatusLabel(record, t)}
        </Tag>
      ),
    },
    {
      title: t('users.detail.column.time'),
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (time: string) => formatDisplayTime(time),
    },
    {
      title: t('users.detail.column.action'),
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
            {t('users.detail.action.orderGovernance')}
          </Button>
          {record.voCoinTransactionId ? (
            <Button onClick={() => handleViewCoinTransactionFromOrder(record)}>
              {t('users.detail.action.coinTransaction')}
            </Button>
          ) : null}
        </Space>
      ),
    },
  ];

  const operationColumns: TableColumnsType<ShopEntitlementOperation> = [
    {
      title: t('users.detail.column.businessObject'),
      key: 'businessObject',
      width: 180,
      render: (_: unknown, record) => record.voBenefitType !== null && record.voBenefitType !== undefined
        ? getBenefitTypeLabel(record.voBenefitType, t)
        : record.voConsumableType !== null && record.voConsumableType !== undefined
          ? getConsumableTypeLabel(record.voConsumableType, t)
          : '-',
    },
    {
      title: t('users.detail.column.quantity'),
      dataIndex: 'voQuantity',
      key: 'voQuantity',
      width: 90,
      render: (quantity?: number | null) => quantity ?? '-',
    },
    {
      title: t('users.detail.column.reason'),
      dataIndex: 'voReason',
      key: 'voReason',
      width: 200,
      render: (reason?: string | null) => reason || '-',
    },
    {
      title: t('users.detail.column.effect'),
      key: 'effect',
      render: (_: unknown, record) => (
        <Space wrap>
          <Tag>{record.voEffectType}</Tag>
          <span>{record.voEffectValue || '-'}</span>
        </Space>
      ),
    },
    {
      title: t('users.detail.column.resource'),
      key: 'resource',
      width: 240,
      render: (_: unknown, record) => {
        const resourceReference = record.voEffectResourceNo || record.voEffectResourceId;
        return record.voEffectResourceType
          ? `${record.voEffectResourceType}${resourceReference ? ` · ${resourceReference}` : ''}`
          : '-';
      },
    },
    {
      title: t('users.detail.column.operationId'),
      dataIndex: 'voId',
      key: 'voId',
      width: 190,
    },
    {
      title: t('users.detail.column.time'),
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (time: string) => formatDisplayTime(time),
    },
  ];

  const benefitColumns: TableColumnsType<UserBenefit> = [
    {
      title: t('users.detail.column.benefit'),
      key: 'benefit',
      width: 220,
      render: (_: unknown, record) => (
        <div>
          <strong>{record.voBenefitName || getBenefitTypeLabel(record.voBenefitType, t)}</strong>
          <div className="admin-feature-subtle">{record.voBenefitValue}</div>
        </div>
      ),
    },
    {
      title: t('users.detail.column.status'),
      dataIndex: 'voStatusDisplay',
      key: 'voStatusDisplay',
      width: 110,
      render: (_statusDisplay: string, record) => {
        const status = String(record.voStatus);
        const color = status === '1' || status === 'Active'
          ? 'success'
          : status === '3' || status === 'Revoked'
            ? 'error'
            : status === '2' || status === 'Expired'
              ? 'default'
              : 'processing';
        return <Tag color={color}>{getBenefitStatusLabel(record.voStatus, t)}</Tag>;
      },
    },
    {
      title: t('users.detail.column.duration'),
      dataIndex: 'voDurationDisplay',
      key: 'voDurationDisplay',
      width: 180,
      render: (_: string, record) => getBenefitDurationLabel(record, t, formatDisplayTime),
    },
    {
      title: t('users.detail.column.source'),
      dataIndex: 'voSourceType',
      key: 'voSourceType',
      width: 110,
      render: (sourceType: string) => getBenefitSourceLabel(sourceType, t),
    },
    {
      title: t('users.detail.column.revocation'),
      key: 'revocation',
      width: 240,
      render: (_: unknown, record) => record.voRevokedAt
        ? `${formatDisplayTime(record.voRevokedAt)} · ${record.voRevocationReason || '-'}`
        : '-',
    },
    {
      title: t('users.detail.column.action'),
      key: 'actions',
      width: 110,
      render: (_: unknown, record) => canRevokeBenefits && !record.voRevokedAt ? (
        <Button onClick={() => {
          setRevokeTarget(record);
          setRevokeReason('');
        }}>
          {t('users.detail.action.revoke')}
        </Button>
      ) : '-',
    },
  ];

  if (!canViewUsers) {
    return (
      <div className="admin-feature-page user-detail-page">
        <section className="admin-feature-card">
          <div className="admin-feature-header">
            <div>
              <h2>
                <UserOutlined /> {t('users.detail.title')}
              </h2>
              <p className="admin-feature-subtle">{t('users.detail.noPermission')}</p>
            </div>
            <Button icon={<LeftOutlined />} onClick={handleBack}>
              {t('users.detail.back')}
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
                <UserOutlined /> {t('users.detail.title')}
              </h2>
              <p className="admin-feature-subtle">{t('users.detail.loadingDescription')}</p>
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
              {t('users.detail.back')}
            </Button>
            <div>
              <h2>
                <UserOutlined /> {t('users.detail.title')}
              </h2>
              <p className="admin-feature-subtle">{t('users.detail.description')}</p>
            </div>
          </div>
          <Tag color={user.isEnabled ? 'success' : 'error'}>
            {t(user.isEnabled ? 'users.common.enabled' : 'users.common.disabled')}
          </Tag>
        </div>
      </section>

      <section className="admin-feature-metrics" aria-label={t('users.detail.metricsLabel')}>
        <div className="admin-feature-metric">
          <span><TrophyOutlined /> {t('users.detail.metric.level')}</span>
          <strong>{experience ? `${experience.voCurrentLevel} ${experience.voCurrentLevelName}` : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span><UserOutlined /> {t('users.detail.metric.currentExperience')}</span>
          <strong>{experience ? `${experience.voCurrentExp} / ${experience.voExpToNextLevel}` : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span><TrophyOutlined /> {t('users.detail.metric.totalExperience')}</span>
          <strong>{experience ? formatLocalizedNumber(experience.voTotalExp, language) : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span><WalletOutlined /> {t('users.detail.metric.balance')}</span>
          <strong>{balance ? formatConsoleInteger(balance.voBalance, language) : '--'}</strong>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <section className="admin-table-panel">
            <div className="user-detail-section-title">
              <div>
                <h3>{t('users.detail.basic.title')}</h3>
                <p className="admin-feature-subtle">{t('users.detail.basic.description')}</p>
              </div>
            </div>
            <Descriptions column={2}>
              <Descriptions.Item label={t('users.detail.basic.displayName')}>{user.displayName}</Descriptions.Item>
              <Descriptions.Item label={t('users.detail.basic.handle')}>{user.displayHandle}</Descriptions.Item>
              <Descriptions.Item label={t('users.detail.basic.email')}>{user.email}</Descriptions.Item>
              <Descriptions.Item label={t('users.detail.basic.userId')}>{user.uuid}</Descriptions.Item>
              <Descriptions.Item label={t('users.detail.basic.status')}>
                <Tag color={user.isEnabled ? 'success' : 'error'}>
                  {t(user.isEnabled ? 'users.common.enabled' : 'users.common.disabled')}
                </Tag>
              </Descriptions.Item>
              <Descriptions.Item label={t('users.detail.basic.registeredAt')}>{formatDisplayTime(user.createTime)}</Descriptions.Item>
              <Descriptions.Item label={t('users.detail.basic.lastLogin')}>{formatDisplayTime(user.lastLoginTime)}</Descriptions.Item>
            </Descriptions>
          </section>

          <section className="admin-table-panel">
            <div className="user-detail-section-title">
              <div>
                <h3>{t('users.detail.records.title')}</h3>
                <p className="admin-feature-subtle">{t('users.detail.records.description')}</p>
              </div>
            </div>
            <Tabs
              items={[
                {
                  key: 'coins',
                  label: t('users.detail.tabs.coins'),
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
                      <Empty description={t('users.detail.permission.noCoins')} />
                    )
                  ),
                },
                {
                  key: 'orders',
                  label: t('users.detail.tabs.orders'),
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
                      <Empty description={t('users.detail.permission.noOrders')} />
                    )
                  ),
                },
                {
                  key: 'benefits',
                  label: t('users.detail.tabs.benefits'),
                  children: (
                    canViewBenefits ? (
                      <div className="admin-table-scroll-region">
                        <Table
                          columns={benefitColumns}
                          dataSource={benefits}
                          rowKey="voId"
                          loading={benefitLoading}
                          pagination={{ pageSize: 10 }}
                          scroll={{ x: 1050 }}
                        />
                      </div>
                    ) : (
                      <Empty description={t('users.detail.permission.noBenefits')} />
                    )
                  ),
                },
                {
                  key: 'entitlement-operations',
                  label: t('users.detail.tabs.operations'),
                  children: (
                    canViewBenefits ? (
                      <div className="admin-table-scroll-region">
                        <Table
                          columns={operationColumns}
                          dataSource={entitlementOperations}
                          rowKey="voId"
                          loading={operationLoading}
                          pagination={{ pageSize: 10 }}
                          scroll={{ x: 1050 }}
                        />
                      </div>
                    ) : (
                      <Empty description={t('users.detail.permission.noOperations')} />
                    )
                  ),
                },
              ]}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>{t('users.detail.summary.title')}</h3>
          <p className="admin-feature-subtle">{t('users.detail.summary.description')}</p>
          <div className="user-detail-case-actions">
            {canViewModeration ? (
              <Button icon={<SafetyOutlined />} onClick={handleViewModerationLogs}>
                {t('users.detail.summary.viewModeration')}
              </Button>
            ) : null}
            {canReviewModeration ? (
              <Button variant="primary" icon={<SafetyOutlined />} onClick={handleOpenManualModeration}>
                {t('users.detail.summary.manualModeration')}
              </Button>
            ) : null}
          </div>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('users.detail.summary.userId')}</span>
              <span className="admin-table-summary__value">{user.uuid}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('users.detail.summary.assetPermission')}</span>
              <span className="admin-table-summary__value">{t(canViewCoins ? 'users.detail.summary.canViewAssets' : 'users.detail.summary.noAssets')}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('users.detail.summary.experiencePermission')}</span>
              <span className="admin-table-summary__value">{t(canViewExperience ? 'users.detail.summary.canViewExperience' : 'users.detail.summary.noExperience')}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('users.detail.summary.orderPermission')}</span>
              <span className="admin-table-summary__value">{t(canViewOrders ? 'users.detail.summary.canViewOrders' : 'users.detail.summary.noOrders')}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('users.detail.summary.moderationPermission')}</span>
              <span className="admin-table-summary__value">
                {t(canReviewModeration
                  ? 'users.detail.summary.canModerate'
                  : canViewModeration
                    ? 'users.detail.summary.canViewModeration'
                    : 'users.detail.summary.noModeration')}
              </span>
            </div>
          </div>
        </aside>
      </div>
      <Modal
        title={t('users.detail.revoke.title')}
        open={revokeTarget !== null}
        okText={t('users.detail.revoke.confirm')}
        cancelText={t('users.detail.revoke.cancel')}
        confirmLoading={revokeLoading}
        onOk={() => void handleRevokeBenefit()}
        onCancel={() => {
          if (revokeLoading) return;
          setRevokeTarget(null);
          setRevokeReason('');
        }}
      >
        <p>
          {revokeTarget
            ? t('users.detail.revoke.description', { benefit: revokeTarget.voBenefitName || revokeTarget.voBenefitTypeDisplay })
            : ''}
        </p>
        <Input.TextArea
          rows={4}
          maxLength={500}
          showCount
          value={revokeReason}
          placeholder={t('users.detail.revoke.placeholder')}
          onChange={(event) => setRevokeReason(event.target.value)}
        />
      </Modal>
    </div>
  );
};
