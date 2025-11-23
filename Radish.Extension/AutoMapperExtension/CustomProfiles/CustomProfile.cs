using AutoMapper;
using Radish.Model;
using Radish.Model.Tenants;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

/// <summary>自定义关系映射配置</summary>
public class CustomProfile : Profile
{
    public CustomProfile() // 自动识别字段前缀 Vo
    {
        // UserRole
        RecognizeDestinationPrefixes("Vo"); // UserRole -> UserRoleVo
        CreateMap<UserRole, UserRoleVo>();
        RecognizePrefixes("Vo"); // UserRoleVo -> UserRole
        CreateMap<UserRoleVo, UserRole>();
        // BusinessTable
        RecognizeDestinationPrefixes("Vo"); // BusinessTable -> BusinessTableVo
        CreateMap<BusinessTable, BusinessTableVo>();
        RecognizePrefixes("Vo"); // BusinessTableVo -> BusinessTable
        CreateMap<BusinessTableVo, BusinessTable>();
        // MultiBusinessTable
        RecognizeDestinationPrefixes("Vo"); // MultiBusinessTable -> MultiBusinessTableVo
        CreateMap<MultiBusinessTable, MultiBusinessTableVo>();
        RecognizePrefixes("Vo"); // MultiBusinessTableVo -> MultiBusinessTable
        CreateMap<MultiBusinessTableVo, MultiBusinessTable>();
        // Tenant
        RecognizeDestinationPrefixes("Vo"); // Tenant -> TenantVo
        CreateMap<Tenant, TenantVo>();
        RecognizePrefixes("Vo"); // TenantVo -> Tenant
        CreateMap<TenantVo, Tenant>();
        // SubLibBusinessTable
        RecognizeDestinationPrefixes("Vo"); // SubLibBusinessTable -> SubLibBusinessTableVo
        CreateMap<SubLibBusinessTable, SubLibBusinessTableVo>();
        RecognizePrefixes("Vo"); // SubLibBusinessTableVo -> SubLibBusinessTable
        CreateMap<SubLibBusinessTableVo, SubLibBusinessTable>();
    }
}