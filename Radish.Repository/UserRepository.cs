using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>用户仓储</summary>
public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public Task<ConsoleUserPageResult> QueryConsolePageAsync(ConsoleUserPageQuery query)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var source = CreateTenantQueryableFor<User>()
                .Where(user => !user.IsDeleted);
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var normalizedKeyword = query.Keyword.ToLowerInvariant();
                source = source.Where(user =>
                    user.UserName.ToLower().Contains(normalizedKeyword) ||
                    (user.PublicId != null && user.PublicId.ToLower().Contains(normalizedKeyword)) ||
                    (user.UserEmail != null && user.UserEmail.ToLower().Contains(normalizedKeyword)));
            }

            if (query.IsEnabled.HasValue)
            {
                var enabled = query.IsEnabled.Value;
                source = source.Where(user => user.IsEnable == enabled);
            }

            if (!string.IsNullOrWhiteSpace(query.RoleName))
            {
                var normalizedRoleName = query.RoleName.ToLowerInvariant();
                source = source.Where(user =>
                    SqlFunc.Subqueryable<UserRole>()
                        .Where(relation =>
                            relation.UserId == user.Id &&
                            !relation.IsDeleted &&
                            SqlFunc.Subqueryable<Role>()
                                .Where(role =>
                                    role.Id == relation.RoleId &&
                                    role.RoleName.Trim().ToLower() == normalizedRoleName &&
                                    role.IsEnabled &&
                                    !role.IsDeleted)
                                .Any())
                        .Any());
            }

            RefAsync<int> total = 0;
            var items = await source
                .OrderByDescending(user => user.Id)
                .ToPageListAsync(query.PageIndex, query.PageSize, total);
            return new ConsoleUserPageResult(items, total.Value);
        });
    }

    public Task<IReadOnlyDictionary<long, IReadOnlyList<string>>> GetRoleNamesByUserIdsAsync(
        IReadOnlyCollection<long> userIds)
    {
        var normalizedUserIds = userIds.Where(userId => userId > 0).Distinct().ToList();
        if (normalizedUserIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyDictionary<long, IReadOnlyList<string>>>(
                new Dictionary<long, IReadOnlyList<string>>());
        }

        return ExecuteDbOperationAsync(async () =>
        {
            var rows = await DbProtectedClient
                .Queryable<UserRole, Role>((relation, role) => new JoinQueryInfos(
                    JoinType.Inner,
                    relation.RoleId == role.Id))
                .Where((relation, role) =>
                    normalizedUserIds.Contains(relation.UserId) &&
                    !relation.IsDeleted &&
                    !role.IsDeleted &&
                    role.IsEnabled)
                .Select((relation, role) => new UserRoleNameProjection
                {
                    UserId = relation.UserId,
                    RoleName = role.RoleName
                })
                .ToListAsync();

            return (IReadOnlyDictionary<long, IReadOnlyList<string>>)rows
                .Where(row => !string.IsNullOrWhiteSpace(row.RoleName))
                .GroupBy(row => row.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group
                        .Select(row => row.RoleName.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(roleName => roleName, StringComparer.OrdinalIgnoreCase)
                        .ToList());
        });
    }

    public async Task<IReadOnlyList<long>> GetActiveUserIdsAsync(
        long tenantId,
        IReadOnlyCollection<long> userIds)
    {
        var normalizedIds = userIds.Where(userId => userId > 0).Distinct().ToList();
        if (normalizedIds.Count == 0)
        {
            return [];
        }

        return await QueryDistinctAsync(
            user => user.Id,
            user =>
                user.TenantId == tenantId &&
                normalizedIds.Contains(user.Id) &&
                user.IsEnable &&
                !user.IsDeleted);
    }

    private sealed class UserRoleNameProjection
    {
        public long UserId { get; init; }

        public string RoleName { get; init; } = string.Empty;
    }

    // /// <summary>
    // /// 获取所有的 角色-API 关系
    // /// </summary>
    // /// <returns>List RoleModulePermission</returns>
    // public async Task<List<RoleModulePermission>> RoleModuleMaps()
    // {
    //     return await QueryMuchAsync<RoleModulePermission, ApiModule, Role, RoleModulePermission>(
    //         (rmp, m, r) => new object[]
    //         {
    //             JoinType.Left, rmp.ApiModuleId == m.Id,
    //             JoinType.Left, rmp.RoleId == r.Id
    //         },
    //         (rmp, m, r) => new RoleModulePermission()
    //         {
    //             Role = r,
    //             ApiModule = m,
    //             IsDeleted = rmp.IsDeleted
    //         },
    //         (rmp, m, r) => rmp.IsDeleted == false && m.IsDeleted == false && r.IsDeleted == false
    //     );
    // }
}
