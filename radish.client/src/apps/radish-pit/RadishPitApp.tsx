import { useState } from 'react';
import { log } from '@/utils/logger';
import { useUserStore } from '@/stores/userStore';
import { AccountOverview } from './components/AccountOverview/AccountOverview';
import { Transfer } from './components/Transfer/Transfer';
import { TransactionHistory } from './components/TransactionHistory/TransactionHistory';
import { SecuritySettings } from './components/SecuritySettings/SecuritySettings';
import { Statistics } from './components/Statistics/Statistics';
import type { TabType } from './types';
import styles from './RadishPitApp.module.css';

/**
 * 萝卜坑应用主组件
 */
export const RadishPitApp = () => {
  const { isAuthenticated } = useUserStore();
  const [activeTab, setActiveTab] = useState<TabType>('overview');

  // 检查用户认证状态
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
      {/* 头部 */}
      <div className={styles.header}>
        <div className={styles.headerContent}>
          <div className={styles.titleSection}>
            <h1 className={styles.title}>
              <span className={styles.titleIcon}>🥕</span>
              萝卜坑
            </h1>
            <p className={styles.subtitle}>您的萝卜管理中心</p>
          </div>
        </div>
      </div>

      {/* 导航标签 */}
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

      {/* 内容区域 */}
      <div className={styles.content}>
        {renderTabContent()}
      </div>
    </div>
  );
};