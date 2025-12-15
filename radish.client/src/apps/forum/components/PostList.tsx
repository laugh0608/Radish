import { useState, useEffect } from 'react';
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
  sortBy: 'newest' | 'hottest' | 'essence';
  onSortChange: (sortBy: 'newest' | 'hottest' | 'essence') => void;
  searchKeyword: string;
  onSearchChange: (keyword: string) => void;
}

export const PostList = ({
  posts,
  selectedPostId,
  onSelectPost,
  loading = false,
  currentPage,
  totalPages,
  onPageChange,
  sortBy,
  onSortChange,
  searchKeyword,
  onSearchChange
}: PostListProps) => {
  // 本地搜索输入状态（用于即时显示用户输入）
  const [localSearch, setLocalSearch] = useState(searchKeyword);

  // 使用 useEffect 实现 debounce
  useEffect(() => {
    const timer = setTimeout(() => {
      onSearchChange(localSearch);
    }, 500); // 500ms 延迟

    return () => clearTimeout(timer);
  }, [localSearch, onSearchChange]);
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
      <div className={styles.header}>
        <h3 className={styles.title}>帖子</h3>
        <div className={styles.sortButtons}>
          <button
            type="button"
            onClick={() => onSortChange('newest')}
            className={`${styles.sortButton} ${sortBy === 'newest' ? styles.sortActive : ''}`}
          >
            最新
          </button>
          <button
            type="button"
            onClick={() => onSortChange('hottest')}
            className={`${styles.sortButton} ${sortBy === 'hottest' ? styles.sortActive : ''}`}
          >
            最热
          </button>
          <button
            type="button"
            onClick={() => onSortChange('essence')}
            className={`${styles.sortButton} ${sortBy === 'essence' ? styles.sortActive : ''}`}
          >
            精华
          </button>
        </div>
      </div>

      {/* 搜索框 */}
      <div className={styles.searchBox}>
        <input
          type="text"
          placeholder="搜索帖子标题或内容..."
          value={localSearch}
          onChange={(e) => setLocalSearch(e.target.value)}
          className={styles.searchInput}
        />
        {localSearch && (
          <button
            type="button"
            onClick={() => setLocalSearch('')}
            className={styles.clearButton}
            title="清除搜索"
          >
            ×
          </button>
        )}
      </div>

      {loading && <p className={styles.loadingText}>加载帖子中...</p>}
      {!loading && posts.length === 0 && (
        <p className={styles.emptyText}>
          {searchKeyword ? '未找到匹配的帖子' : '该分类下暂无帖子'}
        </p>
      )}
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
