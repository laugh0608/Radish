using System.Threading;
using Radish.IService;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Shared.CustomEnum;
using Radish.Common.TimeTool;

namespace Radish.Service.Jobs;

/// <summary>
/// 商城定时任务
/// </summary>
/// <remarks>
/// 负责处理订单超时取消和权益过期标记
/// </remarks>
public class ShopJob
{
    private static readonly SemaphoreSlim TimeoutOrderCancellationLock = new(1, 1);

    private readonly IBaseRepository<Order> _orderRepository;
    private readonly IOrderService _orderService;
    private readonly IUserBenefitService _userBenefitService;
    private readonly TimeProvider _timeProvider;
    private readonly BusinessCalendar _businessCalendar;

    public ShopJob(
        IBaseRepository<Order> orderRepository,
        IOrderService orderService,
        IUserBenefitService userBenefitService,
        TimeProvider timeProvider,
        BusinessCalendar businessCalendar)
    {
        _orderRepository = orderRepository;
        _orderService = orderService;
        _userBenefitService = userBenefitService;
        _timeProvider = timeProvider;
        _businessCalendar = businessCalendar;
    }

    #region 订单超时取消

    /// <summary>
    /// 取消超时未支付的订单
    /// </summary>
    /// <param name="timeoutMinutes">超时时间（分钟，默认 30 分钟）</param>
    /// <returns>取消的订单数量</returns>
    public async Task<int> CancelTimeoutOrdersAsync(int timeoutMinutes = 30)
    {
        if (!await TimeoutOrderCancellationLock.WaitAsync(0))
        {
            return 0;
        }

        var summary = new BusinessJobSummary("shop-timeouts");
        try
        {
            var effectiveTimeoutMinutes = timeoutMinutes > 0 ? timeoutMinutes : 30;

            var cutoffTime = GetUtcNow().AddMinutes(-effectiveTimeoutMinutes);

            // 先获取待处理订单 ID 快照，避免后台任务长时间持有完整订单读取结果。
            var timeoutOrderIds = await _orderRepository.QueryDistinctAsync(
                o => o.Id,
                o =>
                    o.Status == OrderStatus.Pending &&
                    o.CreateTime < cutoffTime &&
                    !o.IsDeleted);

            if (timeoutOrderIds.Count == 0)
            {
                return 0;
            }

            var cancelledCount = 0;

            foreach (var orderId in timeoutOrderIds)
            {
                try
                {
                    var reason = $"订单超时自动取消（超过 {effectiveTimeoutMinutes} 分钟未支付）";
                    var changed = await _orderService.CancelOrderBySystemAsync(orderId, reason);
                    summary.ProcessedCount++;
                    if (changed) summary.UpdatedCount++;
                    cancelledCount++;
                }
                catch (InvalidOperationException)
                {
                    summary.RejectedCount++;
                }
                catch (Exception ex)
                {
                    summary.RecordFailure(ex);
                }
            }

            return cancelledCount;
        }
        catch (Exception ex)
        {
            summary.RecordFailure(ex);

            return 0;
        }
        finally
        {
            TimeoutOrderCancellationLock.Release();
            summary.Write();
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
        var summary = new BusinessJobSummary("shop-benefits");
        try
        {
            var expiredBenefitIds = await _userBenefitService.GetDueBenefitIdsAsync();

            if (expiredBenefitIds.Count == 0)
            {
                return 0;
            }

            var markedCount = 0;

            foreach (var benefitId in expiredBenefitIds)
            {
                try
                {
                    if (await _userBenefitService.ExpireBenefitAsync(benefitId))
                    {
                        markedCount++;
                        summary.UpdatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    summary.RecordFailure(ex);
                }
            }

            return markedCount;
        }
        catch (Exception ex)
        {
            summary.RecordFailure(ex);

            return 0;
        }
        finally
        {
            summary.Write();
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
        var summary = new BusinessJobSummary("shop-stats");
        try
        {
            var today = _businessCalendar.GetCurrentDate();
            var (startUtc, endUtc) = _businessCalendar.GetUtcRange(today);

            // 统计今日订单（排除软删除的记录）
            var todayOrders = await _orderRepository.QueryAsync(o =>
                o.CreateTime >= startUtc && o.CreateTime < endUtc && !o.IsDeleted);

            var stats = new ShopDailyStats
            {
                Date = today.ToDateTime(TimeOnly.MinValue),
                TotalOrders = todayOrders?.Count ?? 0,
                CompletedOrders = todayOrders?.Count(o => o.Status == OrderStatus.Completed) ?? 0,
                CancelledOrders = todayOrders?.Count(o => o.Status == OrderStatus.Cancelled) ?? 0,
                FailedOrders = todayOrders?.Count(o => o.Status == OrderStatus.Failed) ?? 0,
                TotalRevenue = todayOrders?.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalPrice) ?? 0
            };

            summary.ProcessedCount = stats.TotalOrders;
            return stats;
        }
        catch (Exception ex)
        {
            summary.RecordFailure(ex);

            return new ShopDailyStats
            {
                Date = _businessCalendar.GetCurrentDate().ToDateTime(TimeOnly.MinValue)
            };
        }
        finally
        {
            summary.Write();
        }
    }

    #endregion

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }
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
