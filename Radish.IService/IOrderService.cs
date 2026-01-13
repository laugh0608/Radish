using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.IService;

/// <summary>订单服务接口</summary>
public interface IOrderService : IBaseService<Order, OrderVo>
{
    #region 购买流程

    /// <summary>创建订单并完成购买</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="dto">创建订单 DTO</param>
    /// <returns>购买结果</returns>
    Task<PurchaseResultDto> PurchaseAsync(long userId, CreateOrderDto dto);

    /// <summary>取消订单</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="orderId">订单 ID</param>
    /// <param name="reason">取消原因</param>
    /// <returns>是否成功</returns>
    Task<bool> CancelOrderAsync(long userId, long orderId, string? reason = null);

    #endregion

    #region 订单查询

    /// <summary>获取用户订单列表</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="status">订单状态（可选）</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>分页订单列表</returns>
    Task<PageModel<OrderListItemVo>> GetUserOrdersAsync(
        long userId,
        OrderStatus? status = null,
        int pageIndex = 1,
        int pageSize = 20);

    /// <summary>获取订单详情</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="orderId">订单 ID</param>
    /// <returns>订单视图模型</returns>
    Task<OrderVo?> GetOrderDetailAsync(long userId, long orderId);

    /// <summary>根据订单号获取订单</summary>
    /// <param name="orderNo">订单号</param>
    /// <returns>订单视图模型</returns>
    Task<OrderVo?> GetOrderByNoAsync(string orderNo);

    /// <summary>获取用户购买某商品的数量</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="productId">商品 ID</param>
    /// <returns>已购买数量</returns>
    Task<int> GetUserPurchaseCountAsync(long userId, long productId);

    #endregion

    #region 管理员操作

    /// <summary>获取订单列表（管理后台）</summary>
    /// <param name="userId">用户 ID（可选）</param>
    /// <param name="status">订单状态（可选）</param>
    /// <param name="productId">商品 ID（可选）</param>
    /// <param name="orderNo">订单号（可选）</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>分页订单列表</returns>
    Task<PageModel<OrderVo>> GetOrderListForAdminAsync(
        long? userId = null,
        OrderStatus? status = null,
        long? productId = null,
        string? orderNo = null,
        int pageIndex = 1,
        int pageSize = 20);

    /// <summary>管理员备注订单</summary>
    /// <param name="orderId">订单 ID</param>
    /// <param name="remark">备注内容</param>
    /// <returns>是否成功</returns>
    Task<bool> AdminRemarkOrderAsync(long orderId, string remark);

    /// <summary>重新发放权益（发放失败时使用）</summary>
    /// <param name="orderId">订单 ID</param>
    /// <returns>是否成功</returns>
    Task<bool> RetryGrantBenefitAsync(long orderId);

    #endregion
}
