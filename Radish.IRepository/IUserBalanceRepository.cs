using Radish.IRepository.Base;
using Radish.Model;

namespace Radish.IRepository;

/// <summary>用户余额仓储接口</summary>
public interface IUserBalanceRepository : IBaseRepository<UserBalance>
{
    /// <summary>按用户 ID 查询余额记录（包含已软删除数据）</summary>
    Task<UserBalance?> QueryByUserIdIncludingDeletedAsync(long userId);
}
