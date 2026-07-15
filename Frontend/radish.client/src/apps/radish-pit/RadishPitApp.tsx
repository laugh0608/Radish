import { lazy, Suspense, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation();
  const { isAuthenticated } = useUserStore();
  const [activeTab, setActiveTab] = useState<TabType>('overview');

  if (!isAuthenticated()) {
    return (
      <div className={styles.container}>
        <div className={styles.notLoggedIn}>
          <div className={styles.notLoggedInIcon}>🥕</div>
          <h2>{t('pit.auth.title')}</h2>
          <p>{t('pit.auth.description')}</p>
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
        return <AccountOverview onNavigate={handleTabChange} />;
      case 'transfer':
        return <Transfer onNavigate={handleTabChange} />;
      case 'history':
        return <TransactionHistory />;
      case 'security':
        return <SecuritySettings />;
      case 'statistics':
        return <Statistics />;
      default:
        return <AccountOverview onNavigate={handleTabChange} />;
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.topBar}>
        <div className={styles.brand}>
          <span className={styles.brandIcon}>🥕</span>
          <span className={styles.brandName}>{t('pit.title')}</span>
          <span className={styles.brandHint}>{t('pit.subtitle')}</span>
        </div>
      </div>

      <div className={styles.navigation}>
        <div className={styles.tabList}>
          <button
            className={`${styles.tab} ${activeTab === 'overview' ? styles.active : ''}`}
            onClick={() => handleTabChange('overview')}
          >
            <span className={styles.tabIcon}>📊</span>
            <span className={styles.tabText}>{t('pit.tab.overview')}</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'transfer' ? styles.active : ''}`}
            onClick={() => handleTabChange('transfer')}
          >
            <span className={styles.tabIcon}>💸</span>
            <span className={styles.tabText}>{t('pit.tab.transfer')}</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'history' ? styles.active : ''}`}
            onClick={() => handleTabChange('history')}
          >
            <span className={styles.tabIcon}>📋</span>
            <span className={styles.tabText}>{t('pit.tab.history')}</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'security' ? styles.active : ''}`}
            onClick={() => handleTabChange('security')}
          >
            <span className={styles.tabIcon}>🔒</span>
            <span className={styles.tabText}>{t('pit.tab.security')}</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'statistics' ? styles.active : ''}`}
            onClick={() => handleTabChange('statistics')}
          >
            <span className={styles.tabIcon}>📈</span>
            <span className={styles.tabText}>{t('pit.tab.statistics')}</span>
          </button>
        </div>
      </div>

      <div className={styles.content}>
        <div className={styles.contentInner}>
          <Suspense fallback={<div className={styles.loading}>{t('pit.common.loading')}</div>}>
            {renderTabContent()}
          </Suspense>
        </div>
      </div>
    </div>
  );
};
