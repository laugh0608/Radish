import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useCurrentWindow } from '@/desktop/useCurrentWindow';
import { useUserStore } from '@/stores/userStore';
import { useWindowStore } from '@/stores/windowStore';
import { log } from '@/utils/logger';
import { createForumCommentHighlightMap } from '@/utils/forumCommentHighlights';
import { ConfirmDialog } from '@radish/ui/confirm-dialog';
import { ContentReportModal } from '@/components/ContentReportModal';
import {
  getPostList,
  getCurrentGodCommentsBatch,
  getChildComments,
  getCommentNavigation,
  type PostItem,
  type CommentHighlight,
  type CommentNode,
} from '@/api/forum';
import type { ContentReportTargetType } from '@/api/contentModeration';
import {
  followUser,
  getFollowStatus,
  unfollowUser,
  type UserFollowStatus
} from '@/api/userFollow';
import { getMyTimePreference, getTimeSettings } from '@/api/time';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId, resolveTimeZoneId } from '@/utils/dateTime';
import { parseForumWindowParams } from '@/utils/forumNavigation';
import { CategoryList } from './components/CategoryList';
import { TagSection } from './components/TagSection';
import { TrendingSidebar } from './components/TrendingSidebar';
import { PostListView } from './views/PostListView';
import { ForumSearchView, type SearchTimeRange } from './views/ForumSearchView';
import { useForumData } from './hooks/useForumData';
import { useForumActions } from './hooks/useForumActions';
import styles from './ForumApp.module.css';

const PublishPostModal = lazy(() =>
  import('./components/PublishPostModal').then((module) => ({ default: module.PublishPostModal }))
);

const EditPostModal = lazy(() =>
  import('./components/EditPostModal').then((module) => ({ default: module.EditPostModal }))
);

const PostDetailContentView = lazy(() =>
  import('./views/PostDetailContentView').then((module) => ({ default: module.PostDetailContentView }))
);

const EditHistoryModal = lazy(() =>
  import('./components/EditHistoryModal').then((module) => ({ default: module.EditHistoryModal }))
);

const SEARCH_PAGE_SIZE = 20;
const COMMENT_NAVIGATION_CHILD_PAGE_SIZE = 5;

interface ForumCommentNavigationTarget {
  commentId: number;
  expandedRootCommentId?: number;
  navigationKey: string;
}

function mergeCommentChildren(
  comments: CommentNode[],
  parentCommentId: number,
  children: CommentNode[],
  totalChildren: number
): CommentNode[] {
  return comments.map((comment) => {
    if (comment.voId !== parentCommentId) {
      return comment;
    }

    return {
      ...comment,
      voChildren: children,
      voChildrenTotal: totalChildren
    };
  });
}

