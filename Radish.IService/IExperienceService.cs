using Radish.IService.Base;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>经验值服务接口</summary>
public interface IExperienceService : IBaseService<UserExperience, UserExperienceVo>
{
    #region 经验值查询

    /// <summary>
    /// 获取用户经验值信息
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>用户经验值视图模型</returns>
    Task<UserExperienceVo?> GetUserExperienceAsync(long userId);

    /// <summary>
    /// 批量获取用户经验值信息
    /// </summary>
    /// <param name="userIds">用户 ID 列表</param>
    /// <returns>用户经验值视图模型字典</returns>
    Task<Dictionary<long, UserExperienceVo>> GetUserExperiencesAsync(List<long> userIds);

    #endregion

    #region 经验值发放

    /// <summary>
    /// 发放经验值
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="amount">经验值数量</param>
    /// <param name="expType">经验值类型</param>
    /// <param name="businessType">业务类型（可选）</param>
    /// <param name="businessId">业务 ID（可选）</param>
    /// <param name="remark">备注（可选）</param>
    /// <returns>是否成功</returns>
    Task<bool> GrantExperienceAsync(
        long userId,
        int amount,
        string expType,
        string? businessType = null,
        long? businessId = null,
        string? remark = null);

    /// <summary>
    /// 批量发放经验值
    /// </summary>
    /// <param name="grantInfos">发放信息列表</param>
    /// <returns>成功发放的数量</returns>
    Task<int> BatchGrantExperienceAsync(List<ExpGrantInfo> grantInfos);

    #endregion

    #region 等级配置

    /// <summary>
    /// 获取所有等级配置
    /// </summary>
    /// <returns>等级配置列表</returns>
    Task<List<LevelConfigVo>> GetLevelConfigsAsync();

    /// <summary>
    /// 获取指定等级的配置
    /// </summary>
    /// <param name="level">等级</param>
    /// <returns>等级配置</returns>
    Task<LevelConfigVo?> GetLevelConfigAsync(int level);

    /// <summary>
    /// 根据累计经验值计算等级
    /// </summary>
    /// <param name="totalExp">累计经验值</param>
    /// <returns>(等级, 当前等级内的经验值)</returns>
    Task<(int level, long currentExp)> CalculateLevelAsync(long totalExp);

    #endregion

    #region 交易记录查询

    /// <summary>
    /// 获取用户经验值交易记录（分页）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="expType">经验值类型（可选，用于筛选）</param>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <returns>分页的交易记录</returns>
    Task<PageModel<ExpTransactionVo>> GetTransactionsAsync(
        long userId,
        int pageIndex,
        int pageSize,
        string? expType = null,
        DateTime? startDate = null,
        DateTime? endDate = null);

    #endregion

    #region 每日统计

    /// <summary>
    /// 获取用户每日经验值统计
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="days">查询最近N天（默认 7 天，最大 30 天）</param>
    /// <returns>每日统计列表</returns>
    Task<List<UserExpDailyStatsVo>> GetDailyStatsAsync(long userId, int days = 7);

    /// <summary>
    /// 更新每日统计（内部方法，经验值发放时调用）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="expType">经验值类型</param>
    /// <param name="amount">经验值数量</param>
    /// <param name="statDate">统计日期</param>
    Task UpdateDailyStatsAsync(long userId, string expType, int amount, DateTime statDate);

    #endregion

    #region 排行榜

    /// <summary>
    /// 获取等级排行榜
    /// </summary>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量（默认 50，最大 100）</param>
    /// <param name="currentUserId">当前用户 ID（可选，用于标记当前用户）</param>
    /// <returns>分页的排行榜</returns>
    Task<PageModel<LeaderboardItemVo>> GetLeaderboardAsync(
        int pageIndex,
        int pageSize = 50,
        long? currentUserId = null);

    /// <summary>
    /// 获取用户排名
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>用户排名（0 表示未上榜）</returns>
    Task<int> GetUserRankAsync(long userId);

    #endregion

    #region 管理员操作

    /// <summary>
    /// 管理员调整用户经验值
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="deltaExp">变动经验值（正数=增加，负数=减少）</param>
    /// <param name="reason">调整原因</param>
    /// <param name="operatorId">操作员 ID</param>
    /// <param name="operatorName">操作员名称</param>
    /// <returns>是否成功</returns>
    Task<bool> AdminAdjustExperienceAsync(
        long userId,
        int deltaExp,
        string reason,
        long operatorId,
        string operatorName);

    /// <summary>
    /// 冻结用户经验值
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="frozenUntil">冻结到期时间（NULL 表示永久冻结）</param>
    /// <param name="reason">冻结原因</param>
    /// <returns>是否成功</returns>
    Task<bool> FreezeExperienceAsync(long userId, DateTime? frozenUntil, string reason);

    /// <summary>
    /// 解冻用户经验值
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>是否成功</returns>
    Task<bool> UnfreezeExperienceAsync(long userId);

    /// <summary>
    /// 管理员重新计算并更新所有等级配置（根据当前配置文件）
    /// </summary>
    /// <param name="operatorId">操作员 ID</param>
    /// <param name="operatorName">操作员名称</param>
    /// <returns>更新的等级配置列表</returns>
    Task<List<LevelConfigVo>> RecalculateLevelConfigsAsync(long operatorId, string operatorName);

    #endregion
}

/// <summary>
/// 经验值发放信息
/// </summary>
public class ExpGrantInfo
{
    /// <summary>用户 ID</summary>
    public long UserId { get; set; }

    /// <summary>经验值数量</summary>
    public int Amount { get; set; }

    /// <summary>经验值类型</summary>
    public string ExpType { get; set; } = string.Empty;

    /// <summary>业务类型（可选）</summary>
    public string? BusinessType { get; set; }

    /// <summary>业务 ID（可选）</summary>
    public long? BusinessId { get; set; }

    /// <summary>备注（可选）</summary>
    public string? Remark { get; set; }
}
