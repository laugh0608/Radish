# 6. 后端设计

> 入口页：[商城系统设计方案](/guide/shop-system)

## 6.1 项目结构

### 6.1.1 分层组织

```
Radish.Model
├── Models
│   ├── Product.cs                  # 商品实体
│   ├── ProductCategory.cs          # 商品分类实体
│   ├── Order.cs                    # 订单实体
│   └── UserInventory.cs            # 用户权益/背包实体
├── ViewModels
│   ├── Product/
│   │   ├── ProductListVo.cs        # 商品列表 ViewModel
│   │   ├── ProductDetailVo.cs      # 商品详情 ViewModel
│   │   ├── ProductCreateVo.cs      # 创建商品输入
│   │   └── ProductUpdateVo.cs      # 更新商品输入
│   ├── Order/
│   │   ├── OrderListVo.cs          # 订单列表 ViewModel
│   │   ├── OrderDetailVo.cs        # 订单详情 ViewModel
│   │   ├── OrderCreateVo.cs        # 创建订单输入
│   │   └── OrderResultVo.cs        # 订单结果输出
│   └── Inventory/
│       ├── UserInventoryVo.cs      # 用户背包 ViewModel
│       ├── BenefitItemVo.cs        # 权益物品 ViewModel
│       ├── ConsumableItemVo.cs     # 消耗品 ViewModel
│       └── UseConsumableVo.cs      # 使用消耗品输入
└── Enums
    ├── ProductType.cs              # 商品类型枚举
    ├── BenefitType.cs              # 权益类型枚举
    ├── ConsumableType.cs           # 消耗品类型枚举
    ├── OrderStatus.cs              # 订单状态枚举
    ├── StockType.cs                # 库存类型枚举
    ├── DurationType.cs             # 有效期类型枚举
    ├── InventoryItemType.cs        # 背包物品类型枚举
    ├── InventoryStatus.cs          # 背包物品状态枚举
    └── InventorySourceType.cs      # 权益来源类型枚举

Radish.IRepository
├── IProductRepository.cs           # 商品仓储接口
├── IProductCategoryRepository.cs   # 商品分类仓储接口
├── IOrderRepository.cs             # 订单仓储接口
└── IUserInventoryRepository.cs     # 用户背包仓储接口

Radish.Repository
├── ProductRepository.cs            # 商品仓储实现
├── ProductCategoryRepository.cs    # 商品分类仓储实现
├── OrderRepository.cs              # 订单仓储实现
└── UserInventoryRepository.cs      # 用户背包仓储实现

Radish.IServices
├── IProductService.cs              # 商品服务接口
├── IOrderService.cs                # 订单服务接口
└── IInventoryService.cs            # 权益服务接口

Radish.Services
├── ProductService.cs               # 商品服务实现
├── OrderService.cs                 # 订单服务实现
├── InventoryService.cs             # 权益服务实现
└── PurchaseLimitChecker.cs         # 限购检查服务

Radish.Api/Controllers
├── ShopController.cs               # 商城控制器
├── ProductController.cs            # 商品管理控制器（管理员）
├── OrderController.cs              # 订单控制器
└── InventoryController.cs          # 背包控制器

Radish.Extension/AutoMapperExtension/CustomProfiles
└── ShopProfile.cs                  # 商城模块映射配置
```

---

## 6.2 数据库表设计

### 6.2.1 表结构总览

| 表名 | 说明 | 主键 | 索引 |
|------|------|------|------|
| `ShopProduct` | 商品表 | `Id` (bigint) | `CategoryId`, `IsOnSale`, `ProductType` |
| `ShopProductCategory` | 商品分类表 | `Id` (string) | `SortOrder`, `IsEnabled` |
| `ShopOrder` | 订单表 | `Id` (bigint) | `UserId`, `ProductId`, `Status`, `CreateTime` |
| `ShopUserInventory` | 用户背包表 | `Id` (bigint) | `UserId`, `Status`, `ItemType`, `ExpiresAt` |

### 6.2.2 索引设计

**商品表索引**：
```sql
-- 商品列表查询
CREATE INDEX idx_product_category_onsale ON ShopProduct(CategoryId, IsOnSale, SortOrder);
CREATE INDEX idx_product_type_onsale ON ShopProduct(ProductType, IsOnSale, SortOrder);

-- 商品搜索
CREATE INDEX idx_product_name ON ShopProduct(Name);

-- 库存查询
CREATE INDEX idx_product_stock ON ShopProduct(StockType, Stock) WHERE IsOnSale = true;
```

