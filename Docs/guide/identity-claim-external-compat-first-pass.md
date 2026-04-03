# 身份语义 Phase 4 仓库外兼容边界首轮执行记录（仓库资产侧）

> 本文不是新的 checklist，而是对 [身份语义 Phase 4 仓库外兼容边界确认清单](/guide/identity-claim-external-compat-checklist) 的第一轮实际执行记录。
>
> 当前记录范围刻意收敛为：**仓库内可直接核实的交付资产与官方协议入口**。它的目标不是替代测试 / 生产环境盘点，而是先把“仓库里已经能证明什么、还不能证明什么”写成事实，避免下一步继续停留在抽象表述。
>
> 关联文档：
>
> - [身份语义 Phase 4 最终启动评审](/guide/identity-claim-phase4-start-review)
> - [身份语义 Phase 4 仓库外兼容边界确认清单](/guide/identity-claim-external-compat-checklist)
> - [身份语义 Phase 4 协议消费者矩阵](/guide/identity-claim-protocol-consumers)
> - [鉴权与授权指南](/guide/authentication)
> - [部署与容器指南](/deployment/guide)

## 0. 最新补充事实（2026-04-03）

在仓库资产侧首轮排查完成后，当前又补齐了实际部署环境的最关键事实：

- 当前只有 **1 套生产环境**，没有独立测试环境。
- 生产环境当前以 **单个 Docker 实例**运行，部署版本为 `v26.3.2-release`。
- 外层反向代理使用 **1Panel 默认反向代理**，仅承担标准 HTTPS 证书与回源转发职责。
- 当前未配置任何自定义鉴权、JWT 解析或旧 Claim 字段映射规则。
- 生产环境的 `OpenIddict` 当前只有默认种子数据，未发现额外第三方客户端。
- 当前不存在仓库外的换 Token、联调接口、巡检或解析旧 Claim 的独立脚本。

这组事实意味着：此前仍待外部环境确认的“生产环境脚本 / 真实反代 / 真实客户端列表”当前已经在**现网实际部署范围内**完成事实关闭。

## 1. 记录目的

截至 `2026-04-03`，当前仓库已经有足够多的部署、网关、脚本与客户端资产，可以先回答下面两个问题：

1. 仓库随代码交付的部署样例、脚本与官方客户端里，哪些对象已经能确认**不是**旧 Claim 字段消费者？
2. 哪些对象即使仓库里没有直接证据，也仍然必须去测试 / 生产环境继续确认？

这份记录的价值在于把“外部兼容边界未关闭”进一步细化为：

- **已由仓库事实排除的部分**
- **仓库里仍无法证明的部分**

## 2. 本轮取证范围

本轮只核查仓库内可直接打开的资产，不对测试机、生产机、独立部署仓库或外部网关做推测性结论。

本轮已检查的对象包括：

- `Deploy/docker-compose.yml`、`Deploy/docker-compose.local.yml`、`Deploy/docker-compose.test.yml`、`Deploy/docker-compose.prod.yml`
- `Deploy/nginx.prod.conf`
- `Docs/deployment/guide.md`
- `Radish.Auth/OpenIddict/OpenIddictSeedHostedService.cs`
- `Radish.Api.Tests/HttpTest/*`
- `Frontend/radish.client/src/services/tokenService.ts`
- `Frontend/radish.console/src/services/tokenService.ts`
- `Frontend/radish.http/src/oidc-callback.ts`

## 3. 首轮盘点结果

### 3.1 仓库交付的反向代理 / 网关样例

结论：**当前仓库交付样例中，未发现基于旧 Claim 字段的 Header 映射或鉴权规则。**

依据：

- `Deploy/nginx.prod.conf` 当前仅做标准反向代理与转发头透传：
  - `Host`
  - `X-Real-IP`
  - `X-Forwarded-Host`
  - `X-Forwarded-For`
  - `X-Forwarded-Proto`
