# L2 OpenIddict Server 日志治理（2026-10-11）

本批按项目所有者确认范围，治理 2 个精确来源、183 个已知事件身份和 2 个未知摘要，共 185 个公共编码；增加桥接前定向保护，保持认证、令牌和响应语义。生产 `RadishLogging.Enabled=false` 保持，L2 尚未整体关闭。

## 调用关系与版本

- Auth 当前注册 Core + Server，使用授权码、刷新令牌与 client credentials 流程，以及 authorize / token / endsession / userinfo / introspect / revoke 端点；authorize / endsession / userinfo 启用 passthrough。API 的 JwtBearer 边界不在本批改写。
- 已安装 `OpenIddict.Server` / `Server.AspNetCore` 为 `7.2.0+9e528e8beee548cf560dafd29c482bbecd9212a8`，Authentication 基类为 ASP.NET Core `10.0.8`，Serilog 桥接为 `9.0.0`。
- OpenIddictServerFactory 把 ILogger<OpenIddictServerDispatcher> 放入 transaction，Server 和 ASP.NET Core 扩展处理器共用该 logger；不能按处理器类名猜测来源。ASP.NET Core handler 则使用自身类别承接 AuthenticationHandler 的基类事件。
- 依据：[Server factory](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Server/OpenIddictServerFactory.cs)、[dispatcher](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Server/OpenIddictServerDispatcher.cs)、[ASP.NET Core handler](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Server.AspNetCore/OpenIddictServerAspNetCoreHandler.cs)及已安装程序集 IL / LoggerMessage 元数据。

## 精确身份与公共编码

下表 Server 为 `OpenIddict.Server.OpenIddictServerDispatcher`，Handler 为 `OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreHandler`。Server 的 Name 均为空，Handler 的 Name 为实际生成器名称。

当前 Server 两个程序集合计 237 个数字 EventId 调用点、178 个不同身份；源码的 6072 仅属于另一编译分支，当前目标不包含。保留同来源下设备流、PAR、token exchange 等条件性身份映射，不因此启用产品功能或宣称这些端点已运行验收。资源 ID 不一定等于事件 Id（例如 6285 使用 ID6281），不按资源文本识别事件。

