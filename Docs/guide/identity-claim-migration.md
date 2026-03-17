# 身份语义收敛迁移计划

> 本文是《[身份语义收敛与 Claim 治理设计](/architecture/identity-claim-convergence)》的执行版，定义 Radish 如何从“分散解析 Claim”迁移到“统一当前用户上下文”。

## 1. 迁移目标

本次迁移要解决的问题不是单个控制器或中间件的漏网之鱼，而是以下结构性问题：

- 运行时代码直接依赖 `ClaimsPrincipal`
- Claim 名字符串在多处硬编码
- 历史兼容逻辑散落在多个入口
- `IHttpContextUser` 仍暴露原始读取能力
- 新增代码没有强约束，容易回归

迁移完成后，目标状态为：

- 运行时代码统一只依赖 `CurrentUser` / `ICurrentUserAccessor`
- Claim 标准化只保留在唯一转换点
- Auth 保留协议边界职责，业务层不再理解 Claim 细节
- 仓库规则阻止未来回归

## 2. 范围

### 2.1 本次纳入范围

- `Radish.Api`
- `Radish.Auth`
- `Radish.Extension`
- `Radish.Common`
- `Radish.Gateway`
- 文档、规划、回归规则

### 2.2 不在本次范围

- 前端 OIDC 客户端实现重写
- OpenIddict 流程本身变更
- 权限模型重新设计
- 第三方客户端接入模型变化

## 3. 迁移原则

1. **先建抽象，再迁移调用，再删兼容。**
2. **协议边界保留，运行时代码收敛。**
3. **兼容输入可以保留，兼容输出逐步清退。**
4. **所有迁移都要可扫描、可回归、可回滚。**

## 4. 分阶段实施

### 4.1 Phase 0：文档与规则先行

目标：先把唯一真相源建立起来，避免后续实施过程中口径再次漂移。

工作项：

- 新增架构设计文档：`/architecture/identity-claim-convergence`
- 新增迁移计划文档：`/guide/identity-claim-migration`
- 在 `framework`、`specifications`、`authentication` 中补充入口说明
- 在 `development-plan` 中提升为当前最高优先级任务
- 在 `changelog` 记录专项立项

验收：

- 文档应用能直接导航到相关设计与迁移文档
- 规划与日志已经显式反映该专项的优先级

### 4.2 Phase 1：建立唯一身份抽象

目标：引入新的目标模型，但暂不一次性清掉所有旧接口。

建议新增：

- `CurrentUser`
- `ICurrentUserAccessor`
- `IClaimsPrincipalNormalizer`
- `UserClaimTypes`
- `SystemRoles`
- `SystemScopes`

建议改造：

- `HttpContextUser` 逐步转为 `CurrentUser` 的兼容外观
- `UserClaimReader` 下沉为标准化实现细节，而不是公开的全局能力中心

验收：

- 运行时已有一个统一、可注入的“当前用户语义对象”
- 常见业务逻辑无需再直接依赖 Claim 名

### 4.3 Phase 2：迁移所有运行时入口

目标：把运行时代码从原始 Claim 解析迁移到统一上下文。

优先迁移对象：

1. Controller
2. SignalR Hub
3. Middleware
4. Authorization Helper / Filter
5. 宿主内的日志与策略辅助逻辑

具体要求：

- 禁止新增 `FindFirst/FindAll/User.IsInRole`
- 统一改用 `ICurrentUserAccessor.Current`
- 角色判断改为 `CurrentUser.IsInRole(...)`
- Scope 判断改为 `CurrentUser.HasScope(...)`

验收：

- 非协议边界运行时代码的 Claim 直接读取清零

### 4.4 Phase 3：收缩旧逃逸接口

目标：防止虽然迁移了入口，但团队仍能从公共接口绕回去手工解析 Claim。

需要处理的接口：

- `IHttpContextUser.GetClaimsIdentity()`
- `IHttpContextUser.GetClaimValueByType(string)`
- `IHttpContextUser.GetUserInfoFromToken(string)`

建议策略：

