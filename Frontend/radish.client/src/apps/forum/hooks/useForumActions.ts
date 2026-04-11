import { useState, useCallback } from 'react';
import type { Dispatch, SetStateAction } from 'react';
import { useUserStore } from '@/stores/userStore';
import type { TFunction } from 'i18next';
import { toast } from '@radish/ui/toast';
import {
  publishPost,
  votePoll,
  closePoll,
  drawLottery,
  getLotteryByPostId,
  answerQuestion,
  acceptQuestionAnswer,
  createComment,
  createPostQuickReply,
  likePost,
  toggleCommentLike,
  updatePost,
  setPostTop,
  updateComment,
  deletePost,
  deleteComment,
  deletePostQuickReply,
  getChildComments,
  getPostEditHistory,
  getCommentEditHistory,
  type CommentNode,
  type CommentReplyTarget,
  type PostDetail,
  type PostQuickReply,
  type CreatePollRequest,
  type CreateLotteryRequest,
  type PostQuestion,
  type PostLottery,
  type PostEditHistory,
  type CommentEditHistory,
  type ForumPostSortBy,
  type QuestionAnswerSort,
  type QuestionAnswerFilter
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
  replyTo: CommentReplyTarget | null;

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
  handleSelectPost: (postId: string | number) => Promise<void>;
  handlePublishPost: (
    title: string,
    content: string,
    categoryId: number,
    tagNames: string[],
    isQuestion?: boolean,
    poll?: CreatePollRequest | null,
    lottery?: CreateLotteryRequest | null
  ) => Promise<void>;
  handleDrawLottery: () => Promise<void>;
  handleVotePoll: (optionId: number) => Promise<void>;
  handleClosePoll: () => Promise<void>;
  handleAnswerQuestion: (content: string) => Promise<void>;
  handleAcceptAnswer: (answerId: number) => Promise<void>;
  handleQuestionAnswerSortChange: (sortBy: QuestionAnswerSort) => Promise<void>;
  handleQuestionAnswerFilterChange: (filterBy: QuestionAnswerFilter) => void;
  handleLikePost: (postId: number) => Promise<void>;
  handleEditPost: (postId: number) => void;
  handleViewPostHistory: (postId: number) => Promise<void>;
  handleSaveEdit: (postId: number, title: string, content: string, categoryId: number, tagNames: string[]) => Promise<void>;
  handleTogglePostTop: (isTop: boolean) => Promise<void>;
  handleDeletePost: (postId: number) => void;
  confirmDeletePost: () => Promise<void>;
  cancelDeletePost: () => void;

  // 评论操作
  handleCreateQuickReply: (content: string) => Promise<void>;
  handleDeleteQuickReply: (quickReplyId: number) => Promise<void>;
  handleCreateComment: (content: string) => Promise<void>;
  handleReplyComment: (target: CommentReplyTarget) => void;
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
  handleSortChange: (sortBy: ForumPostSortBy) => void;
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
  loadedCommentPages: number;
  questionAnswerSort: QuestionAnswerSort;
  selectedCategoryId: number | null;
  selectedTagName: string | null;
  selectedPost: PostDetail | null;
  setSelectedPost: Dispatch<SetStateAction<PostDetail | null>>;
  setComments: Dispatch<SetStateAction<CommentNode[]>>;
  setQuickReplies: Dispatch<SetStateAction<PostQuickReply[]>>;
  setQuickReplyTotal: Dispatch<SetStateAction<number>>;
  setCommentTotal: Dispatch<SetStateAction<number>>;
  setCurrentPage: (page: number) => void;
  setSortBy: (sortBy: ForumPostSortBy) => void;
  setCommentSortBy: (sortBy: 'newest' | 'hottest' | null) => void;
  setQuestionAnswerSort: (sortBy: QuestionAnswerSort) => void;
  setQuestionAnswerFilter: (filterBy: QuestionAnswerFilter) => void;
  setSearchKeyword: (keyword: string) => void;
  setError: (error: string | null) => void;
  loadPostDetail: (postId: string | number, answerSortOverride?: QuestionAnswerSort) => Promise<void>;
  loadComments: (postId: string | number, pageCount?: number) => Promise<void>;
  loadQuickReplies: (postId: string | number) => Promise<void>;
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
    loadedCommentPages,
    questionAnswerSort,
    selectedPost,
    setSelectedPost,
    setComments,
    setQuickReplies,
    setQuickReplyTotal,
    setCommentTotal,
    setCurrentPage,
    setSortBy,
    setCommentSortBy,
    setQuestionAnswerSort,
    setQuestionAnswerFilter,
    setSearchKeyword,
    setError,
    loadPostDetail,
    loadComments,
    loadQuickReplies,
    loadPosts,
    resetCommentSort,
    commentSortBy
  } = params;

  const getLoadedCommentPageCount = () => Math.max(loadedCommentPages, 1);

  // Modal 状态
  const [isPublishModalOpen, setIsPublishModalOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [postToDelete, setPostToDelete] = useState<number | null>(null);
  const [isDeleteCommentDialogOpen, setIsDeleteCommentDialogOpen] = useState(false);
  const [commentToDelete, setCommentToDelete] = useState<number | null>(null);

  // 回复状态
  const [replyTo, setReplyTo] = useState<CommentReplyTarget | null>(null);

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

  const normalizeTagNames = (tagNames: string[]): string[] => {
    const normalized: string[] = [];
    const seen = new Set<string>();

    for (const tag of tagNames) {
      const trimmed = tag.trim();
      if (!trimmed) {
        continue;
      }

      const lower = trimmed.toLowerCase();
      if (seen.has(lower)) {
        continue;
      }

      seen.add(lower);
      normalized.push(trimmed);
    }

    return normalized;
  };

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
  const handleSelectPost = async (postId: string | number) => {
    resetCommentSort();
    setQuestionAnswerSort('default');
    setQuestionAnswerFilter('all');
    await Promise.all([
      loadPostDetail(postId, 'default'),
      loadComments(postId, 1),
      loadQuickReplies(postId)
    ]);
  };

  const handleQuestionAnswerSortChange = async (sortBy: QuestionAnswerSort) => {
    if (!selectedPost?.voId) {
      return;
    }

    setQuestionAnswerSort(sortBy);
    await loadPostDetail(selectedPost.voId, sortBy);
  };

  const handleQuestionAnswerFilterChange = (filterBy: QuestionAnswerFilter) => {
    setQuestionAnswerFilter(filterBy);
  };

  // 发布帖子
  const handlePublishPost = async (
    title: string,
    content: string,
    categoryId: number,
    tagNames: string[],
    isQuestion?: boolean,
    poll?: CreatePollRequest | null,
    lottery?: CreateLotteryRequest | null
  ) => {
    if (categoryId <= 0) {
      const message = '请先选择分类';
      setError(message);
      throw new Error(message);
    }

    const normalizedTagNames = normalizeTagNames(tagNames);
    if (normalizedTagNames.length < 1) {
      const message = '发布帖子时至少需要 1 个标签';
      setError(message);
      throw new Error(message);
    }

    if (normalizedTagNames.length > 5) {
      const message = '发布帖子时最多只能选择 5 个标签';
      setError(message);
      throw new Error(message);
    }

    setError(null);
    try {
      const postId = await publishPost(
        {
          title,
          content,
          categoryId,
          tagNames: normalizedTagNames,
          isQuestion: Boolean(isQuestion),
          poll: poll ?? undefined,
          lottery: lottery ?? undefined
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

  const applyLotteryState = useCallback((lottery: PostLottery) => {
    setSelectedPost((current) =>
      current && current.voId === lottery.voPostId
        ? {
            ...current,
            voHasLottery: true,
            voLotteryParticipantCount: lottery.voParticipantCount,
            voLotteryIsDrawn: lottery.voIsDrawn,
            voLottery: lottery
          }
        : current
    );
  }, [setSelectedPost]);

  const applyPollState = useCallback((poll: PostDetail['voPoll']) => {
    if (!poll) {
      return;
    }

    setSelectedPost((current) =>
      current && current.voId === poll.voPostId
        ? {
            ...current,
            voHasPoll: true,
            voPollTotalVoteCount: poll.voTotalVoteCount,
            voPollIsClosed: poll.voIsClosed,
            voPoll: poll
          }
        : current
    );
  }, [setSelectedPost]);

  const handleDrawLottery = async () => {
    if (!selectedPost?.voId) {
      const message = '请先选择要开奖的帖子';
      setError(message);
      throw new Error(message);
    }

    if (!isAuthenticated) {
      const message = '请先登录后再开奖';
      setError(message);
      throw new Error(message);
    }

    setError(null);
    try {
      const latestLottery = await drawLottery(selectedPost.voId, t);
      applyLotteryState(latestLottery);
      toast.success('开奖完成');
      await loadPosts();
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      toast.error(message || '开奖失败');
      throw err;
    }
  };

  const handleVotePoll = async (optionId: number) => {
    if (!selectedPost?.voId) {
      const message = '请先选择要投票的帖子';
      setError(message);
      throw new Error(message);
    }

    if (!isAuthenticated) {
      const message = '请先登录后再投票';
      setError(message);
      throw new Error(message);
    }

    setError(null);
    try {
      const latestPoll = await votePoll(
        {
          postId: selectedPost.voId,
          optionId
        },
        t
      );

      applyPollState(latestPoll);
      await loadPosts();
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      throw err;
    }
  };

  const handleClosePoll = async () => {
    if (!selectedPost?.voId) {
      const message = '请先选择要结束投票的帖子';
      setError(message);
      throw new Error(message);
    }

    if (!isAuthenticated) {
      const message = '请先登录后再结束投票';
      setError(message);
      throw new Error(message);
    }

    setError(null);
    try {
      const latestPoll = await closePoll(
        {
          postId: selectedPost.voId
        },
        t
      );

      applyPollState(latestPoll);
      toast.success('投票已结束');
      await loadPosts();
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      toast.error(message || '结束投票失败');
      throw err;
    }
  };

  const handleAnswerQuestion = async (content: string) => {
    if (!selectedPost?.voId) {
      const message = '请先选择要回答的帖子';
      setError(message);
      throw new Error(message);
    }

    if (!isAuthenticated) {
      const message = '请先登录后再回答';
      setError(message);
      throw new Error(message);
    }

    const trimmedContent = content.trim();
    if (!trimmedContent) {
      const message = '回答内容不能为空';
      setError(message);
      throw new Error(message);
    }

    setError(null);
    try {
      await answerQuestion(
        {
          postId: selectedPost.voId,
          content: trimmedContent
        },
        t
      );

      toast.success('回答已发布');
      await Promise.all([
        loadPostDetail(selectedPost.voId, questionAnswerSort),
        loadPosts()
      ]);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      toast.error(message || '提交回答失败');
      throw err;
    }
  };

  const handleAcceptAnswer = async (answerId: number) => {
    if (!selectedPost?.voId) {
      const message = '请先选择要采纳的帖子';
      setError(message);
      throw new Error(message);
    }

    if (!isAuthenticated) {
      const message = '请先登录后再采纳';
      setError(message);
      throw new Error(message);
    }

    setError(null);
    try {
      await acceptQuestionAnswer(
        {
          postId: selectedPost.voId,
          answerId
        },
        t
      );

      toast.success('答案已采纳');
      await Promise.all([
        loadPostDetail(selectedPost.voId, questionAnswerSort),
        loadPosts()
      ]);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      toast.error(message || '采纳回答失败');
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
    if (selectedPost?.voId === postId) {
      setError(null);
      setIsEditModalOpen(true);
      return;
    }

    setError(null);
    void loadPostDetail(postId)
      .then(() => {
        setIsEditModalOpen(true);
      })
      .catch((err) => {
        const message = err instanceof Error ? err.message : String(err);
        setError(message);
      });
  };

  const handleViewPostHistory = async (postId: number) => {
    setActivePostHistoryPostId(postId);
    setIsPostHistoryOpen(true);
    await loadPostHistory(postId, 1);
  };

  // 保存编辑
  const handleSaveEdit = async (postId: number, title: string, content: string, categoryId: number, tagNames: string[]) => {
    setError(null);
    try {
      await updatePost({ postId, title, content, categoryId, tagNames }, t);
      await Promise.all([loadPostDetail(postId), loadPosts()]);
      setIsEditModalOpen(false);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      throw new Error(message);
    }
  };

  const handleTogglePostTop = async (isTop: boolean) => {
    if (!selectedPost?.voId) {
      const message = '请先选择要操作的帖子';
      setError(message);
      throw new Error(message);
    }

    setError(null);
    try {
      await setPostTop(
        {
          postId: selectedPost.voId,
          isTop
        },
        t
      );
      toast.success(isTop ? '帖子已置顶' : '帖子已取消置顶');
      await Promise.all([loadPostDetail(selectedPost.voId), loadPosts()]);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      toast.error(message || '设置帖子置顶状态失败');
      throw err;
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
      setQuickReplies([]);
      setQuickReplyTotal(0);
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

  const handleCreateQuickReply = async (content: string) => {
    if (!selectedPost?.voId) {
      setError('请先选择要回应的帖子');
      throw new Error('未选择帖子');
    }

    if (!isAuthenticated) {
      setError('请先登录后再发布轻回应');
      throw new Error('未登录');
    }

    const normalizedContent = content.trim().replace(/\s+/g, ' ');
    if (!normalizedContent) {
      setError('轻回应内容不能为空');
      throw new Error('内容为空');
    }

    setError(null);
    try {
      const quickReply = await createPostQuickReply(
        {
          postId: selectedPost.voId,
          content: normalizedContent
        },
        t
      );

      setQuickReplies(prev => {
        const next = [quickReply, ...prev.filter(item => item.voId !== quickReply.voId)];
        return next.slice(0, 30);
      });
      setQuickReplyTotal(prev => prev + 1);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      throw err;
    }
  };

  const handleDeleteQuickReply = async (quickReplyId: number) => {
    if (!selectedPost?.voId) {
      setError('请先选择帖子');
      throw new Error('未选择帖子');
    }

    setError(null);
    try {
      await deletePostQuickReply(quickReplyId, t);
      setQuickReplies(prev => prev.filter(item => item.voId !== quickReplyId));
      setQuickReplyTotal(prev => Math.max(0, prev - 1));
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      throw err;
    }
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
          parentId: replyTo?.parentCommentId ?? null,
          replyToCommentId: replyTo?.targetCommentId ?? null,
          replyToCommentSnapshot: replyTo?.contentSnapshot ?? null,
          replyToUserId: null,
          replyToUserName: replyTo?.authorName ?? null
        },
        t
      );

      const userStore = useUserStore.getState();
      const authorName = userStore.userName || '我';
      const authorAvatarUrl = userStore.avatarThumbnailUrl || userStore.avatarUrl || null;
      const now = new Date().toISOString();
      const parentId = replyTo?.parentCommentId ?? null;

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
          if (!isRoot) {
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
        voAuthorAvatarUrl: authorAvatarUrl,
        voParentId: parentId,
        voRootId: parentId,
        voReplyToCommentId: replyTo?.targetCommentId ?? null,
        voReplyToCommentSnapshot: replyTo?.contentSnapshot ?? null,
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
      if (parentId == null && commentSortBy === null) {
        setCommentTotal(prev => prev + 1);
      } else {
        await loadComments(selectedPost.voId, getLoadedCommentPageCount());
      }
      if (selectedPost.voHasLottery && parentId == null) {
        try {
          const latestLottery = await getLotteryByPostId(selectedPost.voId, t);
          if (latestLottery.voLottery) {
            applyLotteryState(latestLottery.voLottery);
          }
        } catch {
          // 评论已提交成功，抽奖摘要刷新失败时避免误报整次评论失败
        }
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    }
  };

  // 回复评论
  const handleReplyComment = (target: CommentReplyTarget) => {
    setReplyTo(target);
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
      await loadComments(selectedPost.voId, getLoadedCommentPageCount());
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
      await loadComments(selectedPost.voId, getLoadedCommentPageCount());
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
  const handleSortChange = (newSortBy: ForumPostSortBy) => {
    setSortBy(newSortBy);
    setCurrentPage(1);
  };

  // 评论排序
  const handleCommentSortChange = (newSortBy: 'newest' | 'hottest') => {
    setCommentSortBy(newSortBy);
    if (selectedPost) {
      void loadComments(selectedPost.voId, 1);
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
    handleDrawLottery,
    handleVotePoll,
    handleClosePoll,
    handleAnswerQuestion,
    handleAcceptAnswer,
    handleQuestionAnswerSortChange,
    handleQuestionAnswerFilterChange,
    handleLikePost,
    handleEditPost,
    handleViewPostHistory,
    handleSaveEdit,
    handleTogglePostTop,
    handleDeletePost,
    confirmDeletePost,
    cancelDeletePost,
    handleCreateQuickReply,
    handleDeleteQuickReply,
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
