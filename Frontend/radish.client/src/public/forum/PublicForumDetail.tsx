import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import {
  getCommentNavigation,
  getChildComments,
  getPostById,
  getPostQuickReplyWall,
  getRootCommentsPage,
  type CommentNode,
  type PostDetail,
  type PostQuickReply,
} from '@/api/forum';
import type { LongId } from '@/api/user';
import { copyToClipboard } from '@/utils/clipboard';
import { log } from '@/utils/logger';
import { resolveMediaUrl } from '@/utils/media';
import { CommentTree } from '@/apps/forum/components/CommentTree';
import { PostDetail as ForumPostDetail } from '@/apps/forum/components/PostDetail';
import { PostQuickReplyWall } from '@/apps/forum/components/PostQuickReplyWall';
import { buildPublicForumPath } from '../forumRouteState';
import { buildPublicCanonicalUrl } from '../publicHead';
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
import { PublicStatusCard } from './PublicStatusCard';
import {
  createForumReadingGuide,
  detailGuideDefinition,
  getForumPostRouteIdentifier,
  isSameLongId,
  mergeCommentChildren,
} from './publicForumUtils';
import styles from './PublicForumApp.module.css';

type RootCommentSort = 'newest' | 'hottest' | null;
const COMMENT_NAVIGATION_CHILD_PAGE_SIZE = 5;

interface PublicForumCommentNavigationTarget {
  commentId: LongId;
  expandedRootCommentId?: LongId;
  navigationKey: string;
}

interface PublicForumDetailProps {
  postId: string;
  commentId?: string;
  displayTimeZone: string;
  backLabel: string;
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
  displayTimeZone,
  backLabel,
  onBack,
  onOpenAuthorProfile,
  onOpenTag,
  onOpenQuestion,
  onOpenPoll,
  onOpenLottery
}: PublicForumDetailProps) => {
  const { t } = useTranslation();
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
  const [highlightedCommentId, setHighlightedCommentId] = useState<LongId | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [shareBusy, setShareBusy] = useState(false);
  const [shareState, setShareState] = useState<'idle' | 'success' | 'error'>('idle');
  const requestIdRef = useRef(0);
  const commentAnchorMapRef = useRef(new Map<string, HTMLDivElement>());
  const handledCommentNavigationRef = useRef<string | null>(null);
  const highlightTimeoutRef = useRef<number | null>(null);
  const commentNoticeRef = useRef<HTMLDivElement | null>(null);
  const commentPageSize = 20;

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

          setComments(nextComments);
          setCommentTotal(rootComments.voTotal ?? 0);
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
  }, [commentId, commentSortBy, postId, reloadToken, t]);

  useEffect(() => {
    return () => {
      if (highlightTimeoutRef.current !== null) {
        window.clearTimeout(highlightTimeoutRef.current);
      }
    };
  }, []);

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

    const structuredPost = {
      ...post,
      voCoverImage: resolveMediaUrl(post.voCoverImage),
    };
    const routePostId = getForumPostRouteIdentifier(post);
    const canonicalPath = buildPublicForumPath(commentId
      ? { kind: 'detail', postId: routePostId, commentId }
      : { kind: 'detail', postId: routePostId });

    applyPublicStructuredData(buildForumPostStructuredData({
      post: structuredPost,
      canonicalPath,
    }));

    return removePublicStructuredData;
  }, [commentId, post]);

  useEffect(() => {
    if (shareState === 'idle') {
      return;
    }

    const timerId = window.setTimeout(() => {
      setShareState('idle');
    }, 2200);

    return () => {
      window.clearTimeout(timerId);
    };
  }, [shareState]);

  const handleShare = async () => {
    setShareBusy(true);

    try {
      const sharePostId = post ? getForumPostRouteIdentifier(post) : postId;
      const sharePath = buildPublicForumPath(commentId
        ? { kind: 'detail', postId: sharePostId, commentId }
        : { kind: 'detail', postId: sharePostId });
      await copyToClipboard(buildPublicCanonicalUrl(sharePath));
      setShareState('success');
    } catch {
      setShareState('error');
    } finally {
      setShareBusy(false);
    }
  };

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
        setComments(nextComments);
        setCommentTotal(rootComments.voTotal ?? 0);
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
  }, [commentPageSize, commentSortBy, comments, loadedCommentPages, post?.voId, postId, t]);

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

      setComments((current) => {
        const existingIds = new Set(current.map((item) => item.voId));
        const appended = nextItems.filter((item) => !existingIds.has(item.voId));
        return [...current, ...appended];
      });
      setCommentTotal((current) => pageData.voTotal ?? current);
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
          <button type="button" className={styles.backButton} onClick={onBack}>
            <Icon icon="mdi:arrow-left" size={18} />
            <span>{backLabel}</span>
          </button>
          <button type="button" className={styles.secondaryButton} onClick={() => void handleShare()} disabled={shareBusy}>
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
            <PublicReadingGuide
              label={readingGuide.label}
              title={readingGuide.title}
              description={readingGuide.description}
              items={readingGuide.items}
            />

            <ForumPostDetail
              post={post}
              loading={false}
              displayTimeZone={displayTimeZone}
              mode="readOnly"
              isAuthenticated={false}
              showSectionTitle={false}
              onAuthorClick={(userId) => onOpenAuthorProfile?.(String(userId))}
              onTagClick={(_, tagSlug) => onOpenTag?.(tagSlug)}
              onQuestionClick={onOpenQuestion}
              onPollClick={onOpenPoll}
              onLotteryClick={onOpenLottery}
            />

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
                  replies={quickReplies}
                  total={quickReplyTotal}
                  loading={loadingQuickReplies}
                  isAuthenticated={false}
                  currentUserId={0}
                  mode="readOnly"
                />
              </>
            )}

            <section className={styles.commentSection}>
              <div className={styles.commentHeading}>
                <div>
                  <h2 className={styles.commentTitle}>{t('forum.commentTree.title')}</h2>
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
                    currentUserId={0}
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
                    showTitle={false}
                    onAuthorClick={(userId) => onOpenAuthorProfile?.(String(userId))}
                    onNavigateToComment={(targetCommentId) => void navigateToComment(
                      targetCommentId,
                      `inline:${postId}:${targetCommentId}:${Date.now()}`
                    )}
                  />
                </>
              )}
            </section>
          </>
        )}
      </div>
    </section>
  );
};
