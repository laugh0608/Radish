using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class ConsoleAuthorizationMapper
{
    // ConsoleResource → ConsoleResourceVo
    [MapProperty(nameof(ConsoleResource.Id), nameof(ConsoleResourceVo.VoId))]
    [MapProperty(nameof(ConsoleResource.ParentId), nameof(ConsoleResourceVo.VoParentId))]
    [MapProperty(nameof(ConsoleResource.ResourceKey), nameof(ConsoleResourceVo.VoResourceKey))]
    [MapProperty(nameof(ConsoleResource.ResourceName), nameof(ConsoleResourceVo.VoResourceName))]
    [MapProperty(nameof(ConsoleResource.ResourceType), nameof(ConsoleResourceVo.VoResourceType))]
    [MapProperty(nameof(ConsoleResource.ModuleKey), nameof(ConsoleResourceVo.VoModuleKey))]
    [MapProperty(nameof(ConsoleResource.RoutePath), nameof(ConsoleResourceVo.VoRoutePath))]
    [MapProperty(nameof(ConsoleResource.Icon), nameof(ConsoleResourceVo.VoIcon))]
    [MapProperty(nameof(ConsoleResource.OrderSort), nameof(ConsoleResourceVo.VoOrderSort))]
    [MapProperty(nameof(ConsoleResource.ShowInSidebar), nameof(ConsoleResourceVo.VoShowInSidebar))]
    [MapProperty(nameof(ConsoleResource.ShowInSearch), nameof(ConsoleResourceVo.VoShowInSearch))]
    [MapProperty(nameof(ConsoleResource.Description), nameof(ConsoleResourceVo.VoDescription))]
    [MapProperty(nameof(ConsoleResource.IsEnabled), nameof(ConsoleResourceVo.VoIsEnabled))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial ConsoleResourceVo? ToConsoleResourceVo(ConsoleResource? source);

    public static partial List<ConsoleResourceVo> ToConsoleResourceVoList(IEnumerable<ConsoleResource>? source);

    // ConsoleResourceVo → ConsoleResource
    [MapProperty(nameof(ConsoleResourceVo.VoParentId), nameof(ConsoleResource.ParentId))]
    [MapProperty(nameof(ConsoleResourceVo.VoResourceKey), nameof(ConsoleResource.ResourceKey))]
    [MapProperty(nameof(ConsoleResourceVo.VoResourceName), nameof(ConsoleResource.ResourceName))]
    [MapProperty(nameof(ConsoleResourceVo.VoResourceType), nameof(ConsoleResource.ResourceType))]
    [MapProperty(nameof(ConsoleResourceVo.VoModuleKey), nameof(ConsoleResource.ModuleKey))]
    [MapProperty(nameof(ConsoleResourceVo.VoRoutePath), nameof(ConsoleResource.RoutePath))]
    [MapProperty(nameof(ConsoleResourceVo.VoIcon), nameof(ConsoleResource.Icon))]
    [MapProperty(nameof(ConsoleResourceVo.VoOrderSort), nameof(ConsoleResource.OrderSort))]
    [MapProperty(nameof(ConsoleResourceVo.VoShowInSidebar), nameof(ConsoleResource.ShowInSidebar))]
    [MapProperty(nameof(ConsoleResourceVo.VoShowInSearch), nameof(ConsoleResource.ShowInSearch))]
    [MapProperty(nameof(ConsoleResourceVo.VoDescription), nameof(ConsoleResource.Description))]
    [MapProperty(nameof(ConsoleResourceVo.VoIsEnabled), nameof(ConsoleResource.IsEnabled))]
    [MapperIgnoreTarget(nameof(ConsoleResource.IsDeleted))]
    [MapperIgnoreTarget(nameof(ConsoleResource.DeletedAt))]
    [MapperIgnoreTarget(nameof(ConsoleResource.DeletedBy))]
    [MapperIgnoreTarget(nameof(ConsoleResource.CreateId))]
    [MapperIgnoreTarget(nameof(ConsoleResource.CreateBy))]
    [MapperIgnoreTarget(nameof(ConsoleResource.CreateTime))]
    [MapperIgnoreTarget(nameof(ConsoleResource.ModifyId))]
    [MapperIgnoreTarget(nameof(ConsoleResource.ModifyBy))]
    [MapperIgnoreTarget(nameof(ConsoleResource.ModifyTime))]
    [MapProperty(nameof(ConsoleResourceVo.VoId), nameof(ConsoleResource.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial ConsoleResource? ToConsoleResource(ConsoleResourceVo? source);

    public static partial List<ConsoleResource> ToConsoleResourceList(IEnumerable<ConsoleResourceVo>? source);

}
