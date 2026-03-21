import { useState } from 'react';
import { useUserStore } from '@/stores/userStore';
import { Icon } from '@radish/ui/icon';
import { AboutTab, OpenSourceTab, QuickStartTab, RulesTab } from './components';
import styles from './WelcomeApp.module.css';

type TabType = 'about' | 'quickstart' | 'rules' | 'open-source';

interface Tab {
  id: TabType;
  label: string;
  icon: string;
}

const tabs: Tab[] = [
  { id: 'about', label: '平台概览', icon: 'mdi:compass-outline' },
  { id: 'quickstart', label: '上手路径', icon: 'mdi:rocket-launch-outline' },
  { id: 'rules', label: '社区约定', icon: 'mdi:shield-half-full' },
  { id: 'open-source', label: '开源说明', icon: 'mdi:open-source-initiative' }
];

/**
 * 欢迎应用
 *
 * 展示 Radish 社区简介、快速入门指南和社区规则
 */
export const WelcomeApp = () => {
  const { userName } = useUserStore();
  const [activeTab, setActiveTab] = useState<TabType>('about');
  const displayName = userName?.trim() || '朋友';

  const heroHighlights = [
    {
      label: '桌面入口',
      value: '论坛、文档、通知、萝卜坑'
    },
    {
      label: '当前主线',
      value: '主题与 i18n 联调优化'
    },
    {
      label: '体验建议',
      value: '先看概览，再逐步打开应用'
    }
  ];

  const renderTabContent = () => {
    switch (activeTab) {
      case 'about':
        return <AboutTab />;
      case 'quickstart':
        return <QuickStartTab />;
      case 'rules':
        return <RulesTab />;
      case 'open-source':
        return <OpenSourceTab />;
      default:
        return null;
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.wrapper}>
        <section className={styles.hero}>
          <div className={styles.heroIconWrap}>
            <Icon icon="mdi:hand-wave" size={40} className={styles.heroIcon} />
          </div>
          <div className={styles.heroContent}>
            <span className={styles.eyebrow}>Radish WebOS</span>
            <h1 className={styles.title}>欢迎来到你的桌面工作台</h1>
            <p className={styles.subtitle}>
              你好，<strong>{displayName}</strong>。这里汇集论坛、文档、通知、个人主页与萝卜坑等主要入口，
              当前重点正在围绕主题一致性、双语切换与桌面交互体验持续优化。
            </p>
          </div>

          <div className={styles.heroStats}>
            {heroHighlights.map((item) => (
              <div key={item.label} className={styles.heroStatCard}>
                <span className={styles.heroStatLabel}>{item.label}</span>
                <strong className={styles.heroStatValue}>{item.value}</strong>
              </div>
            ))}
          </div>
        </section>

        <section className={styles.tabsSection}>
          <div className={styles.tabs}>
            {tabs.map((tab) => (
              <button
                key={tab.id}
                className={`${styles.tab} ${activeTab === tab.id ? styles.tabActive : ''}`}
                onClick={() => setActiveTab(tab.id)}
              >
                <Icon icon={tab.icon} size={18} className={styles.tabIcon} />
                <span>{tab.label}</span>
              </button>
            ))}
          </div>
        </section>

        <div className={styles.contentShell}>
          <div className={styles.contentHeader}>
            <div>
              <h2 className={styles.contentTitle}>{tabs.find((tab) => tab.id === activeTab)?.label}</h2>
              <p className={styles.contentHint}>
                本页内容会跟随桌面应用联调持续更新，具体体验以当前桌面中可见的应用与入口为准。
              </p>
            </div>
          </div>

          <div className={styles.content}>
            {renderTabContent()}
          </div>
        </div>

        <footer className={styles.footer}>
          <div className={styles.footerInfo}>
            <span className={styles.footerBadge}>联调阶段</span>
            <p className={styles.footerText}>
              欢迎页侧重说明当前能力边界、常用入口和基础规则，不再作为版本公告页。
            </p>
          </div>
        </footer>
      </div>
    </div>
  );
};
