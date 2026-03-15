using System.Text.RegularExpressions;
using Radish.Model;

namespace Radish.Service;

internal static partial class AttachmentReferenceHelper
{
    [GeneratedRegex(@"!\[[^\]]*\]\((?<url>[^)\s]+)\)|\[[^\]]+\]\((?<url>[^)\s]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownLinkRegex();

    public static HashSet<string> ExtractUploadUrls(string? content)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(content))
        {
            return urls;
        }

        foreach (Match match in MarkdownLinkRegex().Matches(content))
        {
            if (!match.Success)
            {
                continue;
            }

            var rawUrl = match.Groups["url"].Value;
            AddNormalizedUrl(urls, rawUrl);

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

            var meta = fragment["radish:".Length..];
            foreach (var pair in meta.Split('&', StringSplitOptions.RemoveEmptyEntries))
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

                var value = Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
                AddNormalizedUrl(urls, value);
            }
        }

        return urls;
    }

    public static IEnumerable<string> GetAttachmentUrls(Attachment attachment)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddNormalizedUrl(urls, attachment.Url);
        AddNormalizedUrl(urls, attachment.ThumbnailPath is null ? null : $"/uploads/{attachment.ThumbnailPath}");
        return urls;
    }

    public static bool IsAttachmentReferenced(Attachment attachment, IReadOnlySet<string> referencedUrls)
    {
        return GetAttachmentUrls(attachment).Any(referencedUrls.Contains);
    }

    private static void AddNormalizedUrl(ISet<string> urls, string? rawUrl)
    {
        var normalized = NormalizeUploadUrl(rawUrl);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            urls.Add(normalized);
        }
    }

    private static string? NormalizeUploadUrl(string? rawUrl)
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

        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
        {
            value = absoluteUri.AbsolutePath;
        }

        if (value.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            value = $"/{value}";
        }

        return value.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            ? value
            : null;
    }
}