**订单表索引**：
```sql
-- 用户订单查询
CREATE INDEX idx_order_user_status ON ShopOrder(UserId, Status, CreateTime DESC);
CREATE INDEX idx_order_user_time ON ShopOrder(UserId, CreateTime DESC);

-- 订单号查询
CREATE UNIQUE INDEX idx_order_no ON ShopOrder(OrderNo);

-- 商品销售统计
CREATE INDEX idx_order_product_status ON ShopOrder(ProductId, Status, CreateTime);

-- 超时订单查询
CREATE INDEX idx_order_pending_time ON ShopOrder(Status, CreateTime) WHERE Status = 0;
```

**用户背包表索引**：
```sql
-- 用户背包查询
CREATE INDEX idx_inventory_user_status ON ShopUserInventory(UserId, Status, ItemType);

-- 权益检查
CREATE INDEX idx_inventory_user_benefit ON ShopUserInventory(UserId, BenefitType, Status, ExpiresAt);

-- 消耗品查询
CREATE INDEX idx_inventory_user_consumable ON ShopUserInventory(UserId, ConsumableType, Status);

-- 过期检查
CREATE INDEX idx_inventory_expires ON ShopUserInventory(Status, ExpiresAt) WHERE Status = 1;
CREATE INDEX idx_inventory_deadline ON ShopUserInventory(Status, UseDeadline) WHERE Status = 1;
```

---

## 6.3 Service 层设计

### 6.3.1 ProductService

```csharp
public interface IProductService
{
    // CRUD
    Task<long> CreateProductAsync(ProductCreateVo input);
    Task<bool> UpdateProductAsync(long productId, ProductUpdateVo input);
    Task<bool> DeleteProductAsync(long productId);
    Task<ProductDetailVo?> GetProductDetailAsync(long productId);
    Task<PageModel<ProductListVo>> GetProductListAsync(ProductQueryVo query);

    // 上下架
    Task<bool> SetProductOnSaleAsync(long productId, bool isOnSale);

    // 库存管理
    Task<bool> DeductStockAsync(long productId, int quantity);
    Task<bool> RollbackStockAsync(long productId, int quantity);
    Task CheckStockWarningAsync();

    // 分类管理
    Task<List<ProductCategoryVo>> GetCategoriesAsync();
    Task<bool> CreateCategoryAsync(ProductCategoryCreateVo input);

    // 种子数据
    Task SeedProductsAsync();
}
```

**关键方法实现**：

库存扣减使用乐观锁，详见 `shop-product.md` 的 3.3.1 节。

### 6.3.2 OrderService

```csharp
public interface IOrderService
{
    // 订单创建
    Task<OrderResultVo> CreateOrderAsync(long userId, OrderCreateVo input);
    Task<OrderResultVo> CreateOrderWithIdempotencyAsync(long userId, OrderCreateVo input, string idempotencyKey);

    // 订单查询
    Task<PageModel<OrderListVo>> GetUserOrdersAsync(long userId, OrderQueryVo query);
    Task<OrderDetailVo?> GetOrderDetailAsync(long userId, long orderId);

    // 订单管理
    Task<bool> CancelOrderAsync(long userId, long orderId, string? reason = null);
    Task CancelTimeoutOrdersAsync();
    Task<bool> RetryFailedOrderAsync(long orderId); // 管理员

    // 订单统计
    Task<UserOrderStatsVo> GetUserOrderStatsAsync(long userId);
    Task<ProductSalesStatsVo> GetProductSalesStatsAsync(long productId, DateTime? startTime = null, DateTime? endTime = null);
}
```

**关键特性**：
- 事务保证：使用 `[UseTran]` 特性确保订单创建的原子性
- 幂等性：使用缓存防止重复下单
- 限流：检查用户下单频率，防止刷单
- 超时处理：定时任务自动取消 30 分钟未支付的订单

### 6.3.3 InventoryService

