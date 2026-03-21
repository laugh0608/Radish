import { Icon } from '@radish/ui/icon';
import { aboutContent } from '../data/aboutContent';
import styles from './AboutTab.module.css';

/**
 * 关于 Radish 标签页
 */
export const AboutTab = () => {
  return (
    <div className={styles.container}>
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <div className={styles.sectionIcon}>
            <Icon icon={aboutContent.vision.icon} size={22} className={styles.icon} />
          </div>
          <h2>{aboutContent.vision.title}</h2>
        </div>
        <p className={styles.sectionContent}>{aboutContent.vision.content}</p>
      </section>

      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <div className={styles.sectionIcon}>
            <Icon icon={aboutContent.mission.icon} size={22} className={styles.icon} />
          </div>
          <h2>{aboutContent.mission.title}</h2>
        </div>
        <p className={styles.sectionContent}>{aboutContent.mission.content}</p>
      </section>

      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <div className={styles.sectionIcon}>
            <Icon icon="mdi:star-four-points-circle-outline" size={22} className={styles.icon} />
          </div>
          <h2>当前核心能力</h2>
        </div>
        <div className={styles.featureGrid}>
          {aboutContent.features.map((feature, index) => (
            <div key={index} className={styles.featureCard}>
              <div className={styles.featureIcon}>
                <Icon icon={feature.icon} size={22} className={styles.icon} />
              </div>
              <h3 className={styles.featureName}>
                {feature.name}
                {feature.status === 'iterating' && (
                  <span className={styles.iteratingBadge}>持续完善</span>
                )}
                {feature.status === 'available' && (
                  <span className={styles.availableBadge}>可体验</span>
                )}
              </h3>
              <p className={styles.featureDescription}>{feature.description}</p>
            </div>
          ))}
        </div>
      </section>

      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <div className={styles.sectionIcon}>
            <Icon icon="mdi:code-braces-box" size={22} className={styles.icon} />
          </div>
          <h2>工程基线</h2>
        </div>
        <div className={styles.techGrid}>
          {aboutContent.techStack.map((tech, index) => (
            <div key={index} className={styles.techCard}>
              <div className={styles.techIcon}>
                <Icon icon={tech.icon} size={18} className={styles.icon} />
              </div>
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
