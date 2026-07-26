using Radish.Model;
using Radish.Shared;

namespace Radish.Service;

internal static class AttachmentReferenceHelper
{
    public static HashSet<long> ExtractAttachmentIds(string? content) =>
        AttachmentReferenceParser.ExtractAttachmentIds(content);

    public static IEnumerable<long> GetAttachmentIds(Attachment attachment)
    {
        return attachment.Id > 0 ? new[] { attachment.Id } : Array.Empty<long>();
    }

    public static bool IsAttachmentReferenced(Attachment attachment, IReadOnlySet<long> referencedAttachmentIds)
    {
        return attachment.Id > 0 && referencedAttachmentIds.Contains(attachment.Id);
    }

    public static long? ExtractAttachmentIdFromAssetUrl(string? rawUrl) =>
        AttachmentReferenceParser.ExtractAttachmentIdFromAssetUrl(rawUrl);
}
