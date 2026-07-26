import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { UserBlockListItemVo } from '@radish/http';
import { Icon } from '@radish/ui/icon';
import { getMyBlockedUsers, unblockUser } from '@/api/userBlock';
import { WebStateSlot } from '@/components/web-shell';
import {
  completeUserInteractionOperation,
  getStableUserInteractionOperationKey,
  publishUserInteractionChanged,
  subscribeUserInteractionChanged,
} from '@/services/userInteractionSync';
import { getIntlLocale } from '@/locales/language';
import { resolveMediaUrl } from '@/utils/media';
import { log } from '@/utils/logger';
import styles from './MeBlockedPage.module.css';

const PAGE_SIZE = 10;

interface MeBlockedPageProps {
  page: number;
  onNavigate: (page: number) => void;
  onBack: () => void;
}

export const MeBlockedPage = ({ page, onNavigate, onBack }: MeBlockedPageProps) => {
  const { t, i18n } = useTranslation();
  const [items, setItems] = useState<UserBlockListItemVo[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<UserBlockListItemVo | null>(null);
  const [unblocking, setUnblocking] = useState(false);
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const locale = getIntlLocale(i18n.resolvedLanguage ?? i18n.language);

  const loadPage = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await getMyBlockedUsers(page, PAGE_SIZE, t);
      setItems(result.voItems);
      setTotal(result.voTotal);
      const nextTotalPages = Math.max(1, Math.ceil(result.voTotal / PAGE_SIZE));
      if (page > nextTotalPages) {
        onNavigate(nextTotalPages);
      }
    } catch (loadError) {
      const message = loadError instanceof Error ? loadError.message : t('userBlock.error.listFailed');
      setItems([]);
      setTotal(0);
      setError(message);
      log.warn('MeBlockedPage', '加载已屏蔽用户失败', message);
    } finally {
      setLoading(false);
    }
  }, [onNavigate, page, t]);

  useEffect(() => {
    void loadPage();
  }, [loadPage]);

  useEffect(() => subscribeUserInteractionChanged(() => {
    void loadPage();
  }), [loadPage]);

  useEffect(() => {
    if (!selected) {
      return;
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !unblocking) {
        setSelected(null);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [selected, unblocking]);

  const pageLabel = useMemo(
    () => t('userBlock.list.pageInfo', { current: page, total: totalPages }),
    [page, t, totalPages],
  );

  const handleUnblock = async () => {
    if (!selected || unblocking) {
      return;
    }

    setUnblocking(true);
    setError(null);
    try {
      const operationKey = getStableUserInteractionOperationKey('unblock', selected.voTargetUserPublicId);
      const result = await unblockUser(selected.voTargetUserPublicId, operationKey, t);
      completeUserInteractionOperation('unblock', selected.voTargetUserPublicId);
      setSelected(null);
      publishUserInteractionChanged({
        voRelationshipVersion: result.voRelationshipVersion,
      });
      await loadPage();
    } catch (unblockError) {
      const message = unblockError instanceof Error
        ? unblockError.message
        : t('userBlock.error.unblockFailed');
      setError(message);
    } finally {
      setUnblocking(false);
    }
  };

  return (
    <section className={styles.page}>
      <header className={styles.header}>
        <button type="button" className={styles.backButton} onClick={onBack}>
          <Icon icon="mdi:arrow-left" size={18} />
          <span>{t('me.backToDashboard')}</span>
        </button>
        <div>
          <p className={styles.kicker}>{t('userBlock.list.kicker')}</p>
          <h1>{t('userBlock.list.title')}</h1>
          <p>{t('userBlock.list.description')}</p>
        </div>
        <div className={styles.summary} aria-label={t('userBlock.list.summaryLabel')}>
          <strong>{total}</strong>
          <span>{t('userBlock.list.total')}</span>
        </div>
      </header>

      {loading ? (
        <WebStateSlot
          tone="loading"
          icon="mdi:progress-clock"
          title={t('userBlock.list.loadingTitle')}
          description={t('userBlock.list.loadingDescription')}
        />
      ) : error ? (
        <WebStateSlot
          tone="error"
          icon="mdi:alert-circle-outline"
          title={t('userBlock.list.failedTitle')}
          description={error}
          actions={[{ label: t('common.retry'), onClick: () => void loadPage() }]}
        />
      ) : items.length === 0 ? (
        <WebStateSlot
          tone="empty"
          icon="mdi:account-check-outline"
          title={t('userBlock.list.emptyTitle')}
          description={t('userBlock.list.emptyDescription')}
        />
      ) : (
        <>
          <div className={styles.list}>
            {items.map((item) => {
              const avatarUrl = resolveMediaUrl(item.voTargetAvatarUrl);
              return (
                <article key={item.voTargetUserPublicId} className={styles.item}>
                  <a className={styles.identity} href={`/u/${encodeURIComponent(item.voTargetUserPublicId)}`}>
                    <span className={styles.avatar} aria-hidden="true">
                      {avatarUrl
                        ? <img src={avatarUrl} alt="" />
                        : item.voTargetDisplayName.trim().charAt(0).toUpperCase()}
                    </span>
                    <span>
                      <strong>{item.voTargetDisplayName}</strong>
                      <small>@{item.voTargetUserPublicId}</small>
                    </span>
                  </a>
                  <div className={styles.meta}>
                    <span>{t('userBlock.list.blockedAt', {
                      time: new Intl.DateTimeFormat(locale, {
                        dateStyle: 'medium',
                        timeStyle: 'short',
                      }).format(new Date(item.voBlockedAtUtc)),
                    })}</span>
                    <span>{t('userBlock.list.publicVisible')}</span>
                  </div>
                  <button
                    type="button"
                    disabled={!item.voCanUnblock}
                    onClick={() => setSelected(item)}
                  >
                    <Icon icon="mdi:shield-check-outline" size={17} />
                    <span>{t('userBlock.action.unblock')}</span>
                  </button>
                </article>
              );
            })}
          </div>

          <nav className={styles.pagination} aria-label={t('userBlock.list.paginationLabel')}>
            <button type="button" disabled={page <= 1} onClick={() => onNavigate(page - 1)}>
              {t('common.previousPage')}
            </button>
            <span>{pageLabel}</span>
            <button type="button" disabled={page >= totalPages} onClick={() => onNavigate(page + 1)}>
              {t('common.nextPage')}
            </button>
          </nav>
        </>
      )}

      {selected && (
        <div className={styles.dialogBackdrop} role="presentation">
          <section
            className={styles.dialog}
            role="dialog"
            aria-modal="true"
            aria-labelledby="blocked-user-unblock-title"
          >
            <p className={styles.kicker}>{t('userBlock.confirm.kicker')}</p>
            <h2 id="blocked-user-unblock-title">
              {t('userBlock.confirm.unblockTitle', { name: selected.voTargetDisplayName })}
            </h2>
            <p>{t('userBlock.confirm.unblockDescription')}</p>
            <ul>
              <li>{t('userBlock.confirm.followImpact')}</li>
              <li>{t('userBlock.confirm.authorityImpact')}</li>
            </ul>
            <div className={styles.dialogActions}>
              <button type="button" autoFocus disabled={unblocking} onClick={() => setSelected(null)}>
                {t('common.cancel')}
              </button>
              <button type="button" data-primary="true" disabled={unblocking} onClick={() => void handleUnblock()}>
                {unblocking ? t('common.loading') : t('userBlock.action.unblock')}
              </button>
            </div>
          </section>
        </div>
      )}
    </section>
  );
};
