import type { TFunction } from 'i18next';

const forumPublishErrorMessageKeyByCode: Readonly<Record<string, string>> = {
  'Forum.PublishTitleRequired': 'error.forum.publish_title_required',
  'Forum.PublishTitleTooShort': 'error.forum.publish_title_too_short',
  'Forum.PublishTitleTooLong': 'error.forum.publish_title_too_long',
  'Forum.PublishContentRequired': 'error.forum.publish_content_required',
  'Forum.PublishContentTooShort': 'error.forum.publish_content_too_short',
  'Forum.PublishContentTooLong': 'error.forum.publish_content_too_long',
  'Forum.PublishCategoryRequired': 'error.forum.publish_category_required',
  'Forum.PublishContentTypeInvalid': 'error.forum.publish_content_type_invalid',
  'Forum.PublishTagCountInvalid': 'error.forum.publish_tag_count_invalid',
  'Forum.PublishFeatureCombinationInvalid': 'error.forum.publish_feature_combination_invalid',
  'Forum.PublishPollQuestionRequired': 'error.forum.publish_poll_question_required',
  'Forum.PublishPollQuestionTooLong': 'error.forum.publish_poll_question_too_long',
  'Forum.PublishPollEndTimeInvalid': 'error.forum.publish_poll_end_time_invalid',
  'Forum.PublishPollOptionsRequired': 'error.forum.publish_poll_options_required',
  'Forum.PublishPollOptionCountInvalid': 'error.forum.publish_poll_option_count_invalid',
  'Forum.PublishPollOptionsDuplicate': 'error.forum.publish_poll_options_duplicate',
  'Forum.PublishPollOptionTooLong': 'error.forum.publish_poll_option_too_long',
  'Forum.PublishLotteryPrizeNameRequired': 'error.forum.publish_lottery_prize_name_required',
  'Forum.PublishLotteryPrizeNameTooLong': 'error.forum.publish_lottery_prize_name_too_long',
  'Forum.PublishLotteryPrizeDescriptionTooLong': 'error.forum.publish_lottery_prize_description_too_long',
  'Forum.PublishLotteryDrawTimeRequired': 'error.forum.publish_lottery_draw_time_required',
  'Forum.PublishLotteryDrawTimeTooSoon': 'error.forum.publish_lottery_draw_time_too_soon',
  'Forum.PublishLotteryWinnerCountInvalid': 'error.forum.publish_lottery_winner_count_invalid',
  'Forum.PublishForbidden': 'error.forum.publish_forbidden',
  'Forum.PublishTagCreateForbidden': 'error.forum.publish_tag_create_forbidden',
  'Forum.PublishSubmissionIdInvalid': 'error.forum.publish_submission_id_invalid',
  'Forum.PublishSubmissionConflict': 'error.forum.publish_submission_conflict',
  'Forum.PublishSubmissionProcessing': 'error.forum.publish_submission_processing',
  'Forum.PublishRateLimited': 'error.forum.publish_rate_limited',
};

const forumPublishErrorMessageKeys = new Set(Object.values(forumPublishErrorMessageKeyByCode));

export function resolveForumPublishErrorMessage(
  error: unknown,
  t: TFunction,
  fallback: string,
): string {
  if (typeof error !== 'object' || error === null) {
    return fallback;
  }

  const structuredError = error as { code?: unknown; messageKey?: unknown };
  const messageKey = typeof structuredError.messageKey === 'string'
    && forumPublishErrorMessageKeys.has(structuredError.messageKey)
    ? structuredError.messageKey
    : typeof structuredError.code === 'string'
      ? forumPublishErrorMessageKeyByCode[structuredError.code]
      : undefined;

  if (!messageKey) {
    return fallback;
  }

  const localizedMessage = t(messageKey);
  return typeof localizedMessage === 'string' && localizedMessage !== messageKey
    ? localizedMessage
    : fallback;
}
