import { useCallback, useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  AntInput as Input,
  AntSelect as Select,
  Button,
  Form,
  InputNumber,
  message,
  Tag,
  Table,
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
import '../adminFeature.css';
import './CoinAdminPage.css';

function normalizePositiveLongIdInput(value: string): string | undefined {
  const trimmed = value.trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
}

function normalizeTextFilterInput(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
}

interface CoinAdjustFormValues {
  userId: string;
  deltaAmount: string;
  reason: string;
}

const TRANSACTION_TYPES = ['SYSTEM_GRANT', 'LIKE_REWARD', 'COMMENT_REWARD', 'TRANSFER', 'TIP', 'CONSUME', 'REFUND', 'PENALTY', 'ADMIN_ADJUST'] as const;
const TRANSACTION_STATUSES = ['PENDING', 'SUCCESS', 'FAILED'] as const;

function normalizeOptionQuery(
  value: string | null,
  options: readonly string[]
): string | undefined {
  if (!value) {
    return undefined;
  }

  return options.includes(value) ? value : undefined;
}

export const CoinAdminPage = () => {
  const { t, i18n } = useTranslation();
  useDocumentTitle(t('coins.documentTitle'));
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const queryUserIdFromUrl = normalizePositiveLongIdInput(searchParams.get('userId') ?? '');
  const queryTransactionType = normalizeOptionQuery(searchParams.get('transactionType'), TRANSACTION_TYPES);
  const queryTransactionStatus = normalizeOptionQuery(searchParams.get('status'), TRANSACTION_STATUSES);
  const queryBusinessType = normalizeTextFilterInput(searchParams.get('businessType') ?? '');
  const queryBusinessId = normalizePositiveLongIdInput(searchParams.get('businessId') ?? '');
  const returnTo = normalizeConsoleReturnTo(searchParams.get('returnTo'));

  const [queryUserId, setQueryUserId] = useState(queryUserIdFromUrl ?? '');
  const [loadedUserId, setLoadedUserId] = useState<string | null>(null);
  const [balance, setBalance] = useState<UserBalanceVo | null>(null);
  const [transactions, setTransactions] = useState<CoinTransactionVo[]>([]);
  const [transactionTotal, setTransactionTotal] = useState(0);
  const [transactionPageIndex, setTransactionPageIndex] = useState(1);
  const [transactionPageSize, setTransactionPageSize] = useState(10);
  const [transactionTypeFilter, setTransactionTypeFilter] = useState<string | undefined>(queryTransactionType);
  const [transactionStatusFilter, setTransactionStatusFilter] = useState<string | undefined>(queryTransactionStatus);
  const [businessTypeFilter, setBusinessTypeFilter] = useState<string | undefined>(queryBusinessType);
  const [businessIdFilter, setBusinessIdFilter] = useState<string | undefined>(queryBusinessId);
  const [loading, setLoading] = useState(false);
  const [transactionLoading, setTransactionLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [form] = Form.useForm<CoinAdjustFormValues>();
  const initialQueryUserLoadedRef = useRef(false);
  const canAdjust = usePermission(CONSOLE_PERMISSIONS.coinsAdjust);
  const transactionTypeOptions = TRANSACTION_TYPES.map((value) => ({
    value,
    label: t(`coins.type.${value}`),
  }));
  const transactionStatusOptions = TRANSACTION_STATUSES.map((value) => ({
    value,
    label: t(`coins.status.${value}`),
  }));

  const getSignedCoinAmount = (transaction: CoinTransactionVo, userId: string) => {
    const amount = BigInt(transaction.voAmount);
    if (String(transaction.voFromUserId ?? '') === userId) {
      return -amount;
    }

    return amount;
  };

  const getSignedAmountClassName = (amount: bigint) => (
    amount >= 0n
      ? 'coin-admin-signed-amount coin-admin-signed-amount--positive'
      : 'coin-admin-signed-amount coin-admin-signed-amount--negative'
  );

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'SUCCESS':
        return 'success';
      case 'FAILED':
        return 'error';
      case 'PENDING':
        return 'warning';
      default:
        return 'default';
    }
  };

  const getCurrentReturnTo = () => `${location.pathname}${location.search}`;

  const handleViewOrderFromTransaction = (transaction: CoinTransactionVo) => {
    if (transaction.voBusinessType !== 'Order' || !transaction.voBusinessId) {
      return;
    }

    navigate(buildOrderDetailPath({
      orderId: String(transaction.voBusinessId),
      userId: loadedUserId ?? undefined,
      returnTo: getCurrentReturnTo(),
    }));
  };

  const loadTransactions = useCallback(async (
    userId: string,
    pageIndex = transactionPageIndex,
    pageSize = transactionPageSize,
    transactionType = transactionTypeFilter,
    status = transactionStatusFilter,
    businessType = businessTypeFilter,
    businessId = businessIdFilter
  ) => {
    try {
      setTransactionLoading(true);
      const result = await getTransactionsByUserId({
        userId,
        pageIndex,
        pageSize,
        transactionType,
        status,
        businessType,
        businessId,
      });
      setTransactions(result.data);
      setTransactionTotal(result.dataCount);
      setTransactionPageIndex(result.page);
      setTransactionPageSize(result.pageSize);
    } catch (error) {
      log.error('CoinAdminPage', '加载胡萝卜流水失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'coins.feedback.loadTransactionsFailed'));
      setTransactions([]);
      setTransactionTotal(0);
    } finally {
      setTransactionLoading(false);
    }
  }, [
    businessIdFilter,
    businessTypeFilter,
    transactionPageIndex,
    transactionPageSize,
    transactionStatusFilter,
    transactionTypeFilter,
    t,
  ]);

  const loadBalance = useCallback(async (targetUserId?: string) => {
    const userId = targetUserId ?? normalizePositiveLongIdInput(queryUserId);
    if (!userId) {
      message.error(t('coins.form.userIdInvalid'));
      return;
    }

    try {
      setLoading(true);
      const result = await getBalanceByUserId(userId);
      setBalance(result);
      setLoadedUserId(userId);
      setQueryUserId(userId);
      form.setFieldValue('userId', userId);
    } catch (error) {
      log.error('CoinAdminPage', '加载用户余额失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'coins.feedback.loadBalanceFailed'));
      setBalance(null);
      setLoadedUserId(null);
      setTransactions([]);
      setTransactionTotal(0);
      return;
    } finally {
      setLoading(false);
    }

    await loadTransactions(userId, 1, transactionPageSize);
  }, [form, loadTransactions, queryUserId, t, transactionPageSize]);

  useEffect(() => {
    if (initialQueryUserLoadedRef.current || !queryUserIdFromUrl) {
      return;
    }

    initialQueryUserLoadedRef.current = true;
    form.setFieldValue('userId', queryUserIdFromUrl);
    void loadBalance(queryUserIdFromUrl);
  }, [form, loadBalance, queryUserIdFromUrl]);

  const handleAdjust = async () => {
    try {
      const values = await form.validateFields();
      const normalizedUserId = normalizePositiveLongIdInput(values.userId);
      if (!normalizedUserId) {
        message.error(t('coins.form.userIdInvalid'));
        return;
      }

      setSubmitting(true);
      await adminAdjustBalance({
        userId: normalizedUserId,
        deltaAmount: String(values.deltaAmount),
        reason: values.reason,
      });

      message.success(t('coins.feedback.adjusted'));
      setQueryUserId(normalizedUserId);
      await loadBalance(normalizedUserId);
      form.setFieldsValue({ deltaAmount: '0', reason: '' });
    } catch (error) {
      log.error('CoinAdminPage', '调整胡萝卜余额失败:', error);
      message.error(getLocalizedApiErrorMessage(error, t, 'coins.feedback.adjustFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleTransactionTypeChange = (value: string | undefined) => {
    const nextValue = typeof value === 'string' && value.length > 0 ? value : undefined;
    setTransactionTypeFilter(nextValue);
    if (loadedUserId) {
      void loadTransactions(
        loadedUserId,
        1,
        transactionPageSize,
        nextValue,
        transactionStatusFilter,
        businessTypeFilter,
        businessIdFilter
      );
    }
  };

  const handleTransactionStatusChange = (value: string | undefined) => {
    const nextValue = typeof value === 'string' && value.length > 0 ? value : undefined;
    setTransactionStatusFilter(nextValue);
    if (loadedUserId) {
      void loadTransactions(
        loadedUserId,
        1,
        transactionPageSize,
        transactionTypeFilter,
        nextValue,
        businessTypeFilter,
        businessIdFilter
      );
    }
  };

  const handleBusinessFilterSearch = () => {
    const nextBusinessType = businessTypeFilter ? normalizeTextFilterInput(businessTypeFilter) : undefined;
    const nextBusinessId = businessIdFilter ? normalizePositiveLongIdInput(businessIdFilter) : undefined;
    setBusinessTypeFilter(nextBusinessType);
    setBusinessIdFilter(nextBusinessId);

    if (businessIdFilter && !nextBusinessId) {
      message.error(t('coins.transactions.businessIdInvalid'));
      return;
    }

    if (loadedUserId) {
      void loadTransactions(
        loadedUserId,
        1,
        transactionPageSize,
        transactionTypeFilter,
        transactionStatusFilter,
        nextBusinessType,
        nextBusinessId
      );
    }
  };

  const clearTransactionFilters = () => {
    setTransactionTypeFilter(undefined);
    setTransactionStatusFilter(undefined);
    setBusinessTypeFilter(undefined);
    setBusinessIdFilter(undefined);
    if (loadedUserId) {
      void loadTransactions(loadedUserId, 1, transactionPageSize, undefined, undefined, undefined, undefined);
    }
  };

  const transactionColumns: TableColumnsType<CoinTransactionVo> = [
    {
      title: t('coins.table.time'),
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (time: string) => formatConsoleDateTime(time, i18n.resolvedLanguage),
    },
    {
      title: t('coins.table.amount'),
      dataIndex: 'voAmount',
      key: 'voAmount',
      width: 120,
      render: (_amount: string, record) => {
        const signedAmount = getSignedCoinAmount(record, loadedUserId ?? '');

        return (
          <span className={getSignedAmountClassName(signedAmount)}>
            {formatConsoleSignedInteger(signedAmount, i18n.resolvedLanguage)}
          </span>
        );
      },
    },
    {
      title: t('coins.table.type'),
      dataIndex: 'voTransactionType',
      key: 'voTransactionType',
      width: 130,
      render: (type: string) => t(`coins.type.${type}`, { defaultValue: type }),
    },
    {
      title: t('coins.table.status'),
      dataIndex: 'voStatus',
      key: 'voStatus',
      width: 100,
      render: (status: string, record) => (
        <Tag color={getStatusColor(status)}>
          {t(`coins.status.${status}`, { defaultValue: record.voStatusDisplay || status })}
        </Tag>
      ),
    },
    {
      title: t('coins.table.operator'),
      dataIndex: 'voCreateBy',
      key: 'voCreateBy',
      width: 150,
      render: (operatorName: string, record) => (
        <span>{operatorName || record.voCreateId || '-'}</span>
      ),
    },
    {
      title: t('coins.table.business'),
      dataIndex: 'voBusinessType',
      key: 'voBusinessType',
      width: 140,
      render: (businessType: string | null | undefined, record) => {
        if (!businessType && !record.voBusinessId) return '-';

        return `${businessType || '-'}${record.voBusinessId ? ` #${record.voBusinessId}` : ''}`;
      },
    },
    {
      title: t('coins.table.actions'),
      key: 'action',
      width: 120,
      render: (_: unknown, record) => (
        record.voBusinessType === 'Order' && record.voBusinessId ? (
          <Button onClick={() => handleViewOrderFromTransaction(record)}>
            {t('coins.actions.viewOrder')}
          </Button>
        ) : '-'
      ),
    },
    {
      title: t('coins.table.transactionNo'),
      dataIndex: 'voTransactionNo',
      key: 'voTransactionNo',
      width: 220,
    },
    {
      title: t('coins.table.remark'),
      dataIndex: 'voRemark',
      key: 'voRemark',
      render: (remark?: string | null) => remark || '-',
    },
  ];

  return (
    <div className="admin-feature-page coin-admin-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h2>
              <WalletOutlined /> {t('coins.page.title')}
            </h2>
            <p className="admin-feature-subtle">{t('coins.page.description')}</p>
          </div>
          <div className="coin-admin-header-actions">
            {returnTo ? (
              <Button onClick={() => navigate(returnTo)}>
                {t('coins.actions.back')}
              </Button>
            ) : null}
            <Button icon={<ReloadOutlined />} onClick={() => {
              void loadBalance(loadedUserId ?? undefined);
            }}>
              {t('coins.actions.refresh')}
            </Button>
          </div>
        </div>
      </section>

      <section className="admin-feature-metrics" aria-label={t('coins.metrics.ariaLabel')}>
        <div className="admin-feature-metric">
          <span>{t('coins.metrics.available')}</span>
          <strong>{balance ? t('coins.unit', { value: formatConsoleInteger(balance.voBalance, i18n.resolvedLanguage) }) : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span>{t('coins.metrics.frozen')}</span>
          <strong>{balance ? t('coins.unit', { value: formatConsoleInteger(balance.voFrozenBalance, i18n.resolvedLanguage) }) : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span>{t('coins.metrics.earned')}</span>
          <strong>{balance ? formatConsoleInteger(balance.voTotalEarned, i18n.resolvedLanguage) : '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          <span>{t('coins.metrics.spent')}</span>
          <strong>{balance ? formatConsoleInteger(balance.voTotalSpent, i18n.resolvedLanguage) : '--'}</strong>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <section className="admin-table-toolbar" aria-label={t('coins.query.ariaLabel')}>
            <div className="admin-table-toolbar__title">
              <span>{t('coins.query.title')}</span>
              <Tag>{balance ? t('coins.query.user', { userId: balance.voUserId }) : t('coins.query.notQueried')}</Tag>
            </div>
            <p className="admin-feature-subtle">{t('coins.query.description')}</p>
            <div className="admin-table-toolbar__filters">
              <Input
                className="coin-admin-query-input"
                placeholder={t('coins.form.userIdPlaceholder')}
                value={queryUserId}
                onChange={(event) => setQueryUserId(event.target.value)}
                onPressEnter={() => {
                  void loadBalance();
                }}
              />
              <Button
                variant="primary"
                icon={<SearchOutlined />}
                onClick={() => {
                  void loadBalance();
                }}
                disabled={loading}
              >
                {loading ? t('coins.actions.searching') : t('coins.actions.search')}
              </Button>
            </div>
          </section>

          <section className="admin-table-panel">
            <div className="coin-admin-section-title">
              <div>
                <h3>{t('coins.adjust.title')}</h3>
                <p className="admin-feature-subtle">{t('coins.adjust.description')}</p>
              </div>
              <Tag>{canAdjust ? t('coins.adjust.allowed') : t('coins.adjust.denied')}</Tag>
            </div>

            <Form form={form} layout="vertical" className="admin-feature-form">
              <Form.Item
                name="userId"
                label={t('coins.form.userId')}
                rules={[
                  { required: true, message: t('coins.form.userIdRequired') },
                  { pattern: /^[1-9]\d*$/, message: t('coins.form.userIdInvalid') },
                ]}
              >
                <Input />
              </Form.Item>

              <Form.Item
                name="deltaAmount"
                label={t('coins.form.amount')}
                rules={[{ required: true, message: t('coins.form.amountRequired') }]}
              >
                <InputNumber className="coin-admin-full-width" stringMode />
              </Form.Item>

              <Form.Item
                name="reason"
                label={t('coins.form.reason')}
                rules={[{ required: true, message: t('coins.form.reasonRequired') }]}
              >
                <Input.TextArea rows={4} maxLength={200} showCount placeholder={t('coins.form.reasonPlaceholder')} />
              </Form.Item>

              <div>
                <Button variant="primary" disabled={!canAdjust || submitting} onClick={() => {
                  void handleAdjust();
                }}>
                  {submitting ? t('coins.actions.submitting') : t('coins.actions.submit')}
                </Button>
              </div>
            </Form>
          </section>

          <section className="admin-table-panel">
            <div className="coin-admin-section-title">
              <div>
                <h3>{t('coins.transactions.title')}</h3>
                <p className="admin-feature-subtle">{t('coins.transactions.description')}</p>
              </div>
              <Tag>{loadedUserId ? t('coins.query.user', { userId: loadedUserId }) : t('coins.query.notQueried')}</Tag>
            </div>

            <div className="coin-admin-transaction-filters">
              <Select
                value={transactionTypeFilter}
                allowClear
                placeholder={t('coins.transactions.typePlaceholder')}
                options={transactionTypeOptions}
                disabled={!loadedUserId}
                onChange={handleTransactionTypeChange}
              />
              <Select
                value={transactionStatusFilter}
                allowClear
                placeholder={t('coins.transactions.statusPlaceholder')}
                options={transactionStatusOptions}
                disabled={!loadedUserId}
                onChange={handleTransactionStatusChange}
              />
              <Input
                value={businessTypeFilter ?? ''}
                placeholder={t('coins.transactions.businessTypePlaceholder')}
                disabled={!loadedUserId}
                onChange={(event) => setBusinessTypeFilter(event.target.value)}
                onPressEnter={handleBusinessFilterSearch}
              />
              <Input
                value={businessIdFilter ?? ''}
                placeholder={t('coins.transactions.businessIdPlaceholder')}
                disabled={!loadedUserId}
                onChange={(event) => setBusinessIdFilter(event.target.value.trim())}
                onPressEnter={handleBusinessFilterSearch}
              />
              <Button disabled={!loadedUserId} onClick={handleBusinessFilterSearch}>
                {t('coins.actions.filterBusiness')}
              </Button>
              <Button disabled={!loadedUserId} onClick={clearTransactionFilters}>
                {t('coins.actions.clearFilters')}
              </Button>
            </div>

            <Table<CoinTransactionVo>
              className="coin-admin-transaction-table"
              rowKey="voId"
              columns={transactionColumns}
              dataSource={transactions}
              loading={transactionLoading}
              scroll={{ x: 1340 }}
              pagination={{
                current: transactionPageIndex,
                pageSize: transactionPageSize,
                total: transactionTotal,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total) => t('coins.transactions.total', { count: total }),
                onChange: (page, size) => {
                  if (loadedUserId) {
                    void loadTransactions(loadedUserId, page, size);
                  }
                },
              }}
              locale={{
                emptyText: loadedUserId
                  ? (transactionLoading ? t('coins.transactions.loading') : t('coins.transactions.empty'))
                  : t('coins.transactions.queryFirst'),
              }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>{t('coins.summary.title')}</h3>
          <p className="admin-feature-subtle">{t('coins.summary.description')}</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('coins.summary.currentUser')}</span>
              <span className="admin-table-summary__value">{balance ? balance.voUserId : t('coins.query.notQueried')}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('coins.summary.transferredIn')}</span>
              <span className="admin-table-summary__value">{balance ? formatConsoleInteger(balance.voTotalTransferredIn, i18n.resolvedLanguage) : '--'}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('coins.summary.transferredOut')}</span>
              <span className="admin-table-summary__value">{balance ? formatConsoleInteger(balance.voTotalTransferredOut, i18n.resolvedLanguage) : '--'}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('coins.summary.permission')}</span>
              <span className="admin-table-summary__value">
                {canAdjust ? t('coins.summary.canAdjust') : t('coins.summary.readOnly')}
              </span>
            </div>
          </div>
        </aside>
      </div>
    </div>
  );
};
