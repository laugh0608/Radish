# 4. 订单系统

> 入口页：[商城系统设计方案](/guide/shop-system)

## 4.1 订单数据模型

### 4.1.1 订单实体（Order）

```csharp
/// <summary>
/// 订单实体
/// </summary>
[SugarTable("shop_order")]
public class Order : RootEntityTKey<long>, ITenantEntity
{
    #region 基础信息

    /// <summary>
    /// 订单号（展示用，格式：yyyyMMddHHmmss + 6位随机数）
    /// </summary>
    [SugarColumn(Length = 30)]
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 用户 ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 商品 ID
    /// </summary>
    public long ProductId { get; set; }

    /// <summary>
    /// 购买数量
    /// </summary>
    public int Quantity { get; set; } = 1;

    #endregion

    #region 商品快照

    /// <summary>
    /// 商品名称（快照）
    /// </summary>
    [SugarColumn(Length = 100)]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// 商品图片（快照）
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ProductImage { get; set; }

    /// <summary>
    /// 商品类型（快照）
    /// </summary>
    public ProductType ProductType { get; set; }

    /// <summary>
    /// 权益类型（快照）
    /// </summary>
    public BenefitType? BenefitType { get; set; }

    /// <summary>
    /// 消耗品类型（快照）
    /// </summary>
    public ConsumableType? ConsumableType { get; set; }

    /// <summary>
    /// 效果参数（快照）
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? EffectParams { get; set; }

    /// <summary>
    /// 有效期类型（快照）
    /// </summary>
    public DurationType DurationType { get; set; }

    /// <summary>
    /// 有效期天数（快照）
    /// </summary>
    public int? DurationDays { get; set; }

    #endregion

    #region 金额信息

    /// <summary>
    /// 商品原价（单价）
    /// </summary>
    public int OriginalPrice { get; set; }

    /// <summary>
    /// 商品现价（单价）
    /// </summary>
    public int UnitPrice { get; set; }

    /// <summary>
    /// 订单总金额（UnitPrice × Quantity）
    /// </summary>
    public int TotalAmount { get; set; }

    /// <summary>
    /// 实付金额（萝卜币）
    /// </summary>
    public int PaidAmount { get; set; }

    /// <summary>
    /// 优惠金额（预留）
    /// </summary>
    public int DiscountAmount { get; set; }

    #endregion

    #region 订单状态

    /// <summary>
    /// 订单状态
    /// </summary>
    public OrderStatus Status { get; set; }

    /// <summary>
    /// 支付时间
    /// </summary>
    public DateTime? PaidTime { get; set; }

    /// <summary>
    /// 完成时间（权益发放时间）
    /// </summary>
    public DateTime? CompletedTime { get; set; }

    /// <summary>
    /// 取消时间
    /// </summary>
    public DateTime? CancelledTime { get; set; }

    /// <summary>
    /// 取消原因
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string? CancelReason { get; set; }

    /// <summary>
    /// 失败原因
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? FailReason { get; set; }

    #endregion

    #region 权益发放

    /// <summary>
    /// 发放的权益/背包记录 ID
    /// </summary>
    public long? InventoryId { get; set; }

    /// <summary>
    /// 权益开始时间
    /// </summary>
    public DateTime? BenefitStartTime { get; set; }

    /// <summary>
    /// 权益结束时间
    /// </summary>
    public DateTime? BenefitEndTime { get; set; }

    #endregion

    #region 审计字段

    /// <summary>
    /// 租户 ID
    /// </summary>
    public long TenantId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdateTime { get; set; }

    /// <summary>
    /// 是否删除（软删除）
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Remark { get; set; }

    #endregion
}
```

---

## 4.2 订单创建流程

### 4.2.1 创建订单服务

