import { useState, useCallback } from 'react';
import type { Dispatch, SetStateAction } from 'react';
import { useUserStore } from '@/stores/userStore';
import type { TFunction } from 'i18next';
import {
  publishPost,
  createComment,
  likePost,
  toggleCommentLike,
  updatePost,
  updateComment,
  deletePost,
  deleteComment,
  getChildComments,
  getPostEditHistory,
  getCommentEditHistory,
  type CommentNode,
  type PostDetail,
  type PostEditHistory,
  type CommentEditHistory
} from '@/api/forum';

export interface ForumActionsState {
  // Modal 状态
  isPublishModalOpen: boolean;
  isEditModalOpen: boolean;
  isDeleteDialogOpen: boolean;
  postToDelete: number | null;
  isDeleteCommentDialogOpen: boolean;
  commentToDelete: number | null;

  // 回复状态
  replyTo: { commentId: number; authorName: string } | null;

  // 点赞状态
  likedPosts: Set<number>;

  // 历史弹窗状态
  isPostHistoryOpen: boolean;
  isCommentHistoryOpen: boolean;
  postHistories: PostEditHistory[];
  commentHistories: CommentEditHistory[];
  postHistoryTotal: number;
  commentHistoryTotal: number;
  postHistoryPageIndex: number;
  commentHistoryPageIndex: number;
  postHistoryLoading: boolean;
  commentHistoryLoading: boolean;
  postHistoryError: string | null;
  commentHistoryError: string | null;
}

export interface ForumActionsHandlers {
  // Modal 控制
  setIsPublishModalOpen: (open: boolean) => void;
  setIsEditModalOpen: (open: boolean) => void;

  // 帖子操作
  handleSelectPost: (postId: number) => Promise<void>;
  handlePublishPost: (title: string, content: string, tagNames: string[]) => Promise<void>;
  handleLikePost: (postId: number) => Promise<void>;
  handleEditPost: (postId: number) => void;
  handleViewPostHistory: (postId: number) => Promise<void>;
  handleSaveEdit: (postId: number, title: string, content: string, tagNames: string[]) => Promise<void>;
  handleDeletePost: (postId: number) => void;
  confirmDeletePost: () => Promise<void>;
  cancelDeletePost: () => void;

  // 评论操作
  handleCreateComment: (content: string) => Promise<void>;
  handleReplyComment: (commentId: number, authorName: string) => void;
  handleCancelReply: () => void;
  handleCommentLike: (commentId: number) => Promise<{ isLiked: boolean; likeCount: number }>;
  handleEditComment: (commentId: number, newContent: string) => Promise<void>;
  handleViewCommentHistory: (commentId: number) => Promise<void>;
  handleDeleteComment: (commentId: number) => void;
  confirmDeleteComment: () => Promise<void>;
  cancelDeleteComment: () => void;
  handleLoadMoreChildren: (
    parentId: number,
    pageIndex: number,
    pageSize: number
  ) => Promise<CommentNode[]>;

  // 排序和分页
  handlePageChange: (page: number) => void;
  handleSortChange: (sortBy: 'newest' | 'hottest' | 'essence') => void;
  handleCommentSortChange: (sortBy: 'newest' | 'hottest') => void;
  handleSearchChange: (keyword: string) => void;

  // 历史分页与关闭
  handlePostHistoryPageChange: (pageIndex: number) => Promise<void>;
  handleCommentHistoryPageChange: (pageIndex: number) => Promise<void>;
  closePostHistory: () => void;
  closeCommentHistory: () => void;
}

