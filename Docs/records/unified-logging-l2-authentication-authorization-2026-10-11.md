# L2 认证／授权日志治理（2026-10-11）

本批按项目所有者确认范围，增加 4 个精确框架来源、18 个已知来源 / 事件组合、4 个来源专属未知摘要，以及 API 两处 JWT 回调，共 24 个公共编码。只调整日志输出，保留认证结果、授权策略、401 / 403、Cookie / Header、异常和取消传播。生产 `RadishLogging.Enabled=false` 保持，L2 尚未整体关闭。

## 调用关系与版本

- API 使用 JwtBearer、HangfireDashboard Cookie 和授权策略；Auth 使用 Cookie、授权与 OpenIddict Server。`AuthenticationHandler` 用具体 handler 类型创建 logger，不能把基类的远程登录事件全部映射给 JWT / Cookie。
- `DefaultAuthorizationServiceImpl` 实际注入 `ILogger<DefaultAuthorizationService>`；授权中间件内部实现注入 `ILogger<AuthorizationMiddleware>`，不按实现类名推断来源。
- Gateway 的 YARP 注册授权服务；`WebApplication` 会根据注册服务自动插入中间件，不能用缺少显式 `UseAuthorization` 排除调用。没有因此给 Gateway 增加 JWT 或 Cookie 注册。
- 本机实际程序集：JwtBearer `10.0.0`，Authentication / Cookies / Authorization / Authorization.Policy `10.0.8`。元数据与真实生成器同时验证身份，不只用合成消息匹配测试。

官方依据：[认证基类](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Security/Authentication/Core/src/AuthenticationHandler.cs)、[共同事件](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Security/Authentication/Core/src/LoggingExtensions.cs)、[JWT 处理器](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Security/Authentication/JwtBearer/src/JwtBearerHandler.cs)、[Cookie 处理器](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Security/Authentication/Cookies/src/CookieAuthenticationHandler.cs)、[授权服务事件](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Security/Authorization/Core/src/LoggingExtensions.cs)、[授权中间件](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Security/Authorization/Policy/src/AuthorizationMiddleware.cs)。

## 精确类别与编码

类别表中的框架名称均加前缀 `Microsoft.AspNetCore.`。仅匹配完整 SourceContext + EventId + EventName；相邻类别、错名和未来事件不靠模板内容推断。

| 来源简称 | 精确类别后缀 | 已知数 | 未知编码 |
| --- | --- | ---: | --- |
| JWT | `Authentication.JwtBearer.JwtBearerHandler` | 9 | `auth.jwt_unknown` |
| Cookie | `Authentication.Cookies.CookieAuthenticationHandler` | 7 | `auth.cookie_unknown` |
| Authorization | `Authorization.DefaultAuthorizationService` | 2 | `auth.authorization_unknown` |
| Middleware | `Authorization.AuthorizationMiddleware` | 0 | `auth.authorization_middleware_unknown` |

| 来源 | ID / 实际 Name | 原等级 | 公共编码 |
| --- | --- | --- | --- |
| JWT | `1 / TokenValidationFailed` | Information | `auth.jwt_validation_failed` |
| JWT | `2 / TokenValidationSucceeded` | Debug | `auth.jwt_validation_succeeded` |
| JWT | `3 / ProcessingMessageFailed` | Error | `auth.jwt_processing_failed` |
| JWT | `4 / ForbiddenResponseHasStarted` | Debug | `auth.jwt_forbidden_response_started` |
| JWT | `7 / AuthenticationSchemeNotAuthenticatedWithFailure` | Information | `auth.jwt_authentication_failed` |
| JWT | `8 / AuthenticationSchemeAuthenticated` | Debug | `auth.jwt_authenticated` |
| JWT | `9 / AuthenticationSchemeNotAuthenticated` | Debug | `auth.jwt_not_authenticated` |
| JWT | `12 / AuthenticationSchemeChallenged` | Information | `auth.jwt_challenged` |
| JWT | `13 / AuthenticationSchemeForbidden` | Information | `auth.jwt_forbidden` |
| Cookie | `7 / AuthenticationSchemeNotAuthenticatedWithFailure` | Information | `auth.cookie_authentication_failed` |
| Cookie | `8 / AuthenticationSchemeAuthenticated` | Debug | `auth.cookie_authenticated` |
| Cookie | `9 / AuthenticationSchemeNotAuthenticated` | Debug | `auth.cookie_not_authenticated` |
| Cookie | `10 / AuthenticationSchemeSignedIn` | Information | `auth.cookie_signed_in` |
| Cookie | `11 / AuthenticationSchemeSignedOut` | Information | `auth.cookie_signed_out` |
| Cookie | `12 / AuthenticationSchemeChallenged` | Information | `auth.cookie_challenged` |
| Cookie | `13 / AuthenticationSchemeForbidden` | Information | `auth.cookie_forbidden` |
| Authorization | `1 / UserAuthorizationSucceeded` | Debug | `auth.authorization_succeeded` |
| Authorization | `2 / UserAuthorizationFailed` | Information | `auth.authorization_failed` |