```csharp
public class OrderService : IOrderService
{
    /// <summary>
    /// 创建订单（一步完成：创建 + 支付 + 发放）
    /// </summary>
    [UseTran]
    public async Task<OrderResultVo> CreateOrderAsync(long userId, OrderCreateVo input)
    {
        // 1. 获取商品信息
        var product = await _productRepository.QueryFirstAsync(p => p.Id == input.ProductId);
        if (product == null)
            throw new BusinessException("商品不存在");

        // 2. 限购检查
        var checkResult = await _purchaseLimitChecker.CheckAsync(product, userId, input.Quantity);
        if (!checkResult.CanPurchase)
            throw new BusinessException(checkResult.Message);

        // 3. 计算金额
        var totalAmount = product.Price * input.Quantity;

        // 4. 检查余额
        var balance = await _coinService.GetBalanceAsync(userId);
        if (balance < totalAmount)
            throw new BusinessException($"萝卜币不足，需要 {totalAmount}，当前余额 {balance}");

        // 5. 创建订单
        var order = new Order
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            OrderNo = GenerateOrderNo(),
            UserId = userId,
            ProductId = product.Id,
            Quantity = input.Quantity,

            // 商品快照
            ProductName = product.Name,
            ProductImage = product.ImageUrl,
            ProductType = product.ProductType,
            BenefitType = product.BenefitType,
            ConsumableType = product.ConsumableType,
            EffectParams = product.EffectParams,
            DurationType = product.DurationType,
            DurationDays = product.DurationDays,

            // 金额
            OriginalPrice = product.OriginalPrice,
            UnitPrice = product.Price,
            TotalAmount = totalAmount,
            PaidAmount = totalAmount,

            // 状态
            Status = OrderStatus.Pending,
            CreateTime = DateTime.Now
        };

        await _orderRepository.AddAsync(order);

        // 6. 扣减萝卜币
        var deductResult = await _coinService.DeductAsync(
            userId: userId,
            amount: totalAmount,
            reason: $"购买商品：{product.Name}",
            businessType: "ShopOrder",
            businessId: order.Id.ToString()
        );

        if (!deductResult.Success)
        {
            order.Status = OrderStatus.Cancelled;
            order.CancelReason = "支付失败：" + deductResult.Message;
            order.CancelledTime = DateTime.Now;
            await _orderRepository.UpdateAsync(order);
            throw new BusinessException("支付失败：" + deductResult.Message);
        }

        // 7. 更新订单状态为已支付
        order.Status = OrderStatus.Paid;
        order.PaidTime = DateTime.Now;
        await _orderRepository.UpdateAsync(order);

        // 8. 扣减库存
        try
        {
            await _productService.DeductStockAsync(product.Id, input.Quantity);
        }
        catch (Exception ex)
        {
            // 库存扣减失败，回滚萝卜币
            await _coinService.RefundAsync(
                userId: userId,
                amount: totalAmount,
                reason: $"订单取消退款：{order.OrderNo}",
                businessType: "ShopOrderRefund",
                businessId: order.Id.ToString()
            );

            order.Status = OrderStatus.Cancelled;
            order.CancelReason = "库存扣减失败：" + ex.Message;
            order.CancelledTime = DateTime.Now;
            await _orderRepository.UpdateAsync(order);
            throw new BusinessException("库存不足，订单已取消");
        }

        // 9. 发放权益
        try
        {
            var inventoryId = await _inventoryService.GrantAsync(order);
            order.InventoryId = inventoryId;
            order.Status = OrderStatus.Completed;
            order.CompletedTime = DateTime.Now;
            await _orderRepository.UpdateAsync(order);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "订单 {OrderNo} 权益发放失败", order.OrderNo);
            order.Status = OrderStatus.Failed;
            order.FailReason = ex.Message;
            await _orderRepository.UpdateAsync(order);
            // 不抛出异常，让用户知道订单已支付，等待人工处理
        }

        // 10. 发送通知
        await SendOrderNotificationAsync(order);

        return new OrderResultVo
        {
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            Status = order.Status,
            Message = order.Status == OrderStatus.Completed
                ? "购买成功"
                : "订单已支付，权益发放中"
        };
    }
}
```

### 4.2.2 订单号生成

```csharp
private string GenerateOrderNo()
{
    // 格式：yyyyMMddHHmmss + 6位随机数
    var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
    var random = new Random().Next(100000, 999999);
    return $"{timestamp}{random}";
}
```

---

## 4.3 订单查询

### 4.3.1 用户订单列表

```csharp
public async Task<PageModel<OrderListVo>> GetUserOrdersAsync(
    long userId,
    OrderQueryVo query)
{
    Expression<Func<Order, bool>> where = o => o.UserId == userId && !o.IsDeleted;

    // 状态筛选
    if (query.Status.HasValue)
        where = where.And(o => o.Status == query.Status.Value);

    // 商品类型筛选
    if (query.ProductType.HasValue)
        where = where.And(o => o.ProductType == query.ProductType.Value);

    // 时间范围筛选
    if (query.StartTime.HasValue)
        where = where.And(o => o.CreateTime >= query.StartTime.Value);
    if (query.EndTime.HasValue)
        where = where.And(o => o.CreateTime < query.EndTime.Value.AddDays(1));

    var (data, total) = await _orderRepository.QueryPageAsync(
        whereExpression: where,
        pageIndex: query.PageIndex,
        pageSize: query.PageSize,
        orderByExpression: o => o.CreateTime,
        orderByType: OrderByType.Desc
    );

    return new PageModel<OrderListVo>
    {
        Items = _mapper.Map<List<OrderListVo>>(data),
        Total = total,
        PageIndex = query.PageIndex,
        PageSize = query.PageSize
    };
}
```

