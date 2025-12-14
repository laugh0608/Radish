import type { PostItem } from '@/types/forum';
import styles from './PostList.module.css';

interface PostListProps {
  posts: PostItem[];
  selectedPostId: number | null;
  onSelectPost: (postId: number) => void;
  loading?: boolean;
}

export const PostList = ({
  posts,
  selectedPostId,
  onSelectPost,
  loading = false
}: PostListProps) => {
  return (
    <div className={styles.container}>
      <h3 className={styles.title}>帖子</h3>
      {loading && <p className={styles.loadingText}>加载帖子中...</p>}
      {!loading && posts.length === 0 && <p className={styles.emptyText}>该分类下暂无帖子</p>}
      <ul className={styles.list}>
        {posts.map(post => (
          <li key={post.id}>
            <button
              type="button"
              onClick={() => onSelectPost(post.id)}
              className={`${styles.postButton} ${
                selectedPostId === post.id ? styles.active : ''
              }`}
            >
              <div className={styles.postTitle}>{post.title}</div>
              {post.authorName && (
                <div className={styles.postMeta}>
                  作者：{post.authorName}
                  {post.viewCount !== undefined && <span> · 浏览 {post.viewCount}</span>}
                </div>
              )}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
};
