import { useState, useEffect, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { log } from '@/utils/logger';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { Icon } from '@radish/ui/icon';
import { getUserPosts, type LongId, type UserPost } from '@/api/user';
import styles from './UserPostList.module.css';

interface UserPostListProps {
  userId: LongId;
  apiBaseUrl?: string;
  displayTimeZone: string;
  onPostClick?: (postId: LongId, postPublicId?: string | null) => void;
  getPostHref?: (postId: LongId, postPublicId?: string | null) => string | null;
  onPostLinkClick?: (
    event: MouseEvent<HTMLAnchorElement>,
    href: string,
    postId: LongId,
    postPublicId?: string | null
  ) => void;
  onItemsLoaded?: (items: UserPost[]) => void;
  page?: number;
  onPageChange?: (page: number) => void;
}

export const UserPostList = ({
  userId,
  displayTimeZone,
  onPostClick,
  getPostHref,
  onPostLinkClick,
  onItemsLoaded,
  page: controlledPage,
  onPageChange
}: UserPostListProps) => {
  const { t } = useTranslation();
  const [posts, setPosts] = useState<UserPost[]>([]);
  const [loading, setLoading] = useState(true);
  const [internalPage, setInternalPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const page = controlledPage ?? internalPage;

  useEffect(() => {
    loadPosts();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId, page]);

  const loadPosts = async () => {
    setLoading(true);
    try {
      const result = await getUserPosts(userId, page, 10);
      const loadedPosts = result.data || [];
      setPosts(loadedPosts);
      onItemsLoaded?.(loadedPosts);
      setTotalPages(result.pageCount || 1);
    } catch (error) {
      log.error('加载帖子失败:', error);
      onItemsLoaded?.([]);
    } finally {
      setLoading(false);
    }
  };

  const updatePage = (nextPage: number) => {
    if (onPageChange) {
      onPageChange(nextPage);
      return;
    }

    setInternalPage(nextPage);
  };

  if (loading) {
    return <div className={styles.loading}>{t('common.loading')}</div>;
  }

  if (posts.length === 0) {
    return <div className={styles.empty}>{t('profile.posts.empty')}</div>;
  }

  return (
    <div className={styles.container}>
      <div className={styles.list}>
        {posts.map(post => {
          const href = getPostHref?.(post.voId, post.voPublicId) ?? null;
          const body = (
            <>
              <h3 className={styles.title}>{post.voTitle}</h3>
              <p className={styles.content}>
                {post.voContent?.substring(0, 100) ?? ''}
                {(post.voContent?.length ?? 0) > 100 && '...'}
              </p>
              <div className={styles.meta}>
                <span className={styles.metaItem}>
                  <Icon icon="mdi:eye" size={16} />
                  {post.voViewCount}
                </span>
                <span className={styles.metaItem}>
                  <Icon icon="mdi:heart" size={16} />
                  {post.voLikeCount}
                </span>
                <span className={styles.metaItem}>
                  <Icon icon="mdi:comment" size={16} />
                  {post.voCommentCount}
                </span>
                <span className={styles.time}>
                  {formatDateTimeByTimeZone(post.voCreateTime, displayTimeZone)}
                </span>
              </div>
            </>
          );

          return href ? (
            <a
              key={post.voId}
              className={styles.postItem}
              href={href}
              onClick={(event) => onPostLinkClick?.(event, href, post.voId, post.voPublicId)}
            >
              {body}
            </a>
          ) : (
            <div
              key={post.voId}
              className={styles.postItem}
              onClick={() => onPostClick?.(post.voId, post.voPublicId)}
              style={{ cursor: onPostClick ? 'pointer' : 'default' }}
            >
              {body}
            </div>
          );
        })}
      </div>

      {totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            onClick={() => updatePage(Math.max(1, page - 1))}
            disabled={page === 1}
            className={styles.pageButton}
          >
            {t('common.previousPage')}
          </button>
          <span className={styles.pageInfo}>
            {t('common.pageInfo', { current: page, total: totalPages })}
          </span>
          <button
            onClick={() => updatePage(Math.min(totalPages, page + 1))}
            disabled={page === totalPages}
            className={styles.pageButton}
          >
            {t('common.nextPage')}
          </button>
        </div>
      )}
    </div>
  );
};
