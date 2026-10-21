using System.Diagnostics.CodeAnalysis;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal sealed partial class AttachmentMapper
{
    private readonly IAttachmentUrlResolver _urlResolver;

    public AttachmentMapper(IAttachmentUrlResolver urlResolver)
    {
        _urlResolver = urlResolver;
    }

    // Attachment → AttachmentVo
    [UserMapping(Default = true)]
    [return: NotNullIfNotNull(nameof(source))]
    public AttachmentVo? ToAttachmentVo(Attachment? source)
    {
        if (source is null) return null;
        var target = MapAttachmentToAttachmentVo(source);
        target.VoFileSizeFormatted = FormatFileSize(source.FileSize);
        target.VoUrl = _urlResolver.ResolveAttachmentUrl(source.Id);
        target.VoThumbnailUrl = _urlResolver.ResolveAttachmentUrl(source.Id, AttachmentUrlVariant.Thumbnail);
        return target;
    }

    [MapProperty(nameof(Attachment.Id), nameof(AttachmentVo.VoId))]
    [MapProperty(nameof(Attachment.OriginalName), nameof(AttachmentVo.VoOriginalName))]
    [MapProperty(nameof(Attachment.Extension), nameof(AttachmentVo.VoExtension))]
    [MapProperty(nameof(Attachment.FileSize), nameof(AttachmentVo.VoFileSize))]
    [MapperIgnoreTarget(nameof(AttachmentVo.VoFileSizeFormatted))]
    [MapProperty(nameof(Attachment.MimeType), nameof(AttachmentVo.VoMimeType))]
    [MapProperty(nameof(Attachment.StorageType), nameof(AttachmentVo.VoStorageType))]
    [MapperIgnoreTarget(nameof(AttachmentVo.VoUrl))]
    [MapperIgnoreTarget(nameof(AttachmentVo.VoThumbnailUrl))]
    [MapProperty(nameof(Attachment.UploaderId), nameof(AttachmentVo.VoUploaderId))]
    [MapProperty(nameof(Attachment.UploaderName), nameof(AttachmentVo.VoUploaderName))]
    [MapProperty(nameof(Attachment.BusinessType), nameof(AttachmentVo.VoBusinessType))]
    [MapProperty(nameof(Attachment.BusinessId), nameof(AttachmentVo.VoBusinessId))]
    [MapProperty(nameof(Attachment.IsPublic), nameof(AttachmentVo.VoIsPublic))]
    [MapProperty(nameof(Attachment.DownloadCount), nameof(AttachmentVo.VoDownloadCount))]
    [MapProperty(nameof(Attachment.AuditStatus), nameof(AttachmentVo.VoAuditStatus))]
    [MapProperty(nameof(Attachment.Remark), nameof(AttachmentVo.VoRemark))]
    [MapProperty(nameof(Attachment.CreateTime), nameof(AttachmentVo.VoCreateTime))]
    [UserMapping(Default = false)]
    private partial AttachmentVo MapAttachmentToAttachmentVo(Attachment source);

    public partial List<AttachmentVo> ToAttachmentVoList(IEnumerable<Attachment>? source);

    // AttachmentVo → Attachment
    [MapProperty(nameof(AttachmentVo.VoOriginalName), nameof(Attachment.OriginalName))]
    [MapperIgnoreTarget(nameof(Attachment.StoredName))]
    [MapProperty(nameof(AttachmentVo.VoExtension), nameof(Attachment.Extension))]
    [MapProperty(nameof(AttachmentVo.VoFileSize), nameof(Attachment.FileSize))]
    [MapProperty(nameof(AttachmentVo.VoMimeType), nameof(Attachment.MimeType))]
    [MapperIgnoreTarget(nameof(Attachment.FileHash))]
    [MapProperty(nameof(AttachmentVo.VoStorageType), nameof(Attachment.StorageType))]
    [MapperIgnoreTarget(nameof(Attachment.StoragePath))]
    [MapperIgnoreTarget(nameof(Attachment.ThumbnailPath))]
    [MapperIgnoreTarget(nameof(Attachment.SmallPath))]
    [MapperIgnoreTarget(nameof(Attachment.MediumPath))]
    [MapperIgnoreTarget(nameof(Attachment.LargePath))]
    [MapProperty(nameof(AttachmentVo.VoUploaderId), nameof(Attachment.UploaderId))]
    [MapProperty(nameof(AttachmentVo.VoUploaderName), nameof(Attachment.UploaderName))]
    [MapProperty(nameof(AttachmentVo.VoBusinessType), nameof(Attachment.BusinessType))]
    [MapProperty(nameof(AttachmentVo.VoBusinessId), nameof(Attachment.BusinessId))]
    [MapProperty(nameof(AttachmentVo.VoIsPublic), nameof(Attachment.IsPublic))]
    [MapProperty(nameof(AttachmentVo.VoDownloadCount), nameof(Attachment.DownloadCount))]
    [MapProperty(nameof(AttachmentVo.VoAuditStatus), nameof(Attachment.AuditStatus))]
    [MapperIgnoreTarget(nameof(Attachment.AuditResult))]
    [MapProperty(nameof(AttachmentVo.VoRemark), nameof(Attachment.Remark))]
    [MapperIgnoreTarget(nameof(Attachment.IsEnabled))]
    [MapperIgnoreTarget(nameof(Attachment.IsDeleted))]
    [MapperIgnoreTarget(nameof(Attachment.TenantId))]
    [MapProperty(nameof(AttachmentVo.VoCreateTime), nameof(Attachment.CreateTime))]
    [MapperIgnoreTarget(nameof(Attachment.CreateBy))]
    [MapperIgnoreTarget(nameof(Attachment.CreateId))]
    [MapperIgnoreTarget(nameof(Attachment.ModifyTime))]
    [MapperIgnoreTarget(nameof(Attachment.ModifyBy))]
    [MapperIgnoreTarget(nameof(Attachment.ModifyId))]
    [MapProperty(nameof(AttachmentVo.VoId), nameof(Attachment.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public partial Attachment? ToAttachment(AttachmentVo? source);

    public partial List<Attachment> ToAttachmentList(IEnumerable<AttachmentVo>? source);

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

}
