# 5. 权益系统

> 入口页：[商城系统设计方案](/guide/shop-system)

## 5.1 权益数据模型

### 5.1.1 用户权益/背包实体（UserInventory）

```csharp
/// <summary>
/// 用户权益/背包实体
/// </summary>
[SugarTable("shop_user_inventory")]
public class UserInventory : RootEntityTKey<long>, ITenantEntity
{
    #region 基础信息

    /// <summary>
    /// 用户 ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 物品类型（权益/消耗品）
    /// </summary>
    public InventoryItemType ItemType { get; set; }

    /// <summary>
    /// 权益类型（ItemType = Benefit 时有效）
    /// </summary>
    public BenefitType? BenefitType { get; set; }

    /// <summary>
    /// 消耗品类型（ItemType = Consumable 时有效）
    /// </summary>
    public ConsumableType? ConsumableType { get; set; }

    #endregion

    #region 物品信息

    /// <summary>
    /// 物品名称
    /// </summary>
    [SugarColumn(Length = 100)]
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// 物品图标
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ItemIcon { get; set; }

    /// <summary>
    /// 物品描述
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ItemDescription { get; set; }

    /// <summary>
    /// 效果参数（JSON 格式）
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? EffectParams { get; set; }

    #endregion

    #region 数量与状态

    /// <summary>
    /// 数量（消耗品可堆叠）
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// 物品状态
    /// </summary>
    public InventoryStatus Status { get; set; }

    /// <summary>
    /// 是否已装备/激活（徽章、头像框等）
    /// </summary>
    public bool IsEquipped { get; set; }

    #endregion

    #region 有效期

    /// <summary>
    /// 有效期类型
    /// </summary>
    public DurationType DurationType { get; set; }

    /// <summary>
    /// 生效时间
    /// </summary>
    public DateTime? EffectiveTime { get; set; }

    /// <summary>
    /// 到期时间
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 使用期限（消耗品需在此时间前使用）
    /// </summary>
    public DateTime? UseDeadline { get; set; }

    #endregion

    #region 来源信息

    /// <summary>
    /// 来源类型
    /// </summary>
    public InventorySourceType SourceType { get; set; }

    /// <summary>
    /// 来源订单 ID
    /// </summary>
    public long? SourceOrderId { get; set; }

    /// <summary>
    /// 来源商品 ID
    /// </summary>
    public long? SourceProductId { get; set; }

    /// <summary>
    /// 来源描述
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string? SourceDescription { get; set; }

    #endregion

    #region 使用记录

    /// <summary>
    /// 使用时间（消耗品）
    /// </summary>
    public DateTime? UsedTime { get; set; }

    /// <summary>
    /// 使用目标（如帖子 ID）
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? UsedTarget { get; set; }

    /// <summary>
    /// 使用备注
    /// </summary>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string? UsedRemark { get; set; }

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
    /// 是否删除
    /// </summary>
    public bool IsDeleted { get; set; }

    #endregion
}
```

### 5.1.2 相关枚举

```csharp
/// <summary>
/// 背包物品类型
/// </summary>
public enum InventoryItemType
{
    /// <summary>
    /// 权益类（自动生效）
    /// </summary>
    Benefit = 1,

    /// <summary>
    /// 消耗品（手动使用）
    /// </summary>
    Consumable = 2
}

/// <summary>
/// 背包物品状态
/// </summary>
public enum InventoryStatus
{
    /// <summary>
    /// 有效（权益生效中/消耗品可用）
    /// </summary>
    Active = 1,

    /// <summary>
    /// 已使用（消耗品）
    /// </summary>
    Used = 2,

    /// <summary>
    /// 已过期
    /// </summary>
    Expired = 3,

    /// <summary>
    /// 已失效（被替换或取消）
    /// </summary>
    Invalid = 4
}

/// <summary>
/// 来源类型
/// </summary>
public enum InventorySourceType
{
    /// <summary>
    /// 商城购买
    /// </summary>
    Purchase = 1,

    /// <summary>
    /// 系统赠送
    /// </summary>
    SystemGift = 2,

    /// <summary>
    /// 活动奖励
    /// </summary>
    ActivityReward = 3,

    /// <summary>
    /// 管理员发放
    /// </summary>
    AdminGrant = 4,

    /// <summary>
    /// 升级奖励
    /// </summary>
    LevelUpReward = 5
}
```

