# 身份语义 Phase 4 历史 Claim 保留矩阵

> 本文用于回答第二个关键问题：**哪些 Claim 仍然属于协议承诺，哪些只保留兼容读取，哪些可以停止双写？**
>
> 关联文档：
>
> - [身份语义 Phase 4 启动前提确认](/guide/identity-claim-phase4-readiness)
> - [身份语义 Phase 4 协议消费者矩阵](/guide/identity-claim-protocol-consumers)
> - [身份语义收敛迁移计划](/guide/identity-claim-migration)
> - [身份语义收敛与 Claim 治理设计](/architecture/identity-claim-convergence)
> - [鉴权与授权指南](/guide/authentication)

## 1. 目的

协议消费者矩阵已经回答了“**谁在消费**”。

本矩阵进一步回答“**我们还承诺输出什么**”。

这里的“保留矩阵”只处理 **协议输出承诺**，不等同于运行时标准化组件的读取兼容策略：

- 某个字段被标记为“允许停止双写”，不代表标准化层立即停止读取；
- 某个字段被标记为“只保留输入兼容”，表示 **不再把它当成新 Token / `userinfo` 的协议承诺**；
- 标准化层是否继续读取旧字段，仍由 `UserClaimReader` / `CurrentUser` 兼容策略负责。

## 2. 分类规则

### 2.1 继续保留输出

满足以下任一条件：

- 它是当前明确承诺的标准协议字段；
- 仓库内官方客户端或资源服务器已经把它当成正式字段消费；
- 去掉它会直接破坏当前标准 OIDC / API 授权主链。

### 2.2 只保留输入兼容

满足以下条件：

- 它代表历史阶段的兼容字段；
- 当前不应再把它当成“新 Token 必须继续输出”的协议承诺；
- 但标准化层仍需要在一段时间内读取它，以兼容旧 Token、旧 Cookie 主体或仓库外未完全收口的历史资产。

### 2.3 允许停止双写

满足以下条件：

- 它与标准字段表达的是同一语义；
- 当前主链已经有稳定的标准字段可替代；
- 即使停掉它的重复输出，仓库内已知消费者仍可通过标准字段继续工作。

这类字段在 Phase 4 启动后，可以从签发端和协议输出边界移除“重复发一份”的做法，但标准化层仍可暂时保留输入兼容。

## 3. 当前判断矩阵（截至 2026-04-04）

| 字段 | 语义 | 当前输出侧判断 | 输入兼容是否继续保留 | 主要依据 |
| --- | --- | --- | --- | --- |
| `sub` | 用户唯一标识 | 继续保留输出 | 是 | 标准 OIDC 主标识；`radish-client` 直接消费；`UserInfoController` 输出；`UserClaimReader` 以它为首选来源 |
| `name` | 用户显示名 | 继续保留输出 | 是 | 官方客户端、`userinfo`、当前文档与 OIDC 标准主链均依赖；`UserClaimReader` 以它为首选用户名来源 |
| `preferred_username` | 用户名 / 登录名补充标识 | 继续保留输出 | 是 | `radish-client`、`radish-console` 已直接消费；当前 `AccountController` 已写入，`AuthorizationController` 对 `profile` scope 保留输出 |
| `role` | 角色 | 继续保留输出 | 是 | `Radish.Api` 授权策略和前端角色判断依赖；`AuthorizationController` 明确保留到 Access Token / ID Token |
| `scope` | 授权范围 | 继续保留输出 | 是 | `Radish.Api` 的 `Client` 策略直接依赖 `scope=radish-api`；停掉会破坏资源服务器授权主链 |
| `tenant_id` | 标准租户标识 | 继续保留输出 | 是 | `Radish.Api`、`UserInfoController`、`radish-client` 当前都依赖；它已是多租户语义的标准字段 |
| `ClaimTypes.NameIdentifier` | 历史用户标识映射 | 允许停止双写 | 是 | 当前标准替代字段为 `sub`；`radish-client` 已优先读 `sub`，该字段只剩历史兼容价值 |
| `ClaimTypes.Role` | 历史角色映射 | 允许停止双写 | 是 | 当前标准替代字段为 `role`；前端已兼容读取标准 `role` / `roles`，资源服务器口径也已收口到 `role` |
| `ClaimTypes.Name` | 历史用户名映射 | 允许停止双写 | 是 | 当前标准替代字段为 `name` / `preferred_username`；`radish-client` 与 `radish-console` 已优先读取标准字段 |
| `TenantId` | 历史租户标识 | 只保留输入兼容 | 是 | 当前标准字段已是 `tenant_id`；仓库内已知消费者没有把它作为必须继续输出的唯一字段，且当前 OIDC 主路径已不应再以它作为承诺输出 |
| `jti` | 历史用户标识兼容来源 | 只保留输入兼容 | 是 | `UserClaimReader` 仍兼容读取旧自建 JWT；但 Phase 4 不应继续把 `jti` 视为“用户 Id 输出字段”的协议承诺 |

