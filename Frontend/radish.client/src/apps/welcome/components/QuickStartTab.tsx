import { Icon } from '@radish/ui/icon';
import { useTranslation } from 'react-i18next';
import { quickStartSteps } from '../data/quickStartSteps';
import styles from './QuickStartTab.module.css';

/**
 * 快速入门标签页
 */
export const QuickStartTab = () => {
  const { t } = useTranslation();

  return (
    <div className={styles.container}>
      {quickStartSteps.map((category) => (
        <section key={category.categoryKey} className={styles.category}>
          <div className={styles.categoryHeader}>
            <div className={styles.categoryIcon}>
              <Icon icon={category.icon} size={22} className={styles.icon} />
            </div>
            <h2>{t(category.categoryKey)}</h2>
          </div>
          <div className={styles.stepsList}>
            {category.steps.map((step, stepIndex) => (
              <div key={step.titleKey} className={styles.stepCard}>
                <div className={styles.stepNumber}>{stepIndex + 1}</div>
                <div className={styles.stepIcon}>
                  <Icon icon={step.icon} size={20} className={styles.icon} />
                </div>
                <div className={styles.stepContent}>
                  <h3 className={styles.stepTitle}>{t(step.titleKey)}</h3>
                  <p className={styles.stepDescription}>{t(step.descriptionKey)}</p>
                </div>
              </div>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
};
