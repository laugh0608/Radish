# WOG-5 管理覆盖类写入版本语义方案评审

> 日期：`2026-06-20`（Asia/Shanghai）
>
> 状态：`待确认 / 进入代码前评审`
>
> 关联说明：[写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)、[WOG-1 写操作分级盘点记录](/records/wog-1-write-operation-inventory-2026-06-20)、[WOG-4 奖励业务键唯一性方案](/records/wog-4-reward-business-key-uniqueness-plan-2026-06-20)、[系统设置治理专题](/guide/system-settings-governance)

## 目标

`WOG-5` 聚焦管理类写入的“后提交覆盖前提交”问题。首批只评审当前已经进入 Console 主路径、且已有审计或版本字段基础的对象：

- 系统设置更新 / 恢复默认。
- Console 角色授权保存。
- 商品编辑、上架、下架。
- 内容举报审核。

本批目标不是把所有管理表统一改造为版本表，也不是新增通用审批平台，而是让高价值管理写入口在管理员打开旧页面、他人已经保存新状态后，能明确返回冲突并要求刷新；审计历史需要能说明提交基于哪个版本或状态。

## 当前代码事实

### 1. 系统设置

`SystemConfigService.UpdateConfigAsync` 当前流程为：

1. 按设置定义 ID / 覆盖记录 ID 找到 `SystemConfigDefinition`。
2. 读取 `SystemConfigRecord` 覆盖值。
3. 校验风险等级、原因、确认参数和设置值。
4. 创建、更新或删除 JSON 覆盖记录。
5. 写入系统设置专用变更历史。

现有缺口：

- [系统设置治理专题](/guide/system-settings-governance) 已把 `Version` 写入推荐字段，但 `SystemConfigRecord`、`SystemConfigVo`、`UpdateSystemConfigDto` 和 `RestoreSystemConfigDefaultDto` 当前没有版本字段。
- JSON 仓储通过进程内 `SemaphoreSlim` 串行读写单个文件，但服务层是先读后写；并发请求之间没有“基于旧版本提交”的校验。
- Console 只提交新值、原因和确认参数，不提交期望版本。

### 2. Console 角色授权

`ConsoleAuthorizationService.GetRoleAuthorizationAsync` 已返回 `VoLastModifyTime`，前端 `buildRoleAuthorizationSavePayload` 已把它作为 `expectedModifyTime` 提交到 `SaveRoleAuthorization`。

现有缺口：

- `SaveRoleAuthorizationAsync` 当前没有校验 `ExpectedModifyTime`，仍会按旧页面提交的资源集合全量覆盖角色授权。
- 授权保存会对 `RoleConsoleResource` 做软删除、恢复和新增，但恢复已有软删除记录时未明确更新同一批操作时间；`VoLastModifyTime` 不能稳定表达每次保存后的快照版本。
- 该入口最适合首批落地，因为前端契约字段已经存在，服务端缺的是版本判断和变更时间维护。

### 3. 商品管理

`Product` 实体已有 `Version` 字段，库存扣减 `DeductStockAsync` 已使用 `Version` 条件更新。

现有缺口：

- `ProductVo` 和 `UpdateProductDto` 没有暴露 / 提交版本。
- `UpdateProductAsync` 读取实体后 `Mapper.Map` 并直接 `UpdateAsync`，会覆盖他人刚修改的价格、库存、权益配置、上下架标记等字段。
- `PutOnSaleAsync` / `TakeOffSaleAsync` 当前只按 `Id + TenantId` 条件更新，不检查管理员打开页面时看到的商品版本。

### 4. 内容审核

`ContentModerationService.ReviewReportAsync` 当前先读取举报单，确认 `Status == Pending` 后更新为 `Approved` 或 `Rejected`，再按需要创建禁言 / 封禁动作。

现有缺口：

- 读取 `Pending` 后直接 `UpdateAsync(report)`，并发审核可能在状态检查之后发生覆盖。
- `ReviewContentReportDto` 没有期望状态、期望版本或期望修改时间。
- `ContentReport` 没有版本字段；但举报审核是单向状态迁移，首批可用 `Status == Pending` 条件更新表达版本语义，不必先给整张表加通用版本列。
- `UserModerationAction` 当前可记录禁言 / 封禁 / 解除动作并停用旧动作；活跃动作唯一性和手动治理并发属于后续治理项，不建议并入 WOG-5 首批。

## 建议决策

### 决策 1：首批只治理真实覆盖写入

建议首批纳入：

| 范围 | 首批动作 | 理由 |
| --- | --- | --- |
| 系统设置 | 增加设置覆盖版本，并要求更新 / 恢复默认提交期望版本 | 文档已经定义版本口径，Console 设置页是管理主路径 |
| Console 角色授权 | 使用已有 `ExpectedModifyTime` 做旧快照冲突判断 | 前端已提交字段，服务端可较小范围补齐 |
| 商品管理 | 复用 `Product.Version`，编辑 / 上下架要求期望版本 | 实体已有版本字段，当前管理编辑没有用起来 |
| 内容举报审核 | 使用 `Pending` 状态条件更新，重复审核返回冲突 | 审核是单向状态迁移，条件更新比新增通用版本列更贴合当前模型 |

