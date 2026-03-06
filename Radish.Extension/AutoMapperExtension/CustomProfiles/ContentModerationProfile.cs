using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>内容治理映射配置</summary>
public class ContentModerationProfile : Profile
{
    public ContentModerationProfile()
    {
        RecognizeDestinationPrefixes("Vo");
        CreateMap<ContentReport, ContentReportVo>();
        CreateMap<UserModerationAction, UserModerationActionVo>()
            .ForMember(dest => dest.VoActionId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.VoActionType, opt => opt.Ignore());
    }
}
