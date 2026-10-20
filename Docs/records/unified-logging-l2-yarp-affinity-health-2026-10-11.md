# L2 YARP 条件性亲和与健康检查（2026-10-11）

本批精确增加 7 个日志来源、26 个已知来源 / 事件组合和 7 个未知摘要，共 33 个公共编码。仅重建安全日志，保持原日志等级、亲和策略、健康阈值、状态更新与异常传播。仓库默认亲和 / 健康配置不启用，`RadishLogging.Enabled=false` 保持，L2 尚未整体关闭。

## 调用关系与范围

基于仓库已安装 `Yarp.ReverseProxy 2.2.0`，核对官方同版本源码和实际构造 logger 类别，测试调用真实 LoggerMessage delegate 冻结身份。

- Gateway 使用默认 `MapReverseProxy()`：初始化 → SessionAffinity → LoadBalancing → PassiveHealthChecks → Limits → Forwarder；被动健康处理在下游正常返回后运行。亲和响应由条件性 `AffinitizeTransformProvider / AffinitizeTransform` 写回。
- `AddReverseProxy()` 注册四种亲和策略，默认 HashCookie，失败策略默认 Redistribute。实际共享亲和生成器借用四个具体策略的 logger，不以基类或 `SessionAffinity.Log` 作为类别。
- manager 首次配置加载调用 `IActiveHealthCheckMonitor.CheckHealthAsync`；该入口通过 Task.Run 完成初始检查，随后启动 scheduler，cluster listener 管理调度项。主动探测由请求工厂、cluster HttpClient 和 ConsecutiveFailures 策略协作，状态统一写入 DestinationHealthUpdater。
- 被动中间件、TransportFailureRate / ConsecutiveFailures 策略、ClusterDestinationsUpdater、可用目标策略没有独立 ILogger，不凭类名新增日志来源。被动与主动状态变化通过 DestinationHealthUpdater 的不同方法处理。
- 仓库受版本管理的配置没有启用这些条件性选项；Gateway 的 DownstreamServices 健康 URL 是独立命名 HttpClient 检查，不是 YARP cluster 健康检查。本批未读取或验证生产运行配置，也不启用任何选项。

