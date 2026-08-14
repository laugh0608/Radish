using AutoMapper;
using Microsoft.AspNetCore.Http;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Service;

/// <summary>
/// Console 角色治理服务。
/// </summary>
public sealed class RoleGovernanceService : IRoleGovernanceService
{
    public const long SystemRoleId = 10000;
    public const long AdminRoleId = 10001;

    private static readonly string[] ReservedRoleNames = [UserRoles.System, UserRoles.Admin];
    private static readonly int[] SupportedAuthorityScopes =
    [
        (int)AuthorityScopeKindEnum.None,
        (int)AuthorityScopeKindEnum.Custom,
        (int)AuthorityScopeKindEnum.Department,
        (int)AuthorityScopeKindEnum.DepartmentAndChildren,
        (int)AuthorityScopeKindEnum.Self,
        (int)AuthorityScopeKindEnum.All
    ];

    private readonly IMapper _mapper;
    private readonly IBaseRepository<Role> _roleRepository;

    public RoleGovernanceService(IMapper mapper, IBaseRepository<Role> roleRepository)
    {
        _mapper = mapper;
        _roleRepository = roleRepository;
    }

    public async Task<List<RoleVo>> GetRolesAsync()
    {
        var roles = await _roleRepository.QueryAsync(role => !role.IsDeleted);
        return roles
            .OrderBy(role => role.OrderSort)
            .ThenBy(role => role.Id)
            .Select(MapRole)
            .ToList();
    }

    public async Task<RoleVo?> GetRoleAsync(long roleId)
    {
        EnsureValidRoleId(roleId);
        var role = await _roleRepository.QueryFirstAsync(item => item.Id == roleId && !item.IsDeleted);
        return role == null ? null : MapRole(role);
    }

    public async Task<RoleVo> CreateRoleAsync(
        RoleMutationDto request,
        long operatorId,
        string operatorName)
    {
        var normalized = NormalizeRequest(request);
        EnsureRoleNameIsNotReserved(normalized.RoleName);
        await EnsureRoleNameAvailableAsync(normalized.RoleName, null);

        var now = DateTime.UtcNow;
        var role = new Role
        {
            RoleName = normalized.RoleName,
            RoleDescription = normalized.RoleDescription,
            OrderSort = normalized.OrderSort,
            DepartmentIds = normalized.DepartmentIds,
            AuthorityScope = normalized.AuthorityScope,
            IsEnabled = normalized.IsEnabled,
            IsDeleted = false,
            CreateId = NormalizeOperatorId(operatorId),
            CreateBy = NormalizeOperatorName(operatorName),
            CreateTime = now,
            ModifyId = NormalizeOperatorId(operatorId),
            ModifyBy = NormalizeOperatorName(operatorName),
            ModifyTime = now
        };

        try
        {
            role.Id = await _roleRepository.AddAsync(role);
        }
        catch (Exception ex) when (IsUniqueConstraintConflict(ex))
        {
            throw RoleNameConflict();
        }

        return MapRole(role);
    }

    public async Task<RoleVo> UpdateRoleAsync(
        long roleId,
        RoleMutationDto request,
        long operatorId,
        string operatorName)
    {
        EnsureValidRoleId(roleId);
        var role = await RequireMutableRoleAsync(roleId);
        var normalized = NormalizeRequest(request);
        EnsureRoleNameIsNotReserved(normalized.RoleName);
        await EnsureRoleNameAvailableAsync(normalized.RoleName, roleId);

        var now = DateTime.UtcNow;
        int affectedRows;
        try
        {
            affectedRows = await _roleRepository.UpdateColumnsAsync(
                item => new Role
                {
                    RoleName = normalized.RoleName,
                    RoleDescription = normalized.RoleDescription,
                    OrderSort = normalized.OrderSort,
                    DepartmentIds = normalized.DepartmentIds,
                    AuthorityScope = normalized.AuthorityScope,
                    IsEnabled = normalized.IsEnabled,
                    ModifyId = NormalizeOperatorId(operatorId),
                    ModifyBy = NormalizeOperatorName(operatorName),
                    ModifyTime = now
                },
                item => item.Id == roleId && !item.IsDeleted);
        }
        catch (Exception ex) when (IsUniqueConstraintConflict(ex))
        {
            throw RoleNameConflict();
        }

        if (affectedRows <= 0)
        {
            throw RoleNotFound();
        }

        role.RoleName = normalized.RoleName;
        role.RoleDescription = normalized.RoleDescription;
        role.OrderSort = normalized.OrderSort;
        role.DepartmentIds = normalized.DepartmentIds;
        role.AuthorityScope = normalized.AuthorityScope;
        role.IsEnabled = normalized.IsEnabled;
        role.ModifyId = NormalizeOperatorId(operatorId);
        role.ModifyBy = NormalizeOperatorName(operatorName);
        role.ModifyTime = now;
        return MapRole(role);
    }

