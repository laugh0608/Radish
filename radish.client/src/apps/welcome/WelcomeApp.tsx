import { useState } from 'react';
import { useUserStore } from '@/stores/userStore';
import { Icon } from '@radish/ui';
import { AboutTab, QuickStartTab, RulesTab } from './components';
import styles from './WelcomeApp.module.css';

type TabType = 'about' | 'quickstart' | 'rules';

interface Tab {
  id: TabType;
  label: string;
  icon: string;
}

const tabs: Tab[] = [
  { id: 'about', label: '关于 Radish', icon: 'mdi:information' },
  { id: 'quickstart', label: '快速入门', icon: 'mdi:rocket-launch' },
  { id: 'rules', label: '社区规则', icon: 'mdi:shield-check' }
];

/**
 * 欢迎应用
 *
 * 展示 Radish 社区简介、快速入门指南和社区规则
 */
export const WelcomeApp = () => {
  const { userName } = useUserStore();
  const [activeTab, setActiveTab] = useState<TabType>('about');

  const renderTabContent = () => {
    switch (activeTab) {
      case 'about':
        return <AboutTab />;
      case 'quickstart':
        return <QuickStartTab />;
      case 'rules':
        return <RulesTab />;
      default:
        return null;
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.wrapper}>
        {/* 顶部欢迎区域 */}
        <div className={styles.hero}>
          <Icon icon="mdi:hand-wave" size={64} color="#667eea" />
          <h1 className={styles.title}>欢迎来到 Radish WebOS</h1>
          <p className={styles.subtitle}>
            你好，<strong>{userName}</strong>！欢迎使用 Radish 社区平台
          </p>
        </div>

        {/* 标签栏 */}
        <div className={styles.tabs}>
          {tabs.map((tab) => (
            <button
              key={tab.id}
              className={`${styles.tab} ${activeTab === tab.id ? styles.tabActive : ''}`}
              onClick={() => setActiveTab(tab.id)}
            >
              <Icon icon={tab.icon} size={20} />
              <span>{tab.label}</span>
            </button>
          ))}
        </div>

        {/* 内容区域 */}
        <div className={styles.content}>
          {renderTabContent()}
        </div>

        {/* 底部信息 */}
        <div className={styles.footer}>
          <div className={styles.footerInfo}>
            <span className={styles.version}>Radish v0.1.0</span>
            <span className={styles.separator}>•</span>
            <a
              href="http://localhost:3100/docs/"
              target="_blank"
              rel="noopener noreferrer"
              className={styles.footerLink}
            >
              <Icon icon="mdi:book-open-page-variant" size={16} />
              文档中心
            </a>
            <span className={styles.separator}>•</span>
            <a
              href="https://github.com/your-org/radish"
              target="_blank"
              rel="noopener noreferrer"
              className={styles.footerLink}
            >
              <Icon icon="mdi:github" size={16} />
              GitHub
            </a>
          </div>
        </div>
      </div>
    </div>
  );
};