---

## 5.2 权益发放服务

### 5.2.1 权益发放入口

```csharp
public class InventoryService : IInventoryService
{
    /// <summary>
    /// 根据订单发放权益
    /// </summary>
    public async Task<long> GrantAsync(Order order)
    {
        return order.ProductType switch
        {
            ProductType.Benefit => await GrantBenefitAsync(order),
            ProductType.Consumable => await GrantConsumableAsync(order),
            _ => throw new BusinessException($"不支持的商品类型：{order.ProductType}")
        };
    }
}
```

### 5.2.2 发放权益类商品

```csharp
/// <summary>
/// 发放权益类商品
/// </summary>
private async Task<long> GrantBenefitAsync(Order order)
{
    if (!order.BenefitType.HasValue)
        throw new BusinessException("权益类型不能为空");

    var benefitType = order.BenefitType.Value;

    // 计算有效期
    DateTime? effectiveTime = DateTime.Now;
    DateTime? expiresAt = null;

    if (order.DurationType == DurationType.Days && order.DurationDays.HasValue)
    {
        // 检查是否有同类型的有效权益，叠加时长
        var existingBenefit = await _inventoryRepository.QueryFirstAsync(
            i => i.UserId == order.UserId &&
                 i.ItemType == InventoryItemType.Benefit &&
                 i.BenefitType == benefitType &&
                 i.Status == InventoryStatus.Active &&
                 (i.ExpiresAt == null || i.ExpiresAt > DateTime.Now)
        );

        if (existingBenefit != null && existingBenefit.ExpiresAt.HasValue)
        {
            // 叠加时长：从当前到期时间开始计算
            effectiveTime = existingBenefit.EffectiveTime;
            expiresAt = existingBenefit.ExpiresAt.Value.AddDays(order.DurationDays.Value * order.Quantity);

            // 更新现有记录
            existingBenefit.ExpiresAt = expiresAt;
            existingBenefit.UpdateTime = DateTime.Now;
            await _inventoryRepository.UpdateAsync(existingBenefit);

            // 更新订单的权益时间
            order.BenefitStartTime = effectiveTime;
            order.BenefitEndTime = expiresAt;

            Log.Information("用户 {UserId} 权益 {BenefitType} 时长叠加，新到期时间：{ExpiresAt}",
                order.UserId, benefitType, expiresAt);

            return existingBenefit.Id;
        }
        else
        {
            // 新权益
            expiresAt = DateTime.Now.AddDays(order.DurationDays.Value * order.Quantity);
        }
    }
    else if (order.DurationType == DurationType.FixedDate)
    {
        // 固定到期时间（从商品配置获取）
        var product = await _productRepository.QueryFirstAsync(p => p.Id == order.ProductId);
        expiresAt = product?.ExpiresAt;
    }
    // DurationType.Permanent 则 expiresAt 为 null（永久）

    // 创建新的权益记录
    var inventory = new UserInventory
    {
        Id = SnowFlakeSingle.Instance.NextId(),
        UserId = order.UserId,
        ItemType = InventoryItemType.Benefit,
        BenefitType = benefitType,
        ItemName = order.ProductName,
        ItemIcon = order.ProductImage,
        EffectParams = order.EffectParams,
        Quantity = 1,
        Status = InventoryStatus.Active,
        DurationType = order.DurationType,
        EffectiveTime = effectiveTime,
        ExpiresAt = expiresAt,
        SourceType = InventorySourceType.Purchase,
        SourceOrderId = order.Id,
        SourceProductId = order.ProductId,
        CreateTime = DateTime.Now
    };

    await _inventoryRepository.AddAsync(inventory);

    // 更新订单的权益时间
    order.BenefitStartTime = effectiveTime;
    order.BenefitEndTime = expiresAt;

    Log.Information("用户 {UserId} 获得权益 {BenefitType}，到期时间：{ExpiresAt}",
        order.UserId, benefitType, expiresAt?.ToString("yyyy-MM-dd HH:mm") ?? "永久");

    return inventory.Id;
}
```