| 来源 | Id / Name | 原等级 | 公共编码 |
| --- | --- | --- | --- |
| Server | `6000` | Trace | `oidc.server.token_validation_failed` |
| Server | `6001` | Trace | `oidc.server.token_validated` |
| Server | `6002` | Information | `oidc.server.redeemed_token_rejected` |
| Server | `6003` | Information | `oidc.server.inactive_token_rejected` |
| Server | `6004` | Information | `oidc.server.rejected_token_refused` |
| Server | `6005` | Information | `oidc.server.invalid_token_rejected` |
| Server | `6006` | Information | `oidc.server.invalid_authorization_rejected` |
| Server | `6007` | Information | `oidc.server.authorization_created_without_application` |
| Server | `6008` | Information | `oidc.server.authorization_created_for_application` |
| Server | `6009` | Debug | `oidc.server.access_claim_excluded` |
| Server | `6010` | Debug | `oidc.server.access_scopes_restricted` |
| Server | `6011` | Debug | `oidc.server.identity_claim_excluded` |
| Server | `6012` | Trace | `oidc.server.token_entry_created` |
| Server | `6013` | Trace | `oidc.server.jwt_created` |
| Server | `6014` | Trace | `oidc.server.token_payload_attached` |
| Server | `6015` | Trace | `oidc.server.token_reference_attached` |
| Server | `6021` | Trace | `oidc.server.device_token_payload_updated` |
| Server | `6030` | Information | `oidc.server.authorization_request_extracted` |
| Server | `6031` | Information | `oidc.server.authorization_request_validated` |
| Server | `6032` | Information | `oidc.server.authorization_parameter_unsupported` |
| Server | `6033` | Information | `oidc.server.authorization_parameter_missing` |
| Server | `6034` | Information | `oidc.server.authorization_redirect_uri_invalid` |
| Server | `6035` | Information | `oidc.server.authorization_redirect_fragment_rejected` |
| Server | `6036` | Information | `oidc.server.authorization_response_type_unsupported` |
| Server | `6037` | Information | `oidc.server.authorization_response_mode_incompatible` |
| Server | `6038` | Information | `oidc.server.authorization_response_mode_unsupported` |
| Server | `6039` | Information | `oidc.server.authorization_scope_missing` |
| Server | `6040` | Information | `oidc.server.authorization_prompt_combination_invalid` |
| Server | `6041` | Information | `oidc.server.authorization_pkce_method_unsupported` |
| Server | `6042` | Information | `oidc.server.authorization_pkce_parameters_incompatible` |
| Server | `6043` | Information | `oidc.server.authorization_pkce_response_incompatible` |
| Server | `6045` | Information | `oidc.server.authorization_confidential_access_token_rejected` |
| Server | `6046` | Information | `oidc.server.authorization_redirect_uri_rejected` |
| Server | `6047` | Information | `oidc.server.authorization_scopes_invalid` |
| Server | `6048` | Information | `oidc.server.authorization_endpoint_denied` |
| Server | `6049` | Information | `oidc.server.authorization_code_grant_denied` |
| Server | `6050` | Information | `oidc.server.authorization_implicit_grant_denied` |
| Server | `6051` | Information | `oidc.server.authorization_hybrid_grant_denied` |
| Server | `6052` | Information | `oidc.server.authorization_scope_denied` |
| Server | `6053` | Information | `oidc.server.endpoint_matched` |
| Server | `6054` | Information | `oidc.server.device_request_extracted` |
| Server | `6055` | Information | `oidc.server.device_request_validated` |
| Server | `6057` | Information | `oidc.server.device_scopes_invalid` |
| Server | `6062` | Information | `oidc.server.device_endpoint_denied` |
| Server | `6063` | Information | `oidc.server.device_scope_denied` |
| Server | `6064` | Information | `oidc.server.verification_request_extracted` |
| Server | `6065` | Information | `oidc.server.verification_request_validated` |
| Server | `6066` | Information | `oidc.server.configuration_request_extracted` |
| Server | `6067` | Information | `oidc.server.configuration_request_validated` |
| Server | `6068` | Information | `oidc.server.jwks_request_extracted` |
| Server | `6069` | Information | `oidc.server.jwks_request_validated` |
| Server | `6070` | Warning | `oidc.server.jwk_parameter_missing` |
| Server | `6071` | Information | `oidc.server.signing_key_unsupported` |
| Server | `6073` | Warning | `oidc.server.rsa_parameters_unavailable` |
| Server | `6074` | Warning | `oidc.server.ec_parameters_unavailable` |
| Server | `6075` | Information | `oidc.server.token_request_extracted` |
| Server | `6076` | Information | `oidc.server.token_request_validated` |
| Server | `6077` | Information | `oidc.server.token_parameter_missing` |
| Server | `6078` | Information | `oidc.server.token_grant_unsupported` |
| Server | `6079` | Information | `oidc.server.resource_owner_credentials_missing` |
| Server | `6080` | Information | `oidc.server.token_scopes_invalid` |
| Server | `6086` | Information | `oidc.server.token_endpoint_denied` |
| Server | `6087` | Information | `oidc.server.token_grant_denied` |
| Server | `6088` | Information | `oidc.server.token_special_scope_denied` |
| Server | `6089` | Information | `oidc.server.token_scope_denied` |
| Server | `6090` | Information | `oidc.server.token_client_identifier_missing` |
| Server | `6091` | Warning | `oidc.server.token_presenter_mismatch` |
| Server | `6092` | Information | `oidc.server.token_parameter_mismatch` |
| Server | `6093` | Information | `oidc.server.token_verifier_without_challenge` |
| Server | `6094` | Information | `oidc.server.token_parameter_disallowed` |
| Server | `6095` | Information | `oidc.server.token_parameter_invalid` |
| Server | `6096` | Information | `oidc.server.introspection_request_extracted` |
| Server | `6097` | Information | `oidc.server.introspection_request_validated` |
| Server | `6098` | Information | `oidc.server.introspection_parameter_missing` |
| Server | `6103` | Information | `oidc.server.introspection_endpoint_denied` |
| Server | `6104` | Information | `oidc.server.introspection_token_type_unsupported` |
| Server | `6105` | Information | `oidc.server.introspection_claims_excluded_for_audience` |
| Server | `6106` | Warning | `oidc.server.introspection_access_token_party_mismatch` |
| Server | `6107` | Information | `oidc.server.introspection_claims_excluded_for_public_client` |
| Server | `6108` | Warning | `oidc.server.introspection_refresh_token_party_mismatch` |
| Server | `6109` | Information | `oidc.server.revocation_request_extracted` |
| Server | `6110` | Information | `oidc.server.revocation_request_validated` |
| Server | `6111` | Information | `oidc.server.revocation_parameter_missing` |
| Server | `6116` | Information | `oidc.server.revocation_endpoint_denied` |
| Server | `6117` | Information | `oidc.server.revocation_token_type_unsupported` |
| Server | `6118` | Information | `oidc.server.device_grant_denied` |
| Server | `6119` | Warning | `oidc.server.revocation_access_token_party_mismatch` |
| Server | `6120` | Information | `oidc.server.device_special_scope_denied` |
| Server | `6121` | Warning | `oidc.server.revocation_refresh_token_party_mismatch` |
| Server | `6122` | Information | `oidc.server.revocation_token_identifier_missing` |
| Server | `6123` | Information | `oidc.server.revocation_token_not_found` |
| Server | `6124` | Information | `oidc.server.end_session_request_extracted` |
| Server | `6125` | Information | `oidc.server.end_session_request_validated` |
| Server | `6126` | Information | `oidc.server.end_session_redirect_uri_invalid` |
| Server | `6127` | Information | `oidc.server.end_session_redirect_fragment_rejected` |
| Server | `6128` | Information | `oidc.server.end_session_redirect_uri_rejected` |
| Server | `6129` | Information | `oidc.server.userinfo_request_extracted` |
| Server | `6130` | Information | `oidc.server.userinfo_request_validated` |
| Server | `6131` | Information | `oidc.server.userinfo_parameter_missing` |
| Server | `6132` | Debug | `oidc.server.handler_exception_observed` |
| Server | `6133` | Debug | `oidc.server.handler_returned` |
| Server | `6134` | Debug | `oidc.server.event_handled` |
| Server | `6135` | Debug | `oidc.server.event_skipped` |
| Server | `6136` | Debug | `oidc.server.event_rejected` |
| Server | `6137` | Information | `oidc.server.http_method_rejected` |
| Server | `6138` | Information | `oidc.server.http_header_missing` |
| Server | `6139` | Information | `oidc.server.http_header_invalid` |
| Server | `6140` | Information | `oidc.server.multiple_client_credentials_rejected` |
| Server | `6141` | Information | `oidc.server.challenge_response_prepared` |
| Server | `6142` | Information | `oidc.server.json_response_prepared` |
| Server | `6143` | Information | `oidc.server.text_response_prepared` |
| Server | `6144` | Information | `oidc.server.redirect_response_prepared` |
| Server | `6145` | Information | `oidc.server.empty_response_prepared` |
| Server | `6147` | Information | `oidc.server.form_post_response_prepared` |
| Server | `6148` | Information | `oidc.server.query_response_prepared` |
| Server | `6149` | Information | `oidc.server.fragment_response_prepared` |
| Server | `6151` | Information | `oidc.server.end_session_redirect_prepared` |
| Server | `6167` | Warning | `oidc.server.ec_curve_unavailable` |
| Server | `6177` | Information | `oidc.server.authorization_response_type_denied` |
| Server | `6181` | Information | `oidc.server.authorization_uri_parameter_forbidden` |
| Server | `6197` | Warning | `oidc.server.authorization_hint_party_mismatch` |
| Server | `6198` | Warning | `oidc.server.end_session_hint_party_mismatch` |
| Server | `6212` | Information | `oidc.server.authorization_response_combination_invalid` |
| Server | `6220` | Information | `oidc.server.authentication_parameter_missing` |
| Server | `6221` | Information | `oidc.server.authentication_client_not_found` |
| Server | `6222` | Information | `oidc.server.public_client_credentials_grant_rejected` |
| Server | `6223` | Information | `oidc.server.public_client_secret_rejected` |
| Server | `6224` | Information | `oidc.server.confidential_client_credentials_missing` |
| Server | `6225` | Information | `oidc.server.confidential_client_credentials_invalid` |
| Server | `6226` | Information | `oidc.server.public_client_assertion_rejected` |
| Server | `6227` | Information | `oidc.server.client_authentication_method_disabled` |
| Server | `6228` | Warning | `oidc.server.replay_tokens_revoked` |
| Server | `6229` | Warning | `oidc.server.replay_revocation_failed` |
| Server | `6233` | Information | `oidc.server.authorization_prompt_unsupported` |
| Server | `6237` | Information | `oidc.server.pushed_authorization_request_extracted` |
| Server | `6238` | Information | `oidc.server.pushed_authorization_request_validated` |
| Server | `6239` | Information | `oidc.server.pushed_authorization_parameter_unsupported` |
| Server | `6240` | Information | `oidc.server.pushed_authorization_parameter_missing` |
| Server | `6241` | Information | `oidc.server.pushed_authorization_redirect_uri_invalid` |
| Server | `6242` | Information | `oidc.server.pushed_authorization_redirect_fragment_rejected` |
| Server | `6243` | Information | `oidc.server.pushed_authorization_response_type_unsupported` |
| Server | `6244` | Information | `oidc.server.pushed_authorization_response_mode_incompatible` |
| Server | `6245` | Information | `oidc.server.pushed_authorization_response_mode_unsupported` |
| Server | `6246` | Information | `oidc.server.pushed_authorization_scope_missing` |
| Server | `6247` | Information | `oidc.server.pushed_authorization_prompt_combination_invalid` |
| Server | `6248` | Information | `oidc.server.pushed_authorization_pkce_method_unsupported` |
| Server | `6249` | Information | `oidc.server.pushed_authorization_pkce_parameters_incompatible` |
| Server | `6250` | Information | `oidc.server.pushed_authorization_pkce_response_incompatible` |
| Server | `6251` | Information | `oidc.server.pushed_authorization_confidential_access_token_rejected` |
| Server | `6252` | Information | `oidc.server.pushed_authorization_redirect_uri_rejected` |
| Server | `6253` | Information | `oidc.server.pushed_authorization_scopes_invalid` |
| Server | `6254` | Information | `oidc.server.pushed_authorization_endpoint_denied` |
| Server | `6255` | Information | `oidc.server.pushed_authorization_code_grant_denied` |
| Server | `6256` | Information | `oidc.server.pushed_authorization_implicit_grant_denied` |
| Server | `6257` | Information | `oidc.server.pushed_authorization_hybrid_grant_denied` |
| Server | `6258` | Information | `oidc.server.pushed_authorization_scope_denied` |
| Server | `6259` | Information | `oidc.server.pushed_authorization_uri_parameter_forbidden` |
| Server | `6260` | Information | `oidc.server.pushed_authorization_response_combination_invalid` |
| Server | `6261` | Information | `oidc.server.pushed_authorization_prompt_unsupported` |
| Server | `6262` | Information | `oidc.server.pushed_authorization_response_type_denied` |
| Server | `6263` | Warning | `oidc.server.pushed_authorization_hint_party_mismatch` |
| Server | `6264` | Information | `oidc.server.token_presenter_missing` |
| Server | `6265` | Information | `oidc.server.token_presenter_invalid` |
| Server | `6266` | Information | `oidc.server.token_audience_missing` |
| Server | `6267` | Information | `oidc.server.token_audience_invalid` |
| Server | `6268` | Information | `oidc.server.subject_token_client_identifier_missing` |
| Server | `6269` | Warning | `oidc.server.subject_token_party_mismatch` |
| Server | `6270` | Information | `oidc.server.actor_token_client_identifier_missing` |
| Server | `6271` | Warning | `oidc.server.actor_token_party_mismatch` |
| Server | `6272` | Information | `oidc.server.token_audiences_invalid` |
| Server | `6273` | Information | `oidc.server.token_resources_invalid` |
| Server | `6275` | Information | `oidc.server.authorization_resources_invalid` |
| Server | `6278` | Information | `oidc.server.token_audience_denied` |
| Server | `6279` | Information | `oidc.server.token_resource_denied` |
| Server | `6281` | Information | `oidc.server.authorization_resource_denied` |
| Server | `6283` | Information | `oidc.server.pushed_authorization_resource_denied` |
| Server | `6284` | Information | `oidc.server.token_resource_uri_invalid` |
| Server | `6285` | Information | `oidc.server.token_resource_fragment_rejected` |
| Handler | `7 / AuthenticationSchemeNotAuthenticatedWithFailure` | Information | `oidc.handler.authentication_failed` |
| Handler | `8 / AuthenticationSchemeAuthenticated` | Debug | `oidc.handler.authenticated` |
| Handler | `9 / AuthenticationSchemeNotAuthenticated` | Debug | `oidc.handler.not_authenticated` |
| Handler | `12 / AuthenticationSchemeChallenged` | Information | `oidc.handler.challenged` |
| Handler | `13 / AuthenticationSchemeForbidden` | Information | `oidc.handler.forbidden` |
| Server | 未知 / 无效身份 | 保留输入等级 | `oidc.server.unknown` |
| Handler | 未知 / 无效身份 | 保留输入等级 | `oidc.handler.unknown` |

