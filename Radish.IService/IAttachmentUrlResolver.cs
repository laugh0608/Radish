using Radish.Model;

namespace Radish.IService;

/// <summary>附件公开访问地址解析器</summary>
public interface IAttachmentUrlResolver
{
    /// <summary>解析附件原图地址</summary>
    string ResolveAttachmentUrl(long attachmentId);

    /// <summary>解析附件指定变体地址</summary>
    string ResolveAttachmentUrl(long attachmentId, AttachmentUrlVariant variant);
}