### 5.2.3 发放消耗品商品

```csharp
/// <summary>
/// 发放消耗品商品
/// </summary>
private async Task<long> GrantConsumableAsync(Order order)
{
    if (!order.ConsumableType.HasValue)
        throw new BusinessException("消耗品类型不能为空");

    var consumableType = order.ConsumableType.Value;

    // 计算使用期限
    DateTime? useDeadline = null;
    if (order.DurationType == DurationType.Days && order.DurationDays.HasValue)
    {
        useDeadline = DateTime.Now.AddDays(order.DurationDays.Value);
    }

    // 检查是否有同类型的消耗品可以堆叠
    var existingItem = await _inventoryRepository.QueryFirstAsync(
        i => i.UserId == order.UserId &&
             i.ItemType == InventoryItemType.Consumable &&
             i.ConsumableType == consumableType &&
             i.Status == InventoryStatus.Active &&
             i.EffectParams == order.EffectParams && // 参数相同才能堆叠
             (i.UseDeadline == null || i.UseDeadline > DateTime.Now)
    );

    if (existingItem != null)
    {
        // 堆叠数量
        existingItem.Quantity += order.Quantity;
        existingItem.UpdateTime = DateTime.Now;
        await _inventoryRepository.UpdateAsync(existingItem);

        Log.Information("用户 {UserId} 消耗品 {ConsumableType} 堆叠，当前数量：{Quantity}",
            order.UserId, consumableType, existingItem.Quantity);

        return existingItem.Id;
    }

    // 创建新的消耗品记录
    var inventory = new UserInventory
    {
        Id = SnowFlakeSingle.Instance.NextId(),
        UserId = order.UserId,
        ItemType = InventoryItemType.Consumable,
        ConsumableType = consumableType,
        ItemName = order.ProductName,
        ItemIcon = order.ProductImage,
        EffectParams = order.EffectParams,
        Quantity = order.Quantity,
        Status = InventoryStatus.Active,
        DurationType = order.DurationType,
        UseDeadline = useDeadline,
        SourceType = InventorySourceType.Purchase,
        SourceOrderId = order.Id,
        SourceProductId = order.ProductId,
        CreateTime = DateTime.Now
    };

    await _inventoryRepository.AddAsync(inventory);

    Log.Information("用户 {UserId} 获得消耗品 {ConsumableType} x{Quantity}",
        order.UserId, consumableType, order.Quantity);

    return inventory.Id;
}
```

---

## 5.3 权益查询

### 5.3.1 获取用户背包

```csharp
public async Task<UserInventoryVo> GetUserInventoryAsync(long userId)
{
    var items = await _inventoryRepository.QueryAsync(
        i => i.UserId == userId &&
             i.Status == InventoryStatus.Active &&
             !i.IsDeleted
    );

    // 检查并更新过期状态
    var now = DateTime.Now;
    var expiredItems = items.Where(i =>
        (i.ExpiresAt.HasValue && i.ExpiresAt < now) ||
        (i.UseDeadline.HasValue && i.UseDeadline < now)
    ).ToList();

    foreach (var item in expiredItems)
    {
        item.Status = InventoryStatus.Expired;
        item.UpdateTime = now;
        await _inventoryRepository.UpdateAsync(item);
    }

    // 过滤掉刚过期的
    items = items.Except(expiredItems).ToList();

    return new UserInventoryVo
    {
        Benefits = items
            .Where(i => i.ItemType == InventoryItemType.Benefit)
            .Select(i => _mapper.Map<BenefitItemVo>(i))
            .ToList(),
        Consumables = items
            .Where(i => i.ItemType == InventoryItemType.Consumable)
            .Select(i => _mapper.Map<ConsumableItemVo>(i))
            .ToList()
    };
}
```

