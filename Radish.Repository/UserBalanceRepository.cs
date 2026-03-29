using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;

namespace Radish.Repository;

/// <summary>用户余额仓储</summary>
public class UserBalanceRepository : BaseRepository<UserBalance>, IUserBalanceRepository
{
    public UserBalanceRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<UserBalance?> QueryByUserIdIncludingDeletedAsync(long userId)
    {
        return await CreateTenantQueryableFor<UserBalance>(includeDeleted: true)
            .FirstAsync(balance => balance.UserId == userId);
    }
}
