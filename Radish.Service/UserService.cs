using AutoMapper;
using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.Common.HelpTool;
using Radish.Common.PermissionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using SqlSugar;

namespace Radish.Service;

/// <summary>用户服务类</summary>
public class UserService : BaseService<User, UserVo>, IUserService
{
    private const int PublicIndexAllocationMaxAttempts = 5;
    private readonly IBaseRepository<User> _userBaseRepository;
    private readonly IUserRepository _userRepository;
    private readonly IBaseRepository<Role> _roleRepository;
    private readonly IBaseRepository<UserRole> _userRoleRepository;
    private readonly IDepartmentService _departmentService;
    private readonly IConsoleAuthorizationService _consoleAuthorizationService;

    public UserService(IDepartmentService departmentService, IMapper mapper,
        IBaseRepository<User> baseRepository, IUserRepository userRepository, IBaseRepository<Role> roleRepository,
        IBaseRepository<UserRole> userRoleRepository,
        IConsoleAuthorizationService consoleAuthorizationService) : base(mapper, baseRepository)
    {
        _departmentService = departmentService;
        _userBaseRepository = baseRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _consoleAuthorizationService = consoleAuthorizationService;
    }

    public new async Task<long> AddAsync(User entity)
    {
        var allocatedPublicIndex = !User.HasAssignedPublicIndex(entity.PublicIndex);
        await EnsureUserPublicIdentifiersAsync(entity);

        for (var attempt = 1; attempt <= PublicIndexAllocationMaxAttempts; attempt++)
        {
            try
            {
                return await base.AddAsync(entity);
            }
            catch (Exception ex) when (allocatedPublicIndex &&
                                       attempt < PublicIndexAllocationMaxAttempts &&
                                       IsUniqueConstraintConflict(ex, nameof(User.PublicIndex)))
            {
                entity.PublicIndex = await AllocateNextPublicIndexAsync(entity.PublicIndex);
            }
        }

        return await base.AddAsync(entity);
    }

    public new async Task<int> AddRangeAsync(List<User> entities)
    {
        var nextPublicIndex = await AllocateNextPublicIndexAsync();
        foreach (var entity in entities)
        {
            entity.PublicId = User.EnsurePublicId(entity.PublicId);
            if (!User.HasAssignedPublicIndex(entity.PublicIndex))
            {
                entity.PublicIndex = nextPublicIndex++;
            }
        }

        return await base.AddRangeAsync(entities);
    }