### 4.3.2 订单详情

```csharp
public async Task<OrderDetailVo?> GetOrderDetailAsync(long userId, long orderId)
{
    var order = await _orderRepository.QueryFirstAsync(
        o => o.Id == orderId && o.UserId == userId && !o.IsDeleted
    );

    if (order == null) return null;

    var vo = _mapper.Map<OrderDetailVo>(order);

    // 获取关联的权益信息
    if (order.InventoryId.HasValue)
    {
        var inventory = await _inventoryRepository.QueryFirstAsync(
            i => i.Id == order.InventoryId.Value
        );
        vo.Inventory = _mapper.Map<InventoryBriefVo>(inventory);
    }

    return vo;
}
```

---

## 4.4 订单状态管理

### 4.4.1 取消订单

```csharp
public async Task<bool> CancelOrderAsync(long userId, long orderId, string? reason = null)
{
    var order = await _orderRepository.QueryFirstAsync(
        o => o.Id == orderId && o.UserId == userId
    );

    if (order == null)
        throw new BusinessException("订单不存在");

    if (order.Status != OrderStatus.Pending)
        throw new BusinessException("只能取消待支付的订单");

    order.Status = OrderStatus.Cancelled;
    order.CancelReason = reason ?? "用户取消";
    order.CancelledTime = DateTime.Now;
    order.UpdateTime = DateTime.Now;

    await _orderRepository.UpdateAsync(order);

    Log.Information("订单 {OrderNo} 已取消，原因：{Reason}", order.OrderNo, order.CancelReason);

    return true;
}
```

### 4.4.2 订单超时取消（定时任务）

```csharp
public async Task CancelTimeoutOrdersAsync()
{
    var timeout = DateTime.Now.AddMinutes(-30); // 30 分钟超时

    var timeoutOrders = await _orderRepository.QueryAsync(
        o => o.Status == OrderStatus.Pending && o.CreateTime < timeout
    );

    foreach (var order in timeoutOrders)
    {
        order.Status = OrderStatus.Cancelled;
        order.CancelReason = "支付超时自动取消";
        order.CancelledTime = DateTime.Now;
        order.UpdateTime = DateTime.Now;

        await _orderRepository.UpdateAsync(order);

        Log.Information("订单 {OrderNo} 超时取消", order.OrderNo);
    }
}
```

### 4.4.3 重试失败订单（管理员）

```csharp
[Authorize(Policy = "SystemOrAdmin")]
public async Task<bool> RetryFailedOrderAsync(long orderId)
{
    var order = await _orderRepository.QueryFirstAsync(o => o.Id == orderId);

    if (order == null)
        throw new BusinessException("订单不存在");

    if (order.Status != OrderStatus.Failed)
        throw new BusinessException("只能重试失败的订单");

    try
    {
        var inventoryId = await _inventoryService.GrantAsync(order);
        order.InventoryId = inventoryId;
        order.Status = OrderStatus.Completed;
        order.CompletedTime = DateTime.Now;
        order.UpdateTime = DateTime.Now;
        order.Remark = $"管理员重试成功 @ {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        await _orderRepository.UpdateAsync(order);

        // 发送通知
        await SendOrderNotificationAsync(order);

        Log.Information("订单 {OrderNo} 重试成功", order.OrderNo);
        return true;
    }
    catch (Exception ex)
    {
        Log.Error(ex, "订单 {OrderNo} 重试失败", order.OrderNo);
        order.FailReason = $"重试失败：{ex.Message}";
        order.UpdateTime = DateTime.Now;
        await _orderRepository.UpdateAsync(order);
        throw;
    }
}
```

---

## 4.5 订单通知

### 4.5.1 发送订单通知

