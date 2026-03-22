using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>Wiki 实体与视图模型映射</summary>
public class WikiProfile : Profile
{
    private const char AccessListDelimiter = '|';

    public WikiProfile()
    {
        RecognizeDestinationPrefixes("Vo");
        CreateMap<WikiDocument, WikiDocumentVo>()
            .ForMember(dest => dest.VoVisibility, opt => opt.MapFrom(src => NormalizeVisibility(src.Visibility)))
            .ForMember(dest => dest.VoAllowedRoles, opt => opt.MapFrom(src => ParseAccessList(src.AllowedRoles)))
            .ForMember(dest => dest.VoAllowedPermissions, opt => opt.MapFrom(src => ParseAccessList(src.AllowedPermissions)));
        CreateMap<WikiDocument, WikiDocumentDetailVo>()
            .ForMember(dest => dest.VoVisibility, opt => opt.MapFrom(src => NormalizeVisibility(src.Visibility)))
            .ForMember(dest => dest.VoAllowedRoles, opt => opt.MapFrom(src => ParseAccessList(src.AllowedRoles)))
            .ForMember(dest => dest.VoAllowedPermissions, opt => opt.MapFrom(src => ParseAccessList(src.AllowedPermissions)));
        CreateMap<WikiDocument, WikiDocumentTreeNodeVo>()
            .ForMember(dest => dest.VoVisibility, opt => opt.MapFrom(src => NormalizeVisibility(src.Visibility)))
            .ForMember(dest => dest.VoChildren, opt => opt.Ignore());

        RecognizePrefixes("Vo");
        CreateMap<WikiDocumentVo, WikiDocument>()
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.Visibility, opt => opt.MapFrom(src => NormalizeVisibility(src.VoVisibility)))
            .ForMember(dest => dest.AllowedRoles, opt => opt.MapFrom(src => SerializeAccessList(src.VoAllowedRoles)))
            .ForMember(dest => dest.AllowedPermissions, opt => opt.MapFrom(src => SerializeAccessList(src.VoAllowedPermissions)));
    }

    private static int NormalizeVisibility(int visibility)
    {
        return visibility is >= (int)WikiDocumentVisibilityEnum.Public and <= (int)WikiDocumentVisibilityEnum.Restricted
            ? visibility
            : (int)WikiDocumentVisibilityEnum.Authenticated;
    }

    private static List<string> ParseAccessList(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        return rawValue
            .Split(AccessListDelimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? SerializeAccessList(IEnumerable<string>? values)
    {
        if (values == null)
        {
            return null;
        }

        var normalized = values
            .Select(value => value?.Trim().ToLowerInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count == 0
            ? null
            : $"{AccessListDelimiter}{string.Join(AccessListDelimiter, normalized)}{AccessListDelimiter}";
    }
}
