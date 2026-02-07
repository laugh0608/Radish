import type { Category, PostItem, CommentHighlight } from '@/api/forum';
import { PostCard } from '../components/PostCard';
import styles from './PostListView.module.css';

interface PostListViewProps {
  // 数据
  categories: Category[];
  selectedCategoryId: number | null;
  selectedTagName?: string | null;
  posts: PostItem[];
  postGodComments: Map<number, CommentHighlight>;
  currentPage: number;
  totalPages: number;
  sortBy: 'newest' | 'hottest' | 'essence';
  searchKeyword: string;
  loadingPosts: boolean;

  // 事件处理
  onSortChange: (sortBy: 'newest' | 'hottest' | 'essence') => void;
  onSearchChange: (keyword: string) => void;
  onPageChange: (page: number) => void;
  onPostClick: (postId: number) => void;
}

export const PostListView = ({
  categories,
  selectedCategoryId,
  selectedTagName,
  posts,
  postGodComments,
  currentPage,
  totalPages,
  sortBy,
  searchKeyword,
  loadingPosts,
  onSortChange,
  onSearchChange,
  onPageChange,
  onPostClick
}: PostListViewProps) => {
  return (
    <>
      {/* 工具栏 */}
      <div className={styles.toolbar}>
        <div className={styles.toolbarLeft}>
          <h2 className={styles.toolbarTitle}>
            {selectedTagName
              ? `#${selectedTagName}`
              : categories.find(c => c.voId === selectedCategoryId)?.voName || '全部帖子'}
          </h2>
          <div className={styles.sortButtons}>
            <button
              className={`${styles.sortButton} ${sortBy === 'newest' ? styles.sortActive : ''}`}
              onClick={() => onSortChange('newest')}
            >
              最新
            </button>
            <button
              className={`${styles.sortButton} ${sortBy === 'hottest' ? styles.sortActive : ''}`}
              onClick={() => onSortChange('hottest')}
            >
              最热
            </button>
            <button
              className={`${styles.sortButton} ${sortBy === 'essence' ? styles.sortActive : ''}`}
              onClick={() => onSortChange('essence')}
            >
              精华
            </button>
          </div>
        </div>

        <div className={styles.searchBox}>
          <input
            type="text"
            className={styles.searchInput}
            placeholder="搜索帖子..."
            value={searchKeyword}
            onChange={(e) => onSearchChange(e.target.value)}
          />
          {searchKeyword && (
            <button
              className={styles.clearButton}
              onClick={() => onSearchChange('')}
              aria-label="清除搜索"
            >
              ×
            </button>
          )}
        </div>
      </div>

      {/* 帖子瀑布流 */}
      <div className={styles.postsFeed}>
        {loadingPosts ? (
          <p className={styles.loadingText}>加载中...</p>
        ) : posts.length === 0 ? (
          <p className={styles.emptyText}>暂无帖子</p>
        ) : (
          posts.map((post) => {
            const godComment = postGodComments.get(post.voId);
            return (
              <PostCard
                key={post.voId}
                post={post}
                onClick={() => onPostClick(post.voId)}
                godComment={
                  godComment
                    ? {
                        content: godComment.voContentSnapshot || '',
                        authorName: godComment.voAuthorName,
                        likeCount: godComment.voLikeCount
                      }
                    : null
                }
              />
            );
          })
        )}
      </div>

      {/* 分页 */}
      {totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            className={styles.paginationButton}
            onClick={() => onPageChange(currentPage - 1)}
            disabled={currentPage === 1}
          >
            ‹
          </button>
          {Array.from({ length: Math.min(totalPages, 7) }, (_, i) => {
            let pageNum: number;
            if (totalPages <= 7) {
              pageNum = i + 1;
            } else if (currentPage <= 4) {
              pageNum = i + 1;
            } else if (currentPage >= totalPages - 3) {
              pageNum = totalPages - 6 + i;
            } else {
              pageNum = currentPage - 3 + i;
            }
            return (
              <button
                key={pageNum}
                className={`${styles.paginationButton} ${
                  currentPage === pageNum ? styles.paginationActive : ''
                }`}
                onClick={() => onPageChange(pageNum)}
              >
                {pageNum}
              </button>
            );
          })}
          <button
            className={styles.paginationButton}
            onClick={() => onPageChange(currentPage + 1)}
            disabled={currentPage === totalPages}
          >
            ›
          </button>
        </div>
      )}
    </>
  );
};
