using AutoMapper;
using Radish.Common.PermissionTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>
/// Console 授权服务
/// </summary>
public class ConsoleAuthorizationService : IConsoleAuthorizationService
{
    private readonly IMapper _mapper;
    private readonly IBaseRepository<Role> _roleRepository;
    private readonly IBaseRepository<ConsoleResource> _consoleResourceRepository;
    private readonly IBaseRepository<RoleConsoleResource> _roleConsoleResourceRepository;
    private readonly IBaseRepository<ConsoleResourceApiModule> _consoleResourceApiModuleRepository;
    private readonly IBaseRepository<RoleModulePermission> _roleModulePermissionRepository;
    private readonly IBaseRepository<ApiModule> _apiModuleRepository;

    public ConsoleAuthorizationService(
        IMapper mapper,
        IBaseRepository<Role> roleRepository,
        IBaseRepository<ConsoleResource> consoleResourceRepository,
        IBaseRepository<RoleConsoleResource> roleConsoleResourceRepository,
        IBaseRepository<ConsoleResourceApiModule> consoleResourceApiModuleRepository,
        IBaseRepository<RoleModulePermission> roleModulePermissionRepository,
        IBaseRepository<ApiModule> apiModuleRepository)
    {
        _mapper = mapper;
        _roleRepository = roleRepository;
        _consoleResourceRepository = consoleResourceRepository;
        _roleConsoleResourceRepository = roleConsoleResourceRepository;
        _consoleResourceApiModuleRepository = consoleResourceApiModuleRepository;
        _roleModulePermissionRepository = roleModulePermissionRepository;
        _apiModuleRepository = apiModuleRepository;
    }

    public async Task<List<string>> GetPermissionKeysByRolesAsync(IReadOnlyCollection<string> roleNames)
    {
        if (roleNames.Count <= 0)
        {
            return new List<string>();
        }

        var normalizedRoles = roleNames
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedRoles.Length <= 0)
        {
            return new List<string>();
        }

        var permissionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        permissionKeys.UnionWith(ConsolePermissions.GetDefaultPermissions(normalizedRoles));

        var roles = await _roleRepository.QueryAsync(r =>
            normalizedRoles.Contains(r.RoleName) &&
            !r.IsDeleted &&
            r.IsEnabled);