- 第一步：标记 `[Obsolete("禁止新增使用，请改用 CurrentUser / ICurrentUserAccessor")]`
- 第二步：仓库内调用清零
- 第三步：从接口移除，或降为仅内部实现可见

验收：

- 仓库内不再有新代码依赖这 3 个方法
- 团队无法轻易绕开统一身份抽象

### 4.5 Phase 4：协议输出收敛

目标：逐步停止历史 Claim 的双写，让系统最终只围绕标准 Claim 运转。

需要处理：

- `Radish.Auth` 签发时停止继续扩散 `ClaimTypes.NameIdentifier`、`ClaimTypes.Role` 等历史输出
- `TenantId` 作为历史兼容输入继续读一段时间，但不再作为新 Token 输出
- `UserInfo` 响应仍兼容现有协议字段，但内部读取统一走常量与标准化口径

前提：

- 所有运行时代码已经完成迁移
- 外部客户端不再依赖历史输出 Claim

验收：

- 新签发 Token 的主口径只剩 OIDC 标准 Claim + `tenant_id`

### 4.6 Phase 5：M12 后续的防回归落地

目标：在 M12 主体治理收束后，再从机制上防止未来重新回到“边写边补漏”。

实施内容：

- 为运行时代码整理 `rg` 静态扫描规则与白名单口径
- 在进入 M13（验证基线与回归资产工程化）后，再补为仓库脚本
- 视项目阶段决定接入本地校验或后续 CI/CD 流水线

验收：

- M13 启动后，新增分散 Claim 解析能够被脚本或统一校验入口自动拦截

## 5. 文件级改造建议

### 5.1 新增文件

- `Radish.Common/HttpContextTool/CurrentUser.cs`
- `Radish.Common/HttpContextTool/ICurrentUserAccessor.cs`
- `Radish.Common/HttpContextTool/ClaimsPrincipalNormalizer.cs`
- `Radish.Common/HttpContextTool/IClaimsPrincipalNormalizer.cs`
- `Radish.Common/HttpContextTool/UserClaimTypes.cs`
- `Radish.Common/HttpContextTool/UserRoles.cs`
- `Radish.Common/HttpContextTool/UserScopes.cs`
- `Radish.Common/HttpContextTool/AuthorizationPolicies.cs`

### 5.2 重点改造文件

- `Radish.Common/HttpContextTool/IHttpContextUser.cs`
- `Radish.Common/HttpContextTool/HttpContextUser.cs`
- `Radish.Common/HttpContextTool/UserClaimReader.cs`
- `Radish.Api/Program.cs`
- `Radish.Api/Filters/HangfireAuthorizationFilter.cs`
- `Radish.Extension/AuditLogExtension/AuditLogMiddleware.cs`
- `Radish.Extension/PermissionExtension/PermissionRequirementHandler.cs`
- `Radish.Api/Hubs/ChatHub.cs`
- `Radish.Api/Hubs/NotificationHub.cs`
- `Radish.Auth/Controllers/AccountController.cs`
- `Radish.Auth/Controllers/AuthorizationController.cs`
- `Radish.Auth/Controllers/UserInfoController.cs`

## 6. 推荐迁移顺序

建议按以下顺序一次性推进，而不是零散补点：

1. 新增 `CurrentUser` / `ICurrentUserAccessor` / 常量 / Normalizer
2. 让 `HttpContextUser` 内部改为复用 `CurrentUser`
3. 批量迁移 Api/Extension/Common 的运行时代码
4. 给旧接口加 `[Obsolete]`
5. 批量替换协议边界的字符串为统一常量
6. 加扫描脚本与测试
7. 完成后再评估去掉历史双写 Claim

## 7. 验证清单

### 7.1 编译与测试

- `dotnet build Radish.Api/Radish.Api.csproj -c Debug`
- `dotnet build Radish.Auth/Radish.Auth.csproj -c Debug`
- `dotnet test Radish.Api.Tests`

### 7.2 静态扫描

重点确认以下目录中，非白名单文件不再命中：

- `FindFirst(`
- `FindAll(`
- `ClaimTypes.`
- `IsInRole(`
- `"sub"` / `"jti"` / `"tenant_id"` / `"TenantId"` / `"role"` / `"scope"`

