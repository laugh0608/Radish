import { useState, useEffect } from 'react';
import { log } from '@/utils/logger';
import type { Dispatch, SetStateAction } from 'react';
import type { TFunction } from 'i18next';
import {
  getTopCategories,
  getPostList,
  getPostById,
  getCommentTree,
  getCurrentGodComments,
  type Category,
  type PostItem,
  type PostDetail,
  type CommentNode,
  type CommentHighlight
} from '@/api/forum';

export interface ForumDataState {
  // 数据状态
  categories: Category[];
  selectedCategoryId: number | null;
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
  loadingPosts: boolean;
  loadingPostDetail: boolean;
  loadingComments: boolean;
  loadingTrending: boolean;

  // 错误状态
  error: string | null;
}

export interface ForumDataActions {
  setSelectedCategoryId: (id: number | null) => void;
  setSelectedPost: Dispatch<SetStateAction<PostDetail | null>>;
  setComments: (comments: CommentNode[]) => void;
  setCurrentPage: (page: number) => void;
  setSortBy: (sortBy: 'newest' | 'hottest' | 'essence') => void;
  setCommentSortBy: (sortBy: 'newest' | 'hottest' | null) => void;
  setSearchKeyword: (keyword: string) => void;
  setError: (error: string | null) => void;
  loadCategories: () => Promise<void>;
  loadPosts: () => Promise<void>;
  loadTrendingContent: () => Promise<void>;
  loadPostDetail: (postId: number) => Promise<void>;
  loadComments: (postId: number) => Promise<void>;
  resetCommentSort: () => void;
}

export const useForumData = (t: TFunction): ForumDataState & ForumDataActions => {
  // 数据状态
  const [categories, setCategories] = useState<Category[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<number | null>(null);
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
  const [loadingPosts, setLoadingPosts] = useState(false);
  const [loadingPostDetail, setLoadingPostDetail] = useState(false);
  const [loadingComments, setLoadingComments] = useState(false);
  const [loadingTrending, setLoadingTrending] = useState(false);

  // 错误状态
  const [error, setError] = useState<string | null>(null);

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
    }
  };

  // 加载帖子列表
  const loadPosts = async () => {
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

      // 加载每个帖子的神评
      await loadGodCommentsForPosts(pageModel.data);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      setLoadingPosts(false);
    }
  };

  // 加载热门内容
  const loadTrendingContent = async () => {
    setLoadingTrending(true);
    try {
      // 获取热门帖子
      const hotPostsModel = await getPostList(null, t, 1, 10, 'hottest');
      setHotPosts(hotPostsModel.data);

      // 获取全局热门神评
      const allGodComments: CommentNode[] = [];
      for (const post of hotPostsModel.data.slice(0, 5)) {
        try {
          const godComments = await getCurrentGodComments(post.voId, t);
          if (godComments.length > 0) {
            const topGodComment = godComments[0];
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
              voPostId: post.voId,
              voParentId: null,
              voReplyToUserId: null,
              voReplyToUserName: null,
              voChildren: []
            });
          }
        } catch {
          // 忽略单个帖子的错误
        }
      }
      allGodComments.sort((a, b) => (b.voLikeCount || 0) - (a.voLikeCount || 0));
      setTrendingGodComments(allGodComments.slice(0, 10));
    } catch (err) {
      log.error('加载热门内容失败:', err);
    } finally {
      setLoadingTrending(false);
    }
  };

  // 为帖子列表加载神评预览
  const loadGodCommentsForPosts = async (postList: PostItem[]) => {
    const godCommentsMap = new Map<number, CommentHighlight>();

    await Promise.all(
      postList.map(async (post) => {
        try {
          const godComments = await getCurrentGodComments(post.voId, t);
          if (godComments.length > 0) {
            godCommentsMap.set(post.voId, godComments[0]);
          }
        } catch {
          // 忽略单个帖子的错误
        }
      })
    );

    setPostGodComments(godCommentsMap);
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
    void loadTrendingContent();
  }, []);

  // 当选择分类时重新加载帖子
  useEffect(() => {
    setCurrentPage(1);
    void loadPosts();
  }, [selectedCategoryId]);

  // 当页码、排序或搜索变化时重新加载帖子
  useEffect(() => {
    void loadPosts();
  }, [currentPage, sortBy, searchKeyword]);

  return {
    // 状态
    categories,
    selectedCategoryId,
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
    loadingPosts,
    loadingPostDetail,
    loadingComments,
    loadingTrending,
    error,

    // 操作
    setSelectedCategoryId,
    setSelectedPost,
    setComments,
    setCurrentPage,
    setSortBy,
    setCommentSortBy,
    setSearchKeyword,
    setError,
    loadCategories,
    loadPosts,
    loadTrendingContent,
    loadPostDetail,
    loadComments,
    resetCommentSort
  };
};
