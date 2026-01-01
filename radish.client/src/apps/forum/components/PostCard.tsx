import type { PostItem } from '@/types/forum';
import styles from './PostCard.module.css';

interface PostCardProps {
  post: PostItem;
  onClick: () => void;
  godComment?: {
    content: string;
    authorName: string;
    likeCount: number;
  } | null;
}

export const PostCard = ({ post, onClick, godComment }: PostCardProps) => {
  // 简单的时间格式化函数（待安装 date-fns 后替换）
  const formatTime = (dateString: string) => {
    try {
      const date = new Date(dateString);
      const now = new Date();
      const diffMs = now.getTime() - date.getTime();
      const diffMins = Math.floor(diffMs / 60000);
      const diffHours = Math.floor(diffMs / 3600000);
      const diffDays = Math.floor(diffMs / 86400000);

      if (diffMins < 1) return '刚刚';
      if (diffMins < 60) return `${diffMins}分钟前`;
      if (diffHours < 24) return `${diffHours}小时前`;
      if (diffDays < 30) return `${diffDays}天前`;
      return date.toLocaleDateString('zh-CN');
    } catch {
      return dateString;
    }
  };

  return (
    <article className={styles.card} onClick={onClick}>
      {/* 帖子标题 */}
      <h3 className={styles.title}>{post.title}</h3>

      {/* 帖子元信息 */}
      <div className={styles.meta}>
        <span className={styles.author}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            <circle cx="12" cy="7" r="4" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
          {post.authorName}
        </span>
        <span className={styles.time}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <circle cx="12" cy="12" r="10" strokeWidth="2"/>
            <path d="M12 6v6l4 2" strokeWidth="2" strokeLinecap="round"/>
          </svg>
          {post.createTime ? formatTime(post.createTime) : '未知时间'}
        </span>
        <span className={styles.likes}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
          {post.likeCount || 0}
        </span>
        <span className={styles.views}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" strokeWidth="2"/>
            <circle cx="12" cy="12" r="3" strokeWidth="2"/>
          </svg>
          {post.browseCount || 0}
        </span>
        <span className={styles.comments}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
          {post.commentCount || 0}
        </span>
      </div>

      {/* 神评预览 */}
      {godComment && (
        <div className={styles.godComment}>
          <div className={styles.godCommentHeader}>
            <span className={styles.godBadge}>神评</span>
            <span className={styles.godAuthor}>{godComment.authorName}</span>
            <span className={styles.godLikes}>
              <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor">
                <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
              </svg>
              {godComment.likeCount}
            </span>
          </div>
          <p className={styles.godCommentContent}>{godComment.content}</p>
        </div>
      )}
    </article>
  );
};
