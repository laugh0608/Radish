namespace Radish.Shared.Constants;

/// <summary>论坛内容提交可靠性治理常量。</summary>
public static class ContentSubmissionOperationTypes
{
    public const string ForumPostCreate = "ForumPostCreate";
    public const string ForumCommentCreate = "ForumCommentCreate";
    public const string ForumAnswerCreate = "ForumAnswerCreate";
    public const string ForumPostEdit = "ForumPostEdit";
    public const string ForumCommentEdit = "ForumCommentEdit";
}

/// <summary>论坛内容提交处理状态。</summary>
public static class ContentSubmissionStatuses
{
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}

/// <summary>论坛内容提交关联结果类型。</summary>
public static class ContentSubmissionResultTypes
{
    public const string Post = "Post";
    public const string Comment = "Comment";
    public const string PostQuestion = "PostQuestion";
}
