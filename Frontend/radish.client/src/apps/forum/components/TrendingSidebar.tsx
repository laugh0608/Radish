import type { PostItem, CommentNode, PostDetail } from '@/api/forum';
import { FORUM_DETAIL_TOOL_EVENT, type ForumDetailToolAction } from '../constants/detailTools';
import { PostInfoCard } from './PostInfoCard';
import styles from './TrendingSidebar.module.css';

interface TrendingSidebarProps {
  hotPosts: PostItem[];
  godComments: CommentNode[];
  onPostClick: (postId: number) => void;
  loading?: boolean;
  selectedPost?: PostDetail | null;
}

export const TrendingSidebar = ({
  hotPosts,
  godComments,
  onPostClick,
  loading = false,
  selectedPost = null
}: TrendingSidebarProps) => {
  const emitDetailToolAction = (action: ForumDetailToolAction) => {
    window.dispatchEvent(new CustomEvent<ForumDetailToolAction>(FORUM_DETAIL_TOOL_EVENT, { detail: action }));
  };

  // 简单的时间格式化函数（待安装 date-fns 后替换）
  const formatTime = (dateString?: string) => {
    if (!dateString) return '未知时间';
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

  if (selectedPost) {
    return (
      <aside className={styles.sidebar}>
        <PostInfoCard post={selectedPost} />

        <section className={styles.detailToolsSection}>
          <h3 className={styles.sectionTitle}>快捷操作</h3>
          <div className={styles.detailTools}>
            <button
              className={styles.detailToolButton}
              onClick={() => emitDetailToolAction('scrollTop')}
              title="滚动到顶部"
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                <path d="M18 15l-6-6-6 6" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              回到顶部
            </button>
            <button
              className={styles.detailToolButton}
              onClick={() => emitDetailToolAction('scrollBottom')}
              title="滚动到底部"
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                <path d="M6 9l6 6 6-6" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              到达底部
            </button>
            <button
              className={styles.detailToolButton}
              onClick={() => emitDetailToolAction('openComment')}
              title="快速评论"
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              快速评论
            </button>
          </div>
        </section>
      </aside>
    );
  }

  if (loading) {
    return (
      <aside className={styles.sidebar}>
        <div className={styles.loadingText}>加载中...</div>
      </aside>
    );
  }

  return (
    <aside className={styles.sidebar}>
      {/* 热门帖子 */}
      <section className={styles.section}>
        <h3 className={styles.sectionTitle}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
          热门帖子
        </h3>
        {hotPosts.length === 0 ? (
          <p className={styles.emptyText}>暂无热门帖子</p>
        ) : (
          <ul className={styles.list}>
            {hotPosts.map((post, index) => (
              <li
                key={post.voId}
                className={styles.hotPostItem}
                onClick={() => onPostClick(post.voId)}
              >
                <div className={styles.hotPostRank}>
                  <span className={index < 3 ? styles.topRank : styles.normalRank}>
                    {index + 1}
                  </span>
                </div>
                <div className={styles.hotPostContent}>
                  <h4 className={styles.hotPostTitle}>{post.voTitle}</h4>
                  <div className={styles.hotPostMeta}>
                    <span className={styles.hotPostLikes}>
                      <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
                      </svg>
                      {post.voLikeCount}
                    </span>
                    <span className={styles.hotPostViews}>
                      <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" strokeWidth="2"/>
                        <circle cx="12" cy="12" r="3" strokeWidth="2"/>
                      </svg>
                      {post.voViewCount}
                    </span>
                  </div>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* 神评榜 */}
      <section className={styles.section}>
        <h3 className={styles.sectionTitle}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/>
          </svg>
          神评榜
        </h3>
        {godComments.length === 0 ? (
          <p className={styles.emptyText}>暂无神评</p>
        ) : (
          <ul className={styles.list}>
            {godComments.map((comment) => (
              <li
                key={comment.voId}
                className={styles.godCommentItem}
                onClick={() => comment.voPostId && onPostClick(comment.voPostId)}
              >
                <div className={styles.godCommentHeader}>
                  <span className={styles.godCommentAuthor}>{comment.voAuthorName}</span>
                  <span className={styles.godCommentLikes}>
                    <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor">
                      <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
                    </svg>
                    {comment.voLikeCount}
                  </span>
                </div>
                <p className={styles.godCommentContent}>{comment.voContent}</p>
                <span className={styles.godCommentTime}>{formatTime(comment.voCreateTime)}</span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </aside>
  );
};