Middleware 当前直接调用 LogDebug，EventId 为 0 且没有 EventName，只进入上述来源摘要。`RemoteAuthenticationFailed`、correlation cookie、远程 sign-in / access-denied 事件不是当前 JWT / Cookie 调用，不加入这 18 个映射。

API 自有类别为 `Radish.Api.Security.ApiJwtBearerEvents`，只接收以下两个明确 EventCode，不将其计作额外框架来源：

| 回调 | 等级 | 公共编码 | 语义 |
| --- | --- | --- | --- |
| AuthenticationFailed | Warning | `auth.jwt_failure_observed` | 进入认证失败回调；按异常类型生成受控 failureKind |
| Challenge | Warning | `auth.jwt_challenge_observed` | 进入 challenge 回调；不表示 401 已成功写入 |

## 实现与行为边界

- 新增 `AuthenticationLogSink`，接入 `RuntimeLoggingConfiguration` 和旧链 `SerilogSetup` 的代码 / 配置 WriteTo、AuditTo；在上下文合并后重建事件。保留原等级、时间、类型化 trace/span、固定来源、EventCode、Diagnostic 和受控 failureKind，不输出 token、Cookie、claims、路径、错误文本、requirements、原异常或任意 scope。
- Debug / Information 保持诊断语义；候选 Production 或关闭诊断时抑制诊断事件，Warning / Error 与未知严重事件保持可见。旧链既有等级门禁不变，不因治理降低错误等级或隐藏失败。
- 将 Program 中四个 JWT 回调提取为 `ApiJwtBearerEvents`。只改变两处日志；query token 仍仅用于 notification / chat / comment 三个既有 Hub 路径段，空白 query 仍允许框架使用 Authorization Header。
- API challenge 仍先调用 HandleResponse，再写 MessageModel 401；API forbid 仍写 403。非 API 与 `/api/health` 继续交给框架默认响应。观察事件仍在写入前；写入失败不会生成之后的 `auth.jwt_challenged`，本批不增加 catch、重试、去重或成功事件。
- JWT 校验失败仍返回 Failure，回调主动返回 Fail / NoResult 仍优先；处理阶段异常若未被回调消费，继续抛原异常实例。Cookie 登录 / 退出、普通页面重定向及 Hangfire 401 / 403 策略保持。
- 认证基类在 logger 前读取 Failure.Message；授权服务在 logger 前格式化失败 requirements。适配器不控制这些前置求值，回归保留其异常传播，不宣称本批消除了该阶段的二次失败。
- 不承诺一个请求只有一条日志：校验、回调观察、认证结果、授权拒绝及外层 HTTP 消费具有不同边界。

## 四模式验证

四模式为旧 / 候选 × Development / Production。候选每次显式设置并核对 mode；生成器回归另外交叉 Diagnostics 开关。

