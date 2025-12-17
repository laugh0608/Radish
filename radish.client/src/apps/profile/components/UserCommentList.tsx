import { useState, useEffect } from 'react';
import { Icon } from '@radish/ui';
import styles from './UserCommentList.module.css';

interface Comment {
  id: number;
  content: string;
  postId: number;
  likeCount: number;
  createTime: string;
}

interface UserCommentListProps {
  userId: number;
  apiBaseUrl: string;
}

export const UserCommentList = ({ userId, apiBaseUrl }: UserCommentListProps) => {
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
      console.error('加载评论失败:', error);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return <div className={styles.loading}>加载中...</div>;
  }

  if (comments.length === 0) {
    return <div className={styles.empty}>还没有发表过评论</div>;
  }

  return (
    <div className={styles.container}>
      <div className={styles.list}>
        {comments.map(comment => (
          <div key={comment.id} className={styles.commentItem}>
            <p className={styles.content}>{comment.content}</p>
            <div className={styles.meta}>
              <span className={styles.metaItem}>
                <Icon icon="mdi:heart" size={16} />
                {comment.likeCount}
              </span>
              <span className={styles.time}>
                {new Date(comment.createTime).toLocaleDateString('zh-CN')}
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
            上一页
          </button>
          <span className={styles.pageInfo}>
            {page} / {totalPages}
          </span>
          <button
            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
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
