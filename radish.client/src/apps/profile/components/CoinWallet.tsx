import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { getBalance, type UserBalance } from '@/api/coin';
import { CoinTransactionList } from './CoinTransactionList';
import styles from './CoinWallet.module.css';

interface CoinWalletProps {
  apiBaseUrl: string;
}

/**
 * 萝卜币钱包组件
 *
 * 显示用户余额信息和交易记录
 */
export const CoinWallet = ({ apiBaseUrl }: CoinWalletProps) => {
  const { t } = useTranslation();
  const [balance, setBalance] = useState<UserBalance | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void loadBalance();
  }, []);

  const loadBalance = async () => {
    setLoading(true);
    setError(null);

    try {
      const result = await getBalance(t);
      if (result.ok && result.data) {
        setBalance(result.data);
      } else {
        setError(result.message);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : '获取余额失败');
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>加载中...</div>
      </div>
    );
  }

  if (error || !balance) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>{error || '加载余额失败'}</div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      {/* 余额卡片 */}
      <div className={styles.balanceCard}>
        <div className={styles.cardHeader}>
          <h3 className={styles.cardTitle}>
            <span className={styles.icon}>🥕</span>
            我的余额
          </h3>
          <button className={styles.refreshButton} onClick={loadBalance} title="刷新余额">
            🔄
          </button>
        </div>

        <div className={styles.balanceMain}>
          <div className={styles.balanceItem}>
            <div className={styles.balanceLabel}>可用余额</div>
            <div className={styles.balanceValue}>
              <span className={styles.carrotAmount}>{balance.balance.toLocaleString()}</span>
              <span className={styles.unit}>胡萝卜</span>
            </div>
            <div className={styles.balanceValueAlt}>
              {balance.balanceDisplay} 白萝卜
            </div>
          </div>

          {balance.frozenBalance > 0 && (
            <div className={styles.balanceItem}>
              <div className={styles.balanceLabel}>冻结余额</div>
              <div className={styles.balanceValue}>
                <span className={styles.carrotAmount}>{balance.frozenBalance.toLocaleString()}</span>
                <span className={styles.unit}>胡萝卜</span>
              </div>
              <div className={styles.balanceValueAlt}>
                {balance.frozenBalanceDisplay} 白萝卜
              </div>
            </div>
          )}
        </div>

        <div className={styles.statsGrid}>
          <div className={styles.statItem}>
            <div className={styles.statValue}>+{balance.totalEarned.toLocaleString()}</div>
            <div className={styles.statLabel}>累计收入</div>
          </div>
          <div className={styles.statItem}>
            <div className={styles.statValue}>-{balance.totalSpent.toLocaleString()}</div>
            <div className={styles.statLabel}>累计支出</div>
          </div>
          <div className={styles.statItem}>
            <div className={styles.statValue}>+{balance.totalTransferredIn.toLocaleString()}</div>
            <div className={styles.statLabel}>转入总额</div>
          </div>
          <div className={styles.statItem}>
            <div className={styles.statValue}>-{balance.totalTransferredOut.toLocaleString()}</div>
            <div className={styles.statLabel}>转出总额</div>
          </div>
        </div>

        <div className={styles.conversionTip}>
          💡 兑换比例：1 白萝卜 = 1,000 胡萝卜
        </div>
      </div>

      {/* 交易记录 */}
      <div className={styles.transactionsSection}>
        <h3 className={styles.sectionTitle}>交易记录</h3>
        <CoinTransactionList apiBaseUrl={apiBaseUrl} />
      </div>
    </div>
  );
};
