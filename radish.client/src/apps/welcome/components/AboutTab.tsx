import { Icon } from '@radish/ui';
import { aboutContent } from '../data/aboutContent';
import styles from './AboutTab.module.css';

/**
 * 关于 Radish 标签页
 */
export const AboutTab = () => {
  return (
    <div className={styles.container}>
      {/* 社区愿景 */}
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <Icon icon={aboutContent.vision.icon} size={28} color="#667eea" />
          <h2>{aboutContent.vision.title}</h2>
        </div>
        <p className={styles.sectionContent}>{aboutContent.vision.content}</p>
      </section>

      {/* 我们的使命 */}
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <Icon icon={aboutContent.mission.icon} size={28} color="#667eea" />
          <h2>{aboutContent.mission.title}</h2>
        </div>
        <p className={styles.sectionContent}>{aboutContent.mission.content}</p>
      </section>

      {/* 核心功能 */}
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <Icon icon="mdi:star" size={28} color="#667eea" />
          <h2>核心功能</h2>
        </div>
        <div className={styles.featureGrid}>
          {aboutContent.features.map((feature, index) => (
            <div key={index} className={styles.featureCard}>
              <div className={styles.featureIcon}>
                <Icon icon={feature.icon} size={32} color="#667eea" />
              </div>
              <h3 className={styles.featureName}>
                {feature.name}
                {feature.status === 'planned' && (
                  <span className={styles.plannedBadge}>规划中</span>
                )}
              </h3>
              <p className={styles.featureDescription}>{feature.description}</p>
            </div>
          ))}
        </div>
      </section>

      {/* 技术亮点 */}
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <Icon icon="mdi:code-braces" size={28} color="#667eea" />
          <h2>技术亮点</h2>
        </div>
        <div className={styles.techGrid}>
          {aboutContent.techStack.map((tech, index) => (
            <div key={index} className={styles.techCard}>
              <Icon icon={tech.icon} size={24} color="#667eea" />
              <div className={styles.techInfo}>
                <h4>{tech.name}</h4>
                <p>{tech.description}</p>
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
};
