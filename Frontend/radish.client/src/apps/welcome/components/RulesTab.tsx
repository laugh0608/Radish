import { Icon } from '@radish/ui/icon';
import { useTranslation } from 'react-i18next';
import { communityRules } from '../data/communityRules';
import styles from './RulesTab.module.css';

/**
 * 社区规则标签页
 */
export const RulesTab = () => {
  const { t } = useTranslation();

  const getSeverityClass = (severity?: 'high' | 'medium' | 'low') => {
    switch (severity) {
      case 'high':
        return styles.severityHigh;
      case 'medium':
        return styles.severityMedium;
      case 'low':
        return styles.severityLow;
      default:
        return '';
    }
  };

  const getSeverityLabel = (severity?: 'high' | 'medium' | 'low') => {
    switch (severity) {
      case 'high':
        return t('welcome.rules.severity.high');
      case 'medium':
        return t('welcome.rules.severity.medium');
      case 'low':
        return t('welcome.rules.severity.low');
      default:
        return '';
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.notice}>
        <div className={styles.noticeIcon}>
          <Icon icon="mdi:information-outline" size={18} className={styles.icon} />
        </div>
        <p>{t('welcome.rules.notice')}</p>
      </div>

      {communityRules.map((category) => (
        <section key={category.categoryKey} className={styles.category}>
          <div className={styles.categoryHeader}>
            <div className={styles.categoryIcon}>
              <Icon icon={category.icon} size={22} className={styles.icon} />
            </div>
            <h2>{t(category.categoryKey)}</h2>
          </div>
          <div className={styles.rulesList}>
            {category.rules.map((rule) => (
              <div key={rule.titleKey} className={styles.ruleCard}>
                <div className={styles.ruleHeader}>
                  <h3 className={styles.ruleTitle}>{t(rule.titleKey)}</h3>
                  {rule.severity && (
                    <span className={`${styles.severityBadge} ${getSeverityClass(rule.severity)}`}>
                      {getSeverityLabel(rule.severity)}
                    </span>
                  )}
                </div>
                <p className={styles.ruleDescription}>{t(rule.descriptionKey)}</p>
              </div>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
};