### 5.3.2 检查用户是否拥有权益

```csharp
public async Task<bool> HasBenefitAsync(long userId, BenefitType benefitType)
{
    var benefit = await _inventoryRepository.QueryFirstAsync(
        i => i.UserId == userId &&
             i.ItemType == InventoryItemType.Benefit &&
             i.BenefitType == benefitType &&
             i.Status == InventoryStatus.Active &&
             (i.ExpiresAt == null || i.ExpiresAt > DateTime.Now)
    );

    return benefit != null;
}

public async Task<BenefitItemVo?> GetActiveBenefitAsync(long userId, BenefitType benefitType)
{
    var benefit = await _inventoryRepository.QueryFirstAsync(
        i => i.UserId == userId &&
             i.ItemType == InventoryItemType.Benefit &&
             i.BenefitType == benefitType &&
             i.Status == InventoryStatus.Active &&
             (i.ExpiresAt == null || i.ExpiresAt > DateTime.Now)
    );

    return benefit == null ? null : _mapper.Map<BenefitItemVo>(benefit);
}
```

---

## 5.4 消耗品使用

### 5.4.1 使用消耗品

```csharp
public async Task<UseConsumableResultVo> UseConsumableAsync(
    long userId,
    long inventoryId,
    UseConsumableVo input)
{
    var item = await _inventoryRepository.QueryFirstAsync(
        i => i.Id == inventoryId &&
             i.UserId == userId &&
             i.ItemType == InventoryItemType.Consumable &&
             i.Status == InventoryStatus.Active
    );

    if (item == null)
        throw new BusinessException("物品不存在或已使用");

    if (item.UseDeadline.HasValue && item.UseDeadline < DateTime.Now)
    {
        item.Status = InventoryStatus.Expired;
        await _inventoryRepository.UpdateAsync(item);
        throw new BusinessException("物品已过期");
    }

    if (item.Quantity < 1)
        throw new BusinessException("物品数量不足");

    // 根据消耗品类型执行不同的效果
    var result = await ExecuteConsumableEffectAsync(userId, item, input);

    // 扣减数量
    item.Quantity--;
    item.UpdateTime = DateTime.Now;

    if (item.Quantity <= 0)
    {
        item.Status = InventoryStatus.Used;
        item.UsedTime = DateTime.Now;
        item.UsedTarget = input.TargetId;
        item.UsedRemark = input.Remark;
    }

    await _inventoryRepository.UpdateAsync(item);

    Log.Information("用户 {UserId} 使用消耗品 {ConsumableType}，剩余 {Quantity}",
        userId, item.ConsumableType, item.Quantity);

    return result;
}
```

### 5.4.2 执行消耗品效果

