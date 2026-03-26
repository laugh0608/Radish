import { Icon } from '@radish/ui/icon';
import { useTranslation } from 'react-i18next';
import { aboutContent } from '../data/aboutContent';
import styles from './AboutTab.module.css';

/**
 * 关于 Radish 标签页
 */
export const AboutTab = () => {
  const { t } = useTranslation();

  return (
    <div className={styles.container}>
      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <div className={styles.sectionIcon}>
            <Icon icon={aboutContent.vision.icon} size={22} className={styles.icon} />
          </div>
          <h2>{t(aboutContent.vision.titleKey)}</h2>
        </div>
        <p className={styles.sectionContent}>{t(aboutContent.vision.contentKey)}</p>
      </section>

      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <div className={styles.sectionIcon}>
            <Icon icon={aboutContent.mission.icon} size={22} className={styles.icon} />
          </div>
          <h2>{t(aboutContent.mission.titleKey)}</h2>
        </div>
        <p className={styles.sectionContent}>{t(aboutContent.mission.contentKey)}</p>
      </section>

      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <div className={styles.sectionIcon}>
            <Icon icon="mdi:star-four-points-circle-outline" size={22} className={styles.icon} />
          </div>
          <h2>{t('welcome.about.features.title')}</h2>
        </div>
        <div className={styles.featureGrid}>
          {aboutContent.features.map((feature) => (
            <div key={feature.nameKey} className={styles.featureCard}>
              <div className={styles.featureIcon}>
                <Icon icon={feature.icon} size={22} className={styles.icon} />
              </div>
              <h3 className={styles.featureName}>
                {t(feature.nameKey)}
                {feature.status === 'iterating' && (
                  <span className={styles.iteratingBadge}>{t('welcome.about.status.iterating')}</span>
                )}
                {feature.status === 'available' && (
                  <span className={styles.availableBadge}>{t('welcome.about.status.available')}</span>
                )}
              </h3>
              <p className={styles.featureDescription}>{t(feature.descriptionKey)}</p>
            </div>
          ))}
        </div>
      </section>

      <section className={styles.section}>
        <div className={styles.sectionHeader}>
          <div className={styles.sectionIcon}>
            <Icon icon="mdi:code-braces-box" size={22} className={styles.icon} />
          </div>
          <h2>{t('welcome.about.tech.title')}</h2>
        </div>
        <div className={styles.techGrid}>
          {aboutContent.techStack.map((tech) => (
            <div key={tech.nameKey} className={styles.techCard}>
              <div className={styles.techIcon}>
                <Icon icon={tech.icon} size={18} className={styles.icon} />
              </div>
              <div className={styles.techInfo}>
                <h4>{t(tech.nameKey)}</h4>
                <p>{t(tech.descriptionKey)}</p>
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
};
