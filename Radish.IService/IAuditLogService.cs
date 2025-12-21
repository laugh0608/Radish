using Radish.Model.LogModels;
using Radish.Model.ViewModels;
using Radish.Model;

namespace Radish.IService;

/// <summary>
/// 审计日志服务接口
/// </summary>
public interface IAuditLogService : IBaseService<AuditLog, AuditLogVo>
{
    /// <summary>
    /// 分页查询审计日志
    /// </summary>
    /// <param name="queryDto">查询参数</param>
    /// <returns>分页结果</returns>
    Task<PageModel<AuditLogVo>> QueryPageAsync(AuditLogQueryDto queryDto);

    /// <summary>
    /// 根据用户 ID 查询审计日志
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>分页结果</returns>
    Task<PageModel<AuditLogVo>> QueryByUserIdAsync(long userId, int pageIndex = 1, int pageSize = 20);

    /// <summary>
    /// 根据操作类型查询审计日志
    /// </summary>
    /// <param name="operationType">操作类型</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>分页结果</returns>
    Task<PageModel<AuditLogVo>> QueryByOperationTypeAsync(string operationType, int pageIndex = 1, int pageSize = 20);

    /// <summary>
    /// 根据时间范围查询审计日志
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>分页结果</returns>
    Task<PageModel<AuditLogVo>> QueryByTimeRangeAsync(DateTime startTime, DateTime endTime, int pageIndex = 1, int pageSize = 20);

    /// <summary>
    /// 获取操作类型统计
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <returns>操作类型统计</returns>
    Task<Dictionary<string, int>> GetOperationTypeStatisticsAsync(DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// 获取用户操作统计
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="topN">Top N 用户</param>
    /// <returns>用户操作统计</returns>
    Task<Dictionary<string, int>> GetUserStatisticsAsync(DateTime? startTime = null, DateTime? endTime = null, int topN = 10);
}
