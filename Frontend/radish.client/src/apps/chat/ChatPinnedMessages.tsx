import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ChatMessagePinStateVo } from '@radish/http';
import { Icon } from '@radish/ui/icon';
import { getIntlLocale } from '@/locales/language';
import { getMessagePreviewText } from './chatApp.helpers';
import styles from './ChatPinnedMessages.module.css';

interface ChatPinnedMessagesProps {
  state?: ChatMessagePinStateVo;
  loading: boolean;
  loadError: string | null;
  canManage: boolean;
  pendingMessageId: string | null;
  onRetry: () => void;
  onNavigate: (messageId: string) => void;
  onSetPinned: (messageId: string, isPinned: boolean) => Promise<void>;
}

export const ChatPinnedMessages = ({
  state,
  loading,
  loadError,
  canManage,
  pendingMessageId,
  onRetry,
  onNavigate,
  onSetPinned,
}: ChatPinnedMessagesProps) => {
  const { t, i18n } = useTranslation();
  const [open, setOpen] = useState(false);
  const openButtonRef = useRef<HTMLButtonElement | null>(null);
  const closeButtonRef = useRef<HTMLButtonElement | null>(null);
  const locale = getIntlLocale(i18n.resolvedLanguage ?? i18n.language);
  const items = state?.voItems ?? [];
  const latest = items[0];
  const dateFormatter = useMemo(() => new Intl.DateTimeFormat(locale, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }), [locale]);

  useEffect(() => {
    if (!open) {
      return;
    }

    closeButtonRef.current?.focus();
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
        requestAnimationFrame(() => openButtonRef.current?.focus());
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [open]);

  useEffect(() => {
    if (items.length === 0) {
      setOpen(false);
    }
  }, [items.length]);

  useEffect(() => {
    setOpen(false);
  }, [state?.voChannelId]);

  const close = () => {
    setOpen(false);
    requestAnimationFrame(() => openButtonRef.current?.focus());
  };

  const navigate = (messageId: string) => {
    setOpen(false);
    onNavigate(messageId);
  };

  if (loadError) {
    return (
      <div className={styles.loadError} role="alert">
        <span>{loadError}</span>
        <button type="button" onClick={onRetry}>{t('chat.pin.retry')}</button>
      </div>
    );
  }

  if (!latest) {
    return loading ? <span className={styles.srOnly}>{t('chat.pin.loading')}</span> : null;
  }

  return (
    <>
      <div className={styles.bar}>
        <button
          type="button"
          className={styles.latestButton}
          onClick={() => navigate(latest.voMessageId)}
          title={t('chat.pin.locate')}
        >
          <span className={styles.pinIcon}><Icon icon="mdi:pin" size={17} /></span>
          <span className={styles.latestCopy}>
            <span className={styles.eyebrow}>{t('chat.pin.latest', { name: latest.voPinnedByName })}</span>
            <span className={styles.preview}>{getMessagePreviewText(latest.voMessage, t)}</span>
          </span>
        </button>
        <button
          ref={openButtonRef}
          type="button"
          className={styles.countButton}
          onClick={() => setOpen(true)}
          aria-expanded={open}
          aria-controls="chat-pinned-message-panel"
        >
          {t('chat.pin.count', { count: items.length })}
          <Icon icon="mdi:chevron-right" size={16} />
        </button>
      </div>

      {open && <button type="button" className={styles.backdrop} onClick={close} aria-label={t('common.close')} />}
      {open && (
        <aside
          id="chat-pinned-message-panel"
          className={styles.panel}
          role="dialog"
          aria-labelledby="chat-pinned-message-title"
        >
          <div className={styles.sheetHandle} aria-hidden="true" />
          <header className={styles.panelHeader}>
            <span>
              <strong id="chat-pinned-message-title">{t('chat.pin.title')}</strong>
              <small>{t('chat.pin.capacity', { count: items.length, limit: 20 })}</small>
            </span>
            <button
              ref={closeButtonRef}
              type="button"
              className={styles.closeButton}
              onClick={close}
              aria-label={t('chat.pin.close')}
            >
              <Icon icon="mdi:close" size={18} />
            </button>
          </header>

          <div className={styles.list}>
            {items.map((item) => {
              const pending = pendingMessageId === item.voMessageId;
              return (
                <article key={item.voId} className={styles.item}>
                  <div className={styles.itemMeta}>
                    <span>{item.voMessage.voUserName}</span>
                    <time dateTime={item.voPinnedAt}>{dateFormatter.format(new Date(item.voPinnedAt))}</time>
                  </div>
                  <button
                    type="button"
                    className={styles.itemPreview}
                    onClick={() => navigate(item.voMessageId)}
                  >
                    {getMessagePreviewText(item.voMessage, t)}
                  </button>
                  <div className={styles.itemFooter}>
                    <span>{t('chat.pin.pinnedBy', { name: item.voPinnedByName })}</span>
                    <span className={styles.itemActions}>
                      <button type="button" onClick={() => navigate(item.voMessageId)}>
                        <Icon icon="mdi:target" size={14} />
                        {t('chat.pin.locate')}
                      </button>
                      {canManage && (
                        <button
                          type="button"
                          onClick={() => { void onSetPinned(item.voMessageId, false); }}
                          disabled={pending}
                        >
                          <Icon icon="mdi:pin-off" size={14} />
                          {pending ? t('chat.pin.updating') : t('chat.pin.unpin')}
                        </button>
                      )}
                    </span>
                  </div>
                </article>
              );
            })}
          </div>
          <footer className={styles.panelFooter}>
            <span>{t('chat.pin.revision', { revision: state?.voRevision ?? '0' })}</span>
            <span>{t('chat.pin.authoritativeHint')}</span>
          </footer>
        </aside>
      )}
    </>
  );
};
