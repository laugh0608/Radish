# 神评/沙发功能设计与实现

> Radish 论坛评论高亮功能完整文档
>
> **版本**: v26.1.1
>
> **最后更新**: 2026.01.30

---

## 概述

神评/沙发功能是 Radish 论坛的特色互动功能，通过定时统计评论点赞数，自动标记和展示最受欢迎的评论，提升用户参与度和内容质量。

### 核心特性

- **自动统计**：每天凌晨 1 点自动统计前一天的神评和沙发
- **历史追溯**：保留所有历史记录，支持查看神评/沙发变化趋势
- **追加机制**：点赞数变化时追加新记录，旧记录保留
- **动态置顶**：前端始终显示当前点赞数最高的神评/沙发
- **性能优化**：使用索引、并行加载、错误容错

---

## 核心概念

### 神评（God Comment）

**定义**：每个帖子的父评论（顶级评论）中点赞数最高的评论。

**特点**：
- 统计范围：单个帖子内的所有父评论
- 排序规则：按点赞数降序，点赞数相同时按创建时间降序（最新的在前）
- 显示位置：评论列表顶部（默认排序时）
- 视觉标识：金色"神评"徽章

**业务规则**：
- 一个帖子可以有多个神评（历史记录）
- 每天统计一次，点赞数变化时追加新记录
- 前端始终显示当前点赞数最高的神评置顶

### 沙发（Sofa Comment）

**定义**：每个父评论下的子评论中点赞数最高的评论。

**特点**：
- 统计范围：单个父评论下的所有子评论
- 排序规则：按点赞数降序，点赞数相同时按创建时间降序
- 显示位置：子评论列表顶部（默认排序时）
- 视觉标识：绿色"沙发"徽章

**业务规则**：
- 每个父评论可以有多个沙发（历史记录）
- 每天统计一次，点赞数变化时追加新记录
- 前端始终显示当前点赞数最高的沙发置顶

---

## 实现原理

### 定时统计机制

使用 Hangfire 定时任务框架，每天凌晨 1 点自动执行统计任务。

**执行流程**：

```
1. 查询所有有评论的帖子
   ↓
2. 对每个帖子，查询父评论按点赞数排序
   ↓
3. 检查当前神评与历史记录是否不同
   ↓
4. 如果不同或点赞数变化，追加新记录
   ↓
5. 将之前的记录标记为 IsCurrent = false
   ↓
6. 插入新记录，标记为 IsCurrent = true
   ↓
7. 对每个父评论，重复 2-6 步骤统计沙发
```

**追加机制**：

- **触发条件**：
  - 当前神评/沙发与历史记录的评论 ID 不同
  - 或者点赞数发生变化

- **操作步骤**：
  1. 将之前的 `IsCurrent = true` 记录更新为 `IsCurrent = false`
  2. 插入新记录，设置 `IsCurrent = true`
  3. 保留所有历史记录，不删除

**并列第一名处理**：

如果多个评论点赞数相同，都会被标记为神评/沙发，但 `Rank` 字段会记录排名（1, 2, 3...）。

---

## 数据库设计

### CommentHighlight 表

**表名**：`CommentHighlight`

**字段说明**：

| 字段名 | 类型 | 说明 |
|--------|------|------|
| `Id` | `long` | 主键，雪花 ID |
| `PostId` | `long` | 帖子 ID |
| `CommentId` | `long` | 评论 ID |
| `ParentCommentId` | `long?` | 父评论 ID（神评为 null，沙发为父评论 ID） |
| `HighlightType` | `int` | 高亮类型：1=神评，2=沙发 |
| `StatDate` | `DateTime` | 统计日期（yyyy-MM-dd） |
| `LikeCount` | `int` | 点赞数快照 |
| `Rank` | `int` | 排名（1=第一名） |
| `ContentSnapshot` | `string?` | 评论内容快照（冗余字段，便于历史查询） |
| `AuthorId` | `long` | 作者 ID（冗余字段） |
| `AuthorName` | `string` | 作者名称（冗余字段） |
| `IsCurrent` | `bool` | 是否当前有效（最新一次统计） |
| `TenantId` | `long` | 租户 ID |
| `CreateTime` | `DateTime` | 创建时间 |
| `CreateBy` | `string` | 创建者（固定为 "CommentHighlightJob"） |

**索引设计**：

