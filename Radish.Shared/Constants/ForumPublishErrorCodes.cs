namespace Radish.Shared.Constants;

/// <summary>
/// 论坛发帖业务域的稳定错误码。
/// </summary>
public static class ForumPublishErrorCodes
{
    public const string TitleRequired = "Forum.PublishTitleRequired";
    public const string TitleTooShort = "Forum.PublishTitleTooShort";
    public const string TitleTooLong = "Forum.PublishTitleTooLong";
    public const string ContentRequired = "Forum.PublishContentRequired";
    public const string ContentTooShort = "Forum.PublishContentTooShort";
    public const string ContentTooLong = "Forum.PublishContentTooLong";
    public const string CategoryRequired = "Forum.PublishCategoryRequired";
    public const string ContentTypeInvalid = "Forum.PublishContentTypeInvalid";
    public const string TagCountInvalid = "Forum.PublishTagCountInvalid";
    public const string FeatureCombinationInvalid = "Forum.PublishFeatureCombinationInvalid";
    public const string PollQuestionRequired = "Forum.PublishPollQuestionRequired";
    public const string PollQuestionTooLong = "Forum.PublishPollQuestionTooLong";
    public const string PollEndTimeInvalid = "Forum.PublishPollEndTimeInvalid";
    public const string PollOptionsRequired = "Forum.PublishPollOptionsRequired";
    public const string PollOptionCountInvalid = "Forum.PublishPollOptionCountInvalid";
    public const string PollOptionsDuplicate = "Forum.PublishPollOptionsDuplicate";
    public const string PollOptionTooLong = "Forum.PublishPollOptionTooLong";
    public const string LotteryPrizeNameRequired = "Forum.PublishLotteryPrizeNameRequired";
    public const string LotteryPrizeNameTooLong = "Forum.PublishLotteryPrizeNameTooLong";
    public const string LotteryPrizeDescriptionTooLong = "Forum.PublishLotteryPrizeDescriptionTooLong";
    public const string LotteryDrawTimeRequired = "Forum.PublishLotteryDrawTimeRequired";
    public const string LotteryDrawTimeTooSoon = "Forum.PublishLotteryDrawTimeTooSoon";
    public const string LotteryWinnerCountInvalid = "Forum.PublishLotteryWinnerCountInvalid";
    public const string Forbidden = "Forum.PublishForbidden";
    public const string TagCreateForbidden = "Forum.PublishTagCreateForbidden";
    public const string SubmissionIdInvalid = "Forum.PublishSubmissionIdInvalid";
    public const string SubmissionConflict = "Forum.PublishSubmissionConflict";
    public const string SubmissionProcessing = "Forum.PublishSubmissionProcessing";
    public const string RateLimited = "Forum.PublishRateLimited";

    public static string ResolveMessageKey(string errorCode)
    {
        return errorCode switch
        {
            TitleRequired => "error.forum.publish_title_required",
            TitleTooShort => "error.forum.publish_title_too_short",
            TitleTooLong => "error.forum.publish_title_too_long",
            ContentRequired => "error.forum.publish_content_required",
            ContentTooShort => "error.forum.publish_content_too_short",
            ContentTooLong => "error.forum.publish_content_too_long",
            CategoryRequired => "error.forum.publish_category_required",
            ContentTypeInvalid => "error.forum.publish_content_type_invalid",
            TagCountInvalid => "error.forum.publish_tag_count_invalid",
            FeatureCombinationInvalid => "error.forum.publish_feature_combination_invalid",
            PollQuestionRequired => "error.forum.publish_poll_question_required",
            PollQuestionTooLong => "error.forum.publish_poll_question_too_long",
            PollEndTimeInvalid => "error.forum.publish_poll_end_time_invalid",
            PollOptionsRequired => "error.forum.publish_poll_options_required",
            PollOptionCountInvalid => "error.forum.publish_poll_option_count_invalid",
            PollOptionsDuplicate => "error.forum.publish_poll_options_duplicate",
            PollOptionTooLong => "error.forum.publish_poll_option_too_long",
            LotteryPrizeNameRequired => "error.forum.publish_lottery_prize_name_required",
            LotteryPrizeNameTooLong => "error.forum.publish_lottery_prize_name_too_long",
            LotteryPrizeDescriptionTooLong => "error.forum.publish_lottery_prize_description_too_long",
            LotteryDrawTimeRequired => "error.forum.publish_lottery_draw_time_required",
            LotteryDrawTimeTooSoon => "error.forum.publish_lottery_draw_time_too_soon",
            LotteryWinnerCountInvalid => "error.forum.publish_lottery_winner_count_invalid",
            Forbidden => "error.forum.publish_forbidden",
            TagCreateForbidden => "error.forum.publish_tag_create_forbidden",
            SubmissionIdInvalid => "error.forum.publish_submission_id_invalid",
            SubmissionConflict => "error.forum.publish_submission_conflict",
            SubmissionProcessing => "error.forum.publish_submission_processing",
            RateLimited => "error.forum.publish_rate_limited",
            _ => throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, "Unknown forum publish error code.")
        };
    }
}