```csharp
private async Task<UseConsumableResultVo> ExecuteConsumableEffectAsync(
    long userId,
    UserInventory item,
    UseConsumableVo input)
{
    var result = new UseConsumableResultVo { Success = true };

    switch (item.ConsumableType)
    {
        case ConsumableType.RenameCard:
            // 改名卡：修改用户昵称
            if (string.IsNullOrEmpty(input.NewName))
                throw new BusinessException("请输入新昵称");

            await _userService.UpdateNicknameAsync(userId, input.NewName);
            result.Message = $"昵称已修改为「{input.NewName}」";
            break;

        case ConsumableType.PostPinCard:
            // 帖子置顶卡
            if (!input.TargetId.HasValue)
                throw new BusinessException("请选择要置顶的帖子");

            var pinParams = ParseEffectParams<PostPinParams>(item.EffectParams);
            await _postService.PinPostAsync(input.TargetId.Value, userId, pinParams.Hours);
            result.Message = $"帖子已置顶 {pinParams.Hours} 小时";
            break;

        case ConsumableType.PostHighlightCard:
            // 帖子高亮卡
            if (!input.TargetId.HasValue)
                throw new BusinessException("请选择要高亮的帖子");

            var highlightParams = ParseEffectParams<PostHighlightParams>(item.EffectParams);
            await _postService.HighlightPostAsync(input.TargetId.Value, userId, highlightParams.Hours);
            result.Message = $"帖子已高亮 {highlightParams.Hours} 小时";
            break;

        case ConsumableType.ExpCard:
            // 经验卡：立即获得经验值
            var expParams = ParseEffectParams<ExpCardParams>(item.EffectParams);
            await _experienceService.GrantExperienceAsync(
                userId: userId,
                amount: expParams.Exp,
                expType: ExpType.ITEM_USE,
                businessType: "ExpCard",
                businessId: item.Id.ToString()
            );
            result.Message = $"获得 {expParams.Exp} 点经验值";
            break;

        case ConsumableType.CoinCard:
            // 萝卜币红包
            var coinParams = ParseEffectParams<CoinCardParams>(item.EffectParams);
            await _coinService.GrantAsync(
                userId: userId,
                amount: coinParams.Coins,
                reason: "使用萝卜币红包",
                businessType: "CoinCard",
                businessId: item.Id.ToString()
            );
            result.Message = $"获得 {coinParams.Coins} 萝卜币";
            break;

        case ConsumableType.DoubleExpCard:
            // 双倍经验卡：创建临时权益
            var doubleExpParams = ParseEffectParams<DoubleExpParams>(item.EffectParams);
            await GrantTemporaryBenefitAsync(
                userId: userId,
                benefitType: BenefitType.ExpBoost,
                effectParams: $"{{\"multiplier\": {doubleExpParams.Multiplier}}}",
                hours: doubleExpParams.Hours,
                sourceDescription: "使用双倍经验卡"
            );
            result.Message = $"双倍经验已激活，持续 {doubleExpParams.Hours} 小时";
            break;

        default:
            throw new BusinessException($"不支持的消耗品类型：{item.ConsumableType}");
    }

    return result;
}
```

---

## 5.5 权益过期处理

### 5.5.1 过期检查定时任务

```csharp
public class InventoryExpirationJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTime.Now;

        // 查询即将过期的权益（24 小时内）
        var expiringItems = await _inventoryRepository.QueryAsync(
            i => i.Status == InventoryStatus.Active &&
                 i.ExpiresAt != null &&
                 i.ExpiresAt > now &&
                 i.ExpiresAt <= now.AddHours(24)
        );

        // 发送到期提醒
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

            Log.Information("用户 {UserId} 的 {ItemName} 已过期", item.UserId, item.ItemName);
        }
    }
}
```

---

## 5.6 装备系统

### 5.6.1 装备/卸下物品