```csharp
public interface IInventoryService
{
    // 权益发放
    Task<long> GrantAsync(Order order);
    Task<long> GrantBenefitAsync(long userId, BenefitType benefitType, string itemName, string? effectParams = null, int durationDays = 0, InventorySourceType sourceType = InventorySourceType.SystemGift);
    Task<long> GrantConsumableAsync(long userId, ConsumableType consumableType, string itemName, int quantity = 1, string? effectParams = null, int? useDeadlineDays = null, InventorySourceType sourceType = InventorySourceType.SystemGift);

    // 权益查询
    Task<UserInventoryVo> GetUserInventoryAsync(long userId);
    Task<bool> HasBenefitAsync(long userId, BenefitType benefitType);
    Task<BenefitItemVo?> GetActiveBenefitAsync(long userId, BenefitType benefitType);

    // 消耗品使用
    Task<UseConsumableResultVo> UseConsumableAsync(long userId, long inventoryId, UseConsumableVo input);

    // 装备系统
    Task<bool> EquipItemAsync(long userId, long inventoryId);
    Task<bool> UnequipItemAsync(long userId, long inventoryId);
    Task<UserEquipmentVo> GetUserEquipmentAsync(long userId);
}
```

**权益发放逻辑**：
- 权益类：检查是否有同类型权益，有则叠加时长，无则新建
- 消耗品：检查是否有相同参数的消耗品，有则堆叠数量，无则新建
- 支持多种来源：购买、系统赠送、活动奖励、管理员发放、升级奖励

### 6.3.4 PurchaseLimitChecker

```csharp
public class PurchaseLimitChecker
{
    public async Task<PurchaseCheckResult> CheckAsync(Product product, long userId, int quantity);
}

public class PurchaseCheckResult
{
    public bool CanPurchase { get; set; }
    public string Message { get; set; } = string.Empty;
}
```

**检查顺序**：
1. 商品是否上架
2. 用户等级是否满足
3. 是否需要 VIP
4. 库存是否充足
5. 总量限制
6. 每人限购
7. 每日限购

---

## 6.4 Controller 层设计

### 6.4.1 ShopController（用户端）

```csharp
/// <summary>
/// 商城控制器（用户端）
/// </summary>
[Route("api/shop")]
[Authorize(Policy = "Client")]
public class ShopController : ControllerBase
{
    /// <summary>
    /// 获取商城首页数据
    /// </summary>
    [HttpGet("home")]
    public async Task<MessageModel<ShopHomeVo>> GetShopHome()
    {
        var categories = await _productService.GetCategoriesAsync();
        var recommendedProducts = await _productService.GetProductListAsync(new ProductQueryVo
        {
            IsOnSale = true,
            IsRecommended = true,
            PageIndex = 1,
            PageSize = 10
        });

        return Success(new ShopHomeVo
        {
            Categories = categories,
            RecommendedProducts = recommendedProducts.Items
        });
    }

    /// <summary>
    /// 获取商品列表
    /// </summary>
    [HttpGet("products")]
    public async Task<MessageModel<PageModel<ProductListVo>>> GetProducts([FromQuery] ProductQueryVo query)
    {
        query.IsOnSale = true; // 用户端只能查看上架商品
        var result = await _productService.GetProductListAsync(query);
        return Success(result);
    }

    /// <summary>
    /// 获取商品详情
    /// </summary>
    [HttpGet("products/{productId}")]
    public async Task<MessageModel<ProductDetailVo>> GetProductDetail(long productId)
    {
        var product = await _productService.GetProductDetailAsync(productId);
        if (product == null || !product.IsOnSale)
            return Failed<ProductDetailVo>("商品不存在或已下架");

        return Success(product);
    }

    /// <summary>
    /// 购买商品
    /// </summary>
    [HttpPost("purchase")]
    public async Task<MessageModel<OrderResultVo>> Purchase([FromBody] OrderCreateVo input)
    {
        var userId = GetCurrentUserId();
        var result = await _orderService.CreateOrderAsync(userId, input);
        return Success(result);
    }
}
```

### 6.4.2 OrderController