```csharp
// 说明：SqlSugar 的复合索引特性在本项目当前用法下容易触发 Code First 解析异常，
// 因此这里采用“多个单字段索引”组合来满足查询需求。

[SugarIndex("idx_post_id", nameof(PostId), OrderByType.Asc)]
[SugarIndex("idx_parent_comment_id", nameof(ParentCommentId), OrderByType.Asc)]
[SugarIndex("idx_stat_date", nameof(StatDate), OrderByType.Desc)]
[SugarIndex("idx_comment_id", nameof(CommentId), OrderByType.Asc)]
[SugarIndex("idx_is_current", nameof(IsCurrent), OrderByType.Asc)]
```

**设计要点**：

1. **冗余字段**：`ContentSnapshot`、`AuthorName` 等冗余字段避免 JOIN 查询，提升性能
2. **IsCurrent 标识**：快速查询当前有效的神评/沙发，避免全表扫描
3. **租户隔离**：实现 `ITenantEntity` 接口，自动应用租户过滤器

---

## API 接口

### 1. 获取帖子的当前神评列表

**接口**：`GET /api/v1/CommentHighlight/GetCurrentGodComments`

**参数**：
- `postId` (long): 帖子 ID

**返回**：`List<CommentHighlightVo>`（按排名升序）

**权限**：匿名访问

**示例**：
```http
GET http://localhost:5100/api/v1/CommentHighlight/GetCurrentGodComments?postId=1
```

---

### 2. 获取父评论的当前沙发列表

**接口**：`GET /api/v1/CommentHighlight/GetCurrentSofas`

**参数**：
- `parentCommentId` (long): 父评论 ID

**返回**：`List<CommentHighlightVo>`（按排名升序）

**权限**：匿名访问

**示例**：
```http
GET http://localhost:5100/api/v1/CommentHighlight/GetCurrentSofas?parentCommentId=1
```

---

### 3. 检查评论是否为神评或沙发

**接口**：`GET /api/v1/CommentHighlight/CheckHighlight`

**参数**：
- `commentId` (long): 评论 ID

**返回**：`CommentHighlightVo`（如果是神评/沙发）

**权限**：匿名访问

**示例**：
```http
GET http://localhost:5100/api/v1/CommentHighlight/CheckHighlight?commentId=1
```

---

### 4. 获取评论的历史高亮记录

**接口**：`GET /api/v1/CommentHighlight/GetCommentHistory`

**参数**：
- `commentId` (long): 评论 ID
- `pageIndex` (int): 页码（默认 1）
- `pageSize` (int): 每页数量（默认 20）

**返回**：`PageModel<CommentHighlightVo>`（按统计日期降序）

**权限**：匿名访问

**示例**：
```http
GET http://localhost:5100/api/v1/CommentHighlight/GetCommentHistory?commentId=1&pageIndex=1&pageSize=20
```

---

### 5. 手动触发统计任务

**接口**：`POST /api/v1/CommentHighlight/TriggerStatJob`

**参数**：
- `statDate` (DateTime?): 统计日期（可选，默认昨天）

**返回**：任务 ID

**权限**：Admin 或 System 角色

**示例**：
```http
POST http://localhost:5100/api/v1/CommentHighlight/TriggerStatJob
Authorization: Bearer {token}
```

---

### 6. 获取帖子的神评历史趋势

**接口**：`GET /api/v1/CommentHighlight/GetGodCommentTrend`

**参数**：
- `postId` (long): 帖子 ID
- `days` (int): 查询天数（默认 30 天）

**返回**：`List<CommentHighlightVo>`（按统计日期降序）

**权限**：匿名访问

**示例**：
```http
GET http://localhost:5100/api/v1/CommentHighlight/GetGodCommentTrend?postId=1&days=30
```

---

## 前端集成

### 类型定义

**文件位置**：`radish.client/src/types/forum.ts`

**扩展 CommentNode 接口**：

```typescript
export interface CommentNode {
  // ... 现有字段

  // 神评/沙发标识
  isGodComment?: boolean;  // 是否为神评
  isSofa?: boolean;        // 是否为沙发
  highlightRank?: number;  // 高亮排名（1=第一名）
}
```

**新增 CommentHighlight 接口**：

```typescript
export interface CommentHighlight {
  id: number;
  postId: number;
  commentId: number;
  parentCommentId: number | null;
  highlightType: number;       // 1=神评, 2=沙发
  statDate: string;
  likeCount: number;
  rank: number;
  contentSnapshot: string | null;
  authorId: number;
  authorName: string;
  isCurrent: boolean;
  createTime: string;
}
```

