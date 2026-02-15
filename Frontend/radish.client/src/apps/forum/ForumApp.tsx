import { lazy, Suspense, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useUserStore } from '@/stores/userStore';
import { ConfirmDialog } from '@radish/ui/confirm-dialog';
import { getMyTimePreference, getTimeSettings } from '@/api/time';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId, resolveTimeZoneId } from '@/utils/dateTime';
import { CategoryList } from './components/CategoryList';
import { TagSection } from './components/TagSection';
import { TrendingSidebar } from './components/TrendingSidebar';
import { PostListView } from './views/PostListView';
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

export const ForumApp = () => {
  const { t } = useTranslation();
  const { isAuthenticated, userId } = useUserStore();
  const loggedIn = isAuthenticated();
  const containerShellRef = useRef<HTMLDivElement>(null);
  const [showDetailFloatingTools, setShowDetailFloatingTools] = useState(true);
  const [displayTimeZone, setDisplayTimeZone] = useState(DEFAULT_TIME_ZONE);

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

  // 数据管理
  const dataState = useForumData(t);

  // 事件处理
  const actionsState = useForumActions({
    t,
    isAuthenticated: loggedIn,
    userId: userId ?? 0,
    commentSortBy: dataState.commentSortBy,
    selectedCategoryId: dataState.selectedCategoryId,
    selectedTagName: dataState.selectedTagName,
    selectedPost: dataState.selectedPost,
    setSelectedPost: dataState.setSelectedPost,
    setComments: dataState.setComments,
    setCurrentPage: dataState.setCurrentPage,
    setSortBy: dataState.setSortBy,
    setCommentSortBy: dataState.setCommentSortBy,
    setSearchKeyword: dataState.setSearchKeyword,
    setError: dataState.setError,
    loadPostDetail: dataState.loadPostDetail,
    loadComments: dataState.loadComments,
    loadPosts: dataState.loadPosts,
    resetCommentSort: dataState.resetCommentSort
  });

  return (
    <div className={styles.containerShell} ref={containerShellRef}>
      <div className={styles.container}>
        {/* 左栏：分类和标签 */}
        <div className={styles.leftColumn}>
          {/* 发帖按钮 */}
          <div className={styles.publishSection}>
            <button
              className={styles.publishButton}
              onClick={() => actionsState.setIsPublishModalOpen(true)}
              disabled={!loggedIn}
              title={!loggedIn ? '请先登录后再发帖' : '发布新帖'}
            >
              发帖
            </button>
          </div>

          <div className={styles.categorySection}>
            <CategoryList
              categories={dataState.categories}
              selectedCategoryId={dataState.selectedCategoryId}
              onSelectCategory={dataState.setSelectedCategoryId}
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
              onSelectTag={dataState.setSelectedTagName}
            />
          </div>
        </div>

        {/* 中间栏：帖子瀑布流或帖子详情 */}
        <div className={styles.middleColumn}>
          {dataState.selectedPost ? (
            <Suspense fallback={<div style={{ padding: '1rem', textAlign: 'center' }}>详情加载中...</div>}>
              <PostDetailContentView
                post={dataState.selectedPost}
                comments={dataState.comments}
                loadingPostDetail={dataState.loadingPostDetail}
                loadingComments={dataState.loadingComments}
                displayTimeZone={displayTimeZone}
                isLiked={actionsState.likedPosts.has(dataState.selectedPost.voId)}
                isAuthenticated={loggedIn}
                showFloatingTools={showDetailFloatingTools}
                currentUserId={userId ?? 0}
                commentSortBy={dataState.commentSortBy}
                replyTo={actionsState.replyTo}
                onBack={() => {
                  dataState.setSelectedPost(null);
                  dataState.setComments([]);
                  dataState.resetCommentSort();
                }}
                onLike={actionsState.handleLikePost}
                onEdit={actionsState.handleEditPost}
                onViewPostHistory={actionsState.handleViewPostHistory}
                onDelete={actionsState.handleDeletePost}
                onCommentSortChange={actionsState.handleCommentSortChange}
                onDeleteComment={actionsState.handleDeleteComment}
                onEditComment={actionsState.handleEditComment}
                onViewCommentHistory={actionsState.handleViewCommentHistory}
                onLikeComment={actionsState.handleCommentLike}
                onReplyComment={actionsState.handleReplyComment}
                onLoadMoreChildren={actionsState.handleLoadMoreChildren}
                onCreateComment={actionsState.handleCreateComment}
                onCancelReply={actionsState.handleCancelReply}
              />
            </Suspense>
          ) : (
            /* 帖子列表视图 */
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
              searchKeyword={dataState.searchKeyword}
              loadingPosts={dataState.loadingPosts}
              onSortChange={actionsState.handleSortChange}
              onSearchChange={actionsState.handleSearchChange}
              onPageChange={actionsState.handlePageChange}
              onPostClick={actionsState.handleSelectPost}
            />
          )}
        </div>

        {/* 右栏：热门帖子和神评 */}
        <div className={styles.rightColumn}>
          <TrendingSidebar
            hotPosts={dataState.hotPosts}
            godComments={dataState.trendingGodComments}
            onPostClick={actionsState.handleSelectPost}
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
              onClose={() => actionsState.setIsEditModalOpen(false)}
              onSave={actionsState.handleSaveEdit}
            />
          </Suspense>
        )}

        {/* 删除帖子确认对话框 */}
        <ConfirmDialog
          isOpen={actionsState.isDeleteDialogOpen}
          title="确认删除"
          message="确定要删除这篇帖子吗？删除后无法恢复。"
          confirmText="删除"
          cancelText="取消"
          danger={true}
          onConfirm={actionsState.confirmDeletePost}
          onCancel={actionsState.cancelDeletePost}
        />

        {/* 删除评论确认对话框 */}
        <ConfirmDialog
          isOpen={actionsState.isDeleteCommentDialogOpen}
          title="确认删除"
          message="确定要删除这条评论吗？删除后无法恢复。"
          confirmText="删除"
          cancelText="取消"
          danger={true}
          onConfirm={actionsState.confirmDeleteComment}
          onCancel={actionsState.cancelDeleteComment}
        />

        {actionsState.isPostHistoryOpen && (
          <Suspense fallback={null}>
            <EditHistoryModal
              isOpen={actionsState.isPostHistoryOpen}
              title="帖子编辑历史"
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
              title="评论编辑历史"
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

        {/* 错误提示 */}
        {dataState.error && <p className={styles.errorText}>错误：{dataState.error}</p>}
      </div>
    </div>
  );
};
