import { useTranslation } from 'react-i18next';
import { useUserStore } from '@/stores/userStore';
import { ConfirmDialog } from '@radish/ui';
import { CategoryList } from './components/CategoryList';
import { PublishPostModal } from './components/PublishPostModal';
import { TrendingSidebar } from './components/TrendingSidebar';
import { EditPostModal } from './components/EditPostModal';
import { PostListView } from './views/PostListView';
import { PostDetailContentView } from './views/PostDetailContentView';
import { useForumData } from './hooks/useForumData';
import { useForumActions } from './hooks/useForumActions';
import styles from './ForumApp.module.css';

export const ForumApp = () => {
  const { t } = useTranslation();
  const { isAuthenticated, userId } = useUserStore();
  const loggedIn = isAuthenticated();

  // 数据管理
  const dataState = useForumData(t);

  // 事件处理
  const actionsState = useForumActions({
    t,
    isAuthenticated: loggedIn,
    userId: userId ?? 0,
    selectedCategoryId: dataState.selectedCategoryId,
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
    <div className={styles.containerShell}>
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
            {/* TODO: 标签区域 */}
          </div>
        </div>

        {/* 中间栏：帖子瀑布流或帖子详情 */}
        <div className={styles.middleColumn}>
          {dataState.selectedPost ? (
            /* 帖子详情视图 */
            <PostDetailContentView
              post={dataState.selectedPost}
              comments={dataState.comments}
              loadingPostDetail={dataState.loadingPostDetail}
              loadingComments={dataState.loadingComments}
              isLiked={actionsState.likedPosts.has(dataState.selectedPost.voId)}
              isAuthenticated={loggedIn}
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
              onDelete={actionsState.handleDeletePost}
              onCommentSortChange={actionsState.handleCommentSortChange}
              onDeleteComment={actionsState.handleDeleteComment}
              onEditComment={actionsState.handleEditComment}
              onLikeComment={actionsState.handleCommentLike}
              onReplyComment={actionsState.handleReplyComment}
              onLoadMoreChildren={actionsState.handleLoadMoreChildren}
              onCreateComment={actionsState.handleCreateComment}
              onCancelReply={actionsState.handleCancelReply}
            />
          ) : (
            /* 帖子列表视图 */
            <PostListView
              categories={dataState.categories}
              selectedCategoryId={dataState.selectedCategoryId}
              posts={dataState.posts}
              postGodComments={dataState.postGodComments}
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
          />
        </div>

        {/* 发帖 Modal */}
        <PublishPostModal
          isOpen={actionsState.isPublishModalOpen}
          isAuthenticated={loggedIn}
          onClose={() => actionsState.setIsPublishModalOpen(false)}
          onPublish={actionsState.handlePublishPost}
        />

        {/* 编辑帖子 Modal */}
        <EditPostModal
          isOpen={actionsState.isEditModalOpen}
          post={dataState.selectedPost}
          onClose={() => actionsState.setIsEditModalOpen(false)}
          onSave={actionsState.handleSaveEdit}
        />

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

        {/* 错误提示 */}
        {dataState.error && <p className={styles.errorText}>错误：{dataState.error}</p>}
      </div>
    </div>
  );
};
