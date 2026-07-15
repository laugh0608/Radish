using System.Linq.Expressions;
using AutoMapper;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using Radish.Shared.Security;
using Serilog;
using SqlSugar;

namespace Radish.Service;

/// <summary>订单服务实现</summary>
public class OrderService : BaseService<Order, OrderVo>, IOrderService
{
    private readonly IBaseRepository<Order> _orderRepository;
    private readonly IBaseRepository<Product> _productRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IBaseRepository<CoinTransaction> _coinTransactionRepository;
    private readonly IProductService _productService;
    private readonly IUserBenefitService _userBenefitService;
    private readonly ICoinService _coinService;
    private readonly IPaymentPasswordService _paymentPasswordService;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly IOperationIdempotencyService? _operationIdempotencyService;
    private readonly IReliableOutboxService? _reliableOutboxService;
    private readonly TimeProvider _timeProvider;

    public OrderService(
        IMapper mapper,
        IBaseRepository<Order> orderRepository,
        IBaseRepository<Product> productRepository,
        IBaseRepository<User> userRepository,
        IBaseRepository<CoinTransaction> coinTransactionRepository,
        IProductService productService,
        IUserBenefitService userBenefitService,
        ICoinService coinService,
        IPaymentPasswordService paymentPasswordService,
        IAttachmentUrlResolver attachmentUrlResolver,
        IOperationIdempotencyService? operationIdempotencyService = null,
        INotificationService? notificationService = null,
        IReliableOutboxService? reliableOutboxService = null,
        TimeProvider? timeProvider = null)
        : base(mapper, orderRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _userRepository = userRepository;
        _coinTransactionRepository = coinTransactionRepository;
        _productService = productService;
        _userBenefitService = userBenefitService;
        _coinService = coinService;
        _paymentPasswordService = paymentPasswordService;
        _attachmentUrlResolver = attachmentUrlResolver;
        _operationIdempotencyService = operationIdempotencyService;
        _reliableOutboxService = reliableOutboxService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    #region 购买流程

    /// <summary>创建订单并完成购买</summary>
    [UseTran]
    public async Task<PurchaseResultDto> PurchaseAsync(long userId, CreateOrderDto dto)
    {
        OperationIdempotencyBeginResult? idempotencyResult = null;
        var assetWriteStarted = false;

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

            // 5. 验证支付口令
            var paymentPasscodeError = PaymentPasscodeRules.GetValidationMessage(dto.PaymentPassword);
            if (!string.IsNullOrWhiteSpace(paymentPasscodeError))
            {
                return new PurchaseResultDto
                {
                    Success = false,
                    ErrorMessage = paymentPasscodeError
                };
            }

            var verifyResult = await _paymentPasswordService.VerifyPaymentPasswordAsync(userId, new VerifyPaymentPasswordRequest
            {
                Password = dto.PaymentPassword,
                BusinessType = "ShopPurchase",
                BusinessId = dto.ProductId.ToString()
            });

            if (!verifyResult.IsSuccess)
            {
                Log.Warning("商城购买失败：支付口令验证失败，用户={UserId}, 商品={ProductId}, 原因={Reason}",
                    userId, dto.ProductId, verifyResult.ErrorMessage);
                return new PurchaseResultDto
                {
                    Success = false,
                    ErrorMessage = verifyResult.ErrorMessage ?? "支付口令验证失败",
                    ErrorCode = verifyResult.ErrorCode,
                    RequiresPasscodeUpgrade = verifyResult.RequiresPasscodeUpgrade
                };
            }

            var tenantId = GetCurrentTenantId();
            idempotencyResult = await BeginPurchaseIdempotencyAsync(tenantId, userId, dto);
            var replayResult = ResolvePurchaseIdempotencyResult(idempotencyResult);
            if (replayResult != null)
            {
                return replayResult;
            }

            // 6. 扣减库存
            if (product.StockType == StockType.Limited)
            {
                var stockDeducted = await _productService.DeductStockAsync(dto.ProductId, dto.Quantity);
                if (!stockDeducted)
                {
                    return await CompletePurchaseIdempotencyAsync(
                        idempotencyResult,
                        new PurchaseResultDto { Success = false, ErrorMessage = "库存扣减失败" },
                        shouldOccupyKey: false);
                }

                assetWriteStarted = true;
            }

            // 7. 创建订单
            var order = new Order
            {
                OrderNo = $"ORD_{SnowFlakeSingle.Instance.NextId()}",
                UserId = userId,
                ProductId = product.Id,
                ProductName = product.Name,
                StockType = product.StockType,
                ProductIconAttachmentId = product.IconAttachmentId,
                ProductType = product.ProductType,
                BenefitType = product.BenefitType,
                ConsumableType = product.ConsumableType,
                BenefitValue = product.BenefitValue,
                DurationType = product.DurationType,
                DurationDays = product.DurationDays,
                FixedExpiresAt = product.ExpiresAt,
                Quantity = dto.Quantity,
                UnitPrice = product.Price,
                TotalPrice = totalPrice,
                Status = OrderStatus.Pending,
                FailureStage = OrderFailureStage.None,
                UserRemark = dto.UserRemark,
                TenantId = tenantId,
                CreateTime = GetUtcNow(),
                CreateBy = "User",
                CreateId = userId
            };

            var orderId = await _orderRepository.AddAsync(order);
            order.Id = orderId;
            assetWriteStarted = true;

            // 8. 扣除萝卜币
            try
            {
                var (transactionId, _) = await _coinService.ConsumeCoinAsync(
                    userId,
                    totalPrice,
                    "Order",
                    orderId,
                    $"购买商品：{product.Name}");

                order.CoinTransactionId = transactionId;
                order.Status = OrderStatus.Paid;
                order.FailureStage = OrderFailureStage.None;
                order.PaidTime = GetUtcNow();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "扣除萝卜币失败，订单 {OrderId}", orderId);

                // 恢复库存
                if (product.StockType == StockType.Limited)
                {
                    await _productService.RestoreStockAsync(dto.ProductId, dto.Quantity, product.StockType);
                }

                order.Status = OrderStatus.Failed;
                order.FailureStage = OrderFailureStage.Payment;
                order.FailReason = $"扣除萝卜币失败：{ex.Message}";
                await _orderRepository.UpdateAsync(order);

                return await CompletePurchaseIdempotencyAsync(
                    idempotencyResult,
                    new PurchaseResultDto
                    {
                        Success = false,
                        OrderId = orderId,
                        OrderNo = order.OrderNo,
                        ErrorMessage = "扣除萝卜币失败"
                    },
                    shouldOccupyKey: assetWriteStarted,
                    resourceId: orderId,
                    resourceNo: order.OrderNo);
            }

            // 9. 发放权益
            OrderFulfillmentResultDto? fulfillmentResult = null;
            try
            {
                fulfillmentResult = await _userBenefitService.GrantOrderFulfillmentAsync(order);
                order.GrantedBenefitId = fulfillmentResult.GrantedBenefitId;
                order.GrantedInventoryId = fulfillmentResult.GrantedInventoryId;
                order.BenefitExpiresAt = fulfillmentResult.ExpiresAt;
                order.Status = OrderStatus.Completed;
                order.FailureStage = OrderFailureStage.None;
                order.CompletedTime = GetUtcNow();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "发放权益失败，订单 {OrderId}", orderId);
                order.Status = OrderStatus.Failed;
                order.FailureStage = OrderFailureStage.Fulfillment;
                order.FailReason = $"发放权益失败：{ex.Message}";
            }

