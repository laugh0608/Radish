import { useMemo, useRef } from 'react';
import type { PostItem, CommentHighlight } from '@/api/forum';
import { PostCard } from '../components/PostCard';
import styles from './ForumSearchView.module.css';

export type SearchTimeRange = 'all' | '24h' | '7d' | '30d' | 'custom';

interface ForumSearchViewProps {
  keyword: string;
  draftKeyword: string;
  sortBy: 'newest' | 'hottest';
  timeRange: SearchTimeRange;
  totalCount: number;
  customStartDate: string;
  customEndDate: string;
  appliedStartDate: string;
  appliedEndDate: string;
  isCustomRangeDirty: boolean;
  posts: PostItem[];
  postGodComments: Map<number, CommentHighlight>;
  loading: boolean;
  currentPage: number;
  totalPages: number;
  displayTimeZone: string;
  onBack: () => void;
  onDraftKeywordChange: (value: string) => void;
  onSearchSubmit: () => void;
  onSortChange: (value: 'newest' | 'hottest') => void;
  onTimeRangeChange: (value: SearchTimeRange) => void;
  onCustomStartDateChange: (value: string) => void;
  onCustomEndDateChange: (value: string) => void;
  onApplyCustomRange: () => void;
  onPageChange: (page: number) => void;
  onPostClick: (postId: number) => void;
  onAuthorClick: (userId: number, userName?: string | null, avatarUrl?: string | null) => void;
}

