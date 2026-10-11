# L2 OpenIddict Core 日志治理（2026-10-11）

本批承接 [Server 批次](./unified-logging-l2-openiddict-server-2026-10-11.md)，仅扩展 Application / Authorization / Token manager 的日志身份与保护。项目所有者已确认三个精确来源、21 个编码、桥接前保护、四模式验证及文档同步；保持存储、缓存、认证和响应语义，不扩展自有日志或其他通道。

## 来源与编码

当前 Auth 使用 OpenIddict 7.2.0 的默认 Core manager 与 EF Core 集成，未替换 manager 或关闭实体缓存。源码日志调用和已安装 Core 程序集 IL 均确认 18 个数字事件；实际 Logger<T> 类别不含泛型参数。三个类别分别为：

- `OpenIddict.Core.OpenIddictApplicationManager`
- `OpenIddict.Core.OpenIddictAuthorizationManager`
- `OpenIddict.Core.OpenIddictTokenManager`

18 个已知事件包含 8 个 Information、4 个 Debug、6 个 Warning，各来源再加一个未知摘要。Scope manager 虽注入 logger，但当前版本没有直接日志调用；Core cache 和 EF store 没有本批直接 ILogger 来源。后者的 EF 框架日志不等同于 Core manager 日志。

以下简称分别对应上述三个精确类别：

| 来源 | EventId | 原等级 | EventCode |
| --- | --- | --- | --- |
| Application | `6159` | Warning | `oidc.core.application.public_client_secret_rejected` |
| Application | `6160` | Information | `oidc.core.application.client_secret_missing` |
| Application | `6161` | Information | `oidc.core.application.client_secret_invalid` |
| Application | `6202` | Information | `oidc.core.application.post_logout_redirect_uri_rejected` |
| Application | `6162` | Information | `oidc.core.application.redirect_uri_rejected` |
| Application | `6163` | Warning | `oidc.core.application.client_secret_verification_failed` |
| Application | `未知 / 无效身份` | 保留输入等级 | `oidc.core.application.unknown` |
| Authorization | `6164` | Information | `oidc.core.authorization.revoke_completed` |
| Authorization | `6165` | Debug | `oidc.core.authorization.revoke_concurrency` |
| Authorization | `6166` | Warning | `oidc.core.authorization.revoke_failed` |
| Authorization | `未知 / 无效身份` | 保留输入等级 | `oidc.core.authorization.unknown` |
| Token | `6168` | Information | `oidc.core.token.redeem_completed` |
| Token | `6169` | Debug | `oidc.core.token.redeem_concurrency` |
| Token | `6170` | Warning | `oidc.core.token.redeem_failed` |
| Token | `6171` | Information | `oidc.core.token.reject_completed` |
| Token | `6172` | Debug | `oidc.core.token.reject_concurrency` |
| Token | `6173` | Warning | `oidc.core.token.reject_failed` |
| Token | `6174` | Information | `oidc.core.token.revoke_completed` |
| Token | `6175` | Debug | `oidc.core.token.revoke_concurrency` |
| Token | `6176` | Warning | `oidc.core.token.revoke_failed` |
| Token | `未知 / 无效身份` | 保留输入等级 | `oidc.core.token.unknown` |

来源依据为官方 7.2.0 的 [Application manager](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Core/Managers/OpenIddictApplicationManager.cs)、[Authorization manager](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Core/Managers/OpenIddictAuthorizationManager.cs)和 [Token manager](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Core/Managers/OpenIddictTokenManager.cs)，并用当前已安装程序集核对，不按资源编号或消息模板推断事件身份。

## 实现边界

