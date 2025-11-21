using AutoMapper;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>角色实体和视图模型对象映射配置</summary>
public class RoleProfile : Profile
{
    public RoleProfile()
    {
        // 自动识别字段前缀 Vo
        RecognizeDestinationPrefixes("Vo"); // Role -> RoleVo
        CreateMap<Role, RoleVo>();
        RecognizePrefixes("Vo"); // RoleVo -> Role
        CreateMap<RoleVo, Role>();
    }
}