# L2 路由框架日志治理（2026-10-11）

本批按项目所有者确认范围，治理 4 个精确来源、28 个已知事件和 4 个来源未知摘要，共 32 个公共编码。只调整日志输出，保持路由选择、链接结果、请求体限制、安全元数据检查、响应、异常与取消传播。生产 `RadishLogging.Enabled=false` 保持，L2 尚未整体关闭。

## 调用关系与版本

- API 的 Controllers、Hubs 与健康端点，Auth 的显式 UseRouting / Controllers，以及 Gateway 的 YARP、Razor Pages / fallback 均使用 endpoint routing。Auth 账户流程的 RedirectToAction 还使用链接生成，route values 可携带 email、returnUrl 等数据。
- API 的 ChunkedUpload / Wiki 使用 RequestSizeLimit。当前产品未启用 ShortCircuit / DisableRequestSizeLimit；本批覆盖对应已知框架日志和内存分支，不增加产品调用或配置。
- 本机 `Microsoft.AspNetCore.Routing` 为 `10.0.8`，日志桥接使用已安装的 `Serilog.Extensions.Logging` `9.0.0`。测试核对真实 LoggerMessage 元数据并调用生成器，同时运行真实内存路由与链接生成。

官方依据：[路由中间件](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Http/Routing/src/EndpointRoutingMiddleware.cs)、[端点中间件](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Http/Routing/src/EndpointMiddleware.cs)、[DfaMatcher](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Http/Routing/src/Matching/DfaMatcher.cs)、[DefaultLinkGenerator](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Http/Routing/src/DefaultLinkGenerator.cs)。

## 精确类别与编码

以下类别统一加前缀 `Microsoft.AspNetCore.Routing.`，只按完整来源、EventId、EventName 匹配，不按模板文本猜测。

| 精确类别后缀 | 已知数 | 未知编码 |
| --- | ---: | --- |
| `EndpointRoutingMiddleware` | 12 | `routing.pipeline_unknown` |
| `EndpointMiddleware` | 2 | `routing.endpoint_unknown` |
| `Matching.DfaMatcher` | 6 | `routing.matcher_unknown` |
| `DefaultLinkGenerator` | 8 | `routing.link_unknown` |

| 来源简称 | ID / 实际 Name | 原等级 | 公共编码 |
| --- | --- | --- | --- |
| EndpointRoutingMiddleware | `1 / MatchSuccess` | Debug | `routing.pipeline_matched` |
| EndpointRoutingMiddleware | `2 / MatchFailure` | Debug | `routing.pipeline_unmatched` |
| EndpointRoutingMiddleware | `3 / MatchingSkipped` | Debug | `routing.pipeline_matching_skipped` |
| EndpointRoutingMiddleware | `4 / ExecutingEndpoint` | Information | `routing.pipeline_short_circuit_entered` |
| EndpointRoutingMiddleware | `5 / ExecutedEndpoint` | Information | `routing.pipeline_short_circuit_exited` |
| EndpointRoutingMiddleware | `6 / ShortCircuitedEndpoint` | Information | `routing.pipeline_short_circuit_without_delegate` |
| EndpointRoutingMiddleware | `7 / FallbackMatch` | Debug | `routing.pipeline_fallback_matched` |
| EndpointRoutingMiddleware | `8 / RequestSizeLimitMetadataNotFound` | Trace | `routing.pipeline_size_metadata_missing` |
| EndpointRoutingMiddleware | `9 / RequestSizeFeatureNotFound` | Warning | `routing.pipeline_size_feature_missing` |
| EndpointRoutingMiddleware | `10 / RequestSizeFeatureIsReadOnly` | Warning | `routing.pipeline_size_feature_read_only` |
| EndpointRoutingMiddleware | `11 / MaxRequestBodySizeSet` | Debug | `routing.pipeline_body_size_set` |
| EndpointRoutingMiddleware | `12 / MaxRequestBodySizeDisabled` | Debug | `routing.pipeline_body_size_disabled` |
| EndpointMiddleware | `0 / ExecutingEndpoint` | Information | `routing.endpoint_entered` |
| EndpointMiddleware | `1 / ExecutedEndpoint` | Information | `routing.endpoint_exited` |
| DfaMatcher | `1000 / CandidatesNotFound` | Debug | `routing.matcher_candidates_missing` |
| DfaMatcher | `1001 / CandidatesFound` | Debug | `routing.matcher_candidates_found` |
| DfaMatcher | `1002 / CandidateRejectedByComplexSegment` | Debug | `routing.matcher_candidate_complex_rejected` |
| DfaMatcher | `1003 / CandidateRejectedByConstraint` | Debug | `routing.matcher_candidate_constraint_rejected` |
| DfaMatcher | `1004 / CandidateNotValid` | Debug | `routing.matcher_candidate_invalid` |
| DfaMatcher | `1005 / CandidateValid` | Debug | `routing.matcher_candidate_valid` |
| DefaultLinkGenerator | `100 / EndpointsFound` | Debug | `routing.link_endpoints_found` |
| DefaultLinkGenerator | `101 / EndpointsNotFound` | Debug | `routing.link_endpoints_missing` |
| DefaultLinkGenerator | `102 / TemplateSucceeded` | Debug | `routing.link_template_succeeded` |
| DefaultLinkGenerator | `103 / TemplateFailedRequiredValues` | Debug | `routing.link_required_values_rejected` |
| DefaultLinkGenerator | `107 / TemplateFailedConstraint` | Debug | `routing.link_constraint_rejected` |
| DefaultLinkGenerator | `104 / TemplateFailedExpansion` | Debug | `routing.link_expansion_failed` |
| DefaultLinkGenerator | `105 / LinkGenerationSucceeded` | Debug | `routing.link_generated` |
| DefaultLinkGenerator | `106 / LinkGenerationFailed` | Debug | `routing.link_generation_failed` |

