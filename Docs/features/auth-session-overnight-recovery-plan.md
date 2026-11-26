# 隔夜会话排查与验收方案

2026-11-25 · 六项提案第 2 项。候选已集成，部署开关保持关闭。实际运行进度见[本轮隔离联验](../records/auth-session-candidate-2026-11-25.md)。[当前进行中](../planning/current.md) · [会话不活跃规则](../guide/auth-idle-session.md) · [隔离复现记录](../records/auth-session-overnight-default-lifetime-2026-11-21.md)

## 已核对的生效值

| 证据范围 | 结论 |
| --- | --- |
| `dev` 源码、现有 Debug / Release 构建输出、正式标签 `v26.8.1-release` | Access token 5 分钟，refresh token 25 分钟，授权码 5 分钟。启用 7 天不活跃判断，判断时钟偏移及客户端活动未来容差各 60 秒。 |
| OrbStack 本机缓存正式 Auth 镜像 `linux/arm64`，摘要 `sha256:c3c9e922c02a7f3dc5e2d350755cb38dd6ee1a1d2bff75674f708aa5c3ea137b` | 只读检查镜像内 `/app/appsettings.json`、共享配置、镜像环境及本地覆盖文件存在性，得到相同值。镜像摘要与 11 月 21 日联验使用的正式基线一致。 |
| `DataBases/Radish.OpenIddict.db` 中的历史签发记录 | 只读聚合 `radish-client` 和 `radish-console` 各自最近的 20 条 access、20 条 refresh、20 条授权码记录。每个样本的创建至过期差值分别为 300、1500、300 秒。样本最新日期分别为 2026-09-05 和 2026-08-15。`radish-client` 与 Flutter 共享登记，历史聚合未按回调来源区分。两个客户端及 `radish-scalar` 的应用登记均无单独的令牌期限覆盖。 |
| 当前运行实例与用户报告 | 本机检测时无运行中的 Radish Auth，也无常用服务端口或隔离 Gateway 端口监听。用户遇到掉线的实例、社区 Web / Console 入口和失败响应待确认。目标实例可能有自己的部署覆盖。 |

Auth 从共享文件、内容根目录的 `appsettings.json`、可选 `appsettings.Local.json`、环境变量依次加载，后加载的同名键优先。此次检查的本机覆盖和仓库 Compose 模板均无上述期限覆盖。`AccessTokenLifetime` 与 `RefreshTokenLifetime` 单位为分钟，60 分钟与 30 天是配置键缺失时的代码回退值。核对目标实例时只取其版本或镜像摘要、相同键的覆盖结果、应用期限设置及一次签发的时长和刷新结果。[Auth 启动配置](../../Radish.Auth/Program.cs) · [Auth 默认配置](../../Radish.Auth/appsettings.json) · [联验记录](../records/unified-logging-exit-console-2026-11-21.md)

## 隔夜重开的失效链路

