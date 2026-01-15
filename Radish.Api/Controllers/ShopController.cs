using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>
/// 商城控制器
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[ApiVersion(1)]
public class ShopController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IOrderService _orderService;
    private readonly IUserBenefitService _userBenefitService;
    private readonly IUserInventoryService _userInventoryService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ShopController(
        IProductService productService,
        IOrderService orderService,
        IUserBenefitService userBenefitService,
        IUserInventoryService userInventoryService,
        IHttpContextAccessor httpContextAccessor)
    {
        _productService = productService;
        _orderService = orderService;
        _userBenefitService = userBenefitService;
        _userInventoryService = userInventoryService;
        _httpContextAccessor = httpContextAccessor;
    }

    #region 商品分类

    /// <summary>
    /// 获取商品分类列表
    /// </summary>
    /// <returns>分类列表</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<MessageModel<List<ProductCategoryVo>>> GetCategories()
    {
        var result = await _productService.GetCategoriesAsync();
        return MessageModel<List<ProductCategoryVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 获取分类详情
    /// </summary>
    /// <param name="categoryId">分类 ID</param>
    /// <returns>分类详情</returns>
    [HttpGet("{categoryId}")]
    [AllowAnonymous]
    public async Task<MessageModel<ProductCategoryVo>> GetCategory(string categoryId)
    {
        var result = await _productService.GetCategoryAsync(categoryId);
        if (result == null)
        {
            return MessageModel<ProductCategoryVo>.Message(false, "分类不存在", default!);
        }
        return MessageModel<ProductCategoryVo>.Success("查询成功", result);
    }

    #endregion

    #region 商品查询

    /// <summary>
    /// 获取商品列表
    /// </summary>
    /// <param name="categoryId">分类 ID（可选）</param>
    /// <param name="productType">商品类型（可选）</param>
    /// <param name="keyword">搜索关键词（可选）</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>商品列表</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<MessageModel<PageModel<ProductListItemVo>>> GetProducts(
        string? categoryId = null,
        ProductType? productType = null,
        string? keyword = null,
        int pageIndex = 1,
        int pageSize = 20)
    {
        var result = await _productService.GetProductListAsync(categoryId, productType, keyword, pageIndex, pageSize);
        return MessageModel<PageModel<ProductListItemVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 获取商品详情
    /// </summary>
    /// <param name="productId">商品 ID</param>
    /// <returns>商品详情</returns>
    [HttpGet("{productId:long}")]
    [AllowAnonymous]
    public async Task<MessageModel<ProductVo>> GetProduct(long productId)
    {
        var result = await _productService.GetProductDetailAsync(productId);
        if (result == null)
        {
            return MessageModel<ProductVo>.Message(false, "商品不存在", default!);
        }
        return MessageModel<ProductVo>.Success("查询成功", result);
    }

    /// <summary>
    /// 检查是否可以购买商品
    /// </summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="quantity">购买数量</param>
    /// <returns>是否可购买</returns>
    [HttpGet("{productId:long}")]
    [Authorize(Policy = "Client")]
    public async Task<MessageModel<object>> CheckCanBuy(long productId, int quantity = 1)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<object>.Message(false, "未登录", default!);
        }

        var (canBuy, reason) = await _productService.CheckCanBuyAsync(userId, productId, quantity);
        return MessageModel<object>.Success("查询成功", new { canBuy, reason });
    }

    #endregion

    #region 购买流程

    /// <summary>
    /// 购买商品
    /// </summary>
    /// <param name="dto">购买信息</param>
    /// <returns>购买结果</returns>
    [HttpPost]
    [Authorize(Policy = "Client")]
    public async Task<MessageModel<PurchaseResultDto>> Purchase([FromBody] CreateOrderDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<PurchaseResultDto>.Message(false, "未登录", default!);
        }

        var result = await _orderService.PurchaseAsync(userId, dto);
        if (!result.Success)
        {
            return MessageModel<PurchaseResultDto>.Message(false, result.ErrorMessage ?? "购买失败", result);
        }

        return MessageModel<PurchaseResultDto>.Success("购买成功", result);
    }

    #endregion

    #region 订单管理

    /// <summary>
    /// 获取我的订单列表
    /// </summary>
    /// <param name="status">订单状态（可选）</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>订单列表</returns>
    [HttpGet]
    [Authorize(Policy = "Client")]
    public async Task<MessageModel<PageModel<OrderListItemVo>>> GetMyOrders(
        OrderStatus? status = null,
        int pageIndex = 1,
        int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<PageModel<OrderListItemVo>>.Message(false, "未登录", default!);
        }

        var result = await _orderService.GetUserOrdersAsync(userId, status, pageIndex, pageSize);
        return MessageModel<PageModel<OrderListItemVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 获取订单详情
    /// </summary>
    /// <param name="orderId">订单 ID</param>
    /// <returns>订单详情</returns>
    [HttpGet("{orderId:long}")]
    [Authorize(Policy = "Client")]
    public async Task<MessageModel<OrderVo>> GetOrder(long orderId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<OrderVo>.Message(false, "未登录", default!);
        }

        var result = await _orderService.GetOrderDetailAsync(userId, orderId);
        if (result == null)
        {
            return MessageModel<OrderVo>.Message(false, "订单不存在", default!);
        }

        return MessageModel<OrderVo>.Success("查询成功", result);
    }

    /// <summary>
    /// 取消订单
    /// </summary>
    /// <param name="orderId">订单 ID</param>
    /// <param name="reason">取消原因</param>
    /// <returns>是否成功</returns>
    [HttpPost("{orderId:long}")]
    [Authorize(Policy = "Client")]
    public async Task<MessageModel<bool>> CancelOrder(long orderId, string? reason = null)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<bool>.Message(false, "未登录", false);
        }

        try
        {
            var result = await _orderService.CancelOrderAsync(userId, orderId, reason);
            return MessageModel<bool>.Success("取消成功", result);
        }
        catch (InvalidOperationException ex)
        {
            return MessageModel<bool>.Message(false, ex.Message, false);
        }
    }

    #endregion

    #region 用户权益

    /// <summary>
    /// 获取我的权益列表
    /// </summary>
    /// <param name="includeExpired">是否包含已过期的</param>
    /// <returns>权益列表</returns>
    [HttpGet]
    [Authorize(Policy = "Client")]
    public async Task<MessageModel<List<UserBenefitVo>>> GetMyBenefits(bool includeExpired = false)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<List<UserBenefitVo>>.Message(false, "未登录", default!);
        }

        var result = await _userBenefitService.GetUserBenefitsAsync(userId, includeExpired);
        return MessageModel<List<UserBenefitVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 获取我的激活权益
    /// </summary>
    /// <returns>激活的权益列表</returns>
    [HttpGet]
    [Authorize(Policy = "Client")]
    public async Task<MessageModel<List<UserBenefitVo>>> GetMyActiveBenefits()
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<List<UserBenefitVo>>.Message(false, "未登录", default!);
        }

        var result = await _userBenefitService.GetActiveBenefitsAsync(userId);
        return MessageModel<List<UserBenefitVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 激活权益
    /// </summary>
    /// <param name="benefitId">权益 ID</param>
    /// <returns>是否成功</returns>
    [HttpPost("{benefitId:long}")]
    [Authorize(Policy = "Client")]
    public async Task<MessageModel<bool>> ActivateBenefit(long benefitId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<bool>.Message(false, "未登录", false);
        }

        try
        {
            var result = await _userBenefitService.ActivateBenefitAsync(userId, benefitId);
            return MessageModel<bool>.Success("激活成功", result);
        }
        catch (InvalidOperationException ex)
        {
            return MessageModel<bool>.Message(false, ex.Message, false);
        }
    }

    /// <summary>
    /// 取消激活权益
    /// </summary>
    /// <param name="benefitId">权益 ID</param>
    /// <returns>是否成功</returns>
    [HttpPost("{benefitId:long}")]
    [Authorize(Policy = "Client")]
    public async Task<MessageModel<bool>> DeactivateBenefit(long benefitId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<bool>.Message(false, "未登录", false);
        }

        var result = await _userBenefitService.DeactivateBenefitAsync(userId, benefitId);
        return MessageModel<bool>.Success("取消激活成功", result);
    }

    #endregion

    #region 用户背包

    /// <summary>
    /// 获取我的背包
    /// </summary>
    /// <returns>背包物品列表</returns>
    [HttpGet]
    [Authorize(Policy = "Client")]
    public async Task<MessageModel<List<UserInventoryVo>>> GetMyInventory()
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<List<UserInventoryVo>>.Message(false, "未登录", default!);
        }

        var result = await _userInventoryService.GetUserInventoryAsync(userId);
        return MessageModel<List<UserInventoryVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 使用道具
    /// </summary>
    /// <param name="dto">使用道具信息</param>
    /// <returns>使用结果</returns>
    [HttpPost]
    [Authorize(Policy = "Client")]
    public async Task<MessageModel<UseItemResultDto>> UseItem([FromBody] UseItemDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<UseItemResultDto>.Message(false, "未登录", default!);
        }

        var result = await _userInventoryService.UseItemAsync(userId, dto);
        if (!result.Success)
        {
            return MessageModel<UseItemResultDto>.Message(false, result.ErrorMessage ?? "使用失败", result);
        }

        return MessageModel<UseItemResultDto>.Success("使用成功", result);
    }

    /// <summary>
    /// 使用改名卡
    /// </summary>
    /// <param name="inventoryId">背包项 ID</param>
    /// <param name="newNickname">新昵称</param>
    /// <returns>使用结果</returns>
    [HttpPost("{inventoryId:long}")]
    [Authorize(Policy = "Client")]
    public async Task<MessageModel<UseItemResultDto>> UseRenameCard(long inventoryId, [FromQuery] string newNickname)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<UseItemResultDto>.Message(false, "未登录", default!);
        }

        var result = await _userInventoryService.UseRenameCardAsync(userId, inventoryId, newNickname);
        if (!result.Success)
        {
            return MessageModel<UseItemResultDto>.Message(false, result.ErrorMessage ?? "使用失败", result);
        }

        return MessageModel<UseItemResultDto>.Success("使用成功", result);
    }

    #endregion

    #region 管理员操作

    /// <summary>
    /// 获取商品列表（管理后台）
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "SystemOrAdmin")]
    public async Task<MessageModel<PageModel<ProductVo>>> AdminGetProducts(
        string? categoryId = null,
        ProductType? productType = null,
        bool? isOnSale = null,
        string? keyword = null,
        int pageIndex = 1,
        int pageSize = 20)
    {
        var result = await _productService.GetProductListForAdminAsync(
            categoryId, productType, isOnSale, keyword, pageIndex, pageSize);
        return MessageModel<PageModel<ProductVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 创建商品
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "SystemOrAdmin")]
    public async Task<MessageModel<long>> CreateProduct([FromBody] CreateProductDto dto)
    {
        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();

        var productId = await _productService.CreateProductAsync(dto, userId, userName);
        return MessageModel<long>.Success("创建成功", productId);
    }

    /// <summary>
    /// 更新商品
    /// </summary>
    [HttpPut]
    [Authorize(Policy = "SystemOrAdmin")]
    public async Task<MessageModel<bool>> UpdateProduct([FromBody] UpdateProductDto dto)
    {
        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();

        var result = await _productService.UpdateProductAsync(dto, userId, userName);
        return MessageModel<bool>.Success("更新成功", result);
    }

    /// <summary>
    /// 上架商品
    /// </summary>
    [HttpPost("{productId:long}")]
    [Authorize(Policy = "SystemOrAdmin")]
    public async Task<MessageModel<bool>> PutOnSale(long productId)
    {
        var result = await _productService.PutOnSaleAsync(productId);
        return MessageModel<bool>.Success("上架成功", result);
    }

    /// <summary>
    /// 下架商品
    /// </summary>
    [HttpPost("{productId:long}")]
    [Authorize(Policy = "SystemOrAdmin")]
    public async Task<MessageModel<bool>> TakeOffSale(long productId)
    {
        var result = await _productService.TakeOffSaleAsync(productId);
        return MessageModel<bool>.Success("下架成功", result);
    }

    /// <summary>
    /// 获取订单列表（管理后台）
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "SystemOrAdmin")]
    public async Task<MessageModel<PageModel<OrderVo>>> AdminGetOrders(
        long? userId = null,
        OrderStatus? status = null,
        long? productId = null,
        string? orderNo = null,
        int pageIndex = 1,
        int pageSize = 20)
    {
        var result = await _orderService.GetOrderListForAdminAsync(
            userId, status, productId, orderNo, pageIndex, pageSize);
        return MessageModel<PageModel<OrderVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 重新发放权益
    /// </summary>
    [HttpPost("{orderId:long}")]
    [Authorize(Policy = "SystemOrAdmin")]
    public async Task<MessageModel<bool>> RetryGrantBenefit(long orderId)
    {
        try
        {
            var result = await _orderService.RetryGrantBenefitAsync(orderId);
            return MessageModel<bool>.Success("重新发放成功", result);
        }
        catch (InvalidOperationException ex)
        {
            return MessageModel<bool>.Message(false, ex.Message, false);
        }
    }

    #endregion

    #region 私有方法

    /// <summary>获取当前用户 ID</summary>
    private long GetCurrentUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("jti");

        if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return 0;
    }

    /// <summary>获取当前用户名</summary>
    private string GetCurrentUserName()
    {
        var nameClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("name")
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("preferred_username");

        return nameClaim?.Value ?? "Unknown";
    }

    #endregion
}