## 4. 字段级结论

### 4.1 必须继续保留输出的字段

以下字段当前仍属于 Phase 4 之后应明确承诺保留的标准协议字段：

- `sub`
- `name`
- `preferred_username`
- `role`
- `scope`
- `tenant_id`

原因很直接：

- 它们要么是标准 OIDC 字段；
- 要么是当前官方客户端、资源服务器与 `userinfo` 已明确消费的正式字段；
- 去掉它们会直接影响当前仓库内主链，而不是只影响历史兼容。

### 4.2 可以停止双写的字段

以下字段当前建议在 Phase 4 启动后停止重复输出：

- `ClaimTypes.NameIdentifier`
- `ClaimTypes.Role`
- `ClaimTypes.Name`

原因如下：

- 它们表达的语义，当前都已有标准字段一一对应；
- 当前已知前端消费者都已优先读取标准字段；
- 继续双写只会把“历史映射字段仍是正式承诺”的错觉继续保留下去。

但需要强调：

- **停止双写不等于立刻停止兼容读取。**
- `UserClaimReader` / `CurrentUser` 仍可在过渡期继续读取这些字段，避免旧 Token 或旧 Cookie 主体立刻失效。

### 4.3 只保留输入兼容的字段

以下字段不应继续作为新 Token / `userinfo` 的输出承诺：

- `TenantId`
- `jti`

判断依据：

- `tenant_id` 已经是当前正式的租户字段；
- `TenantId` 只剩历史兼容意义，不应再恢复成协议主口径；
- `jti` 在历史自建 JWT 中曾被当作用户标识兼容来源，但 Phase 4 不应再把它当成用户 Id 语义字段继续承诺。

尤其是 `jti`，后续文档与实现都应避免继续使用“`jti = 用户 Id`”这种表述，以免把旧 JWT 语义误当成新协议承诺。

## 5. 对实施顺序的影响

基于当前矩阵，Phase 4 的协议收敛顺序应满足以下约束：

1. 先保证标准字段契约不动。
   - `sub / name / preferred_username / role / scope / tenant_id` 先继续保持稳定。

2. 再停止历史映射字段双写。
   - 先从 `ClaimTypes.NameIdentifier / ClaimTypes.Role / ClaimTypes.Name` 入手。

3. 输入兼容读取晚于输出收缩。
   - 即使停止双写，也不要在同一批次立即删除 `UserClaimReader` 对旧字段的兼容读取。

4. `TenantId / jti` 不再恢复为新协议承诺。
   - 若仓库外盘点中发现仍有真实依赖，处理方式应是“限定回滚窗口内临时恢复”，而不是重新把它们写回长期标准。

## 6. 当前结论

截至 `2026-04-04`，这份矩阵已经完成“启动前事实职责”，并继续作为 Phase 4 稳定维护阶段的协议承诺基线：

- **历史 Claim 保留矩阵已落文档。**

同时也可以把当前主结论进一步收束为：

- Phase 4 之后的长期协议承诺，应围绕 `sub / name / preferred_username / role / scope / tenant_id`；
- `ClaimTypes.*` 不应继续作为长期双写输出；
- `TenantId / jti` 只保留输入兼容，不再作为新协议承诺。

因此，当前对本矩阵的定位应更新为：

`Phase 4 长期协议承诺基线（首轮实施与官方回归已完成）`
