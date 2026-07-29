namespace Radish.Shared.Constants;

/// <summary>公开标签发现错误契约。</summary>
public static class TagDiscoveryErrorCodes
{
    public const string TopCountInvalid = "Forum.TagDiscoveryTopCountInvalid";
    public const string TagUnavailable = "Forum.TagDiscoveryTagUnavailable";

    public static string ResolveMessageKey(string errorCode) => errorCode switch
    {
        TopCountInvalid => "error.forum.tag_discovery_top_count_invalid",
        TagUnavailable => "error.forum.tag_discovery_tag_unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, "Unknown tag discovery error code.")
    };
}
