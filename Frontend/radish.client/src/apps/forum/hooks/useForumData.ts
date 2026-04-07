import { useState, useEffect, useRef } from 'react';
import { log } from '@/utils/logger';
import type { Dispatch, SetStateAction } from 'react';
import type { TFunction } from 'i18next';
import {
  getTopCategories,
  getHotTags,
  getFixedTags,
  getPostList,
  getPostById,
  getRootCommentsPage,
  getPostQuickReplyWall,
  getCurrentGodCommentsBatch,
  type Category,
  type Tag,
  type PostItem,
  type PostDetail,
  type CommentNode,
  type PostQuickReply,
  type CommentHighlight,
  type ForumPostViewMode,
  type QuestionStatusFilter,
  type PollStatusFilter,
  type ForumPostSortBy,
  type QuestionAnswerSort,
  type QuestionAnswerFilter
} from '@/api/forum';

function isAbortError(error: unknown): boolean {
  if (typeof DOMException !== 'undefined' && error instanceof DOMException) {
    return error.name === 'AbortError';
  }
  return error instanceof Error && error.name === 'AbortError';
}

function sortRootComments(
  comments: CommentNode[],
  sortBy: 'newest' | 'hottest' | 'default'
): CommentNode[] {
  return [...comments].sort((left, right) => {
    const topDiff = Number(right.voIsTop ?? false) - Number(left.voIsTop ?? false);
    if (topDiff !== 0) {
      return topDiff;
    }

    const leftTime = new Date(left.voCreateTime || 0).getTime();
    const rightTime = new Date(right.voCreateTime || 0).getTime();

    if (sortBy === 'hottest') {
      const likeDiff = (right.voLikeCount || 0) - (left.voLikeCount || 0);
      if (likeDiff !== 0) {
        return likeDiff;
      }
      return rightTime - leftTime;
    }

    if (sortBy === 'newest') {
      return rightTime - leftTime;
    }

    return leftTime - rightTime;
  });
}

export interface ForumDataState {
  // 数据状态
  categories: Category[];
  fixedTags: Tag[];
  hotTags: Tag[];
  selectedCategoryId: number | null;
  selectedTagName: string | null;
  posts: PostItem[];
  selectedPost: PostDetail | null;
  comments: CommentNode[];
  quickReplies: PostQuickReply[];
  quickReplyTotal: number;
  commentTotal: number;
  commentPageSize: number;
  loadedCommentPages: number;
  hotPosts: PostItem[];
  trendingGodComments: CommentNode[];
  postGodComments: Map<number, CommentHighlight>;

  // 分页状态
  currentPage: number;
  pageSize: number;
  totalPages: number;

  // 排序状态
  sortBy: ForumPostSortBy;
  commentSortBy: 'newest' | 'hottest' | null;
  postViewMode: ForumPostViewMode;
  questionStatus: QuestionStatusFilter;
  pollStatus: PollStatusFilter;
  questionAnswerSort: QuestionAnswerSort;
  questionAnswerFilter: QuestionAnswerFilter;

  // 搜索状态
  searchKeyword: string;

  // 加载状态
  loadingCategories: boolean;
  loadingHotTags: boolean;
  loadingPosts: boolean;
  loadingPostDetail: boolean;
  loadingComments: boolean;
  loadingQuickReplies: boolean;
  loadingMoreComments: boolean;
  loadingTrending: boolean;

  // 错误状态
  error: string | null;
}

