import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { log } from '@/utils/logger';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { Icon } from '@radish/ui/icon';
import styles from './UserCommentList.module.css';

interface Comment {
  voId: number;
  voContent: string;
  voPostId: number;
  voLikeCount: number;
  voCreateTime: string;
}

interface UserCommentListProps {
  userId: number;
  apiBaseUrl: string;
  displayTimeZone: string;
  onCommentClick?: (postId: number, commentId: number) => void;
}

export const UserCommentList = ({ userId, apiBaseUrl, displayTimeZone, onCommentClick }: UserCommentListProps) => {
  const { t } = useTranslation();
  const [comments, setComments] = useState<Comment[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  useEffect(() => {
    loadComments();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId, page]);

  const loadComments = async () => {
    setLoading(true);
    try {
      const response = await fetch(
        `${apiBaseUrl}/api/v1/Comment/GetUserComments?userId=${userId}&pageIndex=${page}&pageSize=10`
      );
      const json = await response.json();
      if (json.isSuccess && json.responseData) {
        setComments(json.responseData.data || []);
        setTotalPages(json.responseData.pageCount || 1);
      }
    } catch (error) {
      log.error('加载评论失败:', error);
    } finally {
      setLoading(false);
    }
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
            onClick={() => onCommentClick?.(comment.voPostId, comment.voId)}
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
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page === 1}
            className={styles.pageButton}
          >
            {t('common.previousPage')}
          </button>
          <span className={styles.pageInfo}>
            {t('common.pageInfo', { current: page, total: totalPages })}
          </span>
          <button
            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
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
