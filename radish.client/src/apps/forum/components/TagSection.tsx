import type { Tag } from '@/api/forum';
import styles from './TagSection.module.css';

interface TagSectionProps {
  fixedTags: Tag[];
  hotTags: Tag[];
  selectedTagName: string | null;
  loading?: boolean;
  onSelectTag: (tagName: string | null) => void;
}

const renderTagLabel = (tagName: string, postCount?: number) => {
  if (postCount === undefined) {
    return tagName;
  }

  return `${tagName} (${postCount})`;
};

export const TagSection = ({
  fixedTags,
  hotTags,
  selectedTagName,
  loading = false,
  onSelectTag
}: TagSectionProps) => {
  return (
    <div className={styles.container}>
      <div className={styles.headerRow}>
        <h3 className={styles.title}>标签</h3>
        {selectedTagName && (
          <button
            type="button"
            className={styles.clearButton}
            onClick={() => onSelectTag(null)}
          >
            清除
          </button>
        )}
      </div>

      <div className={styles.group}>
        <p className={styles.groupTitle}>固定标签</p>
        <div className={styles.tagWrap}>
          {fixedTags.length === 0 ? (
            <span className={styles.emptyText}>暂无固定标签</span>
          ) : (
            fixedTags.map(tag => (
              <button
                key={tag.voId}
                type="button"
                onClick={() => onSelectTag(tag.voName)}
                className={`${styles.tagButton} ${selectedTagName === tag.voName ? styles.active : ''}`}
              >
                {tag.voName}
              </button>
            ))
          )}
        </div>
      </div>

      <div className={styles.group}>
        <p className={styles.groupTitle}>热门标签</p>
        {loading ? (
          <p className={styles.loadingText}>加载中...</p>
        ) : hotTags.length === 0 ? (
          <p className={styles.emptyText}>暂无热门标签</p>
        ) : (
          <div className={styles.tagWrap}>
            {hotTags.map(tag => (
              <button
                key={tag.voId}
                type="button"
                onClick={() => onSelectTag(tag.voName)}
                className={`${styles.tagButton} ${selectedTagName === tag.voName ? styles.active : ''}`}
              >
                {renderTagLabel(tag.voName, tag.voPostCount)}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
