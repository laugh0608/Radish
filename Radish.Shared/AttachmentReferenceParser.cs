using System.Text.RegularExpressions;

namespace Radish.Shared;

/// <summary>解析正文中的稳定附件引用。</summary>
public static partial class AttachmentReferenceParser
{
    private const string AttachmentAssetPathPrefix = "/_assets/attachments/";

    [GeneratedRegex(@"!\[[^\]]*\]\((?<url>[^)\s]+)\)|\[[^\]]+\]\((?<url>[^)\s]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(
        @"(?ms)^[ \t]*`{3,}[^\r\n]*(?:\r?\n|$).*?^[ \t]*`{3,}[ \t]*(?:\r?\n|$)")]
    private static partial Regex BacktickFencedCodeBlockRegex();

    [GeneratedRegex(
        @"(?ms)^[ \t]*~{3,}[^\r\n]*(?:\r?\n|$).*?^[ \t]*~{3,}[ \t]*(?:\r?\n|$)")]
    private static partial Regex TildeFencedCodeBlockRegex();

    [GeneratedRegex(@"(?<!`)`{1,2}[^`\r\n]*`{1,2}(?!`)")]
    private static partial Regex InlineCodeRegex();

    public static HashSet<long> ExtractAttachmentIds(string? content)
    {
        var attachmentIds = new HashSet<long>();
        foreach (Match match in EnumerateRenderableMarkdownLinks(content))
        {
            var rawUrl = match.Groups["url"].Value;
            AddAttachmentId(attachmentIds, rawUrl);

            var fragmentIndex = rawUrl.IndexOf('#');
            if (fragmentIndex < 0 || fragmentIndex >= rawUrl.Length - 1)
            {
                continue;
            }

            var fragment = rawUrl[(fragmentIndex + 1)..];
            if (!fragment.StartsWith("radish:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var pair in fragment["radish:".Length..]
                         .Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separatorIndex = pair.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex >= pair.Length - 1)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(pair[..separatorIndex]);
                if (!key.Equals("full", StringComparison.OrdinalIgnoreCase) &&
                    !key.Equals("thumbnail", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AddAttachmentId(
                    attachmentIds,
                    Uri.UnescapeDataString(pair[(separatorIndex + 1)..]));
            }
        }

        return attachmentIds;
    }

    public static HashSet<string> ExtractLegacyAttachmentUrls(string? content)
    {
        var legacyUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in EnumerateRenderableMarkdownLinks(content))
        {
            var rawUrl = match.Groups["url"].Value.Trim();
            var path = rawUrl;
            if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var absoluteUri) &&
                (absoluteUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                 absoluteUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                path = absoluteUri.AbsolutePath;
            }

            if (path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("/uploads/DefaultIco/", StringComparison.OrdinalIgnoreCase))
            {
                legacyUrls.Add(rawUrl);
            }
        }

        return legacyUrls;
    }

    public static long? ExtractAttachmentIdFromAssetUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return null;
        }

        var value = rawUrl.Trim();
        string path;
        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             absoluteUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            path = absoluteUri.AbsolutePath;
        }
        else
        {
            var fragmentIndex = value.IndexOf('#');
            if (fragmentIndex >= 0)
            {
                value = value[..fragmentIndex];
            }

            var queryIndex = value.IndexOf('?');
            path = queryIndex >= 0 ? value[..queryIndex] : value;
        }

        if (!path.StartsWith(AttachmentAssetPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var idSegment = path[AttachmentAssetPathPrefix.Length..].TrimEnd('/');
        if (idSegment.EndsWith("/thumbnail", StringComparison.OrdinalIgnoreCase))
        {
            idSegment = idSegment[..^"/thumbnail".Length];
        }

        return long.TryParse(idSegment, out var attachmentId) && attachmentId > 0
            ? attachmentId
            : null;
    }

    private static void AddAttachmentId(ISet<long> attachmentIds, string? rawUrl)
    {
        var attachmentId = ParseAttachmentId(rawUrl);
        if (attachmentId.HasValue && attachmentId.Value > 0)
        {
            attachmentIds.Add(attachmentId.Value);
        }
    }

    private static MatchCollection EnumerateRenderableMarkdownLinks(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return MarkdownLinkRegex().Matches(string.Empty);
        }

        var renderableContent = BacktickFencedCodeBlockRegex().Replace(content, string.Empty);
        renderableContent = TildeFencedCodeBlockRegex().Replace(renderableContent, string.Empty);
        renderableContent = InlineCodeRegex().Replace(renderableContent, string.Empty);
        return MarkdownLinkRegex().Matches(renderableContent);
    }

    private static long? ParseAttachmentId(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return null;
        }

        var value = rawUrl.Trim();
        var fragmentIndex = value.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            value = value[..fragmentIndex];
        }

        var queryIndex = value.IndexOf('?');
        if (queryIndex >= 0)
        {
            value = value[..queryIndex];
        }

        if (!value.StartsWith("attachment://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var idSegment = value["attachment://".Length..].Trim('/');
        return long.TryParse(idSegment, out var attachmentId) ? attachmentId : null;
    }
}
