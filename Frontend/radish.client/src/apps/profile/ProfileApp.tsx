import { lazy, Suspense, useCallback, useState, useEffect, useMemo, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { log } from '@/utils/logger';
import { useUserStore } from '@/stores/userStore';
import { useWindowStore } from '@/stores/windowStore';
import { useCurrentWindow } from '@/desktop/useCurrentWindow';
import { useWindowCloseGuard } from '@/desktop/useWindowCloseGuard';
import { useBrowserNavigationLock } from '@/bootstrap/browserNavigationLock';
import { UserInfoCard } from './components/UserInfoCard';
import { getPublicProfile, getUserStats, type LongId, type PublicUserProfile, type UserBrowseHistoryItem } from '@/api/user';
import { followUser, getFollowStatus, unfollowUser, type UserFollowStatus } from '@/api/userFollow';
import { getMyTimePreference, updateMyTimePreference } from '@/api/time';
import { getApiBaseUrl } from '@/config/env';
import {
  DEFAULT_TIME_FORMAT,
  DEFAULT_TIME_ZONE,
  formatDateTimeByTimeZone,
  getBrowserTimeZoneId,
  resolveTimeZoneId,
} from '@/utils/dateTime';
import { buildForumAppParams } from '@/utils/forumNavigation';
import {
  openWorkspaceNavigationTarget,
  resolveBrowseHistoryWorkspaceTarget,
} from '@/utils/workspaceNavigation';
import { resolveMediaUrl } from '@/utils/media';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import { reuseInFlightRequest } from './requestDedup';
import {
  resolveFailedSnapshotState,
  type ProfileAuthorityState,
  type ProfileStats,
} from './profileAuthority';
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
const UserQuickReplyList = lazy(() =>
  import('./components/UserQuickReplyList').then((module) => ({ default: module.UserQuickReplyList }))
);

interface ProfileWindowParams {
  userId?: LongId;
  userName?: string;
  avatarUrl?: string | null;
  displayName?: string | null;
}

interface ProfileTimeState {
  systemTimeZone: string;
  displayTimeZone: string;
  displayTimeFormat: string;
}

function buildProfileRequestKey(scope: string, ...parts: Array<string | number>): string {
  return [scope, ...parts].join('|');
}

function normalizePositiveLongId(value: unknown): LongId | undefined {
  if (typeof value === 'number') {
    return Number.isSafeInteger(value) && value > 0 ? String(value) : undefined;
  }

  if (typeof value !== 'string') {
    return undefined;
  }

  const trimmed = value.trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
}

function parseProfileWindowParams(input: Record<string, unknown> | undefined): ProfileWindowParams {
  if (!input) {
    return {};
  }

  return {
    userId: normalizePositiveLongId(input.userId),
    userName: typeof input.userName === 'string' ? input.userName : undefined,
    avatarUrl: typeof input.avatarUrl === 'string' ? input.avatarUrl : null,
    displayName: typeof input.displayName === 'string' ? input.displayName : null,
  };
}

function resolveAvatarUrl(apiBaseUrl: string, url: string | null | undefined): string | null {
  return resolveMediaUrl(url, apiBaseUrl);
}

function buildAvatarText(name: string): string {
  const source = name.trim();
  if (!source) return '?';
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

async function fetchProfileTimeState(): Promise<ProfileTimeState> {
  const preference = await getMyTimePreference();
  const systemTimeZone = resolveTimeZoneId(preference.voSystemDefaultTimeZoneId, DEFAULT_TIME_ZONE);
  const browserTimeZone = getBrowserTimeZoneId(systemTimeZone);

  return {
    systemTimeZone,
    displayTimeZone: preference.voIsCustomized
      ? resolveTimeZoneId(preference.voTimeZoneId, browserTimeZone)
      : browserTimeZone,
    displayTimeFormat: preference.voDisplayFormat?.trim() || DEFAULT_TIME_FORMAT,
  };
}

export const ProfileApp = () => {
  const { t } = useTranslation();
  const { userId, displayName: storeDisplayName, userName, displayHandle, isAuthenticated } = useUserStore();
  const { openApp } = useWindowStore();
  const currentWindow = useCurrentWindow();
  const params = useMemo(() => parseProfileWindowParams(currentWindow?.appParams), [currentWindow?.appParams]);
  const authenticatedUserId = userId ? userId : undefined;
  const viewingUserId = params.userId ?? authenticatedUserId;
  const viewingUserIdKey = viewingUserId ? String(viewingUserId) : '';
  const authenticatedUserIdKey = authenticatedUserId ? String(authenticatedUserId) : '';
  const isOwnProfile = viewingUserIdKey !== '' && viewingUserIdKey === authenticatedUserIdKey;
  const loggedIn = isAuthenticated();
  const [activeTab, setActiveTab] = useState<'posts' | 'comments' | 'quick-replies' | 'browse-history' | 'attachments' | 'social'>('posts');
  const statsRef = useRef<ProfileStats | null>(null);
  const statsSnapshotKeyRef = useRef('');
  const statsRequestIdRef = useRef(0);
  const timeSnapshotRef = useRef<ProfileTimeState | null>(null);
  const timeSnapshotKeyRef = useRef('');
  const timeRequestIdRef = useRef(0);
  const [stats, setStats] = useState<ProfileStats | null>(null);
  const [statsState, setStatsState] = useState<ProfileAuthorityState>('loading');
  const [statsError, setStatsError] = useState<string>();
  const [systemTimeZone, setSystemTimeZone] = useState(DEFAULT_TIME_ZONE);
  const [displayTimeZone, setDisplayTimeZone] = useState(DEFAULT_TIME_ZONE);
  const [displayTimeFormat, setDisplayTimeFormat] = useState(DEFAULT_TIME_FORMAT);
  const [timeState, setTimeState] = useState<ProfileAuthorityState>('loading');
  const [timeError, setTimeError] = useState<string>();
  const [savingTimeZone, setSavingTimeZone] = useState(false);
  const [profileDirty, setProfileDirty] = useState(false);
  const [profileBusy, setProfileBusy] = useState(false);
  const [publicProfile, setPublicProfile] = useState<PublicUserProfile | null>(null);
  const [loadingPublicProfile, setLoadingPublicProfile] = useState(false);
  const [followStatus, setFollowStatus] = useState<UserFollowStatus | null>(null);
  const [followLoading, setFollowLoading] = useState(false);

  const selfServiceLocked = profileDirty || profileBusy;
  useBrowserNavigationLock(selfServiceLocked);
  useWindowCloseGuard(selfServiceLocked
    ? t(profileBusy ? 'profile.navigation.busyConfirm' : 'profile.navigation.dirtyConfirm')
    : null);

  useEffect(() => {
    if (!selfServiceLocked) {
      return;
    }

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [selfServiceLocked]);

  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const viewingDisplayName = publicProfile
    ? resolveVisibleUserDisplayName(publicProfile, t('common.userFallback', { id: viewingUserIdKey || 0 }))
    : params.displayName?.trim()
      || storeDisplayName
      || userName
      || t('common.userFallback', { id: viewingUserIdKey || 0 });
  const viewingDisplayHandle = publicProfile
    ? resolveVisibleUserHandle(publicProfile, viewingDisplayName)
    : displayHandle?.trim()
      || params.userName?.trim()
      || null;
  const viewingUserName = viewingDisplayHandle
    || viewingDisplayName
    || t('common.userFallback', { id: viewingUserIdKey || 0 });
  const externalAvatarUrl = useMemo(
    () => resolveAvatarUrl(
      apiBaseUrl,
      publicProfile?.voAvatarThumbnailUrl || publicProfile?.voAvatarUrl || params.avatarUrl
    ),
    [apiBaseUrl, params.avatarUrl, publicProfile?.voAvatarThumbnailUrl, publicProfile?.voAvatarUrl]
  );

  useEffect(() => {
    if (!isOwnProfile && (activeTab === 'quick-replies' || activeTab === 'browse-history' || activeTab === 'attachments' || activeTab === 'social')) {
      setActiveTab('posts');
    }
  }, [activeTab, isOwnProfile]);

  useEffect(() => {
    if (isOwnProfile || !loggedIn || !viewingUserId) {
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
        reuseInFlightRequest(
          buildProfileRequestKey('public-profile', apiBaseUrl, viewingUserIdKey),
          () => getPublicProfile(viewingUserId)
        ),
        reuseInFlightRequest(
          buildProfileRequestKey('follow-status', apiBaseUrl, viewingUserIdKey),
          () => getFollowStatus(viewingUserId, t)
        ),
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
  }, [apiBaseUrl, isOwnProfile, loggedIn, t, viewingUserId, viewingUserIdKey]);

  const handlePostClick = (postId: LongId, postPublicId?: string | null) => {
    openApp('forum', buildForumAppParams({ postId, postPublicId: postPublicId ?? undefined }));
    log.debug('ProfileApp', `打开帖子: ${postPublicId || postId}`);
  };

  const handleCommentClick = (
    postId: LongId,
    commentId: LongId,
    postPublicId?: string | null,
  ) => {
    openApp('forum', buildForumAppParams({
      postId,
      postPublicId: postPublicId ?? undefined,
      commentId,
    }));
    log.debug('ProfileApp', `打开帖子 ${postPublicId || postId} 的评论 ${commentId}`);
  };

  const handleBrowseHistoryClick = (item: UserBrowseHistoryItem) => {
    openWorkspaceNavigationTarget(openApp, resolveBrowseHistoryWorkspaceTarget(item));
  };

  const handleUserClick = (targetUserId: LongId, targetUserName: string, avatarUrl?: string | null, displayName?: string | null) => {
    if (String(targetUserId) === String(userId)) {
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

  const loadStats = useCallback(async () => {
    if (!loggedIn || !viewingUserId) {
      statsRef.current = null;
      statsSnapshotKeyRef.current = '';
      setStats(null);
      setStatsState('unavailable');
      return;
    }

    const snapshotKey = viewingUserIdKey;
    const sameTarget = statsSnapshotKeyRef.current === snapshotKey;
    const hadSnapshot = sameTarget && statsRef.current !== null;
    const requestId = ++statsRequestIdRef.current;
    if (!hadSnapshot) {
      statsRef.current = null;
      setStats(null);
      setStatsState('loading');
    }
    setStatsError(undefined);

    try {
      const nextStats = await reuseInFlightRequest(
        buildProfileRequestKey('profile-stats', apiBaseUrl, snapshotKey),
        () => getUserStats(viewingUserId, t),
      );
      if (requestId !== statsRequestIdRef.current) {
        return;
      }

      statsSnapshotKeyRef.current = snapshotKey;
      statsRef.current = nextStats;
      setStats(nextStats);
      setStatsState('ready');
    } catch (error) {
      if (requestId !== statsRequestIdRef.current) {
        return;
      }

      const errorMessage = error instanceof Error ? error.message : t('profile.authority.statsLoadFailed');
      log.error('ProfileApp', '加载统计信息失败：', error);
      setStatsError(errorMessage);
      setStatsState(resolveFailedSnapshotState(hadSnapshot));
    }
  }, [apiBaseUrl, loggedIn, t, viewingUserId, viewingUserIdKey]);

  const loadTimeState = useCallback(async () => {
    if (!loggedIn || !authenticatedUserId) {
      timeSnapshotRef.current = null;
      timeSnapshotKeyRef.current = '';
      setTimeState('unavailable');
      return;
    }

    const snapshotKey = String(authenticatedUserId);
    const sameTarget = timeSnapshotKeyRef.current === snapshotKey;
    const hadSnapshot = sameTarget && timeSnapshotRef.current !== null;
    const requestId = ++timeRequestIdRef.current;
    if (!hadSnapshot) {
      timeSnapshotRef.current = null;
      setTimeState('loading');
    }
    setTimeError(undefined);

    try {
      const nextTimeState = await reuseInFlightRequest(
        buildProfileRequestKey('profile-time-state', apiBaseUrl, snapshotKey),
        fetchProfileTimeState,
      );
      if (requestId !== timeRequestIdRef.current) {
        return;
      }

      timeSnapshotKeyRef.current = snapshotKey;
      timeSnapshotRef.current = nextTimeState;
      setSystemTimeZone(nextTimeState.systemTimeZone);
      setDisplayTimeZone(nextTimeState.displayTimeZone);
      setDisplayTimeFormat(nextTimeState.displayTimeFormat);
      setTimeState('ready');
    } catch (error) {
      if (requestId !== timeRequestIdRef.current) {
        return;
      }

      const errorMessage = error instanceof Error ? error.message : t('profile.authority.timeLoadFailed');
      log.error('ProfileApp', '加载时间配置失败：', error);
      setTimeError(errorMessage);
      setTimeState(resolveFailedSnapshotState(hadSnapshot));
    }
  }, [apiBaseUrl, authenticatedUserId, loggedIn, t]);

  useEffect(() => {
    void loadStats();
    return () => {
      statsRequestIdRef.current += 1;
    };
  }, [loadStats]);

  useEffect(() => {
    void loadTimeState();
    return () => {
      timeRequestIdRef.current += 1;
    };
  }, [loadTimeState]);

  const handleTimeZoneChange = async (timeZoneId: string) => {
    const resolvedTimeZone = resolveTimeZoneId(timeZoneId, systemTimeZone);
    if (savingTimeZone) {
      return;
    }

    setSavingTimeZone(true);
    try {
      const updatedPreference = await updateMyTimePreference(resolvedTimeZone);
      const nextTimeState: ProfileTimeState = {
        systemTimeZone: resolveTimeZoneId(updatedPreference.voSystemDefaultTimeZoneId, systemTimeZone),
        displayTimeZone: resolveTimeZoneId(updatedPreference.voTimeZoneId, resolvedTimeZone),
        displayTimeFormat: updatedPreference.voDisplayFormat?.trim() || displayTimeFormat,
      };
      timeSnapshotRef.current = nextTimeState;
      setSystemTimeZone(nextTimeState.systemTimeZone);
      setDisplayTimeZone(nextTimeState.displayTimeZone);
      setDisplayTimeFormat(nextTimeState.displayTimeFormat);
      setTimeState('ready');
      setTimeError(undefined);
      log.info('ProfileApp', `用户时区已切换为 ${nextTimeState.displayTimeZone}`);
    } catch (error) {
      log.error('ProfileApp', '保存用户时区偏好失败：', error);
      throw error;
    } finally {
      setSavingTimeZone(false);
    }
  };

  const handleProfileDirtyChange = useCallback((dirty: boolean) => {
    setProfileDirty(dirty);
  }, []);

  const handleProfileBusyChange = useCallback((busy: boolean) => {
    setProfileBusy(busy);
  }, []);

  const handleToggleFollow = async () => {
    if (!loggedIn || isOwnProfile || !viewingUserId || followLoading) {
      return;
    }

    setFollowLoading(true);
    try {
      const nextStatus = followStatus?.voIsFollowing
        ? await unfollowUser(viewingUserId, t)
        : await followUser(viewingUserId, t);
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
            userId={authenticatedUserId!}
            userName={viewingDisplayName}
            stats={stats || undefined}
            statsState={statsState}
            statsError={statsError}
            onRetryStats={() => void loadStats()}
            timeState={timeState}
            timeError={timeError}
            onRetryTime={() => void loadTimeState()}
            apiBaseUrl={apiBaseUrl}
            displayTimeZone={displayTimeZone}
            systemTimeZone={systemTimeZone}
            displayTimeFormat={displayTimeFormat}
            savingTimeZone={savingTimeZone}
            onTimeZoneChange={handleTimeZoneChange}
            onDirtyChange={handleProfileDirtyChange}
            onBusyChange={handleProfileBusyChange}
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
                  <img src={externalAvatarUrl} alt={viewingDisplayName} className={styles.externalAvatarImage} loading="lazy" />
                ) : (
                  buildAvatarText(viewingDisplayName)
                )}
              </div>
              <div className={styles.externalInfo}>
                <div className={styles.externalNameRow}>
                  <h2 className={styles.externalName}>{viewingDisplayName}</h2>
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
                {viewingDisplayHandle ? (
                  <div className={styles.externalSubtle}>
                    {viewingDisplayHandle}
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
                <span>{t('profile.stats.postsLabel')} {statsState === 'ready' && stats ? stats.voPostCount : '--'}</span>
              </div>
              <div className={styles.externalStatItem}>
                <Icon icon="mdi:comment-text-outline" size={18} />
                <span>{t('profile.stats.commentsLabel')} {statsState === 'ready' && stats ? stats.voCommentCount : '--'}</span>
              </div>
              <div className={styles.externalStatItem}>
                <Icon icon="mdi:heart-outline" size={18} />
                <span>{t('profile.stats.likesLabel')} {statsState === 'ready' && stats ? stats.voTotalLikeCount : '--'}</span>
              </div>
              <div className={styles.externalStatItem}>
                <Icon icon="mdi:account-heart-outline" size={18} />
                <span>{t('profile.social.summary.followers')} {loadingPublicProfile || !followStatus ? '--' : followStatus.voFollowerCount}</span>
              </div>
              <div className={styles.externalStatItem}>
                <Icon icon="mdi:account-arrow-right-outline" size={18} />
                <span>{t('profile.social.summary.following')} {loadingPublicProfile || !followStatus ? '--' : followStatus.voFollowingCount}</span>
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
                className={`${styles.tab} ${activeTab === 'quick-replies' ? styles.active : ''}`}
                onClick={() => setActiveTab('quick-replies')}
              >
                {t('profile.tab.quickReplies')}
              </button>
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
            {activeTab === 'posts' && viewingUserId && (
              <UserPostList
                userId={viewingUserId}
                apiBaseUrl={apiBaseUrl}
                onPostClick={handlePostClick}
                displayTimeZone={displayTimeZone}
              />
            )}
            {activeTab === 'comments' && viewingUserId && (
              <UserCommentList
                userId={viewingUserId}
                apiBaseUrl={apiBaseUrl}
                onCommentClick={handleCommentClick}
                displayTimeZone={displayTimeZone}
              />
            )}
            {isOwnProfile && activeTab === 'quick-replies' && (
              <UserQuickReplyList
                displayTimeZone={displayTimeZone}
                onItemClick={handlePostClick}
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
