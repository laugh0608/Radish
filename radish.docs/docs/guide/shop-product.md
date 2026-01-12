# 3. 商品管理

> 入口页：[商城系统设计方案](/guide/shop-system)

## 3.1 商品数据模型

### 3.1.1 商品实体（Product）

```csharp
/// <summary>
/// 商品实体
/// </summary>
[SugarTable("shop_product")]
public class Product : RootEntityTKey<long>, ITenantEntity
{
    #region 基础信息

    /// <summary>
    /// 商品名称
    /// </summary>
    [SugarColumn(Length = 100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 商品副标题
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string? Subtitle { get; set; }

    /// <summary>
    /// 商品描述（支持 Markdown）
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>
    /// 商品主图 URL
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// 商品图片列表（JSON 数组）
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Images { get; set; }

    /// <summary>
    /// 商品分类 ID
    /// </summary>
    [SugarColumn(Length = 50)]
    public string CategoryId { get; set; } = "other";

    #endregion

    #region 商品类型

    /// <summary>
    /// 商品类型（权益/消耗品/实物）
    /// </summary>
    public ProductType ProductType { get; set; }

    /// <summary>
    /// 权益类型（仅权益类商品）
    /// </summary>
    public BenefitType? BenefitType { get; set; }

    /// <summary>
    /// 消耗品类型（仅消耗品商品）
    /// </summary>
    public ConsumableType? ConsumableType { get; set; }

    /// <summary>
    /// 权益/消耗品参数（JSON 格式）
    /// 例如：{"hours": 24} 或 {"exp": 100}
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? EffectParams { get; set; }

    #endregion

    #region 价格信息

    /// <summary>
    /// 原价（萝卜币）
    /// </summary>
    public int OriginalPrice { get; set; }

    /// <summary>
    /// 现价（萝卜币）
    /// </summary>
    public int Price { get; set; }

    /// <summary>
    /// 是否显示原价（用于促销展示）
    /// </summary>
    public bool ShowOriginalPrice { get; set; }

    #endregion

    #region 有效期

    /// <summary>
    /// 有效期类型
    /// </summary>
    public DurationType DurationType { get; set; }

    /// <summary>
    /// 有效期天数（DurationType = Days 时有效）
    /// </summary>
    public int? DurationDays { get; set; }

    /// <summary>
    /// 固定到期时间（DurationType = FixedDate 时有效）
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    #endregion

    #region 库存信息

    /// <summary>
    /// 库存类型
    /// </summary>
    public StockType StockType { get; set; }

    /// <summary>
    /// 库存数量（StockType = Limited 时有效）
    /// </summary>
    public int? Stock { get; set; }

    /// <summary>
    /// 已售数量
    /// </summary>
    public int SoldCount { get; set; }

    /// <summary>
    /// 库存版本号（乐观锁）
    /// </summary>
    public int StockVersion { get; set; }

    #endregion

    #region 限购规则

    /// <summary>
    /// 每人限购数量（0 = 不限）
    /// </summary>
    public int PerUserLimit { get; set; }

    /// <summary>
    /// 每人每日限购数量（0 = 不限）
    /// </summary>
    public int PerUserDailyLimit { get; set; }

    /// <summary>
    /// 总量限制（0 = 不限）
    /// </summary>
    public int TotalLimit { get; set; }

    /// <summary>
    /// 最低等级要求（0 = 不限）
    /// </summary>
    public int MinLevelRequired { get; set; }

    #endregion

    #region 状态与排序

    /// <summary>
    /// 是否上架
    /// </summary>
    public bool IsOnSale { get; set; }

    /// <summary>
    /// 是否推荐（首页展示）
    /// </summary>
    public bool IsRecommended { get; set; }

    /// <summary>
    /// 是否热销（热销标签）
    /// </summary>
    public bool IsHot { get; set; }

    /// <summary>
    /// 是否新品（新品标签）
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// 排序权重（越大越靠前）
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 上架时间
    /// </summary>
    public DateTime? OnSaleTime { get; set; }

    /// <summary>
    /// 下架时间（定时下架）
    /// </summary>
    public DateTime? OffSaleTime { get; set; }

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
    /// 创建人 ID
    /// </summary>
    public long? CreateBy { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdateTime { get; set; }

    /// <summary>
    /// 更新人 ID
    /// </summary>
    public long? UpdateBy { get; set; }

    /// <summary>
    /// 是否删除（软删除）
    /// </summary>
    public bool IsDeleted { get; set; }

    #endregion
}
```

