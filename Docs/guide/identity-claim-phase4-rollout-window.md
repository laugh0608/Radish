# 身份语义 Phase 4 实施与回滚窗口

> 本文用于回答第三个关键问题：**如果要启动协议输出收敛，应按什么顺序实施、按什么顺序验证、出现问题时先回滚什么？**
>
> 关联文档：
>
> - [身份语义 Phase 4 启动前提确认](/guide/identity-claim-phase4-readiness)
> - [身份语义 Phase 4 协议消费者矩阵](/guide/identity-claim-protocol-consumers)
> - [身份语义 Phase 4 历史 Claim 保留矩阵](/guide/identity-claim-retention-matrix)
> - [身份语义收敛迁移计划](/guide/identity-claim-migration)
> - [鉴权与授权指南](/guide/authentication)

## 1. 目标

Phase 4 的首轮实施，不是“一次性删除所有历史兼容”，而是完成一个受控的 **协议输出收敛窗口**：

- 新签发 Token 停止继续双写历史映射字段；
- `userinfo` 继续维持对外稳定口径；
- 标准化层继续保留输入兼容读取；
- 官方客户端、联调脚本与资源服务器在同一窗口内完成回归；
- 一旦出现兼容阻塞，能在同一窗口内按优先级恢复最小必要的历史双写。

## 2. 窗口原则

### 2.1 单窗口收口，不做跨批次悬挂状态

首次实施建议限制在 **一个独立发布窗口** 内完成，不要拆成多个相隔很久的小改动批次。

原因：

- `AccountController` 是 Claim 来源；
- `AuthorizationController` 决定 Token destinations；
- 官方客户端和 `.http` 资产会立刻消费新 Token；
- 如果中间悬挂，最容易出现“代码和文档都改了一半，但问题却不好定位”的状态。

### 2.2 先收缩输出，不先删除读取兼容

首轮窗口只做 **协议输出收缩**，不在同一批次里删除 `UserClaimReader` / `CurrentUser` 的输入兼容。

也就是说：

- 可以停止 `ClaimTypes.*` 的双写输出；
- 可以不再把 `TenantId / jti` 当作协议承诺；
- 但暂时不要同步删除标准化层对这些旧字段的读取兼容。

这样可以把风险限定在“新 Token 承诺变化”，而不是把“新 Token 变化”和“旧 Token 失效”叠在一起。

### 2.3 `userinfo` 保持稳定优先于内部清理

首轮窗口不建议先动 `userinfo` 的对外返回结构。

原因很简单：

- `userinfo` 已经是协议消费者矩阵中的显式消费边界；
- 当前最大的风险点在 Token 双写，不在 `userinfo` 返回字段扩张；
- 先保持 `userinfo` 稳定，能把问题范围收敛到 Token 输出侧。

## 3. 建议实施顺序

### 3.1 第 1 批：收缩签发源头

首批优先改这两类位置：

1. `Radish.Auth/Controllers/AccountController.cs`
2. `Radish.Auth/Controllers/AuthorizationController.cs`

目标：

- `AccountController` 不再继续生成 `ClaimTypes.NameIdentifier`、`ClaimTypes.Name`、`ClaimTypes.Role`、`TenantId` 这类历史双写输出；
- `AuthorizationController` 与 destinations 逻辑同步收口到标准字段承诺；
- 保证“签发源头”和“Token 目标位置”口径一致，不留下只改一半的状态。

### 3.2 第 2 批：保持 `userinfo` 输出稳定，仅做最小对齐

第二批处理：

1. `Radish.Auth/Controllers/UserInfoController.cs`

处理原则：

- 对外返回结构继续围绕 `sub / name / email / role / tenant_id`；
- 不在首轮窗口里主动扩大 `userinfo` 字段面；
- 若内部仍有历史 fallback，可先保留，待窗口验证通过后再评估是否继续收缩。

换句话说，首轮窗口里 `userinfo` 的职责是 **稳住协议面**，不是先追求最彻底的内部清理。

### 3.3 第 3 批：保留标准化层输入兼容，不纳入首轮删除

以下位置当前不建议纳入首轮“删除兼容读取”批次：

1. `Radish.Common/HttpContextTool/UserClaimReader.cs`
2. `Radish.Common/HttpContextTool/UserClaimTypes.cs`
3. `Radish.Api/Program.cs`

原因：

- 这些位置承担的是运行时兼容和资源服务器读取稳定性；
- 把它们和输出收缩绑在同一窗口里，会显著放大排障面；
- 首轮窗口的目标是“停止历史双写输出”，不是“立刻消灭所有旧字段读取”。

## 4. 官方回归顺序

### 4.1 第一优先级：`radish-client`

优先验证：

- 登录跳转
- OIDC 回调换 Token
- 当前用户资料预热
- 桌面壳层登录态恢复
- 登出

原因：