---

### API 服务

**文件位置**：`radish.client/src/api/forum.ts`

**新增函数**：

```typescript
/**
 * 获取帖子的当前神评列表
 */
export async function getCurrentGodComments(
  postId: number,
  t: TFunction
): Promise<CommentHighlight[]>

/**
 * 获取父评论的当前沙发列表
 */
export async function getCurrentSofas(
  parentCommentId: number,
  t: TFunction
): Promise<CommentHighlight[]>
```

---

### 组件改造

#### ForumApp 组件

**文件位置**：`radish.client/src/apps/forum/ForumApp.tsx`

**改造内容**：

1. **加载评论时并行获取神评和沙发标识**：

```typescript
async function loadComments(postId: number) {
  // 并行加载评论树和神评列表
  const [commentsData, godCommentsData] = await Promise.all([
    getCommentTree(postId, sortParam, t),
    getCurrentGodComments(postId, t).catch(() => [])
  ]);

  // 将神评标识合并到评论节点中
  const commentsWithGodHighlight = commentsData.map(comment => {
    const godCommentRecord = godCommentsData.find(gc => gc.commentId === comment.id);
    if (godCommentRecord) {
      return { ...comment, isGodComment: true, highlightRank: godCommentRecord.rank };
    }
    return comment;
  });

  // 为每个父评论加载沙发标识
  const commentsWithAllHighlights = await Promise.all(
    commentsWithGodHighlight.map(async (comment) => {
      if (!comment.children || comment.children.length === 0) {
        return comment;
      }

      const sofasData = await getCurrentSofas(comment.id, t).catch(() => []);
      const childrenWithSofa = comment.children.map(child => {
        const sofaRecord = sofasData.find(s => s.commentId === child.id);
        if (sofaRecord) {
          return { ...child, isSofa: true, highlightRank: sofaRecord.rank };
        }
        return child;
      });

      return { ...comment, children: childrenWithSofa };
    })
  );

  setComments(commentsWithAllHighlights);
}
```

---

#### CommentTree 组件

**文件位置**：`radish.client/src/apps/forum/components/CommentTree.tsx`

**改造内容**：

1. **使用后端返回的神评标识**：

```typescript
// 找出所有神评（后端标记的）
const godComments = useMemo(() => {
  return comments.filter(c => c.isGodComment);
}, [comments]);

// 找出当前点赞数最高的神评（用于置顶显示）
const topGodComment = useMemo(() => {
  if (godComments.length === 0) return null;
  return [...godComments].sort((a, b) => {
    if ((b.likeCount || 0) !== (a.likeCount || 0)) {
      return (b.likeCount || 0) - (a.likeCount || 0);
    }
    return new Date(b.createTime || 0).getTime() - new Date(a.createTime || 0).getTime();
  })[0];
}, [godComments]);
```

2. **置顶逻辑**：

```typescript
const displayComments = useMemo(() => {
  if (comments.length === 0) return [];

  if (sortBy === null && topGodComment) {
    // 默认排序：当前点赞数最高的神评置顶 + 其他按时间升序
    const others = comments.filter(c => c.id !== topGodComment.id);
    return [topGodComment, ...others];
  }

  // 手动排序时，直接使用后端返回的顺序
  return comments;
}, [comments, sortBy, topGodComment]);
```

---

#### CommentNode 组件

**文件位置**：`radish.client/src/apps/forum/components/CommentNode.tsx`

**改造内容**：

1. **使用后端返回的沙发标识**：

```typescript
// 找出所有沙发（后端标记的）
const sofaComments = loadedChildren.filter(c => c.isSofa);

// 找出当前点赞数最高的沙发（用于置顶显示）
const topSofaComment = sofaComments.length > 0
  ? [...sofaComments].sort((a, b) => {
      if ((b.likeCount || 0) !== (a.likeCount || 0)) {
        return (b.likeCount || 0) - (a.likeCount || 0);
      }
      return new Date(b.createTime || 0).getTime() - new Date(a.createTime || 0).getTime();
    })[0]
  : null;
```

2. **显示徽章**：

```tsx
{/* 神评标识（仅父评论） */}
{level === 0 && isGodComment && (
  <span className={styles.godCommentBadge}>神评</span>
)}

{/* 沙发标识（仅子评论） */}
{level === 1 && node.isSofa && (
  <span className={styles.sofaBadge}>沙发</span>
)}
```

