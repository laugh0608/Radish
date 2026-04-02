# 身份语义 Phase 4 启动前提确认

> 本文用于回答一个具体问题：**身份语义收敛 Phase 4（协议输出收敛）现在能不能启动？**
>
> 关联文档：
>
> - [身份语义收敛与 Claim 治理设计](/architecture/identity-claim-convergence)
> - [身份语义收敛迁移计划](/guide/identity-claim-migration)
> - [身份语义 Phase 4 协议消费者矩阵](/guide/identity-claim-protocol-consumers)
> - [身份语义 Phase 4 历史 Claim 保留矩阵](/guide/identity-claim-retention-matrix)
> - [身份语义 Phase 4 实施与回滚窗口](/guide/identity-claim-phase4-rollout-window)
> - [身份语义 Phase 4 最终启动评审](/guide/identity-claim-phase4-start-review)
> - [鉴权与授权指南](/guide/authentication)
> - [当前进行中](/planning/current)
> - [开发路线图总览](/development-plan)

## 1. 目标

Phase 4 的目标不是继续清理运行时代码里的 `FindFirst(...)`，而是正式进入 **协议输出收敛**：

- `Radish.Auth` 停止继续双写历史 Claim 输出；
- 新签发 Token 只保留 OIDC 标准 Claim 与当前仍明确需要的兼容字段；
- `userinfo`、客户端初始化种子与资源服务器验证口径全部围绕统一常量和标准语义运转；
- 仓库内外对“哪些 Claim 仍承诺输出”有同一份事实清单。

换句话说，Phase 4 关心的是 **协议承诺**，不是单纯的运行时代码收口。

## 2. 当前事实基线（2026-04-02）

### 2.1 已满足的前提

以下条件当前已经基本满足：

1. **运行时代码主路径已完成收敛**
   - 运行时代码默认已改为 `CurrentUser` / `ICurrentUserAccessor` / `App.CurrentUser`。
   - 非协议边界的直接 Claim 解析，当前主要只剩标准化组件内部和受控兼容层。

2. **兼容层已经冻结**
   - `IHttpContextUser` 的历史逃逸接口已标记 `[Obsolete]`。
   - `App.HttpContextUser` 仅保留兼容职责，不再作为新代码入口。

3. **防回归扫描已有最小入口**
   - `npm run check:identity-claims` 已存在。
   - `validate:baseline` / `validate:baseline:quick` 已接入身份语义扫描。

### 2.2 当前仍保留的协议边界

根据当前代码与文档，以下位置仍属于 Phase 4 的直接改造面：

- `Radish.Auth/Controllers/AccountController.cs`
- `Radish.Auth/Controllers/AuthorizationController.cs`
- `Radish.Auth/Controllers/UserInfoController.cs`
- `Radish.Auth/OpenIddict/OpenIddictSeedHostedService.cs`
- `Radish.Api/Program.cs`
- `Radish.Common/HttpContextTool/UserClaimReader.cs`
- `Radish.Common/HttpContextTool/UserClaimTypes.cs`

这说明：**运行时已基本收口，但协议输出边界仍明确存在历史 Claim 双写和兼容读取。**

## 3. 当前兼容边界判断

### 3.1 仓库内已确认的官方客户端

当前文档与种子数据明确存在以下官方客户端：

- `radish-client`
- `radish-console`
- `radish-scalar`

它们当前都属于 **官方内部客户端**，并且已经完成登录、回调、登出与基础授权链路的真实联调。

### 3.2 仍未确认清楚的外部依赖

当前还没有一份经过确认的清单，能够回答以下问题：

- 是否存在仓库外脚本、第三方应用、联调工具或历史部署脚本，仍直接依赖 `ClaimTypes.NameIdentifier`、`ClaimTypes.Role`、`TenantId` 这类历史输出 Claim。
- 是否存在只消费 Access Token、不走 `userinfo`、也不依赖前端 SDK 标准映射的外部调用方。
- 是否存在仍以“历史 JWT 字段名”做解析的运维脚本、测试脚本或外部网关配置。

只要这三类问题里有一类没有答案，就不能把“可以停双写”写成事实。

## 4. 启动判定标准

### 4.1 可以启动 Phase 4 的条件

只有同时满足以下条件，才建议正式启动：

1. **协议消费者清单明确**
   - 能明确列出当前仍在消费 Token / `userinfo` 的客户端和调用方。

2. **历史 Claim 依赖已完成盘点**
   - 对仓库外依赖是否仍读取 `ClaimTypes.NameIdentifier`、`ClaimTypes.Role`、`TenantId`、`jti` 等历史字段，已有事实确认。

3. **官方客户端回归入口明确**
   - `radish-client / radish-console / radish-scalar` 的回归顺序、入口与通过标准已明确。

4. **回滚路径明确**
   - 若停双写后出现兼容问题，能明确恢复哪些 Claim 输出、恢复窗口和回滚方式。

5. **文档承诺已更新**
   - `authentication`、`identity-claim-*`、规划页与回归记录中的协议口径可以在同一批次回写，不留模糊区。

### 4.2 不应启动的条件

