import { Icon } from '@radish/ui/icon';
import { openSourceOverview, openSourceProjects } from '../data/openSourceCatalog';
import styles from './OpenSourceTab.module.css';

/**
 * 开源软件声明标签页
 */
export const OpenSourceTab = () => {
  return (
    <div className={styles.container}>
      <section className={styles.heroCard}>
        <div className={styles.heroHeader}>
          <Icon icon="mdi:open-source-initiative" size={30} color="#4f46e5" />
          <div>
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
            <span className={styles.metaLabel}>后续演进</span>
            <p className={styles.metaValue}>{openSourceOverview.nextStep}</p>
          </div>
        </div>
      </section>

      <section>
        <div className={styles.sectionHeader}>
          <Icon icon="mdi:format-list-bulleted-square" size={24} color="#4f46e5" />
          <h3>核心项目清单</h3>
        </div>
        <p className={styles.sectionHint}>
          当前先展示 Radish 主链路中最核心的开源项目，完整许可证文本与全量依赖清单后续可再扩展。
        </p>

        <div className={styles.projectList}>
          {openSourceProjects.map((project) => (
            <article key={project.name} className={styles.projectCard}>
              <div className={styles.projectHeader}>
                <h4 className={styles.projectName}>{project.name}</h4>
                <span className={styles.licenseBadge}>{project.license}</span>
              </div>

              <p className={styles.projectPurpose}>{project.purpose}</p>

              <div className={styles.projectFooter}>
                <p className={styles.projectNote}>{project.note ?? '许可证与项目详情请以官方仓库和发布说明为准。'}</p>
                <a
                  className={styles.projectLink}
                  href={project.website}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  <Icon icon="mdi:open-in-new" size={16} />
                  官方仓库
                </a>
              </div>
            </article>
          ))}
        </div>
      </section>

      <div className={styles.notice}>
        <Icon icon="mdi:information-outline" size={20} color="#4f46e5" />
        <p>
          本页用于欢迎 App 内的开源说明入口，不替代未来发行版的完整许可证公告或第三方通知文件。
        </p>
      </div>
    </div>
  );
};
