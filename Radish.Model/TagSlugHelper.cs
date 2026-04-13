using System.Text;

namespace Radish.Model;

/// <summary>
/// 标签 slug 规范化工具。
/// </summary>
public static class TagSlugHelper
{
    public const int MaxSlugLength = 50;

    private static readonly IReadOnlyDictionary<string, string> FixedTagSlugMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["社区新闻"] = "community-news",
            ["社区活动"] = "community-events",
            ["精华帖"] = "featured-posts",
            ["碎碎念"] = "random-thoughts",
            ["公告"] = "announcements",
            ["问答"] = "questions",
            ["投票"] = "polls",
            ["抽奖"] = "lotteries"
        };

    private static readonly IReadOnlyDictionary<string, string> FixedSlugNameMap =
        FixedTagSlugMap.ToDictionary(entry => entry.Value, entry => entry.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 生成公开侧使用的 canonical slug。
    /// </summary>
    public static string BuildCanonicalSlug(string name, string? preferredSlug = null)
    {
        var normalizedPreferred = NormalizeInput(preferredSlug);
        if (!string.IsNullOrWhiteSpace(normalizedPreferred) && IsFriendlyAsciiCandidate(normalizedPreferred))
        {
            return BuildSlugCore(normalizedPreferred);
        }

        var normalizedName = NormalizeInput(name);
        if (!string.IsNullOrWhiteSpace(normalizedPreferred) &&
            FixedSlugNameMap.TryGetValue(normalizedPreferred, out var fixedNameFromSlug))
        {
            return FixedTagSlugMap[fixedNameFromSlug];
        }

        if (!string.IsNullOrWhiteSpace(normalizedName) &&
            FixedTagSlugMap.TryGetValue(normalizedName, out var fixedSlug))
        {
            return fixedSlug;
        }

        if (!string.IsNullOrWhiteSpace(normalizedPreferred))
        {
            return BuildSlugCore(normalizedPreferred);
        }

        return BuildSlugCore(normalizedName);
    }

    /// <summary>
    /// 是否为已经可公开使用的友好 ASCII slug。
    /// </summary>
    public static bool IsFriendlyAsciiCandidate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (ch > 127)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 尝试根据固定标签 slug 反查固定标签名称。
    /// </summary>
    public static bool TryGetFixedTagNameBySlug(string? slug, out string tagName)
    {
        var normalized = NormalizeInput(slug).ToLowerInvariant();
        return FixedSlugNameMap.TryGetValue(normalized, out tagName!);
    }

    private static string BuildSlugCore(string? value)
    {
        var normalized = NormalizeInput(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "tag";
        }

        var builder = new StringBuilder(normalized.Length * 2);
        var pendingDash = false;

        foreach (var ch in normalized)
        {
            if (ch <= 127 && char.IsLetterOrDigit(ch))
            {
                if (pendingDash && builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(ch));
                pendingDash = false;
                continue;
            }

            if (IsSlugSeparator(ch))
            {
                pendingDash = builder.Length > 0;
                continue;
            }

            if (pendingDash && builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }

            builder.Append('u');
            builder.Append(((int)ch).ToString("x4"));
            pendingDash = true;
        }

        var slug = builder
            .ToString()
            .Trim('-');

        if (slug.Length == 0)
        {
            slug = "tag";
        }

        if (slug.Length > MaxSlugLength)
        {
            slug = slug[..MaxSlugLength].Trim('-');
        }

        return slug.Length == 0 ? "tag" : slug;
    }

    private static string NormalizeInput(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static bool IsSlugSeparator(char ch)
    {
        return char.IsWhiteSpace(ch) || ch is '-' or '_' or '/' or '\\' or '.' or ',' or ':';
    }
}