## 桥接前保护与输出适配

OpenIddictMessage.ToString 只移除部分敏感参数，仍可包含 state、redirect URI、login hint 和自定义参数；Trace 的 token / claims 记录也不依赖这一脱敏。只删除最终 sink 的字段不足以控制桥接前求值。

审计中的内存探针确认，Serilog 9.0.0 在绑定属性时调用消息 ToString；不可格式化的消息会导致事件丢弃并进入 SelfLog。实际 OpenIddictRequest 的自定义参数引用已释放 JsonElement 时也能触发该问题。依据见[消息序列化](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Abstractions/Primitives/OpenIddictMessage.cs)与[令牌处理](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Server/OpenIddictServerHandlers.Protection.cs)。

- `OpenIddictLoggerFactory` 仅包装两个精确类别。Log 先遵循原 IsEnabled，再按实际身份构造固定安全 state，不读取原状态或 formatter，也不传递原异常；failureKind 只按异常类型生成。无异常时显式 null，避免 scope 同名属性补入；未知身份使用固定的非缺省 EventId，避免被 scope 假冒。
- 共享 AddSerilogSetup 在 UseSerilog 后先装饰 OpenIddict、再装饰原健康保护。调用原 descriptor factory，保留 provider、全局 logger、候选 session 所有权与旧链释放行为；其他类别、BeginScope、IsEnabled、AddProvider 原样委托。原工厂不支持的注册形态明确报错，不静默失去保护。
- `OpenIddictLogSink` 在上下文合并后重建事件，覆盖旧链代码 / 配置 WriteTo、AuditTo 和候选输出。只保留原时间与等级、类型化 trace/span、固定身份 / SourceCategory=http / Diagnostic 和受控 failureKind；不输出任意协议对象、凭据、claims、scope 或异常原文。
- 数字事件的 Name 字段在桥接后缺省；显式 null、错误类型、重复身份、未知 Id / Name 都成为来源对应的 unknown。相邻来源、Core、自有日志与其他 OpenIddict 通道原样透传。
- 保留原诊断门禁：低于 Warning 的事件在候选 Production 或 Diagnostics 关闭时抑制；原 Debug 的 6132 不升格为业务 Error。严重未知事件仍可见，旧链原等级策略不变。