出现以下任一情况，都不应直接开工：

- 只能证明“仓库内代码看起来没问题”，但无法证明仓库外没有依赖。
- 只能证明官方客户端能用，但无法证明历史调试/联调路径不再读旧 Claim。
- 只能说“出问题再补回去”，但没有明确回滚步骤与责任边界。

## 5. 当前结论（2026-04-02）

### 5.1 结论

**当前不建议直接启动 Phase 4 实施。**

更准确的表述是：

> **Phase 4 已具备进入“启动前提确认”阶段的条件，但尚不具备直接进入“协议输出收敛实施”的条件。**

### 5.2 原因

原因不是运行时代码还没收口，而是 **外部兼容边界仍未被事实化**：

- 仓库内官方客户端基本清楚；
- 仓库外协议消费者仍缺少确认清单；
- 历史 Claim 的“最后一批依赖者”还没有被明确列出来；
- 因此现在直接停双写，风险不在仓库内，而在仓库外未知依赖。

## 6. 建议的下一步

当前建议先做一个很小但必须完成的“启动前确认批次”，而不是立即改代码。

### 6.1 第一步：形成协议消费者清单

至少确认以下对象：

- 官方客户端：`radish-client / radish-console / radish-scalar`
- 仓库内测试 / 联调脚本
- 现有部署环境中的外部网关或脚本
- 已规划但未正式上线的第三方/开放平台接入点

产出物建议：

- 一份“协议消费者矩阵”
- 每项写明：是否直接读 Token、是否走 `userinfo`、是否仍依赖历史 Claim

当前产出：

- 已新增 [身份语义 Phase 4 协议消费者矩阵](/guide/identity-claim-protocol-consumers)
- 当前已明确：`radish-client`、`radish-console`、`Radish.Api.AuthFlow.http` 属于最需要优先处理的直接协议消费者

### 6.2 第二步：形成历史 Claim 保留矩阵

建议把以下字段拆成三类：

- **继续保留输出**
- **只保留输入兼容**
- **允许停止双写**

最小矩阵至少覆盖：

- `sub`
- `name`
- `preferred_username`
- `role`
- `scope`
- `tenant_id`
- `ClaimTypes.NameIdentifier`
- `ClaimTypes.Role`
- `ClaimTypes.Name`
- `TenantId`
- `jti`

当前产出：

- 已新增 [身份语义 Phase 4 历史 Claim 保留矩阵](/guide/identity-claim-retention-matrix)
- 当前已明确：
  - `sub / name / preferred_username / role / scope / tenant_id` 继续保留输出
  - `ClaimTypes.NameIdentifier / ClaimTypes.Role / ClaimTypes.Name` 允许停止双写
  - `TenantId / jti` 只保留输入兼容

### 6.3 第三步：定义实施与回滚窗口

在真正启动前，至少明确：

- 先改 `AccountController` 还是先改 `userinfo`
- 官方客户端的验证顺序
- 如果出现兼容问题，优先恢复哪一批历史 Claim 双写
- 回滚后的文档口径如何同步

当前产出：

- 已新增 [身份语义 Phase 4 实施与回滚窗口](/guide/identity-claim-phase4-rollout-window)
- 当前已明确：
  - 首轮窗口先收缩 `AccountController + AuthorizationController` 的输出侧双写
  - `userinfo` 首轮以“保持稳定”优先，不作为默认先改入口
  - 官方回归顺序为 `radish-client -> radish-console -> Radish.Api.AuthFlow.http -> radish-scalar`
  - 默认回滚优先恢复 `ClaimTypes.NameIdentifier / ClaimTypes.Name / ClaimTypes.Role`

## 7. 建议的启动批次定义

当且仅当以下三项都完成，才算具备“是否启动 Phase 4”的最终评审输入：

1. 协议消费者清单已落文档（见 [身份语义 Phase 4 协议消费者矩阵](/guide/identity-claim-protocol-consumers)）。
2. 历史 Claim 保留矩阵已落文档（见 [身份语义 Phase 4 历史 Claim 保留矩阵](/guide/identity-claim-retention-matrix)）。
3. 实施顺序与回滚方案已落文档（见 [身份语义 Phase 4 实施与回滚窗口](/guide/identity-claim-phase4-rollout-window)）。

若这三项都完成，且仓库外兼容边界也已被确认清楚，当前事项才从“启动前提确认”升级为：

`身份语义 Phase 4 已启动`

在这之前，当前事项仍应保持为：

`身份语义 Phase 4 启动前提确认`

当前评审结果：

- 三份前置资产当前均已完成；
- 但根据 [身份语义 Phase 4 最终启动评审](/guide/identity-claim-phase4-start-review)，仓库外兼容边界仍未被事实关闭；
- 因此当前结论仍是：**不正式启动 Phase 4 实施。**

## 8. 对当前规划的影响

基于以上结论，当前规划建议保持如下顺序：

1. 先完成 Phase 4 启动前提确认。
2. 若前提确认通过，则启动协议输出收敛。
3. 若短期内无法确认仓库外兼容边界，则先转向 `M14` 的宿主运行与最小可观测性基线重定义，不让主线悬空。
