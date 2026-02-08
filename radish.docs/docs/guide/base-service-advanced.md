# BaseService 扩展方法指南

## 概述

`BaseService<TEntity, TVo>` 提供了丰富的数据访问方法，涵盖基础 CRUD、聚合查询、批量操作和工具方法。本指南详细介绍 2026-01-31 新增的扩展方法，帮助开发者充分利用这些功能，避免在 Service 层直接访问数据库。

## 核心原则

### ✅ 推荐做法
- **优先使用泛型 `IBaseService<TEntity, TVo>`**：大多数业务场景无需创建自定义 Service
- **使用 BaseService 提供的方法**：避免在 Service 层直接访问 `_repository.Db`
- **数据库级聚合**：使用聚合查询方法，避免内存过滤和计算

### ❌ 禁止做法
- **禁止直接访问 Db 实例**：`_repository.Db.Queryable<Entity>()` 是错误的
- **禁止内存聚合**：不要查询全部数据后在内存中计算
- **禁止创建不必要的 Service**：简单 CRUD 直接用 `IBaseService`

## 新增方法分类

### 1. 聚合查询方法

#### QueryDistinctAsync - 去重查询

**用途**：查询某个字段的不同值列表（去重）

**方法签名**：
```csharp
Task<List<TResult>> QueryDistinctAsync<TResult>(
    Expression<Func<TEntity, TResult>> selectExpression,
    Expression<Func<TEntity, bool>>? whereExpression = null);
```

**使用场景**：
- 获取所有不同的分类 ID
- 获取所有不同的标签
- 获取所有不同的状态值

**示例**：
```csharp
// 获取所有不同的帖子 ID（评论表）
var postIds = await _commentService.QueryDistinctAsync(
    c => c.PostId,
    c => c.IsEnabled);

// 获取所有不同的商品分类 ID
var categoryIds = await _productService.QueryDistinctAsync(
    p => p.CategoryId);
```

**性能优势**：
```csharp
// ❌ 错误：内存去重
var allComments = await _commentService.QueryAsync(c => c.IsEnabled);
var postIds = allComments.Select(c => c.PostId).Distinct().ToList();

// ✅ 正确：数据库级去重
var postIds = await _commentService.QueryDistinctAsync(
    c => c.PostId,
    c => c.IsEnabled);
```

#### QuerySumAsync - 求和聚合

**用途**：对某个字段进行求和

**方法签名**：
```csharp
Task<TResult> QuerySumAsync<TResult>(
    Expression<Func<TEntity, TResult>> selectExpression,
    Expression<Func<TEntity, bool>>? whereExpression = null);
```

**使用场景**：
- 计算用户总余额
- 计算订单总金额
- 计算商品总销量

**示例**：
```csharp
// 计算用户的总消费金额
var totalSpent = await _orderService.QuerySumAsync<decimal>(
    o => o.TotalAmount,
    o => o.UserId == userId && o.Status == OrderStatus.Completed);

// 计算所有商品的总销量
var totalSales = await _productService.QuerySumAsync<int>(
    p => p.SalesCount);
```

**性能对比**：
```csharp
// ❌ 错误：内存求和（查询所有数据）
var orders = await _orderService.QueryAsync(o => o.UserId == userId);
var totalSpent = orders.Sum(o => o.TotalAmount);

// ✅ 正确：数据库级求和
var totalSpent = await _orderService.QuerySumAsync<decimal>(
    o => o.TotalAmount,
    o => o.UserId == userId);
```

#### QueryMaxAsync - 最大值聚合

**用途**：查询某个字段的最大值

**方法签名**：
```csharp
Task<TResult> QueryMaxAsync<TResult>(
    Expression<Func<TEntity, TResult>> selectExpression,
    Expression<Func<TEntity, bool>>? whereExpression = null);
```

**使用场景**：
- 查询最高价格
- 查询最大经验值
- 查询最新的排序号

**示例**：
```csharp
// 查询商品的最高价格
var maxPrice = await _productService.QueryMaxAsync<decimal>(
    p => p.Price,
    p => p.IsEnabled);

// 查询用户的最高等级
var maxLevel = await _userExpService.QueryMaxAsync<int>(
    u => u.CurrentLevel);
```

