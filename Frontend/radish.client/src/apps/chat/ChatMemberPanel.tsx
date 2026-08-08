import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import type { ChannelMemberVo, EntityIdValue } from '@/types/chat';
import { normalizeEntityId } from '@/types/chat';
import { resolveVisibleUserDisplayName } from '@/utils/userIdentityDisplay';
import { getFallbackUserName } from './chatApp.helpers';
import styles from './ChatApp.module.css';

interface ChatMemberPanelProps {
  members: ChannelMemberVo[];
  loading: boolean;
  loadError: string | null;
  onClose: () => void;
  onRetry: () => void;
  onOpenUserProfile: (targetUserId: EntityIdValue, targetUserName?: string | null, avatarUrl?: string | null) => void;
  renderAvatarVisual: (name: string, avatarUrl?: string | null, className?: string) => ReactNode;
}

export const ChatMemberPanel = ({
  members,
  loading,
  loadError,
  onClose,
  onRetry,
  onOpenUserProfile,
  renderAvatarVisual,
}: ChatMemberPanelProps) => {
  const { t } = useTranslation();

  return (
    <aside className={styles.memberPanel} aria-label={t('chat.onlineMembers')}>
      <div className={styles.memberPanelHeader}>
        <div className={styles.memberPanelTitle}>
          <Icon icon="mdi:account-group-outline" size={17} />
          <span>{t('chat.onlineMembers')}</span>
          {!loading && !loadError && <span className={styles.memberCount}>{members.length}</span>}
        </div>
        <button type="button" className={styles.memberPanelClose} onClick={onClose} aria-label={t('chat.collapseMembers')}>
          <Icon icon="mdi:close" size={17} />
        </button>
      </div>

      <div className={styles.memberPanelBody}>
        {loading ? (
          <div className={styles.memberEmpty}>{t('chat.loadingMembers')}</div>
        ) : loadError ? (
          <div className={styles.memberLoadError} role="alert">
            <span>{loadError}</span>
            <button type="button" onClick={onRetry}>{t('chat.memberRetry')}</button>
          </div>
        ) : members.length === 0 ? (
          <div className={styles.memberEmpty}>{t('chat.noMembers')}</div>
        ) : (
          members.map((member) => {
            const memberId = normalizeEntityId(member.voUserId) ?? member.voUserName ?? 'member';
            const memberName = resolveVisibleUserDisplayName(
              { voUserName: member.voUserName },
              getFallbackUserName(memberId, t)
            );

            return (
              <button
                key={memberId}
                type="button"
                className={styles.memberItem}
                onClick={() => onOpenUserProfile(member.voUserId, memberName, member.voUserAvatarUrl)}
              >
                {renderAvatarVisual(memberName, member.voUserAvatarUrl, styles.memberAvatar)}
                <span className={styles.memberName}>{memberName}</span>
              </button>
            );
          })
        )}
      </div>
    </aside>
  );
};
