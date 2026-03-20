import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  Category,
  PostItem,
  CommentHighlight,
  ForumPostViewMode,
  QuestionStatusFilter,
  PollStatusFilter,
  ForumPostSortBy
} from '@/api/forum';
import { Icon } from '@radish/ui/icon';
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
  sortBy: ForumPostSortBy;
  postViewMode: ForumPostViewMode;
  questionStatus: QuestionStatusFilter;
  pollStatus: PollStatusFilter;
  loadingPosts: boolean;
  canPublish: boolean;

  // 事件处理
  onSortChange: (sortBy: ForumPostSortBy) => void;
  onViewModeChange: (mode: ForumPostViewMode) => void;
  onQuestionStatusChange: (status: QuestionStatusFilter) => void;
  onPollStatusChange: (status: PollStatusFilter) => void;
  onOpenSearch: (keyword: string) => void;
  onPageChange: (page: number) => void;
  onPostClick: (postId: number) => void;
  onAuthorClick: (userId: number, userName?: string | null, avatarUrl?: string | null) => void;
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
  postViewMode,
  questionStatus,
  pollStatus,
  loadingPosts,
  canPublish,
  onSortChange,
  onViewModeChange,
  onQuestionStatusChange,
  onPollStatusChange,
  onOpenSearch,
  onPageChange,
  onPostClick,
  onAuthorClick,
  onPublishClick
}: PostListViewProps) => {
  const { t } = useTranslation();
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
              : categories.find(c => c.voId === selectedCategoryId)?.voName || t('forum.allPosts')}
          </h2>
          <div className={styles.viewButtons}>
            <button
              className={`${styles.sortButton} ${postViewMode === 'all' ? styles.sortActive : ''}`}
              onClick={() => onViewModeChange('all')}
            >
              {t('forum.viewMode.all')}
            </button>
            <button
              className={`${styles.sortButton} ${postViewMode === 'question' ? styles.sortActive : ''}`}
              onClick={() => onViewModeChange('question')}
            >
              {t('forum.viewMode.question')}
            </button>
            <button
              className={`${styles.sortButton} ${postViewMode === 'poll' ? styles.sortActive : ''}`}
              onClick={() => onViewModeChange('poll')}
            >
              {t('forum.viewMode.poll')}
            </button>
          </div>
          {postViewMode === 'question' && (
            <div className={styles.filterButtons}>
              <button
                className={`${styles.sortButton} ${questionStatus === 'all' ? styles.sortActive : ''}`}
                onClick={() => onQuestionStatusChange('all')}
              >
                {t('forum.filter.allStatus')}
              </button>
              <button
                className={`${styles.sortButton} ${questionStatus === 'pending' ? styles.sortActive : ''}`}
                onClick={() => onQuestionStatusChange('pending')}
              >
                {t('forum.filter.pending')}
              </button>
              <button
                className={`${styles.sortButton} ${questionStatus === 'solved' ? styles.sortActive : ''}`}
                onClick={() => onQuestionStatusChange('solved')}
              >
                {t('forum.filter.solved')}
              </button>
            </div>
          )}
          {postViewMode === 'poll' && (
            <div className={styles.filterButtons}>
              <button
                className={`${styles.sortButton} ${pollStatus === 'all' ? styles.sortActive : ''}`}
                onClick={() => onPollStatusChange('all')}
              >
                {t('forum.filter.allStatus')}
              </button>
              <button
                className={`${styles.sortButton} ${pollStatus === 'active' ? styles.sortActive : ''}`}
                onClick={() => onPollStatusChange('active')}
              >
                {t('forum.filter.active')}
              </button>
              <button
                className={`${styles.sortButton} ${pollStatus === 'closed' ? styles.sortActive : ''}`}
                onClick={() => onPollStatusChange('closed')}
              >
                {t('forum.filter.closed')}
              </button>
            </div>
          )}
          <div className={styles.sortButtons}>
            <button
              className={`${styles.sortButton} ${sortBy === 'newest' ? styles.sortActive : ''}`}
              onClick={() => onSortChange('newest')}
            >
              {t('forum.sort.newest')}
            </button>
            {postViewMode === 'question' ? (
              <>
                <button
                  className={`${styles.sortButton} ${sortBy === 'pending' ? styles.sortActive : ''}`}
                  onClick={() => onSortChange('pending')}
                >
                  {t('forum.sort.pendingFirst')}
                </button>
                <button
                  className={`${styles.sortButton} ${sortBy === 'answers' ? styles.sortActive : ''}`}
                  onClick={() => onSortChange('answers')}
                >
                  {t('forum.sort.answers')}
                </button>
              </>
            ) : postViewMode === 'poll' ? (
              <>
                <button
                  className={`${styles.sortButton} ${sortBy === 'votes' ? styles.sortActive : ''}`}
                  onClick={() => onSortChange('votes')}
                >
                  {t('forum.sort.votes')}
                </button>
                <button
                  className={`${styles.sortButton} ${sortBy === 'deadline' ? styles.sortActive : ''}`}
                  onClick={() => onSortChange('deadline')}
                >
                  {t('forum.sort.deadline')}
                </button>
              </>
            ) : (
              <>
                <button
                  className={`${styles.sortButton} ${sortBy === 'hottest' ? styles.sortActive : ''}`}
                  onClick={() => onSortChange('hottest')}
                >
                  {t('forum.sort.hottest')}
                </button>
              </>
            )}
          </div>
        </div>

        <div className={styles.toolbarRight}>
          {isSearchExpanded ? (
            <div className={styles.searchCapsuleExpanded} ref={searchCapsuleRef}>
              <input
                ref={searchInputRef}
                type="text"
                className={styles.searchInput}
                placeholder={t('forum.searchInputPlaceholder')}
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
                aria-label={t('forum.searchSubmit')}
              >
                {t('forum.searchSubmit')}
              </button>
              <button
                type="button"
                className={styles.searchCollapseButton}
                onClick={() => setIsSearchExpanded(false)}
                aria-label={t('forum.searchCollapse')}
              >
                ×
              </button>
            </div>
          ) : (
            <button
              type="button"
              className={styles.searchCapsuleCollapsed}
              onClick={() => setIsSearchExpanded(true)}
              aria-label={t('forum.searchExpand')}
            >
              <span className={styles.searchIcon} aria-hidden="true">
                <Icon icon="mdi:magnify" size={14} />
              </span>
              <span className={styles.searchPlaceholder}>{t('forum.searchPosts')}</span>
            </button>
          )}

          <button
            type="button"
            className={styles.publishButton}
            onClick={onPublishClick}
            disabled={!canPublish}
            title={!canPublish ? t('forum.loginRequiredToPublish') : t('forum.publishNewPost')}
          >
            {t('forum.publishPost')}
          </button>
        </div>
      </div>

      {/* 帖子瀑布流 */}
      <div className={styles.postsFeed}>
        {loadingPosts ? (
          <p className={styles.loadingText}>{t('forum.loadingPosts')}</p>
        ) : posts.length === 0 ? (
          <p className={styles.emptyText}>{t('forum.emptyPosts')}</p>
        ) : (
          posts.map((post) => {
            const godComment = postGodComments.get(post.voId);
            return (
              <PostCard
                key={post.voId}
                post={post}
                displayTimeZone={displayTimeZone}
                onClick={() => onPostClick(post.voId)}
                onAuthorClick={(userId, userName, avatarUrl) => onAuthorClick(userId, userName, avatarUrl)}
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