```csharp
/// <summary>
/// 订单控制器
/// </summary>
[Route("api/orders")]
[Authorize(Policy = "Client")]
public class OrderController : ControllerBase
{
    /// <summary>
    /// 获取我的订单列表
    /// </summary>
    [HttpGet]
    public async Task<MessageModel<PageModel<OrderListVo>>> GetMyOrders([FromQuery] OrderQueryVo query)
    {
        var userId = GetCurrentUserId();
        var result = await _orderService.GetUserOrdersAsync(userId, query);
        return Success(result);
    }

    /// <summary>
    /// 获取订单详情
    /// </summary>
    [HttpGet("{orderId}")]
    public async Task<MessageModel<OrderDetailVo>> GetOrderDetail(long orderId)
    {
        var userId = GetCurrentUserId();
        var order = await _orderService.GetOrderDetailAsync(userId, orderId);
        if (order == null)
            return Failed<OrderDetailVo>("订单不存在");

        return Success(order);
    }

    /// <summary>
    /// 取消订单
    /// </summary>
    [HttpPost("{orderId}/cancel")]
    public async Task<MessageModel<bool>> CancelOrder(long orderId)
    {
        var userId = GetCurrentUserId();
        var result = await _orderService.CancelOrderAsync(userId, orderId);
        return Success(result);
    }

    /// <summary>
    /// 获取订单统计
    /// </summary>
    [HttpGet("stats")]
    public async Task<MessageModel<UserOrderStatsVo>> GetOrderStats()
    {
        var userId = GetCurrentUserId();
        var stats = await _orderService.GetUserOrderStatsAsync(userId);
        return Success(stats);
    }
}
```

### 6.4.3 InventoryController

```csharp
/// <summary>
/// 背包控制器
/// </summary>
[Route("api/inventory")]
[Authorize(Policy = "Client")]
public class InventoryController : ControllerBase
{
    /// <summary>
    /// 获取我的背包
    /// </summary>
    [HttpGet]
    public async Task<MessageModel<UserInventoryVo>> GetMyInventory()
    {
        var userId = GetCurrentUserId();
        var inventory = await _inventoryService.GetUserInventoryAsync(userId);
        return Success(inventory);
    }

    /// <summary>
    /// 使用消耗品
    /// </summary>
    [HttpPost("{inventoryId}/use")]
    public async Task<MessageModel<UseConsumableResultVo>> UseConsumable(
        long inventoryId,
        [FromBody] UseConsumableVo input)
    {
        var userId = GetCurrentUserId();
        var result = await _inventoryService.UseConsumableAsync(userId, inventoryId, input);
        return Success(result);
    }

    /// <summary>
    /// 装备物品
    /// </summary>
    [HttpPost("{inventoryId}/equip")]
    public async Task<MessageModel<bool>> EquipItem(long inventoryId)
    {
        var userId = GetCurrentUserId();
        var result = await _inventoryService.EquipItemAsync(userId, inventoryId);
        return Success(result);
    }

    /// <summary>
    /// 卸下物品
    /// </summary>
    [HttpPost("{inventoryId}/unequip")]
    public async Task<MessageModel<bool>> UnequipItem(long inventoryId)
    {
        var userId = GetCurrentUserId();
        var result = await _inventoryService.UnequipItemAsync(userId, inventoryId);
        return Success(result);
    }

    /// <summary>
    /// 获取装备信息
    /// </summary>
    [HttpGet("equipment")]
    public async Task<MessageModel<UserEquipmentVo>> GetEquipment()
    {
        var userId = GetCurrentUserId();
        var equipment = await _inventoryService.GetUserEquipmentAsync(userId);
        return Success(equipment);
    }

    /// <summary>
    /// 获取 VIP 状态
    /// </summary>
    [HttpGet("vip")]
    public async Task<MessageModel<VipStatusVo>> GetVipStatus()
    {
        var userId = GetCurrentUserId();
        var status = await _inventoryService.GetVipStatusAsync(userId);
        return Success(status);
    }
}
```

### 6.4.4 ProductController（管理员）