## 实现与语义边界

- `RoutingLogSink` 接入候选 `RuntimeLoggingConfiguration` 和旧链 `SerilogSetup` 的代码 / 配置 WriteTo、AuditTo，在上下文合并后重建事件。只保留原等级与时间、类型化 trace/span、固定 EventCode / 来源 / Diagnostic 及按异常类型生成的 failureKind；不保留路径、query、route values、生成 URI、端点 / constraint 对象、异常原文或任意 scope。
- Serilog 桥接省略值为 0 的 Id。仅对 `EndpointMiddleware` 的 `ExecutingEndpoint` 接受“Name 正确且 Id 字段缺省”；显式 null、类型错误、重复字段和其他来源均不按 0 处理。未来事件、错名或缺失身份仅产生各来源未知摘要，相邻类别原样透传。
- Trace / Debug / Information 都保留诊断语义；候选 Production 或关闭诊断时抑制，Warning / Error / Critical 不因未知身份而隐藏。旧链既有等级策略保持，不强行提升诊断等级。
- `ExecutedEndpoint` 在同步 catch 和异步 finally 中也会发生，编码使用 `exited`，只表示端点执行退出，不表示业务成功、响应已写完或请求完成。短路执行事件同理；不合并、去重或承诺每请求只有一条日志。
- 不改变匹配、404、fallback、预设 endpoint、复杂片段 / constraint 拒绝、歧义异常、请求体大小 feature，以及 ShortCircuit 的状态码 / 下游跳过 / 安全检查。单 literal 的 matcher 快路径可能不产生 CandidateValid，不能要求每次匹配出现所有阶段事件。
- 不改变链接选择、路径 / URI 编码和 null 失败结果。DfaMatcher 拒绝 constraint 时可能在 logger 前调用其 ToString，DefaultLinkGenerator 失败日志可能先格式化 route values；适配器不能控制这些前置求值，测试确认原异常仍传播，不宣称消除了该阶段二次失败。

## 四模式验证

四模式为旧链 / 候选链 × Development / Production；生成器另外交叉 Diagnostics 开关。候选每次显式配置并核对输出 mode。

