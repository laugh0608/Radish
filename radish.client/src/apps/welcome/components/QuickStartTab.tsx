import { Icon } from '@radish/ui';
import { quickStartSteps } from '../data/quickStartSteps';
import styles from './QuickStartTab.module.css';

/**
 * 快速入门标签页
 */
export const QuickStartTab = () => {
  return (
    <div className={styles.container}>
      {quickStartSteps.map((category, categoryIndex) => (
        <section key={categoryIndex} className={styles.category}>
          <div className={styles.categoryHeader}>
            <Icon icon={category.icon} size={28} color="#667eea" />
            <h2>{category.category}</h2>
          </div>
          <div className={styles.stepsList}>
            {category.steps.map((step, stepIndex) => (
              <div key={stepIndex} className={styles.stepCard}>
                <div className={styles.stepNumber}>{stepIndex + 1}</div>
                <div className={styles.stepIcon}>
                  <Icon icon={step.icon} size={24} color="#667eea" />
                </div>
                <div className={styles.stepContent}>
                  <h3 className={styles.stepTitle}>{step.title}</h3>
                  <p className={styles.stepDescription}>{step.description}</p>
                </div>
              </div>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
};