1. 社区 Web 以 `radish-client` 登录，请求 `offline_access`，将 access token 和 refresh token 保存在浏览器 `localStorage`。当前令牌的寿命从**最后一次成功签发**起算，包括登录换取与后续刷新轮换。
2. 关闭页面或设备休眠后没有成功刷新，5 分钟后 access token 过期。页面重开或恢复到前台时，认证初始化尝试用保存的 refresh token 调用 `/connect/token`。
3. 经过 25 分钟后，OpenIddict 7.2.0 在处理刷新请求时先拒绝过期的令牌，令牌端点返回 `invalid_grant`。Radish 的 7 天不活跃校验位于令牌认证之后。`session_idle_expired` 表示该不活跃规则拒绝，本路径在它之前已经失败。OpenIddict 的令牌过期校验默认零时钟偏移，Radish 上述 60 秒容差适用于不活跃规则。[过期校验](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Server/OpenIddictServerHandlers.Protection.cs#L832-L876) · [令牌端点错误映射](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Server/OpenIddictServerHandlers.Exchange.cs#L2035-L2052) · [默认校验参数](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Server/OpenIddictServerOptions.cs#L118-L126)
4. Web 收到刷新失败，发现现有 access token 已到期后清除本地凭据并触发过期事件，认证状态回到游客。Console 用自己的 `radish-console` 凭据走同一 Auth 端点，失败后进入 Console 登录流程。`.Radish.Auth.Session` Cookie 有独立的两小时滑动期限，重走 OIDC 授权有机会复用 Cookie，应用的 `/connect/token` 刷新流程不借此重建会话。[Web 登录](../../Frontend/radish.client/src/services/auth.ts) · [恢复入口](../../Frontend/radish.client/src/services/authBootstrap.ts) · [令牌管理](../../Frontend/radish.client/src/services/tokenService.ts) · [Console 令牌管理](../../Frontend/radish.console/src/services/tokenService.ts)

正式镜像的社区 Web 隔离对照复现了这条链路。7 分钟组刷新成功并恢复同一账号，27 分钟组收到 `400 invalid_grant`，Auth 记录 refresh token 到期诊断码 `ID2018`，Web 清理凭据。两组重开均观察到两次刷新请求，实际轮换顺序和失败处理需另行取证。Web 在 access token 到期后因网络失败或 `5xx` 清除凭据的路径也待验证。[隔离复现记录](../records/auth-session-overnight-default-lifetime-2026-11-21.md) · [Web 失效判定](../../Frontend/radish.client/src/services/authSession.ts) · [共享 HTTP 刷新](../../Frontend/radish.http/src/token-refresh.ts)

## 隔离复现步骤

第一轮已完成默认 `5 / 25` 分钟的两组对照。运行使用本机 OrbStack 缓存的 `v26.8.1-release` ARM64 五镜像、PostgreSQL 16.15 与 Redis 7.4.12。镜像按摘要核对，使用已验证过的联验拓扑准备独立 Compose 项目和测试账号。入口仅绑定 `127.0.0.1:35443`，使用 `https://localhost:35443`、独立测试证书、一次性数据库和浏览器资料目录。其余端口留在项目网络。日志使用本地有界存储。[既有隔离配置](../../Scripts/logging/compose-exit-config.mjs) · [浏览器 Smoke 规则](../guide/browser-smoke.md)

获批后在 `mktemp -d /private/tmp/radish-auth-session.XXXXXXXX` 创建目录，以目录后缀生成唯一项目名。Compose 配置、私有测试凭据、证书及数据库初始化脚本均放于该目录。预检确认镜像已缓存、端口可用、无外部卷及生产地址、所有角色 `pull_policy: never`。确认配置预览只公开 Gateway 端口后，按下列命令执行。PostgreSQL 和 Redis 健康后再迁移数据库，Gateway 健康后再启动浏览器测试。

```bash
auth_probe_compose() {
  docker --context orbstack compose --env-file /dev/null \
    --project-directory "$auth_probe_root" -p "$auth_probe_project" \
    -f "$auth_probe_root/compose.yaml" "$@"
}

auth_probe_compose config --quiet
auth_probe_compose up -d --no-build --pull never --wait postgres redis
auth_probe_compose run --rm --no-deps --pull never dbmigrate apply
auth_probe_compose up -d --no-build --pull never api auth frontend gateway
```

| 分组 | 步骤与预期证据 |
| --- | --- |
| 5–25 分钟对照 | 完成登录并确认发放 refresh token。关闭该会话的全部页面，保留浏览器资料，距最后签发 7 分钟重开。同源触发刷新，预期 `/connect/token` 返回 `200` 且受保护接口返回正确账号的数据。 |
| 25 分钟到期 | 另一独立浏览器资料目录按同样步骤登录与关页，约 27 分钟重开。记录请求到达 Auth、响应为 `400 invalid_grant`，结合签发与刷新时刻区分令牌到期与 `session_idle_expired`。观察本地凭据存在布尔值、游客态和公开帖可读。 |
| 网络故障与并发（待单独授权） | 另取有效 refresh token。access token 刚过期时，使用浏览器请求拦截模拟一次本地网络失败或 `503`，恢复后记录能否续期。另用共享资料的两标签页同时恢复，记录请求数、结果先后及最终会话。 |

首轮按镜像默认 `5 / 25` 分钟复现。若另获准使用加速配置，仅在独立试验 Compose 中为 Auth 设置 `OpenIddict__Server__AccessTokenLifetime=1` 与 `OpenIddict__Server__RefreshTokenLifetime=2`，分别在距最后签发约 75 秒与 135 秒处对照。不活跃值仍为 7 天。两种复现都使用实际时间，浏览器与服务端不改时钟。

报告只保留版本与摘要、客户端 ID、时间、配置键有效值、请求状态、归类后的错误、刷新次数及凭据存在布尔值。浏览器网络记录过滤查询串、请求体、Cookie 和响应中的凭据；不保留 HAR、浏览器资料或原始数据库。退出时关闭测试浏览器，执行 `auth_probe_compose down --volumes --remove-orphans`，按项目标签核对容器、网络和卷均已清除，再确认端口释放。校验临时目录前缀后清除测试证书、凭据、浏览器资料和临时配置，仅留脱敏报告。缺少镜像或浏览器、端口占用、预检不通过时停止该轮并登记缺口。

<a id="web-session-repair"></a>

## 已确认的修复方案

建议普通 Web 使用独立 confidential 客户端 `radish-web`。`Radish.Auth` 持有会话和长期凭据，Gateway 转发同源会话路由，Web 继续通过现有 API 客户端和 SignalR 使用内存中的短效 access token。方案覆盖社区 Web 的 PC / mobile 浏览器。Flutter、Console、Scalar 与 WebOS 历史入口按各自现有契约回归。

### 两道期限

会话记录拟存入 Auth 的 OpenIddict 数据库。每条记录绑定环境、会话 ID、浏览器凭据摘要、客户端、账号与租户、对应的 OpenIddict 授权记录。另存 `CreatedAtUtc`、`LastActiveAtUtc`、状态、撤销时间、并发版本及加密后的令牌。PostgreSQL 和 SQLite 都通过 `Radish.DbMigrate` 交付增量迁移。运行时由 Auth 使用统一 UTC 时钟，数据库更新按会话版本与状态原子裁决。[Auth 持久化](../../Radish.Auth.Persistence/OpenIddict/AuthOpenIddictDbContext.cs)

| 期限 | 初值与更新 | 服务端裁决 |
| --- | --- | --- |
| 7 天不活跃 | `LastActiveAtUtc` 初始等于 `CreatedAtUtc`。已登录的前台页面在实际点击、触控、键盘操作或显式导航后，经同源活动接口上报，Auth 以收到该信号的 UTC 时间更新。建议每个会话最多每 60 秒写入一次，服务端同时限流。 | `now >= LastActiveAtUtc + 7d` 时结束会话。状态查询、关页重开自动恢复、定时刷新、focus / visibility 事件、WebSocket 心跳或自动重连均不更新活动时间。活动信号只表达前台交互，客户端时间戳不参与计算。 |
| 30 天绝对上限 | `CreatedAtUtc` 取本次 Web 新登录通过身份验证的服务端时间。刷新、回访、活动更新、应用重启和多端部署均沿用该值。 | `now >= CreatedAtUtc + 30d` 时结束会话，要求用户重新认证。当前 `AuthorizationController` 按 Auth Cookie 判断是否已登录，实施时须补齐 `radish-web` 的 `prompt=login` 处理及一次性再认证关联，验证旧 Auth SSO Cookie 无法跳过。 |

每次状态读取、活动写入、access token 获取和 refresh grant 先按已有记录判断是否仍有效。令 `deadline = min(LastActiveAtUtc + 7d, CreatedAtUtc + 30d)`。当 `now >= deadline` 时拒绝，迟到的活动请求无法恢复会话。刷新后重算并保持绝对起点，签出的 access token 为 `min(now + 5m, deadline)`。被动恢复可以取得短效 token，直到真正活动发生才推进不活跃截止。会话 Cookie 与服务器保管的 Web refresh token 的期限最晚到绝对截止，两个截止都由 Auth 对 refresh grant 执行。这样真实活动延后不活跃截止时，先前签出的 refresh token 仍能用于后续兑换。现有 7 天参数和 60 秒容差继续用于旧客户端独立流程。[OIDC 再认证](https://openid.net/specs/openid-connect-core-1_0.html#AuthRequest) · [既有不活跃规则](../guide/auth-idle-session.md)

Auth 在 OpenIddict 准备令牌前按当前会话主体设定期限，普通 API 仍按 JWT 的 `exp` 校验。已有 SignalR 握手连接也要执行截止，初步选用仅供 `radish-web` 的 `/hub/web/notification`、`/hub/web/chat`、`/hub/web/comment`，复用各自 Hub 实现，开启 `CloseOnAuthenticationExpiration`。Web 以新 JWT 重连并恢复订阅，重连不计活动。API 明确列出允许 query bearer 的 Web Hub 路径，带正式 Web 签发标记的 JWT 不进入旧认证 Hub 路径，匿名评论仍可走既有入口。主动退出、账号停用或撤销使 Auth 停止签发，现有 JWT 或连接最晚在各自 `exp` 到期，最长剩余 5 分钟。[API JWT 校验](../../Radish.Api/Security/ApiJwtValidationPolicy.cs) · [现有 Hub JWT 入口](../../Radish.Api/Security/ApiJwtBearerEvents.cs) · [SignalR 到期断开](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.connections.httpconnectiondispatcheroptions.closeonauthenticationexpiration?view=aspnetcore-10.0)

### 与 Flutter 隔离

1. 为 `radish-web` 登记 authorization code 与 refresh grant、PKCE S256、现有 Web 所需 scopes 和唯一的正式同源服务端回调 `/web/session/callback`。客户端凭据只注入 Auth 的受保护部署配置。继续使用现有 issuer 和 `radish-api` audience。新登记与 `radish-client`、`radish-console`、`radish-scalar` 并存，旧浏览器、Flutter 和历史 WebOS 各自的回调与令牌设置继续有效。[既有客户端登记](../../Radish.Auth/OpenIddict/OpenIddictSeedHostedService.cs)
2. 普通 Web 的登录入口调用同源会话服务。服务生成一次性、短时的 OIDC `state`、`nonce` 和 PKCE verifier，并绑定发起登录的浏览器。Auth 在验证客户端、登记回调及此次交互认证后建立待激活会话，将服务端生成的会话 ID 绑定到授权码。回调消费一次性登录事务，经固定可信 HTTPS Gateway / Auth 路径由服务器兑换授权码，验证 issuer、audience、nonce 和身份匹配后激活。授权码、refresh token 和客户端凭据不作为页面 JavaScript 的输入。部署预检须验证容器内回连地址、TLS 证书与外部 issuer 一致性，开发与隔离验证沿用 HTTPS Gateway。[Gateway 路由](../guide/gateway.md) · [OAuth 授权码安全实践](https://www.rfc-editor.org/rfc/rfc9700.html#section-2.1)
3. 只有 OpenIddict 已认证的 `radish-web` 客户端及绑定到同账号、租户和授权记录的有效服务端会话，可以走新期限。refresh grant 从已验证的令牌主体取服务端会话 ID，再检查持久状态和当前账号可用性。新客户端缺失绑定或会话已失效时拒绝续期。Flutter 继续使用 `radish-client` 的 `radish://` 回调、原有设置和安全存储。应用类型、回调、grant 等来自服务器端验证，旧 `radish-client` 令牌无法兑换新会话。[Flutter 环境配置](../../Clients/radish.flutter/lib/core/config/app_environment.dart)

### 凭据存储与恢复

| 边界 | 拟定设计 |
| --- | --- |
| 浏览器 | `__Host-Radish-Web-Session` 存放 CSPRNG 生成的 256 位不透明凭据，设置 `HttpOnly`、`Secure`、`SameSite=Lax`、`Path=/`，无 `Domain`，最晚在绝对截止失效。浏览器只在内存保存 5 分钟以内的 API bearer 和会话防伪值，重开时由 Cookie 恢复。公开页可匿名启动。 |
| Auth 与存储 | Auth 保存 Cookie 值的摘要，服务端 refresh token 和可复用的短 access token 用独立用途的 Data Protection 加密。密钥环持久化、独立受访问控制并有静态加密，轮换后保留解密现存会话所需的旧密钥，多实例共用该 Web 用途的密钥环与数据库。避免改动已有 Auth Cookie 的密钥用途。备份恢复时撤销回档的 Web 会话后再开放续期，避免恢复已经退出或撤销的会话。 |
| 同源请求 | Gateway 定向转发 `/web/session/*` 至 Auth，从其他上游请求中移除 Web 会话及登录关联 Cookie。拟用 `GET /status` 返回会话状态与防伪值，`POST /token` 取得短 bearer，`POST /activity` 更新活动，`POST /logout` 撤销会话。三条会话写入路由校验精确 `Origin` 和会话绑定的防伪值。匿名 `POST /login` 也验证同源，`GET /callback` 校验 `state`、`nonce` 和 PKCE。会话端点使用 `Cache-Control: no-store`，日志与前端存储不保留 Cookie、令牌、登录参数或防伪值。 |
| Web 恢复 | 认证 bootstrap、定时刷新、`@radish/http` 的 401 恢复和三个 Hub 统一调用同一个 Web token provider。同一标签页共享请求，多标签页从服务端获得同一批仍有效的短 token。断网、`429` 或临时 `5xx` 进入可恢复状态，保留服务端会话并限速重试。明确缺失、到期、撤销或凭据永久无效时清理内存状态，按原返回路径引导用户登录。重复发送受保护写请求须有明确的鉴权拒绝或幂等性保证。 |

刷新保持 OpenIddict 轮换。Auth 以持久会话版本和短租约合并跨实例请求，其他请求等待或退避后读取已写入的新凭据。租约期间释放数据库事务，再发请求到 OpenIddict。结果通过版本校验写回，写入完成后才回应浏览器。写回前重新核对截止、撤销和版本，退出和停用先禁用会话及后续 refresh grant，再回收授权。显式登出在撤销 Web 会话后继续既有 OIDC 登出回流，自动到期只结束当前 Web 会话。若刷新服务已消费旧凭据但结果未能持久写回，先辨认是否已有新版本，确认当前凭据永久无效后要求重新登录。验证响应中断、进程重启、租约失效和与登出交错的结果，避免重试争抢已消费的旧 token。[OAuth 刷新与撤销安全实践](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14) · [同源与 CSRF 防护](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html)

### 旧会话迁移与回退

1. **先兼容发布。** 交付数据库增量迁移、独立 OIDC 登记、后端会话接口、Gateway 路由及普通 Web 适配，功能默认关闭。先证明升级前的 `radish-client`、Flutter、Console、Scalar 与 `/desktop` 登录路径仍可使用。登记和迁移应可重跑。正式开启前单独核对密钥持久化、备份恢复、HTTPS、回调、浏览器旧静态资源缓存及所有节点版本。
2. **由用户完成一次重新认证。** 开启普通 Web 后，旧浏览器 access / refresh 凭据按既有期限处理，无论当时是否到期均不升级或导入新会话。有旧登录迹象但无新会话时，公开页照常可读，受保护操作提示登录一次并保留回流路径。新 Cookie 生效后，普通 Web 丢弃自己先前的持久 token、用户缓存与活动状态，使用同标签和跨标签更新让旧 Web 状态退出。若同一浏览器共用正式 Web 与 `/desktop` 的旧存储，历史入口可能需要再次登录，列入切换验收。缓存的旧页面可在旧协议下短期运行，刷新页面后切换。服务端不会凭旧客户端 ID 批量撤销共享授权。
3. **回退需要重新登录一次。** 先关闭新 Web 会话的创建与续期并撤销已建立的 Web 会话，关闭已有连接以其短 JWT 到期为上限。再回退 Web 入口和 Gateway 路由，保持增量表与客户端登记以供审计和重试。该关闭与撤销步骤须在旧 Auth 镜像接管前执行。浏览器下次按旧正式 Web 登录链路建立会话，旧 Flutter 和 Console 通过原发放方式继续工作。重新开启前重验迁移结果和会话撤销状态。

实施顺序拟为会话模型与两道期限、独立客户端及同源 Cookie、普通 Web 和 Hub 接入、旧会话迁移与候选回退。确认范围后先验证 OpenIddict 会话绑定、令牌截止和 Auth HTTPS 回连能在现有技术栈落地，再接入界面和部署。服务启动、配置变更、新依赖及生产启用按实际命令单独授权。

### Auth 首期交付边界

- 已实现 `WebAuthSessions` 会话表、授权绑定、激活、活动更新、撤销及带版本条件的写入。会话截止在 Auth 的授权码 / refresh grant 路径检查，签发时对准备好的令牌主体写入确切过期点。Flutter 与 Console 仍由原客户端登记和既有刷新校验处理。[会话服务](../../Radish.Auth/OpenIddict/WebAuthSessionService.cs) · [签发处理](../../Radish.Auth/OpenIddict/WebAuthSessionTokenHandler.cs)
- 首期新增独立的 `radish-web` 登记能力，开关默认关闭。登记和刷新策略均以该开关为限。持有普通 Auth Cookie 的授权请求须有服务端建立的会话绑定才能签发。[客户端登记](../../Radish.Auth/OpenIddict/WebAuthClient.cs)
- 已生成 SQLite、PostgreSQL 的增量迁移。实际环境升级前需经授权执行 `Radish.DbMigrate apply`，其中 Auth schema 使用启动只读自检。验证范围为临时 SQLite 数据库、PostgreSQL schema 与生成 SQL、令牌处理器及登记测试，实际 PostgreSQL 和浏览器联验仍需单独授权。[数据库边界](../guide/database-overview.md#结构真相源)

### 同源会话和凭据首版

- Gateway 将 `/web/session/*` 转发给 Auth。`POST /login` 返回 OIDC 授权地址和短效关联 Cookie，`GET /callback` 在当前浏览器完成交互认证、服务端 PKCE 兑换和 ID token 校验后签发 `__Host-Radish-Web-Session`，随后回跳站内路径。登录事务限时 10 分钟且只能消费一次。先前 Auth SSO Cookie 无法替代这次交互认证。Auth 持久化浏览器随机凭据的摘要，两个 Cookie 均设为 `HttpOnly`、`Secure`、`SameSite=Lax`、`Path=/`。[会话控制器](../../Radish.Auth/Controllers/WebSessionController.cs) · [登录事务](../../Radish.Auth/WebSessions/WebSessionLoginService.cs)
- Auth 回连先验证令牌，再从已验证主体读取标准 Claim 原值。ID token 核对 `sub`、`nonce` 和存在时的 `azp`，access token 核对 `sub`、`tenant_id` 和 `client_id`。这些绑定按原字符串比较。[令牌回连](../../Radish.Auth/WebSessions/WebTokenBackchannel.cs)
- `GET /status` 返回 `VoCsrfToken`。`POST /token`、`POST /activity`、`POST /logout` 要求同源 `Origin` 和 `X-Radish-Web-Session` 防伪头。`/token` 只给出 `VoAccessToken` 与 `VoExpiresAt`。`503` 保留 Cookie 并带 `Retry-After`，`401` 表示会话已失效并清除 Cookie。`/logout` 先撤销本地会话，再撤销对应的 OpenIddict 授权并返回站内 OIDC 登出地址。Auth 将短 access token 和 refresh token 一起加密入库，密文用途绑定会话 ID。[凭据服务](../../Radish.Auth/WebSessions/WebSessionAccessService.cs) · [Cookie 转发边界](../../Radish.Gateway/WebSessionProxy.cs)
- 刷新租约为 20 秒，进入 OpenIddict 刷新时验证租约仍属于当前会话。租约成功后以限时 HTTPS 回连兑换，完成的轮换先落库再交付，即使浏览器已断开，服务端仍可完成写回。临时故障期间保留原凭据并退避，写入中断可在租约到期后读库再尝试。明确的 `invalid_grant` 仅由当前租约持有者终止会话。登出清空长期凭据与租约，迟到的结果无法写回。若旧 refresh token 已永久失效且没有可读的新版本，要求用户重新登录。[持久化租约](../../Radish.Auth.Persistence/OpenIddict/WebSessionStore.cs)
- 以后获准开启时，需配置 HTTPS 公开 `OpenIddict:Server:Issuer`、`OpenIddict__WebSession__Enabled`、`OpenIddict__WebSession__ClientSecret`、独立持久化的 `OpenIddict__WebSession__KeysPath`，以及用于保护密钥环的 `OpenIddict__WebSession__KeyCertificate__Path` 和 `Password`。轮换证书需要保留现有密钥的解密材料，可配置 `PreviousKeyCertificates`。在隔离环境通过 `Radish.DbMigrate apply` 后，须验证 Auth 容器可用目标站点的受信 HTTPS 地址访问 Gateway 的 `/connect/token`、持久化密钥环在重启后可读、实际 PostgreSQL 迁移和真实浏览器登录。该范围涉及服务启动、运行配置和数据库迁移，按实际环境单独授权。[密钥环配置](../../Radish.Auth/WebSessions/WebSessionKeyRing.cs)

### 普通 Web 前端与实时连接

- 普通 Web 页面在 HTTPS 同源 Gateway 读取 `/web/session/status`，按 `X-Radish-Web-Session-Mode` 选择会话。Auth 明确关闭新会话时，该端点返回 `404` 和 `legacy`；返回 `cookie` 的 `401` 表示访客。网络失败、代理错误和缺失模式的响应保留待恢复状态。Flutter、Console、历史 WebOS 和旧 OIDC 回调沿用各自入口。[会话 API](../../Frontend/radish.http/src/web-session.ts) · [浏览器入口策略](../../Frontend/radish.client/src/services/browserWebSession.ts)
- 启动、前台恢复、普通 Web API 的 401 和 Hub 使用同一内存凭据提供者。Cookie 会话恢复后清除旧普通 Web 的浏览器凭据缓存。自动恢复和定时刷新不计活动，前台真实输入通过 `/activity` 节流上报。服务端确认失效后清理当前和其他标签页，只广播退出事件。显式退出先执行同源 `/logout`，收到成功后再进入既有 OIDC 回流。Web 授权读请求可使用已轮换的令牌重试一次，授权写请求发生 401 时只更新凭据，原操作由调用方决定是否重试。[凭据状态机](../../Frontend/radish.client/src/services/webSession.ts) · [统一 HTTP 客户端](../../Frontend/radish.http/src/client.ts)
- `/hub/web/notification`、`/hub/web/chat` 和 `/hub/web/comment` 只接受已验签的 `radish-web` bearer，并让 SignalR 在认证到期时断开。Web 通过同一提供者取新令牌重连，对账通知，恢复当前聊天频道和帖子评论订阅。退出或会话明确失效时取消重连。旧 Hub 路径仍服务 Flutter、Console 和历史入口，评论游客继续使用 `/hub/comment`。[Hub 鉴权边界](../../Radish.Api/Security/WebSessionHubBoundary.cs) · [前端 Hub 选择](../../Frontend/radish.client/src/services/hubSession.ts)

候选 Compose 通过专用覆盖为 Auth 接入 Web 会话配置、持久密钥卷和证书。一次性 PostgreSQL 迁移、Auth 到 Gateway 的 HTTPS 回连、Auth 重建后的令牌轮换、两端的 Hub 重连及隔夜会话恢复已有实测记录。修复版已通过 PC 和移动浏览器的主动退出回流及跨标签退出复验。隔离回退中，关闭开关并撤销候选会话和对应授权，等已发出的短令牌到期后，旧版四个服务成功接管原库。两端均按旧版流程重新输入一次凭据，回到原页面。[隔离回退演练](../records/auth-session-candidate-2026-11-25.md#11-月-26-日隔离回退演练) · [配置入口](../guide/configuration.md#普通-web-会话候选) · [接下来的事项](../planning/current.md#接下来的事项)

<a id="web-session-compose-candidate"></a>

## 候选 Compose 运行方案

本轮使用 `520c8bbf` 的普通 Web 会话实现。在 OrbStack 单独创建 `radish-auth-session-<随机后缀>` 项目。临时目录权限为 `0700`，其中存放项目自己的 env 文件、初始化脚本、测试证书及两份浏览器资料。基础配置与[验收覆盖文件](../../Scripts/auth-session/compose.candidate.yaml)合并使用。

| 项目 | 本轮配置 |
| --- | --- |
| 入口 | `https://web-session.test:35443`。Chromium 仅在测试进程内将该域名映射到 `127.0.0.1`，Compose 网络内同名别名指向 Gateway。Gateway 内外端口均为 `35443`，宿主只发布 TCP `127.0.0.1:35443`。 |
| HTTPS 与密钥 | 生成一次性 CA，签发含该域名 SAN 的 Gateway 证书。Auth 通过 `SSL_CERT_FILE` 信任 CA 公钥，浏览器测试进程限定信任本次 Gateway 证书的公钥指纹。OIDC 签名、加密与 Web 密钥环保护各用独立证书。API 只挂载验签所需证书，Web 专用卷只挂载给 Auth。 |
| 存储 | PostgreSQL 16.15、Redis 7.4.12 使用固定摘要。四个项目卷分别用于 PostgreSQL、Redis、应用数据、Web 专用密钥。复制仓库的 PostgreSQL 初始化脚本到临时目录，迁移与种子数据只写入项目库。 |
| 镜像 | 五个候选角色为 `dbmigrate / api / auth / frontend / gateway`。从候选源码用已有依赖重建四个 .NET 宿主和两套前端静态资源，校验 linux-arm64 native 产物。复用[本地打包方式](../../Scripts/logging/collector-compose-exit-probe.mjs)，执行 `docker buildx build --builder orbstack --platform linux/arm64 --network none --pull=false --build-context probe-base=<缓存 OCI 布局> --load --tag <本项目角色标签> <临时构建目录>`。ASP.NET 与 Node 基础镜像[按摘要选择](../../Scripts/logging/compose-probe-config.mjs)。记录五个本地镜像 ID，旧库与旧页面对照使用[已固定摘要的正式镜像](../../Scripts/logging/compose-exit-config.mjs)中的 DbMigrate 与 Frontend。 |

下表中 `c` 代表 `docker --context orbstack compose --env-file "$probe_root/probe.env" --project-directory "$probe_root" -p "$probe_project" -f "$repo/Deploy/docker-compose.yaml" -f "$repo/Scripts/auth-session/compose.candidate.yaml"`。env 文件权限为 `0600`，使用本轮生成的随机凭据。两种开关分别执行 `c config --quiet`，检查合并结果的镜像、卷、只读挂载、网络与发布端口。启动前复查端口和项目名可用。

| 顺序 | 命令及通过条件 |
| --- | --- |
| 1. 从旧库升级 | `c up -d --no-build --pull never --wait postgres redis`。用旧 DbMigrate 执行一次 `c run --rm --no-deps --pull never dbmigrate apply` 建立旧结构。切回候选镜像，对同一数据库按 `doctor → apply → verify → apply → verify` 分别执行相同形式的单次命令。核对 Auth 迁移历史无重复、无待应用项，并查验两张 Web 表的 UTC 时间列为 `timestamp with time zone`。 |
| 2. 留存旧会话 | Auth 开关为 `false`，Frontend 选择旧正式镜像。执行 `c up -d --no-build --no-deps --pull never api auth frontend gateway`，等待 HTTPS 就绪。PC 和 mobile 分别通过真实登录取得旧会话，记录各自的受保护返回路径。 |
| 3. 验回连与密钥 | 仅本项目切为候选 Frontend 并开启 Auth 开关，执行 `c up -d --no-build --no-deps --pull never --force-recreate auth frontend`。先用独立测试资料完成真实登录与授权码回调，取得受保护接口响应。随后以相同参数只重建 Auth，等待原短令牌到期，原 Cookie 应能恢复同一账号并完成轮换。 |
| 4. 验两端并关页 | PC 用 `1920×1080`，移动 Chromium 用 `390×844 @ DPR 3`。两份旧资料均应只重登一次并回到原页面；再验刷新、通知／聊天／评论 Hub 到期重连和订阅恢复，以及登出后无法续期。完成后重新登录，分别关闭整个测试浏览器，保存签发与关闭时间。 |
| 5. 隔夜重开及清理 | 六个常驻容器、四个卷和两份已关闭的浏览器资料保留 12–24 小时。在时间窗内分别重开，以 `GET /api/v1/User/GetUserByHttpContext` 返回 `200` 且账号一致为准。记录实际间隔、是否出现额外登录及各项结果。随后关闭浏览器并执行 `c down --volumes --remove-orphans`，按本项目标签确认容器、网络、卷为零，并复核端口释放。删除本次五个镜像标签和核对过前缀的临时目录，仅保留脱敏结果。若启动后无法形成隔夜样本，登记失败步骤并当轮清理。 |

留存证据只含镜像标识、时刻、状态码、错误分类、计数和身份一致性等判断。7 天不活跃与 30 天绝对期限在 Auth 控制器和持久化层的六个时间点已用可控时钟验证。两种期限到点时，PC 与移动浏览器均已验证三类认证 Hub 断线、旧会话无法续连，以及交互认证后返回原频道。隔离回退后的登录与页面回流亦已通过。[服务端边界验证](../records/auth-session-candidate-2026-11-25.md#11-月-26-日服务端时钟边界验证) · [浏览器与 Hub 截止联验](../records/auth-session-candidate-2026-11-25.md#11-月-26-日浏览器与-hub-截止联验)

刷新轮换已落库但浏览器丢失响应时，PC 和移动端自动重试均取得有效短令牌，服务端仅轮换一次。两端随后误入登录页，直接重开原地址可沿用原会话。自动留在原页面仍待修复和复验，正式开关保持关闭。[丢失响应联验](../records/auth-session-candidate-2026-11-25.md#11-月-26-日刷新落库后浏览器丢失响应)

## 修复验收

| 场景 | 通过标准 |
| --- | --- |
| 隔夜恢复 | 社区 Web 在 PC 与 mobile 视图分别执行真实关页隔夜重开，间隔超过旧 25 分钟，以 12–24 小时为代表区间。access token 已过期，重开成功恢复同一账号，并通过一个受保护接口确认身份。另覆盖前台恢复和首个受保护请求触发刷新。 |
| 两层时限 | 用可控服务端时钟验证 7 天和 30 天截止前、到点与越界。覆盖临界活动与刷新竞争、活跃后的旧 refresh token、短 JWT 和 Web Hub 在截止时断开。定时刷新、心跳、自动恢复和客户端时间均不能延期，旧 Auth SSO Cookie 不能静默创建下一段 Web 会话。 |
| 故障与轮换 | 临时断网或 `5xx` 恢复后可继续有效会话。多标签、同标签和多 Auth 实例并发收敛。验证轮换结果已落库后响应丢失、持久化失败、进程重启和登出竞争，以及受保护写操作安全重试。 |
| 撤销与迁移 | 主动退出、账号停用或撤销后拒绝继续刷新。已有 access token 和连接受原截止约束。旧 Web 会话重登一次，公开页仍可读，受保护页登录后正确回流。覆盖旧缓存页面、回退、密钥轮换和备份恢复。 |
| 隔离与凭据 | 旧 Flutter 的 `radish-client` 无法换取新 Cookie 或进入 Web 专属期限。Console、Scalar、WebOS 历史登录路径及旧原生回调维持各自契约。验证 Cookie 属性、跨站请求、回调重放、客户端机密和长期凭据从浏览器存储及日志中缺失。 |

默认期限对照已完成。普通 Web 前端与 Hub 接入本轮通过 92 个前端与共享 HTTP 用例、121 个后端定向用例。本轮实际环境结果按场景登记于[候选隔离联验](../records/auth-session-candidate-2026-11-25.md)。[隔离复现记录](../records/auth-session-overnight-default-lifetime-2026-11-21.md)