### 3.1.2 商品分类实体（ProductCategory）

```csharp
/// <summary>
/// 商品分类实体
/// </summary>
[SugarTable("shop_product_category")]
public class ProductCategory : RootEntityTKey<string>
{
    /// <summary>
    /// 分类名称
    /// </summary>
    [SugarColumn(Length = 50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 分类图标（MDI 图标名或 URL）
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? Icon { get; set; }

    /// <summary>
    /// 分类描述
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>
    /// 排序权重
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdateTime { get; set; }
}
```

---

## 3.2 商品管理功能

### 3.2.1 商品 CRUD

**创建商品**：
```csharp
public async Task<long> CreateProductAsync(ProductCreateVo input)
{
    // 1. 验证分类存在
    // 2. 验证价格合理性
    // 3. 验证权益/消耗品参数
    // 4. 生成商品 ID（雪花 ID）
    // 5. 保存商品
    // 6. 记录操作日志
}
```

**更新商品**：
```csharp
public async Task<bool> UpdateProductAsync(long productId, ProductUpdateVo input)
{
    // 1. 验证商品存在
    // 2. 验证修改权限
    // 3. 如果修改了价格，记录价格变更历史
    // 4. 更新商品信息
    // 5. 记录操作日志
}
```

**删除商品**：
```csharp
public async Task<bool> DeleteProductAsync(long productId)
{
    // 1. 验证商品存在
    // 2. 检查是否有未完成订单
    // 3. 软删除商品
    // 4. 记录操作日志
}
```

### 3.2.2 商品上下架

```csharp
public async Task<bool> SetProductOnSaleAsync(long productId, bool isOnSale)
{
    var product = await _productRepository.QueryFirstAsync(p => p.Id == productId);
    if (product == null) return false;

    product.IsOnSale = isOnSale;
    product.OnSaleTime = isOnSale ? DateTime.Now : null;
    product.UpdateTime = DateTime.Now;

    await _productRepository.UpdateAsync(product);

    // 记录操作日志
    Log.Information("商品 {ProductId} {Action}", productId, isOnSale ? "上架" : "下架");

    return true;
}
```

### 3.2.3 商品查询

**商品列表查询**：
```csharp
public async Task<PageModel<ProductListVo>> GetProductListAsync(ProductQueryVo query)
{
    Expression<Func<Product, bool>> where = p => !p.IsDeleted;

    // 分类筛选
    if (!string.IsNullOrEmpty(query.CategoryId))
        where = where.And(p => p.CategoryId == query.CategoryId);

    // 商品类型筛选
    if (query.ProductType.HasValue)
        where = where.And(p => p.ProductType == query.ProductType.Value);

    // 上架状态筛选
    if (query.IsOnSale.HasValue)
        where = where.And(p => p.IsOnSale == query.IsOnSale.Value);

    // 关键词搜索
    if (!string.IsNullOrEmpty(query.Keyword))
        where = where.And(p => p.Name.Contains(query.Keyword) ||
                               p.Subtitle.Contains(query.Keyword));

    // 分页查询
    var (data, total) = await _productRepository.QueryPageAsync(
        whereExpression: where,
        pageIndex: query.PageIndex,
        pageSize: query.PageSize,
        orderByExpression: p => p.SortOrder,
        orderByType: OrderByType.Desc
    );

    return new PageModel<ProductListVo>
    {
        Items = _mapper.Map<List<ProductListVo>>(data),
        Total = total,
        PageIndex = query.PageIndex,
        PageSize = query.PageSize
    };
}
```

---

## 3.3 库存管理

### 3.3.1 库存扣减（乐观锁）

