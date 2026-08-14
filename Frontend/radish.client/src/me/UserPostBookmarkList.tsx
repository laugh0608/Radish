import { useCallback, useEffect, useRef, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { buildAttachmentAssetUrl } from '@radish/ui';
import { Icon } from '@radish/ui/icon';
import { toast } from '@radish/ui/toast';
import {
  getMyPostBookmarkPage,
  removeMyPostBookmark,
  type UserPostBookmarkVo,
} from '@/api/postBookmark';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { log } from '@/utils/logger';
import styles from './UserPostBookmarkList.module.css';

const PAGE_SIZE = 10;

interface UserPostBookmarkListProps {
  displayTimeZone: string;
  page: number;
  onPageChange: (page: number) => void;
  getPostHref: (postPublicId: string) => string;
  onPostLinkClick?: (event: MouseEvent<HTMLAnchorElement>, href: string) => void;
  onItemsLoaded?: (items: UserPostBookmarkVo[]) => void;
}

export const UserPostBookmarkList = ({
  displayTimeZone,
  page,
  onPageChange,
  getPostHref,
  onPostLinkClick,
  onItemsLoaded,
}: UserPostBookmarkListProps) => {
  const { t } = useTranslation();
  const [items, setItems] = useState<UserPostBookmarkVo[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [removingPublicId, setRemovingPublicId] = useState<string | null>(null);
  const requestIdRef = useRef(0);
  const onItemsLoadedRef = useRef(onItemsLoaded);
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  useEffect(() => {
    onItemsLoadedRef.current = onItemsLoaded;
  }, [onItemsLoaded]);

  const loadBookmarks = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);
    try {
      const result = await getMyPostBookmarkPage(page, PAGE_SIZE, t);
      if (requestId !== requestIdRef.current) {
        return;
      }

      const loadedItems = result.voItems ?? [];
      setItems(loadedItems);
      setTotal(result.voTotal ?? 0);
      onItemsLoadedRef.current?.(loadedItems);
    } catch (loadError) {
      if (requestId !== requestIdRef.current) {
        return;
      }

      log.warn('UserPostBookmarkList', '加载个人收藏列表失败', loadError);
      setItems([]);
      setTotal(0);
      setError(t('me.bookmarks.loadFailed'));
      onItemsLoadedRef.current?.([]);
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false);
      }
    }
  }, [page, t]);

  useEffect(() => {
    void loadBookmarks();
  }, [loadBookmarks]);

  const handleRemove = useCallback(async (bookmarkPublicId: string) => {
    if (removingPublicId) {
      return;
    }

    setRemovingPublicId(bookmarkPublicId);
    try {
      await removeMyPostBookmark(bookmarkPublicId, t);
      toast.success(t('me.bookmarks.removeSuccess'));
      if (items.length === 1 && page > 1) {
        onPageChange(page - 1);
        return;
      }

      await loadBookmarks();
    } catch (removeError) {
      log.warn('UserPostBookmarkList', '移除个人收藏失败', removeError);
      toast.error(t('me.bookmarks.removeFailed'));
    } finally {
      setRemovingPublicId(null);
    }
  }, [items.length, loadBookmarks, onPageChange, page, removingPublicId, t]);

  if (loading) {
    return (
      <div className={styles.state} aria-live="polite">
        <Icon icon="mdi:progress-clock" size={22} />
        <span>{t('me.bookmarks.loading')}</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.state} role="alert">
        <Icon icon="mdi:alert-circle-outline" size={22} />
        <span>{error}</span>
        <button type="button" className={styles.retryButton} onClick={() => void loadBookmarks()}>
          {t('common.retry')}
        </button>
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <div className={styles.state}>
        <Icon icon="mdi:bookmark-outline" size={26} />
        <span>{t('me.bookmarks.empty')}</span>
      </div>
    );
  }

  return (
    <div className={styles.container} aria-busy={removingPublicId != null}>
      <div className={styles.list}>
        {items.map((item) => {
          const isAvailable = item.voTargetStatus === 'Available' && Boolean(item.voPostPublicId);
          const href = isAvailable ? getPostHref(item.voPostPublicId!) : null;
          const removing = removingPublicId === item.voBookmarkPublicId;
          const coverUrl = isAvailable && item.voCoverAttachmentId
            ? buildAttachmentAssetUrl(item.voCoverAttachmentId, 'thumbnail')
            : null;
          const title = isAvailable
            ? item.voTitle?.trim() || t('me.bookmarks.untitled')
            : t('me.bookmarks.unavailableTitle');
          const summary = isAvailable
            ? item.voSummary?.trim() || t('me.preview.noSummary')
            : t('me.bookmarks.unavailableDescription');

          return (
            <article
              key={item.voBookmarkPublicId}
              className={`${styles.item} ${isAvailable ? '' : styles.unavailableItem}`}
            >
              {coverUrl ? (
                <img className={styles.cover} src={coverUrl} alt="" loading="lazy" />
              ) : (
                <div className={styles.coverPlaceholder} aria-hidden="true">
                  <Icon
                    icon={isAvailable ? 'mdi:file-document-outline' : 'mdi:file-hidden'}
                    size={28}
                  />
                </div>
              )}

              <div className={styles.body}>
                <div className={styles.heading}>
                  <div>
                    <span className={styles.status}>
                      {isAvailable
                        ? t('me.bookmarks.available')
                        : t('me.bookmarks.unavailable')}
                    </span>
                    {href ? (
                      <a
                        className={styles.titleLink}
                        href={href}
                        onClick={(event) => onPostLinkClick?.(event, href)}
                      >
                        {title}
                      </a>
                    ) : (
                      <h3>{title}</h3>
                    )}
                  </div>
                  <button
                    type="button"
                    className={styles.removeButton}
                    onClick={() => void handleRemove(item.voBookmarkPublicId)}
                    disabled={removing}
                    aria-label={t('me.bookmarks.removeLabel', { title })}
                  >
                    <Icon icon={removing ? 'mdi:progress-clock' : 'mdi:bookmark-remove-outline'} size={18} />
                    <span>
                      {removing ? t('me.bookmarks.removing') : t('me.bookmarks.remove')}
                    </span>
                  </button>
                </div>

                <p className={styles.summary}>{summary}</p>

                {isAvailable && item.voTags.length > 0 ? (
                  <div className={styles.tags} aria-label={t('me.bookmarks.tagsLabel')}>
                    {item.voTags.map((tag) => (
                      <span key={tag.voSlug || tag.voName}>{tag.voName}</span>
                    ))}
                  </div>
                ) : null}

                <div className={styles.meta}>
                  <span>
                    <Icon icon="mdi:bookmark-clock-outline" size={16} />
                    {t('me.bookmarks.savedAt', {
                      time: formatDateTimeByTimeZone(item.voBookmarkedAt, displayTimeZone),
                    })}
                  </span>
                  {isAvailable ? (
                    <>
                      <span>
                        <Icon icon="mdi:account-outline" size={16} />
                        {item.voAuthorName || t('common.unknownUser')}
                      </span>
                      <span>
                        <Icon icon="mdi:eye-outline" size={16} />
                        {item.voViewCount ?? 0}
                      </span>
                      <span>
                        <Icon icon="mdi:heart-outline" size={16} />
                        {item.voLikeCount ?? 0}
                      </span>
                      <span>
                        <Icon icon="mdi:comment-outline" size={16} />
                        {item.voCommentCount ?? 0}
                      </span>
                    </>
                  ) : null}
                </div>
              </div>
            </article>
          );
        })}
      </div>

      {totalPages > 1 ? (
        <nav className={styles.pagination} aria-label={t('me.bookmarks.paginationLabel')}>
          <button
            type="button"
            onClick={() => onPageChange(Math.max(1, page - 1))}
            disabled={page <= 1 || removingPublicId != null}
          >
            {t('common.previousPage')}
          </button>
          <span>{t('common.pageInfo', { current: page, total: totalPages })}</span>
          <button
            type="button"
            onClick={() => onPageChange(Math.min(totalPages, page + 1))}
            disabled={page >= totalPages || removingPublicId != null}
          >
            {t('common.nextPage')}
          </button>
        </nav>
      ) : null}
    </div>
  );
};
