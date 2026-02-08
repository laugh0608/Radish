import { useState, useEffect } from 'react';
import { coinApi } from '@/api/coin';
import { log } from '@/utils/logger';
import { formatCoinAmount, formatDateTime, getSafeUserDisplayName } from '../../utils';
import { useUserStore } from '@/stores/userStore';
import type { CoinTransaction } from '@/api/coin';
import styles from './RecentTransfers.module.css';

interface RecentTransfersProps {
  displayMode: 'carrot' | 'white';
}

/**
 * 最近转账记录组件
 */
export const RecentTransfers = ({ displayMode }: RecentTransfersProps) => {
  const { userId } = useUserStore();
  const currentUserId = String(userId);
  const [transfers, setTransfers] = useState<CoinTransaction[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const useWhiteRadish = displayMode === 'white';

  useEffect(() => {
    loadRecentTransfers();
  }, []);

  const loadRecentTransfers = async () => {
    try {
      setLoading(true);
      setError(null);

      // 获取最近的转账记录（转入和转出）
      const response = await coinApi.getTransactions(1, 10);

      // 筛选出转账相关的交易
      const transferTransactions = (response?.voItems || []).filter(
        (transaction: CoinTransaction) =>
          transaction.voTransactionType === 'TRANSFER_IN' ||
          transaction.voTransactionType === 'TRANSFER_OUT'
      );

      setTransfers(transferTransactions);
      log.debug('RecentTransfers', '加载最近转账记录完成', { count: transferTransactions.length });
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : '加载最近转账记录失败';
      setError(errorMessage);
      log.error('加载最近转账记录失败:', err);
    } finally {
      setLoading(false);
    }
  };

  const getTransferDirection = (transaction: CoinTransaction): 'in' | 'out' => {
    return transaction.voTransactionType === 'TRANSFER_IN' ? 'in' : 'out';
  };

  const getTransferParty = (transaction: CoinTransaction): string => {
    const direction = getTransferDirection(transaction);

    if (direction === 'in') {
      // 转入：显示发送方
      return transaction.voFromUserName
        ? getSafeUserDisplayName(transaction.voFromUserName, transaction.voFromUserId === currentUserId)
        : '系统';
    } else {
      // 转出：显示接收方
      return transaction.voToUserName
        ? getSafeUserDisplayName(transaction.voToUserName, transaction.voToUserId === currentUserId)
        : '系统';
    }
  };

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.header}>
          <h4 className={styles.title}>
            <span className={styles.icon}>📋</span>
            最近转移
          </h4>
        </div>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>加载最近转移记录中...</p>
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
            最近转移
          </h4>
        </div>
        <div className={styles.error}>
          <div className={styles.errorIcon}>⚠️</div>
          <p>{error}</p>
          <button className={styles.retryButton} onClick={loadRecentTransfers}>
            重试
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
          最近转移
        </h4>
        {transfers.length > 0 && (
          <button className={styles.viewAllButton} onClick={() => {
            // TODO: 跳转到交易记录页面并筛选转账记录
            log.debug('RecentTransfers', '查看全部转账记录');
          }}>
            查看全部
          </button>
        )}
      </div>

      <div className={styles.content}>
        {transfers.length === 0 ? (
          <div className={styles.empty}>
            <div className={styles.emptyIcon}>💸</div>
            <p>暂无转移记录</p>
            <p className={styles.emptyHint}>完成首次转移后，记录将显示在这里</p>
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
                        {direction === 'in' ? '转入' : '转出'}
                      </div>
                      <div className={styles.transferParty}>
                        {direction === 'in' ? `来自 ${party}` : `转给 ${party}`}
                      </div>
                    </div>

                    <div className={styles.transferDetails}>
                      <div className={styles.transferAmount}>
                        <span className={`${styles.amountValue} ${
                          direction === 'in' ? styles.positive : styles.negative
                        }`}>
                          {direction === 'in' ? '+' : '-'}
                          {formatCoinAmount(Math.abs(transfer.voAmount), true, useWhiteRadish)}
                        </span>
                      </div>
                      <div className={styles.transferTime}>
                        {formatDateTime(transfer.voCreateTime)}
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
                    {transfer.voStatusDisplay}
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
