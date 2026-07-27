namespace Radish.Shared.Constants;

public static class ForumContentRevisionTargetTypes
{
    public const string Post = "Post";
    public const string Comment = "Comment";
    public const string PostAnswer = "PostAnswer";
}

public static class ForumContentRevisionSourceTypes
{
    public const string Baseline = "Baseline";
    public const string Edit = "Edit";
    public const string Restore = "Restore";
}

public static class ForumContentRevisionIntegrityStatuses
{
    public const string Complete = "Complete";
    public const string LegacyIncomplete = "LegacyIncomplete";
    public const string Redacted = "Redacted";
}

public static class ForumContentRevisionReferenceKinds
{
    public const string Content = "Content";
    public const string Cover = "Cover";
}

public static class ForumContentRevisionErrorCodes
{
    public const string NotFound = "Forum.RevisionNotFound";
    public const string AccessDenied = "Forum.RevisionAccessDenied";
    public const string Incomplete = "Forum.RevisionIncomplete";
    public const string Conflict = "Forum.RevisionConflict";
    public const string CategoryUnavailable = "Forum.RevisionCategoryUnavailable";
    public const string TagUnavailable = "Forum.RevisionTagUnavailable";
    public const string AttachmentUnavailable = "Forum.RevisionAttachmentUnavailable";
    public const string ContentRejected = "Forum.RevisionContentRejected";
    public const string EditLimitReached = "Forum.RevisionEditLimitReached";
    public const string CommentWindowExpired = "Forum.CommentRevisionWindowExpired";
    public const string RestoreKeyConflict = "Forum.RevisionRestoreKeyConflict";

    public static string ResolveMessageKey(string errorCode)
    {
        return errorCode switch
        {
            NotFound => "error.forum.revision_not_found",
            AccessDenied => "error.forum.revision_access_denied",
            Incomplete => "error.forum.revision_incomplete",
            Conflict => "error.forum.revision_conflict",
            CategoryUnavailable => "error.forum.revision_category_unavailable",
            TagUnavailable => "error.forum.revision_tag_unavailable",
            AttachmentUnavailable => "error.forum.revision_attachment_unavailable",
            ContentRejected => "error.forum.revision_content_rejected",
            EditLimitReached => "error.forum.revision_edit_limit_reached",
            CommentWindowExpired => "error.forum.comment_revision_window_expired",
            RestoreKeyConflict => "error.forum.revision_restore_key_conflict",
            _ => throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, "Unknown forum revision error code.")
        };
    }
}
