import type { PostDetail as PostDetailType } from '@/types/forum';
import { MarkdownRenderer } from '@/shared/ui/MarkdownRenderer';
import styles from './PostDetail.module.css';

interface PostDetailProps {
  post: PostDetailType | null;
  loading?: boolean;
}

export const PostDetail = ({ post, loading = false }: PostDetailProps) => {
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
      </div>
    </div>
  );
};
