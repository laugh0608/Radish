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
  getCommentTree,
  getCurrentGodCommentsBatch,
  type Category,
  type Tag,
  type PostItem,
  type PostDetail,
  type CommentNode,
  type CommentHighlight
} from '@/api/forum';

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
  hotPosts: PostItem[];
  trendingGodComments: CommentNode[];
  postGodComments: Map<number, CommentHighlight>;

  // 分页状态
  currentPage: number;
  pageSize: number;
  totalPages: number;

  // 排序状态
  sortBy: 'newest' | 'hottest' | 'essence';
  commentSortBy: 'newest' | 'hottest' | null;

  // 搜索状态
  searchKeyword: string;

  // 加载状态
  loadingCategories: boolean;
  loadingHotTags: boolean;
  loadingPosts: boolean;
  loadingPostDetail: boolean;
  loadingComments: boolean;
  loadingTrending: boolean;

  // 错误状态
  error: string | null;
}

export interface ForumDataActions {
  setSelectedCategoryId: (id: number | null) => void;
  setSelectedTagName: (tagName: string | null) => void;
  setSelectedPost: Dispatch<SetStateAction<PostDetail | null>>;
  setComments: Dispatch<SetStateAction<CommentNode[]>>;
  setCurrentPage: (page: number) => void;
  setSortBy: (sortBy: 'newest' | 'hottest' | 'essence') => void;
  setCommentSortBy: (sortBy: 'newest' | 'hottest' | null) => void;
  setSearchKeyword: (keyword: string) => void;
  setError: (error: string | null) => void;
  loadCategories: () => Promise<void>;
  loadFixedTags: () => Promise<void>;
  loadHotTags: () => Promise<void>;
  loadPosts: () => Promise<void>;
  loadTrendingContent: () => Promise<void>;
  loadPostDetail: (postId: number) => Promise<void>;
  loadComments: (postId: number) => Promise<void>;
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

  // 热门内容
  const [hotPosts, setHotPosts] = useState<PostItem[]>([]);
  const [trendingGodComments, setTrendingGodComments] = useState<CommentNode[]>([]);
  const [postGodComments, setPostGodComments] = useState<Map<number, CommentHighlight>>(new Map());

  // 分页状态
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalPages, setTotalPages] = useState(0);

  // 排序状态
  const [sortBy, setSortBy] = useState<'newest' | 'hottest' | 'essence'>('newest');
  const [commentSortBy, setCommentSortBy] = useState<'newest' | 'hottest' | null>(null);

  // 搜索状态
  const [searchKeyword, setSearchKeyword] = useState('');

  // 加载状态
  const [loadingCategories, setLoadingCategories] = useState(false);
  const [loadingHotTags, setLoadingHotTags] = useState(false);
  const [loadingPosts, setLoadingPosts] = useState(false);
  const [loadingPostDetail, setLoadingPostDetail] = useState(false);
  const [loadingComments, setLoadingComments] = useState(false);
  const [loadingTrending, setLoadingTrending] = useState(false);
  const [categoriesLoaded, setCategoriesLoaded] = useState(false);

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
      if (data.length > 0 && selectedCategoryId == null) {
        setSelectedCategoryId(data[0].voId);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingCategories(false);
      setCategoriesLoaded(true);
    }
  };

  // 加载热门标签
  const loadFixedTags = async () => {
    try {
      const tags = await getFixedTags(t);
      setFixedTags(tags);
    } catch (err) {
      log.warn('加载固定标签失败:', err);
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
      log.warn('加载热门标签失败:', err);
      setHotTags([]);
    } finally {
      setLoadingHotTags(false);
    }
  };

  // 加载帖子列表
  const loadPosts = async () => {
    if (!categoriesLoaded && selectedCategoryId == null) {
      return;
    }
    const resolvedPageIndex = selectedTagName ? 1 : currentPage;
    const resolvedPageSize = selectedTagName ? 100 : pageSize;
    const loadKey = `${selectedCategoryId ?? 'all'}|${resolvedPageIndex}|${resolvedPageSize}|${sortBy}|${searchKeyword.trim()}|${selectedTagName ?? ''}`;
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
        searchKeyword
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

      // 加载每个帖子的神评
      await loadGodCommentsForPosts(pageModel.data);
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
      const hotPostsModel = await getPostList(null, t, 1, 10, 'hottest');
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
      } catch {
        // 忽略热门神评批量请求错误
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
      log.error('加载热门内容失败:', err);
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
    } catch {
      // 忽略批量神评请求错误
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
  const loadPostDetail = async (postId: number) => {
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
  };

  // 加载评论列表
  const loadComments = async (postId: number) => {
    setLoadingComments(true);
    setError(null);
    try {
      const sortParam = commentSortBy || 'default';
      const commentsData = await getCommentTree(postId, sortParam, t);
      setComments(commentsData);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingComments(false);
    }
  };

  // 重置评论排序
  const resetCommentSort = () => {
    setCommentSortBy(null);
  };

  // 初始化：加载分类和热门内容
  useEffect(() => {
    void loadCategories();
    void loadFixedTags();
    void loadHotTags();
    void loadTrendingContent();
  }, []);

  // 当选择分类时重新加载帖子
  useEffect(() => {
    setCurrentPage(1);
  }, [selectedCategoryId, selectedTagName]);

  // 当页码、排序或搜索变化时重新加载帖子
  useEffect(() => {
    void loadPosts();
  }, [categoriesLoaded, selectedCategoryId, selectedTagName, currentPage, sortBy, searchKeyword]);

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
    hotPosts,
    trendingGodComments,
    postGodComments,
    currentPage,
    pageSize,
    totalPages,
    sortBy,
    commentSortBy,
    searchKeyword,
    loadingCategories,
    loadingHotTags,
    loadingPosts,
    loadingPostDetail,
    loadingComments,
    loadingTrending,
    error,

    // 操作
    setSelectedCategoryId,
    setSelectedTagName,
    setSelectedPost,
    setComments,
    setCurrentPage,
    setSortBy,
    setCommentSortBy,
    setSearchKeyword,
    setError,
    loadCategories,
    loadFixedTags,
    loadHotTags,
    loadPosts,
    loadTrendingContent,
    loadPostDetail,
    loadComments,
    resetCommentSort
  };
};
