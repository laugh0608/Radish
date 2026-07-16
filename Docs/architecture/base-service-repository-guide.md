# BaseService 与 BaseRepository 专题指南

> 摘要：本文承接开发规范入口中的泛型 Service / Repository 详细说明，集中记录基础能力、软删除约束、常见查询与自定义 Service 的适用边界。公共方法的最终签名以 `Radish.IService`、`Radish.Service`、`Radish.IRepository` 与 `Radish.Repository` 当前源码为准。

## 阅读导航

- [核心指南](#核心指南)：设计原则、完整能力、查询示例与使用场景
- [软删除实体规范](#软删除实体规范)：实体契约、字段与自动初始化
- [相关文档](#相关文档)：进阶方法与开发规范入口

## 核心指南

### 设计理念

Radish 项目采用泛型基类模式来避免为每个实体重复编写相同的 CRUD 代码：

- `BaseRepository<TEntity>`：提供完整的数据库操作方法（基于 SqlSugar）
- `BaseService<TEntity, TVo>`：提供完整的业务层方法（自动进行实体到 ViewModel 的映射）

**核心原则**：
- ✅ **优先使用 BaseService/BaseRepository** - 减少重复代码，保持架构简洁
- ✅ **只为复杂业务逻辑创建自定义 Service** - 例如涉及多表事务、复杂验证的场景
- ❌ **避免创建只包装 Base 方法的 Service** - 这会增加不必要的抽象层

### BaseService 提供的完整功能


#### 增（Create）
```csharp
Task<long> AddAsync(TEntity entity)                     // 插入单条,返回雪花ID
Task<int> AddRangeAsync(List<TEntity> entities)         // 批量插入
Task<List<long>> AddSplitAsync(TEntity entity)          // 分表插入
```

#### 删（Delete）
```csharp
Task<bool> DeleteByIdAsync(long id)                     // 根据ID删除
Task<bool> DeleteAsync(TEntity entity)                  // 根据实体删除
Task<int> DeleteAsync(Expression<Func<TEntity, bool>>) // 根据条件删除
Task<int> DeleteByIdsAsync(List<long> ids)             // 批量删除
```

**⚠️ 重要：物理删除方法已标记为过时，推荐使用软删除**

#### 软删除（Soft Delete）- 推荐
```csharp
// 软删除方法
Task<bool> SoftDeleteByIdAsync(long id, string? deletedBy = null)           // 根据ID软删除
Task<int> SoftDeleteAsync(Expression<Func<TEntity, bool>>, string? deletedBy) // 根据条件软删除

// 恢复方法
Task<bool> RestoreByIdAsync(long id)                                        // 根据ID恢复
Task<int> RestoreAsync(Expression<Func<TEntity, bool>>)                     // 根据条件恢复
```

**软删除规范**：
- ✅ **推荐**：业务数据使用软删除，保留完整审计轨迹
- ✅ **自动过滤**：查询方法自动过滤 `IsDeleted = true` 的记录
- ✅ **审计信息**：记录删除时间（`DeletedAt`）和操作者（`DeletedBy`）
- ✅ **可恢复**：支持恢复已软删除的记录
- ❌ **避免**：物理删除业务数据（已标记 `[Obsolete]`）

**实体要求**：
```csharp
// 实体必须实现 IDeleteFilter 接口
public class UserBalance : RootEntityTKey<long>, IDeleteFilter
{
    // 业务字段...

    // 软删除字段（自动添加）
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
```

**使用示例**：
```csharp
// Service 层使用
await _userService.SoftDeleteByIdAsync(userId, "Admin");
await _userService.RestoreByIdAsync(userId);

// Repository 层使用
await _repository.SoftDeleteAsync(u => u.IsEnabled == false, "System");
```

#### 改（Update）
```csharp
Task<bool> UpdateAsync(TEntity entity)                  // 更新整个实体
Task<int> UpdateRangeAsync(List<TEntity> entities)     // 批量更新
Task<bool> UpdateColumnsAsync(TEntity entity, columns)  // 更新指定列
Task<int> UpdateColumnsAsync(updateExp, whereExp)      // 根据条件更新指定列
```

#### 查（Query）
```csharp
Task<TVo?> QueryByIdAsync(long id)                     // 根据ID查询
Task<List<TVo>> QueryByIdsAsync(List<long> ids)       // 批量ID查询
Task<TVo?> QueryFirstAsync(Expression<...>)            // 查询第一条
Task<TVo?> QuerySingleAsync(Expression<...>)           // 查询单条（多条抛异常）
Task<List<TVo>> QueryAsync(Expression<...>)            // 条件查询列表
Task<List<TVo>> QueryWithCacheAsync(Expression<...>)   // 带缓存的查询
Task<List<TVo>> QueryWithOrderAsync(...)              // 带排序的列表查询（支持Take限制）
Task<(List<TVo>, int)> QueryPageAsync(...)            // 分页查询（支持二级排序）
Task<int> QueryCountAsync(Expression<...>)             // 查询数量
Task<bool> QueryExistsAsync(Expression<...>)           // 判断是否存在
Task<List<TResult>> QueryMuchAsync<...>(...)          // 三表联查
Task<List<TEntity>> QuerySplitAsync(...)              // 分表查询
```

#### 聚合查询

**数据库级聚合，避免内存过滤**：

```csharp
// 去重查询 - 查询某个字段的不同值列表
Task<List<TResult>> QueryDistinctAsync<TResult>(selectExpression, whereExpression)

// 求和 - 对某个字段进行求和
Task<TResult> QuerySumAsync<TResult>(selectExpression, whereExpression)

// 最大值 - 查询某个字段的最大值
Task<TResult> QueryMaxAsync<TResult>(selectExpression, whereExpression)

// 最小值 - 查询某个字段的最小值
Task<TResult> QueryMinAsync<TResult>(selectExpression, whereExpression)

// 平均值 - 查询某个字段的平均值
Task<decimal> QueryAverageAsync(selectExpression, whereExpression)
```

**使用示例**：

```csharp
// 获取所有不同的帖子 ID（评论表）
var postIds = await _commentService.QueryDistinctAsync(
    c => c.PostId,
    c => c.IsEnabled);

// 计算用户的总消费金额
var totalSpent = await _orderService.QuerySumAsync<decimal>(
    o => o.TotalAmount,
    o => o.UserId == userId && o.Status == OrderStatus.Completed);

// 查询商品的最高价格
var maxPrice = await _productService.QueryMaxAsync<decimal>(
    p => p.Price,
    p => p.IsEnabled);

// 计算商品的平均价格
var avgPrice = await _productService.QueryAverageAsync(
    p => p.Price,
    p => p.IsEnabled);
```

**性能对比**：

```csharp
// ❌ 错误：内存聚合（查询所有数据）
var orders = await _orderService.QueryAsync(o => o.UserId == userId);
var totalSpent = orders.Sum(o => o.TotalAmount);

// ✅ 正确：数据库级聚合
var totalSpent = await _orderService.QuerySumAsync<decimal>(
    o => o.TotalAmount,
    o => o.UserId == userId);
```

#### 批量查询

```csharp
// 根据多个 ID 批量查询实体（避免 N+1 查询）
Task<List<TVo>> QueryByIdsAsync(List<long> ids)
```

**使用示例**：

```csharp
// ✅ 正确：批量查询
var userIds = new List<long> { 1, 2, 3, 4, 5 };
var users = await _userService.QueryByIdsAsync(userIds);

// ❌ 错误：循环查询（N+1 问题）
var users = new List<UserVo>();
foreach (var userId in userIds)
{
    var user = await _userService.QueryByIdAsync(userId);
    if (user != null) users.Add(user);
}
```

#### 排序查询

```csharp
// 带排序的列表查询，可选限制数量
Task<List<TVo>> QueryWithOrderAsync(
    whereExpression,
    orderByExpression,
    orderByType = OrderByType.Asc,
    take = 0)

// 二级排序分页查询
Task<(List<TVo> data, int totalCount)> QueryPageAsync(
    whereExpression,
    pageIndex,
    pageSize,
    orderByExpression,
    orderByType,
    thenByExpression,
    thenByType)
```

**使用示例**：

```csharp
// 查询最新的 10 条帖子
var latestPosts = await _postService.QueryWithOrderAsync(
    p => p.IsEnabled,
    p => p.CreateTime,
    OrderByType.Desc,
    take: 10);

// 查询商品：先按分类升序，再按销量降序
var (products, total) = await _productService.QueryPageAsync(
    whereExpression: p => p.IsEnabled,
    pageIndex: 1,
    pageSize: 20,
    orderByExpression: p => p.CategoryId,
    orderByType: OrderByType.Asc,
    thenByExpression: p => p.SalesCount,
    thenByType: OrderByType.Desc);
```

#### 工具方法

```csharp
// 乐观锁重试机制（指数退避：100ms, 200ms, 400ms...）
Task<TResult> ExecuteWithRetryAsync<TResult>(
    Func<Task<TResult>> action,
    int maxRetryCount = 3,
    int baseDelayMs = 100)

// 获取或创建模式
Task<TEntity> GetOrCreateAsync(
    Expression<Func<TEntity, bool>> predicate,
    Func<TEntity> createFactory)
```

**使用示例**：

```csharp
// 扣减库存（高并发场景，使用乐观锁重试）
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

// 获取或创建用户余额记录
var balance = await _userBalanceService.GetOrCreateAsync(
    b => b.UserId == userId,
    () => new UserBalance
    {
        UserId = userId,
        Balance = 0,
        TotalEarned = 0,
        TotalSpent = 0
    });
```

**详细文档**：参见 [BaseService 扩展方法指南](../guide/base-service-advanced.md)


### 使用场景


#### 场景 1：简单 CRUD（直接使用 BaseService）

✅ **推荐做法**：

```csharp
// Controller
public class CategoryController : ControllerBase
{
    private readonly IBaseService<Category, CategoryVo> _categoryService;

    public CategoryController(IBaseService<Category, CategoryVo> categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<MessageModel> GetTopCategories()
    {
        var categories = await _categoryService.QueryAsync(
            c => c.ParentId == null && c.IsEnabled && !c.IsDeleted);
        return new MessageModel { IsSuccess = true, ResponseData = categories };
    }

    [HttpGet("{id:long}")]
    public async Task<MessageModel> GetById(long id)
    {
        var category = await _categoryService.QueryByIdAsync(id);
        if (category == null)
            return new MessageModel { IsSuccess = false, MessageInfo = "分类不存在" };
        return new MessageModel { IsSuccess = true, ResponseData = category };
    }
}
```

#### 场景 2：复杂业务逻辑（创建自定义 Service）

❌ **不推荐**：创建只包装 BaseService 方法的 Service

```csharp
// 不要这样做！
public interface ICategoryService : IBaseService<Category, CategoryVo>
{
    Task<List<CategoryVo>> GetTopCategoriesAsync(); // 只是 QueryAsync 的包装
}
```

✅ **推荐边界**：当多实体写入、事务、幂等、可靠消息或复杂验证确有需要时创建自定义 Service，并把一致性边界显式交给所属应用服务。不要从通用指南复制一段跨表顺序写入代码；论坛发布的真实实现由 `ForumContentWriteService.PublishPostAsync` 协调提交账本、`PostService.PublishPostAsync`、标签 / 分类更新与 Reliable Outbox，并通过 `[UseTran]` 保证同一事务。查询类扩展仍应返回 Vo，字段遵循 `Vo*` 命名。

具体实现入口：`Radish.Service/ForumContentWriteService.cs`、`Radish.Service/Posts/PostService.Publish.cs` 与 [论坛内容写入可靠性治理](/guide/forum-content-write-reliability-governance)。


### BaseRepository 直接使用

在自定义 Service 中，如果需要操作其他实体，可以直接注入 `IBaseRepository<T>`：


```csharp
public class PostService : BaseService<Post, PostVo>, IPostService
{
    private readonly IBaseRepository<Category> _categoryRepository; // 直接使用 BaseRepository

    public async Task SomeBusinessLogic()
    {
        // 直接调用 BaseRepository 方法
        var category = await _categoryRepository.QueryByIdAsync(123);
        category.PostCount++;
        await _categoryRepository.UpdateAsync(category);
    }
}
```


### 最佳实践总结

1. **默认使用 BaseService** - Controller 直接注入 `IBaseService<TEntity, TVo>`
2. **谨慎创建自定义 Service** - 只在以下情况创建：
   - 需要操作多个实体（事务协调）
   - 有复杂的业务验证逻辑
   - 需要调用外部服务/API
3. **自定义 Service 继承 BaseService** - 复用基础方法，只添加特殊逻辑
4. **命名规范**：
   - 接口：`IPostService : IBaseService<Post, PostVo>`
   - 实现：`PostService : BaseService<Post, PostVo>, IPostService`
   - 若实现规模较大，可保留同名 `PostService`，但按职责拆到子目录和 `partial class` 文件中，避免单文件失控

---

## 软删除实体规范


**实体软删除接口**：
- 所有业务实体应实现 `IDeleteFilter` 接口以支持软删除功能
- 接口定义：
  ```csharp
  public interface IDeleteFilter
  {
      bool IsDeleted { get; set; }        // 软删除标记
      DateTime? DeletedAt { get; set; }   // 删除时间
      string? DeletedBy { get; set; }     // 删除操作者
  }
  ```

**实体实现示例**：
```csharp
[SugarTable("UserBalance")]
public class UserBalance : RootEntityTKey<long>, IDeleteFilter
{
    // 业务字段...
    public long UserId { get; set; }
    public decimal Balance { get; set; }

    #region 审计信息
    public DateTime CreateTime { get; set; } = DateTime.Now;
    public string CreateBy { get; set; } = "System";
    public long CreateId { get; set; } = 0;
    public DateTime? ModifyTime { get; set; }
    public string? ModifyBy { get; set; }
    public long? ModifyId { get; set; }

    // 软删除字段
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    #endregion
}
```

**字段规范**：
- `IsDeleted`: 不可空，默认 `false`，软删除标记
- `DeletedAt`: 可空，软删除时自动设置，恢复时清空
- `DeletedBy`: 可空，最大 50 字符，执行软删除操作的用户名或系统标识

**自动初始化**：
- BaseRepository 的 `AddAsync` 方法自动确保新记录的软删除字段正确初始化
- 实现 `IDeleteFilter` 接口的实体在创建时自动设置 `IsDeleted = false`

## 相关文档

- [开发规范](./specifications.md)
- [BaseService 扩展方法指南](../guide/base-service-advanced.md)
- [数据库结构变更协作口径](../guide/database-schema-change-governance.md)
