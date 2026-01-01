import { useEffect, useState, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { useUserStore } from '@/stores/userStore';
import { ConfirmDialog } from '@radish/ui';
import {
  getTopCategories,
  getPostList,
  getPostById,
  getCommentTree,
  getChildComments,
  publishPost,
  createComment,
  likePost,
  toggleCommentLike,
  updatePost,
  updateComment,
  deletePost,
  deleteComment,
  getCurrentGodComments
} from '@/api/forum';
import type {
  Category,
  PostItem,
  PostDetail,
  CommentNode,
  CommentHighlight
} from '@/types/forum';
import { CategoryList } from './components/CategoryList';
import { PostCard } from './components/PostCard';
import { PublishPostModal } from './components/PublishPostModal';
import { TrendingSidebar } from './components/TrendingSidebar';
import { PostDetail as PostDetailView } from './components/PostDetail';
import { CommentTree } from './components/CommentTree';
import { CreateCommentForm } from './components/CreateCommentForm';
import { EditPostModal } from './components/EditPostModal';
import styles from './ForumApp.module.css';

export const ForumApp = () => {
  const { t } = useTranslation();
  const { isAuthenticated, userId } = useUserStore();

  // 数据状态
  const [categories, setCategories] = useState<Category[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<number | null>(null);
  const [posts, setPosts] = useState<PostItem[]>([]);
  const [selectedPost, setSelectedPost] = useState<PostDetail | null>(null);
  const [comments, setComments] = useState<CommentNode[]>([]);

  // 新增：热门帖子和神评数据
  const [hotPosts, setHotPosts] = useState<PostItem[]>([]);
  const [trendingGodComments, setTrendingGodComments] = useState<CommentNode[]>([]);
  const [postGodComments, setPostGodComments] = useState<Map<number, CommentHighlight>>(new Map());

  // 分页状态
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalPages, setTotalPages] = useState(0);

  // 排序状态
  const [sortBy, setSortBy] = useState<'newest' | 'hottest' | 'essence'>('newest');
  const [commentSortBy, setCommentSortBy] = useState<'newest' | 'hottest' | null>(null); // null表示默认排序(时间升序)

  // 搜索状态
  const [searchKeyword, setSearchKeyword] = useState('');

  // 新增：发帖 Modal 状态
  const [isPublishModalOpen, setIsPublishModalOpen] = useState(false);

  // 编辑和删除状态
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [postToDelete, setPostToDelete] = useState<number | null>(null);
  const [isDeleteCommentDialogOpen, setIsDeleteCommentDialogOpen] = useState(false);
  const [commentToDelete, setCommentToDelete] = useState<number | null>(null);

  // 回复状态
  const [replyTo, setReplyTo] = useState<{ commentId: number; authorName: string } | null>(null);

  // 点赞状态（使用 localStorage 记录用户点赞过的帖子）
  const [likedPosts, setLikedPosts] = useState<Set<number>>(() => {
    try {
      const stored = localStorage.getItem('forum_liked_posts');
      return stored ? new Set(JSON.parse(stored)) : new Set();
    } catch {
      return new Set();
    }
  });

  // 加载状态
  const [loadingCategories, setLoadingCategories] = useState(false);
  const [loadingPosts, setLoadingPosts] = useState(false);
  const [loadingPostDetail, setLoadingPostDetail] = useState(false);
  const [loadingComments, setLoadingComments] = useState(false);
  const [loadingTrending, setLoadingTrending] = useState(false);

  // 错误状态
  const [error, setError] = useState<string | null>(null);

  const loggedIn = isAuthenticated();

  // 加载分类列表
  useEffect(() => {
    void loadCategories();
  }, []);

  // 加载热门内容（启动时加载一次）
  useEffect(() => {
    void loadTrendingContent();
  }, []);

  // 当选择分类时加载帖子列表并重置页码
  useEffect(() => {
    setCurrentPage(1); // 切换分类时重置到第一页
    void loadPosts();
  }, [selectedCategoryId]);

  // 当页码或排序方式或搜索关键词变化时加载帖子列表
  useEffect(() => {
    void loadPosts();
  }, [currentPage, sortBy, searchKeyword]);

  async function loadCategories() {
    setLoadingCategories(true);
    setError(null);
    try {
      const data = await getTopCategories(t);
      setCategories(data);
      if (data.length > 0 && selectedCategoryId == null) {
        setSelectedCategoryId(data[0].id);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingCategories(false);
    }
  }

  async function loadPosts() {
    setLoadingPosts(true);
    setError(null);
    try {
      const pageModel = await getPostList(
        selectedCategoryId,
        t,
        currentPage,
        pageSize,
        sortBy,
        searchKeyword
      );
      setPosts(pageModel.data);
      setTotalPages(pageModel.pageCount);

      // 加载每个帖子的神评（如果有）
      await loadGodCommentsForPosts(pageModel.data);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingPosts(false);
    }
  }

  // 加载热门内容（热门帖子和热门神评）
  async function loadTrendingContent() {
    setLoadingTrending(true);
    try {
      // 获取热门帖子（前10条）
      const hotPostsModel = await getPostList(
        null, // 全部分类
        t,
        1,
        10,
        'hottest' // 按热度排序
      );
      setHotPosts(hotPostsModel.data);

      // 获取全局热门神评：从热门帖子中收集神评
      const allGodComments: CommentNode[] = [];
      for (const post of hotPostsModel.data.slice(0, 5)) { // 只取前5个热门帖子的神评
        try {
          const godComments = await getCurrentGodComments(post.id, t);
          if (godComments.length > 0) {
            const topGodComment = godComments[0]; // 取第一个神评
            allGodComments.push({
              id: topGodComment.commentId,
              content: topGodComment.contentSnapshot || '',
              authorId: topGodComment.authorId,
              authorName: topGodComment.authorName,
              createTime: topGodComment.createTime,
              likeCount: topGodComment.likeCount,
              isLiked: false,
              isGodComment: true,
              isSofa: false,
              postId: post.id,
              parentId: null,
              replyToUserId: null,
              replyToUserName: null,
              children: []
            });
          }
        } catch {
          // 忽略单个帖子的错误
        }
      }
      // 按点赞数排序，取前10条
      allGodComments.sort((a, b) => (b.likeCount || 0) - (a.likeCount || 0));
      setTrendingGodComments(allGodComments.slice(0, 10));
    } catch (err) {
      console.error('加载热门内容失败:', err);
    } finally {
      setLoadingTrending(false);
    }
  }

  // 为帖子列表加载神评预览
  async function loadGodCommentsForPosts(postList: PostItem[]) {
    const godCommentsMap = new Map<number, CommentHighlight>();

    // 并行加载所有帖子的神评
    await Promise.all(
      postList.map(async (post) => {
        try {
          const godComments = await getCurrentGodComments(post.id, t);
          if (godComments.length > 0) {
            godCommentsMap.set(post.id, godComments[0]); // 只取第一个神评
          }
        } catch {
          // 忽略单个帖子的错误
        }
      })
    );

    setPostGodComments(godCommentsMap);
  }

  async function loadPostDetail(postId: number) {
    setLoadingPostDetail(true);
    setError(null);
    try {
      const data = await getPostById(postId, t);
      setSelectedPost(data);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      setSelectedPost(null);
      setComments([]);
    } finally {
      setLoadingPostDetail(false);
    }
  }

  async function loadComments(postId: number) {
    setLoadingComments(true);
    setError(null);
    try {
      const sortParam = commentSortBy || 'default'; // null时传'default'

      // 后端已经在 GetCommentTreeWithLikeStatusAsync 中填充了神评和沙发标识
      // 不需要再额外调用 getCurrentGodComments 和 getCurrentSofas API
      const commentsData = await getCommentTree(postId, sortParam, t);

      setComments(commentsData);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingComments(false);
    }
  }

  function handleCommentSortChange(newSortBy: 'newest' | 'hottest') {
    setCommentSortBy(newSortBy);
    if (selectedPost) {
      // 直接使用新的排序值，而不是依赖状态
      const sortParam = newSortBy;
      setLoadingComments(true);
      setError(null);

      // 后端已经在 GetCommentTreeWithLikeStatusAsync 中填充了神评和沙发标识
      // 不需要再额外调用 getCurrentGodComments 和 getCurrentSofas API
      getCommentTree(selectedPost.id, sortParam, t)
        .then(commentsData => {
          setComments(commentsData);
        })
        .catch(err => {
          const message = err instanceof Error ? err.message : String(err);
          setError(message);
        })
        .finally(() => {
          setLoadingComments(false);
        });
    }
  }

  // 重置评论排序为默认
  function resetCommentSort() {
    setCommentSortBy(null);
  }

  async function handleSelectPost(postId: number) {
    resetCommentSort(); // 切换帖子时重置评论排序
    await loadPostDetail(postId);
    await loadComments(postId);
  }

  async function handlePublishPost(title: string, content: string) {
    if (!selectedCategoryId) {
      setError('请先选择分类');
      return;
    }

    setError(null);
    try {
      const postId = await publishPost(
        {
          title,
          content,
          categoryId: selectedCategoryId,
          tagNames: []
        },
        t
      );
      // 发布成功后关闭 Modal 并重新加载帖子列表（回到第一页）
      setIsPublishModalOpen(false);
      setCurrentPage(1);
      await loadPosts();
      // 自动选择新发布的帖子
      await handleSelectPost(postId);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      throw err; // 让 Modal 显示错误
    }
  }

  function handlePageChange(page: number) {
    setCurrentPage(page);
  }

  function handleSortChange(newSortBy: 'newest' | 'hottest' | 'essence') {
    setSortBy(newSortBy);
    setCurrentPage(1); // 切换排序时重置到第一页
  }

  const handleSearchChange = useCallback((keyword: string) => {
    setSearchKeyword(keyword);
    setCurrentPage(1); // 搜索时重置到第一页
  }, []);

  async function handleLikePost(postId: number) {
    if (!loggedIn) {
      setError('请先登录后再点赞');
      return;
    }

    const isCurrentlyLiked = likedPosts.has(postId);
    const isLike = !isCurrentlyLiked;

    try {
      // 乐观更新：先更新UI
      const newLikedPosts = new Set(likedPosts);
      if (isLike) {
        newLikedPosts.add(postId);
      } else {
        newLikedPosts.delete(postId);
      }
      setLikedPosts(newLikedPosts);
      localStorage.setItem('forum_liked_posts', JSON.stringify([...newLikedPosts]));

      // 更新选中帖子的点赞数
      if (selectedPost && selectedPost.id === postId) {
        setSelectedPost({
          ...selectedPost,
          likeCount: (selectedPost.likeCount || 0) + (isLike ? 1 : -1)
        });
      }

      // 调用API
      await likePost(postId, isLike, t);

      // 刷新帖子详情（确保同步）
      await loadPostDetail(postId);
    } catch (err) {
      // 失败时回滚
      setLikedPosts(new Set(likedPosts));
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    }
  }

  async function handleCreateComment(content: string) {
    if (!selectedPost) {
      setError('请先选择要评论的帖子');
      return;
    }

    setError(null);
    try {
      await createComment(
        {
          postId: selectedPost.id,
          content,
          parentId: replyTo?.commentId ?? null,
          replyToUserId: null, // TODO: 可以从replyTo中获取userId
          replyToUserName: replyTo?.authorName ?? null
        },
        t
      );
      // 发表成功后清除回复状态并重新加载评论列表
      setReplyTo(null);
      await loadComments(selectedPost.id);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    }
  }

  // 处理回复评论
  function handleReplyComment(commentId: number, authorName: string) {
    setReplyTo({ commentId, authorName });
    // 可选：滚动到评论框
    setTimeout(() => {
      const commentForm = document.querySelector('textarea');
      commentForm?.focus();
      commentForm?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 100);
  }

  // 取消回复
  function handleCancelReply() {
    setReplyTo(null);
  }

  // 加载更多子评论
  async function handleLoadMoreChildren(
    parentId: number,
    pageIndex: number,
    pageSize: number
  ): Promise<CommentNode[]> {
    try {
      const result = await getChildComments(parentId, pageIndex, pageSize, t);
      return result.comments;
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      return [];
    }
  }

  // 处理评论点赞
  async function handleCommentLike(commentId: number): Promise<{ isLiked: boolean; likeCount: number }> {
    if (!loggedIn) {
      setError('请先登录后再点赞');
      throw new Error('未登录');
    }

    setError(null);
    try {
      const result = await toggleCommentLike(commentId, t);
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      throw err;
    }
  }

  // 编辑帖子
  function handleEditPost(postId: number) {
    if (!selectedPost || selectedPost.id !== postId) {
      setError('请先选择要编辑的帖子');
      return;
    }
    setIsEditModalOpen(true);
  }

  // 保存编辑的帖子
  async function handleSaveEdit(postId: number, title: string, content: string) {
    setError(null);
    try {
      await updatePost(
        {
          postId,
          title,
          content
        },
        t
      );
      // 编辑成功后重新加载帖子详情和帖子列表
      await Promise.all([loadPostDetail(postId), loadPosts()]);
      setIsEditModalOpen(false);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      throw new Error(message);
    }
  }

  // 删除帖子
  function handleDeletePost(postId: number) {
    setPostToDelete(postId);
    setIsDeleteDialogOpen(true);
  }

  // 确认删除帖子
  async function confirmDeletePost() {
    if (!postToDelete) return;

    setError(null);
    try {
      await deletePost(postToDelete, t);
      // 删除成功后关闭对话框，清空选中的帖子，重新加载帖子列表
      setIsDeleteDialogOpen(false);
      setPostToDelete(null);
      setSelectedPost(null);
      setComments([]);
      await loadPosts();
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      setIsDeleteDialogOpen(false);
    }
  }

  // 取消删除帖子
  function cancelDeletePost() {
    setIsDeleteDialogOpen(false);
    setPostToDelete(null);
  }

  // 编辑评论
  async function handleEditComment(commentId: number, newContent: string): Promise<void> {
    if (!selectedPost) {
      setError('请先选择帖子');
      throw new Error('未选择帖子');
    }

    setError(null);
    try {
      await updateComment(
        {
          commentId,
          content: newContent
        },
        t
      );
      // 编辑成功后重新加载评论列表
      await loadComments(selectedPost.id);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      throw err;
    }
  }

  // 删除评论
  function handleDeleteComment(commentId: number) {
    setCommentToDelete(commentId);
    setIsDeleteCommentDialogOpen(true);
  }

  // 确认删除评论
  async function confirmDeleteComment() {
    if (!commentToDelete || !selectedPost) return;

    setError(null);
    try {
      await deleteComment(commentToDelete, t);
      // 删除成功后关闭对话框，重新加载评论列表
      setIsDeleteCommentDialogOpen(false);
      setCommentToDelete(null);
      await loadComments(selectedPost.id);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      setIsDeleteCommentDialogOpen(false);
    }
  }

  // 取消删除评论
  function cancelDeleteComment() {
    setIsDeleteCommentDialogOpen(false);
    setCommentToDelete(null);
  }

  return (
    <div className={styles.container}>
      {/* 左栏：分类和标签 */}
      <div className={styles.leftColumn}>
        <div className={styles.categorySection}>
          <CategoryList
            categories={categories}
            selectedCategoryId={selectedCategoryId}
            onSelectCategory={setSelectedCategoryId}
            loading={loadingCategories}
          />
        </div>
        {/* TODO: 标签区域 */}
        <div className={styles.tagsSection}>
          {/* 未来添加标签列表 */}
        </div>
      </div>

      {/* 中间栏：帖子瀑布流 */}
      <div className={styles.middleColumn}>
        {/* 工具栏 */}
        <div className={styles.toolbar}>
          <div className={styles.toolbarLeft}>
            <h2 className={styles.toolbarTitle}>
              {categories.find(c => c.id === selectedCategoryId)?.name || '全部帖子'}
            </h2>
            <div className={styles.sortButtons}>
              <button
                className={`${styles.sortButton} ${sortBy === 'newest' ? styles.sortActive : ''}`}
                onClick={() => handleSortChange('newest')}
              >
                最新
              </button>
              <button
                className={`${styles.sortButton} ${sortBy === 'hottest' ? styles.sortActive : ''}`}
                onClick={() => handleSortChange('hottest')}
              >
                最热
              </button>
              <button
                className={`${styles.sortButton} ${sortBy === 'essence' ? styles.sortActive : ''}`}
                onClick={() => handleSortChange('essence')}
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
              onChange={(e) => handleSearchChange(e.target.value)}
            />
            {searchKeyword && (
              <button
                className={styles.clearButton}
                onClick={() => handleSearchChange('')}
                aria-label="清除搜索"
              >
                ×
              </button>
            )}
          </div>

          <button
            className={styles.publishButton}
            onClick={() => setIsPublishModalOpen(true)}
            disabled={!loggedIn}
          >
            发帖
          </button>
        </div>

        {/* 帖子瀑布流 */}
        <div className={styles.postsFeed}>
          {loadingPosts ? (
            <p className={styles.loadingText}>加载中...</p>
          ) : posts.length === 0 ? (
            <p className={styles.emptyText}>暂无帖子</p>
          ) : (
            posts.map((post) => {
              const godComment = postGodComments.get(post.id);
              return (
                <PostCard
                  key={post.id}
                  post={post}
                  onClick={() => handleSelectPost(post.id)}
                  godComment={
                    godComment
                      ? {
                          content: godComment.contentSnapshot || '',
                          authorName: godComment.authorName,
                          likeCount: godComment.likeCount
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
              onClick={() => handlePageChange(currentPage - 1)}
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
                  onClick={() => handlePageChange(pageNum)}
                >
                  {pageNum}
                </button>
              );
            })}
            <button
              className={styles.paginationButton}
              onClick={() => handlePageChange(currentPage + 1)}
              disabled={currentPage === totalPages}
            >
              ›
            </button>
          </div>
        )}
      </div>

      {/* 右栏：热门帖子和神评 */}
      <div className={styles.rightColumn}>
        <TrendingSidebar
          hotPosts={hotPosts}
          godComments={trendingGodComments}
          onPostClick={handleSelectPost}
          loading={loadingTrending}
        />
      </div>

      {/* 发帖 Modal */}
      <PublishPostModal
        isOpen={isPublishModalOpen}
        isAuthenticated={loggedIn}
        onClose={() => setIsPublishModalOpen(false)}
        onPublish={handlePublishPost}
      />

      {/* 帖子详情 Modal - TODO: 实现详情视图 */}
      {selectedPost && (
        <div style={{ display: 'none' }}>
          {/* 暂时隐藏，后续实现为 Modal 或独立页面 */}
          <PostDetailView
            post={selectedPost}
            loading={loadingPostDetail}
            isLiked={selectedPost ? likedPosts.has(selectedPost.id) : false}
            onLike={handleLikePost}
            isAuthenticated={loggedIn}
            currentUserId={userId ?? 0}
            onEdit={handleEditPost}
            onDelete={handleDeletePost}
          />
          <CommentTree
            comments={comments}
            loading={loadingComments}
            hasPost={selectedPost !== null}
            currentUserId={userId ?? 0}
            pageSize={5}
            sortBy={commentSortBy}
            onSortChange={handleCommentSortChange}
            onDeleteComment={handleDeleteComment}
            onEditComment={handleEditComment}
            onLikeComment={handleCommentLike}
            onReplyComment={handleReplyComment}
            onLoadMoreChildren={handleLoadMoreChildren}
          />
          <CreateCommentForm
            isAuthenticated={loggedIn}
            hasPost={selectedPost !== null}
            onSubmit={handleCreateComment}
            replyTo={replyTo}
            onCancelReply={handleCancelReply}
          />
        </div>
      )}

      {/* 编辑帖子 Modal */}
      <EditPostModal
        isOpen={isEditModalOpen}
        post={selectedPost}
        onClose={() => setIsEditModalOpen(false)}
        onSave={handleSaveEdit}
      />

      {/* 删除确认对话框 */}
      <ConfirmDialog
        isOpen={isDeleteDialogOpen}
        title="确认删除"
        message="确定要删除这篇帖子吗？删除后无法恢复。"
        confirmText="删除"
        cancelText="取消"
        danger={true}
        onConfirm={confirmDeletePost}
        onCancel={cancelDeletePost}
      />

      {/* 删除评论确认对话框 */}
      <ConfirmDialog
        isOpen={isDeleteCommentDialogOpen}
        title="确认删除"
        message="确定要删除这条评论吗？删除后无法恢复。"
        confirmText="删除"
        cancelText="取消"
        danger={true}
        onConfirm={confirmDeleteComment}
        onCancel={cancelDeleteComment}
      />

      {/* 错误提示 */}
      {error && <p className={styles.errorText}>错误：{error}</p>}
    </div>
  );
};
