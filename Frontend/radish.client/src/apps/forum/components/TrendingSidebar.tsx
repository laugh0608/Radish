import type { PostItem, CommentNode, PostDetail } from '@/api/forum';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { FORUM_DETAIL_TOOL_EVENT, type ForumDetailToolAction } from '../constants/detailTools';
import { PostInfoCard } from './PostInfoCard';
import styles from './TrendingSidebar.module.css';

interface TrendingSidebarProps {
  hotPosts: PostItem[];
  godComments: CommentNode[];
  onPostClick: (postId: number) => void;
  onAuthorClick?: (userId: number, userName?: string | null, avatarUrl?: string | null) => void;
  loading?: boolean;
  selectedPost?: PostDetail | null;
  displayTimeZone: string;
}

export const TrendingSidebar = ({
  hotPosts,
  godComments,
  onPostClick,
  onAuthorClick,
  loading = false,
  selectedPost = null,
  displayTimeZone
}: TrendingSidebarProps) => {
  const emitDetailToolAction = (action: ForumDetailToolAction) => {
    window.dispatchEvent(new CustomEvent<ForumDetailToolAction>(FORUM_DETAIL_TOOL_EVENT, { detail: action }));
  };

  if (selectedPost) {
    return (
      <aside className={styles.sidebar}>
        <PostInfoCard
          post={selectedPost}
          displayTimeZone={displayTimeZone}
          onAuthorClick={onAuthorClick}
        />

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
                    <button
                      type="button"
                      className={styles.authorButton}
                      onClick={(event) => {
                        event.stopPropagation();
                        onAuthorClick?.(post.voAuthorId, post.voAuthorName, post.voAuthorAvatarUrl);
                      }}
                      title={`查看 ${post.voAuthorName?.trim() || `用户 ${post.voAuthorId}`} 的主页`}
                    >
                      {post.voAuthorName?.trim() || `用户 ${post.voAuthorId}`}
                    </button>
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
                  <button
                    type="button"
                    className={`${styles.authorButton} ${styles.godCommentAuthor}`}
                    onClick={(event) => {
                      event.stopPropagation();
                      onAuthorClick?.(comment.voAuthorId, comment.voAuthorName);
                    }}
                    title={`查看 ${comment.voAuthorName} 的主页`}
                  >
                    {comment.voAuthorName}
                  </button>
                  <span className={styles.godCommentLikes}>
                    <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor">
                      <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
                    </svg>
                    {comment.voLikeCount}
                  </span>
                </div>
                <p className={styles.godCommentContent}>{comment.voContent}</p>
                <span className={styles.godCommentTime}>
                  {formatDateTimeByTimeZone(comment.voCreateTime, displayTimeZone, '未知时间')}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </aside>
  );
};
