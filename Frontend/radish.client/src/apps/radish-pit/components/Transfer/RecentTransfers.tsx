import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { coinApi } from '@/api/coin';
import { log } from '@/utils/logger';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import {
  absoluteCoinValue,
  formatCoinAmount,
  formatCoinRelativeDateTime,
  formatTransactionStatus,
  getSafeUserDisplayName,
  isTransferTransaction,
  resolveTransactionDirection,
} from '../../utils';
import { useUserStore } from '@/stores/userStore';
import type { CoinTransaction } from '@/api/coin';
import styles from './RecentTransfers.module.css';

interface RecentTransfersProps {
  displayMode: 'carrot' | 'white';
  onViewAll: () => void;
}

/**
 * 最近转账记录组件
 */
export const RecentTransfers = ({ displayMode, onViewAll }: RecentTransfersProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const displayTimeZone = getBrowserTimeZoneId(DEFAULT_TIME_ZONE);
  const { userId } = useUserStore();
  const currentUserId = String(userId);
  const [transfers, setTransfers] = useState<CoinTransaction[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadRecentTransfers = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      // 获取最近的转账记录（转入和转出）
      const response = await coinApi.getTransactions(1, 10, null, null, t);

      // 筛选出转账相关的交易
      const transferTransactions = response.data.filter(
        (transaction: CoinTransaction) =>
          isTransferTransaction(transaction)
      );

      setTransfers(transferTransactions);
      log.debug('RecentTransfers', '加载最近转账记录完成', { count: transferTransactions.length });
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : t('pit.api.transactionsFailed');
      setError(errorMessage);
      log.error('加载最近转账记录失败:', err);
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadRecentTransfers();
  }, [loadRecentTransfers]);

  const getTransferDirection = (transaction: CoinTransaction): 'in' | 'out' => {
    return resolveTransactionDirection(transaction, currentUserId);
  };

  const getTransferParty = (transaction: CoinTransaction): string => {
    const direction = getTransferDirection(transaction);

    if (direction === 'in') {
      // 转入：显示发送方
      return transaction.voFromUserId && transaction.voFromUserName
        ? getSafeUserDisplayName(transaction.voFromUserName, transaction.voFromUserId === currentUserId, t('pit.common.me'))
        : t('pit.common.system');
    } else {
      // 转出：显示接收方
      return transaction.voToUserId && transaction.voToUserName
        ? getSafeUserDisplayName(transaction.voToUserName, transaction.voToUserId === currentUserId, t('pit.common.me'))
        : t('pit.common.system');
    }
  };

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.header}>
          <h4 className={styles.title}>
            <span className={styles.icon}>📋</span>
            {t('pit.transfer.recent.title')}
          </h4>
        </div>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>{t('pit.transfer.recent.loading')}</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.container}>
        <div className={styles.header}>
          <h4 className={styles.title}>
            <span className={styles.icon}>📋</span>
            {t('pit.transfer.recent.title')}
          </h4>
        </div>
        <div className={styles.error}>
          <div className={styles.errorIcon}>⚠️</div>
          <p>{error}</p>
          <button className={styles.retryButton} onClick={loadRecentTransfers}>
            {t('pit.common.retry')}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h4 className={styles.title}>
          <span className={styles.icon}>📋</span>
          {t('pit.transfer.recent.title')}
        </h4>
        {transfers.length > 0 && (
          <button className={styles.viewAllButton} onClick={() => {
            log.debug('RecentTransfers', '打开交易记录页');
            onViewAll();
          }}>
            {t('pit.common.viewAll')}
          </button>
        )}
      </div>

      <div className={styles.content}>
        {transfers.length === 0 ? (
          <div className={styles.empty}>
            <div className={styles.emptyIcon}>💸</div>
            <p>{t('pit.transfer.recent.empty')}</p>
            <p className={styles.emptyHint}>{t('pit.transfer.recent.emptyDescription')}</p>
          </div>
        ) : (
          <div className={styles.transferList}>
            {transfers.map((transfer) => {
              const direction = getTransferDirection(transfer);
              const party = getTransferParty(transfer);

              return (
                <div key={transfer.voId} className={styles.transferItem}>
                  <div className={styles.transferIcon}>
                    {direction === 'in' ? '📥' : '📤'}
                  </div>

                  <div className={styles.transferContent}>
                    <div className={styles.transferMain}>
                      <div className={styles.transferType}>
                        {t(direction === 'in' ? 'pit.transfer.direction.in' : 'pit.transfer.direction.out')}
                      </div>
                      <div className={styles.transferParty}>
                        {t(direction === 'in' ? 'pit.transfer.fromParty' : 'pit.transfer.toParty', { party })}
                      </div>
                    </div>

                    <div className={styles.transferDetails}>
                      <div className={styles.transferAmount}>
                        <span className={`${styles.amountValue} ${
                          direction === 'in' ? styles.positive : styles.negative
                        }`}>
                          {direction === 'in' ? '+' : '-'}
                          {formatCoinAmount(absoluteCoinValue(transfer.voAmount), language, t, displayMode)}
                        </span>
                      </div>
                      <div className={styles.transferTime}>
                        {formatCoinRelativeDateTime(transfer.voCreateTime, displayTimeZone, language)}
                      </div>
                    </div>

                    {transfer.voRemark && (
                      <div className={styles.transferNote}>
                        <span className={styles.noteIcon}>💬</span>
                        {transfer.voRemark}
                      </div>
                    )}
                  </div>

                  <div className={`${styles.transferStatus} ${
                    transfer.voStatus === 'SUCCESS' ? styles.success :
                    transfer.voStatus === 'PENDING' ? styles.pending : styles.failed
                  }`}>
                    {formatTransactionStatus(transfer.voStatus, t)}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
};