## 保持的行为边界

- Dispatcher 在 handler 返回后记录 6133；handled / skipped / rejected 仍中止后续 handler。6132 仅在原条件下观察异常并重抛；filter 与 handler 解析发生在该 catch 外，不补造日志或吞异常。
- AuthenticationHandler 基类的认证结果、一次 dispatch 缓存、principal、tokens、properties、challenge / forbid 保持。OpenIddict 自行实现 SignIn / SignOut，不套入基类 10 / 11 编码。
- 请求方法 / Content-Type、GET / POST 参数与 Basic 凭据解析、client / redirect URI / PKCE / scope 校验保持。替身 manager 的异常与取消在原调用边界传播，不变成日志成功或另一个协议结果。
- 真实 JWT 生成、签名验证与 claims 结果保持，密钥仅在测试内存生成。令牌状态、presenter / audience 拒绝和重放撤销仍由原处理器决定；撤销异常（包括取消）仍被该框架分支消费，记录 Warning 后拒绝令牌，不改为向上抛出。
- 6141–6145、6147–6149、6151 使用 prepared 编码。JSON / text / form-post 的日志可发生在流写入之前，写入失败仍留下准备事件，但上下文不会被错误标记为 handled。
- JSON / text writer 传递 context 取消令牌；7.2.0 form-post writer 的 CopyToAsync 不传该令牌，测试仅确认流自身抛出的取消仍传播，不声称修复了请求取消传播。query / fragment / logout redirect 的地址和参数、challenge header、正文内容与默认行为保持。
- 日志保护不修改协议响应，也不控制 factory / filter / handler 解析、logger 前参数求值或 logger 后协议序列化。已存在的语义缺口不在本批修复。依据见[ASP.NET Core 响应处理](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Server.AspNetCore/OpenIddictServerAspNetCoreHandlers.cs)与[form-post / redirect](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Server.AspNetCore/OpenIddictServerAspNetCoreHandlers.Authentication.cs)。

