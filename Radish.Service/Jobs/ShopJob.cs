using Radish.IRepository;
using Radish.Model;
using Radish.Shared.CustomEnum;
using Serilog;

namespace Radish.Service.Jobs;

/// <summary>
/// 商城定时任务
/// </summary>
/// <remarks>
/// 负责处理订单超时取消和权益过期标记
/// </remarks>
public class ShopJob
{
    private readonly IBaseRepository<Order> _orderRepository;
    private readonly IBaseRepository<UserBenefit> _benefitRepository;

    public ShopJob(
        IBaseRepository<Order> orderRepository,
        IBaseRepository<UserBenefit> benefitRepository)
    {
        _orderRepository = orderRepository;
        _benefitRepository = benefitRepository;
    }

    #region 订单超时取消

    /// <summary>
    /// 取消超时未支付的订单
    /// </summary>
    /// <param name="timeoutMinutes">超时时间（分钟，默认 30 分钟）</param>
    /// <returns>取消的订单数量</returns>
    public async Task<int> CancelTimeoutOrdersAsync(int timeoutMinutes = 30)
    {
        try
        {
            Log.Information("[ShopJob] 开始处理超时订单，超时时间：{TimeoutMinutes} 分钟", timeoutMinutes);

            var cutoffTime = DateTime.Now.AddMinutes(-timeoutMinutes);

            // 查询超时的待支付订单（排除软删除的记录）
            var timeoutOrders = await _orderRepository.QueryAsync(o =>
                o.Status == OrderStatus.Pending &&
                o.CreateTime < cutoffTime &&
                !o.IsDeleted);

            if (timeoutOrders == null || timeoutOrders.Count == 0)
            {
                Log.Information("[ShopJob] 没有需要取消的超时订单");
                return 0;
            }

            var cancelledCount = 0;

            foreach (var order in timeoutOrders)
            {
                try
                {
                    order.Status = OrderStatus.Cancelled;
                    order.CancelledTime = DateTime.Now;
                    order.CancelReason = $"订单超时自动取消（超过 {timeoutMinutes} 分钟未支付）";
                    order.ModifyTime = DateTime.Now;
                    order.ModifyBy = "System";

                    await _orderRepository.UpdateAsync(order);
                    cancelledCount++;

                    Log.Information("[ShopJob] 已取消超时订单：{OrderNo}，创建时间：{CreateTime}",
                        order.OrderNo, order.CreateTime);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[ShopJob] 取消订单失败：{OrderNo}", order.OrderNo);
                }
            }

            Log.Information("[ShopJob] 超时订单处理完成，共取消 {Count} 个订单", cancelledCount);
            return cancelledCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ShopJob] 处理超时订单时发生异常");
            return 0;
        }
    }

    #endregion

    #region 权益过期处理

    /// <summary>
    /// 标记已过期的权益
    /// </summary>
    /// <returns>标记的权益数量</returns>
    public async Task<int> MarkExpiredBenefitsAsync()
    {
        try
        {
            Log.Information("[ShopJob] 开始处理过期权益");

            var now = DateTime.Now;

            // 查询已到期但未标记过期的权益
            var expiredBenefits = await _benefitRepository.QueryAsync(b =>
                !b.IsExpired &&
                b.ExpiresAt.HasValue &&
                b.ExpiresAt.Value < now);

            if (expiredBenefits == null || expiredBenefits.Count == 0)
            {
                Log.Information("[ShopJob] 没有需要标记的过期权益");
                return 0;
            }

            var markedCount = 0;

            foreach (var benefit in expiredBenefits)
            {
                try
                {
                    benefit.IsExpired = true;
                    // 如果权益正在激活中，自动取消激活
                    if (benefit.IsActive)
                    {
                        benefit.IsActive = false;
                        Log.Information("[ShopJob] 权益 {BenefitId} 已过期，自动取消激活", benefit.Id);
                    }
                    benefit.ModifyTime = DateTime.Now;
                    benefit.ModifyBy = "System";

                    await _benefitRepository.UpdateAsync(benefit);
                    markedCount++;

                    Log.Information("[ShopJob] 已标记权益过期：{BenefitId}，用户：{UserId}，类型：{BenefitType}，到期时间：{ExpiresAt}",
                        benefit.Id, benefit.UserId, benefit.BenefitType, benefit.ExpiresAt);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[ShopJob] 标记权益过期失败：{BenefitId}", benefit.Id);
                }
            }

            Log.Information("[ShopJob] 过期权益处理完成，共标记 {Count} 个权益", markedCount);
            return markedCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ShopJob] 处理过期权益时发生异常");
            return 0;
        }
    }

    #endregion

    #region 统计报告

    /// <summary>
    /// 生成每日商城统计报告
    /// </summary>
    /// <returns>统计结果</returns>
    public async Task<ShopDailyStats> GenerateDailyStatsAsync()
    {
        try
        {
            Log.Information("[ShopJob] 开始生成每日商城统计");

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // 统计今日订单（排除软删除的记录）
            var todayOrders = await _orderRepository.QueryAsync(o =>
                o.CreateTime >= today && o.CreateTime < tomorrow && !o.IsDeleted);

            var stats = new ShopDailyStats
            {
                Date = today,
                TotalOrders = todayOrders?.Count ?? 0,
                CompletedOrders = todayOrders?.Count(o => o.Status == OrderStatus.Completed) ?? 0,
                CancelledOrders = todayOrders?.Count(o => o.Status == OrderStatus.Cancelled) ?? 0,
                FailedOrders = todayOrders?.Count(o => o.Status == OrderStatus.Failed) ?? 0,
                TotalRevenue = todayOrders?.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalPrice) ?? 0
            };

            Log.Information("[ShopJob] 每日统计完成：日期={Date}，总订单={TotalOrders}，完成={Completed}，取消={Cancelled}，失败={Failed}，收入={Revenue}",
                stats.Date.ToString("yyyy-MM-dd"),
                stats.TotalOrders,
                stats.CompletedOrders,
                stats.CancelledOrders,
                stats.FailedOrders,
                stats.TotalRevenue);

            return stats;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ShopJob] 生成每日统计时发生异常");
            return new ShopDailyStats { Date = DateTime.Today };
        }
    }

    #endregion
}

/// <summary>
/// 商城每日统计数据
/// </summary>
public class ShopDailyStats
{
    /// <summary>统计日期</summary>
    public DateTime Date { get; set; }

    /// <summary>总订单数</summary>
    public int TotalOrders { get; set; }

    /// <summary>完成订单数</summary>
    public int CompletedOrders { get; set; }

    /// <summary>取消订单数</summary>
    public int CancelledOrders { get; set; }

    /// <summary>失败订单数</summary>
    public int FailedOrders { get; set; }

    /// <summary>总收入（胡萝卜）</summary>
    public long TotalRevenue { get; set; }
}
