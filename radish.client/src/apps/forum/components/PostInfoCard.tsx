import type { PostDetail } from '@/api/forum';
import styles from './PostInfoCard.module.css';

interface PostInfoCardProps {
  post: PostDetail;
}

export const PostInfoCard = ({ post }: PostInfoCardProps) => {
  // 简单的时间格式化函数
  const formatTime = (dateString?: string) => {
    if (!dateString) return '未知时间';
    try {
      const date = new Date(dateString);
      return date.toLocaleString('zh-CN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
      });
    } catch {
      return dateString;
    }
  };

  return (
    <div className={styles.card}>
      <h3 className={styles.title}>帖子信息</h3>

      <div className={styles.infoList}>
        {/* 发布者 */}
        <div className={styles.infoItem}>
          <span className={styles.label}>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor">
              <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
              <circle cx="12" cy="7" r="4" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            </svg>
            发布者
          </span>
          <span className={styles.value}>{post.authorName || '匿名'}</span>
        </div>

        {/* 发布时间 */}
        <div className={styles.infoItem}>
          <span className={styles.label}>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor">
              <circle cx="12" cy="12" r="10" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
              <path d="M12 6v6l4 2" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            </svg>
            发布时间
          </span>
          <span className={styles.value}>{formatTime(post.createTime)}</span>
        </div>

        {/* 评论数 */}
        <div className={styles.infoItem}>
          <span className={styles.label}>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor">
              <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            </svg>
            评论数
          </span>
          <span className={styles.value}>{post.commentCount || 0}</span>
        </div>

        {/* 浏览数 */}
        <div className={styles.infoItem}>
          <span className={styles.label}>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor">
              <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
              <circle cx="12" cy="12" r="3" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            </svg>
            浏览数
          </span>
          <span className={styles.value}>{post.viewCount || 0}</span>
        </div>

        {/* 点赞数 */}
        <div className={styles.infoItem}>
          <span className={styles.label}>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor">
              <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            </svg>
            点赞数
          </span>
          <span className={styles.value}>{post.likeCount || 0}</span>
        </div>
      </div>
    </div>
  );
};
