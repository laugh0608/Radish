import { useCallback, useEffect, useMemo, useRef, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import {
  getPublicProfile,
  getPublicUserComments,
  getPublicUserPosts,
  getPublicUserStats,
  type PublicUserComment,
  type PublicUserPost,
  type PublicUserProfile,
  type PublicUserStats,
} from '@/api/user';
import { DEFAULT_TIME_ZONE, formatDateTimeByTimeZone, getBrowserTimeZoneId } from '@/utils/dateTime';
import { resolveMediaUrl } from '@/utils/media';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import { buildPublicProfilePath, type PublicProfileRoute, type PublicProfileTab } from '../profileRouteState';
import { buildPublicForumPath } from '../forumRouteState';
import {
  getPublicDetailBackLabelKey,
  type PublicDetailBackMode,
} from '../publicRouteNavigation';
import {
  applyPublicStructuredData,
  buildProfilePageStructuredData,
  removePublicStructuredData,
} from '../publicStructuredData';
import { buildPublicShareUrl } from '../publicHead';
import { PublicShellHeader } from '../components/PublicShellHeader';
import { usePublicShareLink } from '../hooks/usePublicShareLink';
import {
  resolvePublicProfileCommentForumTarget,
  resolvePublicProfilePostForumTarget,
  resolvePublicProfileRouteIdentifier,
} from './publicProfileNavigation';
import { WebStateSlot, type WebStateSlotAction } from '@/components/web-shell';
import styles from './PublicProfileApp.module.css';

interface PublicProfileAppProps {
  route: PublicProfileRoute;
  backAction?: {
    mode: PublicDetailBackMode;
    href?: string;
    onBack: () => void;
  } | null;
  onNavigate: (route: PublicProfileRoute, options?: { replace?: boolean; preserveSourceState?: boolean }) => void;
  onNavigateToDiscover?: () => void;
  onNavigateToForumList: () => void;
  onNavigateToForumPost: (postId: string, commentId?: string) => void;
}

type PublicStatusTone = 'loading' | 'empty' | 'error' | 'notFound';

interface ProfileGuideFocusDefinition {
  labelKey: string;
  valueKey: string;
}

const profileGuideFocusItems: ProfileGuideFocusDefinition[] = [
  {
    labelKey: 'profile.public.readingGuide.focusProfileLabel',
    valueKey: 'profile.public.readingGuide.focusProfileValue',
  },
  {
    labelKey: 'profile.public.readingGuide.focusContentLabel',
    valueKey: 'profile.public.readingGuide.focusContentValue',
  },
  {
    labelKey: 'profile.public.readingGuide.focusBoundaryLabel',
    valueKey: 'profile.public.readingGuide.focusBoundaryValue',
  },
];

const profileGuideBoundaryItems = [
  'profile.public.readingGuide.boundaryItemEdit',
  'profile.public.readingGuide.boundaryItemHistory',
  'profile.public.readingGuide.boundaryItemWorkspace',
] as const;

function buildProfileForumTargetHref(postId: string, commentId?: string): string {
  return buildPublicForumPath(commentId
    ? { kind: 'detail', postId, commentId }
    : { kind: 'detail', postId });
}

function shouldHandleProfileLinkInternally(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

interface PublicStatusCardProps {
  tone: PublicStatusTone;
  title: string;
  description: string;
  primaryAction?: {
    label: string;
    href?: string;
    onClick: () => void;
  };
  secondaryAction?: {
    label: string;
    href?: string;
    onClick: () => void;
  };
}

function PublicStatusCard({
  tone,
  title,
  description,
  primaryAction,
  secondaryAction
}: PublicStatusCardProps) {
  const resolvedIcon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:text-box-search-outline'
      : tone === 'notFound'
        ? 'mdi:account-search-outline'
        : 'mdi:alert-circle-outline';
  const actions: WebStateSlotAction[] = [];

  if (primaryAction) {
    actions.push({
      label: primaryAction.label,
      href: primaryAction.href,
      kind: 'primary',
      onClick: primaryAction.href
        ? (event) => {
          if (!shouldHandleProfileLinkInternally(event as MouseEvent<HTMLAnchorElement>)) {
            return;
          }

          event.preventDefault();
          primaryAction.onClick();
        }
        : () => primaryAction.onClick(),
    });
  }

  if (secondaryAction) {
    actions.push({
      label: secondaryAction.label,
      href: secondaryAction.href,
      kind: 'secondary',
      onClick: secondaryAction.href
        ? (event) => {
          if (!shouldHandleProfileLinkInternally(event as MouseEvent<HTMLAnchorElement>)) {
            return;
          }

          event.preventDefault();
          secondaryAction.onClick();
        }
        : () => secondaryAction.onClick(),
    });
  }

  return (
    <WebStateSlot
      tone={tone}
      title={title}
      description={description}
      icon={resolvedIcon}
      actions={actions}
    />
  );
}

function buildAvatarText(name: string): string {
  const source = name.trim();
  if (!source) {
    return '?';
  }

  return source.charAt(0).toUpperCase();
}

function buildAvatarStyle(seed: string) {
  let hash = 0;
  for (let index = 0; index < seed.length; index += 1) {
    hash = seed.charCodeAt(index) + ((hash << 5) - hash);
  }

  const hue = Math.abs(hash) % 360;
  return {
    backgroundColor: `hsl(${hue} 80% 92%)`,
    color: `hsl(${hue} 45% 30%)`
  };
}

function buildExcerpt(post: PublicUserPost): string {
  const summary = post.voSummary?.trim();
  if (summary) {
    return summary;
  }

  const content = post.voContent?.replace(/\s+/g, ' ').trim() ?? '';
  if (!content) {
    return '';
  }

  return content.length > 120 ? `${content.slice(0, 120)}...` : content;
}

export const PublicProfileApp = ({
  route,
  backAction,
  onNavigate,
  onNavigateToDiscover,
  onNavigateToForumList,
  onNavigateToForumPost
}: PublicProfileAppProps) => {
  const { t } = useTranslation();
  const pageRef = useRef<HTMLDivElement>(null);
  const profileRequestIdRef = useRef(0);
  const contentRequestIdRef = useRef(0);
  const displayTimeZone = useMemo(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE), []);
  const [profile, setProfile] = useState<PublicUserProfile | null>(null);
  const [stats, setStats] = useState<PublicUserStats | null>(null);
  const [loadingProfile, setLoadingProfile] = useState(true);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [posts, setPosts] = useState<PublicUserPost[]>([]);
  const [comments, setComments] = useState<PublicUserComment[]>([]);
  const [loadingContent, setLoadingContent] = useState(true);
  const [contentError, setContentError] = useState<string | null>(null);
  const [totalPages, setTotalPages] = useState(1);
  const [profileReloadToken, setProfileReloadToken] = useState(0);
  const [contentReloadToken, setContentReloadToken] = useState(0);

  useEffect(() => {
    pageRef.current?.scrollTo({ top: 0, behavior: 'auto' });
  }, [route.page, route.tab, route.userId]);

  useEffect(() => {
    const requestId = ++profileRequestIdRef.current;

    const loadProfile = async () => {
      setLoadingProfile(true);
      setProfileError(null);
      setProfile(null);
      setStats(null);

      try {
        const [profileResult, statsResult] = await Promise.all([
          getPublicProfile(route.userId),
          getPublicUserStats(route.userId),
        ]);

        if (requestId !== profileRequestIdRef.current) {
          return;
        }

        setProfile(profileResult);
        setStats(statsResult);
      } catch (error) {
        if (requestId !== profileRequestIdRef.current) {
          return;
        }

        const message = error instanceof Error ? error.message : String(error);
        setProfile(null);
        setStats(null);
        setProfileError(message);
      } finally {
        if (requestId === profileRequestIdRef.current) {
          setLoadingProfile(false);
        }
      }
    };

    void loadProfile();
  }, [profileReloadToken, route.userId]);

  const profileRouteIdentifier = useMemo(
    () => resolvePublicProfileRouteIdentifier(profile, route.userId),
    [profile, route.userId]
  );

  useEffect(() => {
    if (!profile || profileRouteIdentifier === route.userId) {
      return;
    }

    onNavigate({
      kind: 'detail',
      userId: profileRouteIdentifier,
      tab: route.tab,
      page: route.page,
    }, { replace: true, preserveSourceState: true });
  }, [onNavigate, profile, profileRouteIdentifier, route.page, route.tab, route.userId]);

  useEffect(() => {
    const requestId = ++contentRequestIdRef.current;

    const loadContent = async () => {
      if (!profile) {
        setPosts([]);
        setComments([]);
        setTotalPages(1);
        setLoadingContent(false);
        return;
      }

      setLoadingContent(true);
      setContentError(null);

      try {
        if (route.tab === 'posts') {
          const pageModel = await getPublicUserPosts(profileRouteIdentifier, route.page, 10);
          if (requestId !== contentRequestIdRef.current) {
            return;
          }

          const nextTotalPages = Math.max(pageModel.pageCount || 1, 1);
          if (route.page > nextTotalPages) {
            onNavigate({
              kind: 'detail',
              userId: profileRouteIdentifier,
              tab: route.tab,
              page: nextTotalPages
            }, { replace: true });
            return;
          }

          setPosts(pageModel.data ?? []);
          setComments([]);
          setTotalPages(nextTotalPages);
          return;
        }

        const pageModel = await getPublicUserComments(profileRouteIdentifier, route.page, 10);
        if (requestId !== contentRequestIdRef.current) {
          return;
        }

        const nextTotalPages = Math.max(pageModel.pageCount || 1, 1);
        if (route.page > nextTotalPages) {
          onNavigate({
            kind: 'detail',
            userId: profileRouteIdentifier,
            tab: route.tab,
            page: nextTotalPages
          }, { replace: true });
          return;
        }

        setComments(pageModel.data ?? []);
        setPosts([]);
        setTotalPages(nextTotalPages);
      } catch (error) {
        if (requestId !== contentRequestIdRef.current) {
          return;
        }

        const message = error instanceof Error ? error.message : String(error);
        setPosts([]);
        setComments([]);
        setTotalPages(1);
        setContentError(message);
      } finally {
        if (requestId === contentRequestIdRef.current) {
          setLoadingContent(false);
        }
      }
    };

    void loadContent();
  }, [contentReloadToken, onNavigate, profile, profileRouteIdentifier, route.page, route.tab]);

  useEffect(() => {
    if (loadingProfile) {
      return;
    }

    const titleName = profile
      ? resolveVisibleUserHandle(
          profile,
          resolveVisibleUserDisplayName(profile, t('common.userFallback', { id: route.userId }))
        ) || resolveVisibleUserDisplayName(profile, t('common.userFallback', { id: route.userId }))
      : null;
    const nextTitle = titleName
      ? `${titleName} · ${t('profile.public.title')}`
      : t('profile.public.title');

    document.title = nextTitle;
  }, [loadingProfile, profile, route.userId, t]);

  const avatarUrl = useMemo(
    () => resolveMediaUrl(profile?.voAvatarThumbnailUrl || profile?.voAvatarUrl),
    [profile?.voAvatarThumbnailUrl, profile?.voAvatarUrl]
  );

  useEffect(() => {
    if (!profile) {
      removePublicStructuredData();
      return;
    }

    applyPublicStructuredData(buildProfilePageStructuredData({
      profile,
      stats,
      imageUrl: avatarUrl,
      canonicalPath: buildPublicProfilePath({
        kind: 'detail',
        userId: profileRouteIdentifier,
        tab: route.tab,
        page: route.page,
      }),
    }));

    return removePublicStructuredData;
  }, [avatarUrl, profile, profileRouteIdentifier, route.page, route.tab, stats]);

  const displayName = profile
    ? resolveVisibleUserDisplayName(profile, t('common.userFallback', { id: route.userId }))
    : t('common.userFallback', { id: route.userId });
  const displayHandle = profile ? resolveVisibleUserHandle(profile, displayName) : null;
  const backLabelKey = getPublicDetailBackLabelKey(backAction?.mode);
  const backLabel = backLabelKey
    ? t(backLabelKey)
    : t(onNavigateToDiscover ? 'public.shell.backToDiscover' : 'profile.public.backToForum');
  const handleBack = backAction?.onBack ?? onNavigateToDiscover ?? onNavigateToForumList;
  const backHref = backAction?.href
    ?? (onNavigateToDiscover
      ? '/discover'
      : buildPublicForumPath({ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 }));
  const buildProfileShareUrl = useCallback(() => {
    return buildPublicShareUrl(buildPublicProfilePath({
      kind: 'detail',
      userId: profileRouteIdentifier,
      tab: route.tab,
      page: route.page,
    }));
  }, [profileRouteIdentifier, route.page, route.tab]);
  const { copyShareLink, shareBusy, shareState } = usePublicShareLink({
    buildShareUrl: buildProfileShareUrl,
  });

  const handleTabChange = (tab: PublicProfileTab) => {
    if (tab === route.tab && route.page === 1) {
      return;
    }

    onNavigate({
      kind: 'detail',
      userId: profileRouteIdentifier,
      tab,
      page: 1
    }, { replace: true });
  };

  const handleProfileRouteLinkClick = (
    event: MouseEvent<HTMLAnchorElement>,
    nextRoute: PublicProfileRoute
  ) => {
    if (!shouldHandleProfileLinkInternally(event)) {
      return;
    }

    event.preventDefault();
    onNavigate(nextRoute, { replace: true });
  };

  const handleBackLinkClick = (event: MouseEvent<HTMLAnchorElement>) => {
    if (!shouldHandleProfileLinkInternally(event)) {
      return;
    }

    event.preventDefault();
    handleBack();
  };

  const handleForumTargetLinkClick = (
    event: MouseEvent<HTMLAnchorElement>,
    postId: string,
    commentId?: string
  ) => {
    if (!shouldHandleProfileLinkInternally(event)) {
      return;
    }

    event.preventDefault();
    onNavigateToForumPost(postId, commentId);
  };

  return (
    <div className={styles.page} ref={pageRef}>
      <PublicShellHeader
        brandMark="人"
        brandName={t('profile.public.title')}
        brandSubline={t('profile.public.shellLabel')}
        onBrandClick={() => pageRef.current?.scrollTo({ top: 0, behavior: 'smooth' })}
        onNavigateToDiscover={onNavigateToDiscover}
        discoverLabel={t('public.shell.discoverAction')}
        circleLabel={t('public.shell.circleAction')}
        desktopLabel={t('public.shell.desktopAction')}
      />

      <main className={styles.main}>
        {loadingProfile ? (
          <PublicStatusCard
            tone="loading"
            title={t('profile.publicLoading')}
            description={t('profile.public.intro')}
          />
        ) : profileError ? (
          <PublicStatusCard
            tone={profileError.includes('不存在') ? 'notFound' : 'error'}
            title={profileError.includes('不存在') ? t('profile.public.notFoundTitle') : t('profile.public.loadFailedTitle')}
            description={profileError.includes('不存在') ? t('profile.public.notFoundDescription') : profileError}
            primaryAction={profileError.includes('不存在')
              ? undefined
              : {
                  label: t('common.retry'),
                  onClick: () => setProfileReloadToken((current) => current + 1)
                }}
            secondaryAction={{
              label: backLabel,
              href: backHref,
              onClick: handleBack
            }}
          />
        ) : (
          <>
            <section className={styles.summaryCard}>
              <div className={styles.summaryHeader}>
                <span className={styles.readOnlyBadge}>{t('profile.public.readOnlyBadge')}</span>
                <button type="button" className={`${styles.secondaryButton} ${styles.shareButton}`} onClick={() => void copyShareLink()} disabled={shareBusy}>
                  <Icon icon={shareBusy ? 'mdi:progress-clock' : 'mdi:link-variant'} size={16} />
                  <span>{shareBusy ? t('profile.public.shareSubmitting') : t('profile.public.shareAction')}</span>
                </button>
              </div>
              {shareState !== 'idle' && (
                <p className={styles.shareFeedback} data-state={shareState}>
                  {shareState === 'success' ? t('profile.public.shareSuccess') : t('profile.public.shareFailed')}
                </p>
              )}
              <a className={styles.summaryBackLink} href={backHref} onClick={handleBackLinkClick}>
                <Icon icon="mdi:arrow-left" size={16} />
                <span>{backLabel}</span>
              </a>
              <p className={styles.summaryIntro}>{t('profile.public.intro')}</p>

              <div className={styles.identityRow}>
                <div
                  className={styles.avatar}
                  style={avatarUrl ? undefined : buildAvatarStyle(displayName)}
                  aria-hidden="true"
                >
                  {avatarUrl ? (
                    <img className={styles.avatarImage} src={avatarUrl} alt={displayName} loading="lazy" />
                  ) : (
                    buildAvatarText(displayName)
                  )}
                </div>

                <div className={styles.identityBody}>
                  <div className={styles.identityText}>
                    <h1 className={styles.userName}>{displayName}</h1>
                    {displayHandle && (
                      <p className={styles.displayName}>{displayHandle}</p>
                    )}
                    <p className={styles.joinedAt}>
                      {t('profile.publicSince', {
                        time: formatDateTimeByTimeZone(profile?.voCreateTime ?? '', displayTimeZone)
                      })}
                    </p>
                    <p className={styles.viewHint}>{t('profile.publicViewHint')}</p>
                  </div>

                </div>
              </div>

              <div className={styles.statsGrid}>
                <div className={styles.statCard}>
                  <span className={styles.statLabel}>{t('profile.stats.postsLabel')}</span>
                  <span className={styles.statValue}>{stats?.voPostCount ?? 0}</span>
                </div>
                <div className={styles.statCard}>
                  <span className={styles.statLabel}>{t('profile.stats.commentsLabel')}</span>
                  <span className={styles.statValue}>{stats?.voCommentCount ?? 0}</span>
                </div>
                <div className={styles.statCard}>
                  <span className={styles.statLabel}>{t('profile.stats.likesLabel')}</span>
                  <span className={styles.statValue}>{stats?.voTotalLikeCount ?? 0}</span>
                </div>
              </div>

            </section>

            <section className={styles.contentCard}>
              <div className={styles.contentHeader}>
                <div className={styles.contentHeaderText}>
                  <p className={styles.kicker}>{t('profile.public.contentKicker')}</p>
                  <h2 className={styles.sectionTitle}>{t('profile.public.contentTitle')}</h2>
                  <p className={styles.contentDescription}>{t('profile.public.contentDescription')}</p>
                </div>
                <div className={styles.tabs}>
                  <a
                    className={`${styles.tabButton} ${route.tab === 'posts' ? styles.tabButtonActive : ''}`}
                    href={buildPublicProfilePath({ kind: 'detail', userId: profileRouteIdentifier, tab: 'posts', page: 1 })}
                    aria-current={route.tab === 'posts' ? 'page' : undefined}
                    onClick={(event) => {
                      if (!shouldHandleProfileLinkInternally(event)) {
                        return;
                      }

                      event.preventDefault();
                      handleTabChange('posts');
                    }}
                  >
                    {t('profile.tab.userPosts')}
                  </a>
                  <a
                    className={`${styles.tabButton} ${route.tab === 'comments' ? styles.tabButtonActive : ''}`}
                    href={buildPublicProfilePath({ kind: 'detail', userId: profileRouteIdentifier, tab: 'comments', page: 1 })}
                    aria-current={route.tab === 'comments' ? 'page' : undefined}
                    onClick={(event) => {
                      if (!shouldHandleProfileLinkInternally(event)) {
                        return;
                      }

                      event.preventDefault();
                      handleTabChange('comments');
                    }}
                  >
                    {t('profile.tab.userComments')}
                  </a>
                </div>
              </div>

              {loadingContent ? (
                <PublicStatusCard
                  tone="loading"
                  title={t('common.loading')}
                  description={route.tab === 'posts' ? t('profile.public.postsDescription') : t('profile.public.commentsDescription')}
                />
              ) : contentError ? (
                <PublicStatusCard
                  tone="error"
                  title={t('profile.public.contentLoadFailedTitle')}
                  description={contentError}
                  primaryAction={{
                    label: t('common.retry'),
                    onClick: () => setContentReloadToken((current) => current + 1)
                  }}
                />
              ) : route.tab === 'posts' ? (
                posts.length === 0 ? (
                  <PublicStatusCard
                    tone="empty"
                    title={t('profile.public.postsEmptyTitle')}
                    description={t('profile.public.postsEmptyDescription')}
                  />
                ) : (
                  <div className={styles.list}>
                    {posts.map((post) => {
                      const target = resolvePublicProfilePostForumTarget(post);
                      const href = buildProfileForumTargetHref(target.postId);

                      return (
                        <a
                          key={String(post.voId)}
                          className={styles.contentItem}
                          href={href}
                          onClick={(event) => handleForumTargetLinkClick(event, target.postId)}
                        >
                          <div className={styles.itemTopRow}>
                            <span className={styles.itemType}>{t('profile.tab.userPosts')}</span>
                            <span className={styles.itemTime}>
                              {formatDateTimeByTimeZone(post.voCreateTime, displayTimeZone)}
                            </span>
                          </div>
                          <h3 className={styles.itemTitle}>{post.voTitle}</h3>
                          <p className={styles.itemExcerpt}>{buildExcerpt(post) || t('profile.public.noSummary')}</p>
                          <div className={styles.itemFooter}>
                            <div className={styles.itemMeta}>
                              <span>{t('profile.stats.likes', { count: post.voLikeCount ?? 0 })}</span>
                              <span>{t('profile.stats.comments', { count: post.voCommentCount ?? 0 })}</span>
                              <span>{t('forum.postDetail.views', { count: post.voViewCount ?? 0 })}</span>
                            </div>
                            <span className={styles.itemAction}>
                              <Icon icon="mdi:arrow-right" size={16} />
                              <span>{t('profile.public.openPostDetail')}</span>
                            </span>
                          </div>
                        </a>
                      );
                    })}
                  </div>
                )
              ) : comments.length === 0 ? (
                <PublicStatusCard
                  tone="empty"
                  title={t('profile.public.commentsEmptyTitle')}
                  description={t('profile.public.commentsEmptyDescription')}
                />
              ) : (
                <div className={styles.list}>
                  {comments.map((comment) => {
                    const target = resolvePublicProfileCommentForumTarget(comment);
                    const href = buildProfileForumTargetHref(target.postId, target.commentId);

                    return (
                      <a
                        key={String(comment.voId)}
                        className={styles.contentItem}
                        href={href}
                        onClick={(event) => handleForumTargetLinkClick(event, target.postId, target.commentId)}
                      >
                        <div className={styles.itemTopRow}>
                          <span className={styles.itemType}>{t('profile.tab.userComments')}</span>
                          <span className={styles.itemTime}>
                            {formatDateTimeByTimeZone(comment.voCreateTime, displayTimeZone)}
                          </span>
                        </div>
                        {comment.voReplyToUserName && (
                          <p className={styles.replyMeta}>
                            @{comment.voReplyToUserName}
                            {comment.voReplyToCommentSnapshot ? ` · ${comment.voReplyToCommentSnapshot}` : ''}
                          </p>
                        )}
                        <p className={styles.commentContent}>{comment.voContent}</p>
                        <div className={styles.itemFooter}>
                          <div className={styles.itemMeta}>
                            <span>{t('profile.stats.likes', { count: comment.voLikeCount ?? 0 })}</span>
                          </div>
                          <span className={styles.itemAction}>
                            <Icon icon="mdi:comment-arrow-right-outline" size={16} />
                            <span>{t('profile.public.openCommentContext')}</span>
                          </span>
                        </div>
                      </a>
                    );
                  })}
                </div>
              )}

              {totalPages > 1 && !loadingContent && !contentError && (
                <div className={styles.pagination}>
                  {route.page === 1 ? (
                    <button type="button" className={styles.paginationButton} disabled>
                      {t('common.previousPage')}
                    </button>
                  ) : (
                    <a
                      className={styles.paginationButton}
                      href={buildPublicProfilePath({
                        kind: 'detail',
                        userId: profileRouteIdentifier,
                        tab: route.tab,
                        page: route.page - 1
                      })}
                      onClick={(event) => handleProfileRouteLinkClick(event, {
                        kind: 'detail',
                        userId: profileRouteIdentifier,
                        tab: route.tab,
                        page: route.page - 1
                      })}
                    >
                      {t('common.previousPage')}
                    </a>
                  )}
                  <span className={styles.pageInfo}>
                    {t('common.pageInfo', { current: route.page, total: totalPages })}
                  </span>
                  {route.page === totalPages ? (
                    <button type="button" className={styles.paginationButton} disabled>
                      {t('common.nextPage')}
                    </button>
                  ) : (
                    <a
                      className={styles.paginationButton}
                      href={buildPublicProfilePath({
                        kind: 'detail',
                        userId: profileRouteIdentifier,
                        tab: route.tab,
                        page: route.page + 1
                      })}
                      onClick={(event) => handleProfileRouteLinkClick(event, {
                        kind: 'detail',
                        userId: profileRouteIdentifier,
                        tab: route.tab,
                        page: route.page + 1
                      })}
                    >
                      {t('common.nextPage')}
                    </a>
                  )}
                </div>
              )}
            </section>

            <section className={styles.readingGuideSection} aria-label={t('profile.public.readingGuide.title')}>
              <div className={styles.readingGuideSummary}>
                <div className={styles.readingGuideSummaryCard}>
                  <div className={styles.readingGuideSummaryHeading}>
                    <span className={styles.readingGuideSummaryLabel}>
                      {t('profile.public.readingGuide.summaryLabel')}
                    </span>
                    <h2 className={styles.readingGuideSummaryTitle}>
                      {t('profile.public.readingGuide.summaryTitle')}
                    </h2>
                  </div>
                  <p className={styles.readingGuideSummaryDescription}>
                    {t('profile.public.readingGuide.summaryDescription')}
                  </p>
                  <div className={styles.readingGuideFocusRow}>
                    {profileGuideFocusItems.map((item) => (
                      <article key={item.labelKey} className={styles.readingGuideFocusChip}>
                        <span className={styles.readingGuideFocusLabel}>{t(item.labelKey)}</span>
                        <span className={styles.readingGuideFocusValue}>{t(item.valueKey)}</span>
                      </article>
                    ))}
                  </div>
                </div>

                <aside className={styles.readingGuideBoundaryPanel}>
                  <span className={styles.readingGuideBoundaryLabel}>
                    {t('profile.public.readingGuide.boundaryLabel')}
                  </span>
                  <h2 className={styles.readingGuideBoundaryTitle}>
                    {t('profile.public.readingGuide.boundaryTitle')}
                  </h2>
                  <p className={styles.readingGuideBoundaryDescription}>
                    {t('profile.public.readingGuide.boundaryDescription')}
                  </p>
                  <ul className={styles.readingGuideBoundaryList}>
                    {profileGuideBoundaryItems.map((itemKey) => (
                      <li key={itemKey} className={styles.readingGuideBoundaryItem}>
                        {t(itemKey)}
                      </li>
                    ))}
                  </ul>
                </aside>
              </div>
            </section>
          </>
        )}
      </main>
    </div>
  );
};
