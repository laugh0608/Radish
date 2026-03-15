using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>
/// Console 授权模型映射
/// </summary>
public class ConsoleAuthorizationProfile : Profile
{
    public ConsoleAuthorizationProfile()
    {
        RecognizeDestinationPrefixes("Vo");
        CreateMap<ConsoleResource, ConsoleResourceVo>();

        RecognizePrefixes("Vo");
        CreateMap<ConsoleResourceVo, ConsoleResource>();
    }
}
