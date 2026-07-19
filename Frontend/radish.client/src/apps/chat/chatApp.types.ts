import type { EntityIdValue } from '@/types/chat';

export interface ChatAppProfileNavigationTarget {
  userId: EntityIdValue;
  publicId?: string | null;
  userName?: string | null;
  avatarUrl?: string | null;
}

export interface ChatAppProps {
  onOpenUserProfile?: (target: ChatAppProfileNavigationTarget) => void;
  onOpenFocusedChannel?: (channelId: string) => void;
  onOpenMessageResult?: (target: { channelId: string; messageId: string }) => void;
  onBackToConversationList?: () => void;
  searchRestoreRevision?: number;
  searchHideRevision?: number;
  onSearchVisibilityChange?: (visible: boolean) => void;
}
