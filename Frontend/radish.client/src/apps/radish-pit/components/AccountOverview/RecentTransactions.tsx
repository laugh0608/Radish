import { useState, useEffect } from 'react';
import { coinApi } from '@/api/coin';
import { log } from '@/utils/logger';
import { formatCoinAmount, formatDateTime, getTransactionTypeDisplay, getTransactionStatusColor } from '../../utils';
import type { CoinTransaction } from '@/api/coin';
import styles from './RecentTransactions.module.css';

interface RecentTransactionsProps {
  displayMode: 'carrot' | 'white';
}

/**
 * 最近交易组件
 */
export const RecentTransactions = ({ displayMode }: RecentTransactionsProps) => {
  const [transactions, setTransactions] = useState<CoinTransaction[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const useWhiteRadish = displayMode === 'white';

  useEffect(() => {
    loadRecentTransactions();
  }, []);

  const loadRecentTransactions = async () => {
    try {
      setLoading(true);
      setError(null);

      // 获取最近5条交易记录
      const response = await coinApi.getTransactions(1, 5);
      setTransactions(response.data);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : '加载最近记录失败';
      setError(errorMessage);
      log.error('加载最近交易失败:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleViewAll = () => {
    log.debug('RecentTransactions', '查看全部交易记录');
    // TODO: 触发切换到交易记录标签页
  };

  if (loading) {
    return (
      <div className={styles.card}>
        <div className={styles.header}>
          <h3 className={styles.title}>
            <span className={styles.icon}>📋</span>
            最近记录
          </h3>
        </div>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>加载最近记录中...</p>
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
            最近记录
          </h3>
        </div>
        <div className={styles.error}>
          <div className={styles.errorIcon}>⚠️</div>
          <p>{error}</p>
          <button className={styles.retryButton} onClick={loadRecentTransactions}>
            重试
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
          最近记录
        </h3>
        {transactions.length > 0 && (
          <button className={styles.viewAllButton} onClick={handleViewAll}>
            查看全部
          </button>
        )}
      </div>

      <div className={styles.content}>
        {transactions.length === 0 ? (
          <div className={styles.empty}>
            <div className={styles.emptyIcon}>📝</div>
            <p>暂无交易记录</p>
            <p className={styles.emptyHint}>开始使用萝卜后，记录将显示在这里</p>
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
                      {getTransactionTypeDisplay(transaction.voTransactionType)}
                    </div>
                    <div className={styles.transactionAmount}>
                      <span className={`${styles.amountValue} ${
                        transaction.voAmount > 0 ? styles.positive : styles.negative
                      }`}>
                        {transaction.voAmount > 0 ? '+' : ''}
                        {formatCoinAmount(Math.abs(transaction.voAmount), true, useWhiteRadish)}
                      </span>
                    </div>
                  </div>

                  <div className={styles.transactionDetails}>
                    <div className={styles.transactionTime}>
                      {formatDateTime(transaction.voCreateTime)}
                    </div>
                    <div className={`${styles.transactionStatus} ${styles[getTransactionStatusColor(transaction.voStatus)]}`}>
                      {transaction.voStatusDisplay}
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

/**
 * 根据交易类型获取图标
 */
const getTransactionIcon = (transactionType: string): string => {
  const iconMap: Record<string, string> = {
    'SYSTEM_GRANT': '🎁',
    'LIKE_REWARD': '👍',
    'COMMENT_REWARD': '💬',
    'GODLIKE_REWARD': '⭐',
    'SOFA_REWARD': '🛋️',
    'TRANSFER_IN': '📥',
    'TRANSFER_OUT': '📤',
    'PURCHASE': '🛒',
    'ADMIN_ADJUST': '⚙️'
  };

  return iconMap[transactionType] || '💰';
};
