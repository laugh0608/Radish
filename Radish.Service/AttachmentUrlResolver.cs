using Radish.IService;
using Radish.Model;

namespace Radish.Service;

/// <summary>附件公开访问地址解析器</summary>
public class AttachmentUrlResolver : IAttachmentUrlResolver
{
    public string ResolveAttachmentUrl(long attachmentId)
    {
        return ResolveAttachmentUrl(attachmentId, AttachmentUrlVariant.Original);
    }

    public string ResolveAttachmentUrl(long attachmentId, AttachmentUrlVariant variant)
    {
        if (attachmentId <= 0)
        {
            return string.Empty;
        }

        return variant switch
        {
            AttachmentUrlVariant.Thumbnail => $"/_assets/attachments/{attachmentId}/thumbnail",
            _ => $"/_assets/attachments/{attachmentId}"
        };
    }
}