#### QueryMinAsync - 最小值聚合

**用途**：查询某个字段的最小值

**方法签名**：
```csharp
Task<TResult> QueryMinAsync<TResult>(
    Expression<Func<TEntity, TResult>> selectExpression,
    Expression<Func<TEntity, bool>>? whereExpression = null);
```

**使用场景**：
- 查询最低价格
- 查询最早的创建时间
- 查询最小的排序号

**示例**：
```csharp
// 查询商品的最低价格
var minPrice = await _productService.QueryMinAsync<decimal>(
    p => p.Price,
    p => p.IsEnabled && p.Stock > 0);

// 查询最早的订单时间
var firstOrderTime = await _orderService.QueryMinAsync<DateTime>(
    o => o.CreateTime,
    o => o.UserId == userId);
```

#### QueryAverageAsync - 平均值聚合

**用途**：查询某个字段的平均值

**方法签名**：
```csharp
Task<decimal> QueryAverageAsync(
    Expression<Func<TEntity, decimal>> selectExpression,
    Expression<Func<TEntity, bool>>? whereExpression = null);
```

**使用场景**：
- 计算平均价格
- 计算平均评分
- 计算平均订单金额

**示例**：
```csharp
// 计算商品的平均价格
var avgPrice = await _productService.QueryAverageAsync(
    p => p.Price,
    p => p.IsEnabled);

// 计算用户的平均订单金额
var avgOrderAmount = await _orderService.QueryAverageAsync(
    o => o.TotalAmount,
    o => o.UserId == userId && o.Status == OrderStatus.Completed);
```

### 2. 批量查询方法

#### QueryByIdsAsync - 批量 ID 查询

**用途**：根据多个 ID 批量查询实体

**方法签名**：
```csharp
Task<List<TVo>> QueryByIdsAsync(List<long> ids);
```

**使用场景**：
- 根据多个用户 ID 查询用户信息
- 根据多个商品 ID 查询商品详情
- 根据多个帖子 ID 查询帖子内容

**示例**：
```csharp
// 批量查询用户信息
var userIds = new List<long> { 1, 2, 3, 4, 5 };
var users = await _userService.QueryByIdsAsync(userIds);

// 批量查询商品信息
var productIds = comments.Select(c => c.ProductId).Distinct().ToList();
var products = await _productService.QueryByIdsAsync(productIds);
```

**性能优势**：
```csharp
// ❌ 错误：循环查询（N+1 问题）
var users = new List<UserVo>();
foreach (var userId in userIds)
{
    var user = await _userService.QueryByIdAsync(userId);
    if (user != null) users.Add(user);
}

// ✅ 正确：批量查询
var users = await _userService.QueryByIdsAsync(userIds);
```

### 3. 排序查询方法

#### QueryWithOrderAsync - 带排序的列表查询

**用途**：查询带排序的列表，可选限制数量

**方法签名**：
```csharp
Task<List<TVo>> QueryWithOrderAsync(
    Expression<Func<TEntity, bool>>? whereExpression,
    Expression<Func<TEntity, object>> orderByExpression,
    OrderByType orderByType = OrderByType.Asc,
    int take = 0);
```

**使用场景**：
- 查询最新的 N 条记录
- 查询热门的 N 条记录
- 查询排名前 N 的记录

**示例**：
```csharp
// 查询最新的 10 条帖子
var latestPosts = await _postService.QueryWithOrderAsync(
    p => p.IsEnabled,
    p => p.CreateTime,
    OrderByType.Desc,
    take: 10);

// 查询销量最高的 20 个商品
var hotProducts = await _productService.QueryWithOrderAsync(
    p => p.IsEnabled && p.Stock > 0,
    p => p.SalesCount,
    OrderByType.Desc,
    take: 20);

// 查询所有用户，按经验值降序（不限制数量）
var allUsers = await _userExpService.QueryWithOrderAsync(
    u => u.IsEnabled,
    u => u.TotalExperience,
    OrderByType.Desc);
```

