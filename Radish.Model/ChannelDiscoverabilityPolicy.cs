using System.Text.RegularExpressions;

namespace Radish.Model;

/// <summary>频道进入匿名发现摘要前必须满足的领域资格。</summary>
public static class ChannelDiscoverabilityPolicy
{
    public const string TypeNotPublic = "type-not-public";
    public const string ChannelDisabled = "channel-disabled";
    public const string ChannelDeleted = "channel-deleted";
    public const string NameInvalid = "name-invalid";
    public const string SlugInvalid = "slug-invalid";

    private static readonly Regex ValidSlugPattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> GetSummaryEligibilityIssues(Channel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        var issues = new List<string>();
        if (channel.Type != ChannelType.Public)
        {
            issues.Add(TypeNotPublic);
        }

        if (!channel.IsEnabled)
        {
            issues.Add(ChannelDisabled);
        }

        if (channel.IsDeleted)
        {
            issues.Add(ChannelDeleted);
        }

        var normalizedName = channel.Name?.Trim() ?? string.Empty;
        if (normalizedName.Length is < 1 or > 100)
        {
            issues.Add(NameInvalid);
        }

        var normalizedSlug = channel.Slug?.Trim() ?? string.Empty;
        if (normalizedSlug.Length is < 1 or > 100 || !ValidSlugPattern.IsMatch(normalizedSlug))
        {
            issues.Add(SlugInvalid);
        }

        return issues;
    }

    public static bool CanExposeSummary(Channel channel)
    {
        return GetSummaryEligibilityIssues(channel).Count == 0;
    }
}
