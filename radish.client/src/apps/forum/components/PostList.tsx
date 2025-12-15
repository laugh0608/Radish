import type { PostItem } from '@/types/forum';
import styles from './PostList.module.css';

interface PostListProps {
  posts: PostItem[];
  selectedPostId: number | null;
  onSelectPost: (postId: number) => void;
  loading?: boolean;
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

export const PostList = ({
  posts,
  selectedPostId,
  onSelectPost,
  loading = false,
  currentPage,
  totalPages,
  onPageChange
}: PostListProps) => {
  // 生成页码数组（简单分页，显示前后各2页）
  const getPageNumbers = () => {
    const pages: (number | string)[] = [];
    const maxPagesToShow = 5;

    if (totalPages <= maxPagesToShow) {
      // 总页数少于等于5，显示全部页码
      for (let i = 1; i <= totalPages; i++) {
        pages.push(i);
      }
    } else {
      // 总页数多于5，显示省略号
      if (currentPage <= 3) {
        // 当前页靠前
        for (let i = 1; i <= 4; i++) {
          pages.push(i);
        }
        pages.push('...');
        pages.push(totalPages);
      } else if (currentPage >= totalPages - 2) {
        // 当前页靠后
        pages.push(1);
        pages.push('...');
        for (let i = totalPages - 3; i <= totalPages; i++) {
          pages.push(i);
        }
      } else {
        // 当前页在中间
        pages.push(1);
        pages.push('...');
        for (let i = currentPage - 1; i <= currentPage + 1; i++) {
          pages.push(i);
        }
        pages.push('...');
        pages.push(totalPages);
      }
    }

    return pages;
  };

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

      {/* 分页控件 */}
      {!loading && totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            type="button"
            onClick={() => onPageChange(currentPage - 1)}
            disabled={currentPage === 1}
            className={styles.paginationButton}
          >
            上一页
          </button>

          {getPageNumbers().map((page, index) => {
            if (page === '...') {
              return (
                <span key={`ellipsis-${index}`} className={styles.paginationEllipsis}>
                  ...
                </span>
              );
            }

            return (
              <button
                key={page}
                type="button"
                onClick={() => onPageChange(page as number)}
                className={`${styles.paginationButton} ${
                  currentPage === page ? styles.paginationActive : ''
                }`}
              >
                {page}
              </button>
            );
          })}

          <button
            type="button"
            onClick={() => onPageChange(currentPage + 1)}
            disabled={currentPage === totalPages}
            className={styles.paginationButton}
          >
            下一页
          </button>
        </div>
      )}
    </div>
  );
};
