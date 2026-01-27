import { useState } from 'react';
import { useCoinBalance, useAccountStats } from '../../hooks';
import { formatCoinAmount, formatDateTime } from '../../utils';
import { BalanceCard } from './BalanceCard';
import { StatsCard } from './StatsCard';
import { RecentTransactions } from './RecentTransactions';
import { QuickActions } from './QuickActions';
import styles from './AccountOverview.module.css';

/**
 * 账户总览组件
 */
export const AccountOverview = () => {
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
          <p>加载账户信息中...</p>
        </div>
      </div>
    );
  }

  if (balanceError || statsError) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <div className={styles.errorIcon}>⚠️</div>
          <h3>加载失败</h3>
          <p>{balanceError || statsError}</p>
          <button className={styles.retryButton} onClick={handleRefresh}>
            重试
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
          <h2 className={styles.title}>账户总览</h2>
          <p className={styles.subtitle}>查看您的萝卜存量和统计信息</p>
        </div>
        <div className={styles.headerRight}>
          <button
            className={styles.displayModeButton}
            onClick={toggleDisplayMode}
            title={`切换到${displayMode === 'carrot' ? '白萝卜' : '胡萝卜'}显示`}
          >
            {displayMode === 'carrot' ? '🥕' : '🤍'}
            {displayMode === 'carrot' ? '胡萝卜' : '白萝卜'}
          </button>
          <button className={styles.refreshButton} onClick={handleRefresh} title="刷新数据">
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
          <QuickActions />
        </div>

        {/* 最近交易 */}
        <div className={styles.transactionsSection}>
          <RecentTransactions displayMode={displayMode} />
        </div>
      </div>
    </div>
  );
};