- 它是当前最明确的直接 Token 字段消费者；
- 它同时消费 `sub / preferred_username / name / tenant_id / role`；
- 它也是当前用户侧影响最大的官方客户端。

### 4.2 第二优先级：`radish-console`

优先验证：

- 登录跳转
- OIDC 回调页
- Token 存储与自动刷新
- 登录后首屏 / 权限相关接口

原因：

- 它也是直接解析 Access Token 的官方客户端；
- 它对 `name / preferred_username / nickname / unique_name` 有显式消费；
- 它能较快暴露“标准字段是否足够”。

### 4.3 第三优先级：`Radish.Api.AuthFlow.http`

优先验证：

- `/connect/authorize`
- `/connect/token`
- `/connect/userinfo`
- `refresh_token` 换新 Access Token
- 受保护 API Bearer 调用

原因：

- 这是当前最直接的协议联调资产；
- 它能最快回答“签发端、`userinfo`、Refresh Token 是否仍闭环”。

### 4.4 第四优先级：`radish-scalar`

优先验证：

- OAuth 授权
- 回调
- 受保护接口调试

原因：

- 它对字段本身不如前两者敏感；
- 但它能验证官方文档调试入口是否仍按新 Token 正常工作。

## 5. 通过标准

在同一实施窗口内，至少同时满足以下条件，才能判定“本轮不回滚”：

1. `radish-client` 登录、刷新、资料预热、登出无阻塞。
2. `radish-console` 登录、回调、首屏接口、自动刷新无阻塞。
3. `Radish.Api.AuthFlow.http` 可完整走通授权码、`userinfo`、Refresh Token 与受保护接口。
4. `radish-scalar` OAuth 授权与 Bearer 调试可用。
5. `Radish.Api` 受保护接口没有出现因角色、租户或 scope 解析异常导致的大面积 `401 / 403` 回归。

只要以上任一项出现明确阻塞，就应视为“仍在回滚窗口内”，不能把本次变更直接判定为完成。

## 6. 回滚顺序

### 6.1 第一优先级：恢复 `ClaimTypes.NameIdentifier / ClaimTypes.Name / ClaimTypes.Role`

这是默认的第一回滚动作。

适用场景：

- `radish-client` 或 `radish-console` 出现用户标识、用户名、角色解析异常；
- 回调后有 Token，但前端登录态初始化失败；
- 资源服务器的角色相关行为出现明显回归。

原因：

- 这是当前最有可能影响仓库内现有消费者的历史双写字段；
- 恢复成本最低；
- 对长期目标伤害最小，因为它只是临时恢复映射字段双写。

### 6.2 第二优先级：按需临时恢复 `TenantId`

这不是默认第一动作，只在以下场景触发：

- 明确发现仓库外脚本、网关映射或历史资产仍依赖 `TenantId`；
- 或仓库内某条未盘干净的兼容路径确实把 `TenantId` 当成唯一来源。

原则：

- 仅作为 **限时应急恢复**；
- 恢复后应同步记录“是谁依赖、准备何时移除”；
- 不把它重新写回长期标准口径。

### 6.3 第三优先级：保持 `userinfo` 稳定，不把它当默认回滚入口

如果首轮窗口按建议执行，`userinfo` 本来就不应成为第一问题源。

因此：

- 不建议把“先改 `userinfo` 再回滚 `userinfo`”作为默认策略；
- 只有当 `Radish.Api.AuthFlow.http` 明确证明 `userinfo` 返回结构被破坏时，才针对 `UserInfoController` 单独恢复。

### 6.4 不建议作为默认回滚项：`jti`

`jti` 不应被默认恢复成“用户 Id 输出字段”。

只有在出现明确、不可绕过、且短期无法替换的外部历史依赖时，才允许作为短期应急项讨论；否则继续维持“只保留输入兼容，不恢复输出承诺”。

## 7. 文档同步规则

如果在回滚窗口内触发恢复动作，必须在同一批次同步更新以下文档：

- [身份语义 Phase 4 启动前提确认](/guide/identity-claim-phase4-readiness)
- [身份语义 Phase 4 历史 Claim 保留矩阵](/guide/identity-claim-retention-matrix)
- [当前进行中](/planning/current)
- [开发日志](/changelog/)

要求：

- 写清恢复了哪些字段；
- 写清恢复是临时回滚还是长期口径调整；
- 不允许让代码与文档重新分叉。

## 8. 当前结论

截至 `2026-04-02`，第三份前置资产也已经具备文档形态：

- **实施顺序与回滚窗口已落文档。**

但这不等于“Phase 4 已经自动启动”。

更准确的表述是：

- 当前三份启动前提资产已经齐备；
- 现在具备进入 **启动评审** 的输入条件；
- 是否正式启动实施，仍要看仓库外兼容边界是否被最终确认，以及是否接受当前回滚窗口约束。

因此当前状态建议更新为：

`启动前提资产已完成 3/3，等待最终启动评审`
