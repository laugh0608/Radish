import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useCoinBalance, useAccountStats } from '../../hooks';
import { BalanceCard } from './BalanceCard';
import { StatsCard } from './StatsCard';
import { RecentTransactions } from './RecentTransactions';
import { QuickActions } from './QuickActions';
import type { TabType } from '../../types';
import styles from './AccountOverview.module.css';

interface AccountOverviewProps {
  onNavigate: (tab: TabType) => void;
}

/**
 * 账户总览组件
 */
export const AccountOverview = ({ onNavigate }: AccountOverviewProps) => {
  const { t } = useTranslation();
  const [displayMode, setDisplayMode] = useState<'carrot' | 'white'>('carrot');
  const { balance, frozenBalance, loading: balanceLoading, error: balanceError, refetch: refetchBalance } = useCoinBalance();
  const { stats, loading: statsLoading, error: statsError, refetch: refetchStats } = useAccountStats();

  const handleRefresh = async () => {
    await Promise.all([refetchBalance(), refetchStats()]);
  };

  const toggleDisplayMode = () => {
    setDisplayMode(prev => prev === 'carrot' ? 'white' : 'carrot');
  };

  if (balanceLoading || statsLoading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>{t('pit.overview.loading')}</p>
        </div>
      </div>
    );
  }

  if (balanceError || statsError) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <div className={styles.errorIcon}>⚠️</div>
          <h3>{t('pit.common.loadFailed')}</h3>
          <p>{balanceError || statsError}</p>
          <button className={styles.retryButton} onClick={handleRefresh}>
            {t('pit.common.retry')}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      {/* 页面标题和操作 */}
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <h2 className={styles.title}>{t('pit.overview.title')}</h2>
          <p className={styles.subtitle}>{t('pit.overview.description')}</p>
        </div>
        <div className={styles.headerRight}>
          <button
            className={styles.displayModeButton}
            onClick={toggleDisplayMode}
            title={t('pit.currency.switchTo', {
              mode: t(displayMode === 'carrot' ? 'pit.currency.white' : 'pit.currency.carrot'),
            })}
          >
            {displayMode === 'carrot' ? '🥕' : '🤍'}
            {t(displayMode === 'carrot' ? 'pit.currency.carrot' : 'pit.currency.white')}
          </button>
          <button className={styles.refreshButton} onClick={handleRefresh} title={t('pit.common.refresh')}>
            🔄
          </button>
        </div>
      </div>

      {/* 主要内容区域 */}
      <div className={styles.content}>
        {/* 余额卡片 */}
        <div className={styles.balanceSection}>
          <BalanceCard
            balance={balance}
            frozenBalance={frozenBalance}
            displayMode={displayMode}
          />
        </div>

        {/* 统计卡片 */}
        <div className={styles.statsSection}>
          <StatsCard
            stats={stats}
            displayMode={displayMode}
          />
        </div>

        {/* 快捷操作 */}
        <div className={styles.actionsSection}>
          <QuickActions onNavigate={onNavigate} />
        </div>

        {/* 最近交易 */}
        <div className={styles.transactionsSection}>
          <RecentTransactions displayMode={displayMode} onViewAll={() => onNavigate('history')} />
        </div>
      </div>
    </div>
  );
};