### 7.3 行为验证

- 有认证主体时，`CurrentUser` 能正确读取 `UserId/UserName/TenantId/Roles/Scopes`
- 仅有 Bearer Token、无认证主体时，兼容回退仍正确
- `System/Admin` 角色判断行为不变
- `Client` 策略的 `radish-api` scope 判断行为不变
- 审计日志与 Hub 上下文解析口径不变

## 8. 回滚策略

若迁移中途发现兼容问题，按以下方式回滚：

1. 保留 `CurrentUser` 抽象与常量，不回滚文档与规则
2. 将受影响入口暂时切回 `HttpContextUser` 兼容外观
3. 不立即进入 Phase 4，不停止双写历史 Claim
4. 修复兼容问题后再继续推进 Phase 2/3

这样可以保证“架构方向不退回”，但允许代码实施分批回滚。

## 9. 完成定义（Definition of Done）

满足以下条件，才算专项完成：

1. `CurrentUser` 成为运行时唯一身份视图
2. 统一标准化组件成为 Claim 兼容逻辑唯一入口
3. `IHttpContextUser` 原始逃逸接口已废弃或移除
4. 运行时代码的直接 Claim 解析清零
5. 协议边界完成统一常量化
6. M12 后续阶段已具备可执行的防回归扫描机制

## 10. 当前优先级说明

自 **2026-03-07** 起，本专项提升为 **M12 当前最高优先级 P0 工程治理任务**，优先级高于新增功能扩张。

原因如下：

- 它直接影响权限、租户、审计、Hub、控制器、Filter 等横切能力的稳定性
- 若不先治理身份语义层，后续社区功能越多，修补成本越高
- 该专项完成后，才能真正为权限治理、Console 扩展、Gateway/BFF 演进建立稳定基线

## 11. 当前实施状态（截至 2026-03-07）

### 11.1 阶段状态

- **Phase 0：文档与规则先行** —— 已完成。
- **Phase 1：建立唯一身份抽象** —— 已完成。
- **Phase 2：迁移所有运行时入口** —— 主路径已完成。
- **Phase 3：收缩旧逃逸接口** —— 已完成到“兼容层冻结”状态。
- **Phase 4：协议输出收敛** —— 暂未启动，需等待外部客户端兼容性确认。
- **Phase 5：M12 后续防回归落地** —— 规则已形成，计划在 M13（验证基线与回归资产工程化）阶段接入脚本/流水线。

### 11.2 当前兼容层最终边界

截至当前版本，兼容层只允许保留以下形态：

- `Radish.Api/Program.cs` 中的 `IHttpContextUser` 兼容注册。
- `App.HttpContextUser` 兼容属性（已标记 `[Obsolete]`，禁止新增调用）。
- `IHttpContextUser` 与 `HttpContextUserCompatibilityExtensions` 的历史兼容方法（均已标记 `[Obsolete]`）。
- `IHttpContextUser.GetToken()` 仅用于历史 Bearer Token 直取回退场景，不再作为新代码入口。

### 11.3 当前运行时收敛结果

- 控制器、Hub、中间件、Filter、Repository、Service、Infrastructure 已优先走 `CurrentUser` / `ICurrentUserAccessor` / `App.CurrentUser`。
- `AuditLogMiddleware`、`PermissionRequirementHandler`、`ChatHub`、`NotificationHub` 等横切入口已完成统一口径治理。
- 运行时角色、Scope、策略字符串已经收口到 `UserRoles`、`UserScopes`、`AuthorizationPolicies`。
- 非协议边界剩余的 Claim 直接读取，仅存在于 `ClaimsPrincipalNormalizer` / `UserClaimReader` 这类标准化组件内部。

### 11.4 下一步建议

1. `M13` 首轮已落地 `npm run check:identity-claims`，并接入根目录 `validate:baseline` / `validate:baseline:quick`；后续再视项目阶段决定是否继续上升到 CI/CD。
2. 在确认无外部依赖后，评估移除 `IHttpContextUser` 兼容层。
3. 清理遗留注释、示例与文档中的旧 JWT/Claim 口径，避免回归。
