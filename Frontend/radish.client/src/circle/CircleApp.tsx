import { useCallback, useEffect, useMemo, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { getApiBaseUrl } from '@/config/env';
import { WebStateSlot } from '@/components/web-shell';
import {
  getMyFollowers,
  getMyFollowing,
  getMyFollowingFeed,
  getMyFollowSummary,
  type UserFollowSummary,
  type UserFollowUser
} from '@/api/userFollow';
import type { PostItem } from '@/types/forum';
import { PublicShellHeader } from '@/public/components/PublicShellHeader';
import { buildPublicForumPath, type PublicForumDetailRoute } from '@/public/forumRouteState';
import {
  normalizePublicPostId,
  resolvePublicPostRouteIdentifier,
  resolvePublicUserRouteIdentifier,
} from '@/public/publicId';
import { buildPublicProfilePath, type PublicProfileRoute } from '@/public/profileRouteState';
import {
  createPublicRouteSourceState,
  rememberPublicRouteSourceTransfer,
  type PublicContentRouteDescriptor
} from '@/public/publicRouteNavigation';
import { bootstrapAuth, hydrateAuthUser } from '@/services/authBootstrap';
import { redirectToLogin } from '@/services/auth';
import { buildCircleReturnPath } from '@/services/authReturnPath';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { formatDateTimeByTimeZone, getBrowserTimeZoneId, DEFAULT_TIME_ZONE } from '@/utils/dateTime';
import { resolveMediaUrl } from '@/utils/media';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import { log } from '@/utils/logger';
import {
  buildCirclePath,
  createDefaultCircleRoute,
  parseCircleRoute,
  type CircleRoute,
  type CircleTab
} from './circleRouteState';
import styles from './CircleApp.module.css';

const PAGE_SIZE = 10;

const tabIcons: Record<CircleTab, string> = {
  feed: 'mdi:newspaper-variant-outline',
  following: 'mdi:account-heart-outline',
  followers: 'mdi:account-group-outline',
};

function resolveInitialCircleRoute(): CircleRoute {
  if (typeof window === 'undefined') {
    return createDefaultCircleRoute();
  }

  return parseCircleRoute(window.location.pathname, window.location.search) ?? createDefaultCircleRoute();
}

function buildPostRoute(post: PostItem): PublicForumDetailRoute {
  const publicId = normalizePublicPostId(post.voPublicId);
  return {
    kind: 'detail',
    postId: resolvePublicPostRouteIdentifier(post),
    ...(publicId ? { postPublicId: publicId } : {})
  };
}

function buildUserRoute(user: UserFollowUser): PublicProfileRoute {
  return {
    kind: 'detail',
    userId: resolvePublicUserRouteIdentifier(user) ?? String(user.voUserId),
    tab: 'posts',
    page: 1
  };
}

function shouldHandleCircleSourceLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function buildAvatarText(name: string): string {
  const source = name.trim();
  return source ? source.charAt(0).toUpperCase() : '?';
}

function buildAvatarStyle(seed: string) {
  let hash = 0;
  for (let index = 0; index < seed.length; index += 1) {
    hash = seed.charCodeAt(index) + ((hash << 5) - hash);
  }

  const hue = Math.abs(hash) % 360;
  return {
    backgroundColor: `hsl(${hue} 78% 92%)`,
    color: `hsl(${hue} 42% 28%)`
  };
}

export const CircleApp = () => {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const displayTimeZone = useMemo(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE), []);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const [route, setRoute] = useState<CircleRoute>(() => resolveInitialCircleRoute());
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const [summary, setSummary] = useState<UserFollowSummary>({ voFollowerCount: 0, voFollowingCount: 0 });
  const [loading, setLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [feedItems, setFeedItems] = useState<PostItem[]>([]);
  const [userItems, setUserItems] = useState<UserFollowUser[]>([]);
  const [total, setTotal] = useState(0);
  const [avatarErrorUserIds, setAvatarErrorUserIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    const cleanup = bootstrapAuth({ apiBaseUrl });
    let cancelled = false;

    hydrateAuthUser({ apiBaseUrl })
      .catch((error) => {
        log.warn('CircleApp', '圈子页登录态初始化失败', error);
        return null;
      })
      .finally(() => {
        if (!cancelled) {
          setAuthReady(true);
        }
      });

    return () => {
      cancelled = true;
      cleanup();
    };
  }, [apiBaseUrl]);

  useEffect(() => {
    document.title = `${t('circle.title')} · Radish`;
  }, [t]);

  useEffect(() => {
    const handlePopState = () => {
      setRoute(resolveInitialCircleRoute());
    };

    window.addEventListener('popstate', handlePopState);
    return () => {
      window.removeEventListener('popstate', handlePopState);
    };
  }, []);

  useEffect(() => {
    const canonicalPath = buildCirclePath(route);
    const currentPath = `${window.location.pathname}${window.location.search}`;
    if (currentPath !== canonicalPath) {
      window.history.replaceState(window.history.state, '', canonicalPath);
    }
  }, [route]);

  useEffect(() => {
    if (!authReady || loggedIn || redirecting) {
      return;
    }

    setRedirecting(true);
    redirectToLogin({
      returnPath: buildCircleReturnPath({ tab: route.tab, page: route.page })
    });
  }, [authReady, loggedIn, redirecting, route.page, route.tab]);

  const navigateToRoute = useCallback((nextRoute: CircleRoute) => {
    const nextPath = buildCirclePath(nextRoute);
    const currentPath = `${window.location.pathname}${window.location.search}`;
    if (currentPath !== nextPath) {
      window.history.pushState(window.history.state, '', nextPath);
    }

    setRoute(nextRoute);
  }, []);

  const loadCircleData = useCallback(async (nextRoute: CircleRoute) => {
    setLoading(true);
    setErrorMessage(null);

    try {
      const summaryPromise = getMyFollowSummary()
        .then(setSummary)
        .catch((error) => {
          log.warn('CircleApp', '加载圈子汇总失败', error);
        });

      const pageResult = nextRoute.tab === 'feed'
        ? await getMyFollowingFeed(nextRoute.page, PAGE_SIZE)
        : nextRoute.tab === 'following'
          ? await getMyFollowing(nextRoute.page, PAGE_SIZE)
          : await getMyFollowers(nextRoute.page, PAGE_SIZE);

      await summaryPromise;

      if (nextRoute.tab === 'feed') {
        setFeedItems(pageResult.voItems as PostItem[]);
        setUserItems([]);
      } else {
        setFeedItems([]);
        setUserItems(pageResult.voItems as UserFollowUser[]);
      }

      setTotal(pageResult.voTotal || 0);
    } catch (error) {
      log.error('CircleApp', '加载圈子内容失败', error);
      setFeedItems([]);
      setUserItems([]);
      setTotal(0);
      setErrorMessage(error instanceof Error ? error.message : t('circle.loadFailedDescription'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    if (!authReady || !loggedIn) {
      return;
    }

    void loadCircleData(route);
  }, [authReady, loadCircleData, loggedIn, route]);

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const mutualUserCount = userItems.filter(item => item.voIsMutualFollow).length;
  const feedEngagementCount = feedItems.reduce(
    (sum, item) => sum + (item.voCommentCount || 0) + (item.voLikeCount || 0),
    0
  );

  const switchTab = (tab: CircleTab) => {
    navigateToRoute({ tab, page: 1 });
  };

  const movePage = (page: number) => {
    navigateToRoute({ ...route, page: Math.min(Math.max(1, page), totalPages) });
  };

  const rememberCircleSourceForPublicTarget = (
    event: MouseEvent<HTMLAnchorElement>,
    targetPath: string,
    targetRoute: PublicContentRouteDescriptor
  ) => {
    if (!shouldHandleCircleSourceLink(event)) {
      return;
    }

    rememberPublicRouteSourceTransfer(
      targetPath,
      createPublicRouteSourceState(
        {},
        { app: 'circle', route },
        targetRoute
      )
    );
  };

  const renderUserAvatar = (user: UserFollowUser) => {
    const displayName = resolveVisibleUserDisplayName(user, t('common.unknownUser'));
    const userIdKey = String(user.voUserId);
    const avatarUrl = avatarErrorUserIds.has(userIdKey) ? null : resolveMediaUrl(user.voAvatarUrl, apiBaseUrl);

    return (
      <span
        className={styles.avatar}
        style={avatarUrl ? undefined : buildAvatarStyle(displayName)}
        title={displayName}
      >
        {avatarUrl ? (
          <img
            src={avatarUrl}
            alt={displayName}
            className={styles.avatarImage}
            loading="lazy"
            onError={() => {
              setAvatarErrorUserIds((current) => {
                const next = new Set(current);
                next.add(userIdKey);
                return next;
              });
            }}
          />
        ) : (
          buildAvatarText(displayName)
        )}
      </span>
    );
  };

  const renderFeed = () => {
    if (feedItems.length === 0) {
      return (
        <section className={styles.stateShell}>
          <WebStateSlot
            tone="empty"
            icon="mdi:forum-outline"
            title={t('circle.empty.feedTitle')}
            description={t('circle.feedEmpty')}
          />
        </section>
      );
    }

    return (
      <div className={styles.feedList}>
        {feedItems.map((item) => {
          const postRoute = buildPostRoute(item);
          const postPath = buildPublicForumPath(postRoute);

          return (
            <article key={item.voId} className={styles.feedItem}>
              <h3 className={styles.feedHeading}>
                <a
                  className={styles.feedTitle}
                  href={postPath}
                  onClick={(event) => rememberCircleSourceForPublicTarget(event, postPath, { app: 'forum', route: postRoute })}
                >
                  {item.voTitle}
                </a>
              </h3>
              {item.voSummary ? <p className={styles.feedSummary}>{item.voSummary}</p> : null}
              <div className={styles.metaRow}>
                <span>{t('circle.author', { name: item.voAuthorName || t('common.unknownUser') })}</span>
                {item.voCreateTime ? <span>{formatDateTimeByTimeZone(item.voCreateTime, displayTimeZone)}</span> : null}
                <span className={styles.metric}>
                  <Icon icon="mdi:comment-text-outline" size={15} />
                  <span>{item.voCommentCount || 0}</span>
                </span>
                <span className={styles.metric}>
                  <Icon icon="mdi:heart-outline" size={15} />
                  <span>{item.voLikeCount || 0}</span>
                </span>
              </div>
            </article>
          );
        })}
      </div>
    );
  };

  const renderUsers = () => {
    if (userItems.length === 0) {
      return (
        <section className={styles.stateShell}>
          <WebStateSlot
            tone="empty"
            icon={route.tab === 'following' ? 'mdi:account-heart-outline' : 'mdi:account-group-outline'}
            title={t(route.tab === 'following' ? 'circle.empty.followingTitle' : 'circle.empty.followersTitle')}
            description={route.tab === 'following' ? t('circle.followingEmpty') : t('circle.followersEmpty')}
          />
        </section>
      );
    }

    return (
      <div className={styles.userList}>
        {userItems.map((user) => {
          const displayName = resolveVisibleUserDisplayName(user, t('common.unknownUser'));
          const displayHandle = resolveVisibleUserHandle(user, displayName);
          const userRoute = buildUserRoute(user);
          const userPath = buildPublicProfilePath(userRoute);

          return (
            <a
              key={user.voUserId}
              className={styles.userItem}
              href={userPath}
              onClick={(event) => rememberCircleSourceForPublicTarget(event, userPath, { app: 'profile', route: userRoute })}
            >
              {renderUserAvatar(user)}
              <span className={styles.userBody}>
                <span className={styles.userNameRow}>
                  <span className={styles.userName}>{displayName}</span>
                  {displayHandle ? <span className={styles.userDisplayHandle}>{displayHandle}</span> : null}
                  {user.voIsMutualFollow ? <span className={styles.mutualBadge}>{t('circle.mutualFollow')}</span> : null}
                </span>
                <span className={styles.userMeta}>
                  {t('circle.followTime', { time: formatDateTimeByTimeZone(user.voFollowTime, displayTimeZone) })}
                </span>
              </span>
              <Icon icon="mdi:chevron-right" size={20} />
            </a>
          );
        })}
      </div>
    );
  };

  const renderContent = () => {
    if (!authReady || redirecting || !loggedIn) {
      return (
        <section className={styles.stateShell}>
          <WebStateSlot
            tone="auth"
            icon="mdi:account-clock-outline"
            title={redirecting ? t('circle.redirectingTitle') : t('circle.authLoadingTitle')}
            description={redirecting ? t('circle.redirectingDescription') : t('circle.authLoadingDescription')}
          />
        </section>
      );
    }

    if (loading) {
      return (
        <section className={styles.stateShell}>
          <WebStateSlot
            tone="loading"
            icon="mdi:progress-clock"
            title={t('circle.loadingTitle')}
            description={t('circle.loadingDescription')}
          />
        </section>
      );
    }

    if (errorMessage) {
      return (
        <section className={styles.stateShell}>
          <WebStateSlot
            tone="error"
            title={t('circle.loadFailedTitle')}
            description={errorMessage}
            actions={[{
              label: t('common.retry'),
              onClick: () => void loadCircleData(route),
            }]}
          />
        </section>
      );
    }

    return route.tab === 'feed' ? renderFeed() : renderUsers();
  };

  const renderCircleRail = () => {
    const previewItems = route.tab === 'feed' ? feedItems.slice(0, 4) : userItems.slice(0, 4);
    const secondaryMetricValue = route.tab === 'feed' ? feedEngagementCount : mutualUserCount;
    const secondaryMetricKey = route.tab === 'feed' ? 'circle.rail.engagement' : 'circle.rail.mutual';

    return (
      <aside className={styles.circleRail} aria-label={t('circle.rail.label')}>
        <section className={styles.railCard}>
          <div className={styles.railTitleRow}>
            <span className={styles.railIcon}>
              <Icon icon={tabIcons[route.tab]} size={20} />
            </span>
            <div>
              <p className={styles.railKicker}>{t('circle.rail.contextKicker')}</p>
              <h2>{t('circle.rail.contextTitle')}</h2>
            </div>
          </div>
          <div className={styles.railMetrics}>
            <div>
              <strong>{t(`circle.tab.${route.tab}`)}</strong>
              <span>{t('circle.rail.scope')}</span>
            </div>
            <div>
              <strong>{t('common.pageInfo', { current: route.page, total: totalPages })}</strong>
              <span>{t('circle.rail.page')}</span>
            </div>
            <div>
              <strong>{total}</strong>
              <span>{t('circle.rail.total')}</span>
            </div>
            <div>
              <strong>{secondaryMetricValue}</strong>
              <span>{t(secondaryMetricKey)}</span>
            </div>
          </div>
        </section>

        <section className={styles.railCard}>
          <div className={styles.railTitleRow}>
            <span className={styles.railIcon}>
              <Icon icon="mdi:playlist-star" size={20} />
            </span>
            <div>
              <p className={styles.railKicker}>{t('circle.rail.previewKicker')}</p>
              <h2>{t(`circle.rail.preview.${route.tab}`)}</h2>
            </div>
          </div>
          {previewItems.length > 0 ? (
            <div className={styles.railPreviewList}>
              {route.tab === 'feed'
                ? feedItems.slice(0, 4).map((item) => {
                  const postRoute = buildPostRoute(item);
                  const postPath = buildPublicForumPath(postRoute);
                  return (
                    <a
                      className={styles.railPreviewItem}
                      href={postPath}
                      key={item.voId}
                      onClick={(event) => rememberCircleSourceForPublicTarget(event, postPath, { app: 'forum', route: postRoute })}
                    >
                      <strong>{item.voTitle}</strong>
                      <span>{item.voAuthorName || t('common.unknownUser')}</span>
                    </a>
                  );
                })
                : userItems.slice(0, 4).map((user) => {
                  const displayName = resolveVisibleUserDisplayName(user, t('common.unknownUser'));
                  const displayHandle = resolveVisibleUserHandle(user, displayName);
                  const userRoute = buildUserRoute(user);
                  const userPath = buildPublicProfilePath(userRoute);
                  return (
                    <a
                      className={styles.railPreviewItem}
                      href={userPath}
                      key={user.voUserId}
                      onClick={(event) => rememberCircleSourceForPublicTarget(event, userPath, { app: 'profile', route: userRoute })}
                    >
                      <strong>{displayName}</strong>
                      <span>{displayHandle || t('circle.rail.publicProfile')}</span>
                    </a>
                  );
                })}
            </div>
          ) : (
            <p className={styles.railEmpty}>{t('circle.rail.emptyPreview')}</p>
          )}
        </section>

        <section className={styles.railCard}>
          <div className={styles.railTitleRow}>
            <span className={styles.railIcon}>
              <Icon icon="mdi:routes" size={20} />
            </span>
            <div>
              <p className={styles.railKicker}>{t('circle.rail.routeKicker')}</p>
              <h2>{t('circle.rail.routeTitle')}</h2>
            </div>
          </div>
          <p className={styles.railHint}>{t('circle.rail.routeDescription')}</p>
          <div className={styles.railActions}>
            <a href="/forum">
              <Icon icon="mdi:forum-outline" size={17} />
              <span>{t('circle.forumAction')}</span>
            </a>
            <a href="/discover">
              <Icon icon="mdi:compass-outline" size={17} />
              <span>{t('circle.discoverAction')}</span>
            </a>
            <a href="/me">
              <Icon icon="mdi:account-circle-outline" size={17} />
              <span>{t('circle.meAction')}</span>
            </a>
          </div>
        </section>
      </aside>
    );
  };

  return (
    <div className={styles.page}>
      <PublicShellHeader
        variant="private"
        activeKey="me"
        brandMark="萝"
        brandName={t('circle.title')}
        brandSubline={t('circle.shellSubline')}
        onBrandClick={() => switchTab('feed')}
      />

      <main className={styles.main}>
        <section className={styles.intro}>
          <div className={styles.introBody}>
            <p className={styles.kicker}>{t('circle.heroKicker')}</p>
            <h1 className={styles.introTitle}>{t('circle.heroTitle')}</h1>
            <p className={styles.introDescription}>{t('circle.heroDescription')}</p>
            <div className={styles.summaryGrid} aria-label={t('circle.summaryLabel')}>
              <div className={styles.summaryCard}>
                <span className={styles.summaryIcon}>
                  <Icon icon="mdi:account-heart-outline" size={20} />
                </span>
                <strong>{summary.voFollowingCount}</strong>
                <span>{t('circle.summary.following')}</span>
              </div>
              <div className={styles.summaryCard}>
                <span className={styles.summaryIcon}>
                  <Icon icon="mdi:account-group-outline" size={20} />
                </span>
                <strong>{summary.voFollowerCount}</strong>
                <span>{t('circle.summary.followers')}</span>
              </div>
              <div className={styles.summaryCard}>
                <span className={styles.summaryIcon}>
                  <Icon icon={tabIcons[route.tab]} size={20} />
                </span>
                <strong>{total}</strong>
                <span>{t(`circle.summary.${route.tab}`)}</span>
              </div>
            </div>
          </div>
          <div className={styles.introActions}>
            <a className={styles.forumLink} href="/forum">
              <Icon icon="mdi:forum-outline" size={18} />
              <span>{t('circle.forumAction')}</span>
            </a>
            <a className={styles.forumLink} href="/me">
              <Icon icon="mdi:account-circle-outline" size={18} />
              <span>{t('circle.meAction')}</span>
            </a>
          </div>
        </section>

        <nav className={styles.tabs} aria-label={t('circle.tabsLabel')}>
          {(['feed', 'following', 'followers'] as CircleTab[]).map((tab) => {
            const href = buildCirclePath({ tab, page: 1 });

            return (
              <a
                key={tab}
                className={`${styles.tab} ${route.tab === tab ? styles.activeTab : ''}`}
                href={href}
                aria-current={route.tab === tab ? 'page' : undefined}
                onClick={(event) => {
                  if (!shouldHandleCircleSourceLink(event)) {
                    return;
                  }

                  event.preventDefault();
                  switchTab(tab);
                }}
              >
                {t(`circle.tab.${tab}`)}
              </a>
            );
          })}
        </nav>

        <div className={`${styles.workspace} ${authReady && loggedIn && !loading && !errorMessage ? styles.withRail : ''}`}>
          <section className={styles.contentBlock} aria-labelledby="circle-section-title">
            <div className={styles.sectionHeader}>
              <div>
                <h2 id="circle-section-title">{t(`circle.section.${route.tab}.title`)}</h2>
                <p>{t(`circle.section.${route.tab}.description`)}</p>
              </div>
              {authReady && loggedIn && !errorMessage ? (
                <span className={styles.resultCount}>{t('circle.resultCount', { count: total })}</span>
              ) : null}
            </div>
            <div className={styles.content}>{renderContent()}</div>
          </section>

          {authReady && loggedIn && !loading && !errorMessage ? renderCircleRail() : null}
        </div>

        {authReady && loggedIn && !loading && !errorMessage && totalPages > 1 ? (
          <div className={styles.pagination}>
            {route.page <= 1 ? (
              <button type="button" className={styles.pageButton} disabled>
                {t('common.previousPage')}
              </button>
            ) : (
              <a
                className={styles.pageButton}
                href={buildCirclePath({ ...route, page: route.page - 1 })}
                onClick={(event) => {
                  if (!shouldHandleCircleSourceLink(event)) {
                    return;
                  }

                  event.preventDefault();
                  movePage(route.page - 1);
                }}
              >
                {t('common.previousPage')}
              </a>
            )}
            <span>{t('common.pageInfo', { current: route.page, total: totalPages })}</span>
            {route.page >= totalPages ? (
              <button type="button" className={styles.pageButton} disabled>
                {t('common.nextPage')}
              </button>
            ) : (
              <a
                className={styles.pageButton}
                href={buildCirclePath({ ...route, page: route.page + 1 })}
                onClick={(event) => {
                  if (!shouldHandleCircleSourceLink(event)) {
                    return;
                  }

                  event.preventDefault();
                  movePage(route.page + 1);
                }}
              >
                {t('common.nextPage')}
              </a>
            )}
          </div>
        ) : null}
      </main>
    </div>
  );
};
