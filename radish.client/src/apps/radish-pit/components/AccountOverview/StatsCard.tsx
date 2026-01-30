import { formatCoinAmount } from '../../utils';
import type { AccountStats } from '../../types';
import styles from './StatsCard.module.css';

interface StatsCardProps {
  stats: AccountStats | null;
  displayMode: 'carrot' | 'white';
}

/**
 * 统计卡片组件
 */
export const StatsCard = ({ stats, displayMode }: StatsCardProps) => {
  const useWhiteRadish = displayMode === 'white';

  if (!stats) {
    return (
      <div className={styles.card}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>加载统计信息中...</p>
        </div>
      </div>
    );
  }

  const statsItems = [
    {
      icon: '📈',
      label: '累计获得',
      value: stats.totalEarned,
      color: 'green'
    },
    {
      icon: '📉',
      label: '累计消费',
      value: stats.totalSpent,
      color: 'red'
    },
    {
      icon: '📥',
      label: '累计转入',
      value: stats.totalTransferredIn,
      color: 'blue'
    },
    {
      icon: '📤',
      label: '累计转出',
      value: stats.totalTransferredOut,
      color: 'orange'
    }
  ];

  return (
    <div className={styles.card}>
      <div className={styles.header}>
        <h3 className={styles.title}>
          <span className={styles.icon}>📊</span>
          账户统计
        </h3>
      </div>

      <div className={styles.content}>
        <div className={styles.statsGrid}>
          {statsItems.map((item, index) => (
            <div key={index} className={styles.statItem}>
              <div className={styles.statIcon}>{item.icon}</div>
              <div className={styles.statContent}>
                <div className={styles.statLabel}>{item.label}</div>
                <div className={`${styles.statValue} ${styles[item.color]}`}>
                  {formatCoinAmount(item.value, true, useWhiteRadish)}
                </div>
              </div>
            </div>
          ))}
        </div>

        {/* 净收益计算 */}
        <div className={styles.netProfit}>
          <div className={styles.netProfitLabel}>净收益</div>
          <div className={`${styles.netProfitValue} ${
            (stats.totalEarned + stats.totalTransferredIn - stats.totalSpent - stats.totalTransferredOut) >= 0
              ? styles.positive
              : styles.negative
          }`}>
            {formatCoinAmount(
              stats.totalEarned + stats.totalTransferredIn - stats.totalSpent - stats.totalTransferredOut,
              true,
              useWhiteRadish
            )}
          </div>
        </div>

        {/* 最近活动 */}
        <div className={styles.recentActivity}>
          <div className={styles.activityLabel}>最近记录</div>
          <div className={styles.activityValue}>
            {stats.recentTransactionCount} 条
          </div>
        </div>
      </div>
    </div>
  );
};