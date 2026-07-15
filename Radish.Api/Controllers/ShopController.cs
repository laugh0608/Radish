using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Routing;
using Radish.Api.Filters;
using Radish.Api.Resources;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>
/// 商城控制器
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[ApiVersion(1)]
[ApiErrorContract]
public class ShopController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IOrderService _orderService;
    private readonly IUserBenefitService _userBenefitService;
    private readonly IUserInventoryService _userInventoryService;
    private readonly IUserBrowseHistoryService _userBrowseHistoryService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IStringLocalizer<Errors> _errorsLocalizer;

    public ShopController(
        IProductService productService,
        IOrderService orderService,
        IUserBenefitService userBenefitService,
        IUserInventoryService userInventoryService,
        IUserBrowseHistoryService userBrowseHistoryService,
        ICurrentUserAccessor currentUserAccessor,
        IStringLocalizer<Errors> errorsLocalizer)
    {
        _productService = productService;
        _orderService = orderService;
        _userBenefitService = userBenefitService;
        _userInventoryService = userInventoryService;
        _userBrowseHistoryService = userBrowseHistoryService;
        _currentUserAccessor = currentUserAccessor;
        _errorsLocalizer = errorsLocalizer;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

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

        if (Current.UserId > 0)
        {
            await _userBrowseHistoryService.RecordAsync(new RecordBrowseHistoryDto
            {
                UserId = Current.UserId,
                TenantId = Current.TenantId,
                TargetType = "Product",
                TargetId = result.VoId,
                Title = result.VoName,
                Summary = result.VoDescription,
                CoverAttachmentId = result.VoCoverAttachmentId ?? result.VoIconAttachmentId,
                RoutePath = PublicRoutePathBuilder.BuildShopProductPath(result.VoId),
                OperatorName = Current.UserName
            });
        }

        return MessageModel<ProductVo>.Success("查询成功", result);
    }

    /// <summary>获取服务端权威商品能力元数据。</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<MessageModel<List<ShopProductCapabilityVo>>> GetProductCapabilities()
    {
        var result = await _productService.GetProductCapabilitiesAsync();
        return MessageModel<List<ShopProductCapabilityVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 检查是否可以购买商品
    /// </summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="quantity">购买数量</param>
    /// <returns>是否可购买</returns>
    [HttpGet("{productId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    public async Task<MessageModel<ProductBuyCheckResultVo>> CheckCanBuy(long productId, int quantity = 1)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<ProductBuyCheckResultVo>.Message(false, "未登录", default!);
        }

        var (canBuy, reason) = await _productService.CheckCanBuyAsync(userId, productId, quantity);
        return MessageModel<ProductBuyCheckResultVo>.Success("查询成功", new ProductBuyCheckResultVo
        {
            VoCanBuy = canBuy,
            VoReason = reason
        });
    }

    #endregion

    #region 购买流程

    /// <summary>
    /// 购买商品
    /// </summary>
    /// <param name="dto">购买信息</param>
    /// <returns>购买结果</returns>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Client)]
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
    [Authorize(Policy = AuthorizationPolicies.Client)]
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
    [Authorize(Policy = AuthorizationPolicies.Client)]
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
            return BuildError<OrderVo>(
                HttpStatusCodeEnum.NotFound,
                "订单不存在",
                "Order.NotFound",
                "error.order.not_found");
        }

        return MessageModel<OrderVo>.Success("查询成功", result);
    }

    /// <summary>
    /// 取消订单
    /// </summary>
    /// <param name="orderId">订单 ID</param>
    /// <param name="reason">取消原因</param>
    /// <returns>选择结果与该类型当前权益</returns>
    [HttpPost("{orderId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
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
    [Authorize(Policy = AuthorizationPolicies.Client)]
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
    [Authorize(Policy = AuthorizationPolicies.Client)]
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
    /// <returns>停用结果与该类型当前权益</returns>
    [HttpPost("{benefitId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    public async Task<MessageModel<UserBenefitActionResultVo>> ActivateBenefit(long benefitId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<UserBenefitActionResultVo>.Message(false, "未登录", default!);
        }

        try
        {
            var result = await _userBenefitService.ActivateBenefitAsync(userId, benefitId);
            return MessageModel<UserBenefitActionResultVo>.Success(
                result.VoChanged ? "激活成功" : "权益已处于激活状态",
                result);
        }
        catch (InvalidOperationException ex)
        {
            return MessageModel<UserBenefitActionResultVo>.Message(false, ex.Message, default!);
        }
    }

    /// <summary>
    /// 取消激活权益
    /// </summary>
    /// <param name="benefitId">权益 ID</param>
    /// <returns>是否成功</returns>
    [HttpPost("{benefitId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    public async Task<MessageModel<UserBenefitActionResultVo>> DeactivateBenefit(long benefitId)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<UserBenefitActionResultVo>.Message(false, "未登录", default!);
        }

        try
        {
            var result = await _userBenefitService.DeactivateBenefitAsync(userId, benefitId);
            return MessageModel<UserBenefitActionResultVo>.Success(
                result.VoChanged ? "取消激活成功" : "权益当前未激活",
                result);
        }
        catch (InvalidOperationException ex)
        {
            return MessageModel<UserBenefitActionResultVo>.Message(false, ex.Message, default!);
        }
    }

    #endregion

    #region 用户背包

    /// <summary>
    /// 获取我的背包
    /// </summary>
    /// <returns>背包物品列表</returns>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Client)]
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
    [Authorize(Policy = AuthorizationPolicies.Client)]
    public async Task<MessageModel<UseItemResultDto>> UseItem([FromBody] UseItemDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<UseItemResultDto>.Message(false, "未登录", default!);
        }

        UseItemResultDto result;
        try
        {
            result = await _userInventoryService.UseItemAsync(userId, dto);
        }
        catch (BusinessException ex)
        {
            result = new UseItemResultDto { Success = false, ErrorMessage = ex.Message };
            return MessageModel<UseItemResultDto>.Message(false, ex.Message, result);
        }
        catch (Exception)
        {
            result = new UseItemResultDto { Success = false, ErrorMessage = "使用失败" };
            return MessageModel<UseItemResultDto>.Message(false, "使用失败", result);
        }

        if (!result.Success)
        {
            return MessageModel<UseItemResultDto>.Message(false, result.ErrorMessage ?? "使用失败", result);
        }

        return MessageModel<UseItemResultDto>.Success("使用成功", result);
    }

    /// <summary>
    /// 使用改名卡
    /// </summary>
    /// <param name="dto">改名卡使用请求</param>
    /// <returns>使用结果</returns>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    public async Task<MessageModel<UseItemResultDto>> UseRenameCard([FromBody] UseRenameCardDto dto)
    {
        return await UseRenameCardCoreAsync(dto);
    }

    /// <summary>旧版 query string 改名卡入口，仅保留迁移期兼容。</summary>
    [Obsolete("请改用 POST /api/v1/Shop/UseRenameCard body 请求")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("~/api/v{version:apiVersion}/Shop/UseRenameCard/{inventoryId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    public async Task<MessageModel<UseItemResultDto>> UseRenameCardLegacy(
        long inventoryId,
        [FromQuery] string newNickname,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey)
    {
        return await UseRenameCardCoreAsync(new UseRenameCardDto
        {
            InventoryId = inventoryId,
            NewDisplayName = newNickname,
            IdempotencyKey = idempotencyKey
        });
    }

    private async Task<MessageModel<UseItemResultDto>> UseRenameCardCoreAsync(UseRenameCardDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<UseItemResultDto>.Message(false, "未登录", default!);
        }

        UseItemResultDto result;
        try
        {
            result = await _userInventoryService.UseRenameCardAsync(userId, dto);
        }
        catch (BusinessException ex)
        {
            result = new UseItemResultDto { Success = false, ErrorMessage = ex.Message };
            return MessageModel<UseItemResultDto>.Message(false, ex.Message, result);
        }
        catch (Exception)
        {
            result = new UseItemResultDto { Success = false, ErrorMessage = "使用失败" };
            return MessageModel<UseItemResultDto>.Message(false, "使用失败", result);
        }

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
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.ProductsView)]
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
    /// 获取商品详情（管理后台）
    /// </summary>
    [HttpGet("{productId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.ProductsView)]
    public async Task<MessageModel<ProductVo>> AdminGetProduct(long productId)
    {
        var result = await _productService.GetProductDetailForAdminAsync(productId);
        if (result == null)
        {
            return MessageModel<ProductVo>.Message(false, "商品不存在", default!);
        }

        return MessageModel<ProductVo>.Success("查询成功", result);
    }

    /// <summary>
    /// 创建商品
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.ProductsCreate)]
    public async Task<MessageModel<long>> CreateProduct([FromBody] CreateProductDto dto)
    {
        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();

        try
        {
            var productId = await _productService.CreateProductAsync(dto, userId, userName);
            return MessageModel<long>.Success("创建成功", productId);
        }
        catch (InvalidOperationException ex)
        {
            return MessageModel<long>.Message(false, ex.Message, default);
        }
    }

    /// <summary>
    /// 更新商品
    /// </summary>
    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.ProductsEdit)]
    public async Task<MessageModel<bool>> UpdateProduct([FromBody] UpdateProductDto dto)
    {
        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();

        try
        {
            var result = await _productService.UpdateProductAsync(dto, userId, userName);
            return MessageModel<bool>.Success("更新成功", result);
        }
        catch (InvalidOperationException ex)
        {
            return MessageModel<bool>.Message(false, ex.Message, false);
        }
    }

    /// <summary>
    /// 删除商品
    /// </summary>
    [HttpDelete("{productId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.ProductsDelete)]
    public async Task<MessageModel<bool>> DeleteProduct(long productId)
    {
        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();

        try
        {
            var result = await _productService.DeleteProductAsync(productId, userId, userName);
            return MessageModel<bool>.Success("删除成功", result);
        }
        catch (InvalidOperationException ex)
        {
            return MessageModel<bool>.Message(false, ex.Message, false);
        }
    }

    /// <summary>
    /// 上架商品
    /// </summary>
    [HttpPost("{productId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.ProductsToggleSale)]
    public async Task<MessageModel<bool>> PutOnSale(long productId, [FromBody] ProductVersionWriteDto request)
    {
        try
        {
            var result = await _productService.PutOnSaleAsync(productId, request.ExpectedVersion);
            return MessageModel<bool>.Success("上架成功", result);
        }
        catch (InvalidOperationException ex)
        {
            return MessageModel<bool>.Message(false, ex.Message, false);
        }
    }

    /// <summary>
    /// 下架商品
    /// </summary>
    [HttpPost("{productId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.ProductsToggleSale)]
    public async Task<MessageModel<bool>> TakeOffSale(long productId, [FromBody] ProductVersionWriteDto request)
    {
        try
        {
            var result = await _productService.TakeOffSaleAsync(productId, request.ExpectedVersion);
            return MessageModel<bool>.Success("下架成功", result);
        }
        catch (InvalidOperationException ex)
        {
            return MessageModel<bool>.Message(false, ex.Message, false);
        }
    }

    /// <summary>
    /// 获取订单列表（管理后台）
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.OrdersView)]
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
    /// 获取订单详情（管理后台）
    /// </summary>
    [HttpGet("{orderId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.OrdersView)]
    public async Task<MessageModel<OrderVo>> AdminGetOrder(long orderId)
    {
        var result = await _orderService.GetOrderDetailForAdminAsync(orderId);
        if (result == null)
        {
            return BuildError<OrderVo>(
                HttpStatusCodeEnum.NotFound,
                "订单不存在",
                "Order.NotFound",
                "error.order.not_found");
        }

        return MessageModel<OrderVo>.Success("查询成功", result);
    }

    /// <summary>获取指定用户的持续权益与消耗品业务流水（管理后台）。</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.BenefitsView)]
    public async Task<MessageModel<PageModel<ShopEntitlementOperationVo>>> AdminGetEntitlementOperations(
        long userId,
        string? operationType = null,
        BenefitType? benefitType = null,
        ConsumableType? consumableType = null,
        int pageIndex = 1,
        int pageSize = 20)
    {
        var result = await _userBenefitService.GetOperationsForAdminAsync(
            userId,
            operationType,
            benefitType,
            consumableType,
            pageIndex,
            pageSize);
        return MessageModel<PageModel<ShopEntitlementOperationVo>>.Success("查询成功", result);
    }

    /// <summary>获取指定用户的持续权益（管理后台）。</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.BenefitsView)]
    public async Task<MessageModel<List<UserBenefitVo>>> AdminGetUserBenefits(long userId)
    {
        var result = await _userBenefitService.GetUserBenefitsForAdminAsync(userId);
        return MessageModel<List<UserBenefitVo>>.Success("查询成功", result);
    }

    /// <summary>撤销指定用户权益（管理后台）。</summary>
    [HttpPost("{benefitId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.BenefitsRevoke)]
    public async Task<MessageModel<UserBenefitActionResultVo>> AdminRevokeBenefit(
        long benefitId,
        [FromBody] RevokeUserBenefitDto request)
    {
        try
        {
            var result = await _userBenefitService.RevokeBenefitAsync(
                benefitId,
                request.Reason,
                GetCurrentUserId(),
                GetCurrentUserName());
            return MessageModel<UserBenefitActionResultVo>.Success(
                result.VoChanged ? "权益已撤销" : "权益此前已撤销",
                result);
        }
        catch (InvalidOperationException)
        {
            return BuildError<UserBenefitActionResultVo>(
                HttpStatusCodeEnum.Conflict,
                "当前权益操作无法完成，请刷新后核对权益状态",
                "Shop.BenefitOperationRejected",
                "error.shop.benefit_operation_rejected");
        }
    }

    /// <summary>
    /// 重新发放权益
    /// </summary>
    [HttpPost("{orderId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.OrdersRetry)]
    public async Task<MessageModel<bool>> RetryGrantBenefit(long orderId)
    {
        try
        {
            var result = await _orderService.RetryGrantBenefitAsync(orderId);
            return MessageModel<bool>.Success("重新发放成功", result);
        }
        catch (BusinessException ex)
        {
            return BuildError(
                (HttpStatusCodeEnum)ex.StatusCode,
                ex.Message,
                ex.ErrorCode ?? "Order.RetryRejected",
                ex.MessageKey ?? "error.order.retry_rejected",
                false);
        }
        catch (InvalidOperationException)
        {
            return BuildError(
                HttpStatusCodeEnum.Conflict,
                "当前订单不能重新发放，请刷新后核对订单状态与支付证据",
                "Order.RetryRejected",
                "error.order.retry_rejected",
                false);
        }
    }

    /// <summary>
    /// 管理员备注订单
    /// </summary>
    [HttpPost("{orderId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.OrdersRemark)]
    public async Task<MessageModel<bool>> AdminRemarkOrder(long orderId, [FromBody] AdminRemarkOrderDto request)
    {
        try
        {
            var result = await _orderService.AdminRemarkOrderAsync(
                orderId,
                request.Remark ?? string.Empty,
                GetCurrentUserId(),
                GetCurrentUserName());
            return result
                ? MessageModel<bool>.Success("备注已保存", true)
                : BuildError(
                    HttpStatusCodeEnum.Conflict,
                    "订单备注保存失败，请刷新后重试",
                    "Order.RemarkFailed",
                    "error.order.remark_failed",
                    responseData: false);
        }
        catch (BusinessException ex)
        {
            return BuildError(
                (HttpStatusCodeEnum)ex.StatusCode,
                ex.Message,
                ex.ErrorCode ?? "Order.RemarkFailed",
                ex.MessageKey ?? "error.order.remark_failed",
                false);
        }
        catch (InvalidOperationException)
        {
            return BuildError(
                HttpStatusCodeEnum.Conflict,
                "订单备注保存失败，请刷新后重试",
                "Order.RemarkFailed",
                "error.order.remark_failed",
                false);
        }
    }

    #endregion

    #region 私有方法

    /// <summary>获取当前用户 ID</summary>
    private long GetCurrentUserId() => Current.UserId;

    /// <summary>获取当前用户名</summary>
    private string GetCurrentUserName() =>
        string.IsNullOrWhiteSpace(Current.UserName) ? "Unknown" : Current.UserName;

    private MessageModel<T> BuildError<T>(
        HttpStatusCodeEnum statusCode,
        string fallbackMessage,
        string code,
        string messageKey,
        T? responseData = default)
    {
        var localizedMessage = _errorsLocalizer[messageKey];
        return new MessageModel<T>
        {
            IsSuccess = false,
            StatusCode = (int)statusCode,
            MessageInfo = localizedMessage.ResourceNotFound ? fallbackMessage : localizedMessage.Value,
            ResponseData = responseData,
            Code = code,
            MessageKey = messageKey
        };
    }

    #endregion
}
