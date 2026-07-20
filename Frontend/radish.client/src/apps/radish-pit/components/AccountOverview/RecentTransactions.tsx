import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { coinApi } from '@/api/coin';
import { log } from '@/utils/logger';
import { useUserStore } from '@/stores/userStore';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import {
  absoluteCoinValue,
  formatCoinAmount,
  formatCoinRelativeDateTime,
  formatTransactionStatus,
  formatTransactionType,
  getTransactionIcon,
  getTransactionStatusTone,
  resolveTransactionDirection,
} from '../../utils';
import type { CoinTransaction } from '@/api/coin';
import styles from './RecentTransactions.module.css';

interface RecentTransactionsProps {
  displayMode: 'carrot' | 'white';
  onViewAll: () => void;
}

/**
 * 最近交易组件
 */
export const RecentTransactions = ({ displayMode, onViewAll }: RecentTransactionsProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const displayTimeZone = getBrowserTimeZoneId(DEFAULT_TIME_ZONE);
  const currentUserId = String(useUserStore((state) => state.userId));
  const [transactions, setTransactions] = useState<CoinTransaction[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadRecentTransactions = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      // 获取最近5条交易记录
      const response = await coinApi.getTransactions(1, 5, null, null, t);
      setTransactions(response.data);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : t('pit.api.transactionsFailed');
      setError(errorMessage);
      log.error('加载最近交易失败:', err);
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadRecentTransactions();
  }, [loadRecentTransactions]);

  const handleViewAll = () => {
    log.debug('RecentTransactions', '查看全部交易记录');
    onViewAll();
  };

  if (loading) {
    return (
      <div className={styles.card}>
        <div className={styles.header}>
          <h3 className={styles.title}>
            <span className={styles.icon}>📋</span>
            {t('pit.overview.recentTransactions')}
          </h3>
        </div>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>{t('pit.overview.recentLoading')}</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.card}>
        <div className={styles.header}>
          <h3 className={styles.title}>
            <span className={styles.icon}>📋</span>
            {t('pit.overview.recentTransactions')}
          </h3>
        </div>
        <div className={styles.error}>
          <div className={styles.errorIcon}>⚠️</div>
          <p>{error}</p>
          <button className={styles.retryButton} onClick={loadRecentTransactions}>
            {t('pit.common.retry')}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.card}>
      <div className={styles.header}>
        <h3 className={styles.title}>
          <span className={styles.icon}>📋</span>
          {t('pit.overview.recentTransactions')}
        </h3>
        {transactions.length > 0 && (
          <button className={styles.viewAllButton} onClick={handleViewAll}>
            {t('pit.common.viewAll')}
          </button>
        )}
      </div>

      <div className={styles.content}>
        {transactions.length === 0 ? (
          <div className={styles.empty}>
            <div className={styles.emptyIcon}>📝</div>
            <p>{t('pit.history.emptyTitle')}</p>
            <p className={styles.emptyHint}>{t('pit.overview.recentEmptyDescription')}</p>
          </div>
        ) : (
          <div className={styles.transactionList}>
            {transactions.map((transaction) => (
              <div key={transaction.voId} className={styles.transactionItem}>
                <div className={styles.transactionIcon}>
                  {getTransactionIcon(transaction.voTransactionType)}
                </div>

                <div className={styles.transactionContent}>
                  <div className={styles.transactionMain}>
                    <div className={styles.transactionType}>
                      {formatTransactionType(transaction.voTransactionType, t)}
                    </div>
                    <div className={styles.transactionAmount}>
                      <span className={`${styles.amountValue} ${
                        resolveTransactionDirection(transaction, currentUserId) === 'in' ? styles.positive : styles.negative
                      }`}>
                        {resolveTransactionDirection(transaction, currentUserId) === 'in' ? '+' : '-'}
                        {formatCoinAmount(absoluteCoinValue(transaction.voAmount), language, t, displayMode)}
                      </span>
                    </div>
                  </div>

                  <div className={styles.transactionDetails}>
                    <div className={styles.transactionTime}>
                      {formatCoinRelativeDateTime(transaction.voCreateTime, displayTimeZone, language)}
                    </div>
                    <div className={`${styles.transactionStatus} ${styles[getTransactionStatusTone(transaction.voStatus)]}`}>
                      {formatTransactionStatus(transaction.voStatus, t)}
                    </div>
                  </div>

                  {transaction.voRemark && (
                    <div className={styles.transactionNote}>
                      {transaction.voRemark}
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