## 四模式验证

四模式为旧链 / 候选链 × Development / Production；身份输出另交叉 Diagnostics。候选输出逐次核对实际 mode。

- 178 个数字身份由已安装 Server / Server.AspNetCore 的 IL 调用集合核对；5 个命名身份核对并调用真实 Authentication 基类 LoggerMessage 生成器。数字身份的全覆盖输出测试使用不可读状态，行为测试再调用真实处理器；不将其描述为 178 个真实生成器或每个条件性分支的完整端点验收。
- 全部 185 个编码覆盖 WriteTo / AuditTo；验证原等级 / 时间 / typed trace/span、未知严重事件、错误 / 重复身份、scope 冲突、不可格式化 state / message / claims / exception，以及相邻来源透传。
- 真实 dispatcher / factory 覆盖正常、handled、skipped、rejected、原异常 / 取消、filter 关闭 / 失败、缺失 filter、handler 工厂失败 / 缺失。真实 ASP.NET Core handler 使用可控 dispatcher 核对认证结果与缓存、properties / principal、错误响应分派、challenge / forbid 和 SignIn / SignOut。
- 真实请求处理器覆盖 GET / POST、方法 / Content-Type 拒绝、Basic 凭据与多凭据冲突；授权校验覆盖 client_id、redirect URI、fragment / issuer 参数、PKCE 和 scope。manager 仅为内存替身，不访问存储。
- 真实 JWT 生成 / 验证覆盖成功和错误签名，返回 token / claims 保持且日志不包含 token；校验 presenter / audience 和令牌状态，重放撤销成功 / IO 失败 / 取消均保持原控制流。
- 真实 JSON / text / form-post writer 使用内存流，覆盖正文、类型 / 长度、state 和缓存头以及写入失败 / 取消；query / fragment / logout / challenge / empty 覆盖参数、header 和 handled 状态。
- 共享 HostBuilder 仅 Build / Dispose，验证健康保护仍存在、候选 logger 由 session 保留、旧链 sink 随宿主释放一次。旧链 SelfLog 文件初始化保持，不把这些测试描述为零文件 IO。