    /// <summary>
    /// 根据邮箱获取可登录用户
    /// </summary>
    /// <param name="email">电子邮箱</param>
    /// <returns>用户视图模型</returns>
    public async Task<UserVo?> GetEnabledUserByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var rawEmail = email.Trim();
        var normalizedEmail = rawEmail.ToLowerInvariant();
        return await QueryFirstAsync(u =>
            (u.UserEmail == rawEmail ||
             u.UserEmail == normalizedEmail) &&
            u.IsDeleted == false &&
            u.IsEnable);
    }

    public async Task<UserVo?> GetPublicUserByIdentifierAsync(string identifier)
    {
        var normalizedIdentifier = identifier?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedIdentifier))
        {
            return null;
        }

        User? user;
        if (User.HasPublicIdFormat(normalizedIdentifier))
        {
            var normalizedPublicId = normalizedIdentifier.ToLowerInvariant();
            user = await _userBaseRepository.QueryFirstAsync(u =>
                u.PublicId == normalizedPublicId &&
                !u.IsDeleted &&
                u.IsEnable);
        }
        else if (long.TryParse(normalizedIdentifier, out var userId) && userId > 0)
        {
            user = await _userBaseRepository.QueryFirstAsync(u =>
                u.Id == userId &&
                !u.IsDeleted &&
                u.IsEnable);
        }
        else
        {
            return null;
        }

        if (user == null)
        {
            return null;
        }

        await EnsureUserPublicIdentityBackfilledAsync(user);
        return Mapper.Map<UserVo>(user);
    }

    private async Task EnsureUserPublicIdentifiersAsync(User user)
    {
        user.PublicId = User.EnsurePublicId(user.PublicId);
        if (!User.HasAssignedPublicIndex(user.PublicIndex))
        {
            user.PublicIndex = await AllocateNextPublicIndexAsync();
        }
    }

    private async Task<long> AllocateNextPublicIndexAsync(long? floor = null)
    {
        var maxPublicIndexTask = _userBaseRepository.QueryMaxAsync<long?>(
            item => item.PublicIndex,
            item => item.PublicIndex >= User.PublicIndexStart);
        var maxPublicIndex = maxPublicIndexTask == null ? null : await maxPublicIndexTask;
        var baseline = Math.Max(
            maxPublicIndex.GetValueOrDefault(User.PublicIndexStart - 1),
            floor.GetValueOrDefault(User.PublicIndexStart - 1));

        return baseline + 1;
    }

    private async Task EnsureUserPublicIdentityBackfilledAsync(User user)
    {
        var missingPublicId = string.IsNullOrWhiteSpace(user.PublicId);
        var missingPublicIndex = !User.HasAssignedPublicIndex(user.PublicIndex);

        if (!missingPublicId)
        {
            user.PublicId = user.PublicId?.Trim();
        }

        if (!missingPublicId && !missingPublicIndex)
        {
            return;
        }

        for (var attempt = 1; attempt <= PublicIndexAllocationMaxAttempts; attempt++)
        {
            var publicId = missingPublicId ? User.EnsurePublicId(user.PublicId) : user.PublicId;
            var publicIndex = missingPublicIndex ? await AllocateNextPublicIndexAsync(user.PublicIndex) : user.PublicIndex;

            try
            {
                var affectedRows = await _userBaseRepository.UpdateColumnsAsync(
                    item => new User
                    {
                        PublicId = publicId,
                        PublicIndex = publicIndex,
                        UpdateTime = DateTime.Now
                    },
                    item => item.Id == user.Id &&
                            !item.IsDeleted &&
                            ((missingPublicId && (item.PublicId == null || item.PublicId == string.Empty)) ||
                             (missingPublicIndex && (item.PublicIndex == null || item.PublicIndex <= 0))));

                if (affectedRows > 0)
                {
                    user.PublicId = publicId;
                    user.PublicIndex = publicIndex;
                    return;
                }
            }
            catch (Exception ex) when (missingPublicIndex &&
                                       attempt < PublicIndexAllocationMaxAttempts &&
                                       IsUniqueConstraintConflict(ex, nameof(User.PublicIndex)))
            {
                user.PublicIndex = publicIndex;
                continue;
            }

            break;
        }

        var refreshedUser = await _userBaseRepository.QueryByIdAsync(user.Id);
        if (!string.IsNullOrWhiteSpace(refreshedUser?.PublicId))
        {
            user.PublicId = refreshedUser.PublicId.Trim();
        }

        if (User.HasAssignedPublicIndex(refreshedUser?.PublicIndex))
        {
            user.PublicIndex = refreshedUser!.PublicIndex;
        }
    }

    /// <summary>
    /// 通过登录用户名和登录密码查询用户的角色名称
    /// </summary>
    /// <param name="loginName">登录用户名</param>
    /// <param name="loginPwd">登陆密码</param>
    /// <returns>string RoleName, 可能为多个</returns>
    public async Task<string> GetUserRoleNameStrAsync(string loginName, string loginPwd)
    {
        var user = await QueryFirstAsync(a => a.LoginName == loginName && a.LoginPassword == loginPwd);
        if (user == null)
        {
            return string.Empty;
        }

        var roleNames = await GetUserRoleNamesAsync(user.Uuid);
        return string.Join(',', roleNames);
    }

    public async Task<List<string>> GetUserRoleNamesAsync(long userId)
    {
        var userRoles = await _userRoleRepository.QueryAsync(ur => ur.UserId == userId && ur.IsDeleted == false);
        if (userRoles.Count <= 0)
        {
            return new List<string>();
        }

        var roleIds = userRoles
            .Select(ur => ur.RoleId)
            .Distinct()
            .ToList();

        var roles = await _roleRepository.QueryAsync(role =>
            roleIds.Contains(role.Id) &&
            role.IsDeleted == false &&
            role.IsEnabled);

        return roles
            .Select(role => role.RoleName)
            .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(roleName => roleName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 获取所有的 角色-API 关系
    /// </summary>
    /// <returns>List RoleModulePermission</returns>
    public async Task<List<RoleModulePermission>> RoleModuleMaps()
    {
        // return await _userRepository.RoleModuleMaps();

        return await QueryMuchAsync<RoleModulePermission, ApiModule, Role, RoleModulePermission>(
            (rmp, m, r) => new object[]
            {
                JoinType.Left, rmp.ApiModuleId == m.Id,
                JoinType.Left, rmp.RoleId == r.Id
            },
            (rmp, m, r) => new RoleModulePermission()
            {
                Role = r,
                ApiModule = m,
                IsDeleted = rmp.IsDeleted
            },
            (rmp, m, r) => rmp.IsDeleted == false && m.IsDeleted == false && r.IsDeleted == false
        );
    }

    /// <summary>
    /// 根据角色列表获取当前 Console 权限快照
    /// </summary>
    /// <param name="roleNames">角色名列表</param>
    /// <returns>权限标识列表</returns>
    public async Task<List<string>> GetPermissionKeysByRolesAsync(IReadOnlyCollection<string> roleNames)
    {
        return await _consoleAuthorizationService.GetPermissionKeysByRolesAsync(roleNames);
    }

    /// <summary>
    /// 修改当前用户登录密码。
    /// </summary>
    public async Task ChangeMyLoginPasswordAsync(long userId, string currentPassword, string newPassword, string confirmPassword)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("用户 ID 无效", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            throw new ArgumentException("当前密码不能为空", nameof(currentPassword));
        }

        ValidateNewLoginPassword(newPassword, confirmPassword);

        var user = await QueryFirstAsync(u => u.Id == userId && !u.IsDeleted && u.IsEnable);
        if (user == null)
        {
            throw new InvalidOperationException("用户不存在或已禁用");
        }

        if (!PasswordHasher.VerifyPassword(currentPassword, user.VoLoginPassword))
        {
            throw new InvalidOperationException("当前密码不正确");
        }

        var newPasswordHash = PasswordHasher.HashPassword(newPassword);
        var affectedRows = await UpdateColumnsAsync(
            u => new User
            {
                LoginPassword = newPasswordHash,
                CriticalModifyTime = DateTime.Now,
                UpdateTime = DateTime.Now
            },
            u => u.Id == userId && !u.IsDeleted && u.IsEnable);

        if (affectedRows <= 0)
        {
            throw new InvalidOperationException("密码更新失败");
        }
    }

    private static void ValidateNewLoginPassword(string newPassword, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            throw new ArgumentException("新密码不能为空", nameof(newPassword));
        }

        if (newPassword != confirmPassword)
        {
            throw new ArgumentException("两次输入的密码不一致", nameof(confirmPassword));
        }

        if (newPassword.Length < 6)
        {
            throw new ArgumentException("密码长度至少 6 位", nameof(newPassword));
        }

        if (!newPassword.Any(char.IsLower) || !newPassword.Any(char.IsUpper) || !newPassword.Any(char.IsDigit))
        {
            throw new ArgumentException("密码必须包含大小写字母和数字", nameof(newPassword));
        }
    }

    /// <summary>测试使用同事务</summary>
    /// <remarks>仅为示例，无任何作用</remarks>
    /// <returns></returns>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<bool> TestTranPropagationUser()
    {
        var sysUserInfos = await base.QueryAsync();

        TimeSpan timeSpan = DateTime.Now.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var id = timeSpan.TotalSeconds.ObjToLong();
        var insertSysUserInfo = await base.AddAsync(new User()
        {
            Id = id,
            UserName = $"UserName {id}",
            StatusCode = 0,
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now,
            CriticalModifyTime = DateTime.Now,
            LastErrorTime = DateTime.Now,
            ErrorCount = 0,
            IsEnable = true,
            TenantId = 0,
        });

        await _departmentService.TestTranPropagationDepartment();

        return true;
    }

    /// <summary>
    /// 搜索用户（用于@提及功能）
    /// </summary>
    /// <param name="keyword">搜索关键词（匹配用户名）</param>
    /// <param name="tenantId">当前租户 Id</param>
    /// <param name="limit">返回结果数量限制（默认10）</param>
    /// <returns>用户提及视图模型列表</returns>
    public async Task<List<UserMentionVo>> SearchUsersForMentionAsync(string keyword, long tenantId, int limit = 10)
    {
        var search = UserPublicSearchCriteria.Parse(keyword);
        if (search == null)
        {
            return new List<UserMentionVo>();
        }

        var normalizedTenantId = tenantId > 0 ? tenantId : 0;

        // 限制最大查询数量
        if (limit <= 0) limit = 10;
        if (limit > 50) limit = 50;

        // 多查询一些结果用于排序（最多100条）
        var fetchSize = Math.Min(limit * 3, 100);

        var (users, _) = await _userBaseRepository.QueryPageAsync(
            whereExpression: u => u.TenantId == normalizedTenantId
                                  && u.IsEnable
                                  && !u.IsDeleted
                                  && ((search.MatchDisplayName && u.UserName.Contains(search.DisplayNameKeyword))
                                      || (search.MatchPublicIndex && search.PublicIndex != null && u.PublicIndex == search.PublicIndex)
                                      || (search.DisplayHandleName != null &&
                                          search.PublicIndex != null &&
                                          u.UserName == search.DisplayHandleName &&
                                          u.PublicIndex == search.PublicIndex)
                                      || (search.PublicId != null && u.PublicId == search.PublicId)),
            pageIndex: 1,
            pageSize: fetchSize,
            orderByExpression: u => u.UserName,
            orderByType: OrderByType.Asc
        );

        await EnsureUsersPublicIdentityBackfilledAsync(users);
        var data = Mapper.Map<List<UserVo>>(users);

        var sorted = data
            .OrderBy(u => GetMentionMatchRank(u, search))
            .ThenBy(u => u.VoDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(u => u.VoPublicIndex ?? long.MaxValue)
            .Take(limit)
            .ToList();

        var result = Mapper.Map<List<UserMentionVo>>(sorted);
        return result;
    }

    private async Task EnsureUsersPublicIdentityBackfilledAsync(List<User> users)
    {
        foreach (var user in users)
        {
            await EnsureUserPublicIdentityBackfilledAsync(user);
        }
    }

    private static int GetMentionMatchRank(UserVo user, UserPublicSearchCriteria search)
    {
        if (search.DisplayHandleName != null &&
            search.PublicIndex != null &&
            user.VoPublicIndex == search.PublicIndex &&
            string.Equals(user.VoDisplayName, search.DisplayHandleName, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (search.MatchDisplayName &&
            !string.IsNullOrWhiteSpace(user.VoDisplayName) &&
            user.VoDisplayName.StartsWith(search.DisplayNameKeyword, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (search.PublicIndex != null && user.VoPublicIndex == search.PublicIndex)
        {
            return 2;
        }

        if (search.PublicId != null && string.Equals(user.VoPublicId, search.PublicId, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return 4;
    }

    private static bool IsUniqueConstraintConflict(Exception ex, string token)
    {
        var current = ex;
        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) &&
                (current.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("23505", StringComparison.OrdinalIgnoreCase)) &&
                (current.Message.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("idx_user_public_index", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            current = current.InnerException!;
        }

        return false;
    }

    private sealed class UserPublicSearchCriteria
    {
        private UserPublicSearchCriteria(
            string displayNameKeyword,
            string? displayHandleName,
            long? publicIndex,
            string? publicId,
            bool matchDisplayName,
            bool matchPublicIndex)
        {
            DisplayNameKeyword = displayNameKeyword;
            DisplayHandleName = displayHandleName;
            PublicIndex = publicIndex;
            PublicId = publicId;
            MatchDisplayName = matchDisplayName;
            MatchPublicIndex = matchPublicIndex;
        }

        public string DisplayNameKeyword { get; }

        public string? DisplayHandleName { get; }

        public long? PublicIndex { get; }

        public string? PublicId { get; }

        public bool MatchDisplayName { get; }

        public bool MatchPublicIndex { get; }

        public static UserPublicSearchCriteria? Parse(string? keyword)
        {
            var normalizedKeyword = keyword?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                return null;
            }

            var displayNameKeyword = normalizedKeyword;
            string? displayHandleName = null;
            long? publicIndex = null;
            var matchDisplayName = true;
            var matchPublicIndex = false;
            var publicId = User.HasPublicIdFormat(normalizedKeyword)
                ? normalizedKeyword.ToLowerInvariant()
                : null;
            if (publicId != null)
            {
                matchDisplayName = false;
            }

            var separatorIndex = normalizedKeyword.LastIndexOf('#');
            if (separatorIndex > 0 && separatorIndex < normalizedKeyword.Length - 1)
            {
                var displayNamePart = normalizedKeyword[..separatorIndex].Trim();
                var publicIndexPart = normalizedKeyword[(separatorIndex + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(displayNamePart) &&
                    long.TryParse(publicIndexPart, out var parsedHandleIndex) &&
                    parsedHandleIndex > 0)
                {
                    displayHandleName = displayNamePart;
                    displayNameKeyword = displayNamePart;
                    publicIndex = parsedHandleIndex;
                    matchDisplayName = false;
                    matchPublicIndex = false;
                }
            }
            else if (long.TryParse(normalizedKeyword.TrimStart('#'), out var parsedPublicIndex) &&
                     parsedPublicIndex > 0)
            {
                publicIndex = parsedPublicIndex;
                displayNameKeyword = normalizedKeyword.TrimStart('#');
                matchPublicIndex = true;
            }

            return new UserPublicSearchCriteria(
                displayNameKeyword,
                displayHandleName,
                publicIndex,
                publicId,
                matchDisplayName,
                matchPublicIndex);
        }
    }
}
