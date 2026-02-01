# 排行榜系统

## 功能概述

排行榜系统提供多维度的用户和商品排名功能，支持 8 种不同类型的排行榜，涵盖用户等级、财富、活跃度和商品热度等多个维度。系统采用统一的数据结构设计，支持分页查询、实时排名和个人排名查询。

## 支持的排行榜类型

### 用户类排行榜

#### 1. 等级排行榜 (Experience)
- **排序依据**: 用户总经验值
- **主要数值**: 总经验值
- **次要数值**: 当前等级
- **图标**: `mdi:trophy`
- **用途**: 展示用户等级成长情况

#### 2. 萝卜余额榜 (Balance)
- **排序依据**: 用户当前萝卜币余额
- **主要数值**: 萝卜余额
- **次要数值**: 无
- **图标**: `mdi:currency-usd`
- **用途**: 展示用户财富积累情况

#### 3. 萝卜花销榜 (TotalSpent)
- **排序依据**: 用户累计消费萝卜币
- **主要数值**: 累计花销
- **次要数值**: 购买数量
- **图标**: `mdi:cart`
- **用途**: 展示用户消费活跃度

#### 4. 购买达人榜 (PurchaseCount)
- **排序依据**: 用户购买商品总数量
- **主要数值**: 购买数量
- **次要数值**: 累计花销
- **图标**: `mdi:shopping`
- **用途**: 展示用户购买频率

#### 5. 发帖达人榜 (PostCount)
- **排序依据**: 用户发帖数量
- **主要数值**: 帖子数
- **次要数值**: 无
- **图标**: `mdi:post`
- **用途**: 展示用户内容贡献度

#### 6. 评论达人榜 (CommentCount)
- **排序依据**: 用户评论数量
- **主要数值**: 评论数
- **次要数值**: 无
- **图标**: `mdi:comment`
- **用途**: 展示用户互动活跃度

#### 7. 人气排行榜 (Popularity)
- **排序依据**: 用户获得的总点赞数（帖子 + 评论）
- **主要数值**: 总点赞数
- **次要数值**: 无
- **图标**: `mdi:heart`
- **用途**: 展示用户内容受欢迎程度

### 商品类排行榜

#### 8. 热门商品榜 (HotProduct)
- **排序依据**: 商品销量
- **主要数值**: 销量
- **次要数值**: 商品价格
- **图标**: `mdi:fire`
- **用途**: 展示商品热度

## 技术架构

### 后端实现

#### 控制器层 (LeaderboardController)
```csharp
// 文件位置: Radish.Api/Controllers/v1/LeaderboardController.cs

/// <summary>
/// 获取排行榜数据
/// </summary>
[HttpGet]
[AllowAnonymous]
public async Task<MessageModel<PageModel<UnifiedLeaderboardItemVo>>> GetLeaderboard(
    [FromQuery] LeaderboardType type = LeaderboardType.Experience,
    [FromQuery] int pageIndex = 1,
    [FromQuery] int pageSize = 50)
```

**API 端点**:
- `GET /api/v1/Leaderboard/GetLeaderboard` - 获取排行榜数据
- `GET /api/v1/Leaderboard/GetMyRank` - 获取当前用户排名
- `GET /api/v1/Leaderboard/GetTypes` - 获取所有排行榜类型

#### 服务层 (LeaderboardService)
```csharp
// 文件位置: Radish.Service/LeaderboardService.cs (688 行)

public class LeaderboardService : ILeaderboardService
{
    // 核心方法
    Task<PageModel<UnifiedLeaderboardItemVo>> GetLeaderboardAsync(
        LeaderboardType type,
        int pageIndex,
        int pageSize,
        long? currentUserId);

    Task<int> GetUserRankAsync(LeaderboardType type, long userId);

    Task<List<LeaderboardTypeVo>> GetLeaderboardTypesAsync();
}
```

**设计特点**:
- 使用静态配置字典存储排行榜类型元数据
- 根据排行榜类型动态调用不同的数据查询方法
- 统一的数据结构转换逻辑

