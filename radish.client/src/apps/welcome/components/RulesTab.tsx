import { Icon } from '@radish/ui';
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
        <Icon icon="mdi:information" size={20} color="#667eea" />
        <p>
          请仔细阅读并遵守以下社区规则，共同维护良好的社区环境。违规行为将受到相应处理。
        </p>
      </div>

      {communityRules.map((category, categoryIndex) => (
        <section key={categoryIndex} className={styles.category}>
          <div className={styles.categoryHeader}>
            <Icon icon={category.icon} size={28} color="#667eea" />
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