#### QueryPageAsync (重载) - 二级排序分页查询

**用途**：支持二级排序的分页查询

**方法签名**：
```csharp
Task<(List<TVo> data, int totalCount)> QueryPageAsync(
    Expression<Func<TEntity, bool>>? whereExpression,
    int pageIndex,
    int pageSize,
    Expression<Func<TEntity, object>>? orderByExpression,
    OrderByType orderByType,
    Expression<Func<TEntity, object>>? thenByExpression,
    OrderByType thenByType);
```

**使用场景**：
- 先按分类排序，再按创建时间排序
- 先按状态排序，再按优先级排序
- 先按销量排序，再按价格排序

**示例**：
```csharp
// 查询商品：先按分类升序，再按销量降序
var (products, total) = await _productService.QueryPageAsync(
    whereExpression: p => p.IsEnabled,
    pageIndex: 1,
    pageSize: 20,
    orderByExpression: p => p.CategoryId,
    orderByType: OrderByType.Asc,
    thenByExpression: p => p.SalesCount,
    thenByType: OrderByType.Desc);

// 查询订单：先按状态升序，再按创建时间降序
var (orders, total) = await _orderService.QueryPageAsync(
    whereExpression: o => o.UserId == userId,
    pageIndex: 1,
    pageSize: 10,
    orderByExpression: o => o.Status,
    orderByType: OrderByType.Asc,
    thenByExpression: o => o.CreateTime,
    thenByType: OrderByType.Desc);
```

### 4. 工具方法

#### ExecuteWithRetryAsync - 乐观锁重试机制

**用途**：执行带重试的异步操作，自动处理乐观锁冲突

**方法签名**：
```csharp
Task<TResult> ExecuteWithRetryAsync<TResult>(
    Func<Task<TResult>> action,
    int maxRetryCount = 3,
    int baseDelayMs = 100);
```

**使用场景**：
- 高并发更新操作
- 库存扣减
- 余额变更
- 计数器更新

**重试策略**：
- 指数退避：100ms, 200ms, 400ms...
- 仅捕获 `ConcurrencyException` 进行重试
- 其他异常直接抛出

**示例**：
```csharp
// 扣减库存（高并发场景）
var success = await _productService.ExecuteWithRetryAsync(async () =>
{
    var product = await _productRepository.QueryByIdAsync(productId);
    if (product == null || product.Stock < quantity)
    {
        return false;
    }

    product.Stock -= quantity;
    product.SalesCount += quantity;
    return await _productRepository.UpdateAsync(product);
}, maxRetryCount: 5);

// 更新用户余额
var newBalance = await _userBalanceService.ExecuteWithRetryAsync(async () =>
{
    var balance = await _userBalanceRepository.QueryFirstAsync(b => b.UserId == userId);
    if (balance == null) return 0m;

    balance.CurrentBalance += amount;
    balance.TotalIncome += amount;
    await _userBalanceRepository.UpdateAsync(balance);
    return balance.CurrentBalance;
});
```

**注意事项**：
- 确保操作是幂等的（可以安全重试）
- 避免在重试操作中执行不可逆的操作（如发送邮件）
- 对于非乐观锁异常，不会重试

#### GetOrCreateAsync - 获取或创建模式

**用途**：如果实体不存在则创建，存在则返回

**方法签名**：
```csharp
Task<TEntity> GetOrCreateAsync(
    Expression<Func<TEntity, bool>> predicate,
    Func<TEntity> createFactory);
```

**使用场景**：
- 确保用户配置存在
- 确保用户余额记录存在
- 确保用户经验记录存在

**示例**：
```csharp
// 获取或创建用户余额记录
var balance = await _userBalanceService.GetOrCreateAsync(
    b => b.UserId == userId,
    () => new UserBalance
    {
        UserId = userId,
        CurrentBalance = 0,
        TotalIncome = 0,
        TotalSpent = 0
    });

// 获取或创建用户配置
var config = await _userConfigService.GetOrCreateAsync(
    c => c.UserId == userId,
    () => new UserConfig
    {
        UserId = userId,
        Theme = "light",
        Language = "zh-CN"
    });
```

