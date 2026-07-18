import { useTranslation } from 'react-i18next';
import type { UserMentionOption } from '@/api/user';
import { normalizeEntityId } from '@/types/chat';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import {
  buildAvatarStyle,
  buildAvatarText,
  getFallbackUserName,
  resolveMediaUrl,
} from './chatApp.helpers';
import styles from './ChatApp.module.css';

interface ChatMentionMenuProps {
  keyword: string;
  loading: boolean;
  options: UserMentionOption[];
  selectedIndex: number;
  apiBaseUrl: string;
  onSelect: (option: UserMentionOption) => void;
  onSelectIndex: (index: number) => void;
}

export function ChatMentionMenu({
  keyword,
  loading,
  options,
  selectedIndex,
  apiBaseUrl,
  onSelect,
  onSelectIndex,
}: ChatMentionMenuProps) {
  const { t } = useTranslation();

  return (
    <div className={styles.mentionDropdown}>
      <div className={styles.mentionHeader}>
        <span className={styles.mentionTitle}>{t('chat.mentionTitle')}</span>
        <span className={styles.mentionHint}>{t('chat.mentionHint')}</span>
      </div>
      {!keyword.trim() ? (
        <div className={styles.mentionState}>{t('chat.mentionTypeToSearch')}</div>
      ) : loading ? (
        <div className={styles.mentionState}>{t('chat.mentionSearching')}</div>
      ) : options.length === 0 ? (
        <div className={styles.mentionState}>{t('chat.mentionNotFound')}</div>
      ) : (
        options.map((option, index) => {
          const optionId = normalizeEntityId(option.voId) ?? `mention-${index}`;
          const optionDisplayName = resolveVisibleUserDisplayName(option, getFallbackUserName(optionId, t));
          const optionName = resolveVisibleUserHandle(option, optionDisplayName) || optionDisplayName;
          const optionAvatarUrl = resolveMediaUrl(apiBaseUrl, option.voAvatar);

          return (
            <button
              key={optionId}
              type="button"
              className={`${styles.mentionOption} ${index === selectedIndex ? styles.mentionOptionActive : ''}`}
              onMouseDown={(event) => {
                event.preventDefault();
                onSelect(option);
              }}
              onMouseEnter={() => onSelectIndex(index)}
            >
              {optionAvatarUrl ? (
                <img src={optionAvatarUrl} alt={optionName} className={styles.mentionAvatar} loading="lazy" />
              ) : (
                <span className={styles.mentionAvatarFallback} style={buildAvatarStyle(optionName)}>
                  {buildAvatarText(optionName)}
                </span>
              )}
              <span className={styles.mentionMeta}>
                <span className={styles.mentionLabel}>@{optionName}</span>
                {optionDisplayName !== optionName ? (
                  <span className={styles.mentionDisplayName}>{optionDisplayName}</span>
                ) : null}
              </span>
            </button>
          );
        })
      )}
    </div>
  );
}