        var roleIds = roles.Select(role => role.Id).Distinct().ToList();
        if (roleIds.Count <= 0)
        {
            return permissionKeys.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var explicitResources = await _roleConsoleResourceRepository.QueryAsync(link => roleIds.Contains(link.RoleId));
        if (explicitResources.Count > 0)
        {
            var resourceIds = explicitResources
                .Select(link => link.ConsoleResourceId)
                .Distinct()
                .ToList();

            var resources = await _consoleResourceRepository.QueryAsync(resource =>
                resourceIds.Contains(resource.Id) &&
                resource.IsEnabled);

            foreach (var resourceKey in resources
                         .Select(resource => resource.ResourceKey)
                         .Where(static key => !string.IsNullOrWhiteSpace(key)))
            {
                permissionKeys.Add(resourceKey);
            }
        }

        var legacyMappings = await _roleModulePermissionRepository.QueryAsync(link => roleIds.Contains(link.RoleId));
        if (legacyMappings.Count > 0)
        {
            var apiModuleIds = legacyMappings
                .Select(link => link.ApiModuleId)
                .Distinct()
                .ToList();

            var apiModules = await _apiModuleRepository.QueryAsync(module =>
                apiModuleIds.Contains(module.Id) &&
                !module.IsDeleted &&
                module.IsEnabled);

            foreach (var apiUrl in apiModules
                         .Select(module => module.LinkUrl)
                         .Where(static url => !string.IsNullOrWhiteSpace(url)))
            {
                permissionKeys.UnionWith(ConsolePermissions.GetPermissionsByApiUrl(apiUrl));
            }
        }

        NormalizeEntryPermission(permissionKeys);

        return permissionKeys
            .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<ConsoleResourceTreeNodeVo>> GetResourceTreeAsync()
    {
        var resources = await GetOrderedResourcesAsync();
        var bindingMap = await BuildResourceBindingMapAsync(resources.Select(resource => resource.Id).ToList());

        var childrenLookup = resources
            .GroupBy(resource => resource.ParentId)
            .ToDictionary(group => group.Key, group => group.OrderBy(static item => item.OrderSort).ThenBy(static item => item.Id).ToList());

        return BuildTree(0, childrenLookup, bindingMap);
    }

    public async Task<RoleAuthorizationSnapshotVo?> GetRoleAuthorizationAsync(long roleId)
    {
        var role = await _roleRepository.QueryFirstAsync(item => item.Id == roleId && !item.IsDeleted);
        if (role == null)
        {
            return null;
        }

        var resources = await GetOrderedResourcesAsync();
        var grantedResourceIds = await GetGrantedResourceIdsAsync(role, resources);
        var grantedPermissionKeys = resources
            .Where(resource => grantedResourceIds.Contains(resource.Id))
            .Select(resource => resource.ResourceKey)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var apiBindings = await GetApiBindingsByResourceIdsAsync(grantedResourceIds);
        var latestGrantTime = await GetLatestGrantTimeAsync(roleId);

        return new RoleAuthorizationSnapshotVo
        {
            VoRoleId = role.Id,
            VoRoleName = role.RoleName,
            VoRoleDescription = role.RoleDescription,
            VoRoleIsEnabled = role.IsEnabled,
            VoLastModifyTime = latestGrantTime,
            VoGrantedResourceIds = grantedResourceIds.OrderBy(static item => item).ToList(),
            VoGrantedPermissionKeys = grantedPermissionKeys,
            VoDerivedApiModules = apiBindings
        };
    }

    public async Task<List<ResourceApiBindingVo>> GetRolePermissionPreviewAsync(long roleId)
    {
        var role = await _roleRepository.QueryFirstAsync(item => item.Id == roleId && !item.IsDeleted);
        if (role == null)
        {
            return new List<ResourceApiBindingVo>();
        }

        var resources = await GetOrderedResourcesAsync();
        var grantedResourceIds = await GetGrantedResourceIdsAsync(role, resources);
        return await GetApiBindingsByResourceIdsAsync(grantedResourceIds);
    }

    public async Task<bool> SaveRoleAuthorizationAsync(SaveRoleAuthorizationDto dto, long operatorId, string operatorName)
    {
        var role = await _roleRepository.QueryFirstAsync(item => item.Id == dto.RoleId && !item.IsDeleted);
        if (role == null)
        {
            return false;
        }

        var resources = await GetOrderedResourcesAsync();
        var allowedResourceIds = resources.Select(resource => resource.Id).ToHashSet();
        var selectedResourceIds = dto.ResourceIds
            .Where(allowedResourceIds.Contains)
            .Distinct()
            .ToList();

        var normalizedResourceIds = NormalizeSelectedResourceIds(selectedResourceIds, resources);
        var activeLinks = await _roleConsoleResourceRepository.QueryAsync(link => link.RoleId == dto.RoleId);
        var activeResourceIds = activeLinks
            .Select(link => link.ConsoleResourceId)
            .Distinct()
            .ToHashSet();

        var resourceIdsToDelete = activeResourceIds.Except(normalizedResourceIds).ToList();
        if (resourceIdsToDelete.Count > 0)
        {
            await _roleConsoleResourceRepository.SoftDeleteAsync(
                link => link.RoleId == dto.RoleId && resourceIdsToDelete.Contains(link.ConsoleResourceId),
                operatorName);
        }

        if (normalizedResourceIds.Count > 0)
        {
            await _roleConsoleResourceRepository.RestoreAsync(
                link => link.RoleId == dto.RoleId && normalizedResourceIds.Contains(link.ConsoleResourceId));
        }

        var restoredLinks = await _roleConsoleResourceRepository.QueryAsync(link => link.RoleId == dto.RoleId);
        var restoredResourceIds = restoredLinks
            .Select(link => link.ConsoleResourceId)
            .Distinct()
            .ToHashSet();

        var linksToInsert = normalizedResourceIds
            .Except(restoredResourceIds)
            .Select((resourceId, index) => new RoleConsoleResource
            {
                Id = GenerateStableSeedId(dto.RoleId, resourceId, index),
                RoleId = dto.RoleId,
                ConsoleResourceId = resourceId,
                CreateId = operatorId,
                CreateBy = operatorName,
                ModifyId = operatorId,
                ModifyBy = operatorName,
                ModifyTime = DateTime.UtcNow
            })
            .ToList();

        if (linksToInsert.Count > 0)
        {
            await _roleConsoleResourceRepository.AddRangeAsync(linksToInsert);
        }

        return true;
    }

    private async Task<List<ConsoleResource>> GetOrderedResourcesAsync()
    {
        var resources = await _consoleResourceRepository.QueryAsync(resource => resource.IsEnabled);
        return resources
            .OrderBy(static resource => resource.OrderSort)
            .ThenBy(static resource => resource.Id)
            .ToList();
    }

    private async Task<List<long>> GetGrantedResourceIdsAsync(Role role, IReadOnlyCollection<ConsoleResource> allResources)
    {
        var explicitLinks = await _roleConsoleResourceRepository.QueryAsync(link => link.RoleId == role.Id);
        var grantedResourceIds = explicitLinks
            .Select(link => link.ConsoleResourceId)
            .Distinct()
            .ToHashSet();

        if (string.Equals(role.RoleName, "System", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role.RoleName, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            grantedResourceIds.UnionWith(allResources.Select(resource => resource.Id));
        }

        return NormalizeSelectedResourceIds(grantedResourceIds, allResources);
    }

    private async Task<DateTime?> GetLatestGrantTimeAsync(long roleId)
    {
        var links = await _roleConsoleResourceRepository.QueryAsync(link => link.RoleId == roleId);
        if (links.Count <= 0)
        {
            return null;
        }

        return links
            .Select(link => link.ModifyTime ?? link.CreateTime)
            .OrderByDescending(static time => time)
            .FirstOrDefault();
    }

    private async Task<List<ResourceApiBindingVo>> GetApiBindingsByResourceIdsAsync(IReadOnlyCollection<long> resourceIds)
    {
        if (resourceIds.Count <= 0)
        {
            return new List<ResourceApiBindingVo>();
        }

        var bindingMap = await BuildResourceBindingMapAsync(resourceIds.ToList());
        return resourceIds
            .SelectMany(resourceId => bindingMap.TryGetValue(resourceId, out var bindings)
                ? bindings
                : Enumerable.Empty<ResourceApiBindingVo>())
            .DistinctBy(binding => $"{binding.VoResourceId}:{binding.VoApiModuleId}")
            .OrderBy(static binding => binding.VoResourceKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static binding => binding.VoLinkUrl, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<Dictionary<long, List<ResourceApiBindingVo>>> BuildResourceBindingMapAsync(List<long> resourceIds)
    {
        if (resourceIds.Count <= 0)
        {
            return new Dictionary<long, List<ResourceApiBindingVo>>();
        }

        var mappings = await _consoleResourceApiModuleRepository.QueryAsync(mapping =>
            resourceIds.Contains(mapping.ConsoleResourceId));
        if (mappings.Count <= 0)
        {
            return new Dictionary<long, List<ResourceApiBindingVo>>();
        }

        var resources = await _consoleResourceRepository.QueryAsync(resource =>
            resourceIds.Contains(resource.Id));
        var resourceMap = resources.ToDictionary(resource => resource.Id);

        var apiModuleIds = mappings
            .Select(mapping => mapping.ApiModuleId)
            .Distinct()
            .ToList();

        var apiModules = await _apiModuleRepository.QueryAsync(apiModule =>
            apiModuleIds.Contains(apiModule.Id) &&
            !apiModule.IsDeleted &&
            apiModule.IsEnabled);
        var apiModuleMap = apiModules.ToDictionary(apiModule => apiModule.Id);

        return mappings
            .Where(mapping => resourceMap.ContainsKey(mapping.ConsoleResourceId) && apiModuleMap.ContainsKey(mapping.ApiModuleId))
            .GroupBy(mapping => mapping.ConsoleResourceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(mapping =>
                    {
                        var resource = resourceMap[mapping.ConsoleResourceId];
                        var apiModule = apiModuleMap[mapping.ApiModuleId];
                        return new ResourceApiBindingVo
                        {
                            VoResourceId = resource.Id,
                            VoResourceKey = resource.ResourceKey,
                            VoApiModuleId = apiModule.Id,
                            VoApiModuleName = apiModule.ApiModuleName,
                            VoLinkUrl = apiModule.LinkUrl,
                            VoRelationType = mapping.RelationType
                        };
                    })
                    .OrderBy(static binding => binding.VoLinkUrl, StringComparer.OrdinalIgnoreCase)
                    .ToList());
    }

    private static List<long> NormalizeSelectedResourceIds(IEnumerable<long> selectedResourceIds, IReadOnlyCollection<ConsoleResource> resources)
    {
        var resourceMap = resources.ToDictionary(resource => resource.Id);
        var normalized = selectedResourceIds
            .Where(resourceMap.ContainsKey)
            .ToHashSet();

        var accessResource = resources.FirstOrDefault(resource =>
            string.Equals(resource.ResourceKey, ConsolePermissions.Access, StringComparison.OrdinalIgnoreCase));
        if (accessResource != null && normalized.Any(resourceId =>
                !string.Equals(resourceMap[resourceId].ResourceKey, ConsolePermissions.Access, StringComparison.OrdinalIgnoreCase)))
        {
            normalized.Add(accessResource.Id);
        }

        foreach (var resourceId in normalized.ToList())
        {
            var currentId = resourceMap[resourceId].ParentId;
            while (currentId > 0 && resourceMap.TryGetValue(currentId, out var parent))
            {
                normalized.Add(parent.Id);
                currentId = parent.ParentId;
            }
        }

        return normalized
            .OrderBy(static item => item)
            .ToList();
    }

    private static List<ConsoleResourceTreeNodeVo> BuildTree(
        long parentId,
        IReadOnlyDictionary<long, List<ConsoleResource>> childrenLookup,
        IReadOnlyDictionary<long, List<ResourceApiBindingVo>> bindingMap)
    {
        if (!childrenLookup.TryGetValue(parentId, out var children))
        {
            return new List<ConsoleResourceTreeNodeVo>();
        }

        return children
            .Select(child => new ConsoleResourceTreeNodeVo
            {
                VoId = child.Id,
                VoTitle = child.ResourceName,
                VoResourceKey = child.ResourceKey,
                VoResourceType = child.ResourceType,
                VoApiBindings = bindingMap.TryGetValue(child.Id, out var bindings)
                    ? bindings
                    : new List<ResourceApiBindingVo>(),
                VoChildren = BuildTree(child.Id, childrenLookup, bindingMap)
            })
            .ToList();
    }

    private static long GenerateStableSeedId(long roleId, long resourceId, int index)
    {
        // 避免依赖运行时雪花 ID，确保重复保存时主键稳定可预测。
        return 930000000000L + (roleId * 1000) + resourceId + index;
    }

    private static void NormalizeEntryPermission(HashSet<string> permissionKeys)
    {
        var hasOperationalPermission = permissionKeys.Any(ConsolePermissions.IsConsoleOperationalPermission);
        if (hasOperationalPermission)
        {
            permissionKeys.Add(ConsolePermissions.Access);
            return;
        }

        permissionKeys.Remove(ConsolePermissions.Access);
    }
}
