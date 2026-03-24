import { lazy, Suspense, useState, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { log } from '@/utils/logger';
import { useUserStore } from '@/stores/userStore';
import { useWindowStore } from '@/stores/windowStore';
import { useCurrentWindow } from '@/desktop/useCurrentWindow';
import { UserInfoCard } from './components/UserInfoCard';
import { getPublicProfile, type LongId, type PublicUserProfile, type UserBrowseHistoryItem } from '@/api/user';
import { followUser, getFollowStatus, unfollowUser, type UserFollowStatus } from '@/api/userFollow';
import { getMyTimePreference, getTimeSettings, updateMyTimePreference } from '@/api/time';
import { getApiBaseUrl } from '@/config/env';
import {
  DEFAULT_TIME_FORMAT,
  DEFAULT_TIME_ZONE,
  formatDateTimeByTimeZone,
  getBrowserTimeZoneId,
  resolveTimeZoneId,
} from '@/utils/dateTime';
import styles from './ProfileApp.module.css';

const UserPostList = lazy(() =>
  import('./components/UserPostList').then((module) => ({ default: module.UserPostList }))
);
const UserCommentList = lazy(() =>
  import('./components/UserCommentList').then((module) => ({ default: module.UserCommentList }))
);
const UserAttachmentList = lazy(() =>
  import('./components/UserAttachmentList').then((module) => ({ default: module.UserAttachmentList }))
);
const UserFollowPanel = lazy(() =>
  import('./components/UserFollowPanel').then((module) => ({ default: module.UserFollowPanel }))
);
const UserBrowseHistoryList = lazy(() =>
  import('./components/UserBrowseHistoryList').then((module) => ({ default: module.UserBrowseHistoryList }))
);

interface UserStats {
  voPostCount: number;
  voCommentCount: number;
  voTotalLikeCount: number;
  voPostLikeCount: number;
  voCommentLikeCount: number;
}

interface ProfileWindowParams {
  userId?: number;
  userName?: string;
  avatarUrl?: string | null;
  displayName?: string | null;
}

function parseProfileWindowParams(input: Record<string, unknown> | undefined): ProfileWindowParams {
  if (!input) {
    return {};
  }

  const rawUserId = typeof input.userId === 'number'
    ? input.userId
    : typeof input.userId === 'string'
      ? Number(input.userId)
      : undefined;

  return {
    userId: rawUserId && Number.isFinite(rawUserId) ? rawUserId : undefined,
    userName: typeof input.userName === 'string' ? input.userName : undefined,
    avatarUrl: typeof input.avatarUrl === 'string' ? input.avatarUrl : null,
    displayName: typeof input.displayName === 'string' ? input.displayName : null,
  };
}

function resolveAvatarUrl(apiBaseUrl: string, url: string | null | undefined): string | null {
  if (!url) return null;
  if (/^https?:\/\//i.test(url)) return url;
  if (url.startsWith('/')) return `${apiBaseUrl}${url}`;
  return `${apiBaseUrl}/${url}`;
}

function buildAvatarText(name: string): string {
  const source = name.trim();
  if (!source) return '?';
  return source.charAt(0).toUpperCase();
}

function normalizePositiveId(value: LongId | null | undefined): number | null {
  const parsed = typeof value === 'number' ? value : typeof value === 'string' ? Number(value) : Number.NaN;
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function getRouteTailSegment(routePath: string): string {
  const normalizedPath = routePath.split('?')[0]?.split('#')[0] || '';
  return normalizedPath.split('/').pop() || '';
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

export const ProfileApp = () => {
  const { t } = useTranslation();
  const { userId, userName, isAuthenticated } = useUserStore();
  const { openApp } = useWindowStore();
  const currentWindow = useCurrentWindow();
  const params = useMemo(() => parseProfileWindowParams(currentWindow?.appParams), [currentWindow?.appParams]);
  const viewingUserId = params.userId && params.userId > 0 ? params.userId : userId;
  const isOwnProfile = viewingUserId === userId;
  const loggedIn = isAuthenticated();
  const [activeTab, setActiveTab] = useState<'posts' | 'comments' | 'browse-history' | 'attachments' | 'social'>('posts');
  const [stats, setStats] = useState<UserStats | null>(null);
  const [loadingStats, setLoadingStats] = useState(true);
  const [systemTimeZone, setSystemTimeZone] = useState(DEFAULT_TIME_ZONE);
  const [displayTimeZone, setDisplayTimeZone] = useState(DEFAULT_TIME_ZONE);
  const [displayTimeFormat, setDisplayTimeFormat] = useState(DEFAULT_TIME_FORMAT);
  const [savingTimeZone, setSavingTimeZone] = useState(false);
  const [publicProfile, setPublicProfile] = useState<PublicUserProfile | null>(null);
  const [loadingPublicProfile, setLoadingPublicProfile] = useState(false);
  const [followStatus, setFollowStatus] = useState<UserFollowStatus | null>(null);
  const [followLoading, setFollowLoading] = useState(false);

  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const viewingUserName = publicProfile?.voUserName?.trim() || params.userName?.trim() || userName || t('common.userFallback', { id: viewingUserId });
  const viewingDisplayName = publicProfile?.voDisplayName?.trim() || params.displayName?.trim() || null;
  const externalAvatarUrl = useMemo(
    () => resolveAvatarUrl(
      apiBaseUrl,
      publicProfile?.voAvatarThumbnailUrl || publicProfile?.voAvatarUrl || params.avatarUrl
    ),
    [apiBaseUrl, params.avatarUrl, publicProfile?.voAvatarThumbnailUrl, publicProfile?.voAvatarUrl]
  );

  useEffect(() => {
    if (!isOwnProfile && (activeTab === 'browse-history' || activeTab === 'attachments' || activeTab === 'social')) {
      setActiveTab('posts');
    }
  }, [activeTab, isOwnProfile]);

  useEffect(() => {
    if (isOwnProfile || !loggedIn || viewingUserId <= 0) {
      setPublicProfile(null);
      setFollowStatus(null);
      setLoadingPublicProfile(false);
      setFollowLoading(false);
      return;
    }

    let cancelled = false;
    const loadExternalProfile = async () => {
      setLoadingPublicProfile(true);

      const [profileResult, followStatusResult] = await Promise.allSettled([
        getPublicProfile(viewingUserId),
        getFollowStatus(viewingUserId),
      ]);

      if (cancelled) {
        return;
      }

      if (profileResult.status === 'fulfilled') {
        setPublicProfile(profileResult.value);
      } else {
        setPublicProfile(null);
        log.error('ProfileApp', '加载公开资料失败：', profileResult.reason);
      }

      if (followStatusResult.status === 'fulfilled') {
        setFollowStatus(followStatusResult.value);
      } else {
        setFollowStatus(null);
        log.error('ProfileApp', '加载关注状态失败：', followStatusResult.reason);
      }

      setLoadingPublicProfile(false);
    };

    void loadExternalProfile();

    return () => {
      cancelled = true;
    };
  }, [isOwnProfile, loggedIn, viewingUserId]);

  const handlePostClick = (postId: number) => {
    openApp('forum');
    log.debug('ProfileApp', `打开帖子: ${postId}`);
  };

  const handleCommentClick = (postId: number, commentId: number) => {
    openApp('forum');
    log.debug('ProfileApp', `打开帖子 ${postId} 的评论 ${commentId}`);
  };

  const handleBrowseHistoryClick = (item: UserBrowseHistoryItem) => {
    const routePath = item.voRoutePath?.trim() || '';
    const targetId = normalizePositiveId(item.voTargetId);

    if (routePath.startsWith('/forum/post/')) {
      const postId = normalizePositiveId(getRouteTailSegment(routePath));
      if (postId) {
        openApp('forum', { postId });
        return;
      }
    }

    if (routePath.startsWith('/shop/product/')) {
      const productId = normalizePositiveId(getRouteTailSegment(routePath));
      if (productId) {
        openApp('shop', { productId });
        return;
      }
    }

    if (routePath.startsWith('/wiki/doc/')) {
      const routeTarget = decodeURIComponent(getRouteTailSegment(routePath));
      const documentId = normalizePositiveId(routeTarget);
      if (documentId) {
        openApp('document', { documentId });
        return;
      }

      if (routeTarget) {
        openApp('document', { slug: routeTarget });
        return;
      }
    }

    if (item.voTargetType === 'Post' && targetId) {
      openApp('forum', { postId: targetId });
      return;
    }

    if (item.voTargetType === 'Product' && targetId) {
      openApp('shop', { productId: targetId });
      return;
    }

    if (item.voTargetType === 'Wiki') {
      if (item.voTargetSlug?.trim()) {
        openApp('document', { slug: item.voTargetSlug.trim() });
        return;
      }

      if (targetId) {
        openApp('document', { documentId: targetId });
      }
    }
  };

  const handleUserClick = (targetUserId: number, targetUserName: string, avatarUrl?: string | null, displayName?: string | null) => {
    if (targetUserId === userId) {
      openApp('profile');
      return;
    }

    openApp('profile', {
      userId: targetUserId,
      userName: targetUserName,
      avatarUrl: avatarUrl ?? null,
      displayName: displayName ?? null,
    });
  };

  useEffect(() => {
    if (loggedIn && viewingUserId > 0) {
      void loadStats();
      void loadTimeSettings();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loggedIn, viewingUserId, userId]);

  const loadStats = async () => {
    setLoadingStats(true);
    try {
      const response = await fetch(
        `${apiBaseUrl}/api/v1/User/GetUserStats?userId=${viewingUserId}`
      );
      const json = await response.json();
      if (json.isSuccess && json.responseData) {
        setStats(json.responseData);
      }
    } catch (error) {
      log.error('加载统计信息失败:', error);
    } finally {
      setLoadingStats(false);
    }
  };

  const loadTimeSettings = async () => {
    let fallbackSystemTimeZone = DEFAULT_TIME_ZONE;
    let fallbackDisplayTimeFormat = DEFAULT_TIME_FORMAT;

    try {
      const settings = await getTimeSettings();
      fallbackSystemTimeZone = resolveTimeZoneId(settings.voDefaultTimeZoneId, DEFAULT_TIME_ZONE);
      fallbackDisplayTimeFormat = settings.voDisplayFormat?.trim() || DEFAULT_TIME_FORMAT;
    } catch (error) {
      log.warn('ProfileApp', '加载系统时间配置失败，已回退默认配置');
      log.error('ProfileApp', '加载系统时间配置失败：', error);
    }

    try {
      const preference = await getMyTimePreference();
      const resolvedSystemTimeZone = resolveTimeZoneId(
        preference.voSystemDefaultTimeZoneId,
        fallbackSystemTimeZone
      );
      const resolvedDisplayTimeFormat = preference.voDisplayFormat?.trim() || fallbackDisplayTimeFormat;
      const browserTimeZone = getBrowserTimeZoneId(resolvedSystemTimeZone);
      const initialDisplayTimeZone = preference.voIsCustomized
        ? resolveTimeZoneId(preference.voTimeZoneId, browserTimeZone)
        : browserTimeZone;

      setSystemTimeZone(resolvedSystemTimeZone);
      setDisplayTimeFormat(resolvedDisplayTimeFormat);
      setDisplayTimeZone(initialDisplayTimeZone);

      if (!preference.voIsCustomized) {
        try {
          const updatedPreference = await updateMyTimePreference(initialDisplayTimeZone);
          const updatedDisplayTimeZone = resolveTimeZoneId(
            updatedPreference.voTimeZoneId,
            initialDisplayTimeZone
          );
          setDisplayTimeZone(updatedDisplayTimeZone);
        } catch (error) {
          log.warn('ProfileApp', '初始化用户时区偏好失败，已使用浏览器时区展示');
          log.error('ProfileApp', '初始化用户时区偏好失败：', error);
        }
      }
    } catch (error) {
      const browserTimeZone = getBrowserTimeZoneId(fallbackSystemTimeZone);

      setSystemTimeZone(fallbackSystemTimeZone);
      setDisplayTimeFormat(fallbackDisplayTimeFormat);
      setDisplayTimeZone(browserTimeZone);

      log.warn('ProfileApp', '加载用户时区偏好失败，已回退浏览器时区');
      log.error('ProfileApp', '加载用户时区偏好失败：', error);
    }
  };

  const handleTimeZoneChange = async (timeZoneId: string) => {
    const resolvedTimeZone = resolveTimeZoneId(timeZoneId, systemTimeZone);
    if (savingTimeZone) {
      return;
    }

    setSavingTimeZone(true);
    try {
      const updatedPreference = await updateMyTimePreference(resolvedTimeZone);
      const updatedDisplayTimeZone = resolveTimeZoneId(updatedPreference.voTimeZoneId, resolvedTimeZone);
      setDisplayTimeZone(updatedDisplayTimeZone);
      log.info('ProfileApp', `用户时区已切换为 ${updatedDisplayTimeZone}`);
    } catch (error) {
      log.error('ProfileApp', '保存用户时区偏好失败：', error);
      throw error;
    } finally {
      setSavingTimeZone(false);
    }
  };

  const handleToggleFollow = async () => {
    if (!loggedIn || isOwnProfile || viewingUserId <= 0 || followLoading) {
      return;
    }

    setFollowLoading(true);
    try {
      const nextStatus = followStatus?.voIsFollowing
        ? await unfollowUser(viewingUserId)
        : await followUser(viewingUserId);
      setFollowStatus(nextStatus);
    } catch (error) {
      log.error('ProfileApp', '切换关注状态失败：', error);
    } finally {
      setFollowLoading(false);
    }
  };

  if (!loggedIn) {
    return (
      <div className={styles.container}>
        <div className={styles.notLoggedIn}>
          <p>{t('profile.loginRequired')}</p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h1 className={styles.title}>
          {isOwnProfile ? t('profile.title.self') : t('profile.title.other', { userName: viewingUserName })}
        </h1>
      </div>

      <div className={styles.content}>
        {isOwnProfile ? (
          <UserInfoCard
            userId={viewingUserId}
            userName={viewingUserName}
            stats={stats || undefined}
            loading={loadingStats}
            apiBaseUrl={apiBaseUrl}
            displayTimeZone={displayTimeZone}
            systemTimeZone={systemTimeZone}
            displayTimeFormat={displayTimeFormat}
            savingTimeZone={savingTimeZone}
            onTimeZoneChange={handleTimeZoneChange}
          />
        ) : (
          <section className={styles.externalProfileCard}>
            <div className={styles.externalProfileHeader}>
              <div
                className={styles.externalAvatar}
                style={externalAvatarUrl ? undefined : buildAvatarStyle(viewingUserName)}
                title={viewingDisplayName || viewingUserName}
              >
                {externalAvatarUrl ? (
                  <img src={externalAvatarUrl} alt={viewingUserName} className={styles.externalAvatarImage} loading="lazy" />
                ) : (
                  buildAvatarText(viewingUserName)
                )}
              </div>
              <div className={styles.externalInfo}>
                <div className={styles.externalNameRow}>
                  <h2 className={styles.externalName}>{viewingUserName}</h2>
                  <button
                    type="button"
                    className={`${styles.followButton} ${followStatus?.voIsFollowing ? styles.followingButton : ''}`}
                    onClick={() => {
                      void handleToggleFollow();
                    }}
                    disabled={loadingPublicProfile || followLoading}
                    title={followStatus?.voIsFollowing
                      ? t('forum.postDetail.follow.unfollowTitle')
                      : t('forum.postDetail.follow.followTitle')}
                  >
                    {followLoading
                      ? t('forum.postDetail.follow.loading')
                      : followStatus?.voIsFollowing
                        ? t('forum.postDetail.follow.following')
                        : t('forum.postDetail.follow.follow')}
                  </button>
                </div>
                {viewingDisplayName ? (
                  <div className={styles.externalSubtle}>
                    {t('profile.displayName', { name: viewingDisplayName })}
                  </div>
                ) : null}
                {publicProfile?.voCreateTime ? (
                  <div className={styles.externalSubtle}>
                    {t('profile.publicSince', {
                      time: formatDateTimeByTimeZone(publicProfile.voCreateTime, displayTimeZone)
                    })}
                  </div>
                ) : null}
                <div className={styles.externalSubtle}>
                  {loadingPublicProfile ? t('profile.publicLoading') : t('profile.publicViewHint')}
                </div>
              </div>
            </div>
            <div className={styles.externalStats}>
              <div className={styles.externalStatItem}>
                <Icon icon="mdi:file-document-outline" size={18} />
                <span>{t('profile.stats.postsLabel')} {loadingStats ? '--' : stats?.voPostCount ?? 0}</span>
              </div>
              <div className={styles.externalStatItem}>
                <Icon icon="mdi:comment-text-outline" size={18} />
                <span>{t('profile.stats.commentsLabel')} {loadingStats ? '--' : stats?.voCommentCount ?? 0}</span>
              </div>
              <div className={styles.externalStatItem}>
                <Icon icon="mdi:heart-outline" size={18} />
                <span>{t('profile.stats.likesLabel')} {loadingStats ? '--' : stats?.voTotalLikeCount ?? 0}</span>
              </div>
              <div className={styles.externalStatItem}>
                <Icon icon="mdi:account-heart-outline" size={18} />
                <span>{t('profile.social.summary.followers')} {loadingPublicProfile ? '--' : followStatus?.voFollowerCount ?? 0}</span>
              </div>
              <div className={styles.externalStatItem}>
                <Icon icon="mdi:account-arrow-right-outline" size={18} />
                <span>{t('profile.social.summary.following')} {loadingPublicProfile ? '--' : followStatus?.voFollowingCount ?? 0}</span>
              </div>
            </div>
          </section>
        )}

        <div className={styles.tabs}>
          <button
            className={`${styles.tab} ${activeTab === 'posts' ? styles.active : ''}`}
            onClick={() => setActiveTab('posts')}
          >
            {isOwnProfile ? t('profile.tab.myPosts') : t('profile.tab.userPosts')}
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'comments' ? styles.active : ''}`}
            onClick={() => setActiveTab('comments')}
          >
            {isOwnProfile ? t('profile.tab.myComments') : t('profile.tab.userComments')}
          </button>
          {isOwnProfile ? (
            <>
              <button
                className={`${styles.tab} ${activeTab === 'browse-history' ? styles.active : ''}`}
                onClick={() => setActiveTab('browse-history')}
              >
                {t('profile.tab.browseHistory')}
              </button>
              <button
                className={`${styles.tab} ${activeTab === 'attachments' ? styles.active : ''}`}
                onClick={() => setActiveTab('attachments')}
              >
                {t('profile.tab.attachments')}
              </button>
              <button
                className={`${styles.tab} ${activeTab === 'social' ? styles.active : ''}`}
                onClick={() => setActiveTab('social')}
              >
                {t('profile.tab.social')}
              </button>
            </>
          ) : null}
        </div>

        <div className={styles.tabContent}>
          <Suspense fallback={<div className={styles.notLoggedIn}>{t('common.loading')}</div>}>
            {activeTab === 'posts' && (
              <UserPostList
                userId={viewingUserId}
                apiBaseUrl={apiBaseUrl}
                onPostClick={handlePostClick}
                displayTimeZone={displayTimeZone}
              />
            )}
            {activeTab === 'comments' && (
              <UserCommentList
                userId={viewingUserId}
                apiBaseUrl={apiBaseUrl}
                onCommentClick={handleCommentClick}
                displayTimeZone={displayTimeZone}
              />
            )}
            {isOwnProfile && activeTab === 'browse-history' && (
              <UserBrowseHistoryList
                displayTimeZone={displayTimeZone}
                onItemClick={handleBrowseHistoryClick}
              />
            )}
            {isOwnProfile && activeTab === 'attachments' && (
              <UserAttachmentList apiBaseUrl={apiBaseUrl} displayTimeZone={displayTimeZone} />
            )}
            {isOwnProfile && activeTab === 'social' && (
              <UserFollowPanel
                displayTimeZone={displayTimeZone}
                onPostClick={handlePostClick}
                onUserClick={handleUserClick}
              />
            )}
          </Suspense>
        </div>
      </div>
    </div>
  );
};
