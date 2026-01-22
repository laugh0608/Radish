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
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetDashboardStats()
    {
        try
        {
            // 获取总用户数
            var totalUsers = await _userService.QueryCountAsync();

            // 获取总订单数
            var totalOrders = await _orderService.QueryCountAsync();

            // 获取商品数量
            var totalProducts = await _productService.QueryCountAsync();

            // 计算总收入（这里需要根据实际业务逻辑计算）
            var totalRevenue = await CalculateTotalRevenue();

            var stats = new
            {
                TotalUsers = totalUsers,
                TotalOrders = totalOrders,
                TotalProducts = totalProducts,
                TotalRevenue = totalRevenue
            };

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = stats
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"获取统计数据失败：{ex.Message}"
            };
        }
    }

    /// <summary>获取订单趋势数据</summary>
    /// <param name="days">天数，默认30天</param>
    /// <returns>订单趋势数据</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetOrderTrend(int days = 30)
    {
        try
        {
            var endDate = DateTime.Now.Date;
            var startDate = endDate.AddDays(-days);

            // 这里需要根据实际的 Repository 实现来查询数据
            // 由于使用的是 BaseService，可能需要扩展查询方法
            var trendData = new List<object>();

            for (int i = 0; i < days; i++)
            {
                var date = startDate.AddDays(i);
                // TODO: 实现按日期查询订单数量的逻辑
                var orderCount = await GetOrderCountByDate(date);
                var revenue = await GetRevenueByDate(date);

                trendData.Add(new
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    OrderCount = orderCount,
                    Revenue = revenue
                });
            }

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = trendData
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"获取订单趋势失败：{ex.Message}"
            };
        }
    }

    /// <summary>获取商品销售排行</summary>
    /// <param name="limit">返回数量，默认10</param>
    /// <returns>商品销售排行</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetProductSalesRanking(int limit = 10)
    {
        try
        {
            // TODO: 实现商品销售排行查询逻辑
            // 这里需要根据订单详情表来统计商品销量
            var rankingData = new List<object>
            {
                new { ProductName = "VIP会员", SalesCount = 150, Revenue = 15000 },
                new { ProductName = "经验卡", SalesCount = 120, Revenue = 6000 },
                new { ProductName = "改名卡", SalesCount = 80, Revenue = 4000 },
                new { ProductName = "头像框", SalesCount = 60, Revenue = 3000 },
                new { ProductName = "称号", SalesCount = 45, Revenue = 2250 }
            };

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = rankingData.Take(limit)
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"获取商品销售排行失败：{ex.Message}"
            };
        }
    }

    /// <summary>获取用户等级分布</summary>
    /// <returns>用户等级分布</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetUserLevelDistribution()
    {
        try
        {
            // TODO: 实现用户等级分布查询逻辑
            // 这里需要查询 UserLevel 表来统计各等级用户数量
            var distributionData = new List<object>
            {
                new { Level = 1, LevelName = "凡人", UserCount = 500 },
                new { Level = 2, LevelName = "练气", UserCount = 300 },
                new { Level = 3, LevelName = "筑基", UserCount = 150 },
                new { Level = 4, LevelName = "金丹", UserCount = 80 },
                new { Level = 5, LevelName = "元婴", UserCount = 40 },
                new { Level = 6, LevelName = "化神", UserCount = 20 },
                new { Level = 7, LevelName = "炼虚", UserCount = 10 },
                new { Level = 8, LevelName = "合体", UserCount = 5 },
                new { Level = 9, LevelName = "大乘", UserCount = 3 },
                new { Level = 10, LevelName = "渡劫", UserCount = 2 },
                new { Level = 11, LevelName = "飞升", UserCount = 1 }
            };

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = distributionData
            };
        }
        catch (Exception ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.InternalServerError,
                MessageInfo = $"获取用户等级分布失败：{ex.Message}"
            };
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