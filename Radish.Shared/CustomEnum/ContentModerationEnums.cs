namespace Radish.Shared.CustomEnum;

/// <summary>举报目标类型</summary>
public enum ContentReportTargetTypeEnum
{
    Unknown = 0,
    Post = 1,
    Comment = 2,
    ChatMessage = 3,
    Product = 4
}

/// <summary>举报处理状态</summary>
public enum ContentReportStatusEnum
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>治理动作类型</summary>
public enum ModerationActionTypeEnum
{
    None = 0,
    Mute = 1,
    Ban = 2,
    Unmute = 3,
    Unban = 4
}
