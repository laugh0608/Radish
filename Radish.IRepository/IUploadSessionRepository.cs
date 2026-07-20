using Radish.IRepository.Base;
using Radish.Model.Models;

namespace Radish.IRepository;

/// <summary>
/// 分片上传会话仓储。
/// </summary>
public interface IUploadSessionRepository : IBaseRepository<UploadSession>
{
    /// <summary>
    /// 跨租户查询后台清理任务需要处理的过期会话。
    /// </summary>
    Task<List<UploadSession>> QueryExpiredAcrossTenantsAsync(DateTime expiredBeforeUtc);

    /// <summary>
    /// 跨租户读取临时目录对应的会话，用于后台终态与孤儿目录对账。
    /// </summary>
    Task<List<UploadSession>> QueryBySessionIdsAcrossTenantsAsync(List<string> sessionIds);

    /// <summary>
    /// 跨租户读取近期终态会话，供幂等重放配额结算。
    /// </summary>
    Task<List<UploadSession>> QueryTerminalForSettlementAcrossTenantsAsync(
        DateTime modifiedAfterUtc,
        int maxCount);

    /// <summary>
    /// 在确认会话仍处于可过期状态后，跨租户原子标记为过期。
    /// </summary>
    Task<bool> TryMarkExpiredAcrossTenantsAsync(
        string sessionId,
        long tenantId,
        long userId,
        DateTime expiredBeforeUtc,
        DateTime modifiedAtUtc);
}
