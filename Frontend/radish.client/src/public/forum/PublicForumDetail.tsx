import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ApiResponseError,
  PostBookmarkErrorCode,
  isApiResponseNotFoundError,
  type ContentRewardTargetType,
} from '@radish/http';
import { toast } from '@radish/ui/toast';
import type { ContentReportTargetType } from '@/api/contentModeration';
import { setMyPostBookmarkState } from '@/api/postBookmark';
import {
  createComment,
  createPostQuickReply,
  getCommentNavigation,
  getChildComments,
  getPostById,
  getPostQuickReplyWall,
  getRootCommentsPage,
  getTopCategories,
  likePost,
  toggleCommentLike,
  updateComment,
  updatePost,
  type Category,
  type CommentContentRevisionDetailVo,
  type CommentNode,
  type CommentReplyTarget,
  type PostContentRevisionDetailVo,
  type PostDetail,
  type PostQuickReply,
} from '@/api/forum';
import type { LongId } from '@/api/user';
import { buildPublicForumPostReturnPath } from '@/services/authReturnPath';
import { redirectToLogin } from '@/services/auth';
import { commentHub } from '@/services/commentHub';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { log } from '@/utils/logger';
import {
  createClientSubmissionState,
  type ClientSubmissionState
} from '@/utils/clientSubmission';
import { PostAnswerLifecycleSection } from '@/apps/forum/components/PostAnswerLifecycleSection';
import {
  buildCommentEditSubmissionFingerprint,
  buildCommentSubmissionFingerprint,
  buildPostEditSubmissionFingerprint,
} from '@/apps/forum/utils/forumSubmissionFingerprint';
import { findForumCommentById } from '@/apps/forum/utils/forumCommentTree';
import { useContentRewardStates } from '@/apps/forum/hooks/useContentRewardStates';
import { useReactions } from '@/apps/forum/hooks/useReactions';
import { buildContentRewardTargetKey } from '@/apps/forum/utils/contentRewardState';
import { isForumPostLiked, writeForumPostLikedState } from '@/apps/forum/utils/forumPostLikeState';
import { upsertCommentInTree } from '@/apps/forum/utils/commentRealtimeTree';
import { buildPublicForumPath } from '../forumRouteState';
import { rememberPublicRouteSourceTransfer } from '../publicRouteNavigation';
import { buildPublicShareUrl } from '../publicHead';
import {
  resolvePublicForumDetailLoadState,
  resolvePublicForumReadSectionState,
} from './publicForumViewState';
import { usePublicShareLink } from '../hooks/usePublicShareLink';
import {
  getForumPostRouteIdentifier,
  collectForumCommentIds,
  isSameLongId,
  mergeCommentChildren,
  normalizeForumTagNames,
  resolvePublicProfileUserId,
} from './publicForumUtils';
import type { PublicForumCommentNavigationTarget, PublicForumDetailProps } from './publicForumDetailTypes';
import { usePublicForumDetailNavigationGuard } from './usePublicForumDetailNavigationGuard';
import { usePublicForumAnswerPage } from './usePublicForumAnswerPage';
import { usePublicForumCommentRealtime } from './usePublicForumCommentRealtime';
import { usePublicForumCommentFocus } from './usePublicForumCommentFocus';
import { usePublicForumPostHead } from './usePublicForumPostHead';
import { PublicForumDetailView } from './PublicForumDetailView';
type RootCommentSort = 'newest' | 'hottest' | null;
const COMMENT_NAVIGATION_CHILD_PAGE_SIZE = 5;
const EditPostModal = lazy(() =>
  import('@/apps/forum/components/EditPostModal').then((module) => ({ default: module.EditPostModal }))
);
const ContentRevisionModal = lazy(() =>
  import('@/apps/forum/components/ContentRevisionModal').then((module) => ({ default: module.ContentRevisionModal }))
);
const ContentReportModal = lazy(() =>
  import('@/components/ContentReportModal').then((module) => ({ default: module.ContentReportModal }))
);
export const PublicForumDetail = ({
  postId,
  commentId,
  answerPublicId,
  answerPage: answerPageIndex = 1,
  answerSort = 'default',
  intent,
  sourceState,
  displayTimeZone,
  backLabel,
  backHref,
  onBack,
  isAnswerEditorUploading,
  onAnswerEditorUploadingChange,
  onAnswerStateChange,
  onOpenAuthorProfile,
  onOpenTag,
  onOpenQuestion,
  onOpenPoll,
  onOpenLottery
}: PublicForumDetailProps) => {
  const { t } = useTranslation();
  const authStoreAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const isUserAuthenticated = useUserStore((state) => state.isAuthenticated);
  const currentUserId = useUserStore((state) => state.userId);
  const currentUserName = useUserStore((state) => state.userName);
  const currentUserAvatarUrl = useUserStore((state) => state.avatarThumbnailUrl || state.avatarUrl || null);
  const [post, setPost] = useState<PostDetail | null>(null);
  const [comments, setComments] = useState<CommentNode[]>([]);
  const [quickReplies, setQuickReplies] = useState<PostQuickReply[]>([]);
  const [quickReplyTotal, setQuickReplyTotal] = useState(0);
  const [commentTotal, setCommentTotal] = useState(0);
  const [loadedCommentPages, setLoadedCommentPages] = useState(0);
  const [commentSortBy, setCommentSortBy] = useState<RootCommentSort>(null);
  const [loadingPost, setLoadingPost] = useState(false);
  const [loadingComments, setLoadingComments] = useState(false);
  const [loadingQuickReplies, setLoadingQuickReplies] = useState(false);
  const [loadingMoreComments, setLoadingMoreComments] = useState(false);
  const [bookmarkLoading, setBookmarkLoading] = useState(false);
  const [isPostLiked, setIsPostLiked] = useState(false);
  const [postError, setPostError] = useState<string | null>(null);
  const [postNotFound, setPostNotFound] = useState(false);
  const [commentError, setCommentError] = useState<string | null>(null);
  const [quickReplyError, setQuickReplyError] = useState<string | null>(null);
  const [commentPagingError, setCommentPagingError] = useState<string | null>(null);
  const [commentNavigationTarget, setCommentNavigationTarget] = useState<PublicForumCommentNavigationTarget | null>(null);
  const [commentNavigationNotice, setCommentNavigationNotice] = useState<string | null>(null);
  const [submittingComment, setSubmittingComment] = useState(false);
  const [replyTo, setReplyTo] = useState<CommentReplyTarget | null>(null);
  const [quickReplyFocusKey, setQuickReplyFocusKey] = useState<string | null>(null);
  const [commentFocusKey, setCommentFocusKey] = useState<string | null>(null);
  const [answerFocusKey, setAnswerFocusKey] = useState<string | null>(null);
  const [categories, setCategories] = useState<Category[]>([]);
  const [categoriesLoading, setCategoriesLoading] = useState(false);
  const [categoriesError, setCategoriesError] = useState<string | null>(null);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isPostHistoryOpen, setIsPostHistoryOpen] = useState(false);
  const [activeCommentRevisionId, setActiveCommentRevisionId] = useState<LongId | null>(null);
  const [postRevisionDraft, setPostRevisionDraft] = useState<PostContentRevisionDetailVo | null>(null);
  const [commentRevisionDraft, setCommentRevisionDraft] = useState<CommentContentRevisionDetailVo | null>(null);
  const [reportTarget, setReportTarget] = useState<{
    targetType: ContentReportTargetType;
    targetId: LongId;
  } | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [answerReloadToken, setAnswerReloadToken] = useState(0);
  const requestIdRef = useRef(0);
  const handledAuthorIntentRef = useRef<string | null>(null);
  const commentSubmissionRef = useRef<ClientSubmissionState | null>(null);
  const postEditSubmissionRef = useRef<ClientSubmissionState | null>(null);
  const commentEditSubmissionRef = useRef<ClientSubmissionState | null>(null);
  const commentPageSize = 20;
  const isAuthenticated = authStoreAuthenticated && isUserAuthenticated();
  const isCurrentUserAuthor = !!post && !!currentUserId && isSameLongId(post.voAuthorId, currentUserId);
  const activeRevisionComment = findForumCommentById(comments, activeCommentRevisionId);
  const postRevisionTarget = isPostHistoryOpen && post
    ? {
        kind: 'post' as const,
        targetId: post.voId,
        currentContentRevision: post.voContentRevision,
      }
    : null;
  const commentRevisionTarget = activeCommentRevisionId
    ? {
        kind: 'comment' as const,
        targetId: activeCommentRevisionId,
        currentContentRevision: activeRevisionComment?.voContentRevision ?? 1,
      }
    : null;
  const handleReactionError = useCallback((message: string) => {
    log.warn('公开论坛回应操作失败:', message);
  }, []);
  const {
    stateMap: contentRewardStateMap,
    handleStateChange: handleContentRewardStateChange,
    handleTargetsVisible: handleContentRewardTargetsVisible,
  } = useContentRewardStates({
    postId: post?.voId ?? null,
    comments,
    viewerKey: `${isAuthenticated}:${String(currentUserId ?? '0')}`,
    t,
    logSource: 'PublicForumDetail',
  });
  const {
    commentItemsMap, isPending: isReactionPending, loadCommentReactions, loadPostReactions,
    loadingPost: loadingPostReactions, postItems: postReactionItems,
    toggleCommentReaction, togglePostReaction,
  } = useReactions({ onError: handleReactionError });
  const answerPageState = usePublicForumAnswerPage({
    enabled: Boolean(post?.voIsQuestion),
    postIdentifier: post?.voPublicId?.trim() || postId,
    pageIndex: answerPageIndex,
    sort: answerSort,
    targetAnswerPublicId: answerPublicId,
    reloadToken: answerReloadToken,
    viewerKey: `${isAuthenticated}:${String(currentUserId ?? '0')}`,
    t,
    onStateChange: onAnswerStateChange,
  });
  usePublicForumPostHead({ post, postId, commentId, t });
  const {
    handleBackWhileEditorIdle,
    handleOpenAuthorProfileWhileEditorIdle,
    handleOpenTagWhileEditorIdle,
    handleOpenQuestionWhileEditorIdle,
    handleOpenPollWhileEditorIdle,
    handleOpenLotteryWhileEditorIdle,
  } = usePublicForumDetailNavigationGuard({
    navigationLocked: isAnswerEditorUploading,
    onBack,
    onOpenAuthorProfile,
    onOpenTag,
    onOpenQuestion,
    onOpenPoll,
    onOpenLottery,
  });
  useEffect(() => {
    setIsPostHistoryOpen(false);
    setActiveCommentRevisionId(null);
    setPostRevisionDraft(null);
    setCommentRevisionDraft(null);
    setReplyTo(null);
    postEditSubmissionRef.current = null;
    commentEditSubmissionRef.current = null;
  }, [currentUserId, isAuthenticated, post?.voId]);

  useEffect(() => {
    setIsPostLiked(post?.voId ? isForumPostLiked(post.voId) : false);
  }, [currentUserId, post?.voId]);

  const handleOpenReport = useCallback((targetType: ContentReportTargetType, targetId: LongId) => {
    if (!isAuthenticated) {
      toast.error(t('report.loginRequired'));
      return;
    }

    if (!targetId) {
      return;
    }

    setReportTarget({ targetType, targetId });
  }, [isAuthenticated, t]);

  const applyPostCommentCountDelta = useCallback((delta: number) => {
    setPost((current) => {
      if (!current) {
        return current;
      }

      return {
        ...current,
        voCommentCount: Math.max(0, (current.voCommentCount ?? 0) + delta)
      };
    });
  }, []);

  const syncPostCommentCount = useCallback((count: number | null | undefined) => {
    if (typeof count !== 'number') {
      return;
    }

    setPost((current) => (
      current
        ? {
            ...current,
            voCommentCount: Math.max(0, count)
          }
        : current
    ));
  }, []);
  const {
    clearTypingUsers: clearCommentTypingUsers,
    registerLoadedRootComments,
    registerRootCommentCount,
    syncCountedRootComments,
    typingText: commentTypingText,
  } = usePublicForumCommentRealtime({
    postId: post?.voId,
    commentSortBy,
    setComments,
    setCommentTotal,
    onPostCommentCountDelta: applyPostCommentCountDelta,
  });
  const {
    highlightedCommentId,
    noticeRef: commentNoticeRef,
    registerCommentAnchor,
    resetHighlight: resetCommentHighlight,
  } = usePublicForumCommentFocus({
    navigationTarget: commentNavigationTarget,
    navigationNotice: commentNavigationNotice,
    comments,
  });

  useEffect(() => {
    const requestId = ++requestIdRef.current;

    const loadDetail = async () => {
      setLoadingPost(true);
      setLoadingComments(true);
      setLoadingQuickReplies(true);
      setPostError(null);
      setPostNotFound(false);
      setCommentError(null);
      setQuickReplyError(null);
      setCommentPagingError(null);
      setCommentNavigationTarget(null);
      setCommentNavigationNotice(null);
      clearCommentTypingUsers();
      resetCommentHighlight();
      let resolvedPostId: LongId = postId;

      try {
        const postDetail = await getPostById(postId, t);
        if (requestId !== requestIdRef.current) {
          return;
        }

        resolvedPostId = postDetail.voId;
        setPost(postDetail);
      } catch (err) {
        if (requestId !== requestIdRef.current) {
          return;
        }

        const message = err instanceof Error ? err.message : String(err);
        setPost(null);
        setComments([]);
        setQuickReplies([]);
        setQuickReplyTotal(0);
        setCommentTotal(0);
        setLoadedCommentPages(0);
        setPostNotFound(isApiResponseNotFoundError(err));
        setPostError(message);
        return;
      } finally {
        if (requestId === requestIdRef.current) {
          setLoadingPost(false);
        }
      }

      try {
        let navigation: Awaited<ReturnType<typeof getCommentNavigation>> | null = null;
        if (commentId) {
          try {
            navigation = await getCommentNavigation(
              resolvedPostId,
              commentId,
              commentPageSize,
              COMMENT_NAVIGATION_CHILD_PAGE_SIZE,
              t
            );
          } catch (navigationError) {
            if (requestId !== requestIdRef.current) {
              return;
            }

            log.warn('公开论坛评论定位失败，已降级为普通帖子阅读:', navigationError);
            setCommentNavigationNotice(t('forum.commentNavigation.notice'));
          }
        }

        const [rootCommentsResult, replyWallResult] = await Promise.allSettled([
          getRootCommentsPage(resolvedPostId, navigation?.voRootPageIndex ?? 1, commentPageSize, commentSortBy || 'default', t),
          getPostQuickReplyWall(resolvedPostId, t)
        ]);

        if (requestId !== requestIdRef.current) {
          return;
        }

        if (rootCommentsResult.status === 'fulfilled') {
          const rootComments = rootCommentsResult.value;
          let nextComments = rootComments.voItems ?? [];

          if (!navigation) {
            setCommentNavigationTarget(null);
          } else if (!navigation.voIsRootComment && navigation.voParentCommentId && navigation.voChildPageIndex) {
            try {
              const aggregatedChildren: CommentNode[] = [];
              let totalChildren = 0;

              for (let pageIndex = 1; pageIndex <= navigation.voChildPageIndex; pageIndex += 1) {
                const pageData = await getChildComments(
                  navigation.voParentCommentId,
                  pageIndex,
                  COMMENT_NAVIGATION_CHILD_PAGE_SIZE,
                  t
                );

                if (requestId !== requestIdRef.current) {
                  return;
                }

                totalChildren = pageData.voTotal ?? totalChildren;
                aggregatedChildren.push(...(pageData.voItems ?? []));
              }

              const deduplicatedChildren = aggregatedChildren.filter((child, index, source) =>
                source.findIndex((item) => isSameLongId(item.voId, child.voId)) === index
              );
              nextComments = mergeCommentChildren(
                nextComments,
                navigation.voParentCommentId,
                deduplicatedChildren,
                totalChildren
              );
            } catch (childLoadError) {
              if (requestId !== requestIdRef.current) {
                return;
              }

              log.warn('公开论坛评论定位补载子评论失败，已保留当前评论页:', childLoadError);
              setCommentNavigationNotice(t('forum.commentNavigation.notice'));
            }
          }

          syncCountedRootComments(nextComments);
          setComments(nextComments);
          setCommentTotal(rootComments.voTotal ?? 0);
          syncPostCommentCount(rootComments.voTotal);
          setLoadedCommentPages((rootComments.voItems?.length ?? 0) > 0 ? (rootComments.voPageIndex ?? 1) : 0);
          setCommentError(null);
          setCommentNavigationTarget(navigation ? {
            commentId: navigation.voCommentId,
            expandedRootCommentId: navigation.voIsRootComment
              ? undefined
              : navigation.voParentCommentId ?? navigation.voRootCommentId,
            navigationKey: `${resolvedPostId}:${commentId ?? navigation.voCommentId}:${commentSortBy ?? 'default'}:${reloadToken}`
          } : null);
        } else {
          setComments([]);
          syncCountedRootComments([]);
          setCommentTotal(0);
          setLoadedCommentPages(0);
          const message = rootCommentsResult.reason instanceof Error
            ? rootCommentsResult.reason.message
            : String(rootCommentsResult.reason);
          setCommentError(message);
          setCommentNavigationTarget(null);
        }

        if (replyWallResult.status === 'fulfilled') {
          const replyWall = replyWallResult.value;
          setQuickReplies(replyWall.voItems ?? []);
          setQuickReplyTotal(replyWall.voTotal ?? 0);
          setQuickReplyError(null);
        } else {
          setQuickReplies([]);
          setQuickReplyTotal(0);
          const message = replyWallResult.reason instanceof Error
            ? replyWallResult.reason.message
            : String(replyWallResult.reason);
          setQuickReplyError(message);
        }
      } finally {
        if (requestId === requestIdRef.current) {
          setLoadingComments(false);
          setLoadingQuickReplies(false);
        }
      }
    };

    void loadDetail();
  }, [
    clearCommentTypingUsers,
    commentId,
    commentSortBy,
    postId,
    reloadToken,
    resetCommentHighlight,
    syncCountedRootComments,
    syncPostCommentCount,
    t
  ]);

  useEffect(() => {
    if (post?.voId) {
      void loadPostReactions(post.voId);
    }
  }, [loadPostReactions, post?.voId]);

  useEffect(() => {
    void loadCommentReactions(collectForumCommentIds(comments), { replace: true });
  }, [comments, loadCommentReactions]);

  const buildForumShareUrl = useCallback(() => {
    const sharePostId = post ? getForumPostRouteIdentifier(post) : postId;
    const sharePath = buildPublicForumPath(commentId
      ? { kind: 'detail', postId: sharePostId, commentId }
      : { kind: 'detail', postId: sharePostId });
    return buildPublicShareUrl(sharePath);
  }, [commentId, post, postId]);
  const { copyShareLink, shareBusy, shareState } = usePublicShareLink({
    buildShareUrl: buildForumShareUrl,
  });
  const commentReturnPath = post
    ? buildPublicForumPostReturnPath({
      postId: post.voId,
      postPublicId: post.voPublicId,
      commentId,
      intent: 'comment',
    })
    : null;
  const quickReplyReturnPath = post
    ? buildPublicForumPostReturnPath({
      postId: post.voId,
      postPublicId: post.voPublicId,
      commentId,
      intent: 'quickReply',
    })
    : null;
  const answerReturnPath = post
    ? buildPublicForumPostReturnPath({
      postId: post.voId,
      postPublicId: post.voPublicId,
      intent: 'answer',
    })
    : null;
  const editReturnPath = post
    ? buildPublicForumPostReturnPath({
      postId: post.voId,
      postPublicId: post.voPublicId,
      intent: 'edit',
    })
    : null;
  const historyReturnPath = post
    ? buildPublicForumPostReturnPath({
      postId: post.voId,
      postPublicId: post.voPublicId,
      intent: 'history',
    })
    : null;
  const bookmarkReturnPath = post
    ? buildPublicForumPostReturnPath({
      postId: post.voId,
      postPublicId: post.voPublicId,
      intent: 'bookmark',
    })
    : null;
  const interactionReturnPath = commentReturnPath;
  const routeIntentFocusKey = post && intent
    ? `${post.voId}:${commentId ?? 'root'}:${intent}`
    : null;
  const quickReplyAutoFocusKey = intent === 'quickReply'
    ? routeIntentFocusKey
    : quickReplyFocusKey;
  const commentAutoFocusKey = intent === 'comment'
    ? routeIntentFocusKey
    : commentFocusKey;
  const answerAutoFocusKey = intent === 'answer'
    ? routeIntentFocusKey
    : answerFocusKey;

  const redirectToDetailLogin = useCallback((returnPath: string | null | undefined) => {
    if (returnPath && sourceState) {
      rememberPublicRouteSourceTransfer(returnPath, sourceState);
    }

    redirectToLogin({ returnPath });
  }, [sourceState]);

  const handleLikePost = useCallback(async (targetPostId: LongId) => {
    if (!isAuthenticated) {
      redirectToDetailLogin(interactionReturnPath);
      return;
    }

    try {
      const result = await likePost(targetPostId, t);
      setPost((current) => current && isSameLongId(current.voId, targetPostId)
        ? { ...current, voLikeCount: result.voLikeCount }
        : current);
      setIsPostLiked(result.voIsLiked);
      writeForumPostLikedState(targetPostId, result.voIsLiked);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t('forum.public.likeFailed'));
    }
  }, [interactionReturnPath, isAuthenticated, redirectToDetailLogin, t]);

  const handleLikeComment = useCallback(async (targetCommentId: LongId) => {
    if (!isAuthenticated) {
      redirectToDetailLogin(interactionReturnPath);
      throw new Error(t('forum.public.commentLoginPrompt'));
    }

    return toggleCommentLike(targetCommentId, t);
  }, [interactionReturnPath, isAuthenticated, redirectToDetailLogin, t]);

  const handleRequireReactionLogin = useCallback(() => {
    redirectToDetailLogin(interactionReturnPath);
  }, [interactionReturnPath, redirectToDetailLogin]);

  const handleReplyComment = useCallback((target: CommentReplyTarget) => {
    if (!isAuthenticated) {
      redirectToDetailLogin(commentReturnPath);
      return;
    }

    setReplyTo(target);
    setCommentFocusKey(`${commentReturnPath ?? postId}:reply:${target.targetCommentId}:${Date.now()}`);
  }, [commentReturnPath, isAuthenticated, postId, redirectToDetailLogin]);

  const handleRequireContentRewardLogin = useCallback((
    targetType: ContentRewardTargetType,
    targetId: LongId,
  ) => {
    if (!post) {
      return;
    }

    redirectToDetailLogin(buildPublicForumPostReturnPath({
      postId: String(post.voId),
      postPublicId: post.voPublicId,
      ...(targetType === 'Comment' ? { commentId: String(targetId) } : {}),
      intent: 'reward',
    }));
  }, [post, redirectToDetailLogin]);

  const handleSetBookmarkState = useCallback(async (isBookmarked: boolean) => {
    if (!post) {
      return;
    }
    if (!isAuthenticated) {
      redirectToDetailLogin(bookmarkReturnPath);
      return;
    }

    const postPublicId = post.voPublicId?.trim().toLowerCase();
    if (!postPublicId) {
      toast.error(t('forum.postDetail.bookmark.stateConflict'));
      return;
    }

    const targetPostId = String(post.voId);
    setBookmarkLoading(true);
    try {
      const state = await setMyPostBookmarkState({
        postIdentifier: postPublicId,
        isBookmarked,
      }, t);
      setPost((current) => (
        current && String(current.voId) === targetPostId
          ? {
              ...current,
              voIsBookmarked: state.voIsBookmarked,
              voCollectCount: state.voCollectCount,
            }
          : current
      ));
      toast.success(state.voIsBookmarked
        ? t('forum.postDetail.bookmark.added')
        : t('forum.postDetail.bookmark.removed'));
    } catch (error) {
      log.warn('PublicForumDetail', '帖子收藏状态写入失败', error);
      if (
        error instanceof ApiResponseError
        && error.code === PostBookmarkErrorCode.AuthenticationRequired
      ) {
        redirectToDetailLogin(bookmarkReturnPath);
        return;
      }

      const message = error instanceof ApiResponseError
        ? {
            [PostBookmarkErrorCode.PostNotFound]: t('forum.postDetail.bookmark.postNotFound'),
            [PostBookmarkErrorCode.PostUnavailable]: t('forum.postDetail.bookmark.postUnavailable'),
            [PostBookmarkErrorCode.UserUnavailable]: t('forum.postDetail.bookmark.userUnavailable'),
            [PostBookmarkErrorCode.StateConflict]: t('forum.postDetail.bookmark.stateConflict'),
          }[error.code ?? '']
        : null;
      toast.error(message ?? t('forum.postDetail.bookmark.error'));
    } finally {
      setBookmarkLoading(false);
    }
  }, [
    bookmarkReturnPath,
    isAuthenticated,
    post,
    redirectToDetailLogin,
    t,
  ]);

  const navigateToComment = useCallback(async (
    targetCommentId: LongId,
    navigationKey: string
  ) => {
    try {
      const resolvedPostId = post?.voId ?? postId;
      setCommentPagingError(null);
      setCommentNavigationNotice(null);

      const navigation = await getCommentNavigation(
        resolvedPostId,
        targetCommentId,
        commentPageSize,
        COMMENT_NAVIGATION_CHILD_PAGE_SIZE,
        t
      );

      let nextComments = comments;

      if (
        loadedCommentPages !== navigation.voRootPageIndex
        || !comments.some((item) => isSameLongId(item.voId, navigation.voRootCommentId))
      ) {
        const rootComments = await getRootCommentsPage(
          resolvedPostId,
          navigation.voRootPageIndex,
          commentPageSize,
          commentSortBy || 'default',
          t
        );

        nextComments = rootComments.voItems ?? [];
        syncCountedRootComments(nextComments);
        setComments(nextComments);
        setCommentTotal(rootComments.voTotal ?? 0);
        syncPostCommentCount(rootComments.voTotal);
        setLoadedCommentPages((rootComments.voItems?.length ?? 0) > 0 ? (rootComments.voPageIndex ?? navigation.voRootPageIndex) : 0);
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
          source.findIndex((item) => isSameLongId(item.voId, child.voId)) === index
        );
        nextComments = mergeCommentChildren(
          nextComments,
          navigation.voParentCommentId,
          deduplicatedChildren,
          totalChildren
        );
        setComments(nextComments);
      }

      setCommentNavigationTarget({
        commentId: navigation.voCommentId,
        expandedRootCommentId: navigation.voIsRootComment
          ? undefined
          : navigation.voParentCommentId ?? navigation.voRootCommentId,
        navigationKey
      });
    } catch {
      setCommentNavigationNotice(t('forum.commentNavigation.notice'));
    }
  }, [
    commentPageSize,
    commentSortBy,
    comments,
    loadedCommentPages,
    post?.voId,
    postId,
    syncCountedRootComments,
    syncPostCommentCount,
    t
  ]);

  const handleLoadMoreComments = async () => {
    if (loadingMoreComments || loadingComments || comments.length >= commentTotal) {
      return;
    }

    setLoadingMoreComments(true);
    setCommentPagingError(null);
    try {
      const nextPage = loadedCommentPages + 1;
      const pageData = await getRootCommentsPage(post?.voId ?? postId, nextPage, commentPageSize, commentSortBy || 'default', t);
      const nextItems = pageData.voItems ?? [];
      registerLoadedRootComments(nextItems);

      setComments((current) => {
        const existingIds = new Set(current.map((item) => item.voId));
        const appended = nextItems.filter((item) => !existingIds.has(item.voId));
        return [...current, ...appended];
      });
      setCommentTotal((current) => pageData.voTotal ?? current);
      syncPostCommentCount(pageData.voTotal);
      if (nextItems.length > 0) {
        setLoadedCommentPages(nextPage);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setCommentPagingError(message);
    } finally {
      setLoadingMoreComments(false);
    }
  };

  const handleLoadMoreChildren = async (
    parentId: LongId,
    pageIndex: number,
    pageSize: number
  ): Promise<CommentNode[]> => {
    try {
      setCommentPagingError(null);
      const result = await getChildComments(parentId, pageIndex, pageSize, t);
      const items = result.voItems ?? [];
      void loadCommentReactions(collectForumCommentIds(items));
      return items;
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setCommentPagingError(message);
      return [];
    }
  };

  const handleQuickReplyAction = useCallback(() => {
    if (!quickReplyReturnPath) {
      return;
    }

    if (!isAuthenticated) {
      redirectToDetailLogin(quickReplyReturnPath);
      return;
    }

    setQuickReplyFocusKey(`${quickReplyReturnPath}:${Date.now()}`);
  }, [isAuthenticated, quickReplyReturnPath, redirectToDetailLogin]);

  const handleCommentAction = useCallback(() => {
    if (!commentReturnPath) {
      return;
    }

    if (!isAuthenticated) {
      redirectToDetailLogin(commentReturnPath);
      return;
    }

    setCommentFocusKey(`${commentReturnPath}:${Date.now()}`);
  }, [commentReturnPath, isAuthenticated, redirectToDetailLogin]);

  const loadCategoriesForEdit = useCallback(async (): Promise<Category[]> => {
    if (categories.length > 0) {
      return categories;
    }

    setCategoriesLoading(true);
    setCategoriesError(null);
    try {
      const result = await getTopCategories(t);
      setCategories(result);
      return result;
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setCategoriesError(message);
      throw error;
    } finally {
      setCategoriesLoading(false);
    }
  }, [categories, t]);

  const handleAnswerAction = useCallback(() => {
    if (!post?.voId || !answerReturnPath) {
      return;
    }

    if (!post.voIsQuestion) {
      toast.info(t('forum.public.answerQuestionOnly'));
      return;
    }

    if (!isAuthenticated) {
      redirectToDetailLogin(answerReturnPath);
      return;
    }

    setAnswerFocusKey(`${answerReturnPath}:${Date.now()}`);
  }, [answerReturnPath, isAuthenticated, post?.voId, post?.voIsQuestion, redirectToDetailLogin, t]);

  const handleEditPostAction = useCallback(async () => {
    if (!post?.voId || !editReturnPath) {
      return;
    }

    if (!isAuthenticated) {
      redirectToDetailLogin(editReturnPath);
      return;
    }

    if (!isCurrentUserAuthor) {
      toast.error(t('forum.public.authorOnlyAction'));
      return;
    }

    try {
      await loadCategoriesForEdit();
      setPostRevisionDraft(null);
      setIsEditModalOpen(true);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      toast.error(message || t('forum.public.composeCategoriesErrorDescription'));
    }
  }, [
    editReturnPath,
    isAuthenticated,
    isCurrentUserAuthor,
    loadCategoriesForEdit,
    post?.voId,
    redirectToDetailLogin,
    t
  ]);

  const handleViewPostHistory = useCallback(async () => {
    if (!post?.voId) {
      return;
    }

    setIsPostHistoryOpen(true);
  }, [
    post?.voId,
  ]);

  const handleSavePostEdit = useCallback(async (
    targetPostId: LongId,
    title: string,
    content: string,
    categoryId: LongId,
    tagNames: string[]
  ) => {
    if (!post || !isSameLongId(post.voId, targetPostId)) {
      throw new Error(t('forum.public.postEditFailed'));
    }

    const expectedContentRevision = post.voContentRevision;
    const normalizedTagNames = normalizeForumTagNames(tagNames);
    const submissionState = createClientSubmissionState(
      postEditSubmissionRef.current,
      'forum-post-edit',
      buildPostEditSubmissionFingerprint(
        targetPostId,
        title,
        content,
        categoryId,
        normalizedTagNames,
        expectedContentRevision
      )
    );
    postEditSubmissionRef.current = submissionState;

    try {
      await updatePost({
        postId: targetPostId,
        title,
        content,
        clientSubmissionId: submissionState.clientSubmissionId,
        categoryId,
        tagNames: normalizedTagNames,
        expectedContentRevision
      }, t);
      postEditSubmissionRef.current = null;
      setIsEditModalOpen(false);
      toast.success(t('forum.public.postEditSaved'));
      setReloadToken((current) => current + 1);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      toast.error(message || t('forum.public.postEditFailed'));
      throw error;
    }
  }, [post, t]);

  const handleEditComment = useCallback(async (
    targetCommentId: LongId,
    content: string,
    expectedContentRevision: number
  ) => {
    const normalizedContent = content.trim();
    const submissionState = createClientSubmissionState(
      commentEditSubmissionRef.current,
      'forum-comment-edit',
      buildCommentEditSubmissionFingerprint(
        targetCommentId,
        normalizedContent,
        expectedContentRevision
      )
    );
    commentEditSubmissionRef.current = submissionState;

    try {
      await updateComment({
        commentId: targetCommentId,
        content: normalizedContent,
        expectedContentRevision,
        clientSubmissionId: submissionState.clientSubmissionId,
      }, t);
      commentEditSubmissionRef.current = null;
      setCommentRevisionDraft(null);
      toast.success(t('forum.comment.editSuccess'));
      setReloadToken((current) => current + 1);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      toast.error(message || t('forum.comment.editFailedRetry'));
      throw error;
    }
  }, [t]);

  const handleViewCommentRevisions = useCallback((targetCommentId: LongId) => {
    setActiveCommentRevisionId(targetCommentId);
  }, []);

  const handleRevisionRestored = useCallback(() => {
    setPostRevisionDraft(null);
    setCommentRevisionDraft(null);
    setReloadToken((current) => current + 1);
    toast.success(t('forum.revision.restoreSuccess'));
  }, [t]);

  const handleUsePostRevision = useCallback(async (detail: PostContentRevisionDetailVo) => {
    try {
      await loadCategoriesForEdit();
      setPostRevisionDraft(detail);
      setIsEditModalOpen(true);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      toast.error(message || t('forum.public.composeCategoriesErrorDescription'));
    }
  }, [loadCategoriesForEdit, t]);

  useEffect(() => {
    if (!post?.voId || (intent !== 'answer' && intent !== 'edit' && intent !== 'history')) {
      return;
    }

    const signature = `${post.voId}:${intent}:${isAuthenticated ? currentUserId || 'auth' : 'guest'}`;
    if (handledAuthorIntentRef.current === signature) {
      return;
    }

    handledAuthorIntentRef.current = signature;

    if (intent === 'answer') {
      handleAnswerAction();
      return;
    }

    if (intent === 'edit') {
      void handleEditPostAction();
      return;
    }

    void handleViewPostHistory();
  }, [
    currentUserId,
    handleAnswerAction,
    handleEditPostAction,
    handleViewPostHistory,
    intent,
    isAuthenticated,
    post?.voId
  ]);

  const handleCreateQuickReply = useCallback(async (content: string) => {
    if (!post?.voId) {
      throw new Error(t('forum.public.postNotFoundTitle'));
    }

    const normalizedContent = content.trim().replace(/\s+/g, ' ');
    if (!normalizedContent) {
      return;
    }

    const quickReply = await createPostQuickReply(
      {
        postId: post.voId,
        content: normalizedContent
      },
      t
    );

    setQuickReplies((current) => {
      const next = [
        quickReply,
        ...current.filter((item) => !isSameLongId(item.voId, quickReply.voId))
      ];
      return next.slice(0, 30);
    });
    setQuickReplyTotal((current) => current + 1);
  }, [post?.voId, t]);

  const handleCreateComment = useCallback(async (content: string) => {
    const normalizedContent = content.trim();
    if (!normalizedContent || submittingComment) {
      return;
    }

    if (!post?.voId) {
      toast.error(t('forum.public.postNotFoundTitle'));
      return;
    }

    if (!isAuthenticated) {
      redirectToDetailLogin(commentReturnPath);
      return;
    }

    setSubmittingComment(true);
    try {
      const submissionState = createClientSubmissionState(
        commentSubmissionRef.current,
        'forum-comment',
        buildCommentSubmissionFingerprint(post.voId, normalizedContent, replyTo)
      );
      commentSubmissionRef.current = submissionState;

      const createdCommentId = await createComment(
        {
          postId: post.voId,
          content: normalizedContent,
          clientSubmissionId: submissionState.clientSubmissionId,
          parentId: replyTo?.parentCommentId ?? null,
          replyToCommentId: replyTo?.targetCommentId ?? null,
          replyToCommentSnapshot: replyTo?.contentSnapshot ?? null,
          replyToUserId: null,
          replyToUserName: replyTo?.authorName ?? null
        },
        t
      );
      commentSubmissionRef.current = null;
      const now = new Date().toISOString();
      const newComment: CommentNode = {
        voId: createdCommentId,
        voPostId: post.voId,
        voContent: normalizedContent,
        voContentRevision: 1,
        voAuthorId: currentUserId || '0',
        voAuthorName: currentUserName?.trim() || t('common.unknownUser'),
        voAuthorAvatarUrl: currentUserAvatarUrl,
        voParentId: replyTo?.parentCommentId ?? null,
        voRootId: replyTo?.parentCommentId ?? null,
        voReplyToCommentId: replyTo?.targetCommentId ?? null,
        voReplyToCommentSnapshot: replyTo?.contentSnapshot ?? null,
        voReplyToUserId: null,
        voReplyToUserName: replyTo?.authorName ?? null,
        voLevel: replyTo ? 1 : 0,
        voLikeCount: 0,
        voIsLiked: false,
        voCreateTime: now,
        voChildren: [],
        voChildrenTotal: 0,
        voIsGodComment: false,
        voIsSofa: false
      };

      const shouldIncrementTotal = registerRootCommentCount(newComment.voId, newComment.voParentId);

      setComments((current) => upsertCommentInTree(current, newComment, commentSortBy));

      if (shouldIncrementTotal) {
        setCommentTotal((total) => total + 1);
        applyPostCommentCountDelta(1);
      }
      setReplyTo(null);
      setCommentNavigationTarget({
        commentId: createdCommentId,
        expandedRootCommentId: replyTo?.parentCommentId,
        navigationKey: `${post.voId}:${createdCommentId}:created:${Date.now()}`
      });
      toast.success(t('forum.comment.submitSuccess'));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t('forum.comment.submitFailed'));
    } finally {
      setSubmittingComment(false);
    }
  }, [
    commentReturnPath,
    commentSortBy,
    currentUserAvatarUrl,
    currentUserId,
    currentUserName,
    isAuthenticated,
    post?.voId,
    replyTo,
    registerRootCommentCount,
    applyPostCommentCountDelta,
    redirectToDetailLogin,
    submittingComment,
    t
  ]);

  const handleCommentTyping = useCallback(() => {
    if (!post?.voId || !isAuthenticated) {
      return;
    }

    void commentHub.startTyping(post.voId);
  }, [isAuthenticated, post?.voId]);

  const detailState = resolvePublicForumDetailLoadState({
    loadingPost,
    hasPost: !!post,
    postError,
    postNotFound
  });
  const quickReplySectionState = resolvePublicForumReadSectionState({
    loading: loadingQuickReplies,
    error: quickReplyError,
    itemCount: quickReplies.length,
    totalCount: quickReplyTotal
  });
  const commentSectionState = resolvePublicForumReadSectionState({
    loading: loadingComments,
    error: commentError,
    itemCount: comments.length,
    totalCount: commentTotal
  });
  const commentSortLabel = commentSortBy === 'newest'
    ? t('forum.sort.newest')
    : commentSortBy === 'hottest'
      ? t('forum.sort.hottest')
      : t('forum.public.commentSortDefault');
  const actionLinks = [
    ...(post?.voIsQuestion && answerReturnPath ? [{
      href: answerReturnPath,
      label: isAuthenticated ? t('forum.public.workspaceAnswerAction') : t('forum.public.workspaceAnswerLoginAction'),
      icon: 'mdi:comment-question-outline',
      onActivate: handleAnswerAction,
    }] : []),
    ...(quickReplyReturnPath ? [{
      href: quickReplyReturnPath,
      label: isAuthenticated ? t('forum.public.workspaceQuickReplyAction') : t('forum.public.workspaceQuickReplyLoginAction'),
      icon: 'mdi:message-flash-outline',
      onActivate: handleQuickReplyAction,
    }] : []),
    ...(commentReturnPath ? [{
      href: commentReturnPath,
      label: isAuthenticated ? t('forum.public.workspaceCommentAction') : t('forum.public.workspaceCommentLoginAction'),
      icon: 'mdi:comment-text-outline',
      onActivate: handleCommentAction,
      primary: true,
    }] : []),
    ...(editReturnPath && isAuthenticated && isCurrentUserAuthor ? [{
      href: editReturnPath,
      label: categoriesLoading ? t('forum.public.authorCategoriesLoading') : t('forum.public.workspaceEditAction'),
      icon: 'mdi:pencil-outline',
      onActivate: () => void handleEditPostAction(),
      disabled: categoriesLoading,
    }] : []),
    ...(historyReturnPath && isAuthenticated && isCurrentUserAuthor ? [{
      href: historyReturnPath,
      label: t('forum.public.workspaceHistoryAction'),
      icon: 'mdi:history',
      onActivate: () => void handleViewPostHistory(),
    }] : []),
  ];

  return (
    <PublicForumDetailView
      detailState={detailState}
      post={post}
      backLabel={backLabel}
      backHref={backHref}
      onBack={handleBackWhileEditorIdle}
      navigationLocked={isAnswerEditorUploading}
      shareBusy={shareBusy}
      shareState={shareState}
      onCopyShareLink={() => void copyShareLink()}
      onRetry={() => setReloadToken((current) => current + 1)}
      quickReplyTotal={quickReplyTotal}
      commentTotal={commentTotal}
      postDetailProps={{
        displayTimeZone,
        density: 'compact',
        mode: 'interactive',
        isAuthenticated,
        currentUserId: currentUserId || '0',
        isLiked: isPostLiked,
        onLike: (targetPostId) => void handleLikePost(targetPostId),
        isBookmarked: post?.voIsBookmarked ?? false,
        bookmarkLoading,
        onSetBookmarkState: handleSetBookmarkState,
        showSectionTitle: false,
        postTitleHeadingLevel: 1,
        postReactions: postReactionItems,
        reactionLoading: loadingPostReactions || Boolean(post?.voId && isReactionPending('Post', post.voId)),
        onToggleReaction: async (payload) => {
          if (post?.voId) {
            await togglePostReaction(post.voId, payload);
          }
        },
        onRequireReactionLogin: handleRequireReactionLogin,
        questionAnswerSection: post?.voIsQuestion ? (
          <PostAnswerLifecycleSection
            postIdentifier={getForumPostRouteIdentifier(post)}
            answerPage={answerPageState.answerPage}
            loading={answerPageState.loading}
            error={answerPageState.error}
            sort={answerSort}
            pageIndex={Math.max(1, answerPageIndex)}
            targetAnswerPublicId={answerPublicId}
            targetUnavailable={answerPageState.targetUnavailable}
            isAuthenticated={isAuthenticated}
            isQuestionAuthor={isCurrentUserAuthor}
            currentUserId={currentUserId || '0'}
            displayTimeZone={displayTimeZone}
            autoFocusKey={answerAutoFocusKey}
            onRequireLogin={() => redirectToDetailLogin(answerReturnPath)}
            onPageChange={(nextPage) => onAnswerStateChange(nextPage, answerSort)}
            onSortChange={(nextSort) => onAnswerStateChange(1, nextSort)}
            onReload={() => setAnswerReloadToken((current) => current + 1)}
            onAuthorClick={(userId) => handleOpenAuthorProfileWhileEditorIdle(String(userId))}
            onReport={(targetId) => handleOpenReport('PostAnswer', targetId)}
            onUploadingChange={onAnswerEditorUploadingChange}
          />
        ) : undefined,
        onEdit: () => void handleEditPostAction(),
        onViewHistory: () => void handleViewPostHistory(),
        onAuthorClick: (userId) => handleOpenAuthorProfileWhileEditorIdle(String(userId)),
        resolveAuthorProfileId: resolvePublicProfileUserId,
        onTagClick: (_, tagSlug) => handleOpenTagWhileEditorIdle(tagSlug),
        onQuestionClick: handleOpenQuestionWhileEditorIdle,
        onPollClick: handleOpenPollWhileEditorIdle,
        onLotteryClick: handleOpenLotteryWhileEditorIdle,
        onReport: (targetId) => handleOpenReport('Post', targetId),
        contentRewardState: post
          ? contentRewardStateMap[buildContentRewardTargetKey('Post', post.voId)]
          : undefined,
        onContentRewardStateChange: handleContentRewardStateChange,
        onRequireContentRewardLogin: () => {
          if (post?.voId) {
            handleRequireContentRewardLogin('Post', post.voId);
          }
        },
      }}
      actionLinks={actionLinks}
      categoriesError={categoriesError}
      quickReplySectionState={quickReplySectionState}
      quickReplyError={quickReplyError}
      quickReplies={quickReplies}
      quickReplyProps={{
        loading: loadingQuickReplies,
        density: 'compact',
        isAuthenticated,
        currentUserId: currentUserId || '0',
        titleHeadingLevel: 2,
        onCreate: handleCreateQuickReply,
        loginPromptText: t('forum.public.quickReplyLoginPrompt'),
        loginButtonText: t('forum.public.workspaceQuickReplyLoginAction'),
        loginReturnPath: quickReplyReturnPath,
        onLoginRequired: redirectToDetailLogin,
        autoFocusComposerKey: quickReplyAutoFocusKey,
        onReport: (targetId) => handleOpenReport('PostQuickReply', targetId),
      }}
      commentSectionState={commentSectionState}
      commentError={commentError}
      commentPagingError={commentPagingError}
      commentNavigationNotice={commentNavigationNotice}
      commentNoticeRef={commentNoticeRef}
      commentTypingText={commentTypingText}
      loadedCommentCount={comments.length}
      commentSortLabel={commentSortLabel}
      commentComposerProps={{
        isAuthenticated,
        hasPost: Boolean(post?.voId),
        onSubmit: (content) => void handleCreateComment(content),
        disabled: submittingComment,
        replyTo,
        onCancelReply: () => setReplyTo(null),
        variant: 'inline',
        title: t('forum.joinDiscussion'),
        submitText: t('forum.submitDiscussion'),
        placeholder: t('forum.discussionPlaceholder'),
        loginPromptText: t('forum.public.commentLoginPrompt'),
        loginButtonText: t('forum.public.workspaceCommentLoginAction'),
        loginReturnPath: commentReturnPath,
        onLoginRequired: redirectToDetailLogin,
        onTyping: handleCommentTyping,
        autoFocusKey: commentAutoFocusKey,
      }}
      commentTreeProps={{
        comments,
        loading: loadingComments,
        loadingMoreRootComments: loadingMoreComments,
        hasPost: true,
        displayTimeZone,
        currentUserId: currentUserId || '0',
        highlightedCommentId,
        expandedRootCommentId: commentNavigationTarget?.expandedRootCommentId,
        rootCommentTotal: commentTotal,
        loadedRootCommentCount: comments.length,
        rootCommentPageSize: commentPageSize,
        registerCommentAnchor,
        sortBy: commentSortBy,
        onSortChange: setCommentSortBy,
        onLikeComment: handleLikeComment,
        onReplyComment: handleReplyComment,
        onLoadMoreChildren: handleLoadMoreChildren,
        onLoadMoreRootComments: handleLoadMoreComments,
        onEditComment: handleEditComment,
        onViewCommentHistory: handleViewCommentRevisions,
        commentRevisionDraft,
        reactionMap: commentItemsMap,
        isAuthenticated,
        onToggleReaction: toggleCommentReaction,
        isReactionPending: (targetCommentId) => isReactionPending('Comment', targetCommentId),
        onRequireReactionLogin: handleRequireReactionLogin,
        showTitle: false,
        density: 'compact',
        onAuthorClick: (userId) => handleOpenAuthorProfileWhileEditorIdle(String(userId)),
        resolveAuthorProfileId: resolvePublicProfileUserId,
        onNavigateToComment: (targetCommentId) => void navigateToComment(
          targetCommentId,
          `inline:${postId}:${targetCommentId}:${Date.now()}`
        ),
        onReportComment: (targetId) => handleOpenReport('Comment', targetId),
        contentRewardStateMap,
        onContentRewardStateChange: handleContentRewardStateChange,
        onContentRewardTargetsVisible: handleContentRewardTargetsVisible,
        onRequireContentRewardLogin: handleRequireContentRewardLogin,
      }}
      dialogs={(
        <>
          {isEditModalOpen && (
            <Suspense fallback={null}>
              <EditPostModal
                isOpen={isEditModalOpen}
                post={post}
                revisionDraft={postRevisionDraft}
                categories={categories}
                onClose={() => {
                  setPostRevisionDraft(null);
                  setIsEditModalOpen(false);
                }}
                onSave={async (...args) => {
                  await handleSavePostEdit(...args);
                  setPostRevisionDraft(null);
                }}
              />
            </Suspense>
          )}
          {isPostHistoryOpen && (
            <Suspense fallback={null}>
              <ContentRevisionModal
                isOpen={isPostHistoryOpen}
                target={postRevisionTarget}
                sessionKey={isAuthenticated ? String(currentUserId || '0') : 'anonymous'}
                onClose={() => setIsPostHistoryOpen(false)}
                onRestored={handleRevisionRestored}
                onUseInEditor={(detail) => {
                  if ('voTitle' in detail) {
                    void handleUsePostRevision(detail);
                  }
                }}
              />
            </Suspense>
          )}
          {activeCommentRevisionId && (
            <Suspense fallback={null}>
              <ContentRevisionModal
                isOpen={true}
                target={commentRevisionTarget}
                sessionKey={isAuthenticated ? String(currentUserId || '0') : 'anonymous'}
                onClose={() => setActiveCommentRevisionId(null)}
                onRestored={handleRevisionRestored}
                onUseInEditor={(detail) => {
                  if (!('voTitle' in detail)) {
                    setCommentRevisionDraft(detail);
                  }
                }}
              />
            </Suspense>
          )}
          {reportTarget && (
            <Suspense fallback={null}>
              <ContentReportModal
                isOpen={true}
                targetType={reportTarget.targetType}
                targetId={reportTarget.targetId}
                onClose={() => setReportTarget(null)}
              />
            </Suspense>
          )}
        </>
      )}
    />
  );
};
