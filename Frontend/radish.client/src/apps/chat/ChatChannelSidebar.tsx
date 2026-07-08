import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { ChannelVo, EntityIdValue } from '@/types/chat';
import {
  areEntityIdsEqual,
  normalizeEntityId,
} from '@/types/chat';
import { getMessagePreviewText } from './chatApp.helpers';
import styles from './ChatApp.module.css';

type ChatChannelSectionKey = 'mutual' | 'stranger' | 'group' | 'public';

interface ChatChannelSectionDefinition {
  key: ChatChannelSectionKey;
  titleKey: string;
  emptyKey: string;
}

const CHAT_CHANNEL_SECTIONS: ChatChannelSectionDefinition[] = [
  {
    key: 'mutual',
    titleKey: 'chat.section.mutual',
    emptyKey: 'chat.section.mutual.empty',
  },
  {
    key: 'stranger',
    titleKey: 'chat.section.stranger',
    emptyKey: 'chat.section.stranger.empty',
  },
  {
    key: 'group',
    titleKey: 'chat.section.group',
    emptyKey: 'chat.section.group.empty',
  },
  {
    key: 'public',
    titleKey: 'chat.section.public',
    emptyKey: 'chat.section.public.empty',
  },
];

interface ChatChannelSidebarProps {
  channels: ChannelVo[];
  activeChannelId: EntityIdValue | null;
  loadingChannels: boolean;
  onSelectChannel: (channelId: string) => void;
}

function resolveChannelSectionKey(channel: ChannelVo): ChatChannelSectionKey {
  if (channel.voConversationKind === 'mutual'
    || channel.voConversationKind === 'stranger'
    || channel.voConversationKind === 'group'
    || channel.voConversationKind === 'public') {
    return channel.voConversationKind;
  }

  if (channel.voType === 1 || channel.voType === 2) {
    return 'public';
  }

  return 'stranger';
}

export const ChatChannelSidebar = ({
  channels,
  activeChannelId,
  loadingChannels,
  onSelectChannel,
}: ChatChannelSidebarProps) => {
  const { t } = useTranslation();
  const channelSections = useMemo(() => {
    const groupedChannels = new Map<ChatChannelSectionKey, ChannelVo[]>();
    CHAT_CHANNEL_SECTIONS.forEach((section) => {
      groupedChannels.set(section.key, []);
    });

    channels.forEach((channel) => {
      groupedChannels.get(resolveChannelSectionKey(channel))?.push(channel);
    });

    return CHAT_CHANNEL_SECTIONS.map((section) => ({
      ...section,
      channels: groupedChannels.get(section.key) || [],
    }));
  }, [channels]);

  return (
    <aside className={styles.sidebar}>
      <div className={styles.sidebarHeader}>
        <span className={styles.sidebarTitle}>{t('desktop.apps.chat.name')}</span>
        <span className={styles.sidebarCount}>{t('chat.sidebar.total', { count: channels.length })}</span>
      </div>
      {loadingChannels ? (
        <div className={styles.sidebarEmpty}>{t('chat.loadingChannels')}</div>
      ) : channels.length === 0 ? (
        <div className={styles.sidebarEmpty}>{t('chat.noChannels')}</div>
      ) : (
        <div className={styles.sidebarSections}>
          {channelSections.map((section) => (
            <section className={styles.sidebarSection} key={section.key}>
              <div className={styles.sidebarSectionHeader}>
                <span>{t(section.titleKey)}</span>
                <span>{section.channels.length}</span>
              </div>
              {section.channels.length === 0 ? (
                <div className={styles.sidebarSectionEmpty}>{t(section.emptyKey)}</div>
              ) : (
                section.channels.map((channel) => {
                  const channelId = normalizeEntityId(channel.voId);
                  if (!channelId) {
                    return null;
                  }

                  const isActive = areEntityIdsEqual(activeChannelId, channelId);
                  const channelPreview = channel.voLastMessage
                    ? getMessagePreviewText(channel.voLastMessage, t)
                    : channel.voDescription?.trim() || t('chat.channel.noPreview');

                  return (
                    <button
                      key={channelId}
                      className={`${styles.channelItem} ${isActive ? styles.channelItemActive : ''}`}
                      onClick={() => onSelectChannel(channelId)}
                      type="button"
                    >
                      <span className={styles.channelIcon}>{channel.voIconEmoji || '#'}</span>
                      <span className={styles.channelText}>
                        <span className={styles.channelName}>{channel.voName}</span>
                        <span className={styles.channelPreview}>{channelPreview}</span>
                      </span>
                      <span className={styles.channelSignals}>
                        {channel.voHasMention && <span className={styles.mentionDot} aria-label="@mention" />}
                        {channel.voUnreadCount > 0 && (
                          <span className={styles.unreadBadge}>{channel.voUnreadCount}</span>
                        )}
                      </span>
                    </button>
                  );
                })
              )}
            </section>
          ))}
        </div>
      )}
    </aside>
  );
};
