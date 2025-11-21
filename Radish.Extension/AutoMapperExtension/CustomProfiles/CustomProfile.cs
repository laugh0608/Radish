using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>自定义关系映射配置</summary>
public class CustomProfile : Profile
{
    public CustomProfile()
    {
        // 识别前缀
        // RecognizePrefixes("Vo");
        // CreateMap<Role, RoleVo>();
        // Use CreateMap... Etc. here (Profile methods are the same as configuration methods)
        
        // 自动识别字段前缀 Vo
        RecognizeDestinationPrefixes("Vo"); // UserRole -> UserRoleVo
        CreateMap<UserRole, UserRoleVo>();
        RecognizePrefixes("Vo"); // UserRoleVo -> UserRole
        CreateMap<UserRoleVo, UserRole>();
    }
}