#### 仓储层 (LeaderboardRepository)
```csharp
// 文件位置: Radish.Repository/LeaderboardRepository.cs (288 行)

public class LeaderboardRepository : ILeaderboardRepository
{
    // 聚合查询方法
    Task<List<UserLeaderboardDto>> GetExperienceLeaderboardAsync(int pageIndex, int pageSize);
    Task<List<UserLeaderboardDto>> GetBalanceLeaderboardAsync(int pageIndex, int pageSize);
    Task<List<UserLeaderboardDto>> GetTotalSpentLeaderboardAsync(int pageIndex, int pageSize);
    Task<List<ProductLeaderboardDto>> GetHotProductLeaderboardAsync(int pageIndex, int pageSize);
    // ... 其他排行榜查询方法
}
```

**性能优化**:
- 数据库级聚合查询，避免内存过滤
- 使用 SqlSugar 的 `Mapper` 进行多表联查
- 分页查询减少数据传输量

### 数据模型

#### 统一排行榜项 ViewModel
```csharp
// 文件位置: Radish.Model/ViewModels/UnifiedLeaderboardItemVo.cs

public class UnifiedLeaderboardItemVo
{
    // 通用字段
    public LeaderboardType VoLeaderboardType { get; set; }
    public LeaderboardCategory VoCategory { get; set; }
    public int VoRank { get; set; }

    // 用户信息（用户类排行榜）
    public long? VoUserId { get; set; }
    public string? VoUserName { get; set; }
    public string? VoAvatarUrl { get; set; }
    public int? VoCurrentLevel { get; set; }
    public string? VoCurrentLevelName { get; set; }
    public string? VoThemeColor { get; set; }
    public bool VoIsCurrentUser { get; set; }

    // 商品信息（商品类排行榜）
    public long? VoProductId { get; set; }
    public string? VoProductName { get; set; }
    public string? VoProductIcon { get; set; }
    public decimal? VoProductPrice { get; set; }

    // 统计数值
    public long VoPrimaryValue { get; set; }
    public string VoPrimaryLabel { get; set; } = string.Empty;
    public long? VoSecondaryValue { get; set; }
    public string? VoSecondaryLabel { get; set; }
}
```

**设计优势**:
- 统一的数据结构支持用户和商品两种实体类型
- 可选字段设计，根据排行榜类型填充不同字段
- 前端可根据 `VoCategory` 字段选择不同的渲染组件

### 前端实现

#### API 客户端
```typescript
// 文件位置: radish.client/src/api/leaderboard.ts

export const leaderboardApi = {
  // 获取排行榜数据
  async getLeaderboard(
    type: LeaderboardType = LeaderboardType.Experience,
    pageIndex: number = 1,
    pageSize: number = 50
  ): Promise<PagedResponse<UnifiedLeaderboardItemData> | null>,

  // 获取当前用户排名
  async getMyRank(type: LeaderboardType): Promise<number | null>,

  // 获取所有排行榜类型
  async getTypes(): Promise<LeaderboardTypeData[] | null>
};
```

#### 主应用组件
```typescript
// 文件位置: radish.client/src/apps/LeaderboardApp.tsx

export const LeaderboardApp: React.FC = () => {
  // 使用 Tab 切换不同排行榜类型
  // 根据 category 渲染不同的排行榜项组件
  // 支持分页加载
  // 高亮显示当前用户
};
```

#### 排行榜项组件
```typescript
// 用户排行榜项
// 文件位置: radish.client/src/apps/leaderboard/UserLeaderboardItem.tsx
export const UserLeaderboardItem: React.FC<Props> = ({ item, rank }) => {
  // 显示用户头像、昵称、等级、主要数值、次要数值
};

// 商品排行榜项
// 文件位置: radish.client/src/apps/leaderboard/ProductLeaderboardItem.tsx
export const ProductLeaderboardItem: React.FC<Props> = ({ item, rank }) => {
  // 显示商品图标、名称、价格、销量
};
```

## API 使用示例

### 获取排行榜数据

```typescript
import { leaderboardApi, LeaderboardType } from '@/api/leaderboard';

// 获取等级排行榜第一页
const data = await leaderboardApi.getLeaderboard(
  LeaderboardType.Experience,
  1,
  50
);

if (data) {
  console.log('总记录数:', data.dataCount);
  console.log('排行榜数据:', data.data);
}
```

### 获取当前用户排名

```typescript
// 获取当前用户在等级排行榜中的排名
const rank = await leaderboardApi.getMyRank(LeaderboardType.Experience);

if (rank !== null) {
  if (rank === 0) {
    console.log('未上榜');
  } else {
    console.log('当前排名:', rank);
  }
}
```