- 该样例没有解析 JWT，也没有基于 `ClaimTypes.NameIdentifier`、`ClaimTypes.Role`、`TenantId`、`jti` 做网关判断。
- `Docs/deployment/guide.md` 当前也把生产口径写成“外层反代终止 TLS，Gateway 识别标准 Forwarded Headers”，并未要求在反代层读取历史 Claim 字段。

当前可写成的事实是：

- **仓库随代码交付的 Nginx 样例未发现旧字段依赖。**

当前仍不能写成的事实是：

- 测试 / 生产环境上的真实 `Nginx / Traefik / Caddy / 其他网关` 一定也没有旧字段依赖。

### 3.2 仓库交付的 Compose / 部署变量口径

结论：**当前仓库内的部署编排口径主要依赖 `RADISH_PUBLIC_URL`、OIDC 证书路径、镜像地址与启动顺序，未发现把旧 Claim 字段写进部署变量或 Compose 规则。**

依据：

- `Deploy/docker-compose.test.yml` 与 `Deploy/docker-compose.prod.yml` 当前重点关注：
  - `OpenIddict__Server__Issuer`
  - OIDC signing / encryption 证书路径与密码
  - `dbmigrate -> api/auth -> gateway` 启动顺序
- 本轮未发现 Compose 层直接解析 Token 或按旧 Claim 字段做环境判断。
- `Docs/deployment/guide.md` 中的部署前后复核动作，也集中在 `RADISH_PUBLIC_URL`、回调地址、证书、健康检查与日志入口。

当前可写成的事实是：

- **仓库内部署编排资产未发现旧 Claim 字段依赖。**

当前仍不能写成的事实是：

- 测试 / 生产环境上的宿主机脚本、额外环境变量文件或独立部署仓库同样没有这类依赖。

### 3.3 仓库内 `.http` / Shell / PowerShell 联调资产

结论：**仓库内联调资产里，`userinfo` 的直接消费者仍然存在，但范围当前是已知且收敛的。**

依据：

- `Radish.Api.Tests/HttpTest/Radish.Api.AuthFlow.http` 当前显式调用 `/connect/userinfo`。
- 其余专题 `.http` 文件主要复用 `AuthFlow.http` 获取到的 `access_token`，再透传 Bearer 调业务接口。
- 本轮未发现仓库内 Shell / PowerShell 联调脚本直接解析 `ClaimTypes.NameIdentifier`、`ClaimTypes.Role`、`TenantId` 或 `jti`。

当前可写成的事实是：

- **仓库内仍存在 `userinfo` 契约消费者，但其主入口已收束到 `Radish.Api.AuthFlow.http`。**
- **仓库内其余专题联调脚本当前主要依赖“能拿到 Bearer Token”，不是已确认的旧字段直接消费者。**

这同时意味着：

- `userinfo` 口径仍属于 `Phase 4` 的正式回归范围；
- 但“仓库外脚本是否也这样用”仍然需要到真实环境继续盘点。

### 3.4 官方客户端与仓库内客户端种子

结论：**当前仓库种子明确落地的官方 OIDC 客户端仍只有 `radish-client`、`radish-console`、`radish-scalar` 三个主对象；未发现随代码交付的第三方客户端种子。**

依据：

- `Radish.Auth/OpenIddict/OpenIddictSeedHostedService.cs` 当前初始化的是：
  - `radish-client`
  - `radish-console`
  - `radish-scalar`
- 历史 `radish-shop` 客户端当前会被显式移除。
- 当前仓库没有新的第三方 `{custom}` 客户端种子文件。

但这条结论的边界必须写清：

- 这只能证明**默认仓库种子**没有预置第三方客户端；
- 不能证明测试 / 生产环境的 OpenIddict 数据库里没有通过 Console 或历史脚本创建过额外客户端。

因此当前只能写成：

- **仓库默认交付口径未发现第三方客户端种子。**

不能直接写成：

- **所有已部署环境都没有真实第三方客户端。**

### 3.5 官方前端客户端的直读字段现状

结论：**仓库内最关键的直接字段消费者仍然是官方前端，而不是部署编排资产。**

依据：

