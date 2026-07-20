using System.Text;
using System.Text.RegularExpressions;

namespace Radish.Model;

/// <summary>生成 Chat 写入、迁移与检索共用的稳定可见搜索文本。</summary>
public static partial class ChatMessageSearchTextNormalizer
{
    public const int MaximumLength = 4000;

    [GeneratedRegex(@"@\[(?<name>[^\]\r\n]+)\]\(\d+\)", RegexOptions.CultureInvariant)]
    private static partial Regex MentionPattern();

    [GeneratedRegex(@"(?i)\b(?:sticker|attachment|file|image)://\S+", RegexOptions.CultureInvariant)]
    private static partial Regex InternalResourcePattern();

    /// <summary>从原消息正文提取不含内部用户 ID 或资源定位协议的可见纯文本。</summary>
    public static string? ToVisibleText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var withVisibleMentions = MentionPattern().Replace(content, match => $"@{match.Groups["name"].Value}");
        var withoutInternalResources = InternalResourcePattern().Replace(withVisibleMentions, " ");
        var builder = new StringBuilder(Math.Min(withoutInternalResources.Length, MaximumLength));
        var pendingWhitespace = false;

        foreach (var character in withoutInternalResources)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingWhitespace = builder.Length > 0;
                continue;
            }

            if (char.IsControl(character))
            {
                continue;
            }

            if (pendingWhitespace && builder.Length < MaximumLength)
            {
                builder.Append(' ');
            }

            pendingWhitespace = false;
            if (builder.Length >= MaximumLength)
            {
                break;
            }

            builder.Append(character);
        }

        if (builder.Length == 0)
        {
            return null;
        }

        if (char.IsHighSurrogate(builder[^1]))
        {
            builder.Length--;
        }

        var normalized = builder.ToString().Normalize(NormalizationForm.FormC);
        if (normalized.Length <= MaximumLength)
        {
            return normalized;
        }

        var safeLength = MaximumLength;
        if (char.IsHighSurrogate(normalized[safeLength - 1]))
        {
            safeLength--;
        }

        return normalized[..safeLength];
    }

    /// <summary>生成持久化及查询使用的 invariant 小写字面文本。</summary>
    public static string? Normalize(string? content)
    {
        return ToVisibleText(content)?.ToLowerInvariant();
    }
}
