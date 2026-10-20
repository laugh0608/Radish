# L2 自有 Auth 账户与 IdleSession 日志治理

日期：2026-10-15（Asia/Shanghai）。本批经只读审计与项目所有者确认后实施，承接 [L2 事件契约第 67 节](../features/unified-logging-contract.md#67-l2-自有-auth-账户与-idlesession)。仅关闭列明调用点，L2 未整体关闭，`RadishLogging.Enabled=false` 保持。

## 范围与审计结论

- `AccountController` 原有 23 处调用：登录 7、注册 3、客户端摘要解析 7、returnUrl 解析 6；IdleSession 刷新令牌校验 1 处，共 24 处。
- 登录过程日志携带遮罩邮箱，仍保留域名；returnUrl Debug 携带完整 URL；注册及客户端解析记录展示名、客户端 ID、流水号或原异常。既有字符串查询参数脱敏不足以覆盖这些输入。
- 原“注册成功”发生在用户创建调用返回后，早于奖励与跳转构造。`GrantRegistrationRewardAsync` 可以返回已有流水；Controller 没有涵盖创建与奖励的整体事务，后续失败不表示前序写入已回滚。
- Auth 未接入 API 的错误处理中间件。登录无本地 catch，注册与客户端解析有既有异常消费；不能给登录额外记录 Error 后抛出，也不能将其传播验证写成 API 的 `http.failed` 完成。

## 实现与事件映射

AccountController 改用注入的 `ILogger<AccountController>`；`AuthAccountLog` 仅提交固定命名 EventId、安全阶段／原因、总耗时和异常分类。IdleSession 保留现有 ILogger。生成端不传原异常，不读取其 Message / Data / ToString。

`AuthenticationLogSink` 复用现有接线，在上下文合并后仅重建以下完整来源：

- `Radish.Auth.Controllers.AccountController`
- `Radish.Auth.OpenIddict.ValidateIdleSessionTokenRequestHandler`

| 来源 | Id / Name | eventCode | 生成等级 |
| --- | --- | --- | --- |
| AccountController | 1 / LoginRejected | `auth.account.login_rejected` | Debug / diagnostic |
| AccountController | 2 / LoginResultPrepared | `auth.account.login_result_prepared` | Information |
| AccountController | 3 / RegistrationUserCreateReturned | `auth.account.registration_user_create_returned` | Debug / diagnostic |
| AccountController | 4 / RegistrationRewardReturned | `auth.account.registration_reward_returned` | Debug / diagnostic |
| AccountController | 5 / RegistrationResultPrepared | `auth.account.registration_result_prepared` | Information |
| AccountController | 6 / RegistrationFailed | `auth.account.registration_failed` | Error |
| AccountController | 7 / ClientResolutionReturned | `auth.account.client_resolution_returned` | Debug / diagnostic |
| AccountController | 8 / ClientResolutionFailed | `auth.account.client_resolution_failed` | Warning |
| AccountController | 9 / ReturnUrlParseFailed | `auth.account.return_url_parse_failed` | Warning |
| IdleSession 校验 handler | 1 / IdleSessionRejected | `auth.idle_session.rejected` | Information |
| AccountController | 未知／错误身份 | `auth.account.unknown` | 保留原等级 |
| IdleSession 校验 handler | 未知／错误身份 | `auth.idle_session.unknown` | 保留原等级 |

共增加 12 个策略编码。Id / Name 必须同时匹配；错名、错误类型、重复身份字段不按模板或传入 EventCode 猜测。相邻类别与 AttachIdleSessionRefreshTokenHandler 不被接管。

输出保留时间、原等级、typed trace/span、固定 EventCode / SourceCategory=http / SourceContext / Diagnostic。按事件仅复制以下字段，其他上下文及原异常均丢弃：

| 字段 | 边界 |
| --- | --- |
| `authReason` | `invalid-return-url / invalid-credentials`；凭据错误不区分不存在与密码错误 |
| `authStage` | `validation / user-lookup / password-hash / user-create / reward / result-prepare`；表示最近进入的注册阶段，不是异常栈位置或事务提交证明 |
| `authClientOutcome` | `missing-id / not-found / resolved` |
| `failureKind` | 既有 RuntimeFailureSummary 封闭异常类别，不传原类型名或异常文本 |
| `durationMs` | 登录成功结果准备时的总耗时；long，0 至 9007199254740991 |

登录逐步过程和正常 returnUrl 解析日志移除。普通登录拒绝、非法回跳与客户端未找到不逐次输出 Warning；客户端读取失败降级为空摘要改为 Warning。候选 Debug 映射为 Info + diagnostic，仍要求 Development + Diagnostics；旧链保留其原等级过滤配置。正常结果准备及会话拒绝 Info 不因关闭 diagnostics 消失。

## 完成顺序与异常责任

- 登录保留用户查询、密码校验、确保奖励、角色读取、claims 与 SignInAsync 的原顺序。仅在 SignInAsync 返回且 IActionResult 构造完成后记录 result_prepared；失败／取消仍向外传播。
- 注册的用户创建和奖励返回各保留一条诊断。先构造成功提示与登录跳转，再记录注册 result_prepared；catch 先构造错误提示和注册跳转，再记录 registration_failed。catch 内本地化、TempData 或结果构造再次失败时不提前输出原异常已消费事件。
- 客户端摘要保留 returnUrl 优先、request query fallback、首个 client_id、RadishApplication 与通用 store 投影，以及异常／取消降级为空摘要的规则。query fallback 读取仍位于 resolver catch 之外；生成摘要成功后才记录 returned。
- IdleSession 先执行 Reject，再记录拒绝事件；策略、invalid_grant、错误说明、活动 claim 与 Attach handler 不变。
- 上述结果准备、调用返回和拒绝状态均不代表 HTTP／协议响应写入完成。业务身份、URL 与 TraceIdentifier 在原响应中的用途不变；日志裁剪不等于响应内容治理。
- 普通 WriteTo 输出异常由 Serilog 消费；AuditTo 输出异常可以聚合为 AggregateException 向调用方传播，不加吞错兜底。拒绝状态／登录会话已建立后输出失败不撤销这些状态；注册创建返回后的诊断若在 AuditTo 失败，既有 catch 可将其转换为注册错误结果，用户创建不会自动回滚。

## 验证结果

| 检查 | 结果 |
| --- | --- |
| `dotnet build Radish.slnx -c Debug --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false` | 通过，0 warning / 0 error |
| 本批新增测试 | 74 个通过，包含在下方扩大回归内 |
| 认证、OpenIddict、共享日志与相邻身份定向回归 | 368 个通过，0 failed / skipped，7.862 秒 |
| `npm run check:logging-contract` | 32 个通过，含 5 个新增共享样本 |
| `npm run check:identity-claims` | runtime + protocol 扫描通过 |
| `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` | 通过 |

新增测试按账户操作、客户端解析、IdleSession 和输出适配分组；四模式为旧链／候选链 × Development／Production，另覆盖候选 diagnostics=false、配置 WriteTo／AuditTo、未知严重事件、恶意标量 scope、错误身份、typed correlation 和不可读取异常。

测试使用真实 Controller／handler、内存 HTTP context、临时内存 DataProtection 与真实 Cookie handler、OpenIddict dispatcher；用户／币／客户端 store 使用替身。覆盖登录成功与拒绝、登录阶段异常、注册阶段失败及前序副作用、catch 二次失败、客户端两条投影／降级／取消、会话拒绝先于日志、与框架诊断共存等边界。

扩大回归首次在沙盒内停滞于既有 `ProducerLoggingTests.ApiPipeline_ShouldOwnOneSafeError_AfterTransactionRollback`，未计为通过；精确终止本轮进程后，经最小范围提权重跑同组用例，设 90 秒自动终止，正常完成。没有使用会绑定通信端口的 VSTest 入口。测试数据夹具的初次构造及 AuditTo 聚合异常断言已修正，最终通过以本轮重跑为准。

执行入口为测试输出目录中的 xUnit 进程内 runner：

```bash
dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 \
  -class 'Radish.Api.Tests.Auth.AuthAccountLoggingTests' \
  -class 'Radish.Api.Tests.Auth.AuthClientLoggingTests' \
  -class 'Radish.Api.Tests.Auth.AuthIdleSessionLoggingTests' \
  -class 'Radish.Api.Tests.Auth.AuthOwnedLogSinkTests' \
  -class 'Radish.Api.Tests.Controllers.AccountControllerTest' \
  -class 'Radish.Api.Tests.Auth.IdleSession*' \
  -class 'Radish.Api.Tests.ErrorHandling.Authentication*' \
  -class 'Radish.Api.Tests.ErrorHandling.OpenIddict*' \
  -class 'Radish.Api.Tests.RuntimeLog*' \
  -class 'Radish.Api.Tests.ProducerLoggingTests' \
  -class 'Radish.Api.Tests.Controllers.AuthorizationControllerTest' \
  -class 'Radish.Api.Tests.Controllers.UserInfoControllerTest' \
  -class 'Radish.Api.Tests.Security.ApiJwtValidationPolicyTests' \
  -class 'Radish.Api.Tests.Security.AuthProxyTransportContractTests' \
  -class 'Radish.Api.Tests.Security.OpenIddictTransportSecurityPolicyTests'
```

## 证据限制与后续

- returnUrl 解析 catch 没有稳定触发：普通非法百分号输入不会必然抛错。该事件仅有真实生成方法、身份及安全输出验证，不宣称解析器异常后的整个 fallback 已执行；未新增解析器抽象来人为制造分支。
- 新增的自有日志不传敏感 state 或异常对象，但输出 sink 不能阻止任意外部日志调用／enricher 在桥接前自行求值；本批不提供全局 ILogger 桥接前保护。
- 没有安装／还原依赖、启动项目服务／容器、绑定端口、访问真实数据库、生产或系统证书库。结果不替代完整 OIDC 登录、浏览器、网络、持久密钥、真实存储事务或并发验收。
- `Program.cs` ApplicationStarted 回调剩余 16 处 banner／配置摘要日志单独登记；`auth.seed.completed` 已治理，不重复迁移。OpenIddict Client／Validation／DataProtection 的实际接入、IdentityModel IIdentityLogger／EventSource、SelfLog、DiagnosticListener 及裸输出仍按当前规划只读核对后确认。
- 未变更认证规则、奖励规则、响应契约、数据模型、依赖版本或生产日志开关；未扩展 Windows P8-C、发布与部署范围。
