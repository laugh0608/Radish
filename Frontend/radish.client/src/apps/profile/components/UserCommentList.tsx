import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { log } from '@/utils/logger';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { Icon } from '@radish/ui/icon';
import { getUserComments, type LongId, type UserComment } from '@/api/user';
import styles from './UserCommentList.module.css';

interface UserCommentListProps {
  userId: LongId;
  apiBaseUrl?: string;
  displayTimeZone: string;
  onCommentClick?: (postId: LongId, commentId: LongId, postPublicId?: string | null) => void;
  page?: number;
  onPageChange?: (page: number) => void;
}

export const UserCommentList = ({
  userId,
  displayTimeZone,
  onCommentClick,
  page: controlledPage,
  onPageChange
}: UserCommentListProps) => {
  const { t } = useTranslation();
  const [comments, setComments] = useState<UserComment[]>([]);
  const [loading, setLoading] = useState(true);
  const [internalPage, setInternalPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const page = controlledPage ?? internalPage;

  useEffect(() => {
    loadComments();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId, page]);

  const loadComments = async () => {
    setLoading(true);
    try {
      const result = await getUserComments(userId, page, 10);
      setComments(result.data || []);
      setTotalPages(result.pageCount || 1);
    } catch (error) {
      log.error('加载评论失败:', error);
    } finally {
      setLoading(false);
    }
  };

  const updatePage = (nextPage: number) => {
    if (onPageChange) {
      onPageChange(nextPage);
      return;
    }

    setInternalPage(nextPage);
  };

  if (loading) {
    return <div className={styles.loading}>{t('common.loading')}</div>;
  }

  if (comments.length === 0) {
    return <div className={styles.empty}>{t('profile.comments.empty')}</div>;
  }

  return (
    <div className={styles.container}>
      <div className={styles.list}>
        {comments.map(comment => (
          <div
            key={comment.voId}
            className={styles.commentItem}
            onClick={() => onCommentClick?.(comment.voPostId, comment.voId, comment.voPostPublicId)}
            style={{ cursor: onCommentClick ? 'pointer' : 'default' }}
          >
            <p className={styles.content}>{comment.voContent}</p>
            <div className={styles.meta}>
              <span className={styles.metaItem}>
                <Icon icon="mdi:heart" size={16} />
                {comment.voLikeCount}
              </span>
              <span className={styles.time}>
                {formatDateTimeByTimeZone(comment.voCreateTime, displayTimeZone)}
              </span>
            </div>
          </div>
        ))}
      </div>

      {totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            onClick={() => updatePage(Math.max(1, page - 1))}
            disabled={page === 1}
            className={styles.pageButton}
          >
            {t('common.previousPage')}
          </button>
          <span className={styles.pageInfo}>
            {t('common.pageInfo', { current: page, total: totalPages })}
          </span>
          <button
            onClick={() => updatePage(Math.min(totalPages, page + 1))}
            disabled={page === totalPages}
            className={styles.pageButton}
          >
            {t('common.nextPage')}
          </button>
        </div>
      )}
    </div>
  );
};