- `OpenIddictLogIdentity` 增加三个精确类别和 21 个编码。数字身份要求 Name 缺省；错误名称 / 类型、重复字段和未知 Id 均输出来源对应的 unknown。
- `OpenIddictLoggerFactory` 沿用 Server 的保护，在桥接前不读原 state、不调用原 formatter、不传递原异常；固定来源、事件身份与显式空 failureKind 防止 scope 补入。共享注册名改为 `ProtectOpenIddictLogging`，仍调用原 Serilog factory，保留既有健康保护及工厂所有权。
- `OpenIddictLogSink` 复用原输出链，Core 固定 `SourceCategory=application`。保留原时间、等级、typed trace/span、诊断属性和安全 failureKind；移除客户端 / token / authorization 标识、URI、密钥、异常正文与任意 scope。Server 的 185 个编码和 http 分类不变。
- 低于 Warning 的事件继续视为诊断；候选 Production 或 Diagnostics 关闭时抑制。并发异常不升格为 Error，严重未知事件保持可见。旧链最低等级策略保持。
- 没有修改 OpenIddict manager、EF store、缓存、认证配置、生产开关或协议响应代码。

## 保持的操作与失败语义

真实 manager 使用存储 / 缓存替身验证以下边界，持久化返回点由替身计数，不等同于真实数据库提交证据：

| 触发点 | 原控制流与本批保持的结果 |
| --- | --- |
| public client 校验 secret | 6159 Warning，返回 false |
| secret 缺失 / 不匹配 | 6160 / 6161 Information，返回 false |
| secret 哈希格式损坏 | 6163 Warning 后可继续产生 6161 Information，返回 false；不全局去重 |
| redirect / post-logout URI | 保持精确比较；仅原 native loopback 条件允许动态端口；path / query / fragment / userinfo 等差异仍拒绝 |
| Application 日志所需 ClientId 读取失败 | 原异常 / 取消传播，不生成该条拒绝日志；哈希格式错误的前一条 Warning 可能已发生 |
| manager 更新成功 | 校验 → cache remove → store update → cache add → ID 读取 → 完成日志 → true |
| UpdateAsync 内并发异常 | Debug 后返回 false，实体可能已改变；不声明回滚 |
| UpdateAsync 内非致命异常 / 取消 | Warning 后返回 false，前提是 catch 内 ID 读取及日志输出成功 |
| cache remove 失败 | 不调用 store update，Warning 后 false |
| cache add 失败 | store update 已返回，仍可能 Warning 后 false |
| 完成日志所需 ID 第一次读取失败 | 已完成更新；catch 再读 ID 成功时 Warning 后 false |
| catch 内 ID 读取再次失败 | 异常传播且没有该条日志；可替代原异常 |
| SetStatus / redemption date / 授权初始状态读取失败 | 位于更新 catch 外，原异常传播，无完成日志 |
| 致命异常 | 原框架 IsFatal 边界保持，异常传播 |
| authorization 已 revoked | 直接 true，不再次更新或写完成日志 |
| redemption date 已存在 / 关闭实体缓存 | 保留已有日期；按原选项跳过 cache remove / add |
| logger 禁用 | 框架仍会求值日志参数；不跳过 ID 读取与其失败 |
| WriteTo sink 抛出 | Serilog 消费输出异常，manager 可继续返回 true |
| AuditTo sink 抛出 | Serilog 的 AggregateException 按原链回到 manager；第一次完成日志失败可进入 catch，第二次警告日志失败继续传播 |

`*_completed` 只表示执行到 manager 原完成日志位置；不保证外层事务提交或 HTTP 完成。`*_failed` 不证明数据库未写入、实体未变化或缓存已回滚。本批不修复这些既有语义。

## Server 消费与响应

用真实 Core manager、存储 / 缓存替身与真实 Server handler 联查：

- `RedeemTokenEntry` 保持授权码兑换 false 时拒绝为 invalid_token，以及刷新令牌分支消费 false 的原差异。前置 SetStatus 异常仍传播，不变成协议拒绝。
- `Revocation.RevokeToken` 保持 false 对应 unsupported_token_type；`NormalizeErrorResponse` 保持该错误，仅按原规则清除 invalid_token 的错误字段。
- `RejectDeviceCodeEntry`、`RejectUserCodeEntry` 与 `RedeemLogoutTokenEntry` 保持忽略 false、传播未消费异常的控制流。device 分支只是条件性测试夹具，没有启用对应产品端点或流程。
- principal、令牌状态与持久化调用次数同步断言；既有 Server 请求 / 认证 / JWT / 响应测试继续回归。

