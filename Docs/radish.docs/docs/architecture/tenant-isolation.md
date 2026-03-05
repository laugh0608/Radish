# 多租户隔离实施与审计

## 目标

- 防止跨租户读写（尤其是公共租户读取到其他租户数据）。
- 降低“每个查询手写 TenantId 条件”的维护成本。
- 将隔离策略固化为统一仓储规则 + 实体分层策略。

## 当前实现

### 1. 全局过滤（QueryFilter）

- `RepositorySetting.SetTenantEntityFilter` 统一配置 `ITenantEntity` 查询过滤：
  - `tenantId > 0`：可见当前租户 + 公共租户（`TenantId=0`）。
  - `tenantId <= 0`：仅可见公共租户（`TenantId=0`）。

### 2. 仓储统一作用域（BaseRepository）

- 读操作统一走 `CreateTenantQueryable()` / `CreateTenantQueryableFor<T>()`。
- 写操作统一走 `NormalizeEntityTenantId()`：
  - 有租户上下文时强制归一到当前租户。
  - 无租户上下文默认只允许公共租户写入。
  - 无租户上下文写入指定租户（`TenantId>0`）仅允许 `System/Admin` 或后台任务。
- 三表联查 `QueryMuchAsync<T,T2,T3,...>` 已接入自动租户过滤（仅对实现 `ITenantEntity` 的参与实体生效）。

### 3. 实体隔离策略

- 默认：字段隔离（`ITenantEntity`）。
- 大流量时序数据：按需演进到分表（`[MultiTenant(Tables)]`）。
- 合规/超大租户：按需演进到分库（`[MultiTenant(DataBases)]`）。

## 本轮补齐范围

- 已补齐 `ITenantEntity`：
  - `UserBalance`、`UserBenefit`、`UserInventory`
  - `UserExperience`、`UserExpDailyStats`
  - `CoinTransaction`、`ExpTransaction`
  - `BalanceChangeLog`
- 已收口高风险链路：
  - 用户提及、附件、商城订单/商品、排行榜聚合查询。

## 审计扫描（2026-03-05）

### 扫描范围

- Model：`TenantId` 字段与 `ITenantEntity` 一致性。
- Repository：是否存在绕过统一租户作用域的直接查询入口。
- Service：是否存在直接 `Db.Queryable` 访问或明显越权路径。
- Controller：匿名入口与租户参数来源。

### 结论

- 高风险遗漏：未发现。
- `TenantId` 字段实体与 `ITenantEntity` 接口：一致。
- 自定义仓储查询入口：已统一到 `BaseRepository` 作用域。
- Service 层未发现直接 `Db.Queryable` 访问。

### 持续优化项（非阻断）

1. Service 层仍有少量“防御性 TenantId 条件”（与仓储规则重复），可逐步精简以降低复杂度。  
2. `NotificationService` 仍保留 `dto.TenantId` 传入能力，建议后续补“仅系统调用可指定租户”的显式约束。  
3. 后续若出现 4 表以上联查，可将 `QueryMuchAsync` 扩展为更通用的租户作用域构造器。

### 待评估实体（策略层面）

- 当前仍有部分行为类实体未采用 `ITenantEntity`，以“业务外键 + 全局雪花 ID”间接隔离：
  - `Reaction`、`UserPostLike`、`UserCommentLike`
  - `UploadSession`、`UserPaymentPassword`
- 现状不是直接漏洞，但若后续需要更强审计或严格租户级统计，建议补充 `TenantId` 并迁移到字段隔离模型。

## 删库重建后的验收清单

1. 公共租户账号仅能查询到 `TenantId=0` 数据。  
2. 租户 A 账号无法查询到租户 B 私有数据。  
3. 用户提及/附件/商城/排行榜各做一次跨租户冒烟。  
4. 普通用户在无租户上下文下写 `TenantId>0` 应被拒绝。  
5. `System/Admin` 或后台任务可执行指定租户写入（用于系统修复与批任务）。  
