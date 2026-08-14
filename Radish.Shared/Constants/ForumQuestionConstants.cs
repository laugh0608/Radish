namespace Radish.Shared.Constants;

public static class PostAnswerAcceptanceEventTypes
{
    public const string Accepted = "Accepted";
    public const string Replaced = "Replaced";
    public const string Revoked = "Revoked";
    public const string ClearedByModeration = "ClearedByModeration";
}

public static class ForumQuestionErrorCodes
{
    public const string NotFound = "Forum.QuestionNotFound";
    public const string AnswerNotFound = "Forum.AnswerNotFound";
    public const string AccessDenied = "Forum.AnswerAccessDenied";
    public const string Conflict = "Forum.AnswerRevisionConflict";
    public const string AcceptanceConflict = "Forum.QuestionAcceptanceConflict";
    public const string AcceptedAnswerLocked = "Forum.AcceptedAnswerLocked";
    public const string AttachmentUnavailable = "Forum.AnswerAttachmentUnavailable";
    public const string RevisionIncomplete = "Forum.AnswerRevisionIncomplete";
    public const string InteractionUnavailable = "UserBlock.InteractionUnavailable";

    public static string ResolveMessageKey(string errorCode) => errorCode switch
    {
        NotFound => "error.forum.question_not_found",
        AnswerNotFound => "error.forum.answer_not_found",
        AccessDenied => "error.forum.answer_access_denied",
        Conflict => "error.forum.answer_revision_conflict",
        AcceptanceConflict => "error.forum.question_acceptance_conflict",
        AcceptedAnswerLocked => "error.forum.accepted_answer_locked",
        AttachmentUnavailable => "error.forum.answer_attachment_unavailable",
        RevisionIncomplete => "error.forum.answer_revision_incomplete",
        InteractionUnavailable => "error.user_block.interaction_unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, "Unknown forum question error code.")
    };
}
