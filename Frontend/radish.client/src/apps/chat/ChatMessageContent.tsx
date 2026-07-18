import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { EntityIdValue } from '@/types/chat';
import { normalizeEntityId } from '@/types/chat';
import { MENTION_PATTERN } from './chatApp.helpers';
import styles from './ChatApp.module.css';

interface ChatMessageContentProps {
  content: string | null | undefined;
  onOpenUserProfile: (userId: EntityIdValue, userName?: string | null) => void;
}

export function ChatMessageContent({ content, onOpenUserProfile }: ChatMessageContentProps) {
  const { t } = useTranslation();
  if (!content) {
    return null;
  }

  const nodes: ReactNode[] = [];
  let lastIndex = 0;
  let keyIndex = 0;

  for (const match of content.matchAll(MENTION_PATTERN)) {
    const matchIndex = match.index ?? 0;
    const matchText = match[0];
    const mentionName = match.groups?.name ?? match[1] ?? t('common.unknownUser');
    const mentionUserId = normalizeEntityId(match.groups?.id ?? match[2]) ?? '';

    if (matchIndex > lastIndex) {
      nodes.push(content.slice(lastIndex, matchIndex));
    }

    nodes.push(
      <button
        key={`mention-${mentionUserId || keyIndex}-${keyIndex}`}
        type="button"
        className={styles.mentionChip}
        onClick={() => onOpenUserProfile(mentionUserId, mentionName)}
      >
        @{mentionName}
      </button>
    );

    lastIndex = matchIndex + matchText.length;
    keyIndex += 1;
  }

  if (lastIndex < content.length) {
    nodes.push(content.slice(lastIndex));
  }

  return nodes;
}
