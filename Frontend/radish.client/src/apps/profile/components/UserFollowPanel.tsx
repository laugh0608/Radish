import { useEffect, useMemo, useState } from 'react';
import { Icon } from '@radish/ui/icon';
import { getApiBaseUrl } from '@/config/env';
import { log } from '@/utils/logger';
import type { PostItem } from '@/types/forum';
import {
  getMyFollowers,
  getMyFollowing,
  getMyFollowingFeed,
  getMyDistributionFeed,
  getMyFollowSummary,
  type DistributionStreamType,
  type UserFollowSummary,
  type UserFollowUser
} from '@/api/userFollow';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import styles from './UserFollowPanel.module.css';

type SocialTab = 'feed' | 'followers' | 'following';
type FeedViewType = 'following' | DistributionStreamType;

interface UserFollowPanelProps {
  displayTimeZone: string;
  onPostClick?: (postId: number) => void;
  onUserClick?: (userId: number, userName: string, avatarUrl?: string | null, displayName?: string | null) => void;
}

const PAGE_SIZE = 10;

export const UserFollowPanel = ({ displayTimeZone, onPostClick, onUserClick }: UserFollowPanelProps) => {
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const [activeTab, setActiveTab] = useState<SocialTab>('feed');
  const [feedViewType, setFeedViewType] = useState<FeedViewType>('following');
  const [summary, setSummary] = useState<UserFollowSummary>({ voFollowerCount: 0, voFollowingCount: 0 });
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [avatarErrorUserIds, setAvatarErrorUserIds] = useState<Set<number>>(new Set());
  const [feedItems, setFeedItems] = useState<PostItem[]>([]);
  const [followerItems, setFollowerItems] = useState<UserFollowUser[]>([]);
  const [followingItems, setFollowingItems] = useState<UserFollowUser[]>([]);
  const [feedPage, setFeedPage] = useState(1);
  const [followerPage, setFollowerPage] = useState(1);
  const [followingPage, setFollowingPage] = useState(1);
  const [feedTotal, setFeedTotal] = useState(0);
  const [followerTotal, setFollowerTotal] = useState(0);
  const [followingTotal, setFollowingTotal] = useState(0);

  useEffect(() => {
    void loadSummary();
  }, []);

  useEffect(() => {
    if (activeTab === 'feed') {
      void loadFeed(feedPage, feedViewType);
      return;
    }

    if (activeTab === 'followers') {
      void loadFollowers(followerPage);
      return;
    }

    void loadFollowing(followingPage);
  }, [activeTab, feedPage, feedViewType, followerPage, followingPage]);

  const totalPages = useMemo(() => {
    const total = activeTab === 'feed'
      ? feedTotal
      : activeTab === 'followers'
        ? followerTotal
        : followingTotal;
    return Math.max(1, Math.ceil(total / PAGE_SIZE));
  }, [activeTab, feedTotal, followerTotal, followingTotal]);

  const currentPage = activeTab === 'feed'
    ? feedPage
    : activeTab === 'followers'
      ? followerPage
      : followingPage;

  const setCurrentPage = (page: number) => {
    if (activeTab === 'feed') {
      setFeedPage(page);
      return;
    }

    if (activeTab === 'followers') {
      setFollowerPage(page);
      return;
    }

    setFollowingPage(page);
  };

  const loadSummary = async () => {
    try {
      const data = await getMyFollowSummary();
      setSummary(data);
    } catch (error) {
      log.error('UserFollowPanel', '加载关系链汇总失败:', error);
    }
  };

  const loadFeed = async (pageIndex: number, viewType: FeedViewType) => {
    setLoading(true);
    setErrorMessage(null);
    try {
      const data = viewType === 'following'
        ? await getMyFollowingFeed(pageIndex, PAGE_SIZE)
        : await getMyDistributionFeed(viewType, pageIndex, PAGE_SIZE);
      setFeedItems(data.voItems || []);
      setFeedTotal(data.voTotal || 0);
    } catch (error) {
      log.error('UserFollowPanel', `加载关系链内容失败(${viewType}):`, error);
      setErrorMessage(error instanceof Error ? error.message : '加载动态失败，请稍后重试');
      setFeedItems([]);
      setFeedTotal(0);
    } finally {
      setLoading(false);
    }
  };

  const loadFollowers = async (pageIndex: number) => {
    setLoading(true);
    setErrorMessage(null);
    try {
      const data = await getMyFollowers(pageIndex, PAGE_SIZE);
      setFollowerItems(data.voItems || []);
      setFollowerTotal(data.voTotal || 0);
    } catch (error) {
      log.error('UserFollowPanel', '加载粉丝列表失败:', error);
      setErrorMessage(error instanceof Error ? error.message : '加载粉丝列表失败，请稍后重试');
      setFollowerItems([]);
      setFollowerTotal(0);
    } finally {
      setLoading(false);
    }
  };

  const loadFollowing = async (pageIndex: number) => {
    setLoading(true);
    setErrorMessage(null);
    try {
      const data = await getMyFollowing(pageIndex, PAGE_SIZE);
      setFollowingItems(data.voItems || []);
      setFollowingTotal(data.voTotal || 0);
    } catch (error) {
      log.error('UserFollowPanel', '加载关注列表失败:', error);
      setErrorMessage(error instanceof Error ? error.message : '加载关注列表失败，请稍后重试');
      setFollowingItems([]);
      setFollowingTotal(0);
    } finally {
      setLoading(false);
    }
  };

  const buildAvatarText = (name: string) => {
    const source = name.trim();
    if (!source) return '?';
    return source.charAt(0).toUpperCase();
  };

  const buildAvatarStyle = (seed: string) => {
    let hash = 0;
    for (let i = 0; i < seed.length; i += 1) {
      hash = seed.charCodeAt(i) + ((hash << 5) - hash);
    }

    const hue = Math.abs(hash) % 360;
    return {
      backgroundColor: `hsl(${hue} 80% 92%)`,
      color: `hsl(${hue} 45% 30%)`
    };
  };

  const resolveAvatarUrl = (url: string | null | undefined) => {
    if (!url) return null;
    if (/^https?:\/\//i.test(url)) return url;
    if (url.startsWith('/')) return `${apiBaseUrl}${url}`;
    return `${apiBaseUrl}/${url}`;
  };

  const handleRetry = () => {
    if (activeTab === 'feed') {
      void loadFeed(feedPage, feedViewType);
      return;
    }

    if (activeTab === 'followers') {
      void loadFollowers(followerPage);
      return;
    }

    void loadFollowing(followingPage);
  };

  const renderUserAvatar = (user: UserFollowUser) => {
    const name = user.voDisplayName?.trim() || user.voUserName.trim() || '用户';
    const avatarUrl = avatarErrorUserIds.has(user.voUserId) ? null : resolveAvatarUrl(user.voAvatarUrl);

    return (
      <span
        className={styles.userAvatar}
        style={avatarUrl ? undefined : buildAvatarStyle(name)}
        title={name}
      >
        {avatarUrl ? (
          <img
            src={avatarUrl}
            alt={name}
            className={styles.userAvatarImage}
            loading="lazy"
            onError={() => {
              setAvatarErrorUserIds((current) => {
                const next = new Set(current);
                next.add(user.voUserId);
                return next;
              });
            }}
          />
        ) : (
          buildAvatarText(name)
        )}
      </span>
    );
  };

  const feedViewLabelMap: Record<FeedViewType, string> = {
    following: '关注中',
    recommend: '推荐内容',
    hot: '热门内容',
    newest: '最新发布'
  };

  const feedViewDescriptionMap: Record<FeedViewType, string> = {
    following: '只看你已关注用户的最近动态',
    recommend: '根据互动热度推荐的内容',
    hot: '当前讨论度较高的内容',
    newest: '社区最新发布的内容'
  };

  const renderFeed = () => {
    if (feedItems.length === 0) {
      const emptyText = feedViewType === 'following'
        ? '你关注的人还没有新动态'
        : `${feedViewLabelMap[feedViewType]}暂时没有内容`;
      return <div className={styles.empty}>{emptyText}</div>;
    }

    return (
      <div className={styles.list}>
        {feedItems.map(item => (
          <article
            key={item.voId}
            className={styles.feedItem}
            onClick={() => onPostClick?.(item.voId)}
          >
            <h3 className={styles.feedTitle}>{item.voTitle}</h3>
            <div className={styles.feedMeta}>
              <span>作者：{item.voAuthorName || '未知用户'}</span>
              <span>{formatDateTimeByTimeZone(item.voCreateTime, displayTimeZone)}</span>
              <span>💬 {item.voCommentCount || 0}</span>
              <span>❤️ {item.voLikeCount || 0}</span>
            </div>
          </article>
        ))}
      </div>
    );
  };

  const renderUserList = (users: UserFollowUser[], emptyText: string) => {
    if (users.length === 0) {
      return <div className={styles.empty}>{emptyText}</div>;
    }

    return (
      <div className={styles.list}>
        {users.map(user => (
          <article
            key={user.voUserId}
            className={styles.userItem}
            onClick={() => onUserClick?.(user.voUserId, user.voUserName, user.voAvatarUrl, user.voDisplayName)}
            style={{ cursor: onUserClick ? 'pointer' : 'default' }}
          >
            <div className={styles.userMain}>
              <div className={styles.userNameRow}>
                {renderUserAvatar(user)}
                <span className={styles.userName}>{user.voUserName}</span>
                {user.voDisplayName ? <span className={styles.userDisplay}>({user.voDisplayName})</span> : null}
                {user.voIsMutualFollow ? <span className={styles.mutualBadge}>互关</span> : null}
              </div>
              <div className={styles.userMeta}>
                关注时间：{formatDateTimeByTimeZone(user.voFollowTime, displayTimeZone)}
              </div>
            </div>
            <Icon icon="mdi:chevron-right" size={20} />
          </article>
        ))}
      </div>
    );
  };

  const renderContent = () => {
    if (loading) {
      return <div className={styles.loading}>加载中...</div>;
    }

    if (errorMessage) {
      return (
        <div className={styles.errorState}>
          <div className={styles.errorTitle}>加载失败</div>
          <div className={styles.errorMessage}>{errorMessage}</div>
          <button type="button" className={styles.retryButton} onClick={handleRetry}>
            重试
          </button>
        </div>
      );
    }

    if (activeTab === 'feed') {
      return renderFeed();
    }

    if (activeTab === 'followers') {
      return renderUserList(followerItems, '暂时还没有粉丝');
    }

    return renderUserList(followingItems, '你还没有关注任何人');
  };

  return (
    <div className={styles.container}>
      <div className={styles.summary}>
        <div className={styles.summaryItem}>
          <span className={styles.summaryLabel}>粉丝</span>
          <span className={styles.summaryValue}>{summary.voFollowerCount}</span>
        </div>
        <div className={styles.summaryItem}>
          <span className={styles.summaryLabel}>关注</span>
          <span className={styles.summaryValue}>{summary.voFollowingCount}</span>
        </div>
      </div>

      <div className={styles.tabs}>
        <button
          className={`${styles.tab} ${activeTab === 'feed' ? styles.active : ''}`}
          onClick={() => setActiveTab('feed')}
        >
          动态
        </button>
        <button
          className={`${styles.tab} ${activeTab === 'followers' ? styles.active : ''}`}
          onClick={() => setActiveTab('followers')}
        >
          我的粉丝
        </button>
        <button
          className={`${styles.tab} ${activeTab === 'following' ? styles.active : ''}`}
          onClick={() => setActiveTab('following')}
        >
          我的关注
        </button>
      </div>

      {activeTab === 'feed' && (
        <>
          <div className={styles.feedStreamTabs}>
            {(['following', 'recommend', 'hot', 'newest'] as FeedViewType[]).map(viewType => (
              <button
                key={viewType}
                className={`${styles.feedStreamTab} ${feedViewType === viewType ? styles.feedStreamTabActive : ''}`}
                onClick={() => {
                  setFeedViewType(viewType);
                  setFeedPage(1);
                }}
              >
                {feedViewLabelMap[viewType]}
              </button>
            ))}
          </div>
          <div className={styles.feedStreamHint}>{feedViewDescriptionMap[feedViewType]}</div>
        </>
      )}

      {renderContent()}

      {totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            className={styles.pageButton}
            disabled={currentPage <= 1}
            onClick={() => setCurrentPage(Math.max(1, currentPage - 1))}
          >
            上一页
          </button>
          <span className={styles.pageInfo}>{currentPage} / {totalPages}</span>
          <button
            className={styles.pageButton}
            disabled={currentPage >= totalPages}
            onClick={() => setCurrentPage(Math.min(totalPages, currentPage + 1))}
          >
            下一页
          </button>
        </div>
      )}
    </div>
  );
};
