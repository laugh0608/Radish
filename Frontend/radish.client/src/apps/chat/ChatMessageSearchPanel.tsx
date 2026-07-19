import { type FormEvent, type KeyboardEvent, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { ChatMessageSearchScopes, type ChannelMessageSearchItemVo } from '@radish/http';
import { Icon } from '@radish/ui/icon';
import { useChatMessageSearch } from './useChatMessageSearch';
import styles from './ChatMessageSearchPanel.module.css';

interface ChatMessageSearchPanelProps {
  activeChannelId: string | null;
  accountKey: string;
  hidden?: boolean;
  onClose: () => void;
  onOpenResult: (item: ChannelMessageSearchItemVo) => void;
}

function formatResultTime(value: string, locale: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(locale, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

function renderHighlightedSnippet(snippet: string, keyword: string) {
  const normalizedKeyword = keyword.trim();
  if (!normalizedKeyword) {
    return snippet;
  }

  const lowerSnippet = snippet.toLocaleLowerCase();
  const lowerKeyword = normalizedKeyword.toLocaleLowerCase();
  const parts = [];
  let cursor = 0;
  let matchIndex = lowerSnippet.indexOf(lowerKeyword);
  while (matchIndex >= 0) {
    if (matchIndex > cursor) {
      parts.push(snippet.slice(cursor, matchIndex));
    }
    parts.push(<mark key={`${matchIndex}-${cursor}`}>{snippet.slice(matchIndex, matchIndex + normalizedKeyword.length)}</mark>);
    cursor = matchIndex + normalizedKeyword.length;
    matchIndex = lowerSnippet.indexOf(lowerKeyword, cursor);
  }
  if (cursor < snippet.length) {
    parts.push(snippet.slice(cursor));
  }

  return parts.length > 0 ? parts : snippet;
}

export function ChatMessageSearchPanel({
  activeChannelId,
  accountKey,
  hidden = false,
  onClose,
  onOpenResult,
}: ChatMessageSearchPanelProps) {
  const { t, i18n } = useTranslation();
  const search = useChatMessageSearch({ activeChannelId, accountKey });
  const currentScopeUnavailable = search.scope === ChatMessageSearchScopes.CurrentChannel && !activeChannelId;
  const canSubmit = search.keyword.trim().length >= 2
    && !currentScopeUnavailable
    && search.status !== 'loading'
    && search.status !== 'offline';
  const resultCountLabel = useMemo(() => t('chat.search.resultCount', {
    count: search.items.length,
  }), [search.items.length, t]);

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (canSubmit) {
      void search.search();
    }
  };

  const handlePanelKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (event.key === 'Escape') {
      event.stopPropagation();
      onClose();
    }
  };

  return (
    <aside
      className={styles.panel}
      hidden={hidden}
      aria-label={t('chat.search.title')}
      onKeyDown={handlePanelKeyDown}
    >
      <header className={styles.header}>
        <button type="button" className={styles.mobileBack} onClick={onClose}>
          <Icon icon="mdi:chevron-left" size={20} />
          <span>{t('chat.search.back')}</span>
        </button>
        <div className={styles.headerCopy}>
          <h2>{t('chat.search.title')}</h2>
          <p>{t('chat.search.snapshotHint')}</p>
        </div>
        <button type="button" className={styles.closeButton} onClick={onClose} aria-label={t('chat.search.close')}>
          <Icon icon="mdi:close" size={19} />
        </button>
      </header>

      <form className={styles.form} onSubmit={handleSubmit}>
        <label className={styles.keywordField}>
          <span className={styles.visuallyHidden}>{t('chat.search.keyword')}</span>
          <Icon icon="mdi:magnify" size={19} />
          <input
            autoFocus
            value={search.keyword}
            onChange={(event) => search.setKeyword(event.target.value)}
            placeholder={t('chat.search.placeholder')}
            autoComplete="off"
            maxLength={100}
          />
          {search.keyword && (
            <button type="button" onClick={() => search.setKeyword('')} aria-label={t('chat.search.clearKeyword')}>
              <Icon icon="mdi:close-circle" size={17} />
            </button>
          )}
        </label>

        <fieldset className={styles.scopeFieldset}>
          <legend className={styles.visuallyHidden}>{t('chat.search.scope')}</legend>
          <label data-active={search.scope === ChatMessageSearchScopes.CurrentChannel}>
            <input
              type="radio"
              name="chat-search-scope"
              checked={search.scope === ChatMessageSearchScopes.CurrentChannel}
              disabled={!activeChannelId}
              onChange={() => search.setScope(ChatMessageSearchScopes.CurrentChannel)}
            />
            <span>{t('chat.search.scope.current')}</span>
          </label>
          <label data-active={search.scope === ChatMessageSearchScopes.AllVisibleChannels}>
            <input
              type="radio"
              name="chat-search-scope"
              checked={search.scope === ChatMessageSearchScopes.AllVisibleChannels}
              onChange={() => search.setScope(ChatMessageSearchScopes.AllVisibleChannels)}
            />
            <span>{t('chat.search.scope.all')}</span>
          </label>
        </fieldset>

        <div className={styles.timeFilters}>
          <label>
            <span>{t('chat.search.fromDate')}</span>
            <input type="date" value={search.fromDate} onChange={(event) => search.setFromDate(event.target.value)} />
          </label>
          <label>
            <span>{t('chat.search.toDate')}</span>
            <input type="date" value={search.toDate} onChange={(event) => search.setToDate(event.target.value)} />
          </label>
        </div>

        <button type="submit" className={styles.submitButton} disabled={!canSubmit}>
          <Icon icon="mdi:magnify" size={17} />
          <span>{t(search.status === 'loading' ? 'chat.search.searching' : 'chat.search.submit')}</span>
        </button>
      </form>

      <div className={styles.resultsHeader}>
        <strong>{t('chat.search.results')}</strong>
        {search.items.length > 0 && <span>{resultCountLabel}</span>}
      </div>

      <div className={styles.statusRegion} aria-live="polite" aria-atomic="true">
        {search.status === 'idle' && (
          <div className={styles.stateSlot}>
            <Icon icon="mdi:message-search-outline" size={25} />
            <strong>{t('chat.search.initialTitle')}</strong>
            <span>{t('chat.search.initialDescription')}</span>
          </div>
        )}
        {search.status === 'loading' && search.items.length === 0 && (
          <div className={styles.stateSlot} data-tone="loading">
            <Icon icon="mdi:loading" size={25} />
            <strong>{t('chat.search.loadingTitle')}</strong>
            <span>{t('chat.search.loadingDescription')}</span>
          </div>
        )}
        {search.status === 'empty' && (
          <div className={styles.stateSlot}>
            <Icon icon="mdi:magnify-close" size={25} />
            <strong>{t('chat.search.emptyTitle')}</strong>
            <span>{t('chat.search.emptyDescription')}</span>
          </div>
        )}
        {search.status === 'offline' && (
          <div className={styles.stateSlot} data-tone="warning">
            <Icon icon="mdi:wifi-off" size={25} />
            <strong>{t('chat.search.offlineTitle')}</strong>
            <span>{t('chat.search.offlineDescription')}</span>
          </div>
        )}
        {search.status === 'error' && (
          <div className={styles.stateSlot} data-tone="danger" role="alert">
            <Icon icon="mdi:alert-circle-outline" size={25} />
            <strong>{t('chat.search.errorTitle')}</strong>
            <span>{search.errorMessage || t(
              currentScopeUnavailable ? 'chat.search.currentUnavailable' : 'chat.search.errorDescription'
            )}</span>
            <button type="button" onClick={() => { void search.search(); }} disabled={!canSubmit}>
              {t('chat.search.retry')}
            </button>
          </div>
        )}
        {search.status === 'cursor-invalid' && (
          <div className={styles.stateSlot} data-tone="danger" role="alert">
            <Icon icon="mdi:refresh-alert" size={25} />
            <strong>{t('chat.search.cursorInvalidTitle')}</strong>
            <span>{t('chat.search.cursorInvalidDescription')}</span>
            <button type="button" onClick={() => { void search.search(); }} disabled={!canSubmit}>
              {t('chat.search.restart')}
            </button>
          </div>
        )}
      </div>

      {search.items.length > 0 && (
        <ol className={styles.resultList} aria-label={t('chat.search.results')}>
          {search.items.map((item) => (
            <li key={item.voMessageId}>
              <button type="button" onClick={() => onOpenResult(item)}>
                <span className={styles.resultMeta}>
                  <strong>{item.voChannelDisplayName}</strong>
                  <time dateTime={item.voCreateTime}>{formatResultTime(item.voCreateTime, i18n.language)}</time>
                </span>
                <span className={styles.resultSender}>{item.voSenderDisplayName}</span>
                <span className={styles.resultSnippet}>
                  {renderHighlightedSnippet(item.voSnippet, search.keyword)}
                </span>
                <span className={styles.resultAction}>
                  {t('chat.search.openResult')}
                  <Icon icon="mdi:chevron-right" size={16} />
                </span>
              </button>
            </li>
          ))}
        </ol>
      )}

      {search.hasMore && search.status !== 'cursor-invalid' && (
        <button
          type="button"
          className={styles.loadMoreButton}
          disabled={search.loadingMore || search.status === 'offline'}
          onClick={() => { void search.loadMore(); }}
        >
          <Icon icon={search.loadingMore ? 'mdi:loading' : 'mdi:arrow-down'} size={17} />
          <span>{t(search.loadingMore ? 'chat.search.loadingMore' : 'chat.search.loadMore')}</span>
        </button>
      )}
    </aside>
  );
}
