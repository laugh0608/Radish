import type { PostDetail as PostDetailType } from '@/api/forum';
import { MarkdownRenderer, Icon } from '@radish/ui';
import styles from './PostDetail.module.css';

interface PostDetailProps {
  post: PostDetailType | null;
  loading?: boolean;
  isLiked?: boolean;
  onLike?: (postId: number) => void;
  isAuthenticated?: boolean;
  currentUserId?: number;
  onEdit?: (postId: number) => void;
  onDelete?: (postId: number) => void;
}

export const PostDetail = ({
  post,
  loading = false,
  isLiked = false,
  onLike,
  isAuthenticated = false,
  currentUserId = 0,
  onEdit,
  onDelete
}: PostDetailProps) => {
  // 判断是否是作者本人
  const isAuthor = post && currentUserId > 0 && post.authorId === currentUserId;
  if (loading) {
    return (
      <div className={styles.container}>
        <h3 className={styles.title}>帖子详情</h3>
        <p className={styles.loadingText}>加载帖子详情中...</p>
      </div>
    );
  }

  if (!post) {
    return (
      <div className={styles.container}>
        <h3 className={styles.title}>帖子详情</h3>
        <p className={styles.emptyText}>请选择一个帖子查看详情</p>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <h3 className={styles.title}>帖子详情</h3>
      <div className={styles.postContent}>
        <h4 className={styles.postTitle}>{post.title}</h4>
        <div className={styles.postMeta}>
          {post.authorName && <span>作者：{post.authorName}</span>}
          {post.createTime && <span> · {post.createTime}</span>}
          {post.viewCount !== undefined && <span> · 浏览 {post.viewCount}</span>}
        </div>
        <MarkdownRenderer content={post.content} className={styles.postBody} />
        {post.tagNames && post.tagNames.length > 0 && (
          <div className={styles.postTags}>
            {post.tagNames.map((tag, index) => (
              <span key={index} className={styles.tag}>
                {tag}
              </span>
            ))}
          </div>
        )}

        {/* 操作按钮 */}
        <div className={styles.actions}>
          <button
            type="button"
            onClick={() => onLike?.(post.id)}
            className={`${styles.likeButton} ${isLiked ? styles.liked : ''}`}
            disabled={!isAuthenticated}
            title={!isAuthenticated ? '请先登录' : isLiked ? '取消点赞' : '点赞'}
          >
            <span className={styles.likeIcon}>{isLiked ? '❤️' : '🤍'}</span>
            <span className={styles.likeCount}>{post.likeCount || 0}</span>
          </button>
          <span className={styles.commentCount}>
            💬 {post.commentCount || 0} 条评论
          </span>

          {/* 编辑和删除按钮（仅作者可见） */}
          {isAuthor && (
            <div className={styles.authorActions}>
              <button
                type="button"
                onClick={() => onEdit?.(post.id)}
                className={styles.editButton}
                title="编辑帖子"
              >
                <Icon icon="mdi:pencil" size={18} />
                编辑
              </button>
              <button
                type="button"
                onClick={() => onDelete?.(post.id)}
                className={styles.deleteButton}
                title="删除帖子"
              >
                <Icon icon="mdi:delete" size={18} />
                删除
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
