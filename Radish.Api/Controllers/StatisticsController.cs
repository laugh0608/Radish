using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.Api.Controllers;

/// <summary>统计报表控制器</summary>
[ApiController]
[ApiErrorContract]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Client)]
public class StatisticsController : ControllerBase
{
    private readonly IStatisticsService _statisticsService;

    public StatisticsController(IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    /// <summary>获取仪表盘统计数据</summary>
    /// <returns>仪表盘统计数据</returns>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.DashboardView)]
    [ProducesResponseType(typeof(MessageModel<DashboardStatsVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel<DashboardStatsVo>> GetDashboardStats()
    {
        try
        {
            var stats = await _statisticsService.GetDashboardStatsAsync();
            return MessageModel<DashboardStatsVo>.Success("获取成功", stats);
        }
        catch (Exception ex)
        {
            throw new BusinessException("获取统计数据失败，请稍后重试", ex, 500, "System.UnexpectedError", "error.system.unexpected_error");
        }
    }

    /// <summary>获取订单趋势数据</summary>
    /// <param name="days">天数，默认30天</param>
    /// <returns>订单趋势数据</returns>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.DashboardView)]
    [ProducesResponseType(typeof(MessageModel<List<OrderTrendItemVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<List<OrderTrendItemVo>>> GetOrderTrend(int days = 30)
    {
        try
        {
            var trendData = await _statisticsService.GetOrderTrendAsync(days);
            return MessageModel<List<OrderTrendItemVo>>.Success("获取成功", trendData);
        }
        catch (Exception ex)
        {
            throw new BusinessException("获取订单趋势失败，请稍后重试", ex, 500, "System.UnexpectedError", "error.system.unexpected_error");
        }
    }

    /// <summary>获取商品销售排行</summary>
    /// <param name="limit">返回数量，默认10</param>
    /// <returns>商品销售排行</returns>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.DashboardView)]
    [ProducesResponseType(typeof(MessageModel<List<ProductSalesRankingVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<List<ProductSalesRankingVo>>> GetProductSalesRanking(int limit = 10)
    {
        try
        {
            var rankingData = await _statisticsService.GetProductSalesRankingAsync(limit);
            return MessageModel<List<ProductSalesRankingVo>>.Success("获取成功", rankingData);
        }
        catch (Exception ex)
        {
            throw new BusinessException("获取商品销售排行失败，请稍后重试", ex, 500, "System.UnexpectedError", "error.system.unexpected_error");
        }
    }

    /// <summary>获取用户等级分布</summary>
    /// <returns>用户等级分布</returns>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.DashboardView)]
    [ProducesResponseType(typeof(MessageModel<List<UserLevelDistributionVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<List<UserLevelDistributionVo>>> GetUserLevelDistribution()
    {
        try
        {
            var distributionData = await _statisticsService.GetUserLevelDistributionAsync();
            return MessageModel<List<UserLevelDistributionVo>>.Success("获取成功", distributionData);
        }
        catch (Exception ex)
        {
            throw new BusinessException("获取用户等级分布失败，请稍后重试", ex, 500, "System.UnexpectedError", "error.system.unexpected_error");
        }
    }
}