export interface ForumDataActions {
  setSelectedCategoryId: (id: number | null) => void;
  setSelectedTagName: (tagName: string | null) => void;
  setSelectedPost: Dispatch<SetStateAction<PostDetail | null>>;
  setComments: Dispatch<SetStateAction<CommentNode[]>>;
  setQuickReplies: Dispatch<SetStateAction<PostQuickReply[]>>;
  setQuickReplyTotal: Dispatch<SetStateAction<number>>;
  setCommentTotal: Dispatch<SetStateAction<number>>;
  setCurrentPage: (page: number) => void;
  setSortBy: (sortBy: ForumPostSortBy) => void;
  setCommentSortBy: (sortBy: 'newest' | 'hottest' | null) => void;
  setPostViewMode: (mode: ForumPostViewMode) => void;
  setQuestionStatus: (status: QuestionStatusFilter) => void;
  setPollStatus: (status: PollStatusFilter) => void;
  setQuestionAnswerSort: (sortBy: QuestionAnswerSort) => void;
  setQuestionAnswerFilter: (filterBy: QuestionAnswerFilter) => void;
  setSearchKeyword: (keyword: string) => void;
  setError: (error: string | null) => void;
  loadCategories: () => Promise<void>;
  loadFixedTags: () => Promise<void>;
  loadHotTags: () => Promise<void>;
  loadPosts: () => Promise<void>;
  loadTrendingContent: () => Promise<void>;
  loadPostDetail: (postId: number, answerSortOverride?: QuestionAnswerSort) => Promise<void>;
  loadComments: (postId: number, pageCount?: number) => Promise<void>;
  loadQuickReplies: (postId: number) => Promise<void>;
  loadMoreComments: (postId: number) => Promise<void>;
  resetCommentSort: () => void;
}

