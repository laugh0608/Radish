import { useEffect, useRef, useState } from 'react';
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
  displayTimeZone: string;
  currentPage: number;
  totalPages: number;
  sortBy: 'newest' | 'hottest' | 'essence';
  loadingPosts: boolean;
  canPublish: boolean;

  // 事件处理
  onSortChange: (sortBy: 'newest' | 'hottest' | 'essence') => void;
  onOpenSearch: (keyword: string) => void;
  onPageChange: (page: number) => void;
  onPostClick: (postId: number) => void;
  onPublishClick: () => void;
}

export const PostListView = ({
  categories,
  selectedCategoryId,
  selectedTagName,
  posts,
  postGodComments,
  displayTimeZone,
  currentPage,
  totalPages,
  sortBy,
  loadingPosts,
  canPublish,
  onSortChange,
  onOpenSearch,
  onPageChange,
  onPostClick,
  onPublishClick
}: PostListViewProps) => {
  const [isSearchExpanded, setIsSearchExpanded] = useState(false);
  const [searchDraftKeyword, setSearchDraftKeyword] = useState('');
  const searchInputRef = useRef<HTMLInputElement>(null);
  const searchCapsuleRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (isSearchExpanded) {
      searchInputRef.current?.focus();
    }
  }, [isSearchExpanded]);

  useEffect(() => {
    if (!isSearchExpanded) {
      return;
    }

    const handleDocumentMouseDown = (event: MouseEvent) => {
      const target = event.target as Node;
      if (searchCapsuleRef.current && !searchCapsuleRef.current.contains(target)) {
        setIsSearchExpanded(false);
      }
    };

    document.addEventListener('mousedown', handleDocumentMouseDown);
    return () => {
      document.removeEventListener('mousedown', handleDocumentMouseDown);
    };
  }, [isSearchExpanded]);

  const submitSearch = () => {
    onOpenSearch(searchDraftKeyword.trim());
    setIsSearchExpanded(false);
  };

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
          </div>
        </div>

        <div className={styles.toolbarRight}>
          {isSearchExpanded ? (
            <div className={styles.searchCapsuleExpanded} ref={searchCapsuleRef}>
              <input
                ref={searchInputRef}
                type="text"
                className={styles.searchInput}
                placeholder="输入关键词后搜索"
                value={searchDraftKeyword}
                onChange={(event) => setSearchDraftKeyword(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    event.preventDefault();
                    submitSearch();
                  }
                  if (event.key === 'Escape') {
                    setIsSearchExpanded(false);
                  }
                }}
              />
              <button
                type="button"
                className={styles.searchActionButton}
                onClick={submitSearch}
                aria-label="执行搜索"
              >
                搜索
              </button>
              <button
                type="button"
                className={styles.searchCollapseButton}
                onClick={() => setIsSearchExpanded(false)}
                aria-label="收起搜索"
              >
                ×
              </button>
            </div>
          ) : (
            <button
              type="button"
              className={styles.searchCapsuleCollapsed}
              onClick={() => setIsSearchExpanded(true)}
              aria-label="展开搜索"
            >
              <span className={styles.searchIcon}>搜</span>
              <span className={styles.searchPlaceholder}>搜索帖子</span>
            </button>
          )}

          <button
            type="button"
            className={styles.publishButton}
            onClick={onPublishClick}
            disabled={!canPublish}
            title={!canPublish ? '请先登录后再发帖' : '发布新帖'}
          >
            发帖
          </button>
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
                displayTimeZone={displayTimeZone}
                onClick={() => onPostClick(post.voId)}
                godComment={
                  godComment
                    ? {
                        authorName: godComment.voAuthorName,
                        content: godComment.voContentSnapshot
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
