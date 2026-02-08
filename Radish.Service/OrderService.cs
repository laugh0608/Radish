using System.Linq.Expressions;
using AutoMapper;
using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using Radish.Shared.CustomEnum;
using Serilog;
using SqlSugar;

namespace Radish.Service;

/// <summary>订单服务实现</summary>
public class OrderService : BaseService<Order, OrderVo>, IOrderService
{
    private readonly IBaseRepository<Order> _orderRepository;
    private readonly IBaseRepository<Product> _productRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IProductService _productService;
    private readonly IUserBenefitService _userBenefitService;
    private readonly ICoinService _coinService;
    private readonly INotificationService? _notificationService;

    public OrderService(
        IMapper mapper,
        IBaseRepository<Order> orderRepository,
        IBaseRepository<Product> productRepository,
        IBaseRepository<User> userRepository,
        IProductService productService,
        IUserBenefitService userBenefitService,
        ICoinService coinService,
        INotificationService? notificationService = null)
        : base(mapper, orderRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _userRepository = userRepository;
        _productService = productService;
        _userBenefitService = userBenefitService;
        _coinService = coinService;
        _notificationService = notificationService;
    }

    #region 购买流程

    /// <summary>创建订单并完成购买</summary>
    [UseTran]
    public async Task<PurchaseResultDto> PurchaseAsync(long userId, CreateOrderDto dto)
    {
        try
        {
            Log.Information("用户 {UserId} 开始购买商品 {ProductId}, 数量={Quantity}",
                userId, dto.ProductId, dto.Quantity);

            // 1. 检查是否可以购买
            var (canBuy, reason) = await _productService.CheckCanBuyAsync(userId, dto.ProductId, dto.Quantity);
            if (!canBuy)
            {
                return new PurchaseResultDto { Success = false, ErrorMessage = reason };
            }

            // 2. 获取商品信息
            var product = await _productRepository.QueryFirstAsync(p => p.Id == dto.ProductId && !p.IsDeleted);
            if (product == null)
            {
                return new PurchaseResultDto { Success = false, ErrorMessage = "商品不存在" };
            }

            // 3. 计算总价
            var totalPrice = product.Price * dto.Quantity;

            // 4. 检查用户余额
            var balance = await _coinService.GetBalanceAsync(userId);
            if (balance == null || balance.VoBalance < totalPrice)
            {
                return new PurchaseResultDto
                {
                    Success = false,
                    ErrorMessage = $"萝卜币余额不足，需要 {totalPrice} 胡萝卜，当前余额 {balance?.VoBalance ?? 0} 胡萝卜"
                };
            }

            // 5. 扣减库存
            if (product.StockType == StockType.Limited)
            {
                var stockDeducted = await _productService.DeductStockAsync(dto.ProductId, dto.Quantity);
                if (!stockDeducted)
                {
                    return new PurchaseResultDto { Success = false, ErrorMessage = "库存扣减失败" };
                }
            }

            // 6. 创建订单
            var order = new Order
            {
                OrderNo = $"ORD_{SnowFlakeSingle.Instance.NextId()}",
                UserId = userId,
                ProductId = product.Id,
                ProductName = product.Name,
                ProductIcon = product.Icon,
                ProductType = product.ProductType,
                BenefitType = product.BenefitType,
                ConsumableType = product.ConsumableType,
                BenefitValue = product.BenefitValue,
                DurationType = product.DurationType,
                DurationDays = product.DurationDays,
                Quantity = dto.Quantity,
                UnitPrice = product.Price,
                TotalPrice = totalPrice,
                Status = OrderStatus.Pending,
                UserRemark = dto.UserRemark,
                CreateTime = DateTime.Now,
                CreateBy = "User",
                CreateId = userId
            };

            var orderId = await _orderRepository.AddAsync(order);
            order.Id = orderId;

            // 7. 扣除萝卜币
            try
            {
                var transactionNo = await _coinService.GrantCoinAsync(
                    userId,
                    -totalPrice, // 负数表示扣除
                    "CONSUME",
                    "Order",
                    orderId,
                    $"购买商品：{product.Name}");

                order.CoinTransactionId = long.TryParse(transactionNo.Replace("TXN_", ""), out var txnId) ? txnId : null;
                order.Status = OrderStatus.Paid;
                order.PaidTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "扣除萝卜币失败，订单 {OrderId}", orderId);

                // 恢复库存
                if (product.StockType == StockType.Limited)
                {
                    await _productService.RestoreStockAsync(dto.ProductId, dto.Quantity);
                }

                order.Status = OrderStatus.Failed;
                order.FailReason = $"扣除萝卜币失败：{ex.Message}";
                await _orderRepository.UpdateAsync(order);

                return new PurchaseResultDto { Success = false, ErrorMessage = "扣除萝卜币失败" };
            }

            // 8. 发放权益
            long? userBenefitId = null;
            try
            {
                userBenefitId = await _userBenefitService.GrantBenefitAsync(userId, product, orderId);
                order.UserBenefitId = userBenefitId;
                order.Status = OrderStatus.Completed;
                order.CompletedTime = DateTime.Now;

                // 计算权益到期时间
                if (product.DurationType == DurationType.Days && product.DurationDays.HasValue)
                {
                    order.BenefitExpiresAt = DateTime.Now.AddDays(product.DurationDays.Value);
                }
                else if (product.DurationType == DurationType.FixedDate && product.ExpiresAt.HasValue)
                {
                    order.BenefitExpiresAt = product.ExpiresAt;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "发放权益失败，订单 {OrderId}", orderId);
                order.Status = OrderStatus.Failed;
                order.FailReason = $"发放权益失败：{ex.Message}";
            }

            // 9. 更新订单状态
            await _orderRepository.UpdateAsync(order);

            // 10. 增加已售数量
            await _productService.IncreaseSoldCountAsync(dto.ProductId, dto.Quantity);

            // 11. 获取最新余额
            var newBalance = await _coinService.GetBalanceAsync(userId);

            // 12. 发送通知
            if (_notificationService != null && order.Status == OrderStatus.Completed)
            {
                try
                {
                    await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                    {
                        Type = "PURCHASE_SUCCESS",
                        Title = "购买成功",
                        Content = $"您已成功购买 {product.Name}",
                        BusinessType = "Order",
                        BusinessId = orderId,
                        ReceiverUserIds = [userId]
                    });
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "发送购买成功通知失败");
                }
            }

            Log.Information("用户 {UserId} 购买商品 {ProductId} 成功，订单号={OrderNo}",
                userId, dto.ProductId, order.OrderNo);

            return new PurchaseResultDto
            {
                Success = order.Status == OrderStatus.Completed,
                OrderId = orderId,
                OrderNo = order.OrderNo,
                UserBenefitId = userBenefitId,
                DeductedCoins = totalPrice,
                RemainingBalance = newBalance?.VoBalance,
                ErrorMessage = order.Status == OrderStatus.Failed ? order.FailReason : null
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "用户 {UserId} 购买商品 {ProductId} 失败", userId, dto.ProductId);
            return new PurchaseResultDto { Success = false, ErrorMessage = "购买失败，请稍后重试" };
        }
    }

    /// <summary>取消订单</summary>
    public async Task<bool> CancelOrderAsync(long userId, long orderId, string? reason = null)
    {
        try
        {
            var order = await _orderRepository.QueryFirstAsync(o => o.Id == orderId && o.UserId == userId && !o.IsDeleted);
            if (order == null)
            {
                throw new InvalidOperationException("订单不存在");
            }

            if (order.Status != OrderStatus.Pending)
            {
                throw new InvalidOperationException("只能取消待支付的订单");
            }

            order.Status = OrderStatus.Cancelled;
            order.CancelledTime = DateTime.Now;
            order.CancelReason = reason ?? "用户取消";
            order.ModifyTime = DateTime.Now;

            var result = await _orderRepository.UpdateAsync(order);

            if (result)
            {
                Log.Information("订单 {OrderId} 已取消", orderId);
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "取消订单 {OrderId} 失败", orderId);
            throw;
        }
    }

    #endregion

    #region 订单查询

    /// <summary>获取用户订单列表</summary>
    public async Task<PageModel<OrderListItemVo>> GetUserOrdersAsync(
        long userId,
        OrderStatus? status = null,
        int pageIndex = 1,
        int pageSize = 20)
    {
        try
        {
            Expression<Func<Order, bool>> where = o => o.UserId == userId && !o.IsDeleted;

            if (status.HasValue)
            {
                where = where.And(o => o.Status == status.Value);
            }

            var (orders, totalCount) = await _orderRepository.QueryPageAsync(
                whereExpression: where,
                pageIndex: pageIndex,
                pageSize: pageSize,
                orderByExpression: o => o.CreateTime,
                orderByType: OrderByType.Desc);

            var orderVos = Mapper.Map<List<OrderListItemVo>>(orders);

            return new PageModel<OrderListItemVo>
            {
                Page = pageIndex,
                PageSize = pageSize,
                DataCount = totalCount,
                PageCount = (int)Math.Ceiling((double)totalCount / pageSize),
                Data = orderVos
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 订单列表失败", userId);
            throw;
        }
    }

    /// <summary>获取订单详情</summary>
    public async Task<OrderVo?> GetOrderDetailAsync(long userId, long orderId)
    {
        try
        {
            var order = await _orderRepository.QueryFirstAsync(o => o.Id == orderId && o.UserId == userId && !o.IsDeleted);
            if (order == null) return null;

            return Mapper.Map<OrderVo>(order);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取订单 {OrderId} 详情失败", orderId);
            throw;
        }
    }

    /// <summary>根据订单号获取订单</summary>
    public async Task<OrderVo?> GetOrderByNoAsync(string orderNo)
    {
        try
        {
            var order = await _orderRepository.QueryFirstAsync(o => o.OrderNo == orderNo && !o.IsDeleted);
            if (order == null) return null;

            return Mapper.Map<OrderVo>(order);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "根据订单号 {OrderNo} 获取订单失败", orderNo);
            throw;
        }
    }

    /// <summary>获取用户购买某商品的数量</summary>
    public async Task<int> GetUserPurchaseCountAsync(long userId, long productId)
    {
        try
        {
            var count = await _orderRepository.QueryCountAsync(
                o => o.UserId == userId &&
                     o.ProductId == productId &&
                     (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid) &&
                     !o.IsDeleted);
            return count;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 购买商品 {ProductId} 数量失败", userId, productId);
            throw;
        }
    }

    #endregion

    #region 管理员操作

    /// <summary>获取订单列表（管理后台）</summary>
    public async Task<PageModel<OrderVo>> GetOrderListForAdminAsync(
        long? userId = null,
        OrderStatus? status = null,
        long? productId = null,
        string? orderNo = null,
        int pageIndex = 1,
        int pageSize = 20)
    {
        try
        {
            Expression<Func<Order, bool>> where = o => true;

            if (userId.HasValue)
            {
                where = where.And(o => o.UserId == userId.Value);
            }

            if (status.HasValue)
            {
                where = where.And(o => o.Status == status.Value);
            }

            if (productId.HasValue)
            {
                where = where.And(o => o.ProductId == productId.Value);
            }

            if (!string.IsNullOrWhiteSpace(orderNo))
            {
                where = where.And(o => o.OrderNo.Contains(orderNo));
            }

            var (orders, totalCount) = await _orderRepository.QueryPageAsync(
                whereExpression: where,
                pageIndex: pageIndex,
                pageSize: pageSize,
                orderByExpression: o => o.CreateTime,
                orderByType: OrderByType.Desc);

            var orderVos = Mapper.Map<List<OrderVo>>(orders);

            // 填充用户名
            var userIds = orders.Select(o => o.UserId).Distinct().ToList();
            var users = await _userRepository.QueryAsync(u => userIds.Contains(u.Id));
            var userDict = users.ToDictionary(u => u.Id, u => u.UserName);

            foreach (var vo in orderVos)
            {
                if (userDict.TryGetValue(vo.VoUserId, out var userName))
                {
                    vo.VoUserName = userName;
                }
            }

            return new PageModel<OrderVo>
            {
                Page = pageIndex,
                PageSize = pageSize,
                DataCount = totalCount,
                PageCount = (int)Math.Ceiling((double)totalCount / pageSize),
                Data = orderVos
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取订单列表（管理后台）失败");
            throw;
        }
    }

    /// <summary>管理员备注订单</summary>
    public async Task<bool> AdminRemarkOrderAsync(long orderId, string remark)
    {
        try
        {
            var affected = await _orderRepository.UpdateColumnsAsync(
                o => new Order
                {
                    AdminRemark = remark,
                    ModifyTime = DateTime.Now
                },
                o => o.Id == orderId);

            return affected > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "管理员备注订单 {OrderId} 失败", orderId);
            throw;
        }
    }

    /// <summary>重新发放权益（发放失败时使用）</summary>
    public async Task<bool> RetryGrantBenefitAsync(long orderId)
    {
        try
        {
            var order = await _orderRepository.QueryFirstAsync(o => o.Id == orderId);
            if (order == null)
            {
                throw new InvalidOperationException("订单不存在");
            }

            if (order.Status != OrderStatus.Failed)
            {
                throw new InvalidOperationException("只能重试发放失败的订单");
            }

            var product = await _productRepository.QueryFirstAsync(p => p.Id == order.ProductId && !p.IsDeleted);
            if (product == null)
            {
                throw new InvalidOperationException("商品不存在");
            }

            // 重新发放权益
            var userBenefitId = await _userBenefitService.GrantBenefitAsync(order.UserId, product, orderId);

            order.UserBenefitId = userBenefitId;
            order.Status = OrderStatus.Completed;
            order.CompletedTime = DateTime.Now;
            order.FailReason = null;
            order.ModifyTime = DateTime.Now;

            // 计算权益到期时间
            if (product.DurationType == DurationType.Days && product.DurationDays.HasValue)
            {
                order.BenefitExpiresAt = DateTime.Now.AddDays(product.DurationDays.Value);
            }
            else if (product.DurationType == DurationType.FixedDate && product.ExpiresAt.HasValue)
            {
                order.BenefitExpiresAt = product.ExpiresAt;
            }

            var result = await _orderRepository.UpdateAsync(order);

            if (result)
            {
                Log.Information("订单 {OrderId} 权益重新发放成功", orderId);
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "重新发放订单 {OrderId} 权益失败", orderId);
            throw;
        }
    }

    #endregion
}
