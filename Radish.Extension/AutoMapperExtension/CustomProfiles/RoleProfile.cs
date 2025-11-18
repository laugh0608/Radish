using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>角色实体和视图模型对象映射配置</summary>
public class RoleProfile : Profile
{
    public RoleProfile()
    {
        CreateMap<Role, RoleVo>().ForMember(a => 
            a.VoRoName, o => 
            o.MapFrom(d => d.RoleName));
        CreateMap<RoleVo, Role>().ForMember(a => 
            a.RoleName, o => 
            o.MapFrom(d => d.VoRoName));
    }
}