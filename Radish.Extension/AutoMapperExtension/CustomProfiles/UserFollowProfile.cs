using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>用户关系链映射配置</summary>
public class UserFollowProfile : Profile
{
    public UserFollowProfile()
    {
        RecognizeDestinationPrefixes("Vo");
        CreateMap<UserFollow, UserFollowVo>();
    }
}