验证结果：

| 检查 | 结果 |
| --- | --- |
| 本批 OpenIddict 定向测试 | 73 / 73，失败、跳过、未运行均为 0 |
| 扩大日志 / 健康 / 认证回归 | 1294 / 1294；失败、跳过、未运行均为 0 |
| Node 公共日志契约 | 27 / 27 |
| `dotnet build Radish.slnx -c Debug --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false` | 0 警告、0 错误 |
| `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` | 通过；日志指南与记录索引保留既有篇幅软提醒 |
| 公共策略差异核对 | 恰好新增 185 个 oidc.server / oidc.handler 编码；既有事件及其他策略字段不变 |

扩大回归工作目录为 `Radish.Api.Tests/bin/Debug/net10.0`，使用已安装 xUnit v3 进程内入口：

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

定向测试在沙盒通过；扩大回归首次在既有 `ApiErrorContractTest.ExceptionPipeline_ShouldBuildAndHandleUnknownApiException` 停滞，55 秒超时后终止，未计为通过。最小提权重跑同组测试，设置 90 秒超时清理，28.922 秒完成。未使用会绑定通信端口的 VSTest 入口。

## 剩余边界

- Core 的 Application / Authorization / Token manager、自有 IdleSession / AccountController 日志均未治理；既有 auth.seed.completed 不重复改写。后续需独立确认来源、事件与存储 / 消费失败边界。
- Client / Validation / Server.DataProtection 虽有传递包，但当前未启用对应集成；本批不启用、不扩大来源。IdentityModel 的独立 IIdentityLogger / EventSource 及其他非 Serilog 通道仍后置。
- 没有真实宿主运行、数据库、证书库、网络、浏览器、完整 OIDC 登录、生产采集、发布或部署验收。未安装 / 还原依赖、启动服务或绑定端口。生产候选开关继续关闭，L2 未整体完成。