依据见官方 [Server handlers](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Server/OpenIddictServerHandlers.cs)与[撤销处理器](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Server/OpenIddictServerHandlers.Revocation.cs)。

## 验证

四模式为旧链 / 候选链 × Development / Production；身份测试另交叉 Diagnostics 开关。真实 Application / Authorization / Token manager 的行为测试覆盖全部 18 个已知事件，身份清单由已安装程序集的 IL 反向核对。

- 所有 21 个编码覆盖配置 WriteTo / AuditTo。验证未知严重事件、严格身份、时间 / 等级 / typed trace/span、Core 来源分类与不可读 state / formatter / exception；scope 冲突不能补入原内容。
- 精确来源以外的 Scope、泛型拼写、相邻自定义类别、EF store、自有 Auth 日志及既有 Server 之外通道保持透传。
- 共享宿主只 Build / Dispose，复用 Server 测试并加入三个 Core 来源；候选 logger 仍由 session 保留，旧链 sink 随宿主释放一次，健康保护保持。旧链 SelfLog 的文件初始化未移除。
- 输出失败测试直接比较有 / 无保护的真实 manager 结果，区分 WriteTo 与 AuditTo；不改变异常包装或吞吐边界。

| 检查 | 结果 |
| --- | --- |
| Core 新增定向测试 | 46 / 46 |
| OpenIddict 合计定向测试 | 119 / 119；失败、跳过、未运行均为 0 |
| 扩大日志 / 健康 / 认证回归 | 1340 / 1340；失败、跳过、未运行均为 0 |
| Node 公共日志契约 | 27 / 27 |
| solution 无还原、警告视为错误构建 | 0 警告、0 错误 |
| docs / changed hygiene / diff check | 通过；日志指南与记录索引仅保留既有篇幅软提醒 |
| 公共策略差异 | 仅新增 21 个 Core 编码；既有策略字段与事件保持 |

扩大回归使用已安装 xUnit v3 进程内入口，在 `Radish.Api.Tests/bin/Debug/net10.0` 执行：

```bash
dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 \
  -class 'Radish.Api.Tests.ErrorHandling.*' \
  -class 'Radish.Api.Tests.SignalRFramework*' \
  -class 'Radish.Api.Tests.RuntimeLog*' \
  -class 'Radish.Api.Tests.Security.ApiJwtValidationPolicyTests' \
  -class 'Radish.Api.Tests.Security.AuthorizationBoundaryTests' \
  -class 'Radish.Api.Tests.HealthChecks.*' \
  -class 'Radish.Api.Tests.Auth.IdleSession*' \
  -class 'Radish.Api.Tests.Controllers.AuthorizationControllerTest' \
  -class 'Radish.Api.Tests.Controllers.UserInfoControllerTest' \
  -class 'Radish.Api.Tests.Controllers.AccountControllerTest' \
  -class 'Radish.Api.Tests.Security.OpenIddictTransportSecurityPolicyTests' \
  -class 'Radish.Api.Tests.Security.AuthProxyTransportContractTests'
```

定向测试在沙盒内通过。扩大回归首次在既有 `ApiErrorContractTest.ExceptionPipeline_ShouldBuildAndHandleUnknownApiException` 停滞，55 秒超时终止，未计为通过；最小提权重跑同组用例，设置 90 秒超时清理，30.145 秒完成。

构建命令为 `dotnet build Radish.slnx -c Debug --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false`。不使用会绑定通信端口的 VSTest 入口。

## 未扩展范围

自有 IdleSession / AccountController、Client / Validation / DataProtection 集成、IdentityModel 的 IIdentityLogger / EventSource 和其他非 Serilog 通道继续独立确认。既有 auth.seed.completed 不重复治理。

未安装 / 还原依赖、启动服务、绑定端口或访问真实数据库、证书库、外部网络。没有真实并发竞争、事务提交、完整 OIDC 登录或生产采集证据。生产候选开关保持关闭，L2 尚未整体完成。