    public async Task<RoleVo> ToggleRoleAsync(
        long roleId,
        bool enabled,
        long operatorId,
        string operatorName)
    {
        EnsureValidRoleId(roleId);
        var role = await RequireMutableRoleAsync(roleId);
        var now = DateTime.UtcNow;
        var affectedRows = await _roleRepository.UpdateColumnsAsync(
            item => new Role
            {
                IsEnabled = enabled,
                ModifyId = NormalizeOperatorId(operatorId),
                ModifyBy = NormalizeOperatorName(operatorName),
                ModifyTime = now
            },
            item => item.Id == roleId && !item.IsDeleted);

        if (affectedRows <= 0)
        {
            throw RoleNotFound();
        }

        role.IsEnabled = enabled;
        role.ModifyId = NormalizeOperatorId(operatorId);
        role.ModifyBy = NormalizeOperatorName(operatorName);
        role.ModifyTime = now;
        return MapRole(role);
    }

    public async Task DeleteRoleAsync(long roleId, long operatorId, string operatorName)
    {
        EnsureValidRoleId(roleId);
        _ = await RequireMutableRoleAsync(roleId);
        var affectedRows = await _roleRepository.UpdateColumnsAsync(
            item => new Role
            {
                IsDeleted = true,
                ModifyId = NormalizeOperatorId(operatorId),
                ModifyBy = NormalizeOperatorName(operatorName),
                ModifyTime = DateTime.UtcNow
            },
            item => item.Id == roleId && !item.IsDeleted);

        if (affectedRows <= 0)
        {
            throw RoleNotFound();
        }
    }

    public static bool IsBuiltInRole(long roleId, string? roleName = null)
    {
        return roleId is SystemRoleId or AdminRoleId || IsReservedRoleName(roleName);
    }

    private async Task<Role> RequireMutableRoleAsync(long roleId)
    {
        var role = await _roleRepository.QueryFirstAsync(item => item.Id == roleId && !item.IsDeleted);
        if (role == null)
        {
            throw RoleNotFound();
        }

        if (IsBuiltInRole(role.Id, role.RoleName))
        {
            throw new BusinessException(
                "System / Admin 是系统保护角色，不能修改、停用或删除",
                StatusCodes.Status409Conflict,
                "Role.BuiltInProtected",
                "error.role.built_in_protected");
        }

        return role;
    }

    private async Task EnsureRoleNameAvailableAsync(string roleName, long? excludedRoleId)
    {
        var normalizedName = roleName.ToLowerInvariant();
        var duplicated = await _roleRepository.QueryFirstAsync(role =>
            role.RoleName.Trim().ToLower() == normalizedName &&
            !role.IsDeleted &&
            (!excludedRoleId.HasValue || role.Id != excludedRoleId.Value));
        if (duplicated != null)
        {
            throw RoleNameConflict();
        }
    }