export const ForumApp = () => {
  const { t } = useTranslation();
  const { isAuthenticated, userId } = useUserStore();
  const roles = useUserStore((state) => state.roles || []);
  const { openApp } = useWindowStore();
  const currentWindow = useCurrentWindow();
  const loggedIn = isAuthenticated();
  const containerShellRef = useRef<HTMLDivElement>(null);
  const [showDetailFloatingTools, setShowDetailFloatingTools] = useState(true);
  const [displayTimeZone, setDisplayTimeZone] = useState(DEFAULT_TIME_ZONE);
  const [isSearchView, setIsSearchView] = useState(false);
  const [searchKeyword, setSearchKeyword] = useState('');
  const [searchDraftKeyword, setSearchDraftKeyword] = useState('');
  const [searchSortBy, setSearchSortBy] = useState<'newest' | 'hottest'>('newest');
  const [searchTimeRange, setSearchTimeRange] = useState<SearchTimeRange>('all');
  const [searchCustomStartDate, setSearchCustomStartDate] = useState('');
  const [searchCustomEndDate, setSearchCustomEndDate] = useState('');
  const [searchAppliedStartDate, setSearchAppliedStartDate] = useState('');
  const [searchAppliedEndDate, setSearchAppliedEndDate] = useState('');
  const [searchCurrentPage, setSearchCurrentPage] = useState(1);
  const [searchTotalCount, setSearchTotalCount] = useState(0);
  const [searchTotalPages, setSearchTotalPages] = useState(0);
  const [searchPosts, setSearchPosts] = useState<PostItem[]>([]);
  const [searchPostGodComments, setSearchPostGodComments] = useState<Map<string, CommentHighlight>>(new Map());
  const [loadingSearchPosts, setLoadingSearchPosts] = useState(false);
  const [reportTarget, setReportTarget] = useState<{ targetType: ContentReportTargetType; targetId: number } | null>(null);
  const [commentNavigationTarget, setCommentNavigationTarget] = useState<ForumCommentNavigationTarget | null>(null);
  const [commentNavigationNotice, setCommentNavigationNotice] = useState<string | null>(null);
  const searchRequestIdRef = useRef(0);
  const [followStatus, setFollowStatus] = useState<UserFollowStatus | null>(null);
  const [followLoading, setFollowLoading] = useState(false);
  const windowParams = parseForumWindowParams(currentWindow?.appParams);
  const handledWindowRouteRef = useRef<string | null>(null);
  const canTogglePostTop = roles.some((role) => {
    const normalized = role.trim().toLowerCase();
    return normalized === 'admin' || normalized === 'system';
  });

  useEffect(() => {
    const element = containerShellRef.current;
    if (!element) {
      return;
    }

    const syncByWidth = (width: number) => {
      setShowDetailFloatingTools(width <= 1200);
    };

    syncByWidth(element.clientWidth);

    if (typeof ResizeObserver === 'undefined') {
      return;
    }

    const observer = new ResizeObserver(entries => {
      const entry = entries[0];
      if (!entry) {
        return;
      }
      syncByWidth(entry.contentRect.width);
    });

    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const loadDisplayTimeZone = async () => {
      let systemTimeZone = DEFAULT_TIME_ZONE;
      try {
        const settings = await getTimeSettings();
        systemTimeZone = resolveTimeZoneId(settings.voDefaultTimeZoneId, DEFAULT_TIME_ZONE);
      } catch {
        systemTimeZone = DEFAULT_TIME_ZONE;
      }

      if (!loggedIn || !userId) {
        setDisplayTimeZone(systemTimeZone);
        return;
      }

      try {
        const preference = await getMyTimePreference();
        const resolvedSystemTimeZone = resolveTimeZoneId(preference.voSystemDefaultTimeZoneId, systemTimeZone);
        const browserTimeZone = getBrowserTimeZoneId(resolvedSystemTimeZone);
        const resolvedDisplayTimeZone = preference.voIsCustomized
          ? resolveTimeZoneId(preference.voTimeZoneId, browserTimeZone)
          : browserTimeZone;

        setDisplayTimeZone(resolvedDisplayTimeZone);
      } catch {
        setDisplayTimeZone(getBrowserTimeZoneId(systemTimeZone));
      }
    };

    void loadDisplayTimeZone();
  }, [loggedIn, userId]);

  const buildSearchTimeRange = () => {
    const now = new Date();
    switch (searchTimeRange) {
      case '24h': {
        return {
          startTime: new Date(now.getTime() - 24 * 60 * 60 * 1000).toISOString(),
          endTime: now.toISOString()
        };
      }
      case '7d': {
        return {
          startTime: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000).toISOString(),
          endTime: now.toISOString()
        };
      }
      case '30d': {
        return {
          startTime: new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000).toISOString(),
          endTime: now.toISOString()
        };
      }
      case 'custom': {
        if (!searchAppliedStartDate && !searchAppliedEndDate) {
          return { startTime: undefined, endTime: undefined };
        }

        const parsedStart = searchAppliedStartDate ? new Date(`${searchAppliedStartDate}T00:00:00`) : null;
        const parsedEnd = searchAppliedEndDate ? new Date(`${searchAppliedEndDate}T23:59:59.999`) : null;

        if ((parsedStart && Number.isNaN(parsedStart.getTime())) || (parsedEnd && Number.isNaN(parsedEnd.getTime()))) {
          return { startTime: undefined, endTime: undefined };
        }

        if (parsedStart && parsedEnd && parsedStart > parsedEnd) {
          return { startTime: parsedEnd.toISOString(), endTime: parsedStart.toISOString() };
        }

        return {
          startTime: parsedStart ? parsedStart.toISOString() : undefined,
          endTime: parsedEnd ? parsedEnd.toISOString() : undefined
        };
      }
      case 'all':
      default:
        return { startTime: undefined, endTime: undefined };
    }
  };

  // 数据管理
  const dataState = useForumData(t);

  // 事件处理
  const actionsState = useForumActions({
    t,
    isAuthenticated: loggedIn,
    userId: userId ?? 0,
    commentSortBy: dataState.commentSortBy,
    loadedCommentPages: dataState.loadedCommentPages,
    questionAnswerSort: dataState.questionAnswerSort,
    selectedCategoryId: dataState.selectedCategoryId,
    selectedTagName: dataState.selectedTagName,
    selectedPost: dataState.selectedPost,
    setSelectedPost: dataState.setSelectedPost,
    setComments: dataState.setComments,
    setQuickReplies: dataState.setQuickReplies,
    setQuickReplyTotal: dataState.setQuickReplyTotal,
    setCommentTotal: dataState.setCommentTotal,
    setCurrentPage: dataState.setCurrentPage,
    setSortBy: dataState.setSortBy,
    setCommentSortBy: dataState.setCommentSortBy,
    setQuestionAnswerSort: dataState.setQuestionAnswerSort,
    setQuestionAnswerFilter: dataState.setQuestionAnswerFilter,
    setSearchKeyword: dataState.setSearchKeyword,
    setError: dataState.setError,
    loadPostDetail: dataState.loadPostDetail,
    loadComments: dataState.loadComments,
    loadQuickReplies: dataState.loadQuickReplies,
    loadPosts: dataState.loadPosts,
    resetCommentSort: dataState.resetCommentSort
  });

  const navigateToComment = useCallback(async (
    postId: string | number,
    commentId: string | number,
    navigationKey: string
  ) => {
    const navigation = await getCommentNavigation(
      postId,
      commentId,
      dataState.commentPageSize,
      COMMENT_NAVIGATION_CHILD_PAGE_SIZE,
      t
    );

    if (navigation.voRootPageIndex > 1) {
      await dataState.loadComments(postId, navigation.voRootPageIndex);
    }

    if (!navigation.voIsRootComment && navigation.voParentCommentId && navigation.voChildPageIndex) {
      const aggregatedChildren: CommentNode[] = [];
      let totalChildren = 0;

      for (let pageIndex = 1; pageIndex <= navigation.voChildPageIndex; pageIndex += 1) {
        const pageData = await getChildComments(
          navigation.voParentCommentId,
          pageIndex,
          COMMENT_NAVIGATION_CHILD_PAGE_SIZE,
          t
        );

        totalChildren = pageData.voTotal ?? totalChildren;
        aggregatedChildren.push(...(pageData.voItems ?? []));
      }

      const deduplicatedChildren = aggregatedChildren.filter((child, index, source) =>
        source.findIndex((item) => item.voId === child.voId) === index
      );

      dataState.setComments((prev) =>
        mergeCommentChildren(prev, navigation.voParentCommentId as number, deduplicatedChildren, totalChildren)
      );
    }

    setCommentNavigationTarget({
      commentId: navigation.voCommentId,
      expandedRootCommentId: navigation.voIsRootComment
        ? undefined
        : navigation.voParentCommentId ?? navigation.voRootCommentId,
      navigationKey
    });
    setCommentNavigationNotice(null);
  }, [dataState.commentPageSize, dataState.loadComments, dataState.setComments, t]);

  const handleNavigateToComment = useCallback(async (commentId: number) => {
    const postId = dataState.selectedPost?.voId;
    if (!postId || commentId <= 0) {
      return;
    }

    try {
      await navigateToComment(postId, commentId, `inline:${postId}:${commentId}:${Date.now()}`);
    } catch {
      setCommentNavigationNotice(t('forum.commentNavigation.notice'));
    }
  }, [dataState.selectedPost?.voId, navigateToComment, t]);

  useEffect(() => {
    if (!windowParams.postId) {
      setCommentNavigationTarget(null);
      setCommentNavigationNotice(null);
      return;
    }

    const routeSignature = `${windowParams.postId}:${windowParams.commentId ?? 'none'}:${windowParams.navigationKey ?? 'initial'}`;
    if (handledWindowRouteRef.current === routeSignature) {
      return;
    }

    handledWindowRouteRef.current = routeSignature;

    let cancelled = false;

    const openPostFromWindow = async () => {
      if (!windowParams.postId) {
        return;
      }

      setIsSearchView(false);
      dataState.setSelectedTagName(null);
      dataState.setSelectedCategoryId(null);
      setCommentNavigationTarget(null);
      setCommentNavigationNotice(null);

      await actionsState.handleSelectPost(windowParams.postId);
      if (cancelled || !windowParams.commentId) {
        return;
      }

      try {
        if (cancelled) {
          return;
        }

        await navigateToComment(windowParams.postId, windowParams.commentId, routeSignature);
      } catch (err) {
        if (cancelled) {
          return;
        }

        setCommentNavigationTarget(null);
        setCommentNavigationNotice(t('forum.commentNavigation.notice'));
      }
    };

    void openPostFromWindow();

    return () => {
      cancelled = true;
    };
  }, [
    actionsState.handleSelectPost,
    dataState.commentPageSize,
    dataState.loadComments,
    dataState.setComments,
    dataState.setError,
    windowParams.commentId,
    windowParams.navigationKey,
    windowParams.postId,
    dataState.setSelectedCategoryId,
    dataState.setSelectedTagName,
    t,
    navigateToComment,
  ]);

  useEffect(() => {
    if (!isSearchView) {
      return;
    }

    const requestId = ++searchRequestIdRef.current;
    const loadSearchResults = async () => {
      setLoadingSearchPosts(true);
      dataState.setError(null);
      try {
        const { startTime, endTime } = buildSearchTimeRange();
        const result = await getPostList(
          null,
          t,
          searchCurrentPage,
          SEARCH_PAGE_SIZE,
          searchSortBy,
          searchKeyword,
          startTime,
          endTime
        );

        if (requestId !== searchRequestIdRef.current) {
          return;
        }

        setSearchPosts(result.data);
        setSearchTotalCount(result.dataCount);
        setSearchTotalPages(result.pageCount);

        if (result.data.length === 0) {
          setSearchPostGodComments(new Map());
          return;
        }

        const postIds = result.data.map(post => post.voId);
        try {
          const highlights = await getCurrentGodCommentsBatch(postIds, t);
          if (requestId !== searchRequestIdRef.current) {
            return;
          }

          setSearchPostGodComments(createForumCommentHighlightMap(highlights));
        } catch (highlightError) {
          if (requestId !== searchRequestIdRef.current) {
            return;
          }

          log.warn('搜索结果神评预览补查失败，已降级使用帖子主数据:', highlightError);
          setSearchPostGodComments(new Map());
        }
      } catch (err) {
        if (requestId !== searchRequestIdRef.current) {
          return;
        }

        setSearchPosts([]);
        setSearchTotalCount(0);
        setSearchTotalPages(0);
        const message = err instanceof Error ? err.message : String(err);
        dataState.setError(message);
      } finally {
        if (requestId === searchRequestIdRef.current) {
          setLoadingSearchPosts(false);
        }
      }
    };

    void loadSearchResults();
  }, [
    isSearchView,
    searchKeyword,
    searchSortBy,
    searchCurrentPage,
    searchTimeRange,
    searchAppliedStartDate,
    searchAppliedEndDate,
    t
  ]);

  useEffect(() => {
    if (!loggedIn || !dataState.selectedPost || !dataState.selectedPost.voAuthorId) {
      setFollowStatus(null);
      return;
    }

    if (String(dataState.selectedPost.voAuthorId) === String(userId ?? 0)) {
      setFollowStatus(null);
      return;
    }

    let cancelled = false;
    setFollowLoading(true);
    void getFollowStatus(dataState.selectedPost.voAuthorId)
      .then((status) => {
        if (!cancelled) {
          setFollowStatus(status);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setFollowStatus(null);
          const message = err instanceof Error ? err.message : String(err);
          dataState.setError(message);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setFollowLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [
    loggedIn,
    userId,
    dataState.selectedPost?.voId,
    dataState.selectedPost?.voAuthorId,
    dataState.setError
  ]);

  const handleToggleFollow = async (targetUserId: number, isFollowing: boolean) => {
    if (!loggedIn) {
      dataState.setError(t('forum.loginRequiredToFollow'));
      return;
    }

    setFollowLoading(true);
    dataState.setError(null);
    try {
      const status = isFollowing
        ? await unfollowUser(targetUserId)
        : await followUser(targetUserId);
      setFollowStatus(status);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      dataState.setError(message);
    } finally {
      setFollowLoading(false);
    }
  };

  const handleOpenUserProfile = (targetUserId: number, targetUserName?: string | null, avatarUrl?: string | null) => {
    if (!targetUserId) {
      return;
    }

    if (String(targetUserId) === String(userId ?? 0)) {
      openApp('profile');
      return;
    }

    openApp('profile', {
      userId: targetUserId,
      userName: targetUserName?.trim() || t('common.userFallback', { id: targetUserId }),
      avatarUrl: avatarUrl ?? null,
    });
  };

  const handleOpenReport = (targetType: ContentReportTargetType, targetId: number) => {
    if (!loggedIn) {
      dataState.setError(t('report.loginRequired'));
      return;
    }

    if (targetId <= 0) {
      return;
    }

    dataState.setError(null);
    setReportTarget({ targetType, targetId });
  };

  const handleOpenPublish = () => {
    actionsState.setIsPublishModalOpen(true);
  };

  const handleShowAllPosts = () => {
    setIsSearchView(false);
    setCommentNavigationTarget(null);
    setCommentNavigationNotice(null);
    dataState.setSelectedTagName(null);
    dataState.setSelectedCategoryId(null);
    dataState.setQuickReplies([]);
    dataState.setQuickReplyTotal(0);
    dataState.setSelectedPost(null);
    dataState.setComments([]);
    dataState.resetCommentSort();
  };

  const handleSelectCategory = (categoryId: number) => {
    setIsSearchView(false);
    setCommentNavigationTarget(null);
    setCommentNavigationNotice(null);
    dataState.setSelectedTagName(null);
    dataState.setSelectedCategoryId(categoryId);
  };

  const handleSelectTag = (tagName: string | null) => {
    setIsSearchView(false);
    setCommentNavigationTarget(null);
    setCommentNavigationNotice(null);
    dataState.setSelectedTagName(tagName);
    if (tagName) {
      dataState.setSelectedCategoryId(null);
    }
  };

  const handleOpenSearchView = (keyword: string) => {
    const normalizedKeyword = keyword.trim();
    setCommentNavigationTarget(null);
    setCommentNavigationNotice(null);
    dataState.setSelectedPost(null);
    dataState.setComments([]);
    dataState.setQuickReplies([]);
    dataState.setQuickReplyTotal(0);
    dataState.resetCommentSort();
    dataState.setError(null);

    setSearchDraftKeyword(normalizedKeyword);
    setSearchKeyword(normalizedKeyword);
    setSearchCurrentPage(1);
    setIsSearchView(true);
  };

  return (
    <div className={styles.containerShell} ref={containerShellRef}>
      <div className={styles.container}>
        {/* 左栏：分类和标签 */}
        <div className={styles.leftColumn}>
          {/* 全部帖子按钮 */}
          <div className={styles.publishSection}>
            <button
              className={`${styles.quickActionButton} ${
                dataState.selectedCategoryId === null && !dataState.selectedTagName
                  ? styles.quickActionButtonActive
                  : ''
              }`}
              onClick={handleShowAllPosts}
              title={t('forum.viewAllPosts')}
            >
              {t('forum.allPosts')}
            </button>
          </div>

          <div className={styles.categorySection}>
            <CategoryList
              categories={dataState.categories}
              selectedCategoryId={dataState.selectedCategoryId}
              onSelectCategory={handleSelectCategory}
              loading={dataState.loadingCategories}
            />
          </div>
          <div className={styles.tagsSection}>
            <TagSection
              fixedTags={dataState.fixedTags}
              hotTags={dataState.hotTags.filter(
                tag => !dataState.fixedTags.some(fixedTag => fixedTag.voName.toLowerCase() === tag.voName.toLowerCase())
              )}
              selectedTagName={dataState.selectedTagName}
              loading={dataState.loadingHotTags}
              onSelectTag={handleSelectTag}
            />
          </div>
        </div>

        {/* 中间栏：帖子瀑布流或帖子详情 */}
        <div className={styles.middleColumn}>
          {dataState.selectedPost ? (
            <Suspense fallback={<div style={{ padding: '1rem', textAlign: 'center' }}>{t('forum.detailLoading')}</div>}>
              <PostDetailContentView
                post={dataState.selectedPost}
                comments={dataState.comments}
                quickReplies={dataState.quickReplies}
                quickReplyTotal={dataState.quickReplyTotal}
                commentTotal={dataState.commentTotal}
                commentPageSize={dataState.commentPageSize}
                loadingPostDetail={dataState.loadingPostDetail}
                loadingComments={dataState.loadingComments}
                loadingQuickReplies={dataState.loadingQuickReplies}
                loadingMoreComments={dataState.loadingMoreComments}
                displayTimeZone={displayTimeZone}
                isLiked={actionsState.likedPosts.has(dataState.selectedPost.voId)}
                isAuthenticated={loggedIn}
                showFloatingTools={showDetailFloatingTools}
                currentUserId={userId ?? 0}
                commentSortBy={dataState.commentSortBy}
                questionAnswerSort={dataState.questionAnswerSort}
                questionAnswerFilter={dataState.questionAnswerFilter}
                replyTo={actionsState.replyTo}
                followStatus={followStatus}
                followLoading={followLoading}
                commentNavigationTarget={commentNavigationTarget}
                onBack={() => {
                  setCommentNavigationTarget(null);
                  setCommentNavigationNotice(null);
                  dataState.setSelectedPost(null);
                  dataState.setComments([]);
                  dataState.setQuickReplies([]);
                  dataState.setQuickReplyTotal(0);
                  dataState.resetCommentSort();
                }}
                onLike={actionsState.handleLikePost}
                onVotePoll={actionsState.handleVotePoll}
                onClosePoll={actionsState.handleClosePoll}
                onDrawLottery={actionsState.handleDrawLottery}
                onAnswerQuestion={actionsState.handleAnswerQuestion}
                onAcceptAnswer={actionsState.handleAcceptAnswer}
                onQuestionAnswerSortChange={actionsState.handleQuestionAnswerSortChange}
                onQuestionAnswerFilterChange={actionsState.handleQuestionAnswerFilterChange}
                canToggleTop={canTogglePostTop}
                onToggleTop={actionsState.handleTogglePostTop}
                onEdit={actionsState.handleEditPost}
                onViewPostHistory={actionsState.handleViewPostHistory}
                onDelete={actionsState.handleDeletePost}
                onCreateQuickReply={actionsState.handleCreateQuickReply}
                onDeleteQuickReply={actionsState.handleDeleteQuickReply}
                onCommentSortChange={actionsState.handleCommentSortChange}
                onDeleteComment={actionsState.handleDeleteComment}
                onEditComment={actionsState.handleEditComment}
                onViewCommentHistory={actionsState.handleViewCommentHistory}
                onLikeComment={actionsState.handleCommentLike}
                onReplyComment={actionsState.handleReplyComment}
                onLoadMoreChildren={actionsState.handleLoadMoreChildren}
                onLoadMoreComments={dataState.loadMoreComments}
                onCreateComment={actionsState.handleCreateComment}
                onCancelReply={actionsState.handleCancelReply}
                onReactionError={dataState.setError}
                onToggleFollow={handleToggleFollow}
                onAuthorClick={handleOpenUserProfile}
                onReportPost={(postId) => handleOpenReport('Post', postId)}
                onReportQuickReply={(quickReplyId) => handleOpenReport('PostQuickReply', quickReplyId)}
                onReportComment={(commentId) => handleOpenReport('Comment', commentId)}
                onNavigateToComment={handleNavigateToComment}
              />
            </Suspense>
          ) : (
            <>
              {isSearchView ? (
                <ForumSearchView
                  keyword={searchKeyword}
                  draftKeyword={searchDraftKeyword}
                  sortBy={searchSortBy}
                  timeRange={searchTimeRange}
                  totalCount={searchTotalCount}
                  customStartDate={searchCustomStartDate}
                  customEndDate={searchCustomEndDate}
                  appliedStartDate={searchAppliedStartDate}
                  appliedEndDate={searchAppliedEndDate}
                  isCustomRangeDirty={
                    searchCustomStartDate !== searchAppliedStartDate ||
                    searchCustomEndDate !== searchAppliedEndDate
                  }
                  posts={searchPosts}
                  postGodComments={searchPostGodComments}
                  loading={loadingSearchPosts}
                  currentPage={searchCurrentPage}
                  totalPages={searchTotalPages}
                  displayTimeZone={displayTimeZone}
                  onBack={() => setIsSearchView(false)}
                  onDraftKeywordChange={setSearchDraftKeyword}
                  onSearchSubmit={() => {
                    setSearchKeyword(searchDraftKeyword.trim());
                    setSearchCurrentPage(1);
                  }}
                  onSortChange={(value) => {
                    setSearchSortBy(value);
                    setSearchCurrentPage(1);
                  }}
                  onTimeRangeChange={(value) => {
                    setSearchTimeRange(value);
                    setSearchCurrentPage(1);
                  }}
                  onCustomStartDateChange={setSearchCustomStartDate}
                  onCustomEndDateChange={setSearchCustomEndDate}
                  onApplyCustomRange={() => {
                    setSearchAppliedStartDate(searchCustomStartDate);
                    setSearchAppliedEndDate(searchCustomEndDate);
                    setSearchTimeRange('custom');
                    setSearchCurrentPage(1);
                  }}
                  onPageChange={setSearchCurrentPage}
                  onPostClick={actionsState.handleSelectPost}
                  onAuthorClick={handleOpenUserProfile}
                />
              ) : (
                <PostListView
                  categories={dataState.categories}
                  selectedCategoryId={dataState.selectedCategoryId}
                  selectedTagName={dataState.selectedTagName}
                  posts={dataState.posts}
                  postGodComments={dataState.postGodComments}
                  displayTimeZone={displayTimeZone}
                  currentPage={dataState.currentPage}
                  totalPages={dataState.totalPages}
                  sortBy={dataState.sortBy}
                  postViewMode={dataState.postViewMode}
                  questionStatus={dataState.questionStatus}
                  pollStatus={dataState.pollStatus}
                  loadingPosts={dataState.loadingPosts}
                  onSortChange={actionsState.handleSortChange}
                  onViewModeChange={dataState.setPostViewMode}
                  onQuestionStatusChange={dataState.setQuestionStatus}
                  onPollStatusChange={dataState.setPollStatus}
                  onOpenSearch={handleOpenSearchView}
                  onPageChange={actionsState.handlePageChange}
                  onPostClick={actionsState.handleSelectPost}
                  onAuthorClick={handleOpenUserProfile}
                  canPublish={loggedIn}
                  onPublishClick={handleOpenPublish}
                />
              )}
            </>
          )}
        </div>

        {/* 右栏：热门帖子和神评 */}
        <div className={styles.rightColumn}>
          <TrendingSidebar
            hotPosts={dataState.hotPosts}
            godComments={dataState.trendingGodComments}
            onPostClick={actionsState.handleSelectPost}
            onAuthorClick={handleOpenUserProfile}
            loading={dataState.loadingTrending}
            selectedPost={dataState.selectedPost}
            displayTimeZone={displayTimeZone}
          />
        </div>

        {actionsState.isPublishModalOpen && (
          <Suspense fallback={null}>
            <PublishPostModal
              isOpen={actionsState.isPublishModalOpen}
              isAuthenticated={loggedIn}
              categories={dataState.categories}
              selectedCategoryId={dataState.selectedCategoryId}
              onClose={() => actionsState.setIsPublishModalOpen(false)}
              onPublish={actionsState.handlePublishPost}
            />
          </Suspense>
        )}

        {actionsState.isEditModalOpen && dataState.selectedPost && (
          <Suspense fallback={null}>
            <EditPostModal
              isOpen={actionsState.isEditModalOpen}
              post={dataState.selectedPost}
              categories={dataState.categories}
              onClose={() => actionsState.setIsEditModalOpen(false)}
              onSave={actionsState.handleSaveEdit}
            />
          </Suspense>
        )}

        {/* 删除帖子确认对话框 */}
        <ConfirmDialog
          isOpen={actionsState.isDeleteDialogOpen}
          title={t('forum.confirmDeleteTitle')}
          message={t('forum.confirmDeletePostMessage')}
          confirmText={t('common.delete')}
          cancelText={t('common.cancel')}
          danger={true}
          onConfirm={actionsState.confirmDeletePost}
          onCancel={actionsState.cancelDeletePost}
        />

        {/* 删除评论确认对话框 */}
        <ConfirmDialog
          isOpen={actionsState.isDeleteCommentDialogOpen}
          title={t('forum.confirmDeleteTitle')}
          message={t('forum.confirmDeleteCommentMessage')}
          confirmText={t('common.delete')}
          cancelText={t('common.cancel')}
          danger={true}
          onConfirm={actionsState.confirmDeleteComment}
          onCancel={actionsState.cancelDeleteComment}
        />

        {actionsState.isPostHistoryOpen && (
          <Suspense fallback={null}>
            <EditHistoryModal
              isOpen={actionsState.isPostHistoryOpen}
              title={dataState.selectedPost?.voIsQuestion ? t('forum.questionHistoryTitle') : t('forum.postHistoryTitle')}
              loading={actionsState.postHistoryLoading}
              error={actionsState.postHistoryError}
              items={actionsState.postHistories}
              total={actionsState.postHistoryTotal}
              pageIndex={actionsState.postHistoryPageIndex}
              pageSize={10}
              onClose={actionsState.closePostHistory}
              onPageChange={actionsState.handlePostHistoryPageChange}
              renderContent={(item) => {
                const history = item as import('@/types/forum').PostEditHistory;
                return {
                  beforeTitle: history.voOldTitle,
                  afterTitle: history.voNewTitle,
                  before: history.voOldContent,
                  after: history.voNewContent
                };
              }}
            />
          </Suspense>
        )}

        {actionsState.isCommentHistoryOpen && (
          <Suspense fallback={null}>
            <EditHistoryModal
              isOpen={actionsState.isCommentHistoryOpen}
              title={t('forum.commentHistoryTitle')}
              loading={actionsState.commentHistoryLoading}
              error={actionsState.commentHistoryError}
              items={actionsState.commentHistories}
              total={actionsState.commentHistoryTotal}
              pageIndex={actionsState.commentHistoryPageIndex}
              pageSize={10}
              onClose={actionsState.closeCommentHistory}
              onPageChange={actionsState.handleCommentHistoryPageChange}
              renderContent={(item) => {
                const history = item as import('@/types/forum').CommentEditHistory;
                return {
                  before: history.voOldContent,
                  after: history.voNewContent
                };
              }}
            />
          </Suspense>
        )}

        {reportTarget && (
          <ContentReportModal
            isOpen={!!reportTarget}
            targetType={reportTarget.targetType}
            targetId={reportTarget.targetId}
            onClose={() => setReportTarget(null)}
          />
        )}

        {commentNavigationNotice && <p className={styles.noticeText}>{commentNavigationNotice}</p>}
        {/* 错误提示 */}
        {dataState.error && <p className={styles.errorText}>{t('common.errorPrefix')}{dataState.error}</p>}
      </div>
    </div>
  );
};