---

## 配置说明

### Hangfire 配置

**文件位置**：`Radish.Api/appsettings.json`

**配置项**：

```json
{
  "Hangfire": {
    "CommentHighlight": {
      "Enable": true,
      "Schedule": "0 1 * * *",
      "Description": "神评/沙发统计任务，每天凌晨 1 点执行"
    }
  }
}
```

**Cron 表达式说明**：

- `0 1 * * *`：每天凌晨 1 点执行
- 格式：`分 时 日 月 星期`

**其他时间示例**：
- `0 0 * * *`：每天凌晨 0 点
- `0 2 * * *`：每天凌晨 2 点
- `0 */6 * * *`：每 6 小时执行一次

---

### 任务注册

**文件位置**：`Radish.Api/Program.cs`

**注册代码**：

```csharp
// 神评/沙发统计任务
var commentHighlightConfig = builder.Configuration.GetSection("Hangfire:CommentHighlight");
if (commentHighlightConfig.GetValue<bool>("Enable", true))
{
    var schedule = commentHighlightConfig["Schedule"] ?? "0 1 * * *";

    RecurringJob.AddOrUpdate<CommentHighlightJob>(
        "comment-highlight-stat",
        job => job.ExecuteAsync(null),
        schedule,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    Log.Information("[Hangfire] 已注册定时任务: comment-highlight-stat (计划: {Schedule})", schedule);
}
```

---

## 使用指南

### 启动后端

```bash
dotnet run --project Radish.Api/Radish.Api.csproj
```

### 手动触发统计任务

**方式 1：通过 API**（需要管理员权限）

```bash
POST http://localhost:5100/api/v1/CommentHighlight/TriggerStatJob
Authorization: Bearer {your_admin_token}
```

**方式 2：通过 Hangfire Dashboard**

1. 访问 `http://localhost:5100/hangfire`
2. 找到 `comment-highlight-stat` 任务
3. 点击 "Trigger now" 按钮

### 查看统计结果

**查询神评列表**：

```bash
GET http://localhost:5100/api/v1/CommentHighlight/GetCurrentGodComments?postId=1
```

**查询沙发列表**：

```bash
GET http://localhost:5100/api/v1/CommentHighlight/GetCurrentSofas?parentCommentId=1
```

### 前端测试

1. 启动前端开发服务器：
   ```bash
   npm run dev --workspace=radish.client
   ```

2. 访问论坛应用：`http://localhost:3000`

3. 查看评论区：
   - 神评会显示金色"神评"徽章
   - 沙发会显示绿色"沙发"徽章
   - 默认排序时，神评/沙发会置顶显示

---

## 性能优化

### 数据库优化

1. **索引优化**：
   - 当前实现采用多个单字段索引组合（`PostId`/`ParentCommentId`/`StatDate`/`CommentId`/`IsCurrent`）
   - `IsCurrent` 字段用于快速过滤当前有效记录
   - 若后续需要更强查询性能，可评估在 SqlSugar CodeFirst 之外以迁移脚本方式创建复合索引

2. **冗余字段**：
   - `ContentSnapshot`、`AuthorName` 等冗余字段
   - 避免 JOIN 查询，提升查询性能

3. **批量插入**：
   - 使用 `AddRangeAsync(List<CommentHighlight>)` 批量插入
   - 减少数据库往返次数

### 前端优化

1. **并行加载**：
   - 使用 `Promise.all` 并行加载评论树和神评/沙发列表
   - 减少总加载时间

2. **错误容错**：
   - 神评/沙发加载失败不影响评论显示
   - 使用 `.catch(() => [])` 返回空数组

3. **子评论加载与预览**：
   - 若后端在评论树中不返回 `children` 详情，仅返回 `childrenTotal`，前端会自动预加载第一页子评论用于展示预览
   - 收起态优先展示“沙发”，若没有沙发统计数据则展示当前已加载子评论中的“最热一条”作为预览
   - 展开后支持分页加载更多子评论

### 缓存策略（可选）

**Redis 缓存**：

```csharp
// 缓存键格式
comment:highlight:god:{postId}
comment:highlight:sofa:{parentCommentId}

// TTL: 1 小时
// 在定时任务执行后清除缓存
```

---

## 边界情况处理

