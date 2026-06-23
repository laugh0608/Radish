import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { toast } from '@radish/ui/toast';
import { resolveVisibleUserDisplayName } from '@/utils/userIdentityDisplay';
import {
  acceptQuestionAnswer,
  answerQuestion,
  createComment,
  createPostQuickReply,
  getPostEditHistory,
  getCommentNavigation,
  getChildComments,
  getPostById,
  getPostQuickReplyWall,
  getRootCommentsPage,
  getTopCategories,
  updatePost,
  type Category,
  type CommentNode,
  type PostEditHistory,
  type PostDetail,
  type PostQuickReply,
} from '@/api/forum';
import type { LongId } from '@/api/user';
import { buildPublicForumPostReturnPath } from '@/services/authReturnPath';
import { redirectToLogin } from '@/services/auth';
import { commentHub, type CommentTypingRealtimeEvent } from '@/services/commentHub';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { log } from '@/utils/logger';
import { resolveMediaUrl } from '@/utils/media';
import {
  createClientSubmissionState,
  type ClientSubmissionState
} from '@/utils/clientSubmission';
import { CommentTree } from '@/apps/forum/components/CommentTree';
import { CreateCommentForm } from '@/apps/forum/components/CreateCommentForm';
import { PostDetail as ForumPostDetail } from '@/apps/forum/components/PostDetail';
import { PostQuickReplyWall } from '@/apps/forum/components/PostQuickReplyWall';
import {
  buildAnswerSubmissionFingerprint,
  buildCommentSubmissionFingerprint,
  buildPostEditSubmissionFingerprint,
} from '@/apps/forum/utils/forumSubmissionFingerprint';
import {
  applyCommentHighlightEvent,
  removeCommentFromTree,
  updateCommentLikeCount,
  upsertCommentInTree
} from '@/apps/forum/utils/commentRealtimeTree';
import { buildPublicForumPath, type PublicForumDetailIntent } from '../forumRouteState';
import {
  rememberPublicRouteSourceTransfer,
  type PublicRouteSourceState,
} from '../publicRouteNavigation';
import { applyPublicHead, buildPublicShareUrl } from '../publicHead';
import {
  applyPublicStructuredData,
  buildForumPostStructuredData,
  removePublicStructuredData,
} from '../publicStructuredData';
import { PublicReadingGuide } from '../components/PublicReadingGuide';
import {
  resolvePublicForumDetailLoadState,
  resolvePublicForumReadSectionState,
} from './publicForumViewState';
import { usePublicShareLink } from '../hooks/usePublicShareLink';
import { PublicStatusCard } from './PublicStatusCard';
import {
  createForumReadingGuide,
  buildForumPostPublicHead,
  detailGuideDefinition,
  getForumPostRouteIdentifier,
  isSameLongId,
  mergeCommentChildren,
  resolvePublicProfileUserId,
} from './publicForumUtils';
import { handlePublicForumLinkClick } from './publicForumLinkHandlers';
import styles from './PublicForumApp.module.css';

type RootCommentSort = 'newest' | 'hottest' | null;
const COMMENT_NAVIGATION_CHILD_PAGE_SIZE = 5;
const QUICK_REPLY_SECTION_ID = 'public-forum-quick-replies';
const COMMENT_SECTION_ID = 'public-forum-comments';

const EditPostModal = lazy(() =>
  import('@/apps/forum/components/EditPostModal').then((module) => ({ default: module.EditPostModal }))
);

const EditHistoryModal = lazy(() =>
  import('@/apps/forum/components/EditHistoryModal').then((module) => ({ default: module.EditHistoryModal }))
);

const buildRootCommentIdSet = (rootComments: CommentNode[]): Set<string> => (
  new Set(rootComments.map((comment) => String(comment.voId)))
);

interface PublicForumCommentNavigationTarget {
  commentId: LongId;
  expandedRootCommentId?: LongId;
  navigationKey: string;
}

interface PublicForumDetailProps {
  postId: string;
  commentId?: string;
  intent?: PublicForumDetailIntent;
  sourceState?: PublicRouteSourceState | null;
  displayTimeZone: string;
  backLabel: string;
  backHref: string;
  onBack: () => void;
  onOpenAuthorProfile?: (userId: string) => void;
  onOpenTag?: (tagSlug: string) => void;
  onOpenQuestion?: () => void;
  onOpenPoll?: () => void;
  onOpenLottery?: () => void;
}

