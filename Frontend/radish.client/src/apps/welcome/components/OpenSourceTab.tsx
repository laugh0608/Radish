import { Icon } from '@radish/ui/icon';
import { useTranslation } from 'react-i18next';
import {
  openSourceGroups,
  openSourceOverview,
  openSourceProjects
} from '../data/openSourceCatalog';
import styles from './OpenSourceTab.module.css';

const groupedProjects = openSourceGroups.map((group) => ({
  ...group,
  projects: openSourceProjects.filter((project) => project.groupId === group.id)
}));

/**
 * 开源软件声明标签页
 */
export const OpenSourceTab = () => {
  const { t } = useTranslation();
  const summaryCards = [
    {
      label: t('welcome.openSource.summary.projectCount.label'),
      value: t('welcome.openSource.summary.projectCount.value', { count: openSourceProjects.length }),
      tip: t('welcome.openSource.summary.projectCount.tip')
    },
    {
      label: t('welcome.openSource.summary.coreCount.label'),
      value: t('welcome.openSource.summary.coreCount.value', {
        count: openSourceProjects.filter((project) => project.isCore).length
      }),
      tip: t('welcome.openSource.summary.coreCount.tip')
    },
    {
      label: t('welcome.openSource.summary.groupCount.label'),
      value: t('welcome.openSource.summary.groupCount.value', { count: groupedProjects.length }),
      tip: t('welcome.openSource.summary.groupCount.tip')
    },
    {
      label: t('welcome.openSource.summary.moduleCount.label'),
      value: t('welcome.openSource.summary.moduleCount.value', {
        count: new Set(openSourceProjects.flatMap((project) => project.usedInKeys)).size
      }),
      tip: t('welcome.openSource.summary.moduleCount.tip')
    }
  ];

  return (
    <div className={styles.container}>
      <section className={styles.heroCard}>
        <div className={styles.heroHeader}>
          <div className={styles.heroIcon}>
            <Icon icon="mdi:open-source-initiative" size={24} className={styles.icon} />
          </div>
          <div className={styles.heroContent}>
            <h2>{t(openSourceOverview.titleKey)}</h2>
            <p>{t(openSourceOverview.summaryKey)}</p>
          </div>
        </div>

        <div className={styles.metaGrid}>
          <div className={styles.metaCard}>
            <span className={styles.metaLabel}>{t('welcome.openSource.meta.scope')}</span>
            <p className={styles.metaValue}>{t(openSourceOverview.scopeKey)}</p>
          </div>
          <div className={styles.metaCard}>
            <span className={styles.metaLabel}>{t('welcome.openSource.meta.source')}</span>
            <p className={styles.metaValue}>{t(openSourceOverview.sourceKey)}</p>
          </div>
          <div className={styles.metaCard}>
            <span className={styles.metaLabel}>{t('welcome.openSource.meta.maintenance')}</span>
            <p className={styles.metaValue}>{t(openSourceOverview.maintenanceKey)}</p>
          </div>
          <div className={styles.metaCard}>
            <span className={styles.metaLabel}>{t('welcome.openSource.meta.nextStep')}</span>
            <p className={styles.metaValue}>{t(openSourceOverview.nextStepKey)}</p>
          </div>
        </div>

        <div className={styles.summaryGrid}>
          {summaryCards.map((card) => (
            <div key={card.label} className={styles.summaryCard}>
              <span className={styles.summaryLabel}>{card.label}</span>
              <strong className={styles.summaryValue}>{card.value}</strong>
              <p className={styles.summaryTip}>{card.tip}</p>
            </div>
          ))}
        </div>
      </section>

      <section className={styles.catalogSection}>
        <div className={styles.sectionHeader}>
          <div>
            <h3>{t('welcome.openSource.section.catalog.title')}</h3>
            <p>{t('welcome.openSource.section.catalog.description')}</p>
          </div>
        </div>

        <div className={styles.groupList}>
          {groupedProjects.map((group) => (
            <section key={group.id} className={styles.groupSection}>
              <div className={styles.groupHeader}>
                <div className={styles.groupTitleWrap}>
                  <div className={styles.groupIcon}>
                    <Icon icon={group.icon} size={18} className={styles.icon} />
                  </div>
                  <div>
                    <h4>{t(group.labelKey)}</h4>
                    <p>{t(group.descriptionKey)}</p>
                  </div>
                </div>
                <span className={styles.groupCount}>{t('welcome.openSource.common.itemCount', { count: group.projects.length })}</span>
              </div>

              <div className={styles.projectGrid}>
                {group.projects.map((project) => (
                  <article key={project.id} className={styles.projectCard}>
                    <div className={styles.projectHeader}>
                      <div>
                        <h5 className={styles.projectName}>{project.name}</h5>
                        <p className={styles.projectScope}>{t(project.scopeKey)}</p>
                      </div>
                      <div className={styles.badgeList}>
                        {project.isCore && (
                          <span className={`${styles.badge} ${styles.coreBadge}`}>{t('welcome.openSource.badge.core')}</span>
                        )}
                        <span className={`${styles.badge} ${styles.licenseBadge}`}>
                          {project.licenseKey ? t(project.licenseKey) : project.license}
                        </span>
                      </div>
                    </div>

                    <p className={styles.projectPurpose}>{t(project.purposeKey)}</p>

                    <div className={styles.usageSection}>
                      <span className={styles.usageLabel}>{t('welcome.openSource.usage.label')}</span>
                      <div className={styles.usageList}>
                        {project.usedInKeys.map((usageKey) => (
                          <span key={usageKey} className={styles.usageChip}>
                            {t(usageKey)}
                          </span>
                        ))}
                      </div>
                    </div>

                    {(project.noteKey || project.licenseNoteKey) && (
                      <div className={styles.noteBlock}>
                        {project.noteKey && <p>{t(project.noteKey)}</p>}
                        {project.licenseNoteKey && <p>{t(project.licenseNoteKey)}</p>}
                      </div>
                    )}

                    <div className={styles.projectLinks}>
                      <a
                        className={styles.projectLink}
                        href={project.repository}
                        target="_blank"
                        rel="noopener noreferrer"
                      >
                        <Icon icon="mdi:source-repository" size={16} />
                        {t('welcome.openSource.links.repository')}
                      </a>
                      {project.website && (
                        <a
                          className={styles.projectLink}
                          href={project.website}
                          target="_blank"
                          rel="noopener noreferrer"
                        >
                          <Icon icon="mdi:web" size={16} />
                          {t('welcome.openSource.links.website')}
                        </a>
                      )}
                    </div>
                  </article>
                ))}
              </div>
            </section>
          ))}
        </div>
      </section>

      <section className={styles.noticeCard}>
        <div className={styles.noticeHeader}>
          <div className={styles.noticeIcon}>
            <Icon icon="mdi:information-outline" size={16} className={styles.icon} />
          </div>
          <h3>{t('welcome.openSource.notice.title')}</h3>
        </div>
        <div className={styles.noticeList}>
          {openSourceOverview.boundaryNoteKeys.map((noteKey) => (
            <p key={noteKey}>{t(noteKey)}</p>
          ))}
        </div>
      </section>
    </div>
  );
};
