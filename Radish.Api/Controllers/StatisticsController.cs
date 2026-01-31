using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>统计报表控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Authorize(Policy = "SystemOrAdmin")]
public class StatisticsController : ControllerBase
{
    private readonly IBaseService<User, UserVo> _userService;
    private readonly IBaseService<Order, OrderVo> _orderService;
    private readonly IBaseService<Product, ProductVo> _productService;

    public StatisticsController(
        IBaseService<User, UserVo> userService,
        IBaseService<Order, OrderVo> orderService,
        IBaseService<Product, ProductVo> productService)
    {
        _userService = userService;
        _orderService = orderService;
        _productService = productService;
    }

    /// <summary>获取仪表盘统计数据</summary>
    /// <returns>仪表盘统计数据</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<DashboardStatsVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel<DashboardStatsVo>> GetDashboardStats()
    {
        try
        {
            // 获取总用户数
            var totalUsers = await _userService.QueryCountAsync();

            // 获取总订单数（排除软删除的记录）
            var totalOrders = await _orderService.QueryCountAsync(o => !o.IsDeleted);

            // 获取商品数量（排除软删除的记录）
            var totalProducts = await _productService.QueryCountAsync(p => !p.IsDeleted);

            // 计算总收入（这里需要根据实际业务逻辑计算）
            var totalRevenue = await CalculateTotalRevenue();

            var stats = new DashboardStatsVo
            {
                VoTotalUsers = totalUsers,
                VoTotalOrders = totalOrders,
                VoTotalProducts = totalProducts,
                VoTotalRevenue = totalRevenue
            };

            return MessageModel<DashboardStatsVo>.Success("获取成功", stats);
        }
        catch (Exception ex)
        {
            return MessageModel<DashboardStatsVo>.Failed($"获取统计数据失败：{ex.Message}");
        }
    }

    /// <summary>获取订单趋势数据</summary>
    /// <param name="days">天数，默认30天</param>
    /// <returns>订单趋势数据</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<List<OrderTrendItemVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<List<OrderTrendItemVo>>> GetOrderTrend(int days = 30)
    {
        try
        {
            var endDate = DateTime.Now.Date;
            var startDate = endDate.AddDays(-days);

            var trendData = new List<OrderTrendItemVo>();

            for (int i = 0; i < days; i++)
            {
                var date = startDate.AddDays(i);
                // TODO: 实现按日期查询订单数量的逻辑
                var orderCount = await GetOrderCountByDate(date);
                var revenue = await GetRevenueByDate(date);

                trendData.Add(new OrderTrendItemVo
                {
                    VoDate = date.ToString("yyyy-MM-dd"),
                    VoOrderCount = orderCount,
                    VoRevenue = revenue
                });
            }

            return MessageModel<List<OrderTrendItemVo>>.Success("获取成功", trendData);
        }
        catch (Exception ex)
        {
            return MessageModel<List<OrderTrendItemVo>>.Failed($"获取订单趋势失败：{ex.Message}");
        }
    }

    /// <summary>获取商品销售排行</summary>
    /// <param name="limit">返回数量，默认10</param>
    /// <returns>商品销售排行</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<List<ProductSalesRankingVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<List<ProductSalesRankingVo>>> GetProductSalesRanking(int limit = 10)
    {
        try
        {
            // TODO: 实现商品销售排行查询逻辑
            // 这里需要根据订单详情表来统计商品销量
            var rankingData = new List<ProductSalesRankingVo>
            {
                new() { VoProductName = "VIP会员", VoSalesCount = 150, VoRevenue = 15000 },
                new() { VoProductName = "经验卡", VoSalesCount = 120, VoRevenue = 6000 },
                new() { VoProductName = "改名卡", VoSalesCount = 80, VoRevenue = 4000 },
                new() { VoProductName = "头像框", VoSalesCount = 60, VoRevenue = 3000 },
                new() { VoProductName = "称号", VoSalesCount = 45, VoRevenue = 2250 }
            };

            return MessageModel<List<ProductSalesRankingVo>>.Success("获取成功", rankingData.Take(limit).ToList());
        }
        catch (Exception ex)
        {
            return MessageModel<List<ProductSalesRankingVo>>.Failed($"获取商品销售排行失败：{ex.Message}");
        }
    }

    /// <summary>获取用户等级分布</summary>
    /// <returns>用户等级分布</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<List<UserLevelDistributionVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel<List<UserLevelDistributionVo>>> GetUserLevelDistribution()
    {
        try
        {
            // TODO: 实现用户等级分布查询逻辑
            // 这里需要查询 UserLevel 表来统计各等级用户数量
            var distributionData = new List<UserLevelDistributionVo>
            {
                new() { VoLevel = 1, VoLevelName = "凡人", VoUserCount = 500 },
                new() { VoLevel = 2, VoLevelName = "练气", VoUserCount = 300 },
                new() { VoLevel = 3, VoLevelName = "筑基", VoUserCount = 150 },
                new() { VoLevel = 4, VoLevelName = "金丹", VoUserCount = 80 },
                new() { VoLevel = 5, VoLevelName = "元婴", VoUserCount = 40 },
                new() { VoLevel = 6, VoLevelName = "化神", VoUserCount = 20 },
                new() { VoLevel = 7, VoLevelName = "炼虚", VoUserCount = 10 },
                new() { VoLevel = 8, VoLevelName = "合体", VoUserCount = 5 },
                new() { VoLevel = 9, VoLevelName = "大乘", VoUserCount = 3 },
                new() { VoLevel = 10, VoLevelName = "渡劫", VoUserCount = 2 },
                new() { VoLevel = 11, VoLevelName = "飞升", VoUserCount = 1 }
            };

            return MessageModel<List<UserLevelDistributionVo>>.Success("获取成功", distributionData);
        }
        catch (Exception ex)
        {
            return MessageModel<List<UserLevelDistributionVo>>.Failed($"获取用户等级分布失败：{ex.Message}");
        }
    }

    /// <summary>计算总收入</summary>
    /// <returns>总收入</returns>
    private async Task<decimal> CalculateTotalRevenue()
    {
        // TODO: 实现总收入计算逻辑
        // 这里需要查询所有成功的订单并计算总金额
        return 50000; // 临时返回固定值
    }

    /// <summary>获取指定日期的订单数量</summary>
    /// <param name="date">日期</param>
    /// <returns>订单数量</returns>
    private async Task<int> GetOrderCountByDate(DateTime date)
    {
        // TODO: 实现按日期查询订单数量的逻辑
        var random = new Random();
        return random.Next(10, 50); // 临时返回随机值
    }

    /// <summary>获取指定日期的收入</summary>
    /// <param name="date">日期</param>
    /// <returns>收入</returns>
    private async Task<decimal> GetRevenueByDate(DateTime date)
    {
        // TODO: 实现按日期查询收入的逻辑
        var random = new Random();
        return random.Next(500, 2000); // 临时返回随机值
    }
}