interface UseForumActionsParams {
  t: TFunction;
  isAuthenticated: boolean;
  userId: number;
  commentSortBy: 'newest' | 'hottest' | null;
  selectedCategoryId: number | null;
  selectedTagName: string | null;
  selectedPost: PostDetail | null;
  setSelectedPost: Dispatch<SetStateAction<PostDetail | null>>;
  setComments: Dispatch<SetStateAction<CommentNode[]>>;
  setCurrentPage: (page: number) => void;
  setSortBy: (sortBy: 'newest' | 'hottest' | 'essence') => void;
  setCommentSortBy: (sortBy: 'newest' | 'hottest' | null) => void;
  setSearchKeyword: (keyword: string) => void;
  setError: (error: string | null) => void;
  loadPostDetail: (postId: number) => Promise<void>;
  loadComments: (postId: number) => Promise<void>;
  loadPosts: () => Promise<void>;
  resetCommentSort: () => void;
}

export const useForumActions = (
  params: UseForumActionsParams
): ForumActionsState & ForumActionsHandlers => {
  const {
    t,
    isAuthenticated,
    userId,
    selectedCategoryId,
    selectedTagName,
    selectedPost,
    setSelectedPost,
    setComments,
    setCurrentPage,
    setSortBy,
    setCommentSortBy,
    setSearchKeyword,
    setError,
    loadPostDetail,
    loadComments,
    loadPosts,
    resetCommentSort,
    commentSortBy
  } = params;

  // Modal 状态
  const [isPublishModalOpen, setIsPublishModalOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [postToDelete, setPostToDelete] = useState<number | null>(null);
  const [isDeleteCommentDialogOpen, setIsDeleteCommentDialogOpen] = useState(false);
  const [commentToDelete, setCommentToDelete] = useState<number | null>(null);

  // 回复状态
  const [replyTo, setReplyTo] = useState<{ commentId: number; authorName: string } | null>(null);

  // 点赞状态
  const [likedPosts, setLikedPosts] = useState<Set<number>>(() => {
    try {
      const stored = localStorage.getItem('forum_liked_posts');
      return stored ? new Set(JSON.parse(stored)) : new Set();
    } catch {
      return new Set();
    }
  });

  const [isPostHistoryOpen, setIsPostHistoryOpen] = useState(false);
  const [isCommentHistoryOpen, setIsCommentHistoryOpen] = useState(false);
  const [postHistories, setPostHistories] = useState<PostEditHistory[]>([]);
  const [commentHistories, setCommentHistories] = useState<CommentEditHistory[]>([]);
  const [postHistoryTotal, setPostHistoryTotal] = useState(0);
  const [commentHistoryTotal, setCommentHistoryTotal] = useState(0);
  const [postHistoryPageIndex, setPostHistoryPageIndex] = useState(1);
  const [commentHistoryPageIndex, setCommentHistoryPageIndex] = useState(1);
  const [postHistoryLoading, setPostHistoryLoading] = useState(false);
  const [commentHistoryLoading, setCommentHistoryLoading] = useState(false);
  const [postHistoryError, setPostHistoryError] = useState<string | null>(null);
  const [commentHistoryError, setCommentHistoryError] = useState<string | null>(null);

  const postHistoryPageSize = 10;
  const commentHistoryPageSize = 10;
  const [activePostHistoryPostId, setActivePostHistoryPostId] = useState<number | null>(null);
  const [activeCommentHistoryCommentId, setActiveCommentHistoryCommentId] = useState<number | null>(null);

  const loadPostHistory = async (postId: number, pageIndex: number) => {
    setPostHistoryLoading(true);
    setPostHistoryError(null);
    try {
      const data = await getPostEditHistory(postId, pageIndex, postHistoryPageSize, t);
      setPostHistories(data.voItems || []);
      setPostHistoryTotal(data.voTotal || 0);
      setPostHistoryPageIndex(data.voPageIndex || pageIndex);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setPostHistoryError(message);
    } finally {
      setPostHistoryLoading(false);
    }
  };

  const loadCommentHistory = async (commentId: number, pageIndex: number) => {
    setCommentHistoryLoading(true);
    setCommentHistoryError(null);
    try {
      const data = await getCommentEditHistory(commentId, pageIndex, commentHistoryPageSize, t);
      setCommentHistories(data.voItems || []);
      setCommentHistoryTotal(data.voTotal || 0);
      setCommentHistoryPageIndex(data.voPageIndex || pageIndex);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setCommentHistoryError(message);
    } finally {
      setCommentHistoryLoading(false);
    }
  };

  // 选择帖子
  const handleSelectPost = async (postId: number) => {
    resetCommentSort();
    await loadPostDetail(postId);
    await loadComments(postId);
  };

  // 发布帖子
  const handlePublishPost = async (title: string, content: string, tagNames: string[]) => {
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
          tagNames
        },
        t
      );
      setIsPublishModalOpen(false);
      setCurrentPage(1);
      await loadPosts();
      await handleSelectPost(postId);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      throw err;
    }
  };

  // 点赞帖子
  const handleLikePost = async (postId: number) => {
    if (!isAuthenticated) {
      setError('请先登录后再点赞');
      return;
    }

    setError(null);
    const previousLikedPosts = likedPosts;
    try {
      // 乐观更新：先切换高亮状态
      const optimisticLikedPosts = new Set(previousLikedPosts);
      if (optimisticLikedPosts.has(postId)) {
        optimisticLikedPosts.delete(postId);
      } else {
        optimisticLikedPosts.add(postId);
      }
      setLikedPosts(optimisticLikedPosts);
      localStorage.setItem('forum_liked_posts', JSON.stringify([...optimisticLikedPosts]));

      // 以服务端返回为准，修正本地状态与点赞数
      const result = await likePost(postId, t);
      const reconciledLikedPosts = new Set(optimisticLikedPosts);
      if (result.voIsLiked) {
        reconciledLikedPosts.add(postId);
      } else {
        reconciledLikedPosts.delete(postId);
      }
      setLikedPosts(reconciledLikedPosts);
      localStorage.setItem('forum_liked_posts', JSON.stringify([...reconciledLikedPosts]));

      setSelectedPost((current) =>
        current && current.voId === postId
          ? { ...current, voLikeCount: result.voLikeCount }
          : current
      );
    } catch (err) {
      // 失败时回滚
      setLikedPosts(previousLikedPosts);
      localStorage.setItem('forum_liked_posts', JSON.stringify([...previousLikedPosts]));
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    }
  };

  // 编辑帖子
  const handleEditPost = (postId: number) => {
    if (!selectedPost || selectedPost.voId !== postId) {
      setError('请先选择要编辑的帖子');
      return;
    }
    setIsEditModalOpen(true);
  };

  const handleViewPostHistory = async (postId: number) => {
    setActivePostHistoryPostId(postId);
    setIsPostHistoryOpen(true);
    await loadPostHistory(postId, 1);
  };

  // 保存编辑
  const handleSaveEdit = async (postId: number, title: string, content: string, tagNames: string[]) => {
    setError(null);
    try {
      await updatePost({ postId, title, content, tagNames }, t);
      await Promise.all([loadPostDetail(postId), loadPosts()]);
      setIsEditModalOpen(false);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      throw new Error(message);
    }
  };

  // 删除帖子
  const handleDeletePost = (postId: number) => {
    setPostToDelete(postId);
    setIsDeleteDialogOpen(true);
  };

  const confirmDeletePost = async () => {
    if (!postToDelete) return;

    setError(null);
    try {
      await deletePost(postToDelete, t);
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
  };

  const cancelDeletePost = () => {
    setIsDeleteDialogOpen(false);
    setPostToDelete(null);
  };

  // 创建评论
  const handleCreateComment = async (content: string) => {
    if (!selectedPost) {
      setError('请先选择要评论的帖子');
      return;
    }

    setError(null);
    try {
      const commentId = await createComment(
        {
          postId: selectedPost.voId,
          content,
          parentId: replyTo?.commentId ?? null,
          replyToUserId: null,
          replyToUserName: replyTo?.authorName ?? null
        },
        t
      );

      const userStore = useUserStore.getState();
      const authorName = userStore.userName || '我';
      const now = new Date().toISOString();
      const parentId = replyTo?.commentId ?? null;

      const isSameId = (a: number | string | null | undefined, b: number | string | null | undefined) => {
        if (a == null || b == null) return false;
        return String(a) === String(b);
      };

      const mergeCommentIntoTree = (
        list: CommentNode[],
        targetParentId: number | string | null,
        comment: CommentNode
      ): CommentNode[] => {
        if (!targetParentId) {
          const exists = list.some(item => isSameId(item.voId, comment.voId));
          if (exists) return list;
          return commentSortBy === 'newest' ? [comment, ...list] : [...list, comment];
        }

        let inserted = false;
        const next = list.map(root => {
          const isRoot = isSameId(root.voId, targetParentId);
          const isChild = root.voChildren?.some(child => isSameId(child.voId, targetParentId));
          if (!isRoot && !isChild) {
            return root;
          }

          if (root.voChildren?.some(child => isSameId(child.voId, comment.voId))) {
            inserted = true;
            return root;
          }

          inserted = true;
          const childComment = { ...comment, voRootId: root.voId };
          const nextChildren = root.voChildren ? [...root.voChildren, childComment] : [childComment];
          const nextTotal = (root.voChildrenTotal ?? root.voChildren?.length ?? 0) + 1;

          return {
            ...root,
            voChildren: nextChildren,
            voChildrenTotal: nextTotal
          };
        });

        return inserted ? next : list;
      };

      const newComment: CommentNode = {
        voId: commentId,
        voPostId: selectedPost.voId,
        voContent: content.trim(),
        voAuthorId: userId,
        voAuthorName: authorName,
        voParentId: parentId,
        voRootId: parentId,
        voReplyToUserId: null,
        voReplyToUserName: replyTo?.authorName ?? null,
        voLevel: parentId ? 1 : 0,
        voLikeCount: 0,
        voIsLiked: false,
        voCreateTime: now,
        voChildren: [],
        voChildrenTotal: 0,
        voIsGodComment: false,
        voIsSofa: false
      };

      setComments(prev => mergeCommentIntoTree(prev, parentId, newComment));

      setReplyTo(null);
      await loadComments(selectedPost.voId);

      setComments(prev => mergeCommentIntoTree(prev, parentId, newComment));
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    }
  };

  // 回复评论
  const handleReplyComment = (commentId: number, authorName: string) => {
    setReplyTo({ commentId, authorName });
    setTimeout(() => {
      const commentForm = document.querySelector('textarea');
      commentForm?.focus();
      commentForm?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 100);
  };

  const handleCancelReply = () => {
    setReplyTo(null);
  };

  // 点赞评论
  const handleCommentLike = async (
    commentId: number
  ): Promise<{ isLiked: boolean; likeCount: number }> => {
    if (!isAuthenticated) {
      setError('请先登录后再点赞');
      throw new Error('未登录');
    }

    setError(null);
    try {
      const result = await toggleCommentLike(commentId, t);
      // 直接返回后端字段，不做映射
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      throw err;
    }
  };

  // 编辑评论
  const handleEditComment = async (commentId: number, newContent: string): Promise<void> => {
    if (!selectedPost) {
      setError('请先选择帖子');
      throw new Error('未选择帖子');
    }

    setError(null);
    try {
      await updateComment({ commentId, content: newContent }, t);
      await loadComments(selectedPost.voId);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      throw err;
    }
  };

  const handleViewCommentHistory = async (commentId: number): Promise<void> => {
    setActiveCommentHistoryCommentId(commentId);
    setIsCommentHistoryOpen(true);
    await loadCommentHistory(commentId, 1);
  };

  // 删除评论
  const handleDeleteComment = (commentId: number) => {
    setCommentToDelete(commentId);
    setIsDeleteCommentDialogOpen(true);
  };

  const confirmDeleteComment = async () => {
    if (!commentToDelete || !selectedPost) return;

    setError(null);
    try {
      await deleteComment(commentToDelete, t);
      setIsDeleteCommentDialogOpen(false);
      setCommentToDelete(null);
      await loadComments(selectedPost.voId);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      setIsDeleteCommentDialogOpen(false);
    }
  };

  const cancelDeleteComment = () => {
    setIsDeleteCommentDialogOpen(false);
    setCommentToDelete(null);
  };

  // 加载更多子评论
  const handleLoadMoreChildren = async (
    parentId: number,
    pageIndex: number,
    pageSize: number
  ): Promise<CommentNode[]> => {
    try {
      const result = await getChildComments(parentId, pageIndex, pageSize, t);
      return result.voItems ?? [];
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      return [];
    }
  };

  // 分页
  const handlePageChange = (page: number) => {
    setCurrentPage(page);
  };

  // 排序
  const handleSortChange = (newSortBy: 'newest' | 'hottest' | 'essence') => {
    setSortBy(newSortBy);
    setCurrentPage(1);
  };

  // 评论排序
  const handleCommentSortChange = (newSortBy: 'newest' | 'hottest') => {
    setCommentSortBy(newSortBy);
    if (selectedPost) {
      void loadComments(selectedPost.voId);
    }
  };

  // 搜索
  const handleSearchChange = useCallback(
    (keyword: string) => {
      setSearchKeyword(keyword);
      setCurrentPage(1);
    },
    [setSearchKeyword, setCurrentPage]
  );

  const handlePostHistoryPageChange = async (pageIndex: number) => {
    if (!activePostHistoryPostId) {
      return;
    }
    await loadPostHistory(activePostHistoryPostId, pageIndex);
  };

  const handleCommentHistoryPageChange = async (pageIndex: number) => {
    if (!activeCommentHistoryCommentId) {
      return;
    }
    await loadCommentHistory(activeCommentHistoryCommentId, pageIndex);
  };

  const closePostHistory = () => {
    setIsPostHistoryOpen(false);
    setActivePostHistoryPostId(null);
    setPostHistories([]);
    setPostHistoryTotal(0);
    setPostHistoryPageIndex(1);
    setPostHistoryError(null);
  };

  const closeCommentHistory = () => {
    setIsCommentHistoryOpen(false);
    setActiveCommentHistoryCommentId(null);
    setCommentHistories([]);
    setCommentHistoryTotal(0);
    setCommentHistoryPageIndex(1);
    setCommentHistoryError(null);
  };

  return {
    // 状态
    isPublishModalOpen,
    isEditModalOpen,
    isDeleteDialogOpen,
    postToDelete,
    isDeleteCommentDialogOpen,
    commentToDelete,
    replyTo,
    likedPosts,
    isPostHistoryOpen,
    isCommentHistoryOpen,
    postHistories,
    commentHistories,
    postHistoryTotal,
    commentHistoryTotal,
    postHistoryPageIndex,
    commentHistoryPageIndex,
    postHistoryLoading,
    commentHistoryLoading,
    postHistoryError,
    commentHistoryError,

    // 操作
    setIsPublishModalOpen,
    setIsEditModalOpen,
    handleSelectPost,
    handlePublishPost,
    handleLikePost,
    handleEditPost,
    handleViewPostHistory,
    handleSaveEdit,
    handleDeletePost,
    confirmDeletePost,
    cancelDeletePost,
    handleCreateComment,
    handleReplyComment,
    handleCancelReply,
    handleCommentLike,
    handleEditComment,
    handleViewCommentHistory,
    handleDeleteComment,
    confirmDeleteComment,
    cancelDeleteComment,
    handleLoadMoreChildren,
    handlePageChange,
    handleSortChange,
    handleCommentSortChange,
    handleSearchChange,
    handlePostHistoryPageChange,
    handleCommentHistoryPageChange,
    closePostHistory,
    closeCommentHistory
  };
};
