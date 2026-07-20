import { useCallback, useEffect, useRef, useState } from 'react';
import type {
  ChatReadReceiptMode,
  ChatReadReceiptReaderVo,
  ChatReadReceiptSummaryItemVo,
} from '@radish/http';
import { BottomSheet } from '@radish/ui/bottom-sheet';
import { Icon } from '@radish/ui/icon';
import { useTranslation } from 'react-i18next';
import { getChatReadReceiptReaders } from '@/api/chat';
import type { EntityIdValue } from '@/types/chat';
import { normalizeEntityId } from '@/types/chat';
import { log } from '@/utils/logger';
import { buildAvatarStyle, buildAvatarText, resolveMediaUrl } from './chatApp.helpers';
import styles from './ChatReadReceiptIndicator.module.css';

interface ChatReadReceiptIndicatorProps {
  apiBaseUrl: string;
  channelId: EntityIdValue;
  messageId: EntityIdValue;
  mode: ChatReadReceiptMode;
  summary?: ChatReadReceiptSummaryItemVo;
  isDirectBoundary: boolean;
  compact: boolean;
  onOpenUserProfile: (
    userId: EntityIdValue,
    userName?: string | null,
    avatarUrl?: string | null,
    publicId?: string | null
  ) => void;
}

export const ChatReadReceiptIndicator = ({
  apiBaseUrl,
  channelId,
  messageId,
  mode,
  summary,
  isDirectBoundary,
  compact,
  onOpenUserProfile,
}: ChatReadReceiptIndicatorProps) => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [readers, setReaders] = useState<ChatReadReceiptReaderVo[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const closeButtonRef = useRef<HTMLButtonElement | null>(null);
  const desktopDialogRef = useRef<HTMLElement | null>(null);
  const requestRevisionRef = useRef(0);
  const normalizedChannelId = normalizeEntityId(channelId);
  const normalizedMessageId = normalizeEntityId(messageId);
  const readCount = summary?.voReadCount ?? 0;

  const close = useCallback(() => {
    requestRevisionRef.current += 1;
    setOpen(false);
    requestAnimationFrame(() => triggerRef.current?.focus());
  }, []);

  const loadReaders = useCallback(async (cursor: string | null, append: boolean) => {
    if (!normalizedChannelId || !normalizedMessageId || loading) {
      return;
    }

    const requestRevision = ++requestRevisionRef.current;
    setLoading(true);
    setLoadError(null);
    try {
      const page = await getChatReadReceiptReaders(
        normalizedChannelId,
        normalizedMessageId,
        cursor,
        50
      );
      if (requestRevision !== requestRevisionRef.current) {
        return;
      }

      setReaders((current) => {
        const next = append ? [...current, ...page.voItems] : page.voItems;
        return Array.from(new Map(next.map((reader) => [reader.voUserId, reader])).values());
      });
      setNextCursor(page.voNextCursor ?? null);
      setHasMore(page.voHasMore);
    } catch (error) {
      if (requestRevision !== requestRevisionRef.current) {
        return;
      }
      setLoadError(t('chat.receipt.readerLoadFailed'));
      log.warn('ChatReadReceipt', '读取普通 Private 消息读者失败:', error);
    } finally {
      if (requestRevision === requestRevisionRef.current) {
        setLoading(false);
      }
    }
  }, [loading, normalizedChannelId, normalizedMessageId, t]);

  const openReaders = () => {
    setOpen(true);
    setReaders([]);
    setNextCursor(null);
    setHasMore(false);
    void loadReaders(null, false);
  };

  useEffect(() => {
    if (!open) {
      return;
    }

    requestAnimationFrame(() => {
      if (!compact) {
        closeButtonRef.current?.focus();
      }
    });
  }, [compact, open]);

  useEffect(() => {
    if (!open || compact) {
      return;
    }

    const handleDialogKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        close();
        return;
      }

      if (event.key !== 'Tab' || !desktopDialogRef.current) {
        return;
      }

      const focusable = Array.from(
        desktopDialogRef.current.querySelectorAll<HTMLElement>(
          'button:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])'
        )
      ).filter((element) => element.getClientRects().length > 0);
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (!first || !last) {
        return;
      }

      if (!desktopDialogRef.current.contains(document.activeElement)) {
        event.preventDefault();
        first.focus();
      } else if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener('keydown', handleDialogKeyDown);
    return () => document.removeEventListener('keydown', handleDialogKeyDown);
  }, [close, compact, open]);

  useEffect(() => {
    if (readCount <= 0 || mode !== 'private_group') {
      setOpen(false);
    }
  }, [mode, readCount]);

  useEffect(() => {
    requestRevisionRef.current += 1;
    setOpen(false);
    setReaders([]);
    setLoadError(null);
  }, [normalizedChannelId, normalizedMessageId]);

  useEffect(() => {
    return () => {
      requestRevisionRef.current += 1;
    };
  }, []);

  if (mode === 'direct') {
    return isDirectBoundary ? (
      <span className={styles.directBoundary} title={t('chat.receipt.directHint')}>
        <Icon icon="mdi:check-all" size={14} />
        {t('chat.receipt.directRead')}
      </span>
    ) : null;
  }

  if (mode !== 'private_group' || readCount <= 0) {
    return null;
  }

  const readerList = (
    <div className={styles.readerContent}>
      {loading && readers.length === 0 && (
        <div className={styles.readerState} role="status">{t('chat.receipt.readerLoading')}</div>
      )}
      {loadError && readers.length === 0 && (
        <div className={styles.readerState} role="alert">
          <span>{loadError}</span>
          <button type="button" onClick={() => void loadReaders(null, false)}>
            {t('chat.receipt.retry')}
          </button>
        </div>
      )}
      {!loading && !loadError && readers.length === 0 && (
        <div className={styles.readerState}>{t('chat.receipt.readerEmpty')}</div>
      )}
      {readers.length > 0 && (
        <ul className={styles.readerList}>
          {readers.map((reader) => {
            const avatarUrl = resolveMediaUrl(apiBaseUrl, reader.voAvatarUrl);
            return (
              <li key={reader.voUserId}>
                <button
                  type="button"
                  className={styles.readerButton}
                  onClick={() => {
                    close();
                    onOpenUserProfile(
                      reader.voUserId,
                      reader.voDisplayName,
                      reader.voAvatarUrl,
                      reader.voPublicId
                    );
                  }}
                >
                  {avatarUrl ? (
                    <img src={avatarUrl} alt="" className={styles.readerAvatar} loading="lazy" />
                  ) : (
                    <span className={styles.readerAvatarFallback} style={buildAvatarStyle(reader.voDisplayName)}>
                      {buildAvatarText(reader.voDisplayName)}
                    </span>
                  )}
                  <span>{reader.voDisplayName}</span>
                  <Icon icon="mdi:chevron-right" size={16} />
                </button>
              </li>
            );
          })}
        </ul>
      )}
      {readers.length > 0 && (hasMore || loading || loadError) && (
        <div className={styles.pageFooter}>
          {loadError && <span role="alert">{loadError}</span>}
          <button
            type="button"
            disabled={loading || !hasMore}
            onClick={() => void loadReaders(nextCursor, true)}
          >
            {loading ? t('chat.receipt.readerLoadingMore') : t('chat.receipt.readerLoadMore')}
          </button>
        </div>
      )}
    </div>
  );

  const title = t('chat.receipt.readerTitle', { count: readCount });
  return (
    <span className={styles.root}>
      <button
        ref={triggerRef}
        type="button"
        className={styles.summaryButton}
        onClick={openReaders}
        aria-expanded={open}
      >
        <Icon icon="mdi:account-check-outline" size={14} />
        {t('chat.receipt.privateCount', { count: readCount })}
      </button>

      {open && compact && (
        <BottomSheet
          isOpen
          onClose={close}
          closeLabel={t('chat.receipt.closeReaders')}
          title={title}
          height="min(72%, 620px)"
          className={styles.mobileSheet}
          bodyClassName={styles.mobileSheetBody}
        >
          <div className={styles.mobileDialog}>
            {readerList}
          </div>
        </BottomSheet>
      )}

      {open && !compact && (
        <>
          <button
            type="button"
            className={styles.desktopBackdrop}
            onClick={close}
            aria-label={t('chat.receipt.closeReaders')}
          />
          <aside
            ref={desktopDialogRef}
            className={styles.popover}
            role="dialog"
            aria-modal="true"
            aria-label={title}
          >
            <header className={styles.popoverHeader}>
              <strong>{title}</strong>
              <button
                ref={closeButtonRef}
                type="button"
                onClick={close}
                aria-label={t('chat.receipt.closeReaders')}
              >
                <Icon icon="mdi:close" size={16} />
              </button>
            </header>
            {readerList}
          </aside>
        </>
      )}
    </span>
  );
};