            // 10. 更新订单状态
            await _orderRepository.UpdateAsync(order);

            // 11. 增加已售数量
            await _productService.IncreaseSoldCountAsync(dto.ProductId, dto.Quantity);

            // 12. 获取最新余额
            var newBalance = await _coinService.GetBalanceAsync(userId);

            // 13. 发送通知
            if (order.Status == OrderStatus.Completed)
            {
                var reliableOutboxService = _reliableOutboxService
                    ?? throw new InvalidOperationException("可靠 Outbox 服务未注册");
                var notification = new CreateNotificationDto
                {
                    NotificationId = SnowFlakeSingle.Instance.NextId(),
                    BusinessKey = $"notification:purchase-success:order:{orderId}",
                    Type = "PURCHASE_SUCCESS",
                    Title = "购买成功",
                    Content = $"您已成功购买 {product.Name}",
                    BusinessType = "Order",
                    BusinessId = orderId,
                    ReceiverUserIds = [userId],
                    TenantId = order.TenantId
                };
                await reliableOutboxService.AddAsync(
                    ReliableOutboxSources.Main,
                    order.TenantId,
                    ReliableTaskTypes.NotificationRequested,
                    $"task:notification:purchase-success:order:{orderId}",
                    "Order",
                    orderId.ToString(),
                    new NotificationRequestedTaskPayload(notification),
                    GetUtcNow());
            }