export const PublicForumDetail = ({
  postId,
  commentId,
  intent,
  sourceState,
  displayTimeZone,
  backLabel,
  backHref,
  onBack,
  onOpenAuthorProfile,
  onOpenTag,
  onOpenQuestion,
  onOpenPoll,
  onOpenLottery
}: PublicForumDetailProps) => {
  const { t, i18n } = useTranslation();
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
  const [postError, setPostError] = useState<string | null>(null);
  const [commentError, setCommentError] = useState<string | null>(null);
  const [quickReplyError, setQuickReplyError] = useState<string | null>(null);
  const [commentPagingError, setCommentPagingError] = useState<string | null>(null);
  const [commentNavigationTarget, setCommentNavigationTarget] = useState<PublicForumCommentNavigationTarget | null>(null);
  const [commentNavigationNotice, setCommentNavigationNotice] = useState<string | null>(null);
  const [commentTypingUserNames, setCommentTypingUserNames] = useState<string[]>([]);
  const [highlightedCommentId, setHighlightedCommentId] = useState<LongId | null>(null);
  const [submittingComment, setSubmittingComment] = useState(false);
  const [quickReplyFocusKey, setQuickReplyFocusKey] = useState<string | null>(null);
  const [commentFocusKey, setCommentFocusKey] = useState<string | null>(null);
  const [answerFocusKey, setAnswerFocusKey] = useState<string | null>(null);
  const [categories, setCategories] = useState<Category[]>([]);
  const [categoriesLoading, setCategoriesLoading] = useState(false);
  const [categoriesError, setCategoriesError] = useState<string | null>(null);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isPostHistoryOpen, setIsPostHistoryOpen] = useState(false);
  const [postHistories, setPostHistories] = useState<PostEditHistory[]>([]);
  const [postHistoryTotal, setPostHistoryTotal] = useState(0);
  const [postHistoryPageIndex, setPostHistoryPageIndex] = useState(1);
  const [postHistoryLoading, setPostHistoryLoading] = useState(false);
  const [postHistoryError, setPostHistoryError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const requestIdRef = useRef(0);
  const commentAnchorMapRef = useRef(new Map<string, HTMLDivElement>());
  const handledCommentNavigationRef = useRef<string | null>(null);
  const handledAuthorIntentRef = useRef<string | null>(null);
  const highlightTimeoutRef = useRef<number | null>(null);
  const commentTypingUsersRef = useRef(new Map<string, string>());
  const commentTypingTimersRef = useRef(new Map<string, number>());
  const commentNoticeRef = useRef<HTMLDivElement | null>(null);
  const countedRootCommentIdsRef = useRef(new Set<string>());
  const deletedRootCommentIdsRef = useRef(new Set<string>());
  const commentSubmissionRef = useRef<ClientSubmissionState | null>(null);
  const answerSubmissionRef = useRef<ClientSubmissionState | null>(null);
  const postEditSubmissionRef = useRef<ClientSubmissionState | null>(null);
  const commentPageSize = 20;
  const postHistoryPageSize = 10;
  const isAuthenticated = authStoreAuthenticated && isUserAuthenticated();
  const isCurrentUserAuthor = !!post && !!currentUserId && isSameLongId(post.voAuthorId, currentUserId);

  const normalizeTagNames = useCallback((tagNames: string[]): string[] => {
    const normalized: string[] = [];
    const seen = new Set<string>();

    for (const tagName of tagNames) {
      const trimmed = tagName.trim();
      if (!trimmed) {
        continue;
      }

      const normalizedKey = trimmed.toLowerCase();
      if (seen.has(normalizedKey)) {
        continue;
      }

      seen.add(normalizedKey);
      normalized.push(trimmed);
    }

    return normalized;
  }, []);

  const syncCountedRootComments = useCallback((rootComments: CommentNode[]) => {
    countedRootCommentIdsRef.current = buildRootCommentIdSet(rootComments);
    deletedRootCommentIdsRef.current.clear();
  }, []);

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

  const registerRootCommentCount = useCallback((commentId: LongId, parentCommentId?: LongId | null): boolean => {
    if (parentCommentId) {
      return false;
    }

    const commentKey = String(commentId);
    if (countedRootCommentIdsRef.current.has(commentKey)) {
      return false;
    }

    countedRootCommentIdsRef.current.add(commentKey);
    deletedRootCommentIdsRef.current.delete(commentKey);
    return true;
  }, []);

  const registerRootCommentRemoval = useCallback((commentId: LongId, parentCommentId?: LongId | null): boolean => {
    if (parentCommentId) {
      return false;
    }

    const commentKey = String(commentId);
    if (deletedRootCommentIdsRef.current.has(commentKey)) {
      return false;
    }

    deletedRootCommentIdsRef.current.add(commentKey);
    countedRootCommentIdsRef.current.delete(commentKey);
    return true;
  }, []);

  const syncCommentTypingUsers = useCallback(() => {
    setCommentTypingUserNames([...commentTypingUsersRef.current.values()]);
  }, []);

  const clearCommentTypingUsers = useCallback(() => {
    for (const timer of commentTypingTimersRef.current.values()) {
      window.clearTimeout(timer);
    }

    commentTypingTimersRef.current.clear();
    commentTypingUsersRef.current.clear();
    setCommentTypingUserNames([]);
  }, []);

  const registerCommentTypingUser = useCallback((payload: CommentTypingRealtimeEvent) => {
    if (!post?.voId || !isSameLongId(payload.voPostId, post.voId)) {
      return;
    }

    const userKey = String(payload.voUserId);
    const userName = resolveVisibleUserDisplayName({ voUserName: payload.voUserName }, t('common.unknownUser'));
    const oldTimer = commentTypingTimersRef.current.get(userKey);
    if (oldTimer) {
      window.clearTimeout(oldTimer);
    }

    commentTypingUsersRef.current.set(userKey, userName);
    commentTypingTimersRef.current.set(userKey, window.setTimeout(() => {
      commentTypingUsersRef.current.delete(userKey);
      commentTypingTimersRef.current.delete(userKey);
      syncCommentTypingUsers();
    }, 3200));
    syncCommentTypingUsers();
  }, [post?.voId, syncCommentTypingUsers, t]);

  const commentTypingText = useMemo(() => {
    if (commentTypingUserNames.length === 0) {
      return null;
    }

    const separator = i18n.language.startsWith('zh') ? '、' : ', ';
    return `${commentTypingUserNames.join(separator)}${t('forum.comment.typingSuffix')}`;
  }, [commentTypingUserNames, i18n.language, t]);

  useEffect(() => {
    const requestId = ++requestIdRef.current;

    const loadDetail = async () => {
      setLoadingPost(true);
      setLoadingComments(true);
      setLoadingQuickReplies(true);
      setPostError(null);
      setCommentError(null);
      setQuickReplyError(null);
      setCommentPagingError(null);
      setCommentNavigationTarget(null);
      setCommentNavigationNotice(null);
      clearCommentTypingUsers();
      setHighlightedCommentId(null);
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
    syncCountedRootComments,
    syncPostCommentCount,
    t
  ]);

  useEffect(() => {
    return () => {
      if (highlightTimeoutRef.current !== null) {
        window.clearTimeout(highlightTimeoutRef.current);
      }
    };
  }, []);

  useEffect(() => {
    const resolvedPostId = post?.voId;
    if (!resolvedPostId) {
      return;
    }

    void commentHub.joinPost(resolvedPostId);

    const unsubscribeCreated = commentHub.subscribe('CommentCreated', (payload) => {
      if (!isSameLongId(payload.voPostId, resolvedPostId) || !payload.voComment) {
        return;
      }

      const shouldIncrementTotal = registerRootCommentCount(
        payload.voComment.voId,
        payload.voComment.voParentId
      );

      setComments((current) => upsertCommentInTree(current, payload.voComment!, commentSortBy));

      if (shouldIncrementTotal) {
        setCommentTotal((total) => total + 1);
        applyPostCommentCountDelta(1);
      }
    });

    const unsubscribeUpdated = commentHub.subscribe('CommentUpdated', (payload) => {
      if (!isSameLongId(payload.voPostId, resolvedPostId) || !payload.voComment) {
        return;
      }

      setComments((current) => upsertCommentInTree(current, payload.voComment!, commentSortBy));
    });

    const unsubscribeDeleted = commentHub.subscribe('CommentDeleted', (payload) => {
      if (!isSameLongId(payload.voPostId, resolvedPostId)) {
        return;
      }

      const shouldDecrementTotal = registerRootCommentRemoval(
        payload.voCommentId,
        payload.voParentCommentId
      );

      setComments((current) => removeCommentFromTree(current, payload.voCommentId));

      if (shouldDecrementTotal) {
        setCommentTotal((total) => Math.max(0, total - 1));
        applyPostCommentCountDelta(-1);
      }
    });

    const unsubscribeLikeChanged = commentHub.subscribe('CommentLikeChanged', (payload) => {
      if (!isSameLongId(payload.voPostId, resolvedPostId) || typeof payload.voLikeCount !== 'number') {
        return;
      }

      setComments((current) => updateCommentLikeCount(current, payload.voCommentId, payload.voLikeCount!));
    });

    const unsubscribeHighlightsChanged = commentHub.subscribe('CommentHighlightsChanged', (payload) => {
      if (!isSameLongId(payload.voPostId, resolvedPostId)) {
        return;
      }

      setComments((current) => applyCommentHighlightEvent(current, payload));
    });
    const unsubscribeTyping = commentHub.subscribe('CommentTyping', registerCommentTypingUser);

    return () => {
      unsubscribeCreated();
      unsubscribeUpdated();
      unsubscribeDeleted();
      unsubscribeLikeChanged();
      unsubscribeHighlightsChanged();
      unsubscribeTyping();
      clearCommentTypingUsers();
      void commentHub.leavePost(resolvedPostId);
    };
  }, [
    applyPostCommentCountDelta,
    clearCommentTypingUsers,
    commentSortBy,
    post?.voId,
    registerCommentTypingUser,
    registerRootCommentCount,
    registerRootCommentRemoval
  ]);

  useEffect(() => {
    if (!post?.voTitle) {
      return;
    }

    document.title = `${post.voTitle} · ${t('desktop.apps.forum.name')}`;
  }, [post?.voTitle, t]);

  useEffect(() => {
    if (!post) {
      removePublicStructuredData();
      return;
    }

    const coverImageUrl = resolveMediaUrl(post.voCoverImage);
    const postHead = buildForumPostPublicHead(post, commentId, coverImageUrl);
    applyPublicHead(postHead);

    const structuredPost = {
      ...post,
      voCoverImage: coverImageUrl,
    };

    applyPublicStructuredData(buildForumPostStructuredData({
      post: structuredPost,
      canonicalPath: postHead.canonicalPath,
    }));

    return removePublicStructuredData;
  }, [commentId, post]);

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

  const registerCommentAnchor = (targetCommentId: LongId, element: HTMLDivElement | null) => {
    const targetCommentIdKey = String(targetCommentId);
    if (element) {
      commentAnchorMapRef.current.set(targetCommentIdKey, element);
    } else {
      commentAnchorMapRef.current.delete(targetCommentIdKey);
    }
  };

  useEffect(() => {
    if (!commentNavigationTarget?.commentId) {
      return;
    }

    const navigationSignature = `${commentNavigationTarget.navigationKey}:${commentNavigationTarget.commentId}`;
    if (handledCommentNavigationRef.current === navigationSignature) {
      return;
    }

    const targetElement = commentAnchorMapRef.current.get(String(commentNavigationTarget.commentId));
    if (!targetElement) {
      return;
    }

    handledCommentNavigationRef.current = navigationSignature;
    targetElement.scrollIntoView({
      behavior: 'smooth',
      block: 'center'
    });

    setHighlightedCommentId(commentNavigationTarget.commentId);
    if (highlightTimeoutRef.current !== null) {
      window.clearTimeout(highlightTimeoutRef.current);
    }
    highlightTimeoutRef.current = window.setTimeout(() => {
      setHighlightedCommentId((current) => (
        current === commentNavigationTarget.commentId
          ? null
          : current
      ));
    }, 3200);
  }, [commentNavigationTarget, comments]);

  useEffect(() => {
    if (!commentNavigationNotice) {
      return;
    }

    const frameId = window.requestAnimationFrame(() => {
      commentNoticeRef.current?.scrollIntoView({
        behavior: 'smooth',
        block: 'center'
      });
    });

    return () => {
      window.cancelAnimationFrame(frameId);
    };
  }, [commentNavigationNotice]);

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
      for (const item of nextItems) {
        countedRootCommentIdsRef.current.add(String(item.voId));
      }

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
      return result.voItems ?? [];
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

  const loadPostHistory = useCallback(async (targetPostId: LongId, pageIndex: number) => {
    setPostHistoryLoading(true);
    setPostHistoryError(null);
    try {
      const data = await getPostEditHistory(targetPostId, pageIndex, postHistoryPageSize, t);
      setPostHistories(data.voItems || []);
      setPostHistoryTotal(data.voTotal || 0);
      setPostHistoryPageIndex(data.voPageIndex || pageIndex);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setPostHistoryError(message);
    } finally {
      setPostHistoryLoading(false);
    }
  }, [postHistoryPageSize, t]);

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
    if (!post?.voId || !historyReturnPath) {
      return;
    }

    if (!isAuthenticated) {
      redirectToDetailLogin(historyReturnPath);
      return;
    }

    if (!isCurrentUserAuthor) {
      toast.error(t('forum.public.authorOnlyAction'));
      return;
    }

    setIsPostHistoryOpen(true);
    await loadPostHistory(post.voId, 1);
  }, [
    historyReturnPath,
    isAuthenticated,
    isCurrentUserAuthor,
    loadPostHistory,
    post?.voId,
    redirectToDetailLogin,
    t
  ]);

  const handleAnswerQuestion = useCallback(async (content: string) => {
    const normalizedContent = content.trim();
    if (!normalizedContent || !post?.voId) {
      return;
    }

    if (!post.voIsQuestion) {
      throw new Error(t('forum.public.answerQuestionOnly'));
    }

    if (!isAuthenticated) {
      redirectToDetailLogin(answerReturnPath);
      throw new Error(t('forum.loginRequiredToReact'));
    }

    const submissionState = createClientSubmissionState(
      answerSubmissionRef.current,
      'forum-answer',
      buildAnswerSubmissionFingerprint(post.voId, normalizedContent)
    );
    answerSubmissionRef.current = submissionState;

    try {
      await answerQuestion({
        postId: post.voId,
        content: normalizedContent,
        clientSubmissionId: submissionState.clientSubmissionId
      }, t);
      answerSubmissionRef.current = null;
      toast.success(t('forum.public.answerPublished'));
      setReloadToken((current) => current + 1);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      toast.error(message || t('forum.public.answerFailed'));
      throw error;
    }
  }, [answerReturnPath, isAuthenticated, post?.voId, post?.voIsQuestion, redirectToDetailLogin, t]);

  const handleAcceptAnswer = useCallback(async (answerId: LongId) => {
    if (!post?.voId) {
      return;
    }

    if (!isAuthenticated) {
      redirectToDetailLogin(answerReturnPath);
      throw new Error(t('forum.loginRequiredToReact'));
    }

    try {
      await acceptQuestionAnswer({
        postId: post.voId,
        answerId
      }, t);
      toast.success(t('forum.public.answerAccepted'));
      setReloadToken((current) => current + 1);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      toast.error(message || t('forum.public.answerAcceptFailed'));
      throw error;
    }
  }, [answerReturnPath, isAuthenticated, post?.voId, redirectToDetailLogin, t]);

  const handleSavePostEdit = useCallback(async (
    targetPostId: LongId,
    title: string,
    content: string,
    categoryId: LongId,
    tagNames: string[]
  ) => {
    const normalizedTagNames = normalizeTagNames(tagNames);
    const submissionState = createClientSubmissionState(
      postEditSubmissionRef.current,
      'forum-post-edit',
      buildPostEditSubmissionFingerprint(targetPostId, title, content, categoryId, normalizedTagNames)
    );
    postEditSubmissionRef.current = submissionState;

    try {
      await updatePost({
        postId: targetPostId,
        title,
        content,
        clientSubmissionId: submissionState.clientSubmissionId,
        categoryId,
        tagNames: normalizedTagNames
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
  }, [normalizeTagNames, t]);

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
        buildCommentSubmissionFingerprint(post.voId, normalizedContent, null)
      );
      commentSubmissionRef.current = submissionState;

      const createdCommentId = await createComment(
        {
          postId: post.voId,
          content: normalizedContent,
          clientSubmissionId: submissionState.clientSubmissionId,
          parentId: null,
          replyToCommentId: null,
          replyToCommentSnapshot: null,
          replyToUserId: null,
          replyToUserName: null
        },
        t
      );
      commentSubmissionRef.current = null;
      const now = new Date().toISOString();
      const newComment: CommentNode = {
        voId: createdCommentId,
        voPostId: post.voId,
        voContent: normalizedContent,
        voAuthorId: currentUserId || '0',
        voAuthorName: currentUserName?.trim() || t('common.unknownUser'),
        voAuthorAvatarUrl: currentUserAvatarUrl,
        voParentId: null,
        voRootId: null,
        voReplyToCommentId: null,
        voReplyToCommentSnapshot: null,
        voReplyToUserId: null,
        voReplyToUserName: null,
        voLevel: 0,
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
      setCommentNavigationTarget({
        commentId: createdCommentId,
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
    postError
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
  const readingGuide = useMemo(
    () => createForumReadingGuide(t, detailGuideDefinition),
    [t]
  );

  return (
    <section className={`${styles.sectionCard} ${styles.detailSectionCard}`}>
      <div className={styles.detailTopbar}>
        <div className={styles.detailTopbarActions}>
          <a
            className={styles.backButton}
            href={backHref}
            onClick={(event) => handlePublicForumLinkClick(event, onBack)}
          >
            <Icon icon="mdi:arrow-left" size={18} />
            <span>{backLabel}</span>
          </a>
          <button type="button" className={styles.secondaryButton} onClick={() => void copyShareLink()} disabled={shareBusy}>
            <Icon icon={shareBusy ? 'mdi:progress-clock' : 'mdi:link-variant'} size={18} />
            <span>{shareBusy ? t('forum.public.shareSubmitting') : t('forum.public.shareAction')}</span>
          </button>
        </div>
        {shareState !== 'idle' && (
          <p className={styles.shareFeedback} data-state={shareState}>
            {shareState === 'success' ? t('forum.public.shareSuccess') : t('forum.public.shareFailed')}
          </p>
        )}
      </div>

      <div className={styles.detailStack}>
        {detailState.kind === 'loading' && (
          <PublicStatusCard
            tone="loading"
            title={t('forum.public.loadingTitle')}
            description={t('forum.public.loadingDescription')}
          />
        )}

        {detailState.kind === 'notFound' && (
          <PublicStatusCard
            tone="notFound"
            title={t('forum.public.postNotFoundTitle')}
            description={t('forum.public.postNotFoundDescription')}
            secondaryAction={{
              label: backLabel,
              href: backHref,
              onClick: onBack
            }}
          />
        )}

        {detailState.kind === 'error' && (
          <PublicStatusCard
            tone="error"
            title={t('forum.public.postErrorTitle')}
            description={detailState.message}
            primaryAction={{
              label: t('common.retry'),
              onClick: () => setReloadToken((current) => current + 1)
            }}
            secondaryAction={{
              label: backLabel,
              href: backHref,
              onClick: onBack
            }}
          />
        )}

        {detailState.kind === 'ready' && post && (
          <div className={styles.detailMetaRail}>
            <span className={styles.readOnlyBadge}>{t('forum.public.readOnlyBadge')}</span>
            <span className={styles.detailMetaChip}>{t('forum.postDetail.views', { count: post.voViewCount ?? 0 })}</span>
            <span className={styles.detailMetaChip}>{t('forum.quickReply.total', { count: quickReplyTotal })}</span>
            <span className={styles.detailMetaChip}>{t('forum.postDetail.commentCount', { count: commentTotal })}</span>
          </div>
        )}

        {detailState.kind === 'ready' && (
          <>
            <ForumPostDetail
              post={post}
              loading={false}
              displayTimeZone={displayTimeZone}
              mode="interactive"
              isAuthenticated={isAuthenticated}
              currentUserId={currentUserId || '0'}
              showSectionTitle={false}
              postTitleHeadingLevel={1}
              onAnswerQuestion={handleAnswerQuestion}
              onAcceptAnswer={handleAcceptAnswer}
              answerAutoFocusKey={answerAutoFocusKey}
              onEdit={() => void handleEditPostAction()}
              onViewHistory={() => void handleViewPostHistory()}
              onAuthorClick={(userId) => onOpenAuthorProfile?.(String(userId))}
              resolveAuthorProfileId={resolvePublicProfileUserId}
              onTagClick={(_, tagSlug) => onOpenTag?.(tagSlug)}
              onQuestionClick={onOpenQuestion}
              onPollClick={onOpenPoll}
              onLotteryClick={onOpenLottery}
            />

            <PublicReadingGuide
              label={readingGuide.label}
              title={readingGuide.title}
              description={readingGuide.description}
              items={readingGuide.items}
            />

            {(commentReturnPath || quickReplyReturnPath || answerReturnPath || (isAuthenticated && isCurrentUserAuthor && (editReturnPath || historyReturnPath))) && (
              <section className={styles.workspaceActionPanel}>
                <div className={styles.workspaceActionCopy}>
                  <h2 className={styles.workspaceActionTitle}>{t('forum.public.workspaceActionTitle')}</h2>
                  <p className={styles.workspaceActionDescription}>
                    {t('forum.public.workspaceActionDescription')}
                  </p>
                </div>
                <div className={styles.workspaceActionButtons}>
                  {answerReturnPath && post?.voIsQuestion && (
                    <button
                      type="button"
                      className={`${styles.workspaceActionButton} ${styles.workspaceActionButtonPrimary}`}
                      onClick={handleAnswerAction}
                    >
                      <Icon icon="mdi:comment-question-outline" size={18} />
                      <span>
                        {isAuthenticated
                          ? t('forum.public.workspaceAnswerAction')
                          : t('forum.public.workspaceAnswerLoginAction')}
                      </span>
                    </button>
                  )}
                  {quickReplyReturnPath && (
                    <button
                      type="button"
                      className={styles.workspaceActionButton}
                      aria-controls={QUICK_REPLY_SECTION_ID}
                      onClick={handleQuickReplyAction}
                    >
                      <Icon icon="mdi:message-flash-outline" size={18} />
                      <span>
                        {isAuthenticated
                          ? t('forum.public.workspaceQuickReplyAction')
                          : t('forum.public.workspaceQuickReplyLoginAction')}
                      </span>
                    </button>
                  )}
                  {editReturnPath && isAuthenticated && isCurrentUserAuthor && (
                    <button
                      type="button"
                      className={styles.workspaceActionButton}
                      onClick={() => void handleEditPostAction()}
                      disabled={categoriesLoading}
                    >
                      <Icon icon={categoriesLoading ? 'mdi:progress-clock' : 'mdi:pencil-outline'} size={18} />
                      <span>{categoriesLoading ? t('forum.public.authorCategoriesLoading') : t('forum.public.workspaceEditAction')}</span>
                    </button>
                  )}
                  {historyReturnPath && isAuthenticated && isCurrentUserAuthor && (
                    <button
                      type="button"
                      className={styles.workspaceActionButton}
                      onClick={() => void handleViewPostHistory()}
                    >
                      <Icon icon="mdi:history" size={18} />
                      <span>{t('forum.public.workspaceHistoryAction')}</span>
                    </button>
                  )}
                  {commentReturnPath && (
                    <button
                      type="button"
                      className={styles.workspaceActionButton}
                      aria-controls={COMMENT_SECTION_ID}
                      onClick={handleCommentAction}
                    >
                      <Icon icon="mdi:comment-text-outline" size={18} />
                      <span>
                        {isAuthenticated
                          ? t('forum.public.workspaceCommentAction')
                          : t('forum.public.workspaceCommentLoginAction')}
                      </span>
                    </button>
                  )}
                </div>
              </section>
            )}

            {categoriesError && (
              <div className={styles.inlineNotice} data-tone="warning">
                <span className={styles.inlineNoticeText}>{categoriesError}</span>
              </div>
            )}

            {quickReplySectionState === 'error' ? (
              <section className={styles.sectionShell}>
                <div className={styles.sectionShellHeader}>
                  <h2 className={styles.sectionShellTitle}>{t('forum.quickReply.title')}</h2>
                  <span className={styles.detailMetaChip}>{t('forum.quickReply.total', { count: quickReplyTotal })}</span>
                </div>
                <PublicStatusCard
                  tone="error"
                  compact={true}
                  title={t('forum.public.partialErrorTitle')}
                  description={quickReplyError || t('forum.public.quickRepliesErrorDescription')}
                  primaryAction={{
                    label: t('common.retry'),
                    onClick: () => setReloadToken((current) => current + 1)
                  }}
                />
              </section>
            ) : (
              <>
                {quickReplyError && (
                  <div className={styles.inlineNotice} data-tone="warning">
                    <span className={styles.inlineNoticeText}>{t('forum.public.quickRepliesErrorDescription')}</span>
                  </div>
                )}
                <PostQuickReplyWall
                  sectionId={QUICK_REPLY_SECTION_ID}
                  replies={quickReplies}
                  total={quickReplyTotal}
                  loading={loadingQuickReplies}
                  isAuthenticated={isAuthenticated}
                  currentUserId={currentUserId || '0'}
                  titleHeadingLevel={2}
                  onCreate={handleCreateQuickReply}
                  loginPromptText={t('forum.public.quickReplyLoginPrompt')}
                  loginButtonText={t('forum.public.workspaceQuickReplyLoginAction')}
                  loginReturnPath={quickReplyReturnPath}
                  onLoginRequired={redirectToDetailLogin}
                  autoFocusComposerKey={quickReplyAutoFocusKey}
                />
              </>
            )}

            <section
              id={COMMENT_SECTION_ID}
              className={styles.commentSection}
              aria-labelledby={`${COMMENT_SECTION_ID}-title`}
            >
              <div className={styles.commentHeading}>
                <div>
                  <h2 id={`${COMMENT_SECTION_ID}-title`} className={styles.commentTitle}>
                    {t('forum.commentTree.title')}
                  </h2>
                  <p className={styles.commentIntro}>{t('forum.quickReply.discussionSubtitle')}</p>
                </div>
                <div className={styles.commentSummary}>
                  <span className={styles.commentSummaryChip}>
                    {t('forum.public.loadedComments', { loaded: comments.length, total: commentTotal })}
                  </span>
                  <span className={styles.commentSummaryChip}>
                    {t('forum.public.commentOrder', { label: commentSortLabel })}
                  </span>
                </div>
              </div>

              {commentPagingError && (
                <div className={styles.inlineNotice} data-tone="warning">
                  <span className={styles.inlineNoticeText}>
                    {t('forum.public.commentPagingErrorDescription')}
                  </span>
                </div>
              )}

              {commentNavigationNotice && (
                <div ref={commentNoticeRef} className={styles.inlineNotice} data-tone="warning">
                  <span className={styles.inlineNoticeText}>{commentNavigationNotice}</span>
                </div>
              )}

              {commentTypingText && (
                <div className={styles.inlineNotice}>
                  <span className={styles.inlineNoticeText}>{commentTypingText}</span>
                </div>
              )}

              <div className={styles.commentComposerPanel}>
                <CreateCommentForm
                  isAuthenticated={isAuthenticated}
                  hasPost={Boolean(post?.voId)}
                  onSubmit={(content) => {
                    void handleCreateComment(content);
                  }}
                  disabled={submittingComment}
                  variant="inline"
                  title={t('forum.joinDiscussion')}
                  submitText={t('forum.submitDiscussion')}
                  placeholder={t('forum.discussionPlaceholder')}
                  loginPromptText={t('forum.public.commentLoginPrompt')}
                  loginButtonText={t('forum.public.workspaceCommentLoginAction')}
                  loginReturnPath={commentReturnPath}
                  onLoginRequired={redirectToDetailLogin}
                  onTyping={handleCommentTyping}
                  autoFocusKey={commentAutoFocusKey}
                />
              </div>

              {commentSectionState === 'error' ? (
                <PublicStatusCard
                  tone="error"
                  compact={true}
                  title={t('forum.public.partialErrorTitle')}
                  description={commentError || t('forum.public.commentsErrorDescription')}
                  primaryAction={{
                    label: t('common.retry'),
                    onClick: () => setReloadToken((current) => current + 1)
                  }}
                />
              ) : (
                <>
                  {commentError && (
                    <div className={styles.inlineNotice} data-tone="warning">
                      <span className={styles.inlineNoticeText}>{t('forum.public.commentsErrorDescription')}</span>
                    </div>
                  )}
                  <CommentTree
                    comments={comments}
                    loading={loadingComments}
                    loadingMoreRootComments={loadingMoreComments}
                    hasPost={true}
                    displayTimeZone={displayTimeZone}
                    currentUserId={currentUserId || '0'}
                    highlightedCommentId={highlightedCommentId}
                    expandedRootCommentId={commentNavigationTarget?.expandedRootCommentId}
                    rootCommentTotal={commentTotal}
                    loadedRootCommentCount={comments.length}
                    rootCommentPageSize={commentPageSize}
                    registerCommentAnchor={registerCommentAnchor}
                    sortBy={commentSortBy}
                    onSortChange={setCommentSortBy}
                    onLoadMoreChildren={handleLoadMoreChildren}
                    onLoadMoreRootComments={handleLoadMoreComments}
                    isAuthenticated={isAuthenticated}
                    showTitle={false}
                    onAuthorClick={(userId) => onOpenAuthorProfile?.(String(userId))}
                    resolveAuthorProfileId={resolvePublicProfileUserId}
                    onNavigateToComment={(targetCommentId) => void navigateToComment(
                      targetCommentId,
                      `inline:${postId}:${targetCommentId}:${Date.now()}`
                    )}
                  />
                </>
              )}
            </section>

            {isEditModalOpen && (
              <Suspense fallback={null}>
                <EditPostModal
                  isOpen={isEditModalOpen}
                  post={post}
                  categories={categories}
                  onClose={() => setIsEditModalOpen(false)}
                  onSave={handleSavePostEdit}
                />
              </Suspense>
            )}

            {isPostHistoryOpen && (
              <Suspense fallback={null}>
                <EditHistoryModal
                  isOpen={isPostHistoryOpen}
                  title={t('forum.postDetail.action.history')}
                  loading={postHistoryLoading}
                  error={postHistoryError}
                  items={postHistories}
                  total={postHistoryTotal}
                  pageIndex={postHistoryPageIndex}
                  pageSize={postHistoryPageSize}
                  onClose={() => setIsPostHistoryOpen(false)}
                  onPageChange={(nextPageIndex) => {
                    if (post?.voId) {
                      void loadPostHistory(post.voId, nextPageIndex);
                    }
                  }}
                  renderContent={(item) => ({
                    before: item.voOldContent,
                    after: item.voNewContent,
                    beforeTitle: 'voOldTitle' in item ? item.voOldTitle : undefined,
                    afterTitle: 'voNewTitle' in item ? item.voNewTitle : undefined
                  })}
                />
              </Suspense>
            )}
          </>
        )}
      </div>
    </section>
  );
};
