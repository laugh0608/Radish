import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { ChannelVo, ChatChannelListView, EntityIdValue } from '@/types/chat';
import {
  areEntityIdsEqual,
  normalizeEntityId,
} from '@/types/chat';
import { getMessagePreviewText } from './chatApp.helpers';
import { resolveMediaUrl } from '@/utils/media';
import styles from './ChatApp.module.css';

type ChatChannelSectionKey = 'mutual' | 'stranger' | 'group' | 'public' | 'archived';

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

const ARCHIVED_CHANNEL_SECTION: ChatChannelSectionDefinition = {
  key: 'archived',
  titleKey: 'chat.section.archived',
  emptyKey: 'chat.section.archived.empty',
};

interface ChatChannelSidebarProps {
  channels: ChannelVo[];
  activeChannelId: EntityIdValue | null;
  listView: ChatChannelListView;
  loadingChannels: boolean;
  listError: string | null;
  onSelectChannel: (channelId: string) => void;
  onChangeListView: (view: ChatChannelListView) => void;
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

function resolveChannelStatusKey(channel: ChannelVo): string | null {
  if (!channel.voIsPeerAvailable) {
    return 'chat.channelStatus.unavailable';
  }

  if (channel.voIsBlockedByCurrentUser) {
    return 'chat.channelStatus.blockedByMe';
  }

  if (channel.voCanUnblock) {
    return 'chat.channelStatus.blockedByMe';
  }

  if (channel.voDirectRequestStatus === 'declined') {
    return 'chat.channelStatus.declined';
  }

  if (channel.voDirectRequestStatus === 'pending') {
    return channel.voCanAccept
      ? 'chat.channelStatus.requestReceived'
      : 'chat.channelStatus.requestPending';
  }

  return channel.voIsArchived ? 'chat.channelStatus.archived' : null;
}

export const ChatChannelSidebar = ({
  channels,
  activeChannelId,
  listView,
  loadingChannels,
  listError,
  onSelectChannel,
  onChangeListView,
}: ChatChannelSidebarProps) => {
  const { t } = useTranslation();
  const channelSections = useMemo(() => {
    const groupedChannels = new Map<ChatChannelSectionKey, ChannelVo[]>();
    const definitions = listView === 'archived' ? [ARCHIVED_CHANNEL_SECTION] : CHAT_CHANNEL_SECTIONS;
    definitions.forEach((section) => {
      groupedChannels.set(section.key, []);
    });

    channels.forEach((channel) => {
      const sectionKey = listView === 'archived' ? 'archived' : resolveChannelSectionKey(channel);
      groupedChannels.get(sectionKey)?.push(channel);
    });

    return definitions.map((section) => ({
      ...section,
      channels: groupedChannels.get(section.key) || [],
    }));
  }, [channels, listView]);

  return (
    <aside className={styles.sidebar}>
      <div className={styles.sidebarHeader}>
        <span className={styles.sidebarTitle}>{t('desktop.apps.chat.name')}</span>
        <span className={styles.sidebarCount}>{t('chat.sidebar.total', { count: channels.length })}</span>
      </div>
      <div className={styles.sidebarViews} aria-label={t('chat.sidebar.viewsLabel')}>
        <button
          type="button"
          className={`${styles.sidebarViewButton} ${listView === 'active' ? styles.sidebarViewButtonActive : ''}`}
          aria-pressed={listView === 'active'}
          onClick={() => onChangeListView('active')}
        >
          {t('chat.view.active')}
        </button>
        <button
          type="button"
          className={`${styles.sidebarViewButton} ${listView === 'archived' ? styles.sidebarViewButtonActive : ''}`}
          aria-pressed={listView === 'archived'}
          onClick={() => onChangeListView('archived')}
        >
          {t('chat.view.archived')}
        </button>
      </div>
      {loadingChannels ? (
        <div className={styles.sidebarEmpty}>{t('chat.loadingChannels')}</div>
      ) : listError ? (
        <div className={styles.sidebarEmpty} role="alert">{listError}</div>
      ) : channels.length === 0 ? (
        <div className={styles.sidebarEmpty}>
          {t(listView === 'archived' ? 'chat.noArchivedChannels' : 'chat.noChannels')}
        </div>
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
                  const channelName = channel.voPeerDisplayName?.trim() || channel.voName;
                  const channelAvatarUrl = resolveMediaUrl(channel.voPeerAvatarUrl);
                  const channelStatusKey = resolveChannelStatusKey(channel);
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
                      <span className={styles.channelIcon} aria-hidden="true">
                        {channelAvatarUrl ? (
                          <img src={channelAvatarUrl} alt="" className={styles.channelAvatarImage} loading="lazy" />
                        ) : (
                          channel.voIconEmoji || channelName.charAt(0).toUpperCase() || '#'
                        )}
                      </span>
                      <span className={styles.channelText}>
                        <span className={styles.channelName}>{channelName}</span>
                        <span className={styles.channelPreview}>{channelPreview}</span>
                      </span>
                      <span className={styles.channelSignals}>
                        {channelStatusKey && (
                          <span className={styles.channelStatus}>{t(channelStatusKey)}</span>
                        )}
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