```csharp
/// <summary>
/// 商品管理控制器（管理员）
/// </summary>
[Route("api/admin/products")]
[Authorize(Policy = "SystemOrAdmin")]
public class ProductController : ControllerBase
{
    /// <summary>
    /// 创建商品
    /// </summary>
    [HttpPost]
    public async Task<MessageModel<long>> CreateProduct([FromBody] ProductCreateVo input)
    {
        var productId = await _productService.CreateProductAsync(input);
        return Success(productId);
    }

    /// <summary>
    /// 更新商品
    /// </summary>
    [HttpPut("{productId}")]
    public async Task<MessageModel<bool>> UpdateProduct(long productId, [FromBody] ProductUpdateVo input)
    {
        var result = await _productService.UpdateProductAsync(productId, input);
        return Success(result);
    }

    /// <summary>
    /// 删除商品
    /// </summary>
    [HttpDelete("{productId}")]
    public async Task<MessageModel<bool>> DeleteProduct(long productId)
    {
        var result = await _productService.DeleteProductAsync(productId);
        return Success(result);
    }

    /// <summary>
    /// 上下架商品
    /// </summary>
    [HttpPost("{productId}/onsale")]
    public async Task<MessageModel<bool>> SetProductOnSale(long productId, [FromBody] SetOnSaleVo input)
    {
        var result = await _productService.SetProductOnSaleAsync(productId, input.IsOnSale);
        return Success(result);
    }

    /// <summary>
    /// 获取商品销售统计
    /// </summary>
    [HttpGet("{productId}/stats")]
    public async Task<MessageModel<ProductSalesStatsVo>> GetProductStats(
        long productId,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null)
    {
        var stats = await _orderService.GetProductSalesStatsAsync(productId, startTime, endTime);
        return Success(stats);
    }

    /// <summary>
    /// 重试失败订单
    /// </summary>
    [HttpPost("/api/admin/orders/{orderId}/retry")]
    public async Task<MessageModel<bool>> RetryFailedOrder(long orderId)
    {
        var result = await _orderService.RetryFailedOrderAsync(orderId);
        return Success(result);
    }
}
```

---

## 6.5 AutoMapper 配置

### 6.5.1 ShopProfile

```csharp
public class ShopProfile : Profile
{
    public ShopProfile()
    {
        // Product
        CreateMap<Product, ProductListVo>()
            .ForMember(dest => dest.CategoryName,
                opt => opt.MapFrom((src, dest, _, context) =>
                    context.Items.ContainsKey("Categories")
                        ? ((List<ProductCategory>)context.Items["Categories"])
                            .FirstOrDefault(c => c.Id == src.CategoryId)?.Name
                        : null));

        CreateMap<Product, ProductDetailVo>();

        CreateMap<ProductCreateVo, Product>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreateTime, opt => opt.Ignore());

        // Order
        CreateMap<Order, OrderListVo>();
        CreateMap<Order, OrderDetailVo>();

        // Inventory
        CreateMap<UserInventory, BenefitItemVo>();
        CreateMap<UserInventory, ConsumableItemVo>();
        CreateMap<UserInventory, InventoryBriefVo>();
    }
}
```

---

## 6.6 集成服务

### 6.6.1 与萝卜币系统集成

```csharp
public interface ICoinService
{
    /// <summary>
    /// 获取用户余额
    /// </summary>
    Task<int> GetBalanceAsync(long userId);

    /// <summary>
    /// 扣减萝卜币
    /// </summary>
    Task<CoinOperationResult> DeductAsync(long userId, int amount, string reason, string businessType, string businessId);

    /// <summary>
    /// 退款
    /// </summary>
    Task<CoinOperationResult> RefundAsync(long userId, int amount, string reason, string businessType, string businessId);

    /// <summary>
    /// 发放萝卜币
    /// </summary>
    Task<CoinOperationResult> GrantAsync(long userId, int amount, string reason, string businessType, string businessId);
}
```

**业务类型定义**：
- `ShopOrder`: 商城购买
- `ShopOrderRefund`: 订单退款
- `CoinCard`: 使用萝卜币红包

### 6.6.2 与经验值系统集成

```csharp
public interface IExperienceService
{
    /// <summary>
    /// 获取用户等级
    /// </summary>
    Task<int> GetUserLevelAsync(long userId);

    /// <summary>
    /// 发放经验值
    /// </summary>
    Task GrantExperienceAsync(long userId, int amount, ExpType expType, string businessType, string businessId);

    /// <summary>
    /// 计算加成后的经验值
    /// </summary>
    Task<int> CalculateExpWithBoostAsync(long userId, int baseExp);
}
```

**集成点**：
- 限购检查：检查用户等级是否满足商品要求
- 经验加成：经验值获取加成计算
- 消耗品：经验卡、双倍经验卡

### 6.6.3 与通知系统集成