### 获取所有排行榜类型

```typescript
// 获取所有可用的排行榜类型
const types = await leaderboardApi.getTypes();

if (types) {
  types.forEach(type => {
    console.log(`${type.voName}: ${type.voDescription}`);
  });
}
```

## 前端集成指南

### 1. 导入必要的依赖

```typescript
import { leaderboardApi, LeaderboardType, LeaderboardCategory } from '@/api/leaderboard';
import type { UnifiedLeaderboardItemData } from '@/api/leaderboard';
```

### 2. 创建状态管理

```typescript
const [leaderboardType, setLeaderboardType] = useState(LeaderboardType.Experience);
const [leaderboardData, setLeaderboardData] = useState<UnifiedLeaderboardItemData[]>([]);
const [pageIndex, setPageIndex] = useState(1);
const [totalCount, setTotalCount] = useState(0);
const [loading, setLoading] = useState(false);
```

### 3. 加载排行榜数据

```typescript
const loadLeaderboard = async () => {
  setLoading(true);
  try {
    const data = await leaderboardApi.getLeaderboard(leaderboardType, pageIndex, 50);
    if (data) {
      setLeaderboardData(data.data);
      setTotalCount(data.dataCount);
    }
  } catch (error) {
    console.error('加载排行榜失败:', error);
  } finally {
    setLoading(false);
  }
};

useEffect(() => {
  loadLeaderboard();
}, [leaderboardType, pageIndex]);
```

### 4. 渲染排行榜项

```typescript
{leaderboardData.map((item, index) => {
  const rank = (pageIndex - 1) * 50 + index + 1;

  if (item.voCategory === LeaderboardCategory.User) {
    return <UserLeaderboardItem key={item.voUserId} item={item} rank={rank} />;
  } else {
    return <ProductLeaderboardItem key={item.voProductId} item={item} rank={rank} />;
  }
})}
```

## 性能优化建议

### 1. 数据库层优化

**使用索引**:
```sql
-- 用户经验表索引
CREATE INDEX idx_user_experience_total ON UserExperience(TotalExperience DESC);

-- 用户余额表索引
CREATE INDEX idx_user_balance_current ON UserBalance(CurrentBalance DESC);

-- 商品表索引
CREATE INDEX idx_product_sales ON Product(SalesCount DESC);
```

**查询优化**:
- 使用 `LIMIT` 和 `OFFSET` 进行分页
- 避免 `SELECT *`，只查询需要的字段
- 使用 `LEFT JOIN` 而非多次查询

### 2. 缓存策略

**Redis 缓存**:
```csharp
// 缓存排行榜数据 5 分钟
var cacheKey = $"leaderboard:{type}:{pageIndex}:{pageSize}";
var cachedData = await _cache.GetAsync<PageModel<UnifiedLeaderboardItemVo>>(cacheKey);

if (cachedData == null)
{
    cachedData = await QueryLeaderboardFromDatabase(type, pageIndex, pageSize);
    await _cache.SetAsync(cacheKey, cachedData, TimeSpan.FromMinutes(5));
}

return cachedData;
```

**缓存失效策略**:
- 用户经验变化时，清除等级排行榜缓存
- 订单完成时，清除余额、花销、购买榜缓存
- 帖子/评论创建时，清除对应排行榜缓存

### 3. 前端优化

**虚拟滚动**:
```typescript
// 使用 react-window 或 react-virtualized 实现虚拟滚动
import { FixedSizeList } from 'react-window';

<FixedSizeList
  height={600}
  itemCount={leaderboardData.length}
  itemSize={80}
  width="100%"
>
  {({ index, style }) => (
    <div style={style}>
      <UserLeaderboardItem item={leaderboardData[index]} rank={index + 1} />
    </div>
  )}
</FixedSizeList>
```

**懒加载**:
```typescript
// 使用 Intersection Observer 实现无限滚动
const loadMore = () => {
  if (!loading && leaderboardData.length < totalCount) {
    setPageIndex(prev => prev + 1);
  }
};
```

## 扩展指南

### 添加新的排行榜类型

#### 1. 定义枚举

