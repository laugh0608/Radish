import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useLocation, useNavigate, useSearchParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import { ApiResponseError } from '@radish/http';
import {
  AntInput as Input,
  AntModal as Modal,
  AntSelect as Select,
  BottomSheet,
  Button,
  Form,
  InputNumber,
  Space,
  Table,
  Tag,
  message,
  type TableColumnsType,
} from '@radish/ui';
import { ReloadOutlined, SearchOutlined, WalletOutlined } from '@radish/ui';
import {
  adminAdjustBalance,
  getBalanceByUserId,
  getTransactionsByUserId,
  type CoinTransactionVo,
  type UserBalanceVo,
} from '@/api/coinAdminApi';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleResourceList,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { buildOrderDetailPath } from '@/pages/Orders/orderListUrlState';
import { getLocalizedApiErrorMessage } from '@/utils/apiErrorMessage';
import {
  formatConsoleDateTime,
  formatConsoleInteger,
  formatConsoleSignedInteger,
} from '@/utils/localeFormatters';
import { log } from '@/utils/logger';
import { normalizeConsoleReturnTo } from '@/utils/returnTo';
import {
  buildCoinAdminSearchParams,
  COIN_TRANSACTION_STATUSES,
  COIN_TRANSACTION_TYPES,
  createAdminAdjustmentIdempotencyKey,
  normalizePositiveLongIdInput,
  parseCoinAdminUrlState,
  type CoinAdminUrlState,
} from './coinAdminUrlState';
import '../adminFeature.css';
import './CoinAdminPage.css';

type ResourceReadState = 'idle' | 'loading' | 'ready' | 'unavailable' | 'stale';

interface CoinAdjustFormValues {
  deltaAmount?: string;
  reason?: string;
}

function normalizeTextFilterInput(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed.slice(0, 50) : undefined;
}