- 真实 LoggerMessage 方法覆盖全部 18 个目标来源 / 事件组合；核对元数据、等级、时间、类型化 trace/span、未知身份、错名 / 相邻来源及不能格式化的异常 / 对象。
- 配置 WriteTo / AuditTo 分别覆盖全部 24 个编码，并验证敏感 scope 不进入输出。
- 真实 JWT 处理器验证内存签名 token、过期拒绝、无凭据、结果缓存、Hub query 范围、API / 非 API challenge 与 forbid、已开始响应、TokenValidated Fail / NoResult、异常回调消费和未消费异常的传播。
- 真实 Cookie 处理器验证登录发 cookie、读取票据、退出清除、无 cookie / 损坏 cookie、普通页面跳转、Hangfire 状态，以及签入回调失败不产生完成事件。
- 真实 Authentication / Authorization 中间件验证策略成功继续下游、已认证拒绝 403、未认证 challenge 401、匿名端点跳过拒绝、下游异常原样传播；另验证上述 Message / requirements 的前置求值失败。
- 响应写入使用受控内存流：验证回调观察发生在写入前、取消状态传到写入层、IO / 取消异常仍传播。异常对象身份不等于底层取消令牌身份；StreamPipeWriter 可包装 token。
- DI 和 DefaultHttpContext 不启动宿主；Cookie 使用临时内存 DataProtection，JWT 使用内存密钥和 StaticConfigurationManager，不访问证书库、密钥目录、发现端点或真实数据库。

验证结果：

| 检查 | 结果 |
| --- | --- |
| 本批认证／授权用例（包含在扩大回归中） | 88 / 88，通过四模式、身份和配置输出检查 |
| ErrorHandling、SignalRFramework、RuntimeLog 及既有 JWT 策略 / 授权边界回归 | 1052 / 1052；失败、跳过、未运行均为 0 |
| Node 公共日志契约 | 27 / 27 |
| `dotnet build Radish.slnx -c Debug --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false` | 0 警告、0 错误 |
| `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` | 通过；日志指南与记录索引保留既有篇幅软提醒 |
| 公共策略差异核对 | 恰好新增 24 个 auth 编码，既有事件及其他策略字段不变 |

测试使用已安装 xUnit v3 的进程内入口，工作目录为 `Radish.Api.Tests/bin/Debug/net10.0`：

```bash
dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 \
  -class 'Radish.Api.Tests.ErrorHandling.*' \
  -class 'Radish.Api.Tests.SignalRFramework*' \
  -class 'Radish.Api.Tests.RuntimeLog*' \
  -class 'Radish.Api.Tests.Security.ApiJwtValidationPolicyTests' \
  -class 'Radish.Api.Tests.Security.AuthorizationBoundaryTests'
```

最初的沙盒扩大回归停滞，已终止本轮测试进程，未计为通过。随后经最小提权重跑同一组进程内测试，并由外层进程监督加 90 秒超时与清理；24.409 秒完成。全过程未使用会绑定通信端口的 VSTest 入口，未安装 / 还原依赖、启动项目服务或绑定网络端口。

## 剩余来源与非 Serilog 通道

本段记录只读盘点结论，不扩大本批实施或运行授权。

- 路由：EndpointRoutingMiddleware、EndpointMiddleware、DfaMatcher，以及 Auth 重定向使用的 DefaultLinkGenerator 均待治理；DefaultLinkParser 未找到项目显式调用，保留条件性范围。不能把 endpoint 的 Executed 事件解释为业务成功。
- 宿主健康：`DefaultHealthCheckService` 有 100–105 六个事件身份，103 / HealthCheckEnd 按 Healthy / Degraded / Unhealthy 使用 Debug / Warning / Error；Data 可包含任意对象。当前未发现 IHealthCheckPublisher 注册，不把后台 publisher 计作已触发调用。YARP 健康适配和命名 HttpClient 适配不覆盖此服务日志。
- OpenIddict Server 使用 `OpenIddict.Server.OpenIddictServerDispatcher` 类别，独立于本批四来源；Auth 账户控制器自有业务日志也不因本批自动完成治理。
- IdentityModel 的 EventSource 和独立 IIdentityLogger、框架 DiagnosticListener / Activity / Meter 不经过本适配器。路由诊断可携带 HttpContext；仓库未见主动注册外部采集器，不等于运行中没有外部订阅者。
- 候选 SelfLog 已转为固定故障摘要，旧链 SelfLog 仍写内部诊断文件；程序集加载失败路径还有直接 Console 输出。两者继续独立治理，不因本批声明安全。
- 后续建议按路由、宿主健康、OpenIddict 与非 Serilog 专项分别确认。真实项目宿主、OIDC 登录全流程、持久 Cookie 密钥、网络 / DNS / TLS / 浏览器、数据库权限数据及生产外部采集均未验收。
