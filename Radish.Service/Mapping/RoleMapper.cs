using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class RoleMapper
{
    // Role → RoleVo
    [MapProperty(nameof(Role.Id), nameof(RoleVo.VoId))]
    [MapProperty(nameof(Role.RoleName), nameof(RoleVo.VoRoleName))]
    [MapProperty(nameof(Role.RoleDescription), nameof(RoleVo.VoRoleDescription))]
    [MapProperty(nameof(Role.OrderSort), nameof(RoleVo.VoOrderSort))]
    [MapProperty(nameof(Role.DepartmentIds), nameof(RoleVo.VoDepartmentIds))]
    [MapProperty(nameof(Role.AuthorityScope), nameof(RoleVo.VoAuthorityScope))]
    [MapProperty(nameof(Role.IsEnabled), nameof(RoleVo.VoIsEnabled))]
    [MapperIgnoreTarget(nameof(RoleVo.VoIsBuiltIn))]
    [MapProperty(nameof(Role.IsDeleted), nameof(RoleVo.VoIsDeleted))]
    [MapProperty(nameof(Role.CreateId), nameof(RoleVo.VoCreateId))]
    [MapProperty(nameof(Role.CreateBy), nameof(RoleVo.VoCreateBy))]
    [MapProperty(nameof(Role.CreateTime), nameof(RoleVo.VoCreateTime))]
    [MapProperty(nameof(Role.ModifyId), nameof(RoleVo.VoModifyId))]
    [MapProperty(nameof(Role.ModifyBy), nameof(RoleVo.VoModifyBy))]
    [MapProperty(nameof(Role.ModifyTime), nameof(RoleVo.VoModifyTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial RoleVo? ToRoleVo(Role? source);

    public static partial List<RoleVo> ToRoleVoList(IEnumerable<Role>? source);

    // RoleVo → Role
    [MapProperty(nameof(RoleVo.VoIsDeleted), nameof(Role.IsDeleted))]
    [MapProperty(nameof(RoleVo.VoRoleName), nameof(Role.RoleName))]
    [MapProperty(nameof(RoleVo.VoRoleDescription), nameof(Role.RoleDescription))]
    [MapProperty(nameof(RoleVo.VoOrderSort), nameof(Role.OrderSort))]
    [MapProperty(nameof(RoleVo.VoDepartmentIds), nameof(Role.DepartmentIds))]
    [MapProperty(nameof(RoleVo.VoAuthorityScope), nameof(Role.AuthorityScope))]
    [MapProperty(nameof(RoleVo.VoIsEnabled), nameof(Role.IsEnabled))]
    [MapProperty(nameof(RoleVo.VoCreateId), nameof(Role.CreateId))]
    [MapProperty(nameof(RoleVo.VoCreateBy), nameof(Role.CreateBy))]
    [MapProperty(nameof(RoleVo.VoCreateTime), nameof(Role.CreateTime))]
    [MapProperty(nameof(RoleVo.VoModifyId), nameof(Role.ModifyId))]
    [MapProperty(nameof(RoleVo.VoModifyBy), nameof(Role.ModifyBy))]
    [MapProperty(nameof(RoleVo.VoModifyTime), nameof(Role.ModifyTime))]
    [MapProperty(nameof(RoleVo.VoId), nameof(Role.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial Role? ToRole(RoleVo? source);

    [ObjectFactory]
    private static Role CreateRole(RoleVo source) => new(source.VoRoleName);

    public static partial List<Role> ToRoleList(IEnumerable<RoleVo>? source);

}
