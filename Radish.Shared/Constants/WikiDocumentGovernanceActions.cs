namespace Radish.Shared.Constants;

/// <summary>Wiki 文档治理事件的稳定动作值。</summary>
public static class WikiDocumentGovernanceActions
{
    public const string Publish = "Publish";
    public const string Unpublish = "Unpublish";
    public const string Archive = "Archive";
    public const string UpdateAccessPolicy = "UpdateAccessPolicy";
    public const string Delete = "Delete";
    public const string Restore = "Restore";
    public const string Rollback = "Rollback";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Publish,
        Unpublish,
        Archive,
        UpdateAccessPolicy,
        Delete,
        Restore,
        Rollback
    };
}
