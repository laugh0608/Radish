import { useCallback, useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams, useSearchParams } from 'react-router';
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
import { userManagementApi, type ConsoleUserAuthorization } from '@/api/userManagement';
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
  updateTime?: string;
  roleNames: string[];
}

type ResourceReadState = 'loading' | 'ready' | 'unavailable' | 'stale';

interface PageQuery {
  pageIndex: number;
  pageSize: number;
  total: number;
}

const DEFAULT_PAGE_QUERY: PageQuery = { pageIndex: 1, pageSize: 10, total: 0 };

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
  const canViewRoles = usePermission(CONSOLE_PERMISSIONS.rolesView);
  const canViewCoins = usePermission(CONSOLE_PERMISSIONS.coinsView);
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const canViewBenefits = usePermission(CONSOLE_PERMISSIONS.benefitsView);
  const canRevokeBenefits = usePermission(CONSOLE_PERMISSIONS.benefitsRevoke);
  const canViewExperience = usePermission(CONSOLE_PERMISSIONS.experienceView);
  const canViewModeration = usePermission(CONSOLE_PERMISSIONS.moderationView);
  const canReviewModeration = usePermission(CONSOLE_PERMISSIONS.moderationReview);

  const [revokeLoading, setRevokeLoading] = useState(false);
  const [user, setUser] = useState<UserDetailData | null>(null);
  const [authorization, setAuthorization] = useState<ConsoleUserAuthorization | null>(null);
  const [balance, setBalance] = useState<UserBalanceVo | null>(null);
  const [experience, setExperience] = useState<UserExperienceVo | null>(null);
  const [coinTransactions, setCoinTransactions] = useState<CoinTransactionVo[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);
  const [entitlementOperations, setEntitlementOperations] = useState<ShopEntitlementOperation[]>([]);
  const [benefits, setBenefits] = useState<UserBenefit[]>([]);
  const [profileReadState, setProfileReadState] = useState<ResourceReadState>('loading');
  const [authorizationReadState, setAuthorizationReadState] = useState<ResourceReadState>('loading');
  const [balanceReadState, setBalanceReadState] = useState<ResourceReadState>('loading');
  const [experienceReadState, setExperienceReadState] = useState<ResourceReadState>('loading');
  const [coinReadState, setCoinReadState] = useState<ResourceReadState>('loading');
  const [orderReadState, setOrderReadState] = useState<ResourceReadState>('loading');
  const [operationReadState, setOperationReadState] = useState<ResourceReadState>('loading');
  const [benefitReadState, setBenefitReadState] = useState<ResourceReadState>('loading');
  const [coinPage, setCoinPage] = useState<PageQuery>(DEFAULT_PAGE_QUERY);
  const [orderPage, setOrderPage] = useState<PageQuery>(DEFAULT_PAGE_QUERY);
  const [operationPage, setOperationPage] = useState<PageQuery>(DEFAULT_PAGE_QUERY);
  const [revokeTarget, setRevokeTarget] = useState<UserBenefit | null>(null);
  const [revokeReason, setRevokeReason] = useState('');
  const requestSequences = useRef({
    profile: 0,
    authorization: 0,
    balance: 0,
    experience: 0,
    coins: 0,
    orders: 0,
    operations: 0,
    benefits: 0,
  });
  const snapshotKeys = useRef<Partial<Record<keyof typeof requestSequences.current, string>>>({});

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
      updateTime: item.voUpdateTime,
      roleNames: item.voRoleNames,
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

  const handleViewModerationCases = () => {
    if (!userId) {
      return;
    }

    navigate(buildModerationPath({
      keyword: userId,
      returnTo: getCurrentReturnTo(),
    }));
  };

  const loadUserDetail = useCallback(async () => {
    if (!userId) return;

    const requestId = requestSequences.current.profile + 1;
    const hasCurrentSnapshot = snapshotKeys.current.profile === userId;
    requestSequences.current.profile = requestId;
    setProfileReadState('loading');
    if (!hasCurrentSnapshot) setUser(null);

    try {
      const response = await userManagementApi.getUserById(userId);
      if (requestSequences.current.profile !== requestId) return;
      if (!response.ok || !response.data) {
        throw new Error(response.message || t('users.detail.loadFailed'));
      }

      setUser(mapUserDetail(response.data));
      snapshotKeys.current.profile = userId;
      setProfileReadState('ready');
    } catch (error) {
      if (requestSequences.current.profile !== requestId) return;
      log.error('UserDetail', '加载用户详情失败:', error);
      setProfileReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
    }
  }, [mapUserDetail, t, userId]);

  const loadAuthorization = useCallback(async () => {
    if (!userId || !canViewRoles) return;

    const requestId = requestSequences.current.authorization + 1;
    const hasCurrentSnapshot = snapshotKeys.current.authorization === userId;
    requestSequences.current.authorization = requestId;
    setAuthorizationReadState('loading');
    if (!hasCurrentSnapshot) setAuthorization(null);

    try {
      const response = await userManagementApi.getUserAuthorization(userId);
      if (requestSequences.current.authorization !== requestId) return;
      if (!response.ok || !response.data) throw new Error(response.message || t('users.detail.authorization.loadFailed'));
      setAuthorization(response.data);
      snapshotKeys.current.authorization = userId;
      setAuthorizationReadState('ready');
    } catch (error) {
      if (requestSequences.current.authorization !== requestId) return;
      log.error('UserDetail', '加载用户授权快照失败:', error);
      setAuthorizationReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
    }
  }, [canViewRoles, t, userId]);

  const loadBalance = useCallback(async () => {
    if (!userId || !canViewCoins) return;

    const requestId = requestSequences.current.balance + 1;
    const hasCurrentSnapshot = snapshotKeys.current.balance === userId;
    requestSequences.current.balance = requestId;
    setBalanceReadState('loading');
    if (!hasCurrentSnapshot) setBalance(null);

    try {
      const result = await getBalanceByUserId(userId);
      if (requestSequences.current.balance !== requestId) return;
      setBalance(result);
      snapshotKeys.current.balance = userId;
      setBalanceReadState('ready');
    } catch (error) {
      if (requestSequences.current.balance !== requestId) return;
      log.error('UserDetail', '加载萝卜币余额失败:', error);
      setBalanceReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
    }
  }, [userId, canViewCoins]);

  const loadExperience = useCallback(async () => {
    if (!userId || !canViewExperience) return;

    const requestId = requestSequences.current.experience + 1;
    const hasCurrentSnapshot = snapshotKeys.current.experience === userId;
    requestSequences.current.experience = requestId;
    setExperienceReadState('loading');
    if (!hasCurrentSnapshot) setExperience(null);

    try {
      const result = await getUserExperience(userId);
      if (requestSequences.current.experience !== requestId) return;
      setExperience(result);
      snapshotKeys.current.experience = userId;
      setExperienceReadState('ready');
    } catch (error) {
      if (requestSequences.current.experience !== requestId) return;
      log.error('UserDetail', '加载经验信息失败:', error);
      setExperienceReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
    }
  }, [userId, canViewExperience]);

  const loadCoinTransactions = useCallback(async () => {
    if (!userId || !canViewCoins) return;

    const snapshotKey = `${userId}:${coinPage.pageIndex}:${coinPage.pageSize}`;
    const requestId = requestSequences.current.coins + 1;
    const hasCurrentSnapshot = snapshotKeys.current.coins === snapshotKey;
    requestSequences.current.coins = requestId;
    setCoinReadState('loading');
    if (!hasCurrentSnapshot) setCoinTransactions([]);

    try {
      const result = await getTransactionsByUserId({
        userId,
        pageIndex: coinPage.pageIndex,
        pageSize: coinPage.pageSize,
      });
      if (requestSequences.current.coins !== requestId) return;
      if (result.data.length === 0 && result.dataCount > 0 && coinPage.pageIndex > result.pageCount) {
        setCoinPage((current) => ({ ...current, pageIndex: Math.max(1, result.pageCount), total: result.dataCount }));
        return;
      }
      setCoinTransactions(result.data);
      setCoinPage((current) => ({ ...current, total: result.dataCount }));
      snapshotKeys.current.coins = snapshotKey;
      setCoinReadState('ready');
    } catch (error) {
      if (requestSequences.current.coins !== requestId) return;
      log.error('UserDetail', '加载萝卜币流水失败:', error);
      setCoinReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
    }
  }, [canViewCoins, coinPage.pageIndex, coinPage.pageSize, userId]);

  const loadOrders = useCallback(async () => {
    if (!userId || !canViewOrders) return;

    const snapshotKey = `${userId}:${orderPage.pageIndex}:${orderPage.pageSize}`;
    const requestId = requestSequences.current.orders + 1;
    const hasCurrentSnapshot = snapshotKeys.current.orders === snapshotKey;
    requestSequences.current.orders = requestId;
    setOrderReadState('loading');
    if (!hasCurrentSnapshot) setOrders([]);

    try {
      const result = await adminGetOrders({
        userId,
        pageIndex: orderPage.pageIndex,
        pageSize: orderPage.pageSize,
      });
      if (requestSequences.current.orders !== requestId) return;
      if (result.data.length === 0 && result.dataCount > 0 && orderPage.pageIndex > result.pageCount) {
        setOrderPage((current) => ({ ...current, pageIndex: Math.max(1, result.pageCount), total: result.dataCount }));
        return;
      }
      setOrders(result.data);
      setOrderPage((current) => ({ ...current, total: result.dataCount }));
      snapshotKeys.current.orders = snapshotKey;
      setOrderReadState('ready');
    } catch (error) {
      if (requestSequences.current.orders !== requestId) return;
      log.error('UserDetail', '加载购买记录失败:', error);
      setOrderReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
    }
  }, [canViewOrders, orderPage.pageIndex, orderPage.pageSize, userId]);

  const loadEntitlementOperations = useCallback(async () => {
    if (!userId || !canViewBenefits) return;

    const snapshotKey = `${userId}:${operationPage.pageIndex}:${operationPage.pageSize}`;
    const requestId = requestSequences.current.operations + 1;
    const hasCurrentSnapshot = snapshotKeys.current.operations === snapshotKey;
    requestSequences.current.operations = requestId;
    setOperationReadState('loading');
    if (!hasCurrentSnapshot) setEntitlementOperations([]);

    try {
      const result = await adminGetEntitlementOperations({
        userId,
        pageIndex: operationPage.pageIndex,
        pageSize: operationPage.pageSize,
      });
      if (requestSequences.current.operations !== requestId) return;
      if (result.data.length === 0 && result.dataCount > 0 && operationPage.pageIndex > result.pageCount) {
        setOperationPage((current) => ({ ...current, pageIndex: Math.max(1, result.pageCount), total: result.dataCount }));
        return;
      }
      setEntitlementOperations(result.data);
      setOperationPage((current) => ({ ...current, total: result.dataCount }));
      snapshotKeys.current.operations = snapshotKey;
      setOperationReadState('ready');
    } catch (error) {
      if (requestSequences.current.operations !== requestId) return;
      log.error('UserDetail', '加载商城权益流水失败:', error);
      setOperationReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
    }
  }, [canViewBenefits, operationPage.pageIndex, operationPage.pageSize, userId]);

  const loadBenefits = useCallback(async () => {
    if (!userId || !canViewBenefits) return;

    const requestId = requestSequences.current.benefits + 1;
    const hasCurrentSnapshot = snapshotKeys.current.benefits === userId;
    requestSequences.current.benefits = requestId;
    setBenefitReadState('loading');
    if (!hasCurrentSnapshot) setBenefits([]);

    try {
      const result = await adminGetUserBenefits(userId);
      if (requestSequences.current.benefits !== requestId) return;
      setBenefits(result);
      snapshotKeys.current.benefits = userId;
      setBenefitReadState('ready');
    } catch (error) {
      if (requestSequences.current.benefits !== requestId) return;
      log.error('UserDetail', '加载持续权益失败:', error);
      setBenefitReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
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
      if (canViewRoles) void loadAuthorization();
    }
  }, [canViewRoles, canViewUsers, loadAuthorization, loadUserDetail, userId]);

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

  const renderReadNotice = (state: ResourceReadState, retry: () => void) => {
    if (state !== 'stale' && state !== 'unavailable') return null;

    return (
      <div className={`user-detail-read-notice user-detail-read-notice--${state}`} role="alert">
        <span>{t(state === 'stale' ? 'users.detail.read.stale' : 'users.detail.read.unavailable')}</span>
        <Button size="small" onClick={retry}>{t('users.detail.read.retry')}</Button>
      </div>
    );
  };

  const authoritativeRoles = authorization?.voRoleNames ?? user?.roleNames ?? [];

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

  if (!user) {
    return (
      <div className="admin-feature-page user-detail-page">
        <section className="admin-feature-card">
          <div className="admin-feature-header">
            <div>
              <h2>
                <UserOutlined /> {t('users.detail.title')}
              </h2>
              <p className="admin-feature-subtle">
                {t(profileReadState === 'unavailable' ? 'users.detail.unavailableDescription' : 'users.detail.loadingDescription')}
              </p>
            </div>
            {profileReadState === 'unavailable' ? (
              <Space wrap>
                <Button icon={<LeftOutlined />} onClick={handleBack}>{t('users.detail.back')}</Button>
                <Button onClick={() => void loadUserDetail()}>{t('users.detail.read.retry')}</Button>
              </Space>
            ) : null}
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
          <strong>{experienceReadState === 'ready' || experienceReadState === 'stale' ? `${experience?.voCurrentLevel ?? '--'} ${experience?.voCurrentLevelName ?? ''}` : t(`users.detail.read.${experienceReadState}`)}</strong>
        </div>
        <div className="admin-feature-metric">
          <span><UserOutlined /> {t('users.detail.metric.currentExperience')}</span>
          <strong>{experienceReadState === 'ready' || experienceReadState === 'stale' ? (experience ? `${experience.voCurrentExp} / ${experience.voExpToNextLevel}` : '--') : t(`users.detail.read.${experienceReadState}`)}</strong>
        </div>
        <div className="admin-feature-metric">
          <span><TrophyOutlined /> {t('users.detail.metric.totalExperience')}</span>
          <strong>{experienceReadState === 'ready' || experienceReadState === 'stale' ? (experience ? formatLocalizedNumber(experience.voTotalExp, language) : '--') : t(`users.detail.read.${experienceReadState}`)}</strong>
        </div>
        <div className="admin-feature-metric">
          <span><WalletOutlined /> {t('users.detail.metric.balance')}</span>
          <strong>{balanceReadState === 'ready' || balanceReadState === 'stale' ? (balance ? formatConsoleInteger(balance.voBalance, language) : '--') : t(`users.detail.read.${balanceReadState}`)}</strong>
        </div>
      </section>

      <section className="user-detail-source-status" aria-label={t('users.detail.sources.label')}>
        <span>{t('users.detail.sources.profile')} <Tag color={profileReadState === 'ready' ? 'success' : profileReadState === 'stale' ? 'warning' : 'default'}>{t(`users.detail.read.${profileReadState}`)}</Tag></span>
        {canViewRoles ? <span>{t('users.detail.sources.authorization')} <Tag color={authorizationReadState === 'ready' ? 'success' : authorizationReadState === 'stale' ? 'warning' : 'default'}>{t(`users.detail.read.${authorizationReadState}`)}</Tag></span> : null}
        {canViewCoins ? <span>{t('users.detail.sources.assets')} <Tag color={balanceReadState === 'ready' ? 'success' : balanceReadState === 'stale' ? 'warning' : 'default'}>{t(`users.detail.read.${balanceReadState}`)}</Tag></span> : null}
        {canViewExperience ? <span>{t('users.detail.sources.experience')} <Tag color={experienceReadState === 'ready' ? 'success' : experienceReadState === 'stale' ? 'warning' : 'default'}>{t(`users.detail.read.${experienceReadState}`)}</Tag></span> : null}
      </section>
      <div className="user-detail-metric-notices">
        {canViewCoins ? renderReadNotice(balanceReadState, () => void loadBalance()) : null}
        {canViewExperience ? renderReadNotice(experienceReadState, () => void loadExperience()) : null}
      </div>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <section className="admin-table-panel">
            {renderReadNotice(profileReadState, () => void loadUserDetail())}
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
              <Descriptions.Item label={t('users.detail.basic.updatedAt')}>{formatDisplayTime(user.updateTime)}</Descriptions.Item>
              <Descriptions.Item label={t('users.detail.basic.roles')}>
                <Space size="small" wrap>
                  {authoritativeRoles.length > 0
                    ? authoritativeRoles.map((roleName) => <Tag key={roleName}>{roleName}</Tag>)
                    : t('users.list.noRoles')}
                </Space>
              </Descriptions.Item>
            </Descriptions>
            {canViewRoles ? renderReadNotice(authorizationReadState, () => void loadAuthorization()) : null}
            {canViewRoles && authorization ? (
              <div className="user-detail-permission-summary">
                <span>{t('users.detail.authorization.permissionCount', { count: authorization.voPermissionKeys.length })}</span>
                <div>{authorization.voPermissionKeys.map((permissionKey) => <code key={permissionKey}>{permissionKey}</code>)}</div>
              </div>
            ) : null}
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
                        {renderReadNotice(coinReadState, () => void loadCoinTransactions())}
                        <Table
                          columns={coinColumns}
                          dataSource={coinTransactions}
                          rowKey="voId"
                          loading={coinReadState === 'loading'}
                          pagination={{
                            current: coinPage.pageIndex,
                            pageSize: coinPage.pageSize,
                            total: coinPage.total,
                            showSizeChanger: true,
                            onChange: (pageIndex, pageSize) => setCoinPage({ pageIndex, pageSize, total: coinPage.total }),
                          }}
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
                        {renderReadNotice(orderReadState, () => void loadOrders())}
                        <Table
                          columns={orderColumns}
                          dataSource={orders}
                          rowKey="voId"
                          loading={orderReadState === 'loading'}
                          pagination={{
                            current: orderPage.pageIndex,
                            pageSize: orderPage.pageSize,
                            total: orderPage.total,
                            showSizeChanger: true,
                            onChange: (pageIndex, pageSize) => setOrderPage({ pageIndex, pageSize, total: orderPage.total }),
                          }}
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
                        {renderReadNotice(benefitReadState, () => void loadBenefits())}
                        <Table
                          columns={benefitColumns}
                          dataSource={benefits}
                          rowKey="voId"
                          loading={benefitReadState === 'loading'}
                          pagination={{ pageSize: 10, showSizeChanger: true }}
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
                        {renderReadNotice(operationReadState, () => void loadEntitlementOperations())}
                        <Table
                          columns={operationColumns}
                          dataSource={entitlementOperations}
                          rowKey="voId"
                          loading={operationReadState === 'loading'}
                          pagination={{
                            current: operationPage.pageIndex,
                            pageSize: operationPage.pageSize,
                            total: operationPage.total,
                            showSizeChanger: true,
                            onChange: (pageIndex, pageSize) => setOperationPage({ pageIndex, pageSize, total: operationPage.total }),
                          }}
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
              <Button icon={<SafetyOutlined />} onClick={handleViewModerationCases}>
                {t('users.detail.summary.viewModeration')}
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