**注意事项**：
- 此方法返回 `TEntity` 而非 `TVo`
- 如果需要 ViewModel，需要手动映射
- 在高并发场景下可能出现重复创建，建议配合唯一索引使用

## 实战案例

### 案例 1：排行榜功能

**需求**：实现用户经验排行榜

**错误实现**（内存排序）：
```csharp
// ❌ 查询所有用户，内存排序
var allUsers = await _userExpService.QueryAsync();
var topUsers = allUsers
    .OrderByDescending(u => u.TotalExperience)
    .Take(100)
    .ToList();
```

**正确实现**（数据库排序）：
```csharp
// ✅ 使用 QueryWithOrderAsync
var topUsers = await _userExpService.QueryWithOrderAsync(
    u => u.IsEnabled,
    u => u.TotalExperience,
    OrderByType.Desc,
    take: 100);
```

### 案例 2：统计面板

**需求**：显示用户的统计数据

**错误实现**（多次查询 + 内存计算）：
```csharp
// ❌ 查询所有订单，内存计算
var orders = await _orderService.QueryAsync(o => o.UserId == userId);
var totalSpent = orders.Sum(o => o.TotalAmount);
var orderCount = orders.Count;
var avgAmount = orders.Average(o => o.TotalAmount);
```

**正确实现**（数据库聚合）：
```csharp
// ✅ 使用聚合方法
var totalSpent = await _orderService.QuerySumAsync<decimal>(
    o => o.TotalAmount,
    o => o.UserId == userId);

var orderCount = await _orderService.QueryCountAsync(
    o => o.UserId == userId);

var avgAmount = await _orderService.QueryAverageAsync(
    o => o.TotalAmount,
    o => o.UserId == userId);
```

### 案例 3：商品详情页

**需求**：显示商品详情和相关评论的用户信息

**错误实现**（N+1 查询）：
```csharp
// ❌ 循环查询用户信息
var comments = await _commentService.QueryAsync(c => c.ProductId == productId);
foreach (var comment in comments)
{
    var user = await _userService.QueryByIdAsync(comment.UserId);
    // 处理用户信息
}
```

**正确实现**（批量查询）：
```csharp
// ✅ 批量查询用户信息
var comments = await _commentService.QueryAsync(c => c.ProductId == productId);
var userIds = comments.Select(c => c.UserId).Distinct().ToList();
var users = await _userService.QueryByIdsAsync(userIds);
var userDict = users.ToDictionary(u => u.VoId);

foreach (var comment in comments)
{
    if (userDict.TryGetValue(comment.UserId, out var user))
    {
        // 处理用户信息
    }
}
```

### 案例 4：库存扣减

**需求**：高并发场景下扣减商品库存

**错误实现**（无重试机制）：
```csharp
// ❌ 无重试，并发冲突时失败
var product = await _productRepository.QueryByIdAsync(productId);
if (product.Stock < quantity) return false;

product.Stock -= quantity;
return await _productRepository.UpdateAsync(product);
```

**正确实现**（乐观锁重试）：
```csharp
// ✅ 使用 ExecuteWithRetryAsync
var success = await _productService.ExecuteWithRetryAsync(async () =>
{
    var product = await _productRepository.QueryByIdAsync(productId);
    if (product == null || product.Stock < quantity)
    {
        return false;
    }

    product.Stock -= quantity;
    product.SalesCount += quantity;
    return await _productRepository.UpdateAsync(product);
}, maxRetryCount: 5);
```

## 性能优化建议

### 1. 使用数据库级聚合

**原则**：能在数据库做的计算，不要在内存做

```csharp
// ❌ 内存聚合
var all = await _service.QueryAsync();
var sum = all.Sum(x => x.Amount);

// ✅ 数据库聚合
var sum = await _service.QuerySumAsync<decimal>(x => x.Amount);
```

### 2. 避免 N+1 查询

**原则**：批量查询代替循环查询

```csharp
// ❌ N+1 查询
foreach (var id in ids)
{
    var item = await _service.QueryByIdAsync(id);
}

// ✅ 批量查询
var items = await _service.QueryByIdsAsync(ids);
```

