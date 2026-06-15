using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>电子宠物映射配置</summary>
public class PetMappingProfile : Profile
{
    public PetMappingProfile()
    {
        RecognizeDestinationPrefixes("Vo");

        CreateMap<PetProfile, PetProfileVo>()
            .ForMember(dest => dest.VoCareActions, opt => opt.Ignore());

        CreateMap<PetStatLog, PetStatLogVo>();
    }
}
