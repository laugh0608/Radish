using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>萝卜币服务接口</summary>
public interface ICoinService : IBaseService<UserBalance, UserBalanceVo>
{
    #region 余额查询

    /// <summary>
    /// 获取用户余额信息
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>用户余额视图模型</returns>
    Task<UserBalanceVo?> GetBalanceAsync(long userId);

    /// <summary>
    /// 批量获取用户余额信息
    /// </summary>
    /// <param name="userIds">用户 ID 列表</param>
    /// <returns>用户余额视图模型列表</returns>
    Task<Dictionary<long, UserBalanceVo>> GetBalancesAsync(List<long> userIds);

    #endregion

    #region 萝卜币发放

    /// <summary>
    /// 发放萝卜币（系统赠送、奖励等）
    /// </summary>
    /// <param name="userId">接收用户 ID</param>
    /// <param name="amount">发放金额（胡萝卜）</param>
    /// <param name="transactionType">交易类型（SYSTEM_GRANT/LIKE_REWARD/COMMENT_REWARD等）</param>
    /// <param name="businessType">业务类型（可选）</param>
    /// <param name="businessId">业务 ID（可选）</param>
    /// <param name="remark">备注（可选）</param>
    /// <returns>交易流水号</returns>
    Task<string> GrantCoinAsync(
        long userId,
        long amount,
        string transactionType,
        string? businessType = null,
        long? businessId = null,
        string? remark = null);

    /// <summary>
    /// 批量发放萝卜币
    /// </summary>
    /// <param name="grantInfos">发放信息列表</param>
    /// <returns>成功发放的交易流水号列表</returns>
    Task<List<string>> BatchGrantCoinAsync(List<CoinGrantInfo> grantInfos);

    #endregion

    #region 交易记录查询

    /// <summary>
    /// 获取用户交易记录（分页）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="transactionType">交易类型（可选，用于筛选）</param>
    /// <param name="status">交易状态（可选，用于筛选）</param>
    /// <returns>分页的交易记录</returns>
    Task<PageModel<CoinTransactionVo>> GetTransactionsAsync(
        long userId,
        int pageIndex,
        int pageSize,
        string? transactionType = null,
        string? status = null);

    /// <summary>
    /// 根据交易流水号获取交易详情
    /// </summary>
    /// <param name="transactionNo">交易流水号</param>
    /// <returns>交易记录视图模型</returns>
    Task<CoinTransactionVo?> GetTransactionByNoAsync(string transactionNo);

    #endregion

    #region 管理员操作

    /// <summary>
    /// 管理员调整用户余额
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="deltaAmount">变动金额（正数=增加，负数=减少）</param>
    /// <param name="reason">调整原因</param>
    /// <param name="operatorId">操作员 ID</param>
    /// <param name="operatorName">操作员名称</param>
    /// <returns>交易流水号</returns>
    Task<string> AdminAdjustBalanceAsync(
        long userId,
        long deltaAmount,
        string reason,
        long operatorId,
        string operatorName);

    #endregion
}

/// <summary>
/// 萝卜币发放信息
/// </summary>
public class CoinGrantInfo
{
    /// <summary>接收用户 ID</summary>
    public long UserId { get; set; }

    /// <summary>发放金额（胡萝卜）</summary>
    public long Amount { get; set; }

    /// <summary>交易类型</summary>
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>业务类型（可选）</summary>
    public string? BusinessType { get; set; }

    /// <summary>业务 ID（可选）</summary>
    public long? BusinessId { get; set; }

    /// <summary>备注（可选）</summary>
    public string? Remark { get; set; }
}
