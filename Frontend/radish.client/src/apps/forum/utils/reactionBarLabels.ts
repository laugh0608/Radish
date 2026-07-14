import type { ReactionBarLabels } from '@radish/ui';
import type { TFunction } from 'i18next';

export function createReactionBarLabels(t: TFunction): ReactionBarLabels {
  return {
    loginHint: t('forum.reaction.loginHint'),
    limitHint: (limit) => t('forum.reaction.limitHint', { limit }),
    reactionLabel: (emoji, count, selected) => t(
      selected ? 'forum.reaction.label.selected' : 'forum.reaction.label.unselected',
      { emoji, count }
    ),
    expandRemaining: (count) => t('forum.reaction.expandRemaining', { count }),
    expandTitle: t('forum.reaction.expandTitle'),
    collapse: t('forum.reaction.collapse'),
    addReaction: t('forum.reaction.add'),
    addEmoji: (emoji) => t('forum.reaction.addEmoji', { emoji }),
    moreEmoji: t('forum.reaction.moreEmoji'),
    stickerPicker: {
      searchPlaceholder: t('forum.reaction.searchPlaceholder'),
      clearSearch: t('forum.reaction.clearSearch'),
      reactionOnly: (name) => t('forum.reaction.reactionOnly', { name }),
      noEmoji: t('forum.reaction.noEmoji'),
      noSticker: t('forum.reaction.noSticker'),
    },
  };
}
