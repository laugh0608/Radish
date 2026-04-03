# 身份语义 Phase 4 协议消费者矩阵

> 本文用于把“谁在消费 Token / `userinfo` / 历史 Claim 字段”从印象判断收束为文档事实。
>
> 关联文档：
>
> - [身份语义 Phase 4 启动前提确认](/guide/identity-claim-phase4-readiness)
> - [身份语义 Phase 4 历史 Claim 保留矩阵](/guide/identity-claim-retention-matrix)
> - [身份语义 Phase 4 实施与回滚窗口](/guide/identity-claim-phase4-rollout-window)
> - [身份语义收敛迁移计划](/guide/identity-claim-migration)
> - [身份语义收敛与 Claim 治理设计](/architecture/identity-claim-convergence)
> - [鉴权与授权指南](/guide/authentication)
> - [认证服务统一指南](/guide/authentication-service)
> - [开放平台设计文档](/features/open-platform)

## 1. 目的与范围

Phase 4 要处理的不是“仓库里还有没有 `FindFirst(...)`”，而是 **协议输出一旦收缩，谁会被影响**。

因此这里不只记录“谁能登录”，而是重点记录：

- 谁会直接读取 Access Token / ID Token 里的字段；
- 谁会显式调用 `/connect/userinfo`；
- 谁只是透传 `Authorization: Bearer ...`，但仍依赖当前授权码、Token、Scope 或回调契约；
- 哪些边界当前仍未知，因而不能把“可以停双写”写成事实。

## 2. 消费方式分层

为避免把不同风险混在一起，本文把协议消费者分成三类：

1. **直接字段消费者**
   - 直接解析 JWT Payload，或直接读取 `userinfo` 返回字段。
   - 这类对象最容易受历史 Claim 停双写影响。

2. **协议链路消费者**
   - 不直接读历史字段，但明确依赖 `/connect/authorize`、`/connect/token`、回调地址、Scope、Refresh Token 或 Bearer 透传链路。
   - 这类对象通常不是 Phase 4 的字段阻塞项，但必须纳入官方回归。

3. **待确认外部消费者**
   - 仓库里没有完整事实，只能从部署、联调或开放平台规划中推断其可能存在。
   - 这类对象本身就是当前“不能直接启动 Phase 4”的核心原因之一。

## 3. 协议消费者矩阵（截至 2026-04-02）

| 消费者 | 类型 | 当前入口 | 消费方式 | 已确认依赖的字段 / 契约 | 是否属于启动阻塞项 |
| --- | --- | --- | --- | --- | --- |
| `Radish.Api` 资源服务器与授权策略 | 仓库内协议边界 | `/guide/authentication`、`Radish.Api/Program.cs`、`Radish.Common/HttpContextTool/*` | Bearer Token 验签、`scope` 策略判断、角色判断、当前用户标准化读取 | `scope`、`role`、`tenant_id`，以及兼容读取链中的历史 Claim 常量 | 否。属于仓库内已知、可控边界，但必须纳入正式实施批次 |
| `radish-client` | 官方客户端 | `Frontend/radish.client/src/services/auth.ts`、`Frontend/radish.client/src/services/tokenService.ts`、`/guide/authentication-service` | 授权码换 Token，本地保存 `access_token/refresh_token`，并直接解析 Access Token Payload | `sub`、`preferred_username`、`name`、`tenant_id`、`TenantId`、`role`、`roles`、`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`、`http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`、`http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name` | 是。当前最明确的直接字段消费者之一 |
| `radish-console` | 官方客户端 | `Frontend/radish.console/src/pages/OidcCallback/OidcCallback.tsx`、`Frontend/radish.console/src/services/tokenService.ts`、`/guide/authentication-service` | 通过 `@radish/http` 完成授权码换 Token，本地保存 Token，并直接解析 Access Token 作为用户名兜底 | `name`、`preferred_username`、`nickname`、`unique_name`、`http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name` | 是。当前最明确的直接字段消费者之一 |
| `radish-scalar` | 官方客户端 | `/guide/authentication`、`/features/open-platform` | 通过授权码流程获取 Bearer Token，用于 API 文档调试 | 当前文档仅明确依赖 `openid profile radish-api` 授权链路；未确认直接读取历史 Claim 字段 | 否。不是已确认的历史 Claim 阻塞项，但必须纳入官方回归 |
| `@radish/http` OIDC 回调工具 | 共享前端基础设施 | `Frontend/radish.http/src/oidc-callback.ts` | 负责授权码换 Token、重复回调去重与错误处理 | `client_id`、`redirect_uri`、`code`、`access_token` 响应存在性；未直接解析 Claim 字段 | 否。属于协议链路依赖，不是历史 Claim 字段消费者 |
| `Radish.Api.AuthFlow.http` | 仓库内联调资产 | `Radish.Api.Tests/HttpTest/Radish.Api.AuthFlow.http` | 手工走授权码流程、手工粘贴 `access_token/refresh_token`、显式调用 `/connect/userinfo` | `/connect/token`、`/connect/userinfo`、`access_token`、`refresh_token`；`userinfo` 当前预期包含 `sub / name / email / role` 等字段 | 是。它直接消费 `userinfo` 契约，是启动前必须确认的联调资产 |
| 其余 `HttpTest` 专题脚本 | 仓库内测试资产 | `Radish.Api.Tests/HttpTest/README.md` 与各专题 `.http` 文件 | 统一复用 `Radish.Api.AuthFlow.http` 获取到的 Access Token，再透传 Bearer 调用业务接口 | 依赖 Token 可被成功获取并用于 Bearer 调用；通常不直接读取 Claim 字段 | 否。不是历史 Claim 字段阻塞项，但依赖 AuthFlow 口径稳定 |
| 已部署环境中的脚本 / 外部网关 / 历史联调资产 | 仓库外待确认边界 | 当前仓库内无完整事实，入口仅能从 `/guide/identity-claim-phase4-readiness` 与部署环境盘点中补齐 | 可能直接读 Access Token，可能调用 `userinfo`，也可能只做网关侧字段映射 | 当前未知；重点待确认 `ClaimTypes.NameIdentifier`、`ClaimTypes.Role`、`TenantId` 等历史字段是否仍被消费 | 是。未知边界本身就是当前不能直接启动 Phase 4 的原因 |
| 未来第三方 / 开放平台客户端 | 规划中的外部边界 | `/features/open-platform` | 规划上允许 `{custom}` 第三方客户端接入，但仓库内尚无已事实化的消费清单 | 当前只能确认将依赖标准 OIDC 协议；是否存在历史 Claim 依赖尚无事实 | 是。正式启动前必须确认“规划存在”与“生产已接入”不是两回事 |

