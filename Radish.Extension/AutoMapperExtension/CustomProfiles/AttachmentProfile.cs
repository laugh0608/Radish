using AutoMapper;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>附件实体映射配置</summary>
public class AttachmentProfile : Profile
{
    public AttachmentProfile()
    {
        // Attachment -> AttachmentVo (使用前缀识别 + 手动配置特殊字段)
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Attachment, AttachmentVo>()
            .ForMember(dest => dest.VoFileSizeFormatted, opt => opt.MapFrom(src => FormatFileSize(src.FileSize)))
            .ForMember(dest => dest.VoUrl, opt => opt.MapFrom<AttachmentVoUrlResolver>())
            .ForMember(dest => dest.VoThumbnailUrl, opt => opt.MapFrom<AttachmentVoThumbnailUrlResolver>());

        // AttachmentVo -> Attachment (使用前缀识别)
        RecognizePrefixes("Vo");
        CreateMap<AttachmentVo, Attachment>();
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    /// <param name="bytes">文件大小（字节）</param>
    /// <returns>格式化后的文件大小（如 1.5MB）</returns>
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

internal sealed class AttachmentVoUrlResolver : IValueResolver<Attachment, AttachmentVo, string>
{
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;

    public AttachmentVoUrlResolver(IAttachmentUrlResolver attachmentUrlResolver)
    {
        _attachmentUrlResolver = attachmentUrlResolver;
    }

    public string Resolve(Attachment source, AttachmentVo destination, string destMember, ResolutionContext context)
    {
        return _attachmentUrlResolver.ResolveAttachmentUrl(source.Id);
    }
}

internal sealed class AttachmentVoThumbnailUrlResolver : IValueResolver<Attachment, AttachmentVo, string?>
{
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;

    public AttachmentVoThumbnailUrlResolver(IAttachmentUrlResolver attachmentUrlResolver)
    {
        _attachmentUrlResolver = attachmentUrlResolver;
    }

    public string? Resolve(Attachment source, AttachmentVo destination, string? destMember, ResolutionContext context)
    {
        return _attachmentUrlResolver.ResolveAttachmentUrl(source.Id, AttachmentUrlVariant.Thumbnail);
    }
}
