import { Icon } from '@radish/ui/icon';
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

const summaryCards = [
  {
    label: '收录项目',
    value: `${openSourceProjects.length} 项`,
    tip: '首批关键依赖'
  },
  {
    label: '核心链路',
    value: `${openSourceProjects.filter((project) => project.isCore).length} 项`,
    tip: '优先展示主路径依赖'
  },
  {
    label: '展示分组',
    value: `${groupedProjects.length} 组`,
    tip: '后端、数据、前端、实时能力'
  },
  {
    label: '涉及模块',
    value: `${new Set(openSourceProjects.flatMap((project) => project.usedIn)).size} 类`,
    tip: '覆盖 API / Gateway / Auth / Client / Console 等场景'
  }
];

/**
 * 开源软件声明标签页
 */
export const OpenSourceTab = () => {
  return (
    <div className={styles.container}>
      <section className={styles.heroCard}>
        <div className={styles.heroHeader}>
          <div className={styles.heroIcon}>
            <Icon icon="mdi:open-source-initiative" size={24} className={styles.icon} />
          </div>
          <div className={styles.heroContent}>
            <h2>{openSourceOverview.title}</h2>
            <p>{openSourceOverview.summary}</p>
          </div>
        </div>

        <div className={styles.metaGrid}>
          <div className={styles.metaCard}>
            <span className={styles.metaLabel}>当前范围</span>
            <p className={styles.metaValue}>{openSourceOverview.scope}</p>
          </div>
          <div className={styles.metaCard}>
            <span className={styles.metaLabel}>清单来源</span>
            <p className={styles.metaValue}>{openSourceOverview.source}</p>
          </div>
          <div className={styles.metaCard}>
            <span className={styles.metaLabel}>维护方式</span>
            <p className={styles.metaValue}>{openSourceOverview.maintenance}</p>
          </div>
          <div className={styles.metaCard}>
            <span className={styles.metaLabel}>后续演进</span>
            <p className={styles.metaValue}>{openSourceOverview.nextStep}</p>
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
            <h3>首批开源项目清单</h3>
            <p>
              当前优先展示 Radish 主链路中最关键的一批依赖，并按职责分组说明用途、覆盖模块和许可证口径。
            </p>
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
                    <h4>{group.label}</h4>
                    <p>{group.description}</p>
                  </div>
                </div>
                <span className={styles.groupCount}>{group.projects.length} 项</span>
              </div>

              <div className={styles.projectGrid}>
                {group.projects.map((project) => (
                  <article key={project.id} className={styles.projectCard}>
                    <div className={styles.projectHeader}>
                      <div>
                        <h5 className={styles.projectName}>{project.name}</h5>
                        <p className={styles.projectScope}>{project.scope}</p>
                      </div>
                      <div className={styles.badgeList}>
                        {project.isCore && (
                          <span className={`${styles.badge} ${styles.coreBadge}`}>核心链路</span>
                        )}
                        <span className={`${styles.badge} ${styles.licenseBadge}`}>{project.license}</span>
                      </div>
                    </div>

                    <p className={styles.projectPurpose}>{project.purpose}</p>

                    <div className={styles.usageSection}>
                      <span className={styles.usageLabel}>项目内使用</span>
                      <div className={styles.usageList}>
                        {project.usedIn.map((usage) => (
                          <span key={usage} className={styles.usageChip}>
                            {usage}
                          </span>
                        ))}
                      </div>
                    </div>

                    {(project.note || project.licenseNote) && (
                      <div className={styles.noteBlock}>
                        {project.note && <p>{project.note}</p>}
                        {project.licenseNote && <p>{project.licenseNote}</p>}
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
                        官方仓库
                      </a>
                      {project.website && (
                        <a
                          className={styles.projectLink}
                          href={project.website}
                          target="_blank"
                          rel="noopener noreferrer"
                        >
                          <Icon icon="mdi:web" size={16} />
                          官方网站
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
          <h3>展示边界说明</h3>
        </div>
        <div className={styles.noticeList}>
          {openSourceOverview.boundaryNotes.map((note) => (
            <p key={note}>{note}</p>
          ))}
        </div>
      </section>
    </div>
  );
};