## 4. 当前判断

### 4.1 已确认的直接字段消费者

当前最需要优先处理的直接协议字段消费者，已经可以明确写成事实：

- `radish-client`
- `radish-console`
- `Radish.Api.AuthFlow.http`（直接消费 `userinfo` 契约）

其中前两个风险最大，因为它们并不是“只拿 token 发请求”，而是已经在前端直接解析 JWT 字段名。

### 4.2 已确认的协议链路消费者

以下对象当前更偏向“协议链路依赖”，而不是“历史 Claim 字段阻塞项”：

- `radish-scalar`
- `@radish/http` OIDC 回调工具
- 其余复用 `AuthFlow.http` 的专题 `.http` 脚本
- `Radish.Api` 资源服务器与策略口径

它们仍必须纳入回归，但不构成“停掉历史 Claim 双写后第一时间就会按字段名炸掉”的主要未知点。

### 4.3 当前仍未事实化的边界

当前还不能写成事实的部分主要有两类：

1. 已部署环境里是否还有仓库外脚本、联调工具、网关映射或历史资产，仍直接读取旧字段名。
2. 开放平台“允许第三方客户端接入”的规划，是否在某些环境里已经出现真实消费者。

截至 `2026-04-03`，这两类边界已经结合仓库资产侧与当前生产环境事实完成当前轮次确认，因此不再构成“是否允许启动”的默认阻塞项；后续若部署形态变化，仍需回到仓库外边界清单补事实。

## 5. 对 Phase 4 的直接影响

基于当前矩阵，可以先把结论收束为三点：

1. **协议消费者清单的第一份事实资产已经形成。**
   - 当前已能明确指出最关键的直接字段消费者，不再停留在“可能有前端依赖”。

2. **Phase 4 的首要风险不在仓库内运行时代码，而在协议承诺变更后的消费者兼容。**
   - 运行时代码主路径已基本收口；
   - 真正需要谨慎的是官方客户端直读字段、`userinfo` 契约，以及仓库外未知依赖。

3. **截至 `2026-04-03`，本矩阵的“启动前事实职责”已完成，Phase 4 已正式进入实施。**
   - 最终启动评审与当前部署范围内的仓库外边界确认当前都已完成，见 [身份语义 Phase 4 最终启动评审](/guide/identity-claim-phase4-start-review)。
   - 当前下一步不再是继续确认是否能启动，而是继续把这些直接消费者与官方回归资产维持在“标准优先 + 输入兼容 fallback”的一致口径下。
   - 当前执行状态与回滚边界，见 [身份语义 Phase 4 实施与回滚窗口](/guide/identity-claim-phase4-rollout-window) 与 [当前进行中](/planning/current)。

因此当前对本矩阵的定位应更新为：

`身份语义 Phase 4 已启动实施（本矩阵继续作为直接消费者事实基线）`
