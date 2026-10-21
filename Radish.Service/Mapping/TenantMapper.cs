using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.Tenants;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class TenantMapper
{
    // Tenant → TenantVo
    [MapProperty(nameof(Tenant.TenantName), nameof(TenantVo.VoTenantName))]
    [MapProperty(nameof(Tenant.TenantType), nameof(TenantVo.VoTenantType))]
    [MapProperty(nameof(Tenant.TenantConfigId), nameof(TenantVo.VoTenantConfigId))]
    [MapProperty(nameof(Tenant.TenantHost), nameof(TenantVo.VoTenantHost))]
    [MapProperty(nameof(Tenant.DbType), nameof(TenantVo.VoDbType))]
    [MapProperty(nameof(Tenant.DbConnectionStr), nameof(TenantVo.VoDbConnectionStr))]
    [MapProperty(nameof(Tenant.IsEnable), nameof(TenantVo.VoIsEnable))]
    [MapProperty(nameof(Tenant.TenantRemark), nameof(TenantVo.VoTenantRemark))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial TenantVo? ToTenantVo(Tenant? source);

    public static partial List<TenantVo> ToTenantVoList(IEnumerable<Tenant>? source);

    // BusinessTable → BusinessTableVo
    [MapProperty(nameof(BusinessTable.TenantId), nameof(BusinessTableVo.VoTenantId))]
    [MapProperty(nameof(BusinessTable.Name), nameof(BusinessTableVo.VoName))]
    [MapProperty(nameof(BusinessTable.Amount), nameof(BusinessTableVo.VoAmount))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial BusinessTableVo? ToBusinessTableVo(BusinessTable? source);

    public static partial List<BusinessTableVo> ToBusinessTableVoList(IEnumerable<BusinessTable>? source);

    // MultiBusinessTable → MultiBusinessTableVo
    [MapProperty(nameof(MultiBusinessTable.Name), nameof(MultiBusinessTableVo.VoName))]
    [MapProperty(nameof(MultiBusinessTable.Amount), nameof(MultiBusinessTableVo.VoAmount))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial MultiBusinessTableVo? ToMultiBusinessTableVo(MultiBusinessTable? source);

    public static partial List<MultiBusinessTableVo> ToMultiBusinessTableVoList(IEnumerable<MultiBusinessTable>? source);

    // SubLibBusinessTable → SubLibBusinessTableVo
    [MapProperty(nameof(SubLibBusinessTable.Name), nameof(SubLibBusinessTableVo.VoName))]
    [MapProperty(nameof(SubLibBusinessTable.Amount), nameof(SubLibBusinessTableVo.VoAmount))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial SubLibBusinessTableVo? ToSubLibBusinessTableVo(SubLibBusinessTable? source);

    public static partial List<SubLibBusinessTableVo> ToSubLibBusinessTableVoList(IEnumerable<SubLibBusinessTable>? source);

    // UserRole → UserRoleVo
    [MapProperty(nameof(UserRole.UserId), nameof(UserRoleVo.VoUserId))]
    [MapProperty(nameof(UserRole.RoleId), nameof(UserRoleVo.VoRoleId))]
    [MapProperty(nameof(UserRole.IsDeleted), nameof(UserRoleVo.VoIsDeleted))]
    [MapProperty(nameof(UserRole.CreateId), nameof(UserRoleVo.VoCreateId))]
    [MapProperty(nameof(UserRole.CreateBy), nameof(UserRoleVo.VoCreateBy))]
    [MapProperty(nameof(UserRole.CreateTime), nameof(UserRoleVo.VoCreateTime))]
    [MapProperty(nameof(UserRole.ModifyId), nameof(UserRoleVo.VoModifyId))]
    [MapProperty(nameof(UserRole.ModifyBy), nameof(UserRoleVo.VoModifyBy))]
    [MapProperty(nameof(UserRole.ModifyTime), nameof(UserRoleVo.VoModifyTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserRoleVo? ToUserRoleVo(UserRole? source);

    public static partial List<UserRoleVo> ToUserRoleVoList(IEnumerable<UserRole>? source);

    // BusinessTableVo → BusinessTable
    [MapProperty(nameof(BusinessTableVo.VoTenantId), nameof(BusinessTable.TenantId))]
    [MapProperty(nameof(BusinessTableVo.VoName), nameof(BusinessTable.Name))]
    [MapProperty(nameof(BusinessTableVo.VoAmount), nameof(BusinessTable.Amount))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial BusinessTable? ToBusinessTable(BusinessTableVo? source);

    public static partial List<BusinessTable> ToBusinessTableList(IEnumerable<BusinessTableVo>? source);

    // MultiBusinessTableVo → MultiBusinessTable
    [MapProperty(nameof(MultiBusinessTableVo.VoName), nameof(MultiBusinessTable.Name))]
    [MapProperty(nameof(MultiBusinessTableVo.VoAmount), nameof(MultiBusinessTable.Amount))]
    [MapperIgnoreTarget(nameof(MultiBusinessTable.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial MultiBusinessTable? ToMultiBusinessTable(MultiBusinessTableVo? source);

    public static partial List<MultiBusinessTable> ToMultiBusinessTableList(IEnumerable<MultiBusinessTableVo>? source);

    // SubLibBusinessTableVo → SubLibBusinessTable
    [MapProperty(nameof(SubLibBusinessTableVo.VoName), nameof(SubLibBusinessTable.Name))]
    [MapProperty(nameof(SubLibBusinessTableVo.VoAmount), nameof(SubLibBusinessTable.Amount))]
    [MapperIgnoreTarget(nameof(SubLibBusinessTable.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial SubLibBusinessTable? ToSubLibBusinessTable(SubLibBusinessTableVo? source);

    public static partial List<SubLibBusinessTable> ToSubLibBusinessTableList(IEnumerable<SubLibBusinessTableVo>? source);

    // TenantVo → Tenant
    [MapProperty(nameof(TenantVo.VoTenantName), nameof(Tenant.TenantName))]
    [MapProperty(nameof(TenantVo.VoTenantType), nameof(Tenant.TenantType))]
    [MapProperty(nameof(TenantVo.VoTenantConfigId), nameof(Tenant.TenantConfigId))]
    [MapProperty(nameof(TenantVo.VoTenantHost), nameof(Tenant.TenantHost))]
    [MapProperty(nameof(TenantVo.VoDbType), nameof(Tenant.DbType))]
    [MapProperty(nameof(TenantVo.VoDbConnectionStr), nameof(Tenant.DbConnectionStr))]
    [MapProperty(nameof(TenantVo.VoIsEnable), nameof(Tenant.IsEnable))]
    [MapProperty(nameof(TenantVo.VoTenantRemark), nameof(Tenant.TenantRemark))]
    [MapperIgnoreTarget(nameof(Tenant.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial Tenant? ToTenant(TenantVo? source);

    [ObjectFactory]
    private static Tenant CreateTenant(TenantVo source) => new(source.VoTenantName, source.VoTenantType);

    public static partial List<Tenant> ToTenantList(IEnumerable<TenantVo>? source);

    // UserRoleVo → UserRole
    [MapProperty(nameof(UserRoleVo.VoUserId), nameof(UserRole.UserId))]
    [MapProperty(nameof(UserRoleVo.VoRoleId), nameof(UserRole.RoleId))]
    [MapProperty(nameof(UserRoleVo.VoIsDeleted), nameof(UserRole.IsDeleted))]
    [MapProperty(nameof(UserRoleVo.VoCreateId), nameof(UserRole.CreateId))]
    [MapProperty(nameof(UserRoleVo.VoCreateBy), nameof(UserRole.CreateBy))]
    [MapProperty(nameof(UserRoleVo.VoCreateTime), nameof(UserRole.CreateTime))]
    [MapProperty(nameof(UserRoleVo.VoModifyId), nameof(UserRole.ModifyId))]
    [MapProperty(nameof(UserRoleVo.VoModifyBy), nameof(UserRole.ModifyBy))]
    [MapProperty(nameof(UserRoleVo.VoModifyTime), nameof(UserRole.ModifyTime))]
    [MapperIgnoreTarget(nameof(UserRole.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserRole? ToUserRole(UserRoleVo? source);

    public static partial List<UserRole> ToUserRoleList(IEnumerable<UserRoleVo>? source);

}