- `Frontend/radish.client/src/services/tokenService.ts` 当前仍直接解析：
  - `sub`
  - `preferred_username`
  - `name`
  - `tenant_id`
  - `TenantId`
  - `role`
  - 历史 `nameidentifier` / `name` / `role` Claim URI
- `Frontend/radish.console/src/services/tokenService.ts` 当前仍直接解析：
  - `name`
  - `preferred_username`
  - `nickname`
  - `unique_name`
  - 历史 `ClaimTypes.Name` URI
- `Frontend/radish.http/src/oidc-callback.ts` 只处理授权码换 Token 与回调去重，不直接消费 Claim 字段。

这说明：

- 当前 `Phase 4` 的首要协议风险仍然首先在**官方客户端字段兼容**；
- 而“仓库外兼容边界”要确认的是：除这些仓库内已知消费者外，部署环境是否还存在**仓库外**对象继续读取旧字段。

## 4. 对确认清单的影响

基于本轮仓库资产侧盘点，当前可以把确认清单拆成两层状态：

| 检查项 | 当前状态 | 本轮结论 |
| --- | --- | --- |
| 仓库交付的反向代理 / 网关样例 | 已完成仓库侧排查 | 未发现旧 Claim 字段映射 |
| 仓库交付的 Compose / 部署变量口径 | 已完成仓库侧排查 | 未发现旧 Claim 字段依赖 |
| 仓库内联调脚本 | 已完成仓库侧排查 | `userinfo` 消费入口仍存在，但主入口已知且收敛 |
| 仓库默认客户端种子 | 已完成仓库侧排查 | 未发现第三方客户端种子，仅有官方客户端 |
| 测试环境脚本目录 | 当前无此环境 | 当前仅存在生产环境，无独立 test 部署 |
| 生产环境脚本目录 | 已完成环境侧确认 | 当前无换 Token、联调接口、巡检或旧 Claim 解析脚本 |
| 真实外部网关 / 反代配置 | 已完成环境侧确认 | 当前为 1Panel 默认 HTTPS 反代，无自定义 JWT / Claim 映射 |
| 独立调用方 / 外部小工具 / 调试页 | 已完成环境侧确认 | 当前未发现仓库外独立调用方 |
| 已部署环境中的第三方 / 开放平台客户端 | 已完成环境侧确认 | 当前 `OpenIddict` 只有默认种子数据 |
| 旧 Token 存量窗口 | 已形成当前结论 | 当前只有单套生产环境，版本已固定为 `v26.3.2-release`；首轮实施仍需按回滚窗口控制发布顺序，但不再作为“外部边界未知”阻塞项 |

## 5. 当前结论

截至 `2026-04-03`，当前已经可以把结论进一步收束为一句更精确的话：

> **仓库交付资产侧与当前生产环境侧的首轮盘点均已完成：当前部署范围内未发现新的旧 Claim 外部依赖，因此“仓库外兼容边界未关闭”不再是 Phase 4 的阻塞项。**

这比此前单纯写“仓库外兼容边界未关闭”更进一步，因为当前已经明确：

- 哪些对象已经由仓库事实排除；
- 当前生产环境的真实脚本、真实反代与真实客户端列表也已被事实确认；
- 当前阻塞 `Phase 4` 的重点已从“外部边界未知”切换为“按既有实施 / 回滚窗口有序收缩官方协议输出”。

## 6. 下一步建议

本页完成后，下一步不应再从仓库里重复做同样的全文搜索，也不应继续把主线停留在“等待外部边界确认”上。当前下一步应直接进入：

1. 以 [身份语义 Phase 4 最终启动评审](/guide/identity-claim-phase4-start-review) 为准，更新当前结论为“允许启动 Phase 4”。
2. 按 [身份语义 Phase 4 实施与回滚窗口](/guide/identity-claim-phase4-rollout-window) 进入协议输出收缩实施。
3. 官方回归顺序继续保持 `radish-client -> radish-console -> Radish.Api.AuthFlow.http -> radish-scalar`。
4. 若后续部署形态发生变化，例如新增测试环境、独立脚本、第三方客户端或自定义反代规则，再重新打开这份记录补事实。
