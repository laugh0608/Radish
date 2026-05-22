import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { ChannelMemberVo, EntityIdValue } from '@/types/chat';
import { normalizeEntityId } from '@/types/chat';
import { getFallbackUserName } from './chatApp.helpers';
import styles from './ChatApp.module.css';

interface ChatMemberPanelProps {
  collapsed: boolean;
  members: ChannelMemberVo[];
  loading: boolean;
  onToggleCollapsed: () => void;
  onOpenUserProfile: (targetUserId: EntityIdValue, targetUserName?: string | null, avatarUrl?: string | null) => void;
  renderAvatarVisual: (name: string, avatarUrl?: string | null, className?: string) => ReactNode;
}

export const ChatMemberPanel = ({
  collapsed,
  members,
  loading,
  onToggleCollapsed,
  onOpenUserProfile,
  renderAvatarVisual,
}: ChatMemberPanelProps) => {
  const { t } = useTranslation();

  return (
    <aside className={`${styles.memberPanel} ${collapsed ? styles.memberPanelCollapsed : ''}`}>
      <button
        type="button"
        className={styles.memberPanelHeader}
        onClick={onToggleCollapsed}
        aria-expanded={!collapsed}
        aria-label={collapsed ? t('chat.expandMembers') : t('chat.collapseMembers')}
        title={collapsed ? t('chat.expandMembers') : t('chat.collapseMembers')}
      >
        {!collapsed && (
          <div className={styles.memberPanelTitle}>
            {t('chat.members')}
            <span className={styles.memberCount}>{members.length}</span>
          </div>
        )}
        <span className={styles.memberCollapseIndicator} aria-hidden="true">
          {collapsed ? '>' : '<'}
        </span>
      </button>

      {!collapsed && (
        <div className={styles.memberPanelBody}>
          {loading ? (
            <div className={styles.memberEmpty}>{t('chat.loadingMembers')}</div>
          ) : members.length === 0 ? (
            <div className={styles.memberEmpty}>{t('chat.noMembers')}</div>
          ) : (
            members.map((member) => {
              const memberId = normalizeEntityId(member.voUserId) ?? member.voUserName ?? 'member';
              const memberName = member.voUserName?.trim() || getFallbackUserName(memberId, t);

              return (
                <button
                  key={memberId}
                  type="button"
                  className={styles.memberItem}
                  onClick={() => onOpenUserProfile(member.voUserId, member.voUserName, member.voUserAvatarUrl)}
                >
                  {renderAvatarVisual(member.voUserName, member.voUserAvatarUrl, styles.memberAvatar)}
                  <span className={styles.memberName}>{memberName}</span>
                </button>
              );
            })
          )}
        </div>
      )}
    </aside>
  );
};
