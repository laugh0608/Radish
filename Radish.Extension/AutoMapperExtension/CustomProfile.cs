using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension;

// public class CustomProfile : Profile
// {
//     public CustomProfile()
//     {
//         // RecognizePrefixes("Vo");
//         // CreateMap<Role, RoleVo>();
//         // Use CreateMap... Etc. here (Profile methods are the same as configuration methods)
//     }
// }

/// <summary>角色实体和视图模型对象映射配置</summary>
public class RoleProfile : Profile
{
    public RoleProfile()
    {
        // 识别前缀
        // RecognizePrefixes("Vo");
        // CreateMap<Role, RoleVo>();
        // 自定义映射
        CreateMap<Role, RoleVo>().ForMember(a => 
            a.VoRoName, o => 
            o.MapFrom(d => d.RoleName));
        CreateMap<RoleVo, Role>().ForMember(a => 
            a.RoleName, o => 
            o.MapFrom(d => d.VoRoName));
    }
}

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