            Log.Information("用户 {UserId} 购买商品 {ProductId} 成功，订单号={OrderNo}",
                userId, dto.ProductId, order.OrderNo);

            var purchaseResult = new PurchaseResultDto
            {
                Success = order.Status == OrderStatus.Completed,
                OrderId = orderId,
                OrderNo = order.OrderNo,
                UserBenefitId = fulfillmentResult?.GrantedBenefitId ?? fulfillmentResult?.GrantedInventoryId,
                GrantedBenefitId = fulfillmentResult?.GrantedBenefitId,
                GrantedInventoryId = fulfillmentResult?.GrantedInventoryId,
                DeductedCoins = totalPrice,
                RemainingBalance = newBalance?.VoBalance,
                ErrorMessage = order.Status == OrderStatus.Failed ? order.FailReason : null
            };

            return await CompletePurchaseIdempotencyAsync(
                idempotencyResult,
                purchaseResult,
                shouldOccupyKey: true,
                resourceId: orderId,
                resourceNo: order.OrderNo);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "用户 {UserId} 购买商品 {ProductId} 失败", userId, dto.ProductId);
            throw new BusinessException("购买失败，请稍后重试", ex);
        }
    }

    /// <summary>取消订单</summary>
    [UseTran]
    public async Task<bool> CancelOrderAsync(long userId, long orderId, string? reason = null)
    {
        try
        {
            var order = await _orderRepository.QueryFirstAsync(o => o.Id == orderId && o.UserId == userId && !o.IsDeleted);
            if (order == null)
            {
                throw new InvalidOperationException("订单不存在");
            }

            return await CancelPendingOrderAsync(
                order,
                string.IsNullOrWhiteSpace(reason) ? "用户取消" : reason.Trim(),
                "User",
                userId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "取消订单 {OrderId} 失败", orderId);
            throw;
        }
    }

    /// <summary>系统取消订单</summary>
    [UseTran]
    public async Task<bool> CancelOrderBySystemAsync(long orderId, string reason)
    {
        try
        {
            var order = await _orderRepository.QueryFirstAsync(o => o.Id == orderId && !o.IsDeleted);
            if (order == null)
            {
                throw new InvalidOperationException("订单不存在");
            }

            return await CancelPendingOrderAsync(
                order,
                string.IsNullOrWhiteSpace(reason) ? "系统取消" : reason.Trim(),
                "System",
                0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "系统取消订单 {OrderId} 失败", orderId);
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
            FillOrderListItemUrls(orderVos);

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

            var orderVo = Mapper.Map<OrderVo>(order);
            FillOrderUrl(orderVo);
            return orderVo;
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

            var orderVo = Mapper.Map<OrderVo>(order);
            FillOrderUrl(orderVo);
            return orderVo;
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
            Expression<Func<Order, bool>> where = o => !o.IsDeleted;

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
            await FillOrderUsersAsync(orderVos);
            FillOrderUrls(orderVos);

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

    /// <summary>获取订单详情（管理后台）</summary>
    public async Task<OrderVo?> GetOrderDetailForAdminAsync(long orderId)
    {
        try
        {
            var order = await _orderRepository.QueryFirstAsync(o => o.Id == orderId && !o.IsDeleted);
            if (order == null)
            {
                return null;
            }

            var orderVo = Mapper.Map<OrderVo>(order);
            await FillOrderUsersAsync([orderVo]);
            FillOrderUrl(orderVo);
            return orderVo;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取订单 {OrderId} 详情（管理后台）失败", orderId);
            throw;
        }
    }

    /// <summary>管理员备注订单</summary>
    public async Task<bool> AdminRemarkOrderAsync(long orderId, string remark, long operatorId, string operatorName)
    {
        try
        {
            var order = await _orderRepository.QueryFirstAsync(o => o.Id == orderId && !o.IsDeleted);
            if (order == null)
            {
                throw new BusinessException(
                    "订单不存在",
                    (int)HttpStatusCodeEnum.NotFound,
                    "Order.NotFound",
                    "error.order.not_found");
            }

            order.AdminRemark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
            order.ModifyTime = GetUtcNow();
            order.ModifyBy = string.IsNullOrWhiteSpace(operatorName) ? "Unknown" : operatorName.Trim();
            order.ModifyId = operatorId;

            var result = await _orderRepository.UpdateAsync(order);
            if (result)
            {
                Log.Information("管理员 {OperatorName}({OperatorId}) 更新订单 {OrderId} 备注成功",
                    order.ModifyBy, operatorId, orderId);
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "管理员备注订单 {OrderId} 失败", orderId);
            throw;
        }
    }

    /// <summary>重新发放权益（发放失败时使用）</summary>
    [UseTran]
    public async Task<bool> RetryGrantBenefitAsync(long orderId)
    {
        try
        {
            var order = await _orderRepository.QueryFirstAsync(o => o.Id == orderId && !o.IsDeleted);
            if (order == null)
            {
                throw new BusinessException(
                    "订单不存在",
                    (int)HttpStatusCodeEnum.NotFound,
                    "Order.NotFound",
                    "error.order.not_found");
            }

            if (order.Status != OrderStatus.Failed)
            {
                throw BuildRetryRejected("只能重试发放失败的订单");
            }

            if (order.FailureStage != OrderFailureStage.Fulfillment)
            {
                throw BuildRetryRejected("支付阶段失败的订单不能重试发放");
            }

            await EnsureValidPaymentEvidenceAsync(order);

            var fulfillmentResult = await _userBenefitService.GrantOrderFulfillmentAsync(order);
            order.GrantedBenefitId = fulfillmentResult.GrantedBenefitId;
            order.GrantedInventoryId = fulfillmentResult.GrantedInventoryId;
            order.BenefitExpiresAt = fulfillmentResult.ExpiresAt;
            order.Status = OrderStatus.Completed;
            order.FailureStage = OrderFailureStage.None;
            order.CompletedTime = GetUtcNow();
            order.FailReason = null;
            order.ModifyTime = GetUtcNow();

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

    private async Task EnsureValidPaymentEvidenceAsync(Order order)
    {
        if (!order.PaidTime.HasValue || !order.CoinTransactionId.HasValue)
        {
            throw BuildRetryRejected("订单缺少有效支付证据，不能重试发放");
        }

        var transaction = await _coinTransactionRepository.QueryFirstAsync(transaction =>
            transaction.Id == order.CoinTransactionId.Value &&
            transaction.TenantId == order.TenantId &&
            transaction.FromUserId == order.UserId &&
            transaction.TransactionType == "CONSUME" &&
            transaction.Status == "SUCCESS" &&
            transaction.BusinessType == "Order" &&
            transaction.BusinessId == order.Id &&
            transaction.Amount == order.TotalPrice);

        if (transaction == null)
        {
            throw BuildRetryRejected("订单扣款流水与履约快照不匹配，不能自动重试发放");
        }
    }

    private static BusinessException BuildRetryRejected(string message)
    {
        return new BusinessException(
            message,
            (int)HttpStatusCodeEnum.Conflict,
            "Order.RetryRejected",
            "error.order.retry_rejected");
    }

    private static long NormalizeTenantId(long tenantId)
    {
        return tenantId > 0 ? tenantId : 0;
    }

    private static long GetCurrentTenantId()
    {
        try
        {
            return NormalizeTenantId(App.CurrentUser.TenantId);
        }
        catch (Exception ex) when (ex is ArgumentNullException or InvalidOperationException)
        {
            return 0;
        }
    }

    private async Task<OperationIdempotencyBeginResult?> BeginPurchaseIdempotencyAsync(
        long tenantId,
        long userId,
        CreateOrderDto dto)
    {
        if (_operationIdempotencyService == null)
        {
            return null;
        }

        var key = _operationIdempotencyService.NormalizeKey(dto.IdempotencyKey);
        if (key == null)
        {
            return null;
        }

        var snapshot = _operationIdempotencyService.CreateRequestSnapshot(
            new Dictionary<string, object?>
            {
                ["productId"] = dto.ProductId,
                ["quantity"] = dto.Quantity,
                ["userRemark"] = NormalizeRequestSummaryText(dto.UserRemark)
            });

        return await _operationIdempotencyService.BeginAsync(new OperationIdempotencyBeginRequest
        {
            TenantId = tenantId,
            UserId = userId,
            OperationType = OperationIdempotencyOperationTypes.ShopPurchase,
            IdempotencyKey = key,
            RequestHash = snapshot.RequestHash,
            RequestSummary = snapshot.RequestSummary
        });
    }

    private PurchaseResultDto? ResolvePurchaseIdempotencyResult(OperationIdempotencyBeginResult? idempotencyResult)
    {
        if (idempotencyResult == null || idempotencyResult.Status == OperationIdempotencyBeginStatus.Started)
        {
            return null;
        }

        return idempotencyResult.Status switch
        {
            OperationIdempotencyBeginStatus.Succeeded => ResolvePurchaseReplayResult(idempotencyResult),
            OperationIdempotencyBeginStatus.Processing => new PurchaseResultDto
            {
                Success = false,
                ErrorMessage = idempotencyResult.Message ?? "请求处理中，请稍后查询结果或重试"
            },
            OperationIdempotencyBeginStatus.Conflict => new PurchaseResultDto
            {
                Success = false,
                ErrorMessage = idempotencyResult.Message ?? "幂等键已被不同请求使用"
            },
            OperationIdempotencyBeginStatus.InvalidKey => new PurchaseResultDto
            {
                Success = false,
                ErrorMessage = idempotencyResult.Message ?? "幂等键格式无效"
            },
            _ => new PurchaseResultDto
            {
                Success = false,
                ErrorMessage = "幂等记录状态无效"
            }
        };
    }

    private PurchaseResultDto ResolvePurchaseReplayResult(OperationIdempotencyBeginResult idempotencyResult)
    {
        var replayResult = _operationIdempotencyService?.DeserializeResponse<PurchaseResultDto>(
            idempotencyResult.ResponsePayload);

        return replayResult ?? new PurchaseResultDto
        {
            Success = false,
            ErrorMessage = "幂等记录缺少响应，请稍后重试"
        };
    }

    private async Task<PurchaseResultDto> CompletePurchaseIdempotencyAsync(
        OperationIdempotencyBeginResult? idempotencyResult,
        PurchaseResultDto result,
        bool shouldOccupyKey,
        long? resourceId = null,
        string? resourceNo = null)
    {
        if (_operationIdempotencyService == null ||
            idempotencyResult?.Status != OperationIdempotencyBeginStatus.Started ||
            !idempotencyResult.RecordId.HasValue)
        {
            return result;
        }

        if (shouldOccupyKey)
        {
            await _operationIdempotencyService.CompleteSuccessAsync(new OperationIdempotencyCompletionRequest
            {
                RecordId = idempotencyResult.RecordId.Value,
                ResourceType = OperationIdempotencyResourceTypes.Order,
                ResourceId = resourceId,
                ResourceNo = resourceNo,
                ErrorCode = result.ErrorCode,
                ErrorMessage = result.ErrorMessage,
                ResponsePayload = _operationIdempotencyService.SerializeResponse(result)
            });
        }
        else
        {
            await _operationIdempotencyService.CompleteFailureAsync(
                idempotencyResult.RecordId.Value,
                result.ErrorCode,
                result.ErrorMessage);
        }

        return result;
    }

    private static string NormalizeRequestSummaryText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
    }

    private async Task<bool> CancelPendingOrderAsync(Order order, string cancelReason, string operatorName, long operatorId)
    {
        if (order.Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException("只能取消待支付的订单");
        }

        var cancelledTime = GetUtcNow();
        var affected = await _orderRepository.UpdateColumnsAsync(
            o => new Order
            {
                Status = OrderStatus.Cancelled,
                CancelledTime = cancelledTime,
                CancelReason = cancelReason,
                ModifyTime = cancelledTime,
                ModifyBy = operatorName,
                ModifyId = operatorId
            },
            o => o.Id == order.Id && o.UserId == order.UserId && o.Status == OrderStatus.Pending && !o.IsDeleted);

        if (affected <= 0)
        {
            throw new InvalidOperationException("订单状态已变更，请刷新后重试");
        }

        if (order.StockType == StockType.Limited)
        {
            var stockRestored = await _productService.RestoreStockAsync(order.ProductId, order.Quantity, order.StockType);
            if (!stockRestored)
            {
                throw new InvalidOperationException("取消订单失败，库存回补未完成");
            }
        }

        Log.Information(
            "订单 {OrderId} 已取消，用户={UserId}，数量={Quantity}，原因={Reason}",
            order.Id,
            order.UserId,
            order.Quantity,
            cancelReason);

        return true;
    }

    private void FillOrderUrls(List<OrderVo> orders)
    {
        foreach (var order in orders)
        {
            FillOrderUrl(order);
        }
    }

    private async Task FillOrderUsersAsync(IReadOnlyCollection<OrderVo> orders)
    {
        if (orders.Count == 0)
        {
            return;
        }

        var userIds = orders
            .Select(order => order.VoUserId)
            .Distinct()
            .ToList();
        if (userIds.Count == 0)
        {
            return;
        }

        var users = await _userRepository.QueryAsync(user => userIds.Contains(user.Id));
        var userDict = users.ToDictionary(
            user => user.Id,
            user => User.BuildDisplayHandle(user.UserName, user.PublicIndex, user.Id)
                ?? User.NormalizeDisplayName(user.UserName, user.Id));

        foreach (var order in orders)
        {
            if (userDict.TryGetValue(order.VoUserId, out var userName))
            {
                order.VoUserName = userName;
            }
        }
    }

    private void FillOrderUrl(OrderVo order)
    {
        order.VoProductIcon = ResolveAttachmentUrl(order.VoProductIconAttachmentId);
    }

    private void FillOrderListItemUrls(List<OrderListItemVo> orders)
    {
        foreach (var order in orders)
        {
            order.VoProductIcon = ResolveAttachmentUrl(order.VoProductIconAttachmentId);
        }
    }

    private string? ResolveAttachmentUrl(long? attachmentId)
    {
        if (!attachmentId.HasValue || attachmentId.Value <= 0)
        {
            return null;
        }

        return _attachmentUrlResolver.ResolveAttachmentUrl(attachmentId.Value);
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }
}