```csharp
public interface INotificationService
{
    /// <summary>
    /// 发送通知
    /// </summary>
    Task SendNotificationAsync(
        long userId,
        NotificationType type,
        string title,
        string content,
        string? relatedType = null,
        string? relatedId = null);
}
```

**通知场景**：
- 购买成功
- 订单异常
- 权益即将到期
- 库存预警（管理员）

---

## 6.7 定时任务

### 6.7.1 订单超时取消

```csharp
/// <summary>
/// 订单超时取消任务（每 5 分钟执行一次）
/// </summary>
public class OrderTimeoutJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await _orderService.CancelTimeoutOrdersAsync();
    }
}
```

**配置**：
```csharp
// Quartz 配置
services.AddQuartz(q =>
{
    q.AddJob<OrderTimeoutJob>(j => j.WithIdentity("OrderTimeoutJob"))
        .AddTrigger(t => t
            .ForJob("OrderTimeoutJob")
            .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));
});
```

### 6.7.2 权益过期检查

```csharp
/// <summary>
/// 权益过期检查任务（每小时执行一次）
/// </summary>
public class InventoryExpirationJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTime.Now;

        // 发送到期提醒（24 小时内）
        var expiringItems = await _inventoryRepository.QueryAsync(
            i => i.Status == InventoryStatus.Active &&
                 i.ExpiresAt != null &&
                 i.ExpiresAt > now &&
                 i.ExpiresAt <= now.AddHours(24)
        );

        foreach (var item in expiringItems)
        {
            await _notificationService.SendNotificationAsync(
                userId: item.UserId,
                type: NotificationType.System,
                title: "权益即将到期",
                content: $"您的「{item.ItemName}」将于 {item.ExpiresAt:MM-dd HH:mm} 到期",
                relatedType: "Inventory",
                relatedId: item.Id.ToString()
            );
        }

        // 处理已过期的权益
        var expiredItems = await _inventoryRepository.QueryAsync(
            i => i.Status == InventoryStatus.Active &&
                 ((i.ExpiresAt != null && i.ExpiresAt <= now) ||
                  (i.UseDeadline != null && i.UseDeadline <= now))
        );

        foreach (var item in expiredItems)
        {
            item.Status = InventoryStatus.Expired;
            item.UpdateTime = now;
            await _inventoryRepository.UpdateAsync(item);
        }
    }
}
```

### 6.7.3 库存预警

```csharp
/// <summary>
/// 库存预警任务（每天执行一次）
/// </summary>
public class StockWarningJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await _productService.CheckStockWarningAsync();
    }
}
```

---

## 6.8 异常处理

### 6.8.1 业务异常

```csharp
public class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
}
```

**使用场景**：
- 商品不存在
- 库存不足
- 余额不足
- 超出限购
- 权益发放失败

### 6.8.2 全局异常处理

```csharp
public class GlobalExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is BusinessException businessException)
        {
            context.Result = new JsonResult(new MessageModel<object>
            {
                Success = false,
                Msg = businessException.Message
            });
            context.ExceptionHandled = true;
        }
        else
        {
            // 记录日志
            Log.Error(context.Exception, "未处理的异常");

            context.Result = new JsonResult(new MessageModel<object>
            {
                Success = false,
                Msg = "系统错误，请稍后重试"
            });
            context.ExceptionHandled = true;
        }
    }
}
```

---

## 6.9 缓存策略

### 6.9.1 商品分类缓存

```csharp
public async Task<List<ProductCategoryVo>> GetCategoriesAsync()
{
    var cacheKey = "shop:categories";
    var cached = await _cache.GetAsync<List<ProductCategoryVo>>(cacheKey);

    if (cached != null)
        return cached;

    var categories = await _categoryRepository.QueryAsync(c => c.IsEnabled);
    var result = _mapper.Map<List<ProductCategoryVo>>(categories);

    await _cache.SetAsync(cacheKey, result, TimeSpan.FromHours(1));

    return result;
}
```

### 6.9.2 用户 VIP 状态缓存