export const ForumSearchView = ({
  keyword,
  draftKeyword,
  sortBy,
  timeRange,
  totalCount,
  customStartDate,
  customEndDate,
  appliedStartDate,
  appliedEndDate,
  isCustomRangeDirty,
  posts,
  postGodComments,
  loading,
  currentPage,
  totalPages,
  displayTimeZone,
  onBack,
  onDraftKeywordChange,
  onSearchSubmit,
  onSortChange,
  onTimeRangeChange,
  onCustomStartDateChange,
  onCustomEndDateChange,
  onApplyCustomRange,
  onPageChange,
  onPostClick,
  onAuthorClick
}: ForumSearchViewProps) => {
  const keywordInputRef = useRef<HTMLInputElement>(null);
  const sortButtonsRef = useRef<HTMLDivElement>(null);
  const timeButtonsRef = useRef<HTMLDivElement>(null);

  const keywordSummary = useMemo(
    () => (keyword.trim() ? `关键词：${keyword.trim()}` : '关键词：全部'),
    [keyword]
  );

  const sortSummary = useMemo(
    () => `排序：${sortBy === 'hottest' ? '最热' : '最新'}`,
    [sortBy]
  );

  const timeSummary = useMemo(() => {
    switch (timeRange) {
      case '24h':
        return '时间：最近24小时';
      case '7d':
        return '时间：最近7天';
      case '30d':
        return '时间：最近30天';
      case 'custom': {
        if (appliedStartDate || appliedEndDate) {
          const start = appliedStartDate || '最早';
          const end = appliedEndDate || '现在';
          return `时间：自定义 ${start} ~ ${end}`;
        }
        return '时间：自定义（未应用）';
      }
      case 'all':
      default:
        return '时间：全部';
    }
  }, [timeRange, appliedStartDate, appliedEndDate]);

  const focusFirstButton = (container: HTMLDivElement | null) => {
    const firstButton = container?.querySelector('button');
    if (firstButton instanceof HTMLButtonElement) {
      firstButton.focus();
    }
  };

  const resultTitle = useMemo(() => {
    if (keyword.trim()) {
      return `“${keyword.trim()}” 的搜索结果`;
    }
    return '搜索结果';
  }, [keyword]);

  return (
    <>
      <div className={styles.header}>
        <div className={styles.headerTop}>
          <button type="button" className={styles.backButton} onClick={onBack}>
            返回列表
          </button>
          <h2 className={styles.title}>{resultTitle}</h2>
          <span className={styles.resultCount}>共 {totalCount} 条</span>
        </div>

        <div className={styles.summaryBar}>
          <button
            type="button"
            className={styles.summaryItem}
            onClick={() => keywordInputRef.current?.focus()}
          >
            {keywordSummary}
          </button>
          <button
            type="button"
            className={styles.summaryItem}
            onClick={() => focusFirstButton(timeButtonsRef.current)}
          >
            {timeSummary}
          </button>
          <button
            type="button"
            className={styles.summaryItem}
            onClick={() => focusFirstButton(sortButtonsRef.current)}
          >
            {sortSummary}
          </button>
        </div>

        <div className={styles.filterBar}>
          <div className={styles.searchForm}>
            <input
              ref={keywordInputRef}
              type="text"
              className={styles.searchInput}
              placeholder="输入关键词后搜索"
              value={draftKeyword}
              onChange={(event) => onDraftKeywordChange(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  event.preventDefault();
                  onSearchSubmit();
                }
              }}
            />
            <button type="button" className={styles.searchButton} onClick={onSearchSubmit}>
              搜索
            </button>
          </div>

          <div className={styles.sortButtons} ref={sortButtonsRef}>
            <button
              type="button"
              className={`${styles.sortButton} ${sortBy === 'newest' ? styles.active : ''}`}
              onClick={() => onSortChange('newest')}
            >
              最新
            </button>
            <button
              type="button"
              className={`${styles.sortButton} ${sortBy === 'hottest' ? styles.active : ''}`}
              onClick={() => onSortChange('hottest')}
            >
              最热
            </button>
          </div>

          <div className={styles.timeButtons} ref={timeButtonsRef}>
            <button
              type="button"
              className={`${styles.timeButton} ${timeRange === 'all' ? styles.active : ''}`}
              onClick={() => onTimeRangeChange('all')}
            >
              全部时间
            </button>
            <button
              type="button"
              className={`${styles.timeButton} ${timeRange === '24h' ? styles.active : ''}`}
              onClick={() => onTimeRangeChange('24h')}
            >
              24小时
            </button>
            <button
              type="button"
              className={`${styles.timeButton} ${timeRange === '7d' ? styles.active : ''}`}
              onClick={() => onTimeRangeChange('7d')}
            >
              7天
            </button>
            <button
              type="button"
              className={`${styles.timeButton} ${timeRange === '30d' ? styles.active : ''}`}
              onClick={() => onTimeRangeChange('30d')}
            >
              30天
            </button>
            <button
              type="button"
              className={`${styles.timeButton} ${timeRange === 'custom' ? styles.active : ''}`}
              onClick={() => onTimeRangeChange('custom')}
            >
              自定义
            </button>
          </div>
        </div>

        {timeRange === 'custom' && (
          <div className={styles.customRange}>
            <input
              type="date"
              className={styles.dateInput}
              value={customStartDate}
              onChange={(event) => onCustomStartDateChange(event.target.value)}
            />
            <span className={styles.dateSeparator}>至</span>
            <input
              type="date"
              className={styles.dateInput}
              value={customEndDate}
              onChange={(event) => onCustomEndDateChange(event.target.value)}
            />
            <button
              type="button"
              className={styles.applyButton}
              onClick={onApplyCustomRange}
              disabled={!isCustomRangeDirty}
            >
              应用时间
            </button>
            {isCustomRangeDirty && <span className={styles.pendingHint}>已修改，点击“应用时间”生效</span>}
          </div>
        )}
      </div>

      <div className={styles.postsFeed}>
        {loading ? (
          <p className={styles.loadingText}>搜索中...</p>
        ) : posts.length === 0 ? (
          <p className={styles.emptyText}>暂无匹配结果，请尝试调整关键词或时间范围</p>
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

      {totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            className={styles.paginationButton}
            onClick={() => onPageChange(currentPage - 1)}
            disabled={currentPage === 1}
          >
            ‹
          </button>
          {Array.from({ length: Math.min(totalPages, 7) }, (_, index) => {
            let pageNum: number;
            if (totalPages <= 7) {
              pageNum = index + 1;
            } else if (currentPage <= 4) {
              pageNum = index + 1;
            } else if (currentPage >= totalPages - 3) {
              pageNum = totalPages - 6 + index;
            } else {
              pageNum = currentPage - 3 + index;
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