export const CoinAdminPage = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('coins.documentTitle'));
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const urlState = useMemo(() => parseCoinAdminUrlState(searchParams), [searchParams]);
  const returnTo = normalizeConsoleReturnTo(urlState.returnTo ?? null);
  const canAdjust = usePermission(CONSOLE_PERMISSIONS.coinsAdjust);
  const [form] = Form.useForm<CoinAdjustFormValues>();

  const [targetDraft, setTargetDraft] = useState(urlState.userId ?? '');
  const [businessTypeDraft, setBusinessTypeDraft] = useState(urlState.businessType ?? '');
  const [businessIdDraft, setBusinessIdDraft] = useState(urlState.businessId ?? '');
  const [balance, setBalance] = useState<UserBalanceVo | null>(null);
  const [balanceState, setBalanceState] = useState<ResourceReadState>(urlState.userId ? 'loading' : 'idle');
  const [transactions, setTransactions] = useState<CoinTransactionVo[]>([]);
  const [transactionTotal, setTransactionTotal] = useState(0);
  const [transactionState, setTransactionState] = useState<ResourceReadState>(urlState.userId ? 'loading' : 'idle');
  const [balanceLoading, setBalanceLoading] = useState(false);
  const [transactionLoading, setTransactionLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [isDirty, setIsDirty] = useState(false);
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);
  const [refreshGeneration, setRefreshGeneration] = useState(0);
  const [lastTransactionNo, setLastTransactionNo] = useState<string>();
  const balanceRequestGeneration = useRef(0);
  const transactionRequestGeneration = useRef(0);
  const transactionSnapshotKey = useRef<string | undefined>(undefined);
  const balanceRef = useRef<UserBalanceVo | null>(null);
  const idempotencyKey = useRef(createAdminAdjustmentIdempotencyKey());

  const updateUrl = useCallback((nextState: CoinAdminUrlState, replace = false) => {
    setSearchParams(buildCoinAdminSearchParams(nextState), { replace });
  }, [setSearchParams]);

  const resetAdjustmentDraft = useCallback(() => {
    form.setFieldsValue({ deltaAmount: undefined, reason: '' });
    idempotencyKey.current = createAdminAdjustmentIdempotencyKey();
    setIsDirty(false);
    setLastTransactionNo(undefined);
  }, [form]);

  useEffect(() => {
    setTargetDraft(urlState.userId ?? '');
    setBusinessTypeDraft(urlState.businessType ?? '');
    setBusinessIdDraft(urlState.businessId ?? '');
  }, [urlState.businessId, urlState.businessType, urlState.userId]);

  useEffect(() => {
    resetAdjustmentDraft();
  }, [resetAdjustmentDraft, urlState.userId]);

  useEffect(() => {
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!isDirty) return;
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [isDirty]);

  useEffect(() => {
    const userId = urlState.userId;
    const requestGeneration = ++balanceRequestGeneration.current;
    if (!userId) {
      setBalance(null);
      balanceRef.current = null;
      setBalanceState('idle');
      setBalanceLoading(false);
      return;
    }

    const sameTarget = balanceRef.current?.voUserId === userId;
    setBalanceLoading(true);
    setBalanceState('loading');
    if (!sameTarget) {
      setBalance(null);
      balanceRef.current = null;
    }

    void getBalanceByUserId(userId)
      .then((result) => {
        if (requestGeneration !== balanceRequestGeneration.current) return;
        setBalance(result);
        balanceRef.current = result;
        setBalanceState('ready');
      })
      .catch((error: unknown) => {
        if (requestGeneration !== balanceRequestGeneration.current) return;
        log.error('CoinAdminPage', '加载权威胡萝卜余额失败:', error);
        message.error(getLocalizedApiErrorMessage(error, t, 'coins.feedback.loadBalanceFailed'));
        if (sameTarget) {
          setBalanceState('stale');
        } else {
          setBalance(null);
          balanceRef.current = null;
          setBalanceState('unavailable');
        }
      })
      .finally(() => {
        if (requestGeneration === balanceRequestGeneration.current) setBalanceLoading(false);
      });
  }, [refreshGeneration, t, urlState.userId]);

  useEffect(() => {
    const userId = urlState.userId;
    const requestGeneration = ++transactionRequestGeneration.current;
    if (!userId) {
      setTransactions([]);
      setTransactionTotal(0);
      setTransactionState('idle');
      setTransactionLoading(false);
      transactionSnapshotKey.current = undefined;
      return;
    }

    const snapshotKey = JSON.stringify({
      userId,
      pageIndex: urlState.pageIndex,
      pageSize: urlState.pageSize,
      transactionType: urlState.transactionType,
      status: urlState.status,
      businessType: urlState.businessType,
      businessId: urlState.businessId,
    });
    const sameSnapshot = transactionSnapshotKey.current === snapshotKey;
    setTransactionLoading(true);
    setTransactionState('loading');
    if (!sameSnapshot) {
      setTransactions([]);
      setTransactionTotal(0);
    }

    void getTransactionsByUserId({
      userId,
      pageIndex: urlState.pageIndex,
      pageSize: urlState.pageSize,
      transactionType: urlState.transactionType,
      status: urlState.status,
      businessType: urlState.businessType,
      businessId: urlState.businessId,
    })
      .then((result) => {
        if (requestGeneration !== transactionRequestGeneration.current) return;
        setTransactions(result.data);
        setTransactionTotal(result.dataCount);
        setTransactionState('ready');
        transactionSnapshotKey.current = snapshotKey;
      })
      .catch((error: unknown) => {
        if (requestGeneration !== transactionRequestGeneration.current) return;
        log.error('CoinAdminPage', '加载权威胡萝卜流水失败:', error);
        message.error(getLocalizedApiErrorMessage(error, t, 'coins.feedback.loadTransactionsFailed'));
        if (sameSnapshot) {
          setTransactionState('stale');
        } else {
          setTransactions([]);
          setTransactionTotal(0);
          setTransactionState('unavailable');
        }
      })
      .finally(() => {
        if (requestGeneration === transactionRequestGeneration.current) setTransactionLoading(false);
      });
  }, [
    refreshGeneration,
    t,
    urlState.businessId,
    urlState.businessType,
    urlState.pageIndex,
    urlState.pageSize,
    urlState.status,
    urlState.transactionType,
    urlState.userId,
  ]);

  const actionsAreAuthoritative = balanceState === 'ready'
    && balance !== null
    && balance.voUserId === urlState.userId;

  const getSignedCoinAmount = (transaction: CoinTransactionVo) => {
    const amount = BigInt(transaction.voAmount);
    return String(transaction.voFromUserId ?? '') === urlState.userId ? -amount : amount;
  };

  const getSignedAmountClassName = (amount: bigint) => (
    amount >= 0n
      ? 'coin-admin-signed-amount coin-admin-signed-amount--positive'
      : 'coin-admin-signed-amount coin-admin-signed-amount--negative'
  );

  const getStatusColor = (status: string) => {
    if (status === 'SUCCESS') return 'success';
    if (status === 'FAILED') return 'error';
    if (status === 'PENDING') return 'warning';
    return 'default';
  };

  const requestTargetChange = (userId: string) => {
    const apply = () => updateUrl({ ...urlState, userId, pageIndex: 1 });
    if (!isDirty || userId === urlState.userId) {
      if (userId === urlState.userId) setRefreshGeneration((value) => value + 1);
      else apply();
      return;
    }

    Modal.confirm({
      title: t('coins.dirty.targetTitle'),
      content: t('coins.dirty.targetDescription'),
      okText: t('coins.dirty.discard'),
      cancelText: t('coins.dirty.continue'),
      okButtonProps: { danger: true },
      onOk: apply,
    });
  };

  const handleSearchTarget = () => {
    const userId = normalizePositiveLongIdInput(targetDraft);
    if (!userId) {
      message.error(t('coins.form.userIdInvalid'));
      return;
    }
    requestTargetChange(userId);
  };

  const handleRefresh = () => {
    if (!urlState.userId) {
      handleSearchTarget();
      return;
    }
    setRefreshGeneration((value) => value + 1);
  };

  const handleBack = () => {
    if (!returnTo) return;
    if (!isDirty) {
      navigate(returnTo);
      return;
    }

    Modal.confirm({
      title: t('coins.dirty.leaveTitle'),
      content: t('coins.dirty.leaveDescription'),
      okText: t('coins.dirty.leave'),
      cancelText: t('coins.dirty.continue'),
      okButtonProps: { danger: true },
      onOk: () => navigate(returnTo),
    });
  };

  const performAdjustment = async (values: Required<CoinAdjustFormValues>) => {
    if (!canAdjust || !actionsAreAuthoritative || !balance || submitting) {
      message.error(t(!canAdjust ? 'coins.feedback.permissionDenied' : 'coins.feedback.authorityRequired'));
      return;
    }

    try {
      setSubmitting(true);
      const result = await adminAdjustBalance({
        userId: balance.voUserId,
        deltaAmount: String(values.deltaAmount),
        reason: values.reason.trim(),
        expectedVersion: balance.voVersion,
        idempotencyKey: idempotencyKey.current,
      });
      setLastTransactionNo(result.voTransactionNo);
      message.success(t('coins.feedback.adjustedWithNo', { transactionNo: result.voTransactionNo }));
      form.setFieldsValue({ deltaAmount: undefined, reason: '' });
      idempotencyKey.current = createAdminAdjustmentIdempotencyKey();
      setIsDirty(false);
      setRefreshGeneration((value) => value + 1);
    } catch (error) {
      log.error('CoinAdminPage', '调整胡萝卜余额失败:', error);
      if (error instanceof ApiResponseError && [
        'Coin.AdminAdjustVersionConflict',
        'Coin.AdminAdjustProcessing',
        'Coin.AdminAdjustReplayUnavailable',
      ].includes(error.code ?? '')) {
        setBalanceState('stale');
      }
      message.error(getLocalizedApiErrorMessage(error, t, 'coins.feedback.adjustFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleAdjust = async () => {
    if (!canAdjust || !actionsAreAuthoritative || !balance || submitting) {
      message.error(t(!canAdjust ? 'coins.feedback.permissionDenied' : 'coins.feedback.authorityRequired'));
      return;
    }

    let values: CoinAdjustFormValues;
    try {
      values = await form.validateFields();
    } catch {
      return;
    }

    const deltaAmount = String(values.deltaAmount ?? '');
    const reason = values.reason?.trim() ?? '';
    Modal.confirm({
      title: t('coins.confirm.title'),
      content: (
        <div className="coin-admin-confirmation">
          <p>{t('coins.confirm.target', { name: balance.voUserName, userId: balance.voUserId })}</p>
          <p>{t('coins.confirm.version', { version: balance.voVersion })}</p>
          <p>{t('coins.confirm.direction', { direction: t(BigInt(deltaAmount) > 0n ? 'coins.adjust.grant' : 'coins.adjust.deduct') })}</p>
          <p>{t('coins.confirm.amount', { amount: formatConsoleSignedInteger(deltaAmount, language) })}</p>
          <p>{t('coins.confirm.reason', { reason })}</p>
        </div>
      ),
      okText: t('coins.confirm.submit'),
      cancelText: t('coins.confirm.cancel'),
      okButtonProps: { danger: BigInt(deltaAmount) < 0n },
      onOk: () => performAdjustment({ deltaAmount, reason }),
    });
  };

  const updateTransactionQuery = (changes: Partial<CoinAdminUrlState>) => {
    updateUrl({ ...urlState, ...changes, pageIndex: changes.pageIndex ?? 1 });
  };

  const handleBusinessFilterSearch = () => {
    const businessType = normalizeTextFilterInput(businessTypeDraft);
    const businessId = businessIdDraft ? normalizePositiveLongIdInput(businessIdDraft) : undefined;
    if (businessIdDraft && !businessId) {
      message.error(t('coins.transactions.businessIdInvalid'));
      return;
    }
    updateTransactionQuery({ businessType, businessId });
    setFilterSheetOpen(false);
  };

  const clearTransactionFilters = () => {
    setBusinessTypeDraft('');
    setBusinessIdDraft('');
    updateTransactionQuery({
      transactionType: undefined,
      status: undefined,
      businessType: undefined,
      businessId: undefined,
    });
    setFilterSheetOpen(false);
  };

  const handleViewOrderFromTransaction = (transaction: CoinTransactionVo) => {
    if (transaction.voBusinessType !== 'Order' || !transaction.voBusinessId) return;
    navigate(buildOrderDetailPath({
      orderId: String(transaction.voBusinessId),
      userId: urlState.userId,
      returnTo: `${location.pathname}${location.search}`,
    }));
  };

  const transactionTypeOptions = COIN_TRANSACTION_TYPES.map((value) => ({
    value,
    label: t(`coins.type.${value}`),
  }));
  const transactionStatusOptions = COIN_TRANSACTION_STATUSES.map((value) => ({
    value,
    label: t(`coins.status.${value}`),
  }));

  const filterControls = (
    <div className="console-resource-filter-controls console-resource-filter-controls--wide coin-admin-transaction-filters">
      <Select
        value={urlState.transactionType}
        allowClear
        placeholder={t('coins.transactions.typePlaceholder')}
        options={transactionTypeOptions}
        disabled={!urlState.userId}
        onChange={(value) => updateTransactionQuery({ transactionType: value })}
      />
      <Select
        value={urlState.status}
        allowClear
        placeholder={t('coins.transactions.statusPlaceholder')}
        options={transactionStatusOptions}
        disabled={!urlState.userId}
        onChange={(value) => updateTransactionQuery({ status: value })}
      />
      <Input
        value={businessTypeDraft}
        placeholder={t('coins.transactions.businessTypePlaceholder')}
        disabled={!urlState.userId}
        onChange={(event) => setBusinessTypeDraft(event.target.value)}
        onPressEnter={handleBusinessFilterSearch}
      />
      <Input
        value={businessIdDraft}
        placeholder={t('coins.transactions.businessIdPlaceholder')}
        disabled={!urlState.userId}
        onChange={(event) => setBusinessIdDraft(event.target.value.trim())}
        onPressEnter={handleBusinessFilterSearch}
      />
      <div className="console-resource-filter-controls__actions">
        <Button disabled={!urlState.userId} onClick={handleBusinessFilterSearch}>
          {t('coins.actions.filterBusiness')}
        </Button>
        <Button disabled={!urlState.userId} onClick={clearTransactionFilters}>
          {t('coins.actions.clearFilters')}
        </Button>
      </div>
    </div>
  );

  const transactionColumns: TableColumnsType<CoinTransactionVo> = [
    {
      title: t('coins.table.time'), dataIndex: 'voCreateTime', key: 'voCreateTime', width: 180,
      render: (time: string) => formatConsoleDateTime(time, language),
    },
    {
      title: t('coins.table.amount'), dataIndex: 'voAmount', key: 'voAmount', width: 120,
      render: (_amount: string, record) => {
        const amount = getSignedCoinAmount(record);
        return <span className={getSignedAmountClassName(amount)}>{formatConsoleSignedInteger(amount, language)}</span>;
      },
    },
    {
      title: t('coins.table.type'), dataIndex: 'voTransactionType', key: 'voTransactionType', width: 140,
      render: (type: string) => t(`coins.type.${type}`, { defaultValue: type }),
    },
    {
      title: t('coins.table.status'), dataIndex: 'voStatus', key: 'voStatus', width: 100,
      render: (status: string, record) => <Tag color={getStatusColor(status)}>{t(`coins.status.${status}`, { defaultValue: record.voStatusDisplay || status })}</Tag>,
    },
    {
      title: t('coins.table.business'), dataIndex: 'voBusinessType', key: 'voBusinessType', width: 150,
      render: (businessType: string | null | undefined, record) => (
        businessType || record.voBusinessId ? `${businessType || '-'}${record.voBusinessId ? ` #${record.voBusinessId}` : ''}` : '-'
      ),
    },
    {
      title: t('coins.table.operator'), dataIndex: 'voCreateBy', key: 'voCreateBy', width: 150,
      render: (operatorName: string, record) => operatorName || record.voCreateId || '-',
    },
    { title: t('coins.table.transactionNo'), dataIndex: 'voTransactionNo', key: 'voTransactionNo', width: 220 },
    { title: t('coins.table.remark'), dataIndex: 'voRemark', key: 'voRemark', render: (remark?: string | null) => remark || '-' },
    {
      title: t('coins.table.actions'), key: 'action', width: 120,
      render: (_: unknown, record) => record.voBusinessType === 'Order' && record.voBusinessId
        ? <Button size="small" onClick={() => handleViewOrderFromTransaction(record)}>{t('coins.actions.viewOrder')}</Button>
        : '-',
    },
  ];

  const balanceStatus = balanceState === 'ready'
    ? <ConsoleStatusChip tone="success">{t('coins.state.authoritative')}</ConsoleStatusChip>
    : balanceState === 'stale'
      ? <ConsoleStatusChip tone="warning">{t('coins.state.stale')}</ConsoleStatusChip>
      : balanceState === 'unavailable'
        ? <ConsoleStatusChip tone="danger">{t('coins.state.unavailable')}</ConsoleStatusChip>
        : <ConsoleStatusChip tone="info">{t(balanceState === 'idle' ? 'coins.state.idle' : 'coins.state.loading')}</ConsoleStatusChip>;

  const transactionNotice = transactionState === 'stale' || transactionState === 'unavailable'
    ? <div className={`admin-feature-notice admin-feature-notice--${transactionState === 'stale' ? 'warning' : 'danger'}`}><strong>{t(`coins.transactions.${transactionState}Title`)}</strong><span>{t(`coins.transactions.${transactionState}Description`)}</span></div>
    : null;

  const mobileTransactions = (
    <>
      {transactionNotice}
      {transactionLoading && transactions.length === 0 ? <div className="console-resource-mobile-loading">{t('coins.transactions.loading')}</div> : null}
      {transactionState === 'ready' && transactions.length === 0 ? <div className="console-resource-mobile-empty"><strong>{t('coins.transactions.empty')}</strong></div> : null}
      {transactions.map((transaction) => {
        const amount = getSignedCoinAmount(transaction);
        return (
          <article className="console-resource-mobile-card coin-admin-mobile-transaction" key={transaction.voId}>
            <div className="console-resource-mobile-card__header">
              <div className="console-resource-mobile-card__identity">
                <strong className={getSignedAmountClassName(amount)}>{formatConsoleSignedInteger(amount, language)}</strong>
                <span>{t(`coins.type.${transaction.voTransactionType}`, { defaultValue: transaction.voTransactionType })}</span>
              </div>
              <Tag color={getStatusColor(transaction.voStatus)}>{t(`coins.status.${transaction.voStatus}`, { defaultValue: transaction.voStatus })}</Tag>
            </div>
            <div className="console-resource-mobile-card__facts">
              <div className="console-resource-mobile-card__fact"><span>{t('coins.table.time')}</span><strong>{formatConsoleDateTime(transaction.voCreateTime, language)}</strong></div>
              <div className="console-resource-mobile-card__fact"><span>{t('coins.table.business')}</span><strong>{transaction.voBusinessType || '-'}{transaction.voBusinessId ? ` #${transaction.voBusinessId}` : ''}</strong></div>
              <div className="console-resource-mobile-card__fact"><span>{t('coins.table.operator')}</span><strong>{transaction.voCreateBy || transaction.voCreateId || '-'}</strong></div>
            </div>
            <p className="console-resource-mobile-card__description">{transaction.voRemark || t('coins.transactions.noRemark')}</p>
            <div className="console-resource-mobile-card__footer">
              <span className="coin-admin-mobile-transaction__number">{transaction.voTransactionNo}</span>
              {transaction.voBusinessType === 'Order' && transaction.voBusinessId ? <Button size="small" onClick={() => handleViewOrderFromTransaction(transaction)}>{t('coins.actions.viewOrder')}</Button> : null}
            </div>
          </article>
        );
      })}
      {urlState.userId && transactionTotal > 0 ? (
        <div className="console-resource-mobile-pagination">
          <Button size="small" disabled={transactionLoading || urlState.pageIndex <= 1} onClick={() => updateTransactionQuery({ pageIndex: urlState.pageIndex - 1 })}>{t('coins.actions.previous')}</Button>
          <span>{t('coins.transactions.page', { page: urlState.pageIndex, pages: Math.max(1, Math.ceil(transactionTotal / urlState.pageSize)) })}</span>
          <Button size="small" disabled={transactionLoading || urlState.pageIndex * urlState.pageSize >= transactionTotal} onClick={() => updateTransactionQuery({ pageIndex: urlState.pageIndex + 1 })}>{t('coins.actions.next')}</Button>
        </div>
      ) : null}
    </>
  );

  return (
    <div className="admin-feature-page coin-admin-page">
      <ConsolePageHeader
        icon={<WalletOutlined />}
        eyebrow="R3-C04-F"
        title={t('coins.page.title')}
        description={t('coins.page.description')}
        status={balanceStatus}
        actions={(
          <>
            {returnTo ? <Button onClick={handleBack}>{t('coins.actions.back')}</Button> : null}
            <Button icon={<ReloadOutlined />} disabled={balanceLoading || transactionLoading} onClick={handleRefresh}>{t('coins.actions.refresh')}</Button>
          </>
        )}
      />

      <ConsoleToolbar title={t('coins.query.title')} description={t('coins.query.description')} meta={balance ? <Tag>{t('coins.query.userIdentity', { name: balance.voUserName, userId: balance.voUserId })}</Tag> : <Tag>{t('coins.query.notQueried')}</Tag>}>
        <Input className="coin-admin-query-input" placeholder={t('coins.form.userIdPlaceholder')} value={targetDraft} onChange={(event) => setTargetDraft(event.target.value)} onPressEnter={handleSearchTarget} />
        <Button variant="primary" icon={<SearchOutlined />} disabled={balanceLoading} onClick={handleSearchTarget}>{balanceLoading ? t('coins.actions.searching') : t('coins.actions.search')}</Button>
      </ConsoleToolbar>

      {balanceState === 'stale' || balanceState === 'unavailable' ? (
        <div className={`admin-feature-notice admin-feature-notice--${balanceState === 'stale' ? 'warning' : 'danger'}`}>
          <strong>{t(`coins.balance.${balanceState}Title`)}</strong>
          <span>{t(`coins.balance.${balanceState}Description`)}</span>
        </div>
      ) : null}

      <ConsoleMetricGrid label={t('coins.metrics.ariaLabel')}>
        <ConsoleMetricCard label={t('coins.metrics.available')} value={balance ? t('coins.unit', { value: formatConsoleInteger(balance.voBalance, language) }) : '--'} tone="success" />
        <ConsoleMetricCard label={t('coins.metrics.frozen')} value={balance ? t('coins.unit', { value: formatConsoleInteger(balance.voFrozenBalance, language) }) : '--'} tone="warning" />
        <ConsoleMetricCard label={t('coins.metrics.earned')} value={balance ? formatConsoleInteger(balance.voTotalEarned, language) : '--'} />
        <ConsoleMetricCard label={t('coins.metrics.spent')} value={balance ? formatConsoleInteger(balance.voTotalSpent, language) : '--'} />
      </ConsoleMetricGrid>

      <section className="admin-table-panel coin-admin-adjustment-task">
        <div className="coin-admin-section-title">
          <div>
            <h3>{t('coins.adjust.title')}</h3>
            <p className="admin-feature-subtle">{t('coins.adjust.description')}</p>
          </div>
          <Space wrap>
            {isDirty ? <Tag color="warning">{t('coins.adjust.dirty')}</Tag> : null}
            <Tag>{canAdjust ? t('coins.adjust.allowed') : t('coins.adjust.denied')}</Tag>
          </Space>
        </div>
        <div className="coin-admin-authoritative-target">
          <span>{t('coins.adjust.target')}</span>
          <strong>{balance ? t('coins.query.userIdentity', { name: balance.voUserName, userId: balance.voUserId }) : t('coins.adjust.targetRequired')}</strong>
          <small>{balance ? t('coins.adjust.targetSnapshot', { balance: formatConsoleInteger(balance.voBalance, language), version: balance.voVersion }) : t('coins.adjust.targetHint')}</small>
        </div>
        <Form
          form={form}
          layout="vertical"
          className="admin-feature-form coin-admin-adjustment-form"
          onValuesChange={(_, values: CoinAdjustFormValues) => {
            idempotencyKey.current = createAdminAdjustmentIdempotencyKey();
            setLastTransactionNo(undefined);
            setIsDirty(Boolean(values.deltaAmount && String(values.deltaAmount) !== '0') || Boolean(values.reason?.trim()));
          }}
        >
          <Form.Item
            name="deltaAmount"
            label={t('coins.form.amount')}
            rules={[
              { required: true, message: t('coins.form.amountRequired') },
              { validator: async (_, value) => {
                if (value !== undefined && value !== null && String(value) === '0') throw new Error(t('coins.form.amountNonZero'));
              } },
            ]}
          >
            <InputNumber className="coin-admin-full-width" stringMode precision={0} disabled={!canAdjust || !actionsAreAuthoritative || submitting} />
          </Form.Item>
          <Form.Item name="reason" label={t('coins.form.reason')} rules={[{ required: true, whitespace: true, message: t('coins.form.reasonRequired') }]}>
            <Input.TextArea rows={3} maxLength={200} showCount disabled={!canAdjust || !actionsAreAuthoritative || submitting} placeholder={t('coins.form.reasonPlaceholder')} />
          </Form.Item>
          <div className="coin-admin-adjustment-actions">
            <Button variant="primary" disabled={!canAdjust || !actionsAreAuthoritative || submitting || !isDirty} onClick={() => void handleAdjust()}>{submitting ? t('coins.actions.submitting') : t('coins.actions.review')}</Button>
            {lastTransactionNo ? <span>{t('coins.adjust.lastTransaction', { transactionNo: lastTransactionNo })}</span> : null}
          </div>
        </Form>
      </section>

      <ConsoleResourceList
        toolbar={<ConsoleToolbar title={t('coins.transactions.title')} description={t('coins.transactions.description')} meta={<Tag>{t('coins.transactions.total', { count: transactionTotal })}</Tag>}>{filterControls}</ConsoleToolbar>}
        mobileToolbar={<div className="console-resource-mobile-summary"><div className="console-resource-mobile-summary__copy"><strong>{t('coins.transactions.title')}</strong><span>{t('coins.transactions.total', { count: transactionTotal })}</span></div><Button size="small" icon={<SearchOutlined />} disabled={!urlState.userId} onClick={() => setFilterSheetOpen(true)}>{t('coins.actions.filters')}</Button></div>}
        desktopList={(
          <section className="admin-table-panel coin-admin-transaction-table">
            {transactionNotice}
            <Table<CoinTransactionVo>
              rowKey="voId"
              columns={transactionColumns}
              dataSource={transactions}
              loading={transactionLoading}
              scroll={{ x: 1360 }}
              pagination={{
                current: urlState.pageIndex,
                pageSize: urlState.pageSize,
                total: transactionTotal,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total) => t('coins.transactions.total', { count: total }),
                onChange: (pageIndex, pageSize) => updateTransactionQuery({ pageIndex, pageSize }),
              }}
              locale={{ emptyText: urlState.userId ? (transactionLoading ? t('coins.transactions.loading') : t('coins.transactions.empty')) : t('coins.transactions.queryFirst') }}
            />
          </section>
        )}
        mobileList={mobileTransactions}
        context={(
          <>
            <h3>{t('coins.summary.title')}</h3>
            <p className="admin-feature-subtle">{t('coins.summary.description')}</p>
            <div className="admin-table-summary">
              <div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('coins.summary.currentUser')}</span><span className="admin-table-summary__value">{balance ? balance.voUserName : t('coins.query.notQueried')}</span></div>
              <div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('coins.summary.version')}</span><span className="admin-table-summary__value">{balance ? balance.voVersion : '--'}</span></div>
              <div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('coins.summary.transferredIn')}</span><span className="admin-table-summary__value">{balance ? formatConsoleInteger(balance.voTotalTransferredIn, language) : '--'}</span></div>
              <div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('coins.summary.transferredOut')}</span><span className="admin-table-summary__value">{balance ? formatConsoleInteger(balance.voTotalTransferredOut, language) : '--'}</span></div>
              <div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('coins.summary.permission')}</span><span className="admin-table-summary__value">{canAdjust ? t('coins.summary.canAdjust') : t('coins.summary.readOnly')}</span></div>
            </div>
          </>
        )}
      />

      <BottomSheet isOpen={filterSheetOpen} onClose={() => setFilterSheetOpen(false)} closeLabel={t('coins.actions.closeFilters')} title={t('coins.transactions.filterTitle')} height="auto" className="console-resource-filter-sheet">{filterControls}</BottomSheet>
    </div>
  );
};