```csharp
public async Task<VipStatusVo> GetVipStatusAsync(long userId)
{
    var cacheKey = $"shop:vip:{userId}";
    var cached = await _cache.GetAsync<VipStatusVo>(cacheKey);

    if (cached != null)
        return cached;

    var vipBenefit = await GetActiveBenefitAsync(userId, BenefitType.ExpBoost);

    var status = vipBenefit == null
        ? new VipStatusVo { IsVip = false, ExpBoostRate = 1.0m, CoinBoostRate = 1.0m }
        : MapVipStatus(vipBenefit);

    // 缓存 5 分钟
    await _cache.SetAsync(cacheKey, status, TimeSpan.FromMinutes(5));

    return status;
}
```

---

## 6.10 日志记录

### 6.10.1 关键操作日志

```csharp
// 订单创建
Log.Information("用户 {UserId} 创建订单 {OrderNo}，商品 {ProductId}，金额 {Amount}",
    userId, orderNo, productId, amount);

// 库存扣减
Log.Information("商品 {ProductId} 库存扣减成功，剩余 {Stock}", productId, stock);

// 权益发放
Log.Information("用户 {UserId} 获得权益 {BenefitType}，到期时间：{ExpiresAt}",
    userId, benefitType, expiresAt);

// 权益过期
Log.Information("用户 {UserId} 的 {ItemName} 已过期", userId, itemName);
```

### 6.10.2 异常日志

```csharp
// 库存扣减失败
Log.Warning("商品 {ProductId} 库存扣减冲突，重试 {Retry}/{MaxRetries}",
    productId, retry, maxRetries);

// 权益发放失败
Log.Error(ex, "订单 {OrderNo} 权益发放失败", orderNo);

// 订单重试
Log.Information("订单 {OrderNo} 重试成功", orderNo);
Log.Error(ex, "订单 {OrderNo} 重试失败", orderNo);
```

---

## 6.11 安全性考虑

### 6.11.1 授权验证

```csharp
// 用户端接口
[Authorize(Policy = "Client")]  // 需要登录

// 管理员接口
[Authorize(Policy = "SystemOrAdmin")]  // 需要 System 或 Admin 角色
```

### 6.11.2 数据权限

```csharp
// 确保用户只能操作自己的订单
var order = await _orderRepository.QueryFirstAsync(
    o => o.Id == orderId && o.UserId == userId
);

// 确保用户只能使用自己的背包物品
var item = await _inventoryRepository.QueryFirstAsync(
    i => i.Id == inventoryId && i.UserId == userId
);
```

### 6.11.3 参数验证

```csharp
[Required(ErrorMessage = "商品 ID 不能为空")]
public long ProductId { get; set; }

[Range(1, 100, ErrorMessage = "购买数量必须在 1-100 之间")]
public int Quantity { get; set; } = 1;

[Range(0, int.MaxValue, ErrorMessage = "价格不能为负数")]
public int Price { get; set; }
```

### 6.11.4 防刷机制

```csharp
// 限流：每分钟最多 10 单
private async Task CheckOrderRateLimitAsync(long userId)
{
    var cacheKey = $"order:rate:{userId}";
    var recentCount = await _cache.GetAsync<int>(cacheKey);

    if (recentCount >= 10)
    {
        throw new BusinessException("操作过于频繁，请稍后再试");
    }

    await _cache.SetAsync(cacheKey, recentCount + 1, TimeSpan.FromMinutes(1));
}
```

---

## 6.12 性能优化

### 6.12.1 查询优化

```csharp
// 使用 SelectVo 减少数据传输
var products = await _productRepository.Context
    .Queryable<Product>()
    .Where(p => p.IsOnSale && !p.IsDeleted)
    .OrderByDescending(p => p.SortOrder)
    .Select(p => new ProductListVo
    {
        Id = p.Id,
        Name = p.Name,
        Price = p.Price,
        ImageUrl = p.ImageUrl
    })
    .ToPageListAsync(pageIndex, pageSize);
```

### 6.12.2 批量操作

```csharp
// 批量更新过期状态
await _inventoryRepository.Context
    .Updateable<UserInventory>()
    .SetColumns(i => new UserInventory { Status = InventoryStatus.Expired })
    .Where(i => i.Status == InventoryStatus.Active && i.ExpiresAt <= DateTime.Now)
    .ExecuteCommandAsync();
```

### 6.12.3 缓存预热

```csharp
// 启动时预热分类缓存
public async Task PreloadCategoriesAsync()
{
    await _productService.GetCategoriesAsync();
}
```

---

> 下一篇：[7. 前端设计](/guide/shop-frontend)