- 核对 28 个真实 LoggerMessage 元数据与生成器，固定 32 个公共编码；配置 WriteTo / AuditTo 都覆盖已知与未知事件。验证原等级、时间、类型化 trace/span、受控异常分类，以及不能格式化的原始属性 / 异常对象不会被适配器读取。
- 覆盖缺省 0、显式错误 Id、错名、重复身份字段、相邻来源、无名 / 未来事件和未知严重事件；敏感 scope、路径、email / returnUrl、端点 / constraint 名称均不进入治理后的输出。
- 真实 UseRouting / UseEndpoints 管道覆盖选择、未匹配 404、fallback、预设端点跳过匹配、constraint / 复杂片段拒绝和歧义异常；核对 status、selected endpoint、route values 和下游是否执行。
- 请求体限制覆盖无 metadata、无 feature、只读 feature、设置值和禁用上限。短路覆盖有 / 无 delegate、状态码、成功、同步 / 异步失败和取消；普通端点也覆盖对应退出路径。RouteEndpoint 构造器拒绝空 delegate；无 delegate 分支仅以普通 Endpoint 直接调用真实 ExecuteShortCircuit 方法验证，不将此证据宣称为完整匹配管道或产品可达路径。
- 普通 / 短路端点均验证 Authorization、CORS、Antiforgery metadata 缺少必要中间件时的原拒绝，未进入 endpoint delegate。
- 真实 LinkGenerator 覆盖 path / URI 成功、无地址、缺少 required value、constraint 拒绝和 complex segment expansion 失败；保持含 email / returnUrl 的返回 URI，同时从日志移除数据。前置 ToString 失败保留原异常对象传播。
- 测试只构造 ServiceCollection、ApplicationBuilder、DefaultHttpContext 与内存流，按目标来源过滤测试采集；不构建项目宿主、不启动服务、不绑定端口、不访问网络、数据库或外部凭据。

验证结果：

| 检查 | 结果 |
| --- | --- |
| 本批路由用例（包含在扩大回归中） | 69 / 69，四模式、身份和配置输出验证通过 |
| ErrorHandling、SignalRFramework、RuntimeLog 及既有 JWT 策略 / 授权边界回归 | 1121 / 1121；失败、跳过、未运行均为 0 |
| Node 公共日志契约 | 27 / 27 |
| `dotnet build Radish.slnx -c Debug --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false` | 0 警告、0 错误 |
| `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` | 通过；日志指南与记录索引保留既有篇幅软提醒 |
| 公共策略差异核对 | 恰好新增 32 个 routing 编码，既有事件和其他策略字段不变 |

测试使用已安装 xUnit v3 的进程内入口，工作目录为 `Radish.Api.Tests/bin/Debug/net10.0`：

```bash
dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 \
  -class 'Radish.Api.Tests.ErrorHandling.*' \
  -class 'Radish.Api.Tests.SignalRFramework*' \
  -class 'Radish.Api.Tests.RuntimeLog*' \
  -class 'Radish.Api.Tests.Security.ApiJwtValidationPolicyTests' \
  -class 'Radish.Api.Tests.Security.AuthorizationBoundaryTests'
```

路由定向回归在沙盒内通过。首次扩大回归在既有 `ApiErrorContractTest.ExceptionPipeline_ShouldBuildAndHandleUnknownApiException` 停滞，外层 55 秒超时后终止测试进程，未计为通过。经最小提权重跑同一组进程内测试，外层设置 90 秒超时与清理，39.217 秒完成。没有使用会绑定通信端口的 VSTest 入口，未安装 / 还原依赖、启动项目服务或绑定网络端口。

## 剩余边界

- `DefaultLinkParser` 当前未找到项目显式调用，保持条件性范围。MVC / Auth 自有日志、宿主健康服务和 OpenIddict 均不因本批纳入治理。
- `DiagnosticListener` 的 EndpointMatched 可携带 HttpContext；Activity / Meter / EventSource / 独立 IIdentityLogger 不经过本适配器。未修改这些通道，也未验证外部订阅或采集器。
- 没有执行真实宿主、网络、浏览器、OIDC 全流程、数据库、生产外部采集或发布验收。其余来源须分批确认；后续建议先审计宿主健康服务，再推进 OpenIddict 与非 Serilog 专项。