    private static NormalizedRoleMutation NormalizeRequest(RoleMutationDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var roleName = request.VoRoleName?.Trim() ?? string.Empty;
        if (roleName.Length <= 0 || roleName.Length > 50)
        {
            throw InvalidRole("角色名称长度必须在 1-50 个字符之间", "Role.InvalidName");
        }

        var description = request.VoRoleDescription?.Trim() ?? string.Empty;
        if (description.Length > 500)
        {
            throw InvalidRole("角色描述不能超过 500 个字符", "Role.InvalidDescription");
        }

        if (request.VoOrderSort < 0)
        {
            throw InvalidRole("角色排序不能小于 0", "Role.InvalidOrderSort");
        }

        if (!SupportedAuthorityScopes.Contains(request.VoAuthorityScope))
        {
            throw InvalidRole("角色权限范围无效", "Role.InvalidAuthorityScope");
        }

        var departmentIds = NormalizeDepartmentIds(request.VoDepartmentIds, request.VoAuthorityScope);
        return new NormalizedRoleMutation(
            roleName,
            description,
            request.VoOrderSort,
            departmentIds,
            request.VoAuthorityScope,
            request.VoIsEnabled);
    }

    private static string NormalizeDepartmentIds(string? value, int authorityScope)
    {
        if (authorityScope != (int)AuthorityScopeKindEnum.Custom)
        {
            return string.Empty;
        }

        var rawValue = value?.Trim() ?? string.Empty;
        if (rawValue.Length <= 0)
        {
            throw InvalidRole("自定义权限范围必须指定部门 ID", "Role.DepartmentIdsRequired");
        }

        if (rawValue.Length > 500)
        {
            throw InvalidRole("部门 ID 列表不能超过 500 个字符", "Role.InvalidDepartmentIds");
        }

        var normalizedIds = new SortedSet<long>();
        foreach (var item in rawValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!long.TryParse(item, out var departmentId) || departmentId <= 0)
            {
                throw InvalidRole("部门 ID 必须是逗号分隔的正整数字符串", "Role.InvalidDepartmentIds");
            }

            normalizedIds.Add(departmentId);
        }

        if (normalizedIds.Count <= 0)
        {
            throw InvalidRole("自定义权限范围必须指定部门 ID", "Role.DepartmentIdsRequired");
        }

        return string.Join(',', normalizedIds);
    }

    private RoleVo MapRole(Role role)
    {
        var result = _mapper.Map<RoleVo>(role);
        result.VoIsBuiltIn = IsBuiltInRole(role.Id, role.RoleName);
        return result;
    }

    private static void EnsureRoleNameIsNotReserved(string roleName)
    {
        if (IsReservedRoleName(roleName))
        {
            throw new BusinessException(
                "System / Admin 是系统保留角色名称",
                StatusCodes.Status409Conflict,
                "Role.ReservedName",
                "error.role.reserved_name");
        }
    }

    private static bool IsReservedRoleName(string? roleName)
    {
        return !string.IsNullOrWhiteSpace(roleName) &&
               ReservedRoleNames.Contains(roleName.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static void EnsureValidRoleId(long roleId)
    {
        if (roleId <= 0)
        {
            throw InvalidRole("角色 ID 无效", "Role.InvalidId");
        }
    }

    private static long NormalizeOperatorId(long operatorId) => operatorId > 0 ? operatorId : 0;

    private static string NormalizeOperatorName(string? operatorName)
    {
        return string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();
    }

    private static BusinessException InvalidRole(string message, string code) => new(
        message,
        StatusCodes.Status400BadRequest,
        code,
        "error.role.invalid_request");

    private static BusinessException RoleNotFound() => new(
        "角色不存在",
        StatusCodes.Status404NotFound,
        "Role.NotFound",
        "error.role.not_found");

    private static BusinessException RoleNameConflict() => new(
        "角色名称已存在",
        StatusCodes.Status409Conflict,
        "Role.NameConflict",
        "error.role.name_conflict");

    private static bool IsUniqueConstraintConflict(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record NormalizedRoleMutation(
        string RoleName,
        string RoleDescription,
        int OrderSort,
        string DepartmentIds,
        int AuthorityScope,
        bool IsEnabled);
}