```csharp
/// <summary>
/// 装备物品（徽章、头像框、称号等）
/// </summary>
public async Task<bool> EquipItemAsync(long userId, long inventoryId)
{
    var item = await _inventoryRepository.QueryFirstAsync(
        i => i.Id == inventoryId &&
             i.UserId == userId &&
             i.Status == InventoryStatus.Active
    );

    if (item == null)
        throw new BusinessException("物品不存在");

    // 只有特定类型的权益可以装备
    var equippableTypes = new[] {
        BenefitType.Badge,
        BenefitType.AvatarFrame,
        BenefitType.Title,
        BenefitType.Theme
    };

    if (item.ItemType != InventoryItemType.Benefit ||
        !item.BenefitType.HasValue ||
        !equippableTypes.Contains(item.BenefitType.Value))
    {
        throw new BusinessException("该物品不可装备");
    }

    // 卸下同类型的其他装备
    var sameTypeItems = await _inventoryRepository.QueryAsync(
        i => i.UserId == userId &&
             i.BenefitType == item.BenefitType &&
             i.IsEquipped &&
             i.Id != inventoryId
    );

    foreach (var sameItem in sameTypeItems)
    {
        sameItem.IsEquipped = false;
        sameItem.UpdateTime = DateTime.Now;
        await _inventoryRepository.UpdateAsync(sameItem);
    }

    // 装备当前物品
    item.IsEquipped = true;
    item.UpdateTime = DateTime.Now;
    await _inventoryRepository.UpdateAsync(item);

    Log.Information("用户 {UserId} 装备了 {ItemName}", userId, item.ItemName);

    return true;
}

/// <summary>
/// 卸下物品
/// </summary>
public async Task<bool> UnequipItemAsync(long userId, long inventoryId)
{
    var item = await _inventoryRepository.QueryFirstAsync(
        i => i.Id == inventoryId &&
             i.UserId == userId &&
             i.IsEquipped
    );

    if (item == null)
        throw new BusinessException("物品未装备");

    item.IsEquipped = false;
    item.UpdateTime = DateTime.Now;
    await _inventoryRepository.UpdateAsync(item);

    Log.Information("用户 {UserId} 卸下了 {ItemName}", userId, item.ItemName);

    return true;
}
```

### 5.6.2 获取用户装备信息

```csharp
public async Task<UserEquipmentVo> GetUserEquipmentAsync(long userId)
{
    var equippedItems = await _inventoryRepository.QueryAsync(
        i => i.UserId == userId &&
             i.IsEquipped &&
             i.Status == InventoryStatus.Active
    );

    return new UserEquipmentVo
    {
        Badge = equippedItems.FirstOrDefault(i => i.BenefitType == BenefitType.Badge),
        AvatarFrame = equippedItems.FirstOrDefault(i => i.BenefitType == BenefitType.AvatarFrame),
        Title = equippedItems.FirstOrDefault(i => i.BenefitType == BenefitType.Title),
        Theme = equippedItems.FirstOrDefault(i => i.BenefitType == BenefitType.Theme)
    };
}
```

---

## 5.7 加成系统集成

### 5.7.1 经验加成计算

```csharp
// 在 ExperienceService 中集成
public async Task<int> CalculateExpWithBoostAsync(long userId, int baseExp)
{
    // 检查是否有经验加成权益
    var expBoost = await _inventoryService.GetActiveBenefitAsync(userId, BenefitType.ExpBoost);
    if (expBoost == null)
    {
        return baseExp;
    }

    var boostParams = ParseEffectParams<ExpBoostParams>(expBoost.EffectParams);
    var boostedExp = (int)(baseExp * boostParams.Multiplier);

    return boostedExp;
}
```

### 5.7.2 萝卜币加成计算

```csharp
// 在 CoinService 中集成
public async Task<int> CalculateCoinWithBoostAsync(long userId, int baseCoins)
{
    // 检查是否有萝卜币加成权益
    var coinBoost = await _inventoryService.GetActiveBenefitAsync(userId, BenefitType.CoinBoost);
    if (coinBoost == null)
    {
        return baseCoins;
    }

    var boostParams = ParseEffectParams<CoinBoostParams>(coinBoost.EffectParams);
    var boostedCoins = (int)(baseCoins * boostParams.Multiplier);

    return boostedCoins;
}
```

### 5.7.3 加成权益叠加规则

**多个加成同时生效**：
- 加成效果取最高值，不累加
- 按到期时间叠加时长
- 示例：已有 1.5 倍经验加成（还剩 10 小时），再购买 1.2 倍经验加成（24 小时）
  - 前 10 小时：使用 1.5 倍
  - 后 24 小时：使用 1.2 倍

---

> 下一篇：[6. 后端设计](/guide/shop-backend)

