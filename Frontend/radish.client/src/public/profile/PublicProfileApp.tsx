import { useEffect, useMemo, useRef, useState } from 'react';
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
import { followUser, getFollowStatus, unfollowUser, type UserFollowStatus } from '@/api/userFollow';
import { useUserStore } from '@/stores/userStore';
import { DEFAULT_TIME_ZONE, formatDateTimeByTimeZone, getBrowserTimeZoneId } from '@/utils/dateTime';
import { resolveMediaUrl } from '@/utils/media';
import type { PublicProfileRoute, PublicProfileTab } from '../profileRouteState';
import type { PublicDetailBackMode } from '../publicRouteNavigation';
import styles from './PublicProfileApp.module.css';

interface PublicProfileAppProps {
  route: PublicProfileRoute;
  backAction?: {
    mode: PublicDetailBackMode;
    onBack: () => void;
  } | null;
  onNavigate: (route: PublicProfileRoute, options?: { replace?: boolean }) => void;
  onNavigateToDiscover?: () => void;
  onNavigateToForumList: () => void;
  onNavigateToForumPost: (postId: string, commentId?: string) => void;
}

type PublicStatusTone = 'loading' | 'empty' | 'error' | 'notFound';

interface PublicStatusCardProps {
  tone: PublicStatusTone;
  title: string;
  description: string;
  primaryAction?: {
    label: string;
    onClick: () => void;
  };
  secondaryAction?: {
    label: string;
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
  const icon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:text-box-search-outline'
      : tone === 'notFound'
        ? 'mdi:account-search-outline'
        : 'mdi:alert-circle-outline';

  return (
    <div className={styles.statusCard} data-tone={tone}>
      <div className={styles.statusIcon}>
        <Icon icon={icon} size={22} />
      </div>
      <div className={styles.statusBody}>
        <h2 className={styles.statusTitle}>{title}</h2>
        <p className={styles.statusDescription}>{description}</p>
        {(primaryAction || secondaryAction) && (
          <div className={styles.statusActions}>
            {primaryAction && (
              <button type="button" className={styles.primaryButton} onClick={primaryAction.onClick}>
                {primaryAction.label}
              </button>
            )}
            {secondaryAction && (
              <button type="button" className={styles.secondaryButton} onClick={secondaryAction.onClick}>
                {secondaryAction.label}
              </button>
            )}
          </div>
        )}
      </div>
    </div>
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
  const { userId, isAuthenticated } = useUserStore();
  const isLoggedIn = isAuthenticated();
  const currentUserIdKey = String(userId || 0);
  const isOwnProfile = currentUserIdKey !== '0' && currentUserIdKey === route.userId;
  const [profile, setProfile] = useState<PublicUserProfile | null>(null);
  const [stats, setStats] = useState<PublicUserStats | null>(null);
  const [followStatus, setFollowStatus] = useState<UserFollowStatus | null>(null);
  const [loadingProfile, setLoadingProfile] = useState(true);
  const [loadingFollowStatus, setLoadingFollowStatus] = useState(false);
  const [togglingFollow, setTogglingFollow] = useState(false);
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
      setLoadingFollowStatus(isLoggedIn && !isOwnProfile);

      try {
        const [profileResult, statsResult, followResult] = await Promise.all([
          getPublicProfile(route.userId),
          getPublicUserStats(route.userId),
          isLoggedIn && !isOwnProfile
            ? getFollowStatus(route.userId).catch(() => null)
            : Promise.resolve(null)
        ]);

        if (requestId !== profileRequestIdRef.current) {
          return;
        }

        setProfile(profileResult);
        setStats(statsResult);
        setFollowStatus(followResult);
      } catch (error) {
        if (requestId !== profileRequestIdRef.current) {
          return;
        }

        const message = error instanceof Error ? error.message : String(error);
        setProfile(null);
        setStats(null);
        setFollowStatus(null);
        setProfileError(message);
      } finally {
        if (requestId === profileRequestIdRef.current) {
          setLoadingProfile(false);
          setLoadingFollowStatus(false);
        }
      }
    };

    void loadProfile();
  }, [isLoggedIn, isOwnProfile, profileReloadToken, route.userId]);

  useEffect(() => {
    const requestId = ++contentRequestIdRef.current;

    const loadContent = async () => {
      setLoadingContent(true);
      setContentError(null);

      try {
        if (route.tab === 'posts') {
          const pageModel = await getPublicUserPosts(route.userId, route.page, 10);
          if (requestId !== contentRequestIdRef.current) {
            return;
          }

          const nextTotalPages = Math.max(pageModel.pageCount || 1, 1);
          if (route.page > nextTotalPages) {
            onNavigate({
              kind: 'detail',
              userId: route.userId,
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

        const pageModel = await getPublicUserComments(route.userId, route.page, 10);
        if (requestId !== contentRequestIdRef.current) {
          return;
        }

        const nextTotalPages = Math.max(pageModel.pageCount || 1, 1);
        if (route.page > nextTotalPages) {
          onNavigate({
            kind: 'detail',
            userId: route.userId,
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
  }, [contentReloadToken, onNavigate, route.page, route.tab, route.userId]);

  useEffect(() => {
    if (loadingProfile) {
      return;
    }

    const nextTitle = profile?.voUserName?.trim()
      ? `${profile.voUserName} · ${t('profile.public.title')}`
      : t('profile.public.title');

    document.title = nextTitle;
  }, [loadingProfile, profile?.voUserName, t]);

  const avatarUrl = useMemo(
    () => resolveMediaUrl(profile?.voAvatarThumbnailUrl || profile?.voAvatarUrl),
    [profile?.voAvatarThumbnailUrl, profile?.voAvatarUrl]
  );

  const displayName = profile?.voDisplayName?.trim() || null;
  const userName = profile?.voUserName?.trim() || t('common.userFallback', { id: route.userId });
  const canToggleFollow = isLoggedIn && !isOwnProfile && !!profile;
  const backLabel = backAction?.mode === 'discover'
    ? t('public.shell.backToDiscover')
    : backAction
      ? t('public.shell.backToSource')
      : t('profile.public.backToForum');
  const handleBack = backAction?.onBack ?? onNavigateToForumList;

  const handleToggleFollow = async () => {
    if (!canToggleFollow || togglingFollow) {
      return;
    }

    setTogglingFollow(true);
    try {
      const nextStatus = followStatus?.voIsFollowing
        ? await unfollowUser(route.userId)
        : await followUser(route.userId);
      setFollowStatus(nextStatus);
    } catch {
      setProfileReloadToken((current) => current + 1);
    } finally {
      setTogglingFollow(false);
    }
  };

  const handleTabChange = (tab: PublicProfileTab) => {
    if (tab === route.tab && route.page === 1) {
      return;
    }

    onNavigate({
      kind: 'detail',
      userId: route.userId,
      tab,
      page: 1
    }, { replace: true });
  };

  return (
    <div className={styles.page} ref={pageRef}>
      <header className={styles.hero}>
        <div className={styles.heroInner}>
          <div className={styles.brand}>
            <span className={styles.brandMark}>人</span>
            <span className={styles.brandText}>
              <span className={styles.brandName}>{t('profile.public.title')}</span>
              <span className={styles.brandSubline}>{t('profile.public.shellLabel')}</span>
            </span>
          </div>
          <div className={styles.heroActions}>
            <button type="button" className={styles.discoverLink} onClick={onNavigateToDiscover}>
              <Icon icon="mdi:compass-outline" size={18} />
              <span>{t('public.shell.discoverAction')}</span>
            </button>
            <a className={styles.desktopLink} href="/">
              <Icon icon="mdi:view-dashboard-outline" size={18} />
              <span>WebOS</span>
            </a>
          </div>
        </div>
      </header>

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
              onClick: handleBack
            }}
          />
        ) : (
          <>
            <section className={styles.summaryCard}>
              <div className={styles.summaryHeader}>
                <span className={styles.readOnlyBadge}>{t('profile.public.readOnlyBadge')}</span>
              </div>
              <button type="button" className={styles.summaryBackLink} onClick={handleBack}>
                <Icon icon="mdi:arrow-left" size={16} />
                <span>{backLabel}</span>
              </button>
              <p className={styles.summaryIntro}>{t('profile.public.intro')}</p>

              <div className={styles.identityRow}>
                <div
                  className={styles.avatar}
                  style={avatarUrl ? undefined : buildAvatarStyle(userName)}
                  aria-hidden="true"
                >
                  {avatarUrl ? (
                    <img className={styles.avatarImage} src={avatarUrl} alt={userName} loading="lazy" />
                  ) : (
                    buildAvatarText(userName)
                  )}
                </div>

                <div className={styles.identityBody}>
                  <div className={styles.identityText}>
                    <h1 className={styles.userName}>{userName}</h1>
                    {displayName && (
                      <p className={styles.displayName}>{t('profile.displayName', { name: displayName })}</p>
                    )}
                    <p className={styles.joinedAt}>
                      {t('profile.publicSince', {
                        time: formatDateTimeByTimeZone(profile?.voCreateTime ?? '', displayTimeZone)
                      })}
                    </p>
                    <p className={styles.viewHint}>{t('profile.publicViewHint')}</p>
                  </div>

                  {canToggleFollow && (
                    <div className={styles.followPanel}>
                      {followStatus && (
                        <span className={styles.followStats}>
                          {t('forum.postDetail.followStats', {
                            followers: followStatus.voFollowerCount,
                            following: followStatus.voFollowingCount
                          })}
                        </span>
                      )}
                      <button
                        type="button"
                        className={`${styles.primaryButton} ${followStatus?.voIsFollowing ? styles.followingButton : ''}`}
                        onClick={() => void handleToggleFollow()}
                        disabled={loadingFollowStatus || togglingFollow}
                        title={followStatus?.voIsFollowing ? t('forum.postDetail.follow.unfollowTitle') : t('forum.postDetail.follow.followTitle')}
                      >
                        {loadingFollowStatus || togglingFollow
                          ? t('forum.postDetail.follow.loading')
                          : followStatus?.voIsFollowing
                            ? t('forum.postDetail.follow.following')
                            : t('forum.postDetail.follow.follow')}
                      </button>
                    </div>
                  )}
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
                <div>
                  <p className={styles.kicker}>Phase 2-2</p>
                  <h2 className={styles.sectionTitle}>{t('profile.public.contentTitle')}</h2>
                </div>
                <div className={styles.tabs}>
                  <button
                    type="button"
                    className={`${styles.tabButton} ${route.tab === 'posts' ? styles.tabButtonActive : ''}`}
                    onClick={() => handleTabChange('posts')}
                  >
                    {t('profile.tab.userPosts')}
                  </button>
                  <button
                    type="button"
                    className={`${styles.tabButton} ${route.tab === 'comments' ? styles.tabButtonActive : ''}`}
                    onClick={() => handleTabChange('comments')}
                  >
                    {t('profile.tab.userComments')}
                  </button>
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
                    {posts.map((post) => (
                    <article
                      key={String(post.voId)}
                      className={styles.contentItem}
                      onClick={() => onNavigateToForumPost(String(post.voId))}
                      >
                        <div className={styles.itemTopRow}>
                          <span className={styles.itemType}>{t('profile.tab.userPosts')}</span>
                          <span className={styles.itemTime}>
                            {formatDateTimeByTimeZone(post.voCreateTime, displayTimeZone)}
                          </span>
                        </div>
                        <h3 className={styles.itemTitle}>{post.voTitle}</h3>
                        <p className={styles.itemExcerpt}>{buildExcerpt(post) || t('profile.public.noSummary')}</p>
                        <div className={styles.itemMeta}>
                          <span>{t('profile.stats.likes', { count: post.voLikeCount ?? 0 })}</span>
                          <span>{t('profile.stats.comments', { count: post.voCommentCount ?? 0 })}</span>
                          <span>{t('forum.postDetail.views', { count: post.voViewCount ?? 0 })}</span>
                        </div>
                      </article>
                    ))}
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
                  {comments.map((comment) => (
                    <article
                      key={String(comment.voId)}
                      className={styles.contentItem}
                      onClick={() => onNavigateToForumPost(String(comment.voPostId), String(comment.voId))}
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
                      <div className={styles.itemMeta}>
                        <span>{t('profile.stats.likes', { count: comment.voLikeCount ?? 0 })}</span>
                        <button
                          type="button"
                          className={styles.inlineLinkButton}
                          onClick={(event) => {
                            event.stopPropagation();
                            onNavigateToForumPost(String(comment.voPostId), String(comment.voId));
                          }}
                        >
                          {t('profile.public.openPost')}
                        </button>
                      </div>
                    </article>
                  ))}
                </div>
              )}

              {totalPages > 1 && !loadingContent && !contentError && (
                <div className={styles.pagination}>
                  <button
                    type="button"
                    className={styles.paginationButton}
                    onClick={() => onNavigate({
                      kind: 'detail',
                      userId: route.userId,
                      tab: route.tab,
                      page: Math.max(1, route.page - 1)
                    }, { replace: true })}
                    disabled={route.page === 1}
                  >
                    {t('common.previousPage')}
                  </button>
                  <span className={styles.pageInfo}>
                    {t('common.pageInfo', { current: route.page, total: totalPages })}
                  </span>
                  <button
                    type="button"
                    className={styles.paginationButton}
                    onClick={() => onNavigate({
                      kind: 'detail',
                      userId: route.userId,
                      tab: route.tab,
                      page: Math.min(totalPages, route.page + 1)
                    }, { replace: true })}
                    disabled={route.page === totalPages}
                  >
                    {t('common.nextPage')}
                  </button>
                </div>
              )}
            </section>
          </>
        )}
      </main>
    </div>
  );
};
