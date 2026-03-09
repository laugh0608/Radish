import { lazy, Suspense, useState, useEffect, useMemo } from 'react';
import { Icon } from '@radish/ui/icon';
import { log } from '@/utils/logger';
import { useUserStore } from '@/stores/userStore';
import { useWindowStore } from '@/stores/windowStore';
import { useCurrentWindow } from '@/desktop/CurrentWindowContext';
import { UserInfoCard } from './components/UserInfoCard';
import { getMyTimePreference, getTimeSettings, updateMyTimePreference } from '@/api/time';
import { getApiBaseUrl } from '@/config/env';
import {
  DEFAULT_TIME_FORMAT,
  DEFAULT_TIME_ZONE,
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
  const { userId, userName, isAuthenticated } = useUserStore();
  const { openApp } = useWindowStore();
  const currentWindow = useCurrentWindow();
  const params = useMemo(() => parseProfileWindowParams(currentWindow?.appParams), [currentWindow?.appParams]);
  const viewingUserId = params.userId && params.userId > 0 ? params.userId : userId;
  const viewingUserName = params.userName?.trim() || userName || `用户 ${viewingUserId}`;
  const isOwnProfile = viewingUserId === userId;
  const [activeTab, setActiveTab] = useState<'posts' | 'comments' | 'attachments' | 'social'>('posts');
  const [stats, setStats] = useState<UserStats | null>(null);
  const [loadingStats, setLoadingStats] = useState(true);
  const [systemTimeZone, setSystemTimeZone] = useState(DEFAULT_TIME_ZONE);
  const [displayTimeZone, setDisplayTimeZone] = useState(DEFAULT_TIME_ZONE);
  const [displayTimeFormat, setDisplayTimeFormat] = useState(DEFAULT_TIME_FORMAT);
  const [savingTimeZone, setSavingTimeZone] = useState(false);

  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const externalAvatarUrl = useMemo(() => resolveAvatarUrl(apiBaseUrl, params.avatarUrl), [apiBaseUrl, params.avatarUrl]);

  useEffect(() => {
    if (!isOwnProfile && (activeTab === 'attachments' || activeTab === 'social')) {
      setActiveTab('posts');
    }
  }, [activeTab, isOwnProfile]);

  const handlePostClick = (postId: number) => {
    openApp('forum');
    log.debug('ProfileApp', `打开帖子: ${postId}`);
  };

  const handleCommentClick = (postId: number, commentId: number) => {
    openApp('forum');
    log.debug('ProfileApp', `打开帖子 ${postId} 的评论 ${commentId}`);
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
    if (isAuthenticated() && viewingUserId > 0) {
      void loadStats();
      void loadTimeSettings();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [viewingUserId, userId]);

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

  if (!isAuthenticated()) {
    return (
      <div className={styles.container}>
        <div className={styles.notLoggedIn}>
          <p>请先登录查看个人主页</p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h1 className={styles.title}>{isOwnProfile ? '个人主页' : `${viewingUserName} 的主页`}</h1>
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
                title={params.displayName?.trim() || viewingUserName}
              >
                {externalAvatarUrl ? (
                  <img src={externalAvatarUrl} alt={viewingUserName} className={styles.externalAvatarImage} loading="lazy" />
                ) : (
                  buildAvatarText(viewingUserName)
                )}
              </div>
              <div className={styles.externalInfo}>
                <h2 className={styles.externalName}>{viewingUserName}</h2>
                {params.displayName?.trim() ? <div className={styles.externalSubtle}>显示名：{params.displayName.trim()}</div> : null}
                <div className={styles.externalSubtle}>当前查看的是用户主页，只提供公开内容浏览。</div>
              </div>
            </div>
            <div className={styles.externalStats}>
              <div className={styles.externalStatItem}>
                <Icon icon="mdi:file-document-outline" size={18} />
                <span>帖子 {loadingStats ? '--' : stats?.voPostCount ?? 0}</span>
              </div>
              <div className={styles.externalStatItem}>
                <Icon icon="mdi:comment-text-outline" size={18} />
                <span>评论 {loadingStats ? '--' : stats?.voCommentCount ?? 0}</span>
              </div>
              <div className={styles.externalStatItem}>
                <Icon icon="mdi:heart-outline" size={18} />
                <span>获赞 {loadingStats ? '--' : stats?.voTotalLikeCount ?? 0}</span>
              </div>
            </div>
          </section>
        )}

        <div className={styles.tabs}>
          <button
            className={`${styles.tab} ${activeTab === 'posts' ? styles.active : ''}`}
            onClick={() => setActiveTab('posts')}
          >
            {isOwnProfile ? '我的帖子' : 'TA 的帖子'}
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'comments' ? styles.active : ''}`}
            onClick={() => setActiveTab('comments')}
          >
            {isOwnProfile ? '我的评论' : 'TA 的评论'}
          </button>
          {isOwnProfile ? (
            <>
              <button
                className={`${styles.tab} ${activeTab === 'attachments' ? styles.active : ''}`}
                onClick={() => setActiveTab('attachments')}
              >
                我的附件
              </button>
              <button
                className={`${styles.tab} ${activeTab === 'social' ? styles.active : ''}`}
                onClick={() => setActiveTab('social')}
              >
                关系链
              </button>
            </>
          ) : null}
        </div>

        <div className={styles.tabContent}>
          <Suspense fallback={<div className={styles.notLoggedIn}>加载中...</div>}>
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
