import { useTranslation } from 'react-i18next';
import { addCoinValues, compareCoinValues, formatCoinAmount, formatCoinNumber, subtractCoinValues } from '../../utils';
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
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;

  if (!stats) {
    return (
      <div className={styles.card}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>{t('pit.statistics.loading')}</p>
        </div>
      </div>
    );
  }

  const statsItems = [
    {
      icon: '📈',
      label: t('pit.overview.totalEarned'),
      value: stats.totalEarned,
      color: 'green'
    },
    {
      icon: '📉',
      label: t('pit.overview.totalSpent'),
      value: stats.totalSpent,
      color: 'red'
    },
    {
      icon: '📥',
      label: t('pit.overview.totalTransferredIn'),
      value: stats.totalTransferredIn,
      color: 'blue'
    },
    {
      icon: '📤',
      label: t('pit.overview.totalTransferredOut'),
      value: stats.totalTransferredOut,
      color: 'orange'
    }
  ];

  return (
    <div className={styles.card}>
      <div className={styles.header}>
        <h3 className={styles.title}>
          <span className={styles.icon}>📊</span>
          {t('pit.overview.statisticsTitle')}
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
                  {formatCoinAmount(item.value, language, t, displayMode)}
                </div>
              </div>
            </div>
          ))}
        </div>

        {/* 净收益计算 */}
        <div className={styles.netProfit}>
          <div className={styles.netProfitLabel}>{t('pit.overview.netIncome')}</div>
          <div className={`${styles.netProfitValue} ${
            compareCoinValues(
              subtractCoinValues(
                addCoinValues(stats.totalEarned, stats.totalTransferredIn),
                addCoinValues(stats.totalSpent, stats.totalTransferredOut),
              ),
              0,
            ) >= 0
              ? styles.positive
              : styles.negative
          }`}>
            {formatCoinAmount(
              subtractCoinValues(
                addCoinValues(stats.totalEarned, stats.totalTransferredIn),
                addCoinValues(stats.totalSpent, stats.totalTransferredOut),
              ),
              language,
              t,
              displayMode,
            )}
          </div>
        </div>

        {/* 最近活动 */}
        <div className={styles.recentActivity}>
          <div className={styles.activityLabel}>{t('pit.overview.recentActivity')}</div>
          <div className={styles.activityValue}>
            {t('pit.overview.recentCount', {
              count: stats.recentTransactionCount,
              value: formatCoinNumber(stats.recentTransactionCount, language),
            })}
          </div>
        </div>
      </div>
    </div>
  );
};
