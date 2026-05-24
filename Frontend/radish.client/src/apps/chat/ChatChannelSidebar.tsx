import { useTranslation } from 'react-i18next';
import type { ChannelVo, EntityIdValue } from '@/types/chat';
import {
  areEntityIdsEqual,
  normalizeEntityId,
} from '@/types/chat';
import styles from './ChatApp.module.css';

interface ChatChannelSidebarProps {
  channels: ChannelVo[];
  activeChannelId: EntityIdValue | null;
  loadingChannels: boolean;
  onSelectChannel: (channelId: string) => void;
}

export const ChatChannelSidebar = ({
  channels,
  activeChannelId,
  loadingChannels,
  onSelectChannel,
}: ChatChannelSidebarProps) => {
  const { t } = useTranslation();

  return (
    <aside className={styles.sidebar}>
      <div className={styles.sidebarHeader}>{t('desktop.apps.chat.name')}</div>
      {loadingChannels ? (
        <div className={styles.sidebarEmpty}>{t('chat.loadingChannels')}</div>
      ) : channels.length === 0 ? (
        <div className={styles.sidebarEmpty}>{t('chat.noChannels')}</div>
      ) : (
        channels.map((channel) => {
          const channelId = normalizeEntityId(channel.voId);
          if (!channelId) {
            return null;
          }

          const isActive = areEntityIdsEqual(activeChannelId, channelId);

          return (
            <button
              key={channelId}
              className={`${styles.channelItem} ${isActive ? styles.channelItemActive : ''}`}
              onClick={() => onSelectChannel(channelId)}
              type="button"
            >
              <span className={styles.channelName}>
                {channel.voIconEmoji || '#'} {channel.voName}
              </span>
              {channel.voUnreadCount > 0 && (
                <span className={styles.unreadBadge}>{channel.voUnreadCount}</span>
              )}
            </button>
          );
        })
      )}
    </aside>
  );
};
