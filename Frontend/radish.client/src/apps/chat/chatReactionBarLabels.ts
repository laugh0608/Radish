import type { TFunction } from 'i18next';
import type { ReactionBarLabels } from '@radish/ui';

export function createChatReactionBarLabels(t: TFunction): ReactionBarLabels {
  return {
    loginHint: t('chat.reaction.loginHint'),
    limitHint: (limit) => t('chat.reaction.limitHint', { limit }),
    reactionLabel: (emoji, count, selected) => t(
      selected ? 'chat.reaction.label.selected' : 'chat.reaction.label.unselected',
      { emoji, count }
    ),
    expandRemaining: (count) => t('chat.reaction.expandRemaining', { count }),
    expandTitle: t('chat.reaction.expandTitle'),
    collapse: t('chat.reaction.collapse'),
    addReaction: t('chat.reaction.add'),
    addEmoji: (emoji) => t('chat.reaction.addEmoji', { emoji }),
    moreEmoji: t('chat.reaction.moreEmoji'),
    stickerPicker: {
      searchPlaceholder: t('chat.reaction.searchPlaceholder'),
      clearSearch: t('chat.reaction.clearSearch'),
      reactionOnly: (name) => t('chat.reaction.reactionOnly', { name }),
      noEmoji: t('chat.reaction.noEmoji'),
      noSticker: t('chat.reaction.noSticker'),
    },
  };
}