```csharp
public async Task<bool> DeductStockAsync(long productId, int quantity)
{
    const int maxRetries = 5;

    for (int i = 0; i < maxRetries; i++)
    {
        var product = await _productRepository.QueryFirstAsync(p => p.Id == productId);

        if (product == null)
            throw new BusinessException("商品不存在");

        // 无限库存，直接返回成功
        if (product.StockType == StockType.Unlimited)
        {
            product.SoldCount += quantity;
            await _productRepository.UpdateAsync(product);
            return true;
        }

        // 检查库存
        if (product.Stock < quantity)
            throw new BusinessException("库存不足");

        // 乐观锁更新
        var oldVersion = product.StockVersion;
        product.Stock -= quantity;
        product.SoldCount += quantity;
        product.StockVersion++;

        var affected = await _productRepository.Context.Updateable(product)
            .Where(p => p.Id == productId && p.StockVersion == oldVersion)
            .ExecuteCommandAsync();

        if (affected > 0)
        {
            Log.Information("商品 {ProductId} 库存扣减成功，剩余 {Stock}", productId, product.Stock);
            return true;
        }

        // 乐观锁冲突，重试
        Log.Warning("商品 {ProductId} 库存扣减冲突，重试 {Retry}/{MaxRetries}",
            productId, i + 1, maxRetries);

        await Task.Delay(50 * (i + 1)); // 指数退避
    }

    throw new BusinessException("库存扣减失败，请稍后重试");
}
```

### 3.3.2 库存回滚

```csharp
public async Task<bool> RollbackStockAsync(long productId, int quantity)
{
    var product = await _productRepository.QueryFirstAsync(p => p.Id == productId);

    if (product == null || product.StockType == StockType.Unlimited)
        return true;

    product.Stock += quantity;
    product.SoldCount -= quantity;
    product.StockVersion++;

    await _productRepository.UpdateAsync(product);

    Log.Information("商品 {ProductId} 库存回滚 {Quantity}，当前 {Stock}",
        productId, quantity, product.Stock);

    return true;
}
```

### 3.3.3 库存预警

```csharp
public async Task CheckStockWarningAsync()
{
    // 查询低库存商品（库存 < 10 且为限量商品）
    var lowStockProducts = await _productRepository.QueryAsync(
        p => p.StockType == StockType.Limited &&
             p.Stock < 10 &&
             p.IsOnSale &&
             !p.IsDeleted
    );

    foreach (var product in lowStockProducts)
    {
        // 发送库存预警通知给管理员
        await _notificationService.SendSystemNotificationAsync(
            userId: 0, // 系统管理员
            title: "库存预警",
            content: $"商品「{product.Name}」库存不足，当前库存：{product.Stock}",
            relatedType: "Product",
            relatedId: product.Id.ToString()
        );
    }
}
```

---

## 3.4 限购检查

### 3.4.1 限购检查服务

```csharp
public class PurchaseLimitChecker
{
    public async Task<PurchaseCheckResult> CheckAsync(
        Product product,
        long userId,
        int quantity)
    {
        var result = new PurchaseCheckResult { CanPurchase = true };

        // 1. 检查商品是否上架
        if (!product.IsOnSale)
        {
            result.CanPurchase = false;
            result.Message = "商品已下架";
            return result;
        }

        // 2. 检查用户等级
        if (product.MinLevelRequired > 0)
        {
            var userLevel = await _experienceService.GetUserLevelAsync(userId);
            if (userLevel < product.MinLevelRequired)
            {
                result.CanPurchase = false;
                result.Message = $"需要达到 Lv.{product.MinLevelRequired} 才能购买";
                return result;
            }
        }

        // 3. 检查库存
        if (product.StockType == StockType.Limited && product.Stock < quantity)
        {
            result.CanPurchase = false;
            result.Message = "库存不足";
            return result;
        }

        // 4. 检查总量限制
        if (product.TotalLimit > 0 && product.SoldCount + quantity > product.TotalLimit)
        {
            result.CanPurchase = false;
            result.Message = "已达到销售上限";
            return result;
        }

        // 5. 检查每人限购
        if (product.PerUserLimit > 0)
        {
            var userPurchased = await GetUserPurchasedCountAsync(product.Id, userId);
            if (userPurchased + quantity > product.PerUserLimit)
            {
                result.CanPurchase = false;
                result.Message = $"每人限购 {product.PerUserLimit} 件";
                return result;
            }
        }

        // 6. 检查每日限购
        if (product.PerUserDailyLimit > 0)
        {
            var todayPurchased = await GetUserTodayPurchasedCountAsync(product.Id, userId);
            if (todayPurchased + quantity > product.PerUserDailyLimit)
            {
                result.CanPurchase = false;
                result.Message = $"每日限购 {product.PerUserDailyLimit} 件";
                return result;
            }
        }

        return result;
    }
}
```

