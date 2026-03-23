import { useState } from 'react';
import { Trans, useTranslation } from 'react-i18next';
import { useUserStore } from '@/stores/userStore';
import { Icon } from '@radish/ui/icon';
import { AboutTab, OpenSourceTab, QuickStartTab, RulesTab } from './components';
import styles from './WelcomeApp.module.css';

type TabType = 'about' | 'quickstart' | 'rules' | 'open-source';

interface Tab {
  id: TabType;
  labelKey: string;
  icon: string;
}

const tabs: Tab[] = [
  { id: 'about', labelKey: 'welcome.tabs.about', icon: 'mdi:compass-outline' },
  { id: 'quickstart', labelKey: 'welcome.tabs.quickstart', icon: 'mdi:rocket-launch-outline' },
  { id: 'rules', labelKey: 'welcome.tabs.rules', icon: 'mdi:shield-half-full' },
  { id: 'open-source', labelKey: 'welcome.tabs.openSource', icon: 'mdi:open-source-initiative' }
];

/**
 * 欢迎应用
 *
 * 展示 Radish 社区简介、快速入门指南和社区规则
 */
export const WelcomeApp = () => {
  const { t } = useTranslation();
  const { userName } = useUserStore();
  const [activeTab, setActiveTab] = useState<TabType>('about');
  const displayName = userName?.trim() || t('welcome.common.friendFallback');

  const heroHighlights = [
    {
      label: t('welcome.hero.highlights.entry.label'),
      value: t('welcome.hero.highlights.entry.value')
    },
    {
      label: t('welcome.hero.highlights.focus.label'),
      value: t('welcome.hero.highlights.focus.value')
    },
    {
      label: t('welcome.hero.highlights.suggestion.label'),
      value: t('welcome.hero.highlights.suggestion.value')
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
            <h1 className={styles.title}>{t('welcome.hero.title')}</h1>
            <p className={styles.subtitle}>
              <Trans
                i18nKey="welcome.hero.subtitle"
                values={{ name: displayName }}
                components={{ strong: <strong /> }}
              />
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
                <span>{t(tab.labelKey)}</span>
              </button>
            ))}
          </div>
        </section>

        <div className={styles.contentShell}>
          <div className={styles.contentHeader}>
            <div>
              <h2 className={styles.contentTitle}>{t(tabs.find((tab) => tab.id === activeTab)?.labelKey ?? 'welcome.tabs.about')}</h2>
              <p className={styles.contentHint}>{t('welcome.content.hint')}</p>
            </div>
          </div>

          <div className={styles.content}>
            {renderTabContent()}
          </div>
        </div>

        <footer className={styles.footer}>
          <div className={styles.footerInfo}>
            <span className={styles.footerBadge}>{t('welcome.footer.badge')}</span>
            <p className={styles.footerText}>{t('welcome.footer.text')}</p>
          </div>
        </footer>
      </div>
    </div>
  );
};
