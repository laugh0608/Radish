import { lazy, Suspense, useState } from 'react';
import { log } from '@/utils/logger';
import { useUserStore } from '@/stores/userStore';
import type { TabType } from './types';
import styles from './RadishPitApp.module.css';

const AccountOverview = lazy(() =>
  import('./components/AccountOverview/AccountOverview').then((module) => ({ default: module.AccountOverview }))
);
const Transfer = lazy(() =>
  import('./components/Transfer/Transfer').then((module) => ({ default: module.Transfer }))
);
const TransactionHistory = lazy(() =>
  import('./components/TransactionHistory/TransactionHistory').then((module) => ({ default: module.TransactionHistory }))
);
const SecuritySettings = lazy(() =>
  import('./components/SecuritySettings/SecuritySettings').then((module) => ({ default: module.SecuritySettings }))
);
const Statistics = lazy(() =>
  import('./components/Statistics/Statistics').then((module) => ({ default: module.Statistics }))
);

/**
 * 萝卜坑应用主组件
 */
export const RadishPitApp = () => {
  const { isAuthenticated } = useUserStore();
  const [activeTab, setActiveTab] = useState<TabType>('overview');

  if (!isAuthenticated()) {
    return (
      <div className={styles.container}>
        <div className={styles.notLoggedIn}>
          <div className={styles.notLoggedInIcon}>🥕</div>
          <h2>欢迎来到萝卜坑</h2>
          <p>请先登录以管理您的萝卜</p>
        </div>
      </div>
    );
  }

  const handleTabChange = (tab: TabType) => {
    log.debug('RadishPit', `切换到标签页: ${tab}`);
    setActiveTab(tab);
  };

  const renderTabContent = () => {
    switch (activeTab) {
      case 'overview':
        return <AccountOverview />;
      case 'transfer':
        return <Transfer />;
      case 'history':
        return <TransactionHistory />;
      case 'security':
        return <SecuritySettings />;
      case 'statistics':
        return <Statistics />;
      default:
        return <AccountOverview />;
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.topBar}>
        <div className={styles.brand}>
          <span className={styles.brandIcon}>🥕</span>
          <span className={styles.brandName}>萝卜坑</span>
          <span className={styles.brandHint}>资产中心</span>
        </div>
      </div>

      <div className={styles.navigation}>
        <div className={styles.tabList}>
          <button
            className={`${styles.tab} ${activeTab === 'overview' ? styles.active : ''}`}
            onClick={() => handleTabChange('overview')}
          >
            <span className={styles.tabIcon}>📊</span>
            <span className={styles.tabText}>账户总览</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'transfer' ? styles.active : ''}`}
            onClick={() => handleTabChange('transfer')}
          >
            <span className={styles.tabIcon}>💸</span>
            <span className={styles.tabText}>转移</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'history' ? styles.active : ''}`}
            onClick={() => handleTabChange('history')}
          >
            <span className={styles.tabIcon}>📋</span>
            <span className={styles.tabText}>记录</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'security' ? styles.active : ''}`}
            onClick={() => handleTabChange('security')}
          >
            <span className={styles.tabIcon}>🔒</span>
            <span className={styles.tabText}>安全</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'statistics' ? styles.active : ''}`}
            onClick={() => handleTabChange('statistics')}
          >
            <span className={styles.tabIcon}>📈</span>
            <span className={styles.tabText}>统计</span>
          </button>
        </div>
      </div>

      <div className={styles.content}>
        <div className={styles.contentInner}>
          <Suspense fallback={<div className={styles.loading}>加载中...</div>}>
            {renderTabContent()}
          </Suspense>
        </div>
      </div>
    </div>
  );
};
