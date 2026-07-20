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
