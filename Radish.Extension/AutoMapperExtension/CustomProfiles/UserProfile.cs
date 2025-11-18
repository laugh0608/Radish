using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>用户实体和视图模型对象映射配置</summary>
public class UserProfile : Profile
{
    public UserProfile()
    {
        // CreateMap<User, UserVo>();
        CreateMap<User, UserVo>().ForMember(a => 
            a.VoUsName, o => 
            o.MapFrom(d => d.UserName));
        CreateMap<UserVo, User>().ForMember(a => 
            a.UserName, o => 
            o.MapFrom(d => d.VoUsName));
    }
}