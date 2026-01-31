import { useState, useEffect } from 'react';
import { log } from '@/utils/logger';
import { Icon } from '@radish/ui';
import styles from './UserPostList.module.css';

interface Post {
  voId: number;
  voTitle: string;
  voContent: string;
  voViewCount: number;
  voLikeCount: number;
  voCommentCount: number;
  voCreateTime: string;
}

interface UserPostListProps {
  userId: number;
  apiBaseUrl: string;
}

export const UserPostList = ({ userId, apiBaseUrl }: UserPostListProps) => {
  const [posts, setPosts] = useState<Post[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  useEffect(() => {
    loadPosts();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId, page]);

  const loadPosts = async () => {
    setLoading(true);
    try {
      const response = await fetch(
        `${apiBaseUrl}/api/v1/Post/GetUserPosts?userId=${userId}&pageIndex=${page}&pageSize=10`
      );
      const json = await response.json();
      if (json.isSuccess && json.responseData) {
        setPosts(json.responseData.data || []);
        setTotalPages(json.responseData.pageCount || 1);
      }
    } catch (error) {
      log.error('加载帖子失败:', error);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return <div className={styles.loading}>加载中...</div>;
  }

  if (posts.length === 0) {
    return <div className={styles.empty}>还没有发表过帖子</div>;
  }

  return (
    <div className={styles.container}>
      <div className={styles.list}>
        {posts.map(post => (
          <div key={post.voId} className={styles.postItem}>
            <h3 className={styles.title}>{post.voTitle}</h3>
            <p className={styles.content}>
              {post.voContent?.substring(0, 100) ?? ''}
              {(post.voContent?.length ?? 0) > 100 && '...'}
            </p>
            <div className={styles.meta}>
              <span className={styles.metaItem}>
                <Icon icon="mdi:eye" size={16} />
                {post.voViewCount}
              </span>
              <span className={styles.metaItem}>
                <Icon icon="mdi:heart" size={16} />
                {post.voLikeCount}
              </span>
              <span className={styles.metaItem}>
                <Icon icon="mdi:comment" size={16} />
                {post.voCommentCount}
              </span>
              <span className={styles.time}>
                {new Date(post.voCreateTime).toLocaleDateString('zh-CN')}
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