建议首批不纳入：

- 管理员调账、订单退款、用户账号高危字段、开放 API 应用密钥和 OIDC 授权对象。
- `UserModerationAction` 活跃动作唯一约束和手动治理动作版本。
- 商品分类、标签、贴纸、Wiki 文档权限等其他 Console 编辑页。

这些对象后续可按 WOG-1 矩阵继续筛选，但不应在 WOG-5 首批里扩大为“所有管理写入统一平台”。

### 决策 2：按对象选择版本真值

| 范围 | 版本真值 | 客户端提交 | 冲突判定 |
| --- | --- | --- | --- |
| 系统设置覆盖值 | `SystemConfigRecord.Version` | `expectedVersion` | 当前是否覆盖 + 当前版本必须等于期望版本 |
| Console 角色授权 | 当前生效授权集合的 `VoLastModifyTime` | `expectedModifyTime` | 当前生效授权最后修改时间必须等于期望值 |
| 商品管理 | `Product.Version` | `expectedVersion` | 更新条件包含 `Id + TenantId + Version` |
| 内容举报审核 | `ContentReport.Status` | 可不新增字段，服务端条件更新 `Pending` | 仅 `Pending` 能转为终态，受影响行数为 0 即冲突 |

系统设置不建议用 `ModifyTime` 作为主要版本真值，因为 JSON 存储时间精度和序列化格式更容易造成比较误差；使用整数 `Version` 更稳定。

Console 授权暂不新增角色授权版本表。现有前端已经传 `ExpectedModifyTime`，首批可先把该字段变成有效保护；如后续需要更强语义，再评审 `RoleAuthorizationSnapshotVersion` 或角色级授权版本字段。

内容审核不建议首批新增 `ContentReport.Version`。举报单审核只有 `Pending -> Approved / Rejected` 的一次性终态迁移，条件更新足以表达“旧状态不能覆盖新状态”。

### 决策 3：冲突响应必须是业务响应

服务层应把版本冲突作为明确业务冲突，而不是静默失败或写普通错误日志。

建议响应语义：

- 系统设置：`系统设置已被其他管理员修改，请刷新后重试`。
- Console 授权：`角色授权已被其他管理员修改，请刷新后重试`。
- 商品管理：`商品信息已被其他管理员修改，请刷新后重试`。
- 内容审核：`举报单已被处理，请刷新审核队列`。

如果当前项目没有统一 `409 Conflict` 返回封装，首批可以先用现有业务异常路径承接；但文档和测试应明确这是并发冲突，不是参数错误或权限不足。

## 方案细节

### 1. 系统设置版本

建议新增字段：

- `SystemConfigRecord.Version`：`int`，默认 `1`；已有 JSON 覆盖记录加载时若缺失或小于 `1`，按 `1` 处理。
- `SystemConfigVo.VoVersion`：当前覆盖记录版本；没有覆盖值时返回 `0`。
- `UpdateSystemConfigDto.ExpectedVersion`：设置页保存覆盖值时提交。
- `RestoreSystemConfigDefaultDto.ExpectedVersion`：恢复默认时提交。

写入规则：

1. 如果当前没有覆盖记录，客户端必须提交 `expectedVersion = 0`。
2. 如果当前已有覆盖记录，客户端提交的 `expectedVersion` 必须等于当前记录 `Version`。
3. 创建覆盖记录时写 `Version = 1`。
4. 更新覆盖记录时写 `Version = current.Version + 1`。
5. 恢复默认时只允许期望版本匹配；恢复后没有覆盖记录，下一次详情返回 `VoVersion = 0`。
6. 审计历史建议新增 `ExpectedVersion`、`BeforeVersion`、`AfterVersion` 字段；若为控制范围，首批至少在日志文本或记录字段中保留版本上下文。

迁移边界：

- `SystemConfigRecord` 当前由 JSON 文件持久化，不需要 PostgreSQL / SQLite 表结构迁移。
- 已存在的 `system-configs.json` 记录兼容读取，缺失 `Version` 时视为 `1`。
- 保存后使用新 JSON 字段落盘，保持 UTF-8 可读文本。

### 2. Console 角色授权

建议沿用已有 DTO：

- `SaveRoleAuthorizationDto.ExpectedModifyTime`
- `RoleAuthorizationSnapshotVo.VoLastModifyTime`

写入规则：

1. 保存前重新计算当前角色授权的 `latestGrantTime`。
2. 当前 `latestGrantTime` 与 `ExpectedModifyTime` 不一致时拒绝保存。
3. 如果当前没有任何授权且客户端也传 `null`，允许保存。
4. 本次保存发生软删除、恢复或新增时，所有变更行都写同一个 `operationTime` 到 `ModifyTime`。
5. 保存成功后前端应重新拉取授权快照，不依赖本地旧快照继续二次保存。

注意事项：

- 只比较有效授权集合的最后修改时间，不比较 `Role` 自身名称或启用状态。
- 如果未来角色基础信息编辑也需要版本语义，应在角色编辑专题处理，不和授权集合混在一起。
- 如果恢复软删除授权记录不更新时间，`VoLastModifyTime` 会退回历史值，必须同步修正。

