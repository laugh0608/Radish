import { useCallback, useEffect, useMemo, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import {
  coinApi,
  TransactionStatus,
  TransactionType,
  type CoinTransaction,
  type UserBalance,
} from '@/api/coin';
import { WebStateSlot } from '@/components/web-shell';
import { DEFAULT_TIME_ZONE, formatDateTimeByTimeZone, getBrowserTimeZoneId } from '@/utils/dateTime';
import { log } from '@/utils/logger';
import { copyRecoveryDiagnostics } from '@/utils/recoveryDiagnostics';
import { buildMePath, type MeRoute } from './meRouteState';
import styles from './MeAssetsPage.module.css';

type MeAssetsMode = 'overview' | 'transactions';

interface MeAssetsPageProps {
  mode: MeAssetsMode;
  onNavigate: (route: MeRoute) => void;
}

interface AssetsPageData {
  balance: UserBalance | null;
  transactions: CoinTransaction[];
  totalPages: number;
  totalCount: number;
}

const OVERVIEW_PAGE_SIZE = 5;
const TRANSACTIONS_PAGE_SIZE = 20;

const initialData: AssetsPageData = {
  balance: null,
  transactions: [],
  totalPages: 1,
  totalCount: 0,
};

const transactionTypes = Object.values(TransactionType);
const transactionStatuses = Object.values(TransactionStatus);

function shouldHandleMeAssetsLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function formatNumber(value: number | null | undefined): string {
  return Number(value ?? 0).toLocaleString();
}

function formatAmount(transaction: CoinTransaction): string {
  return transaction.voAmountDisplay || `${transaction.voAmount > 0 ? '+' : ''}${formatNumber(transaction.voAmount)}`;
}

function getTransactionTone(transaction: CoinTransaction): 'positive' | 'negative' {
  return transaction.voAmount >= 0 ? 'positive' : 'negative';
}

function sumTransactions(
  transactions: CoinTransaction[],
  predicate: (transaction: CoinTransaction) => boolean
): number {
  return transactions.reduce((total, transaction) => {
    if (!predicate(transaction)) {
      return total;
    }

    return total + transaction.voAmount;
  }, 0);
}

export function MeAssetsPage({ mode, onNavigate }: MeAssetsPageProps) {
  const { t } = useTranslation();
  const displayTimeZone = useMemo(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE), []);
  const [pageData, setPageData] = useState<AssetsPageData>(initialData);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [diagnosticCopyState, setDiagnosticCopyState] = useState<'idle' | 'copied' | 'failed'>('idle');
  const [currentPage, setCurrentPage] = useState(1);
  const [transactionType, setTransactionType] = useState('');
  const [transactionStatus, setTransactionStatus] = useState('');

  const isTransactionsMode = mode === 'transactions';
  const assetsHref = buildMePath({ kind: 'assets' });
  const transactionsHref = buildMePath({ kind: 'assets-transactions' });
  const pageSize = isTransactionsMode ? TRANSACTIONS_PAGE_SIZE : OVERVIEW_PAGE_SIZE;

  const loadAssetsData = useCallback(async () => {
    setLoading(true);
    setError(null);
    setDiagnosticCopyState('idle');

    try {
      const [balance, transactions] = await Promise.all([
        coinApi.getBalance(t),
        coinApi.getTransactions(
          isTransactionsMode ? currentPage : 1,
          pageSize,
          isTransactionsMode && transactionType ? transactionType : null,
          isTransactionsMode && transactionStatus ? transactionStatus : null,
          t,
        ),
      ]);

      setPageData({
        balance,
        transactions: transactions.data,
        totalPages: transactions.pageCount || 1,
        totalCount: transactions.dataCount || 0,
      });
    } catch (err) {
      log.error('MeAssetsPage', '加载资产页失败', err);
      setError(err instanceof Error ? err.message : t('me.assets.error'));
    } finally {
      setLoading(false);
    }
  }, [currentPage, isTransactionsMode, pageSize, t, transactionStatus, transactionType]);

  const handleCopyDiagnostics = useCallback(async () => {
    if (!error) {
      return;
    }

    try {
      await copyRecoveryDiagnostics({
        module: 'me.assets',
        stage: isTransactionsMode ? 'transactions-load' : 'overview-load',
        error,
        target: {
          mode,
          page: currentPage,
          transactionType: transactionType || 'all',
          transactionStatus: transactionStatus || 'all',
        },
        context: {
          totalCount: pageData.totalCount,
          totalPages: pageData.totalPages,
          hasBalance: Boolean(pageData.balance),
        },
      });
      setDiagnosticCopyState('copied');
    } catch (copyError) {
      log.warn('MeAssetsPage', '复制资产页诊断失败', copyError);
      setDiagnosticCopyState('failed');
    }
  }, [currentPage, error, isTransactionsMode, mode, pageData.balance, pageData.totalCount, pageData.totalPages, transactionStatus, transactionType]);

  useEffect(() => {
    void loadAssetsData();
  }, [loadAssetsData]);

  const handleInternalLink = (event: MouseEvent<HTMLAnchorElement>, route: MeRoute) => {
    if (!shouldHandleMeAssetsLink(event)) {
      return;
    }

    event.preventDefault();
    onNavigate(route);
  };

  const balance = pageData.balance;
  const transactionIncome = sumTransactions(pageData.transactions, (transaction) => transaction.voAmount > 0);
  const transactionSpending = Math.abs(sumTransactions(pageData.transactions, (transaction) => transaction.voAmount < 0));
  const abnormalTransactions = pageData.transactions.filter((transaction) => transaction.voStatus !== TransactionStatus.SUCCESS);
  const latestTransaction = pageData.transactions[0] ?? null;

  return (
    <>
      <section className={styles.heroPanel}>
        <div>
          <p className={styles.kicker}>{t('me.assets.kicker')}</p>
          <h1>{t(isTransactionsMode ? 'me.assets.transactionsTitle' : 'me.assets.overviewTitle')}</h1>
          <p>{t(isTransactionsMode ? 'me.assets.transactionsDescription' : 'me.assets.overviewDescription')}</p>
          {!isTransactionsMode && (
            <strong className={styles.heroBalance}>
              {balance?.voBalanceDisplay || `${formatNumber(balance?.voBalance)} ${t('me.carrotUnit')}`}
            </strong>
          )}
        </div>
        <div className={styles.heroActions}>
          <a
            className={styles.secondaryButton}
            href={buildMePath()}
            onClick={(event) => handleInternalLink(event, { kind: 'dashboard' })}
          >
            <Icon icon="mdi:account-circle-outline" size={18} />
            <span>{t('me.assets.backToStatus')}</span>
          </a>
          <button type="button" className={styles.secondaryButton} onClick={() => void loadAssetsData()} disabled={loading}>
            <Icon icon={loading ? 'mdi:loading' : 'mdi:refresh'} size={18} className={loading ? styles.spin : undefined} />
            <span>{loading ? t('me.refreshing') : t('me.refresh')}</span>
          </button>
        </div>
      </section>

      <section className={styles.summaryGrid}>
        <article className={styles.summaryCard}>
          <span>{t('me.assets.available')}</span>
          <strong>{balance?.voBalanceDisplay || `${formatNumber(balance?.voBalance)} ${t('me.carrotUnit')}`}</strong>
        </article>
        <article className={styles.summaryCard}>
          <span>{t('me.frozenBalance')}</span>
          <strong>{balance?.voFrozenBalanceDisplay || `${formatNumber(balance?.voFrozenBalance)} ${t('me.carrotUnit')}`}</strong>
        </article>
        <article className={styles.summaryCard}>
          <span>{t('me.totalEarned')}</span>
          <strong>{formatNumber(balance?.voTotalEarned)}</strong>
        </article>
        <article className={styles.summaryCard}>
          <span>{t('me.assets.totalSpent')}</span>
          <strong>{formatNumber(balance?.voTotalSpent)}</strong>
        </article>
      </section>

      <div className={styles.assetWorkspace}>
        <section className={styles.transactionPanel}>
          <div className={styles.panelHeader}>
            <div>
              <h2>{t(isTransactionsMode ? 'me.assets.allTransactions' : 'me.recentAssets')}</h2>
              <p>{t(isTransactionsMode ? 'me.assets.transactionCount' : 'me.assets.recentDescription', { count: pageData.totalCount })}</p>
            </div>
            {isTransactionsMode ? (
              <a
                className={styles.secondaryButton}
                href={assetsHref}
                onClick={(event) => handleInternalLink(event, { kind: 'assets' })}
              >
                {t('me.assets.backToAssets')}
              </a>
            ) : (
              <a
                className={styles.secondaryButton}
                href={transactionsHref}
                onClick={(event) => handleInternalLink(event, { kind: 'assets-transactions' })}
              >
                {t('me.assets.fullHistory')}
              </a>
            )}
          </div>

          {isTransactionsMode && (
            <div className={styles.filters}>
              <label>
                <span>{t('me.assets.filterType')}</span>
                <select
                  value={transactionType}
                  onChange={(event) => {
                    setTransactionType(event.target.value);
                    setCurrentPage(1);
                  }}
                >
                  <option value="">{t('me.assets.allTypes')}</option>
                  {transactionTypes.map((type) => (
                    <option key={type} value={type}>
                      {t(`me.assets.type.${type}`)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span>{t('me.assets.filterStatus')}</span>
                <select
                  value={transactionStatus}
                  onChange={(event) => {
                    setTransactionStatus(event.target.value);
                    setCurrentPage(1);
                  }}
                >
                  <option value="">{t('me.assets.allStatuses')}</option>
                  {transactionStatuses.map((status) => (
                    <option key={status} value={status}>
                      {t(`me.assets.status.${status}`)}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          )}

          {error ? (
            <WebStateSlot
              tone="error"
              compact
              title={t('me.assets.loadFailed')}
              description={error}
              actions={[
                {
                  label: t('me.refresh'),
                  onClick: () => void loadAssetsData(),
                },
                {
                  label: t(diagnosticCopyState === 'copied'
                    ? 'common.diagnosticsCopied'
                    : diagnosticCopyState === 'failed'
                      ? 'common.diagnosticsCopyFailed'
                      : 'common.copyDiagnostics'),
                  kind: 'secondary',
                  onClick: () => void handleCopyDiagnostics(),
                },
              ]}
            />
          ) : loading && pageData.transactions.length === 0 ? (
            <WebStateSlot
              tone="loading"
              compact
              title={t('me.assets.loading')}
              description={t('me.assets.loadingDescription')}
            />
          ) : pageData.transactions.length > 0 ? (
            <div className={styles.transactionList}>
              {pageData.transactions.map((transaction) => (
                <article key={transaction.voId} className={styles.transactionItem}>
                  <div className={styles.transactionIcon} data-tone={getTransactionTone(transaction)}>
                    <Icon icon={transaction.voAmount >= 0 ? 'mdi:arrow-up' : 'mdi:arrow-down'} size={16} />
                  </div>
                  <div className={styles.transactionBody}>
                    <strong>{transaction.voTransactionTypeDisplay || transaction.voTransactionType}</strong>
                    <span>
                      {formatDateTimeByTimeZone(transaction.voCreateTime, displayTimeZone)}
                      {' · '}
                      {transaction.voStatusDisplay || transaction.voStatus}
                    </span>
                    {(transaction.voBusinessType || transaction.voBusinessId || transaction.voRemark) && (
                      <span>
                        {[transaction.voBusinessType, transaction.voBusinessId, transaction.voRemark].filter(Boolean).join(' · ')}
                      </span>
                    )}
                  </div>
                  <div className={styles.transactionAmount} data-tone={getTransactionTone(transaction)}>
                    {formatAmount(transaction)}
                  </div>
                </article>
              ))}
            </div>
          ) : (
            <WebStateSlot
              tone="empty"
              compact
              icon="mdi:receipt-text-outline"
              title={t('me.assets.empty')}
              description={t('me.assets.emptyDescription')}
            />
          )}

          {isTransactionsMode && pageData.totalPages > 1 && (
            <div className={styles.pagination}>
              <button
                type="button"
                className={styles.pageButton}
                disabled={currentPage <= 1 || loading}
                onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
              >
                {t('me.assets.prevPage')}
              </button>
              <span>{t('me.assets.pageInfo', { current: currentPage, total: pageData.totalPages })}</span>
              <button
                type="button"
                className={styles.pageButton}
                disabled={currentPage >= pageData.totalPages || loading}
                onClick={() => setCurrentPage((page) => Math.min(pageData.totalPages, page + 1))}
              >
                {t('me.assets.nextPage')}
              </button>
            </div>
          )}
        </section>

        <aside className={styles.assetRail}>
          <div>
            <p className={styles.kicker}>{t(isTransactionsMode ? 'me.assets.rail.transactionsKicker' : 'me.assets.rail.overviewKicker')}</p>
            <h2>{t(isTransactionsMode ? 'me.assets.rail.transactionsTitle' : 'me.assets.rail.overviewTitle')}</h2>
            <p>{t(isTransactionsMode ? 'me.assets.rail.transactionsDescription' : 'me.assets.rail.overviewDescription')}</p>
          </div>
          <div className={styles.railMetricList}>
            <article>
              <span>{t('me.assets.rail.income')}</span>
              <strong>+{formatNumber(transactionIncome)}</strong>
            </article>
            <article>
              <span>{t('me.assets.rail.spending')}</span>
              <strong>{formatNumber(transactionSpending)}</strong>
            </article>
            <article>
              <span>{t('me.assets.rail.abnormal')}</span>
              <strong>{formatNumber(abnormalTransactions.length)}</strong>
            </article>
          </div>
          {latestTransaction && (
            <div className={styles.railPreview}>
              <span>{t('me.assets.rail.latest')}</span>
              <strong>{latestTransaction.voTransactionTypeDisplay || latestTransaction.voTransactionType}</strong>
              <p>
                {formatAmount(latestTransaction)}
                {' · '}
                {latestTransaction.voStatusDisplay || latestTransaction.voStatus}
              </p>
            </div>
          )}
          <p className={styles.railHint}>{t('me.assets.rail.scopeHint')}</p>
        </aside>
      </div>
    </>
  );
}