官方参考：[默认代理管道](https://github.com/dotnet/yarp/blob/v2.2.0/src/ReverseProxy/Routing/ReverseProxyIEndpointRouteBuilderExtensions.cs)、[亲和中间件](https://github.com/dotnet/yarp/blob/v2.2.0/src/ReverseProxy/SessionAffinity/SessionAffinityMiddleware.cs)、[主动健康监视器](https://github.com/dotnet/yarp/blob/v2.2.0/src/ReverseProxy/Health/ActiveHealthCheckMonitor.cs)、[健康状态更新器](https://github.com/dotnet/yarp/blob/v2.2.0/src/ReverseProxy/Health/DestinationHealthUpdater.cs)。

## 精确类别与编码

下面 SourceContext 均加前缀 `Yarp.ReverseProxy.`。每个来源拥有独立未知摘要；不按命名空间前缀或消息内容推断。

| SourceContext 后缀 | 已知数 | 未知编码 |
| --- | ---: | --- |
| `SessionAffinity.SessionAffinityMiddleware` | 2 | `proxy.affinity_unclassified` |
| `SessionAffinity.CookieSessionAffinityPolicy` | 3 | `proxy.affinity_cookie_unclassified` |
| `SessionAffinity.CustomHeaderSessionAffinityPolicy` | 4 | `proxy.affinity_header_unclassified` |
| `SessionAffinity.HashCookieSessionAffinityPolicy` | 2 | `proxy.affinity_hash_unclassified` |
| `SessionAffinity.ArrCookieSessionAffinityPolicy` | 2 | `proxy.affinity_arr_unclassified` |
| `Health.ActiveHealthCheckMonitor` | 9 | `proxy.health_probe_unclassified` |
| `Health.DestinationHealthUpdater` | 4 | `proxy.health_state_unclassified` |

| 来源简称 | ID / 实际 Name | 等级 | 公共编码 |
| --- | --- | --- | --- |
| SessionAffinityMiddleware | `34 / AffinityResolutionFailedForCluster` | Warning | `proxy.affinity_rejected` |
| SessionAffinityMiddleware | `41 / AffinityResolutionFailureWasHandledProcessingWillBeContinued` | Debug | `proxy.affinity_failure_handled` |
| CookieSessionAffinityPolicy | `36 / AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster` | Warning | `proxy.affinity_cookie_no_destinations` |
| CookieSessionAffinityPolicy | `38 / RequestAffinityKeyDecryptionFailed` | Error | `proxy.affinity_cookie_decrypt_failed` |
| CookieSessionAffinityPolicy | `39 / DestinationMatchingToAffinityKeyNotFound` | Warning | `proxy.affinity_cookie_destination_missing` |
| CustomHeaderSessionAffinityPolicy | `36 / AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster` | Warning | `proxy.affinity_header_no_destinations` |
| CustomHeaderSessionAffinityPolicy | `38 / RequestAffinityKeyDecryptionFailed` | Error | `proxy.affinity_header_decrypt_failed` |
| CustomHeaderSessionAffinityPolicy | `39 / DestinationMatchingToAffinityKeyNotFound` | Warning | `proxy.affinity_header_destination_missing` |
| CustomHeaderSessionAffinityPolicy | `40 / RequestAffinityHeaderHasMultipleValues` | Error | `proxy.affinity_header_multiple_values` |
| HashCookieSessionAffinityPolicy | `36 / AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster` | Warning | `proxy.affinity_hash_no_destinations` |
| HashCookieSessionAffinityPolicy | `39 / DestinationMatchingToAffinityKeyNotFound` | Warning | `proxy.affinity_hash_destination_missing` |
| ArrCookieSessionAffinityPolicy | `36 / AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster` | Warning | `proxy.affinity_arr_no_destinations` |
| ArrCookieSessionAffinityPolicy | `39 / DestinationMatchingToAffinityKeyNotFound` | Warning | `proxy.affinity_arr_destination_missing` |
| ActiveHealthCheckMonitor | `10 / ExplicitActiveCheckOfAllClustersHealthFailed` | Error | `proxy.health_probe_all_failed` |
| ActiveHealthCheckMonitor | `11 / ActiveHealthProbingFailedOnCluster` | Error | `proxy.health_probe_cluster_failed` |
| ActiveHealthCheckMonitor | `12 / ErrorOccuredDuringActiveHealthProbingShutdownOnCluster` | Error | `proxy.health_probe_cleanup_failed` |
| ActiveHealthCheckMonitor | `13 / ActiveHealthProbeConstructionFailedOnCluster` | Error | `proxy.health_probe_construction_failed` |
| ActiveHealthCheckMonitor | `14 / StartingActiveHealthProbingOnCluster` | Debug | `proxy.health_probe_started` |
| ActiveHealthCheckMonitor | `15 / StoppedActiveHealthProbingOnCluster` | Debug | `proxy.health_probe_stopped` |
| ActiveHealthCheckMonitor | `16 / DestinationActiveProbingCompleted` | Information | `proxy.health_probe_completed` |
| ActiveHealthCheckMonitor | `17 / DestinationActiveProbingFailed` | Warning | `proxy.health_probe_failed` |
| ActiveHealthCheckMonitor | `18 / SendingHealthProbeToEndpointOfDestination` | Debug | `proxy.health_probe_sending` |
| DestinationHealthUpdater | `19 / UnhealthyDestinationIsScheduledForReactivation` | Warning | `proxy.health_state_reactivation_scheduled` |
| DestinationHealthUpdater | `20 / PassiveDestinationHealthResetToUnkownState` | Information | `proxy.health_state_passive_reset` |
| DestinationHealthUpdater | `49 / ActiveDestinationHealthStateIsSetToUnhealthy` | Warning | `proxy.health_state_active_unhealthy` |
| DestinationHealthUpdater | `50 / ActiveDestinationHealthStateIsSet` | Information | `proxy.health_state_active_changed` |

HashCookie / ARR 仅产生共享事件 36 / 39，不映射解密事件 38。CustomHeader 额外产生 40；实际 16 / 17 Name 带 `Active`，20 的 `Unkown` 和 12 的 `Occured` 保留框架原拼写，不按生成方法名纠正或猜测。

## 保持的运行语义

- 日志在上下文合并后重建，适用于旧链代码 / 配置 WriteTo / AuditTo 与候选输出。保留时间、原等级、类型化 trace/span、固定来源、`SourceCategory=http`、Diagnostic 和受控 failureKind；不复制亲和键、cookie、header 名称 / 值、cluster / destination 标识、URI、原异常或任意 scope。Warning / Error / Critical 保持可见；Debug / Information 沿用现有诊断门禁和候选等级规范。
- 亲和无键保持安静；命中时缩小候选目标并标记已经匹配，响应不重复写入。有效键找不到目标与没有目标分别保留原 Warning。加密键损坏或解密异常被消费为 Error 和提取失败；多值 header 保留独立 Error，不继续尝试解密。
- Redistribute 保留原候选集合并继续下游，记录 Debug；Return503Error 设置 503、停止处理并记录 Warning。不做跨阶段去重。响应加密失败和下游异常继续传播，取消时不写响应亲和键，异步查找接口保留取消传播。
- 主动探测每次 HTTP 响应都记录 Information 完成，包括 HTTP 500。默认策略根据 2xx / 连续失败次数判断健康：默认阈值 2，阈值前保留旧状态，成功重置计数。状态不变不重复生成状态日志；Unhealthy 为 Warning，Healthy / Unknown 为 Information。
- 请求构造失败产生 Error 并保留探测结果中的原异常；发送 / 取消异常产生 Warning，交给策略。策略错误和响应释放错误分别由既有 Error 消费，初始检查的外层异常由另一 Error 消费，保留 InitialProbeCompleted 与调度启动行为。不扩大 catch 范围。
- 被动健康只把六种 destination 侧转发错误视为失败；请求取消被排除，HTTP 500 且无错误 feature 不算传输失败。默认最少 10 个样本、失败率 0.3、60 秒窗口保持，恢复周期取配置值与检测窗口较大者。
- SetPassive 先更新状态、安排恢复，再异步重算可用目标；公共方法仍丢弃 Task。恢复将仍为 Unhealthy 的状态置为 Unknown；已提前恢复为 Healthy 时不覆盖。主动状态不改被动状态，未启用的检查不参与目标过滤。
- HealthyAndUnknown 排除不健康目标；默认 HealthyOrPanic 在全部不健康时返回全部目标。不把日志适配改成新的故障转移规则。状态更新后重算失败仍保留已经变化的状态和原异常边界，不新增回滚。

## 四模式回归与证据边界

旧 / 候选 × Development / Production 覆盖：

1. 全部 26 个真实来源 / 事件组合、7 个 logger 构造注入类别、生成器完整清单和 33 个编码；diagnostics true / false、配置 WriteTo / AuditTo、未知零 / 未来事件、畸形身份、不可读异常 / payload、trace/span、相邻来源同实例通过，以及 Hash / ARR 收到解密身份时仅进入未知摘要。
2. 真实四种亲和策略的响应键生成与请求回读、缺失 / 无目标 / 不匹配、有效匹配不重复写回、取消、加密键错误、header 多值、Redistribute / Return503Error、默认 HashCookie、下游与响应加密异常。使用真实响应 AffinitizeTransform；实际浏览器 cookie 往返和代理端到端响应不在证据内。
3. 真实主动 monitor、默认请求工厂、ConsecutiveFailures 和 updater，配合替代 HTTP handler 验证探测 GET / URI / HTTP 版本、500 完成事件、连续失败阈值、成功重置、状态恢复与响应释放。另用受控工厂 / policy / response 故障覆盖构造、发送 / 取消、策略、释放和初始外层失败。
4. 真实 cluster listener 和 scheduler 的新增、周期修改、禁用 / 移除与 Dispose。TimeProvider.CreateTimer 使用测试 ITimer，不创建系统 timer；测试显式等待框架 timer 委托调用的真实异步方法，避免 fire-and-forget 任务遗留。控制时间和直接触发不等于真实定时调度验收。
5. 真实被动中间件和 TransportFailureRate 配合受控状态接收器，覆盖全部 ForwarderError 分类、请求取消、500、最小样本、默认 0.3 边界、检测窗口与恢复周期，以及下游失败跳过后处理。真实 updater 另行验证状态先变、重算、无变化安静、恢复 Unknown、提前恢复不覆盖、可用目标策略和 timer 清理。
6. 被动 updater 的异步证据显式等待内部 SetPassiveAsync；公共 SetPassive 的任务丢弃语义不改写为可等待传播。重算失败测试验证原异常和已修改状态，但不启动 scheduler 的 Debug.Fail 异常回调路径。

密钥使用 EphemeralDataProtectionProvider，仅在内存存在，不访问用户密钥目录。模型使用内存 Cluster / Destination，HTTP handler 不发网络请求；没有启动项目宿主、访问 DNS / TLS、运行外部健康探测或绑定端口。

ActiveHealthCheckMonitor 的探测超时采用 CancellationTokenSource(timeout)，不经过 TimeProvider。测试把该超时设为无限，并通过 handler 提供取消异常，只证明消费分支，不证明真实超时到期、实际网络超时或后台计时。真实定时竞态、多 cluster 并发、浏览器亲和、平台运行及外部探测均未验收；未触发的策略异常 / 平台分支仅有生成器与安全输出证据。Activity / EventSource / Metrics 与 scheduler 的 Debug.Fail 等非 Serilog 通道不由本批适配器治理。

## 验证结果

- 定向测试 **86 / 86 通过**，在沙盒内通过 xUnit 进程内入口执行；覆盖全部来源 / 事件组合及四模式内存行为。
- HTTP 错误边界、YARP / HttpClient、Kestrel / HTTPS / 证书、SignalR Core、运行日志适配和共享策略扩展回归 **930 / 930 通过**，包含本批 86 项；无失败、跳过或未运行，最终复验约 31 秒。
- 扩展回归首次在沙盒内停在既有 `ApiErrorContractTest.ExceptionPipeline_ShouldBuildAndHandleUnknownApiException`，55 秒后仅停止本次进程，不计通过；获准最小范围提权后，以相同进程内参数完整通过。未跳过测试或为此修改产品行为。
- `dotnet build Radish.slnx --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false`：**0 warning / 0 error**。
- `npm run check:logging-contract`：**27 / 27 通过**；文档检查、changed-only 文本卫生与 `git diff --check` 通过。既有 logging / records 索引只有篇幅软上限提醒，未超过硬上限。
- 注册表核对精确新增 33 个编码，既有定义未改写。事件 41 使用 `proxy.affinity_failure_handled`，表达所有允许继续处理的失败策略，不限定为默认 Redistribute。

扩展回归命令（工作目录 `Radish.Api.Tests/bin/Debug/net10.0`）：

```bash
dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 \
  -class 'Radish.Api.Tests.ErrorHandling.*' \
  -class 'Radish.Api.Tests.SignalRFramework*' \
  -class 'Radish.Api.Tests.RuntimeLog*'
```

未使用 VSTest 通信端口，没有安装 / 恢复依赖、启动项目服务或绑定网络端口。

## 后续边界

Gateway 非 Windows 移除 HttpSys delegation 注册，默认管道不包含 UseHttpSysDelegation；该两个来源继续单独核对。默认 NoOp destination resolver 不做 DNS，未注册的 DNS resolver 也没有独立 ILogger，不能凭类名增添来源。其他框架类别及非 Serilog 通道后续只读盘点、确认范围；本批不取得启用功能、安装依赖、运行服务、发布或生产切换授权。
