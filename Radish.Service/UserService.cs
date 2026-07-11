using AutoMapper;
using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.Common.HelpTool;
using Radish.Common.PermissionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
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
    private readonly IBaseRepository<UserDisplayNameChangeRecord> _displayNameChangeRecordRepository;
    private readonly IConsoleAuthorizationService _consoleAuthorizationService;
    private readonly ISystemSettingProvider _systemSettingProvider;

    public UserService(IMapper mapper,
        IBaseRepository<User> baseRepository, IUserRepository userRepository, IBaseRepository<Role> roleRepository,
        IBaseRepository<UserRole> userRoleRepository,
        IBaseRepository<UserDisplayNameChangeRecord> displayNameChangeRecordRepository,
        IConsoleAuthorizationService consoleAuthorizationService,
        ISystemSettingProvider systemSettingProvider) : base(mapper, baseRepository)
    {
        _userBaseRepository = baseRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _displayNameChangeRecordRepository = displayNameChangeRecordRepository;
        _consoleAuthorizationService = consoleAuthorizationService;
        _systemSettingProvider = systemSettingProvider;
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
        var publicIndexBaseline = await ResolvePublicIndexBaselineAsync();
        var reservationPolicy = await GetPublicIndexReservationPolicyAsync();
        foreach (var entity in entities)
        {
            entity.PublicId = User.EnsurePublicId(entity.PublicId);
            if (!User.HasAssignedPublicIndex(entity.PublicIndex))
            {
                entity.PublicIndex = reservationPolicy.FindNextAvailableAfter(publicIndexBaseline);
                publicIndexBaseline = entity.PublicIndex.Value;
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

    /// <summary>
    /// 修改用户公开展示名，并记录变更审计。
    /// </summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<bool> ChangeDisplayNameAsync(long userId, string displayName, UserDisplayNameChangeContext context)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("用户 ID 无效", nameof(userId));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var normalizedDisplayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDisplayName))
        {
            throw new ArgumentException("显示名不能为空", nameof(displayName));
        }

        await ValidateDisplayNameAsync(normalizedDisplayName);

        var user = await _userBaseRepository.QueryFirstAsync(u => u.Id == userId && !u.IsDeleted && u.IsEnable);
        if (user == null)
        {
            throw new InvalidOperationException("用户不存在或已禁用");
        }

        var storedDisplayName = user.UserName?.Trim() ?? string.Empty;
        if (string.Equals(storedDisplayName, normalizedDisplayName, StringComparison.Ordinal))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        await EnsureDisplayNameChangeAllowedAsync(user, now);

        var affectedRows = await _userBaseRepository.UpdateColumnsAsync(
            item => new User
            {
                UserName = normalizedDisplayName,
                UpdateTime = now
            },
            item => item.Id == userId && !item.IsDeleted && item.IsEnable);

        if (affectedRows <= 0)
        {
            throw new InvalidOperationException("显示名更新失败");
        }

        var operatorUserId = context.OperatorUserId > 0 ? context.OperatorUserId : userId;
        var operatorUserName = NormalizeAuditText(context.OperatorUserName, User.NormalizeDisplayName(user.UserName, user.Id), 100);
        var reason = NormalizeAuditText(context.Reason, "用户个人资料修改", 500);
        var source = NormalizeAuditText(context.Source, UserDisplayNameChangeSources.Profile, 50);

        await _displayNameChangeRecordRepository.AddAsync(new UserDisplayNameChangeRecord
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            OldDisplayName = User.NormalizeDisplayName(user.UserName, user.Id),
            NewDisplayName = normalizedDisplayName,
            OperatorUserId = operatorUserId,
            OperatorUserName = operatorUserName,
            ChangeSource = source,
            Reason = reason,
            ChangeTime = now,
            CreateTime = now,
            CreateBy = NormalizeAuditText(operatorUserName, "System", 50),
            CreateId = operatorUserId
        });

        return true;
    }

    private async Task ValidateDisplayNameAsync(string displayName)
    {
        var minLength = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.DisplayNameMinLengthKey);
        var maxLength = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.DisplayNameMaxLengthKey);
        if (minLength > maxLength)
        {
            throw new InvalidOperationException("展示名长度系统设置无效：最小长度不能大于最大长度");
        }

        if (displayName.Length < minLength || displayName.Length > maxLength)
        {
            throw new ArgumentException($"展示名长度必须在 {minLength}-{maxLength} 个字符之间", nameof(displayName));
        }

        if (!displayName.All(IsValidDisplayNameCharacter))
        {
            throw new ArgumentException("展示名只能包含中文、英文字母和数字", nameof(displayName));
        }
    }

    private static bool IsValidDisplayNameCharacter(char value)
    {
        return (value >= '0' && value <= '9') ||
               (value >= 'a' && value <= 'z') ||
               (value >= 'A' && value <= 'Z') ||
               (value >= '\u4e00' && value <= '\u9fff');
    }

    private async Task EnsureDisplayNameChangeAllowedAsync(User user, DateTime now)
    {
        var cooldownDays = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.DisplayNameChangeCooldownDaysKey);
        var windowDays = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.DisplayNameChangeWindowDaysKey);
        var windowMaxCount = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.DisplayNameChangeWindowMaxCountKey);

        if (cooldownDays < 0 || windowDays < 0 || windowMaxCount < 0)
        {
            throw new InvalidOperationException("展示名修改频率系统设置无效：数值不能为负数");
        }

        if (cooldownDays > 0)
        {
            var latestRecords = await _displayNameChangeRecordRepository.QueryWithOrderAsync(
                item => item.TenantId == user.TenantId && item.UserId == user.Id,
                item => item.ChangeTime,
                OrderByType.Desc,
                1);
            var latestRecord = latestRecords.FirstOrDefault();
            if (latestRecord != null)
            {
                var nextAllowedTime = latestRecord.ChangeTime.AddDays(cooldownDays);
                if (nextAllowedTime > now)
                {
                    throw new InvalidOperationException($"显示名修改过于频繁，请在 {nextAllowedTime:yyyy-MM-dd HH:mm:ss} 后再修改");
                }
            }
        }

        if (windowDays <= 0 || windowMaxCount <= 0)
        {
            return;
        }

        var windowStartTime = now.AddDays(-windowDays);
        var changeCount = await _displayNameChangeRecordRepository.QueryCountAsync(item =>
            item.TenantId == user.TenantId &&
            item.UserId == user.Id &&
            item.ChangeTime >= windowStartTime &&
            item.ChangeTime <= now);

        if (changeCount >= windowMaxCount)
        {
            throw new InvalidOperationException($"显示名在 {windowDays} 天内最多可修改 {windowMaxCount} 次");
        }
    }

    private static string NormalizeAuditText(string? value, string fallback, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
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
        var baseline = await ResolvePublicIndexBaselineAsync(floor);
        var reservationPolicy = await GetPublicIndexReservationPolicyAsync();
        return reservationPolicy.FindNextAvailableAfter(baseline);
    }

    private async Task<long> ResolvePublicIndexBaselineAsync(long? floor = null)
    {
        var maxPublicIndexTask = _userBaseRepository.QueryMaxAsync<long?>(
            item => item.PublicIndex,
            item => item.PublicIndex >= User.PublicIndexStart);
        var maxPublicIndex = maxPublicIndexTask == null ? null : await maxPublicIndexTask;
        return Math.Max(
            maxPublicIndex.GetValueOrDefault(User.PublicIndexStart - 1),
            floor.GetValueOrDefault(User.PublicIndexStart - 1));
    }

    private async Task<PublicIndexReservationPolicy> GetPublicIndexReservationPolicyAsync()
    {
        var reservedIndexes = await _systemSettingProvider.GetEffectiveValueAsync(
            SystemConfigDefaults.PublicIndexReservedIndexesKey);
        var vanityRules = await _systemSettingProvider.GetEffectiveValueAsync(
            SystemConfigDefaults.PublicIndexVanityRulesKey);
        return PublicIndexReservationPolicy.FromSettings(reservedIndexes, vanityRules);
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
