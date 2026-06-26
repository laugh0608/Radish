import { useEffect, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { getMyQuickReplies, type UserPostQuickReply } from '@/api/forum';
import type { LongId } from '@/api/user';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { log } from '@/utils/logger';
import styles from './UserQuickReplyList.module.css';

interface UserQuickReplyListProps {
  displayTimeZone: string;
  onItemClick?: (postId: LongId, postPublicId?: string | null) => void;
  getItemHref?: (postId: LongId, postPublicId?: string | null) => string | null;
  onItemLinkClick?: (
    event: MouseEvent<HTMLAnchorElement>,
    href: string,
    postId: LongId,
    postPublicId?: string | null
  ) => void;
  page?: number;
  onPageChange?: (page: number) => void;
}

export const UserQuickReplyList = ({
  displayTimeZone,
  onItemClick,
  getItemHref,
  onItemLinkClick,
  page: controlledPage,
  onPageChange
}: UserQuickReplyListProps) => {
  const { t } = useTranslation();
  const [items, setItems] = useState<UserPostQuickReply[]>([]);
  const [loading, setLoading] = useState(true);
  const [internalPage, setInternalPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const page = controlledPage ?? internalPage;

  useEffect(() => {
    const loadQuickReplies = async () => {
      setLoading(true);
      try {
        const result = await getMyQuickReplies(page, 10);
        setItems(result.voItems || []);
        setTotalPages(Math.max(1, Math.ceil((result.voTotal || 0) / Math.max(result.voPageSize || 10, 1))));
      } catch (error) {
        log.error('UserQuickReplyList', '加载轻回应失败：', error);
        setItems([]);
        setTotalPages(1);
      } finally {
        setLoading(false);
      }
    };

    void loadQuickReplies();
  }, [page]);

  const updatePage = (nextPage: number) => {
    if (onPageChange) {
      onPageChange(nextPage);
      return;
    }

    setInternalPage(nextPage);
  };

  if (loading) {
    return <div className={styles.loading}>{t('profile.quickReplies.loading')}</div>;
  }

  if (items.length === 0) {
    return <div className={styles.empty}>{t('profile.quickReplies.empty')}</div>;
  }

  return (
    <div className={styles.container}>
      <div className={styles.list}>
        {items.map((item) => {
          const href = getItemHref?.(item.voPostId, item.voPostPublicId) ?? null;
          const body = (
            <>
              <h3 className={styles.postTitle}>{item.voPostTitle}</h3>
              <p className={styles.content}>{item.voContent}</p>
              <div className={styles.meta}>
                <span className={styles.metaItem}>
                  <Icon icon="mdi:comment-quote-outline" size={16} />
                  {t('profile.quickReplies.fromPost')}
                </span>
                <span className={styles.time}>
                  {formatDateTimeByTimeZone(item.voCreateTime, displayTimeZone)}
                </span>
              </div>
            </>
          );

          return href ? (
            <a
              key={item.voId}
              className={styles.replyItem}
              href={href}
              onClick={(event) => onItemLinkClick?.(event, href, item.voPostId, item.voPostPublicId)}
            >
              {body}
            </a>
          ) : (
            <article
              key={item.voId}
              className={styles.replyItem}
              onClick={() => onItemClick?.(item.voPostId, item.voPostPublicId)}
              style={{ cursor: onItemClick ? 'pointer' : 'default' }}
            >
              {body}
            </article>
          );
        })}
      </div>

      {totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            type="button"
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
            type="button"
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
