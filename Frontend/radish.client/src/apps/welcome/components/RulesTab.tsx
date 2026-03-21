import { Icon } from '@radish/ui/icon';
import { communityRules } from '../data/communityRules';
import styles from './RulesTab.module.css';

/**
 * 社区规则标签页
 */
export const RulesTab = () => {
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
        return '重要';
      case 'medium':
        return '一般';
      case 'low':
        return '提示';
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
        <p>
          这不是冗长的公告墙，而是当前阶段最需要遵守的一组社区约定。它们的目标是减少噪音、保护边界，并帮助讨论回到问题本身。
        </p>
      </div>

      {communityRules.map((category, categoryIndex) => (
        <section key={categoryIndex} className={styles.category}>
          <div className={styles.categoryHeader}>
            <div className={styles.categoryIcon}>
              <Icon icon={category.icon} size={22} className={styles.icon} />
            </div>
            <h2>{category.category}</h2>
          </div>
          <div className={styles.rulesList}>
            {category.rules.map((rule, ruleIndex) => (
              <div key={ruleIndex} className={styles.ruleCard}>
                <div className={styles.ruleHeader}>
                  <h3 className={styles.ruleTitle}>{rule.title}</h3>
                  {rule.severity && (
                    <span className={`${styles.severityBadge} ${getSeverityClass(rule.severity)}`}>
                      {getSeverityLabel(rule.severity)}
                    </span>
                  )}
                </div>
                <p className={styles.ruleDescription}>{rule.description}</p>
              </div>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
};
