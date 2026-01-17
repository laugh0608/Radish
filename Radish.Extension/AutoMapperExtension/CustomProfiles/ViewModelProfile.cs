using AutoMapper;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>新增ViewModel映射配置</summary>
public class ViewModelProfile : Profile
{
    public ViewModelProfile()
    {
        // 注意：VoUserStats, VoLikeResult, VoUnreadMessageCount 等通常由Service层手动构造
        // 这里暂时不添加自动映射，因为它们通常是聚合数据或计算结果

        // 如果后续需要从特定实体映射，可以在这里添加配置
        // 例如：
        // CreateMap<SomeEntity, VoUserStats>()
        //     .ForMember(dest => dest.PostCount, opt => opt.MapFrom(src => src.Posts.Count));
    }
}