### 3. 商品管理版本

建议新增 / 暴露字段：

- `ProductVo.VoVersion`
- `UpdateProductDto.ExpectedVersion`
- 上架 / 下架请求 DTO：`ProductStatusChangeDto { ProductId, ExpectedVersion }`，或等价接口参数。

写入规则：

1. 商品编辑时读取当前商品，校验基础配置后，使用 `Id + TenantId + Version == ExpectedVersion` 条件更新。
2. 更新成功时 `Version = Version + 1`，并写 `ModifyTime / ModifyBy / ModifyId`。
3. 上架 / 下架也必须检查 `ExpectedVersion`，成功后递增版本。
4. 库存扣减已使用 `Version`，保持现有资产保护逻辑；管理编辑不要绕过库存版本。
5. 如果商品被删除、版本不匹配或租户不匹配，返回明确失败语义。

首批不要求新增商品编辑历史表；如果后续需要追踪价格、库存、权益配置历史，再单独评审商品审计日志。

### 4. 内容举报审核条件更新

建议首批不加 `ContentReport.Version`，改为状态条件更新：

1. `ReviewReportAsync` 读取举报单用于校验和构造动作。
2. 提交审核时用 `UpdateColumnsAsync` 将 `Status` 从 `Pending` 改为 `Approved` / `Rejected`，条件包含 `Id + TenantId + Status == Pending + !IsDeleted`。
3. 受影响行数为 `0` 时，返回“举报单已被处理，请刷新审核队列”。
4. 审核通过且需要禁言 / 封禁时，举报单状态更新与治理动作创建应在同一事务尝试中完成。
5. 如事务内治理动作创建失败，举报单不应留下已通过但动作未创建的半成品状态。

手动治理动作首批保持现状，只补记录和测试说明；活跃禁言 / 封禁唯一约束、取消动作并发和来源举报单幂等可作为后续治理候选。

## 测试入口

代码批次确认后建议新增或补充以下测试：

1. `SystemConfigServiceTest`
   - 更新覆盖值时 `expectedVersion` 匹配才成功，并返回递增后的 `VoVersion`。
   - 旧版本保存返回冲突，不写覆盖值，不写审计历史。
   - 默认态 `expectedVersion = 0` 可创建覆盖值。
   - 恢复默认时版本不匹配返回冲突。
2. `ConsoleAuthorizationServiceTest`
   - `ExpectedModifyTime` 与当前授权快照不一致时拒绝保存。
   - 保存授权时软删除、恢复、新增链接都更新同一批 `ModifyTime`。
   - 保存成功后重新获取快照能拿到新的 `VoLastModifyTime`。
3. `ProductServiceTest`
   - 商品编辑版本匹配时递增 `Version`。
   - 商品编辑旧版本不覆盖价格、库存、上下架状态。
   - 上架 / 下架旧版本返回冲突。
4. `ContentModerationServiceTest`
   - 举报单从 `Pending` 审核成功。
   - 已处理举报单重复审核返回冲突。
   - 并发状态条件更新受影响行数为 `0` 时不创建治理动作。
   - 审核通过并创建治理动作保持同一事务边界。

验证命令建议：

1. `dotnet test Radish.Api.Tests --filter "FullyQualifiedName~SystemConfigServiceTest|FullyQualifiedName~ConsoleAuthorizationServiceTest|FullyQualifiedName~ProductServiceTest|FullyQualifiedName~ContentModerationServiceTest"`
2. `dotnet test Radish.Api.Tests`
3. `dotnet build Radish.slnx -c Debug`
4. `npm run build --workspace=radish.console`
5. `git diff --check`
6. `npm run check:repo-hygiene:changed`

如果只完成后端服务契约而没有修改 Console 页面，可暂不执行真实浏览器 smoke；如果 Console 设置页、角色授权页或商品页需要显示冲突提示，则补 `radish.console` 构建和必要的页面级复核。

## 不做范围

- 不新增通用审批流、发布工作流、草稿系统、双人复核、完整操作回放或通用配置中心。
- 不把所有 Console CRUD 页面一次性加版本。
- 不开放 High / Critical 系统设置编辑。
- 不把部署密钥、数据库连接、证书、OIDC 密钥、会话安全策略、高危资产设置或宠物经济数值搬进 Console。
- 不治理管理员调账、订单退款、用户账号高危字段和开放 API 应用密钥。
- 不把 Redis 锁、Outbox 或分布式事务作为本批前置。
- 不改变商品库存扣减、订单购买、权益发放和 WOG-3 / WOG-4 已落地的业务键与幂等口径。

## 待确认决策

1. 系统设置首批是否采用整数 `Version` 作为唯一版本真值，默认态返回 `VoVersion = 0`。
2. Console 角色授权首批是否沿用已有 `ExpectedModifyTime`，不新增授权版本表。
3. 商品管理首批是否复用 `Product.Version`，并把编辑 / 上架 / 下架都纳入版本检查。
4. 内容举报审核首批是否只使用 `Pending` 状态条件更新，不新增 `ContentReport.Version`。