```csharp
private async Task SendOrderNotificationAsync(Order order)
{
    string title, content;

    switch (order.Status)
    {
        case OrderStatus.Completed:
            title = "购买成功";
            content = $"您已成功购买「{order.ProductName}」";
            if (order.BenefitEndTime.HasValue)
            {
                content += $"，有效期至 {order.BenefitEndTime:yyyy-MM-dd HH:mm}";
            }
            break;

        case OrderStatus.Failed:
            title = "订单异常";
            content = $"订单「{order.OrderNo}」权益发放失败，我们正在处理中";
            break;

        case OrderStatus.Cancelled:
            title = "订单已取消";
            content = $"订单「{order.OrderNo}」已取消";
            break;

        default:
            return;
    }

    await _notificationService.SendNotificationAsync(
        userId: order.UserId,
        type: NotificationType.System,
        title: title,
        content: content,
        relatedType: "Order",
        relatedId: order.Id.ToString()
    );
}
```

---

## 4.6 订单统计

### 4.6.1 用户订单统计

```csharp
public async Task<UserOrderStatsVo> GetUserOrderStatsAsync(long userId)
{
    var orders = await _orderRepository.QueryAsync(
        o => o.UserId == userId && !o.IsDeleted
    );

    return new UserOrderStatsVo
    {
        TotalOrders = orders.Count,
        CompletedOrders = orders.Count(o => o.Status == OrderStatus.Completed),
        TotalSpent = orders
            .Where(o => o.Status == OrderStatus.Completed)
            .Sum(o => o.PaidAmount),
        RecentOrders = orders
            .OrderByDescending(o => o.CreateTime)
            .Take(5)
            .Select(o => new OrderBriefVo
            {
                OrderNo = o.OrderNo,
                ProductName = o.ProductName,
                PaidAmount = o.PaidAmount,
                Status = o.Status,
                CreateTime = o.CreateTime
            })
            .ToList()
    };
}
```

### 4.6.2 商品销售统计（管理员）

```csharp
public async Task<ProductSalesStatsVo> GetProductSalesStatsAsync(
    long productId,
    DateTime? startTime = null,
    DateTime? endTime = null)
{
    Expression<Func<Order, bool>> where = o =>
        o.ProductId == productId &&
        o.Status == OrderStatus.Completed;

    if (startTime.HasValue)
        where = where.And(o => o.CreateTime >= startTime.Value);
    if (endTime.HasValue)
        where = where.And(o => o.CreateTime < endTime.Value.AddDays(1));

    var orders = await _orderRepository.QueryAsync(where);

    return new ProductSalesStatsVo
    {
        ProductId = productId,
        TotalSales = orders.Count,
        TotalQuantity = orders.Sum(o => o.Quantity),
        TotalRevenue = orders.Sum(o => o.PaidAmount),
        DailySales = orders
            .GroupBy(o => o.CreateTime.Date)
            .Select(g => new DailySalesVo
            {
                Date = g.Key,
                Sales = g.Count(),
                Revenue = g.Sum(o => o.PaidAmount)
            })
            .OrderBy(d => d.Date)
            .ToList()
    };
}
```

---

## 4.7 订单安全

### 4.7.1 幂等性保证

```csharp
/// <summary>
/// 使用幂等键防止重复下单
/// </summary>
public async Task<OrderResultVo> CreateOrderWithIdempotencyAsync(
    long userId,
    OrderCreateVo input,
    string idempotencyKey)
{
    // 检查幂等键是否已使用
    var cacheKey = $"order:idempotency:{userId}:{idempotencyKey}";
    var existingOrderId = await _cache.GetAsync<long?>(cacheKey);

    if (existingOrderId.HasValue)
    {
        // 返回已存在的订单
        var existingOrder = await _orderRepository.QueryFirstAsync(
            o => o.Id == existingOrderId.Value
        );
        return new OrderResultVo
        {
            OrderId = existingOrder.Id,
            OrderNo = existingOrder.OrderNo,
            Status = existingOrder.Status,
            Message = "订单已存在"
        };
    }

    // 创建新订单
    var result = await CreateOrderAsync(userId, input);

    // 缓存幂等键（24 小时）
    await _cache.SetAsync(cacheKey, result.OrderId, TimeSpan.FromHours(24));

    return result;
}
```

### 4.7.2 防刷机制

```csharp
/// <summary>
/// 检查用户下单频率
/// </summary>
private async Task CheckOrderRateLimitAsync(long userId)
{
    var cacheKey = $"order:rate:{userId}";
    var recentCount = await _cache.GetAsync<int>(cacheKey);

    if (recentCount >= 10) // 每分钟最多 10 单
    {
        throw new BusinessException("操作过于频繁，请稍后再试");
    }

    await _cache.SetAsync(cacheKey, recentCount + 1, TimeSpan.FromMinutes(1));
}
```

---

> 下一篇：[5. 权益系统](/guide/shop-inventory)
