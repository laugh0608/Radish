import { useState } from 'react';
import { log } from '@/utils/logger';
import { useSecurityStatus } from '../../hooks';
import { SecurityOverview } from './SecurityOverview';
import { PaymentPasswordSettings } from './PaymentPasswordSettings';
import { SecurityLog } from './SecurityLog';
import { SecurityTips } from './SecurityTips';
import styles from './SecuritySettings.module.css';

type SecurityTab = 'overview' | 'password' | 'log' | 'tips';

/**
 * 安全设置组件
 */
export const SecuritySettings = () => {
  const [activeTab, setActiveTab] = useState<SecurityTab>('overview');
  const { status, loading, error, refetch } = useSecurityStatus();

  const handleTabChange = (tab: SecurityTab) => {
    log.debug('SecuritySettings', `切换到标签页: ${tab}`);
    setActiveTab(tab);
  };

  const handleSecurityUpdate = () => {
    log.debug('SecuritySettings', '安全设置更新，刷新状态');
    refetch();
  };

  const renderTabContent = () => {
    switch (activeTab) {
      case 'overview':
        return (
          <SecurityOverview
            status={status}
            loading={loading}
            error={error}
            onRefresh={refetch}
          />
        );
      case 'password':
        return (
          <PaymentPasswordSettings
            status={status}
            onUpdate={handleSecurityUpdate}
          />
        );
      case 'log':
        return <SecurityLog />;
      case 'tips':
        return <SecurityTips status={status} />;
      default:
        return null;
    }
  };

  return (
    <div className={styles.container}>
      {/* 页面标题 */}
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <h2 className={styles.title}>安全设置</h2>
          <p className={styles.subtitle}>管理您的萝卜坑账户安全设置</p>
        </div>
        <div className={styles.headerRight}>
          <div className={`${styles.securityLevel} ${
            status?.isLocked ? styles.weak :
            status?.hasPaymentPassword ? styles.secure : styles.moderate
          }`}>
            <span className={styles.securityIcon}>🔒</span>
            <span className={styles.securityText}>
              {status?.isLocked ? '较弱' : status?.hasPaymentPassword ? '安全' : '一般'}
            </span>
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
            <span className={styles.tabText}>安全概览</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'password' ? styles.active : ''}`}
            onClick={() => handleTabChange('password')}
          >
            <span className={styles.tabIcon}>🔑</span>
            <span className={styles.tabText}>支付密码</span>
            {!status?.hasPaymentPassword && (
              <span className={styles.tabBadge}>!</span>
            )}
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'log' ? styles.active : ''}`}
            onClick={() => handleTabChange('log')}
          >
            <span className={styles.tabIcon}>📋</span>
            <span className={styles.tabText}>安全日志</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'tips' ? styles.active : ''}`}
            onClick={() => handleTabChange('tips')}
          >
            <span className={styles.tabIcon}>💡</span>
            <span className={styles.tabText}>安全建议</span>
            {status && !status.hasPaymentPassword && (
              <span className={styles.tabBadge}>1</span>
            )}
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
