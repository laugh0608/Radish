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
  const parsedTags = post?.voTags
    ? post.voTags
        .split(',')
        .map(tag => tag.trim())
        .filter(Boolean)
    : [];
  const tagList = post?.voTagNames && post.voTagNames.length > 0 ? post.voTagNames : parsedTags;

  // 判断是否是作者本人
  const isAuthor = post && currentUserId > 0 && post.voAuthorId === currentUserId;
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
        <h4 className={styles.postTitle}>{post.voTitle}</h4>
        <div className={styles.postMeta}>
          {post.voAuthorName && <span>作者：{post.voAuthorName}</span>}
          {post.voCreateTime && <span> · {post.voCreateTime}</span>}
          {post.voViewCount !== undefined && <span> · 浏览 {post.voViewCount}</span>}
        </div>
        <MarkdownRenderer content={post.voContent} className={styles.postBody} />
        {tagList.length > 0 && (
          <div className={styles.postTags}>
            {tagList.map((tag, index) => (
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
            onClick={() => onLike?.(post.voId)}
            className={`${styles.likeButton} ${isLiked ? styles.liked : ''}`}
            disabled={!isAuthenticated}
            title={!isAuthenticated ? '请先登录' : isLiked ? '取消点赞' : '点赞'}
          >
            <span className={styles.likeIcon}>{isLiked ? '❤️' : '🤍'}</span>
            <span className={styles.likeCount}>{post.voLikeCount || 0}</span>
          </button>
          <span className={styles.commentCount}>
            💬 {post.voCommentCount || 0} 条评论
          </span>

          {/* 编辑和删除按钮（仅作者可见） */}
          {isAuthor && (
            <div className={styles.authorActions}>
              <button
                type="button"
                onClick={() => onEdit?.(post.voId)}
                className={styles.editButton}
                title="编辑帖子"
              >
                <Icon icon="mdi:pencil" size={18} />
                编辑
              </button>
              <button
                type="button"
                onClick={() => onDelete?.(post.voId)}
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
