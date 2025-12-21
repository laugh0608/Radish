using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>附件实体映射配置</summary>
public class AttachmentProfile : Profile
{
    public AttachmentProfile()
    {
        // Attachment -> AttachmentVo
        RecognizeDestinationPrefixes("Vo");
        CreateMap<Attachment, AttachmentVo>()
            .ForMember(dest => dest.FileSizeFormatted, opt => opt.MapFrom(src => FormatFileSize(src.FileSize)))
            .ForMember(dest => dest.ThumbnailUrl, opt => opt.MapFrom(src =>
                string.IsNullOrWhiteSpace(src.ThumbnailPath)
                    ? null
                    : $"/uploads/{src.ThumbnailPath}"));

        // AttachmentVo -> Attachment
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