```csharp
// Radish.Shared/CustomEnum/LeaderboardEnums.cs
public enum LeaderboardType
{
    // ... 现有类型

    /// <summary>签到达人排行榜</summary>
    /// <remarks>按用户连续签到天数排序</remarks>
    CheckInStreak = 9
}
```

#### 2. 添加类型配置

```csharp
// Radish.Service/LeaderboardService.cs
[LeaderboardType.CheckInStreak] = new LeaderboardTypeVo
{
    VoType = LeaderboardType.CheckInStreak,
    VoCategory = LeaderboardCategory.User,
    VoName = "签到达人榜",
    VoDescription = "按用户连续签到天数排序",
    VoIcon = "mdi:calendar-check",
    VoPrimaryLabel = "连续签到",
    VoSortOrder = 9
}
```

#### 3. 实现查询方法

```csharp
// Radish.Repository/LeaderboardRepository.cs
public async Task<List<UserLeaderboardDto>> GetCheckInStreakLeaderboardAsync(
    int pageIndex,
    int pageSize)
{
    return await _db.Queryable<UserCheckIn>()
        .Where(c => !c.IsDeleted)
        .OrderByDescending(c => c.CurrentStreak)
        .Select(c => new UserLeaderboardDto
        {
            UserId = c.UserId,
            PrimaryValue = c.CurrentStreak,
            SecondaryValue = c.TotalCheckInDays
        })
        .ToPageListAsync(pageIndex, pageSize);
}
```

#### 4. 在 Service 中调用

```csharp
// Radish.Service/LeaderboardService.cs
private async Task<List<UserLeaderboardDto>> GetUserLeaderboardDataAsync(
    LeaderboardType type,
    int pageIndex,
    int pageSize)
{
    return type switch
    {
        // ... 现有类型
        LeaderboardType.CheckInStreak =>
            await _leaderboardRepository.GetCheckInStreakLeaderboardAsync(pageIndex, pageSize),
        _ => throw new ArgumentException($"不支持的排行榜类型: {type}")
    };
}
```

#### 5. 更新前端枚举

```typescript
// radish.client/src/api/leaderboard.ts
export enum LeaderboardType {
  // ... 现有类型
  /** 签到达人排行榜 */
  CheckInStreak = 9,
}
```

### 添加实时更新功能

使用 SignalR 实现排行榜实时更新：

```csharp
// 后端 Hub
public class LeaderboardHub : Hub
{
    public async Task SubscribeLeaderboard(LeaderboardType type)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"leaderboard_{type}");
    }

    public async Task UnsubscribeLeaderboard(LeaderboardType type)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"leaderboard_{type}");
    }
}

// 数据变化时推送更新
await _hubContext.Clients.Group($"leaderboard_{type}")
    .SendAsync("LeaderboardUpdated", updatedData);
```

```typescript
// 前端订阅
const connection = new HubConnectionBuilder()
  .withUrl('/hubs/leaderboard')
  .build();

connection.on('LeaderboardUpdated', (data) => {
  setLeaderboardData(data);
});

await connection.start();
await connection.invoke('SubscribeLeaderboard', leaderboardType);
```

## 常见问题

### Q: 排行榜数据多久更新一次？
A: 默认情况下，排行榜数据实时查询数据库。建议在生产环境中启用 Redis 缓存，缓存时间设置为 5-10 分钟。

### Q: 如何处理大量用户的排名查询？
A: 对于用户排名查询，建议：
1. 使用 Redis 缓存用户排名，缓存时间 10 分钟
2. 使用数据库索引优化查询性能
3. 考虑使用 Redis Sorted Set 存储排行榜数据

### Q: 排行榜支持多少用户？
A: 理论上支持无限用户，但建议：
1. 前端只展示 Top 100 或 Top 1000
2. 使用分页查询，每页 50-100 条
3. 对于超大规模用户，考虑使用预计算 + 定时任务更新

### Q: 如何防止排行榜作弊？
A: 建议实施以下措施：
1. 数据验证：在业务逻辑层验证数据合法性
2. 异常检测：监控异常增长的数据
3. 人工审核：对 Top 排名用户进行人工审核
4. 限流机制：限制用户操作频率

## 相关文档

- [BaseService 扩展方法指南](../guide/base-service-advanced.md)
- [架构规范](../architecture/specifications.md)
- [前端设计](../frontend/design.md)
