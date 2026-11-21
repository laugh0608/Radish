# 隔夜会话排查与验收方案

2026-11-21 · `dev@f6f9f008` · 六项提案第 2 项。只读排查及默认期限隔离对照已完成，修复待评审。[当前进行中](../planning/current.md) · [既有不活跃规则](../guide/auth-idle-session.md) · [隔离复现记录](../records/auth-session-overnight-default-lifetime-2026-11-21.md)

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

## 修复方案（待评审）

1. **限定社区 Web 的会话策略。** 保留 5 分钟 access token，建议将普通 Web 的不活跃上限设为 7 天，绝对上限设为初次授权后的 30 天，由服务端执行。每次签发的 refresh token 截止不晚于两者中较早的一项。服务端在确认有效会话后记录可认可的交互时间，后台定时刷新与心跳不续长不活跃窗口。首次恢复先核对旧窗口，再记录新的交互。绝对起点随轮换保留。现有活动请求参数可作为提示，服务端应限制可采信的范围。
2. **按登记的回调来源绑定策略。** `radish-client` 同时承载 Web 和 Flutter，直接按客户端 ID 改期限会一起作用于两者。优先评审用服务端已验证的 Web 回调在初次授权时确定策略，将可信标记通过授权码与轮换保留到 refresh token。若现有授权链不能可靠保留来源，再评审独立 Web 客户端 ID 的迁移。Console、Scalar 与原生策略分别适用既有登记。[OIDC 客户端登记](../../Radish.Auth/OpenIddict/OpenIddictSeedHostedService.cs)
3. **长期凭据和恢复路径一并评审。** 现有 refresh token 可被页面脚本读取，延长后的暴露时间也会增长。建议由同源服务端持有长期刷新凭据，浏览器仅持有 `HttpOnly`、`Secure` 的会话 Cookie，并落实 CSRF 防护与服务端撤销。启动恢复、定时任务和受保护请求共享刷新协调，轮换后多标签使用最新结果。网络错误和临时 `5xx` 保留恢复能力，明确过期、撤销或持久凭据缺失再进入重登。策略变化后，已经过期的旧凭据需要重新登录。对退出和停用，明确验证 refresh 拒绝及已签发 access token 的剩余有效期。

## 修复验收

| 场景 | 通过标准 |
| --- | --- |
| 隔夜恢复 | 社区 Web 在 PC 与 mobile 视图分别执行真实关页隔夜重开，间隔超过旧 25 分钟，以 12–24 小时为代表区间。access token 已过期，重开成功恢复同一账号，并通过一个受保护接口确认身份。另覆盖前台恢复和首个受保护请求触发刷新。 |
| 两层时限 | 用可控服务端时钟在边界前后验证 7 天不活跃和 30 天硬上限。验证 refresh 签发截止、轮换后初次授权时间与有效活动时间，以及定时刷新、心跳、恢复事件和超范围活动时间上报。 |
| 故障与轮换 | 临时断网或 `5xx` 恢复后可继续有效会话，多标签与同标签并发恢复收敛到最新轮换结果。验证明确失效的凭据，以及失败重试时受保护写操作不会重复提交。 |
| 撤销与迁移 | 主动退出、账号停用或撤销后拒绝继续刷新，验证旧 access token 是否可用至原期限。已有过期凭据重登一次后按新策略签发。撤销后可匿名浏览公开页，受保护页仍要求登录。 |
| 范围回归 | Console、Scalar 与原生登记的令牌期限和回调契约符合既有口径。若引入 Cookie，覆盖其属性、跨站请求防护、登出与服务端撤销。 |

只读排查执行的 19 个后端与 26 个前端定向用例全部通过。默认期限对照已完成，修复验收待实施后执行。[隔离复现记录](../records/auth-session-overnight-default-lifetime-2026-11-21.md)