### 3.4.2 购买数量统计

```csharp
private async Task<int> GetUserPurchasedCountAsync(long productId, long userId)
{
    return (int)await _orderRepository.QueryCountAsync(
        o => o.ProductId == productId &&
             o.UserId == userId &&
             o.Status != OrderStatus.Cancelled &&
             o.Status != OrderStatus.Refunded
    );
}

private async Task<int> GetUserTodayPurchasedCountAsync(long productId, long userId)
{
    var today = DateTime.Today;
    var tomorrow = today.AddDays(1);

    return (int)await _orderRepository.QueryCountAsync(
        o => o.ProductId == productId &&
             o.UserId == userId &&
             o.CreateTime >= today &&
             o.CreateTime < tomorrow &&
             o.Status != OrderStatus.Cancelled &&
             o.Status != OrderStatus.Refunded
    );
}
```

---

## 3.5 商品种子数据

### 3.5.1 初始商品配置

```csharp
public async Task SeedProductsAsync()
{
    var products = new List<Product>
    {
        // 徽章装饰
        new Product
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            Name = "元老徽章",
            Subtitle = "社区元老专属标识",
            CategoryId = "badge",
            ProductType = ProductType.Benefit,
            BenefitType = BenefitType.Badge,
            EffectParams = "{\"badgeId\": \"veteran\", \"badgeName\": \"元老\"}",
            Price = 1000,
            DurationType = DurationType.Permanent,
            StockType = StockType.Limited,
            Stock = 100,
            TotalLimit = 100,
            IsOnSale = true,
            IsNew = true,
            SortOrder = 80
        },

        // 功能卡片
        new Product
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            Name = "改名卡",
            Subtitle = "修改昵称一次",
            CategoryId = "card",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.RenameCard,
            Price = 100,
            DurationType = DurationType.Days,
            DurationDays = 30, // 30 天内使用
            StockType = StockType.Unlimited,
            PerUserLimit = 5,
            IsOnSale = true,
            SortOrder = 70
        },
        new Product
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            Name = "帖子置顶卡（24小时）",
            Subtitle = "让你的帖子置顶 24 小时",
            CategoryId = "card",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.PostPinCard,
            EffectParams = "{\"hours\": 24}",
            Price = 200,
            StockType = StockType.Unlimited,
            PerUserDailyLimit = 3,
            IsOnSale = true,
            SortOrder = 69
        },

        // 加成道具
        new Product
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            Name = "经验卡（100点）",
            Subtitle = "立即获得 100 点经验值",
            CategoryId = "boost",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.ExpCard,
            EffectParams = "{\"exp\": 100}",
            Price = 50,
            StockType = StockType.Unlimited,
            PerUserDailyLimit = 10,
            IsOnSale = true,
            SortOrder = 60
        },
        new Product
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            Name = "双倍经验卡（1小时）",
            Subtitle = "1 小时内经验值获取翻倍",
            CategoryId = "boost",
            ProductType = ProductType.Consumable,
            ConsumableType = ConsumableType.DoubleExpCard,
            EffectParams = "{\"hours\": 1, \"multiplier\": 2}",
            Price = 100,
            StockType = StockType.Unlimited,
            PerUserDailyLimit = 3,
            IsOnSale = true,
            SortOrder = 59
        }
    };

    foreach (var product in products)
    {
        product.CreateTime = DateTime.Now;
        await _productRepository.AddAsync(product);
    }
}
```

---

> 下一篇：[4. 订单系统](/guide/shop-order)
