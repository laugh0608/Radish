namespace Radish.Model.DtoModels;

/// <summary>展示名变更来源</summary>
public static class UserDisplayNameChangeSources
{
    public const string Profile = "Profile";

    public const string Console = "Console";

    public const string System = "System";

    public const string RenameCard = "RenameCard";
}

/// <summary>展示名变更上下文</summary>
public sealed class UserDisplayNameChangeContext
{
    public long OperatorUserId { get; init; }

    public string OperatorUserName { get; init; } = string.Empty;

    public string Source { get; init; } = UserDisplayNameChangeSources.Profile;

    public string? Reason { get; init; }
}
