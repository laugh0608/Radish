namespace Radish.IService;

/// <summary>
/// 通知去重服务接口
/// </summary>
/// <remarks>
/// 防止短时间内向同一用户发送重复的相同类型通知
/// 主要用于点赞等高频操作的通知合并
/// </remarks>
public interface INotificationDedupService
{
    /// <summary>
    /// 检查通知是否应该被去重（是否在去重窗口内）
    /// </summary>
    /// <param name="userId">接收者用户 ID</param>
    /// <param name="notificationType">通知类型</param>
    /// <param name="businessId">业务 ID（如帖子 ID、评论 ID）</param>
    /// <returns>true: 应该去重（不发送）; false: 不去重（可发送）</returns>
    Task<bool> ShouldDedupAsync(long userId, string notificationType, long businessId);

    /// <summary>
    /// 记录通知去重键（发送通知后调用）
    /// </summary>
    /// <param name="userId">接收者用户 ID</param>
    /// <param name="notificationType">通知类型</param>
    /// <param name="businessId">业务 ID</param>
    /// <param name="windowSeconds">去重窗口时间（秒），默认 300 秒（5 分钟）</param>
    Task RecordDedupKeyAsync(long userId, string notificationType, long businessId, int windowSeconds = 300);

    /// <summary>
    /// 清除通知去重键（用于测试或手动清除）
    /// </summary>
    /// <param name="userId">接收者用户 ID</param>
    /// <param name="notificationType">通知类型</param>
    /// <param name="businessId">业务 ID</param>
    Task ClearDedupKeyAsync(long userId, string notificationType, long businessId);
}
