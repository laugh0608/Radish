using System.Text.RegularExpressions;
using Radish.Model;

namespace Radish.Service;

internal static partial class AttachmentReferenceHelper
{
    [GeneratedRegex(@"!\[[^\]]*\]\((?<url>[^)\s]+)\)|\[[^\]]+\]\((?<url>[^)\s]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownLinkRegex();

    public static HashSet<long> ExtractAttachmentIds(string? content)
    {
        var attachmentIds = new HashSet<long>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return attachmentIds;
        }

        foreach (Match match in MarkdownLinkRegex().Matches(content))
        {
            if (!match.Success)
            {
                continue;
            }

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
                AddAttachmentId(attachmentIds, value);
            }
        }

        return attachmentIds;
    }

    public static IEnumerable<long> GetAttachmentIds(Attachment attachment)
    {
        return attachment.Id > 0 ? new[] { attachment.Id } : Array.Empty<long>();
    }

    public static bool IsAttachmentReferenced(Attachment attachment, IReadOnlySet<long> referencedAttachmentIds)
    {
        return attachment.Id > 0 && referencedAttachmentIds.Contains(attachment.Id);
    }

    private static void AddAttachmentId(ISet<long> attachmentIds, string? rawUrl)
    {
        var attachmentId = ParseAttachmentId(rawUrl);
        if (attachmentId.HasValue && attachmentId.Value > 0)
        {
            attachmentIds.Add(attachmentId.Value);
        }
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
