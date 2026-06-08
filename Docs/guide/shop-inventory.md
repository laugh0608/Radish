# 5. 权益系统

> 入口页：[商城系统设计方案](/guide/shop-system)

## 5.1 权益数据模型

### 5.1.1 当前实现口径（UserBenefit / UserInventory）

当前商城权益与背包已经拆成两个运行时概念：

| 模型 | 职责 | 来源字段口径 |
|------|------|--------------|
| `UserBenefit` | 自动生效或持续生效的权益记录，例如会员、徽章、头像框等 | `SourceOrderId`、`SourceProductId` |
| `UserInventory` | 可持有 / 可使用的消耗品背包记录，例如改名卡、经验卡等 | `SourceProductId` |
| `UserBenefitVo` | 前端“我的背包”权益卡片使用的展示契约 | `VoSourceOrderId`、`VoSourceProductId` |
| `UserInventoryVo` | 前端“我的背包”消耗品卡片使用的展示契约 | `VoSourceProductId`，不承载统一订单来源 |

WebOS 商城当前在权益卡片上展示购买来源回访。当 `UserBenefitVo.VoSourceOrderId` 或 `UserBenefitVo.VoSourceProductId` 有效时，前端分别展示“查看订单”和“查看商品”动作，并打开商城订单详情 / 商品详情。

消耗品卡片只展示商品维度的相关回访。当 `UserInventoryVo.VoSourceProductId` 有效时，前端展示“相关商品”动作并打开商城商品详情；它不表示消耗品具备订单级来源追溯。

不要把 `UserBenefitVo` 的订单来源字段误迁移为 `UserInventory` 的统一订单来源契约；消耗品订单级追溯应单独评估契约与前端展示。

来源字段虽然在后端实体中仍是 long 主键，但前端和移动端只按规范字符串 LongId 处理：

- `VoSourceOrderId / VoSourceProductId` 进入 WebOS、Flutter 或 Console 回访入口时不得通过 `Number(...)`、`parseInt(...)`、`int.tryParse` 等路径数值化。
- 来源 ID 应先按正整数 LongId 字符串做格式校验；不规范来源应禁用来源入口或展示明确错误，不应被数值化后误打开订单 / 商品。
- 消耗品只承接 `VoSourceProductId` 的相关商品回访，不因前端需要返回状态而扩展出统一订单来源字段。

### 5.1.2 背包物品模型补充

> **2026-03 对齐说明**：背包道具图标同样已经收口为“附件快照 Id + 运行时 URL 派生”。实体层以 `ItemIconAttachmentId` 为真值，列表 / 详情展示时再解析 `ItemIcon`。

`UserInventory` 当前用于消耗品持有、数量、状态、使用目标、使用备注、有效期和商品来源记录。它不再作为权益与消耗品的统一大模型说明；权益来源、有效期和装备态以 `UserBenefit` / `UserBenefitVo` 为准。

### 5.1.3 相关枚举

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

权益类商品由 `UserBenefitService.GrantBenefitAsync` 发放：

- 先校验商品权益类型、有效期和数量配置。
- 同类有效权益按当前规则叠加或刷新有效期。
- 新权益写入 `UserBenefit`，并记录 `SourceOrderId`、`SourceProductId` 与商品图标附件快照。
- 订单记录保存发放后的 `UserBenefitId`，供订单详情与治理链路追溯。
- 前端通过 `UserBenefitVo.VoSourceOrderId` / `VoSourceProductId` 展示来源回访入口。

### 5.2.3 发放消耗品商品

消耗品仍写入 `UserInventory`：

- 同类型、同参数且仍有效的消耗品可以堆叠数量。
- 新消耗品记录持有名称、图标附件快照、效果参数、数量、使用期限和商品来源。
- 前端可通过 `UserInventoryVo.VoSourceProductId` 展示“相关商品”回访入口。
- 当前 `UserInventory` 不承载统一订单来源；如果后续需要消耗品订单级追溯，应单独扩展契约与前端展示。

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
