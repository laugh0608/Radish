import { useEffect, useState } from 'react';
import { Icon } from '@radish/ui/icon';
import { getApiBaseUrl } from '@/config/env';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { log } from '@/utils/logger';
import { getMyBrowseHistory, type UserBrowseHistoryItem } from '@/api/user';
import styles from './UserBrowseHistoryList.module.css';

interface UserBrowseHistoryListProps {
  displayTimeZone: string;
  onItemClick?: (item: UserBrowseHistoryItem) => void;
}

const apiBaseUrl = getApiBaseUrl();

const buildCoverUrl = (coverImage?: string | null): string | null => {
  if (!coverImage) {
    return null;
  }

  if (/^https?:\/\//i.test(coverImage)) {
    return coverImage;
  }

  if (coverImage.startsWith('/')) {
    return `${apiBaseUrl}${coverImage}`;
  }

  return `${apiBaseUrl}/${coverImage}`;
};

const getTypeIcon = (targetType: string): string => {
  switch (targetType) {
    case 'Post':
      return 'mdi:forum-outline';
    case 'Product':
      return 'mdi:shopping-outline';
    case 'Wiki':
      return 'mdi:file-document-outline';
    default:
      return 'mdi:history';
  }
};

export const UserBrowseHistoryList = ({
  displayTimeZone,
  onItemClick
}: UserBrowseHistoryListProps) => {
  const [items, setItems] = useState<UserBrowseHistoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  useEffect(() => {
    const loadBrowseHistory = async () => {
      setLoading(true);
      try {
        const result = await getMyBrowseHistory(page, 10);
        setItems(result.voItems || []);
        setTotalPages(Math.max(1, Math.ceil((result.voTotal || 0) / Math.max(result.voPageSize || 10, 1))));
      } catch (error) {
        log.error('UserBrowseHistoryList', '加载浏览记录失败：', error);
        setItems([]);
        setTotalPages(1);
      } finally {
        setLoading(false);
      }
    };

    void loadBrowseHistory();
  }, [page]);

  if (loading) {
    return <div className={styles.loading}>加载中...</div>;
  }

  if (items.length === 0) {
    return <div className={styles.empty}>最近还没有浏览记录</div>;
  }

  return (
    <div className={styles.container}>
      <div className={styles.list}>
        {items.map((item) => {
          const coverUrl = buildCoverUrl(item.voCoverImage);
          return (
            <article
              key={String(item.voId)}
              className={styles.card}
              onClick={() => onItemClick?.(item)}
            >
              <div className={styles.cover}>
                {coverUrl ? (
                  <img src={coverUrl} alt={item.voTitle} className={styles.coverImage} loading="lazy" />
                ) : (
                  <span className={styles.coverFallback}>
                    <Icon icon={getTypeIcon(item.voTargetType)} size={24} />
                  </span>
                )}
              </div>

              <div className={styles.content}>
                <div className={styles.header}>
                  <span className={styles.typeBadge}>
                    <Icon icon={getTypeIcon(item.voTargetType)} size={14} />
                    {item.voTargetTypeDisplay}
                  </span>
                  <span className={styles.time}>
                    {formatDateTimeByTimeZone(item.voLastViewTime, displayTimeZone)}
                  </span>
                </div>

                <h3 className={styles.title}>{item.voTitle}</h3>
                <p className={styles.summary}>
                  {item.voSummary?.trim() || '暂无摘要'}
                </p>

                <div className={styles.footer}>
                  <span className={styles.metaItem}>浏览 {item.voViewCount} 次</span>
                  <span className={styles.routeText}>{item.voRoutePath || '站内详情页'}</span>
                </div>
              </div>
            </article>
          );
        })}
      </div>

      {totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            type="button"
            onClick={() => setPage((current) => Math.max(1, current - 1))}
            disabled={page === 1}
            className={styles.pageButton}
          >
            上一页
          </button>
          <span className={styles.pageInfo}>
            {page} / {totalPages}
          </span>
          <button
            type="button"
            onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
            disabled={page === totalPages}
            className={styles.pageButton}
          >
            下一页
          </button>
        </div>
      )}
    </div>
  );
};