export const useForumData = (t: TFunction): ForumDataState & ForumDataActions => {
  // 数据状态
  const [categories, setCategories] = useState<Category[]>([]);
  const [fixedTags, setFixedTags] = useState<Tag[]>([]);
  const [hotTags, setHotTags] = useState<Tag[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<number | null>(null);
  const [selectedTagName, setSelectedTagName] = useState<string | null>(null);
  const [posts, setPosts] = useState<PostItem[]>([]);
  const [selectedPost, setSelectedPost] = useState<PostDetail | null>(null);
  const [comments, setComments] = useState<CommentNode[]>([]);
  const [quickReplies, setQuickReplies] = useState<PostQuickReply[]>([]);
  const [quickReplyTotal, setQuickReplyTotal] = useState(0);
  const [commentTotal, setCommentTotal] = useState(0);
  const [loadedCommentPages, setLoadedCommentPages] = useState(0);

  // 热门内容
  const [hotPosts, setHotPosts] = useState<PostItem[]>([]);
  const [trendingGodComments, setTrendingGodComments] = useState<CommentNode[]>([]);
  const [postGodComments, setPostGodComments] = useState<Map<number, CommentHighlight>>(new Map());

  // 分页状态
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalPages, setTotalPages] = useState(0);

  // 排序状态
  const [sortBy, setSortBy] = useState<ForumPostSortBy>('newest');
  const [commentSortBy, setCommentSortBy] = useState<'newest' | 'hottest' | null>(null);
  const [postViewMode, setPostViewMode] = useState<ForumPostViewMode>('all');
  const [questionStatus, setQuestionStatus] = useState<QuestionStatusFilter>('all');
  const [pollStatus, setPollStatus] = useState<PollStatusFilter>('all');
  const [questionAnswerSort, setQuestionAnswerSort] = useState<QuestionAnswerSort>('default');
  const [questionAnswerFilter, setQuestionAnswerFilter] = useState<QuestionAnswerFilter>('all');

  // 搜索状态
  const [searchKeyword, setSearchKeyword] = useState('');

  // 加载状态
  const [loadingCategories, setLoadingCategories] = useState(false);
  const [loadingHotTags, setLoadingHotTags] = useState(false);
  const [loadingPosts, setLoadingPosts] = useState(false);
  const [loadingPostDetail, setLoadingPostDetail] = useState(false);
  const [loadingComments, setLoadingComments] = useState(false);
  const [loadingQuickReplies, setLoadingQuickReplies] = useState(false);
  const [loadingMoreComments, setLoadingMoreComments] = useState(false);
  const [loadingTrending, setLoadingTrending] = useState(false);
  const commentPageSize = 20;

  // 错误状态
  const [error, setError] = useState<string | null>(null);
  const inFlightGodCommentsRef = useRef<Set<number>>(new Set());
  const inFlightPostListRef = useRef<Set<string>>(new Set());
  const trendingLoadedRef = useRef(false);

  // 加载分类列表
  const loadCategories = async () => {
    setLoadingCategories(true);
    setError(null);
    try {
      const data = await getTopCategories(t);
      setCategories(data);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingCategories(false);
    }
  };

  // 加载热门标签
  const loadFixedTags = async () => {
    try {
      const tags = await getFixedTags(t);
      setFixedTags(tags);
    } catch (err) {
      if (isAbortError(err)) {
        log.warn('加载固定标签超时，已降级为空列表');
      } else {
        log.warn('加载固定标签失败:', err);
      }
      setFixedTags([]);
    }
  };

  // 加载热门标签
  const loadHotTags = async () => {
    setLoadingHotTags(true);
    try {
      const tags = await getHotTags(t, 20);
      setHotTags(tags);
    } catch (err) {
      if (isAbortError(err)) {
        log.warn('加载热门标签超时，已降级为空列表');
      } else {
        log.warn('加载热门标签失败:', err);
      }
      setHotTags([]);
    } finally {
      setLoadingHotTags(false);
    }
  };

  // 加载帖子列表
  const loadPosts = async () => {
    const resolvedPageIndex = selectedTagName ? 1 : currentPage;
    const resolvedPageSize = selectedTagName ? 100 : pageSize;
    const loadKey = `${selectedCategoryId ?? 'all'}|${resolvedPageIndex}|${resolvedPageSize}|${sortBy}|${postViewMode}|${questionStatus}|${pollStatus}|${searchKeyword.trim()}|${selectedTagName ?? ''}`;
    if (inFlightPostListRef.current.has(loadKey)) {
      return;
    }
    inFlightPostListRef.current.add(loadKey);
    setLoadingPosts(true);
    setError(null);
    try {
      const pageModel = await getPostList(
        selectedCategoryId,
        t,
        resolvedPageIndex,
        resolvedPageSize,
        sortBy,
        searchKeyword,
        undefined,
        undefined,
        postViewMode,
        questionStatus,
        pollStatus
      );
      const filteredPosts = selectedTagName
        ? pageModel.data.filter(post => {
            const rawTags = post.voTags || '';
            if (!rawTags.trim()) {
              return false;
            }

            const tags = rawTags
              .split(',')
              .map(item => item.trim())
              .filter(Boolean);

            return tags.some(tag => tag.toLowerCase() === selectedTagName.toLowerCase());
          })
        : pageModel.data;

      setPosts(filteredPosts);
      setTotalPages(selectedTagName ? (filteredPosts.length > 0 ? 1 : 0) : pageModel.pageCount);

      // 神评仅用于补充预览，不应阻塞帖子首屏渲染。
      void loadGodCommentsForPosts(pageModel.data);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingPosts(false);
      inFlightPostListRef.current.delete(loadKey);
    }
  };

  // 加载热门内容
  const loadTrendingContent = async () => {
    if (trendingLoadedRef.current) {
      return;
    }
    trendingLoadedRef.current = true;
    setLoadingTrending(true);
    try {
      // 获取热门帖子
      const hotPostsModel = await getPostList(null, t, 1, 6, 'hottest');
      setHotPosts(hotPostsModel.data);

      // 获取全局热门神评
      const allGodComments: CommentNode[] = [];
      const godCommentsMap = new Map(postGodComments);
      const hotPostIds = hotPostsModel.data
        .slice(0, 5)
        .map(post => post.voId)
        .filter(postId => !inFlightGodCommentsRef.current.has(postId));
      hotPostIds.forEach(postId => inFlightGodCommentsRef.current.add(postId));
      try {
        const batchResult = await getCurrentGodCommentsBatch(hotPostIds, t);
        for (const postId of hotPostIds) {
          const topGodComment = batchResult[postId];
          if (!topGodComment) continue;
          godCommentsMap.set(postId, topGodComment);
          allGodComments.push({
            voId: topGodComment.voCommentId,
            voContent: topGodComment.voContentSnapshot || '',
            voAuthorId: topGodComment.voAuthorId,
            voAuthorName: topGodComment.voAuthorName,
            voCreateTime: topGodComment.voCreateTime,
            voLikeCount: topGodComment.voLikeCount,
            voIsLiked: false,
            voIsGodComment: true,
            voIsSofa: false,
            voPostId: postId,
            voParentId: null,
            voReplyToUserId: null,
            voReplyToUserName: null,
            voChildren: []
          });
        }
      } catch (err) {
        log.warn('加载热门神评失败:', err);
      } finally {
        hotPostIds.forEach(postId => inFlightGodCommentsRef.current.delete(postId));
      }
      allGodComments.sort((a, b) => (b.voLikeCount || 0) - (a.voLikeCount || 0));
      setTrendingGodComments(allGodComments.slice(0, 10));
      setPostGodComments(prev => {
        const next = new Map(prev);
        for (const [postId, highlight] of godCommentsMap.entries()) {
          next.set(postId, highlight);
        }
        return next;
      });
    } catch (err) {
      if (isAbortError(err)) {
        log.warn('加载热门内容超时，稍后可重试');
      } else {
        log.error('加载热门内容失败:', err);
      }
      trendingLoadedRef.current = false;
    } finally {
      setLoadingTrending(false);
    }
  };

  // 为帖子列表加载神评预览
  const loadGodCommentsForPosts = async (postList: PostItem[]) => {
    const godCommentsMap = new Map(postGodComments);
    const missingPostIds = postList
      .filter(post => !godCommentsMap.has(post.voId) && !inFlightGodCommentsRef.current.has(post.voId))
      .map(post => post.voId);

    if (missingPostIds.length === 0) {
      return;
    }

    try {
      missingPostIds.forEach(postId => inFlightGodCommentsRef.current.add(postId));
      const batchResult = await getCurrentGodCommentsBatch(missingPostIds, t);
      for (const [postIdStr, highlight] of Object.entries(batchResult)) {
        const postId = Number(postIdStr);
        if (!Number.isNaN(postId) && highlight) {
          godCommentsMap.set(postId, highlight);
        }
      }
    } catch (err) {
      log.warn('加载帖子神评预览失败:', err);
    } finally {
      missingPostIds.forEach(postId => inFlightGodCommentsRef.current.delete(postId));
    }

    setPostGodComments(prev => {
      const next = new Map(prev);
      for (const [postId, highlight] of godCommentsMap.entries()) {
        next.set(postId, highlight);
      }
      return next;
    });
  };

  // 加载帖子详情
  const loadPostDetail = async (postId: number, answerSortOverride?: QuestionAnswerSort) => {
    setLoadingPostDetail(true);
    setError(null);
    try {
      const data = await getPostById(postId, t, answerSortOverride ?? questionAnswerSort);
      setSelectedPost(data);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      setSelectedPost(null);
      setComments([]);
      setQuickReplies([]);
      setQuickReplyTotal(0);
      setCommentTotal(0);
      setLoadedCommentPages(0);
    } finally {
      setLoadingPostDetail(false);
    }
  };

  // 加载评论列表
  const loadComments = async (postId: number, pageCount = 1) => {
    setLoadingComments(true);
    setError(null);
    try {
      const sortParam = commentSortBy || 'default';
      const resolvedPageCount = Math.max(1, pageCount);
      const firstPage = await getRootCommentsPage(postId, 1, commentPageSize, sortParam, t);
      const total = firstPage.voTotal ?? 0;
      const totalPages = total > 0 ? Math.ceil(total / commentPageSize) : 0;
      const targetPageCount = totalPages > 0
        ? Math.min(resolvedPageCount, totalPages)
        : 1;

      const aggregatedComments: CommentNode[] = [...(firstPage.voItems ?? [])];
      for (let pageIndex = 2; pageIndex <= targetPageCount; pageIndex += 1) {
        const pageData = await getRootCommentsPage(postId, pageIndex, commentPageSize, sortParam, t);
        aggregatedComments.push(...(pageData.voItems ?? []));
      }

      const deduplicatedComments = aggregatedComments.filter((comment, index, source) =>
        source.findIndex(item => item.voId === comment.voId) === index
      );

      setComments(sortRootComments(deduplicatedComments, sortParam));
      setCommentTotal(total);
      setLoadedCommentPages(totalPages > 0 ? targetPageCount : 0);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      setComments([]);
      setCommentTotal(0);
      setLoadedCommentPages(0);
    } finally {
      setLoadingComments(false);
    }
  };

  // 加载轻回应墙
  const loadQuickReplies = async (postId: number) => {
    setLoadingQuickReplies(true);
    setError(null);
    try {
      const wall = await getPostQuickReplyWall(postId, t);
      setQuickReplies(wall.voItems ?? []);
      setQuickReplyTotal(wall.voTotal ?? 0);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
      setQuickReplies([]);
      setQuickReplyTotal(0);
    } finally {
      setLoadingQuickReplies(false);
    }
  };

  const loadMoreComments = async (postId: number) => {
    if (loadingComments || loadingMoreComments) {
      return;
    }

    const nextPage = loadedCommentPages + 1;
    if (commentTotal > 0 && comments.length >= commentTotal) {
      return;
    }

    setLoadingMoreComments(true);
    setError(null);
    try {
      const sortParam = commentSortBy || 'default';
      const pageData = await getRootCommentsPage(postId, nextPage, commentPageSize, sortParam, t);
      const nextItems = pageData.voItems ?? [];

      setComments(prev => {
        const existingIds = new Set(prev.map(item => item.voId));
        const appendedItems = nextItems.filter(item => !existingIds.has(item.voId));
        return sortRootComments([...prev, ...appendedItems], sortParam);
      });
      setCommentTotal(prev => pageData.voTotal ?? prev);
      setLoadedCommentPages(prev => nextItems.length > 0 ? Math.max(prev, nextPage) : prev);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingMoreComments(false);
    }
  };

  // 重置评论排序
  const resetCommentSort = () => {
    setCommentSortBy(null);
  };

  // 初始化：加载分类和热门内容
  useEffect(() => {
    void loadCategories();

    const loadDeferredPanels = () => {
      void loadFixedTags();
      void loadHotTags();
      void loadTrendingContent();
    };

    if (typeof window !== 'undefined' && typeof window.requestIdleCallback === 'function') {
      const idleHandle = window.requestIdleCallback(() => loadDeferredPanels(), { timeout: 1200 });
      return () => window.cancelIdleCallback(idleHandle);
    }

    const timer = window.setTimeout(loadDeferredPanels, 240);
    return () => window.clearTimeout(timer);
  }, []);

  // 当选择分类时重新加载帖子
  useEffect(() => {
    setCurrentPage(1);
  }, [selectedCategoryId, selectedTagName, postViewMode, questionStatus, pollStatus]);

  useEffect(() => {
    if (postViewMode === 'question' && (sortBy === 'hottest' || sortBy === 'essence' || sortBy === 'votes' || sortBy === 'deadline')) {
      setSortBy('newest');
      return;
    }

    if (postViewMode === 'poll' && (sortBy === 'hottest' || sortBy === 'essence' || sortBy === 'pending' || sortBy === 'answers')) {
      setSortBy('newest');
      return;
    }

    if (postViewMode === 'all' && (sortBy === 'pending' || sortBy === 'answers' || sortBy === 'votes' || sortBy === 'deadline')) {
      setSortBy('newest');
    }
  }, [postViewMode, sortBy]);

  // 当页码、排序或搜索变化时重新加载帖子
  useEffect(() => {
    void loadPosts();
  }, [selectedCategoryId, selectedTagName, currentPage, sortBy, searchKeyword, postViewMode, questionStatus, pollStatus]);

  return {
    // 状态
    categories,
    fixedTags,
    hotTags,
    selectedCategoryId,
    selectedTagName,
    posts,
    selectedPost,
    comments,
    quickReplies,
    quickReplyTotal,
    commentTotal,
    commentPageSize,
    loadedCommentPages,
    hotPosts,
    trendingGodComments,
    postGodComments,
    currentPage,
    pageSize,
    totalPages,
    sortBy,
    commentSortBy,
    postViewMode,
    questionStatus,
    pollStatus,
    questionAnswerSort,
    questionAnswerFilter,
    searchKeyword,
    loadingCategories,
    loadingHotTags,
    loadingPosts,
    loadingPostDetail,
    loadingComments,
    loadingQuickReplies,
    loadingMoreComments,
    loadingTrending,
    error,

    // 操作
    setSelectedCategoryId,
    setSelectedTagName,
    setSelectedPost,
    setComments,
    setQuickReplies,
    setQuickReplyTotal,
    setCommentTotal,
    setCurrentPage,
    setSortBy,
    setCommentSortBy,
    setPostViewMode,
    setQuestionStatus,
    setPollStatus,
    setQuestionAnswerSort,
    setQuestionAnswerFilter,
    setSearchKeyword,
    setError,
    loadCategories,
    loadFixedTags,
    loadHotTags,
    loadPosts,
    loadTrendingContent,
    loadPostDetail,
    loadComments,
    loadQuickReplies,
    loadMoreComments,
    resetCommentSort
  };
};