### 1. 没有新评论

- 定时任务正常执行，但不会插入新记录
- 日志记录：`没有找到有父评论的帖子`

### 2. 点赞数相同

- 按创建时间降序排序（最新的在前）
- 支持多个并列第一名，都标记为神评/沙发
- `Rank` 字段记录排名（1, 2, 3...）

### 3. 评论被删除

- 定时任务查询时过滤 `IsDeleted = true` 的评论
- 已删除的评论不会被统计为神评/沙发
- 历史记录保留（不删除），但 `IsCurrent` 会被更新为 `false`

### 4. 跨天点赞数变化

- 定时任务每天凌晨 1 点执行，统计前一天的数据
- 如果某个评论的点赞数在统计后继续增长，下次统计时会追加新记录
- 前端始终显示当前点赞数最高的神评/沙发（通过 `IsCurrent = true` 查询）

### 5. 租户隔离

- `CommentHighlight` 表实现 `ITenantEntity` 接口
- 自动应用租户过滤器，确保多租户数据隔离

---

## 未来优化方向

### 1. 实时更新

**目标**：使用 SignalR 推送神评/沙发变化

**实现方案**：
- 当评论点赞数变化时，实时推送给前端
- 前端动态更新神评/沙发标识
- 无需等待定时任务执行

### 2. 缓存优化

**目标**：使用 Redis 缓存当前神评/沙发列表

**实现方案**：
- 缓存键：`comment:highlight:god:{postId}`
- TTL：1 小时
- 在定时任务执行后清除缓存

### 3. 趋势分析

**目标**：提供神评趋势图表

**实现方案**：
- 前端使用 Chart.js 或 ECharts
- 展示神评点赞数随时间的变化
- 支持导出数据

### 4. 通知功能

**目标**：评论成为神评/沙发时通知作者

**实现方案**：
- 在定时任务中检测新的神评/沙发
- 发送站内通知或邮件通知
- 提升用户参与度

### 5. 排行榜

**目标**：全站神评/沙发排行榜

**实现方案**：
- 统计用户获得神评/沙发的次数
- 展示"神评达人"排行榜
- 增加游戏化元素

### 6. 数据归档

**目标**：定期归档历史数据，减少表大小

**实现方案**：
- 保留最近 1 年的历史记录
- 超过 1 年的数据归档到历史表
- 提升查询性能

---

## 技术栈

### 后端

- **框架**：ASP.NET Core 10
- **ORM**：SqlSugar
- **定时任务**：Hangfire
- **数据库**：PostgreSQL / SQLite
- **日志**：Serilog

### 前端

- **框架**：React 19 + TypeScript
- **构建工具**：Vite (Rolldown bundler)
- **组件库**：@radish/ui
- **样式**：CSS Modules

---

## 相关文件

### 后端文件

- **实体类**：`Radish.Model/CommentHighlight.cs`
- **视图模型**：`Radish.Model/ViewModels/CommentHighlightVo.cs`
- **定时任务**：`Radish.Service/Jobs/CommentHighlightJob.cs`
- **控制器**：`Radish.Api/Controllers/CommentHighlightController.cs`
- **AutoMapper**：`Radish.Extension/AutoMapperExtension/CustomProfiles/ForumProfile.cs`
- **配置文件**：`Radish.Api/appsettings.json`
- **任务注册**：`Radish.Api/Program.cs`
- **测试文件**：`Radish.Api.Tests/HttpTest/Radish.Api.CommentHighlight.http`

### 前端文件

- **类型定义**：`radish.client/src/types/forum.ts`
- **API 服务**：`radish.client/src/api/forum.ts`
- **主应用**：`radish.client/src/apps/forum/ForumApp.tsx`
- **评论树**：`radish.client/src/apps/forum/components/CommentTree.tsx`
- **评论节点**：`radish.client/src/apps/forum/components/CommentNode.tsx`

---

## 总结

神评/沙发功能通过定时统计和动态展示，提升了论坛的互动性和内容质量。该功能具有以下优势：

1. **自动化**：无需人工干预，定时自动统计
2. **可追溯**：保留完整历史记录，支持趋势分析
3. **高性能**：使用索引、并行加载、错误容错
4. **用户友好**：视觉标识醒目，置顶逻辑合理
5. **可扩展**：支持实时更新、缓存优化、通知功能等未来扩展

该功能已在 Radish 论坛中成功实现并投入使用。