### 3. 使用分页查询

**原则**：大数据量必须分页

```csharp
// ❌ 查询全部数据
var all = await _service.QueryAsync();

// ✅ 分页查询
var (data, total) = await _service.QueryPageAsync(
    whereExpression: null,
    pageIndex: 1,
    pageSize: 20);
```

### 4. 合理使用缓存

**原则**：频繁查询且变化不频繁的数据使用缓存

```csharp
// 配置数据适合缓存
var configs = await _configService.QueryWithCacheAsync(
    c => c.IsEnabled,
    cacheTime: 600); // 缓存 10 分钟
```

## 扩展 BaseRepository

当 BaseService 提供的方法无法满足需求时，可以扩展 BaseRepository。

### 扩展策略

#### 1. 优先扩展泛型方法（跨实体复用）

```csharp
// Radish.Repository/Base/BaseRepository.cs
public partial class BaseRepository<TEntity>
{
    /// <summary>查询字段的中位数</summary>
    public async Task<decimal> QueryMedianAsync(
        Expression<Func<TEntity, decimal>> selectExpression,
        Expression<Func<TEntity, bool>>? whereExpression = null)
    {
        // 实现中位数查询逻辑
    }
}
```

#### 2. 次选创建实体专属仓储（复杂查询）

```csharp
// Radish.IRepository/IUserRepository.cs
public interface IUserRepository : IBaseRepository<User>
{
    Task<List<UserStatisticsDto>> GetUserStatisticsAsync(DateTime startDate, DateTime endDate);
}

// Radish.Repository/UserRepository.cs
public class UserRepository : BaseRepository<User>, IUserRepository
{
    public async Task<List<UserStatisticsDto>> GetUserStatisticsAsync(
        DateTime startDate,
        DateTime endDate)
    {
        return await Db.Queryable<User>()
            .LeftJoin<UserExperience>((u, e) => u.Id == e.UserId)
            .LeftJoin<UserBalance>((u, e, b) => u.Id == b.UserId)
            .Where((u, e, b) => u.CreateTime >= startDate && u.CreateTime <= endDate)
            .Select((u, e, b) => new UserStatisticsDto
            {
                UserId = u.Id,
                UserName = u.UserName,
                TotalExperience = e.TotalExperience,
                CurrentBalance = b.CurrentBalance
            })
            .ToListAsync();
    }
}
```

## 常见问题

### Q: 什么时候需要创建自定义 Service？

A: 仅在以下情况下创建自定义 Service：
- 需要复杂的业务逻辑（跨多个实体的事务操作）
- 需要调用外部服务（发送邮件、调用第三方 API）
- 需要特殊的数据转换逻辑

简单的 CRUD 操作直接使用 `IBaseService<TEntity, TVo>` 即可。

### Q: Service 层可以直接访问 `_repository.Db` 吗？

A: **不可以**。这违反了架构规范。所有数据库访问必须通过 Repository 方法。如果 BaseRepository 没有提供所需方法，应该扩展 BaseRepository 或创建实体专属仓储。

### Q: 聚合查询方法的性能如何？

A: 聚合查询方法直接在数据库层执行，性能远优于查询全部数据后在内存中计算。例如：
- `QuerySumAsync` 生成 `SELECT SUM(column) FROM table` SQL
- `QueryDistinctAsync` 生成 `SELECT DISTINCT column FROM table` SQL

### Q: ExecuteWithRetryAsync 适用于所有更新操作吗？

A: 不是。仅适用于可能出现乐观锁冲突的高并发更新场景。对于低并发场景，直接使用 `UpdateAsync` 即可。

### Q: GetOrCreateAsync 在高并发下安全吗？

A: 在高并发场景下可能出现重复创建。建议：
1. 在数据库层添加唯一索引
2. 捕获唯一约束异常，重新查询
3. 或使用分布式锁

## 相关文档

- [架构规范](../architecture/specifications.md)
- [排行榜功能文档](../features/leaderboard.md)
- [软删除规范](../architecture/specifications.md#软删除规范)
