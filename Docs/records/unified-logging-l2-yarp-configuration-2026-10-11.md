# L2 YARP 配置加载、管理与校验（2026-10-11）

本批在已完成的 YARP / 出站 HTTP 日志适配上增加三个精确来源、15 个已知事件和三个未知摘要，共 18 个公共编码。仅治理日志输出，保留配置加载 / 重载、状态更新、通知与异常传播；`RadishLogging.Enabled=false` 保持，L2 尚未整体关闭。

## 范围与实际调用关系

- 基于仓库已安装 `Yarp.ReverseProxy 2.2.0`，核对官方同版本源码、构造参数与真实日志生成器。来源必须使用实际 `ILogger<T>` 类别；`ProxyHttpRequestValidator` 注入的是 `ILogger<ConfigValidator>`。
- Gateway 的 `AddReverseProxy().LoadFromConfig(...)` 注册配置 provider；`MapReverseProxy()` 解析 manager 并同步等待 `InitialLoadAsync`，发生在项目 `Run` 之前。manager 从 provider 读取快照，经真实 validator 校验，再更新 cluster / destination / route 和 endpoint。
- Gateway 三个 JSON 配置源均为 `reloadOnChange:false`；环境配置也没有自动监视。本批不改该设置，不增加文件监视、配置 provider、过滤器、DNS resolver 或 transform。
- 当前代理配置未启用 YARP 的 SessionAffinity、主动 / 被动健康检查或 HttpRequest 自定义项。Gateway 自有 URL 健康检查属于上批命名 HttpClient 范围，不与 YARP 健康监视混同。

官方同版本参考：[ConfigurationConfigProvider](https://github.com/dotnet/yarp/blob/v2.2.0/src/ReverseProxy/Configuration/ConfigProvider/ConfigurationConfigProvider.cs)、[ProxyConfigManager](https://github.com/dotnet/yarp/blob/v2.2.0/src/ReverseProxy/Management/ProxyConfigManager.cs)、[ProxyHttpRequestValidator](https://github.com/dotnet/yarp/blob/v2.2.0/src/ReverseProxy/Configuration/ClusterValidators/ProxyHttpRequestValidator.cs)。行为证据另由本批测试取得。

## 精确来源与编码

| 简称 | 实际 SourceContext | 已知 + 未知 |
| --- | --- | ---: |
| provider | `Yarp.ReverseProxy.Configuration.ConfigProvider.ConfigurationConfigProvider` | 3 + 1 |
| manager | `Yarp.ReverseProxy.Management.ProxyConfigManager` | 11 + 1 |
| validator | `Yarp.ReverseProxy.Configuration.ConfigValidator` | 1 + 1 |

| 来源 | EventId / Name | 等级 | 公共编码 |
| --- | --- | --- | --- |
| provider | `1 / ApplyProxyConfig` | Information | `proxy.config_source_loading` |
| provider | `2 / ApplyProxyConfigFailed` | Error | `proxy.config_source_notification_failed` |
| provider | `47 / ConfigurationDataConversionFailed` | Error | `proxy.config_source_conversion_failed` |
| manager | `21 / ClusterAdded` | Debug | `proxy.config_manager_cluster_added` |
| manager | `22 / ClusterChanged` | Debug | `proxy.config_manager_cluster_changed` |
| manager | `23 / ClusterRemoved` | Debug | `proxy.config_manager_cluster_removed` |
| manager | `24 / EndpointAdded` | Debug | `proxy.config_manager_destination_added` |
| manager | `25 / EndpointChanged` | Debug | `proxy.config_manager_destination_changed` |
| manager | `26 / EndpointRemoved` | Debug | `proxy.config_manager_destination_removed` |
| manager | `27 / RouteAdded` | Debug | `proxy.config_manager_route_added` |
| manager | `28 / RouteChanged` | Debug | `proxy.config_manager_route_changed` |
| manager | `29 / RouteRemoved` | Debug | `proxy.config_manager_route_removed` |
| manager | `43 / ErrorReloadingConfig` | Error | `proxy.config_manager_reload_failed` |
| manager | `44 / ErrorApplyingConfig` | Error | `proxy.config_manager_apply_failed` |
| validator | `58 / Http10RequestVersionDetected` | Warning | `proxy.config_validator_http10_detected` |

各来源未知事件分别使用 `proxy.config_source_unclassified`、`proxy.config_manager_unclassified`、`proxy.config_validator_unclassified`。来源 + ID + Name 精确匹配；相邻类别、错误大小写、未来来源不会按前缀自动纳入。目标来源的畸形 / 未知身份只产生安全摘要，不按消息或异常文本猜测。

`24–26` 的实际 Name 是 `EndpointAdded / EndpointChanged / EndpointRemoved`；`2 / ApplyProxyConfigFailed` 的生成方法是通知失败处理，编码据此采用 `notification_failed`，不误称为配置事务回滚。

## 日志与运行语义

- 复用 `OutboundHttpFrameworkLogSink` 和共享策略；旧链代码 / 配置 WriteTo / AuditTo、候选输出均在上下文合并后重建事件。保留原时间、等级、类型化 trace/span、固定来源、`SourceCategory=http` 与受控 failureKind；不保留原异常、消息、配置值、URI、route / cluster / destination 标识和任意 scope。
- Debug / Information 仍为诊断事件，候选继续遵守 mode、diagnostics 和最低等级；Warning / Error / Critical 保持既有可见性与规范映射，不提升配置加载或增改删的等级。
- provider 首次转换错误在记录 47 后原样抛出；重载转换失败保留旧快照和原 token。转换成功先发布新快照再通知旧 token；回调抛出时记录 2，仍保留新快照。
- manager 首次加载错误继续包装为 `InvalidOperationException`，保留原 InnerException；不生成仅属于重载的 43 / 44，也不新增日志所有权。项目启动失败仍交给既有启动边界。
- 重载读取失败由 43 报告并保留旧配置；框架原有失败标记和五分钟轮询安排不变。校验失败在更新前由 44 消费，不触碰旧运行状态。实际更新先 cluster 后 route，后期客户端构造等失败可能保留已更新的 destination，不增加事务回滚。
- `ConfigurationLoaded` 在 apply catch 之外，异常继续传播；`ConfigurationApplied` 在更新完成后抛错会被 44 消费，已经更新的状态仍保留。不新增全局 catch、重试或跨阶段去重。
- HTTP/1.0 产生 Warning 58 但校验通过；未配置 HttpRequest、HTTP/1.1 / 2 / 3 保持安静。不支持的版本仍返回 `ArgumentException` 校验错误，不产生 58。

## 四模式与验证限制

旧 / 候选 × Development / Production 均覆盖：

1. 真实 15 个 LoggerMessage 生成器与构造注入类别、完整生成器清单、18 个编码；diagnostics true / false、配置 WriteTo / AuditTo、未知事件、畸形身份、不可读异常 / payload、trace/span 和非目标同实例通过。
2. 真实 `ConfigurationConfigProvider` 读取内存 IConfiguration：首次转换失败、重载失败保留旧快照、恢复后替换快照、真实内存 token 通知回调失败后保留新快照，以及 Dispose 后取消订阅。
3. 真实 `ConfigValidator` 验证空 HttpRequest、HTTP/1.0 / 1.1 / 2 / 3 / 不支持版本，保留警告与错误结果。
4. 真实 manager / validator / endpoint 构造验证首次加载包装、大小写不敏感的重复 route / cluster 校验、增改删、等价配置安静、endpoint token 和释放订阅、读取失败 / 校验失败恢复、后期应用失败留下部分状态，以及已加载 / 已应用回调的不同传播边界。

manager 测试使用受控 `IProxyConfigProvider` / change token，标记变化后显式 await 真实 `ReloadConfigAsync`，避免 fire-and-forget 竞态；并未验证 manager 的真实自动回调调度或五分钟计时。provider 测试另行执行真实内存 `IConfiguration.Reload()` 和 token 回调。

`InitialLoadAsync` 会调用健康监视入口，真实 monitor 即使没有健康配置也可能启动后台 scheduler。测试在 YARP 注册前替换 `IActiveHealthCheckMonitor`，移除真实 cluster listener（包括 HttpSys delegation），使用受控监听器和不允许 Send 的 HTTP 客户端工厂；保留默认 NoOp destination resolver，DataProtection 使用临时内存密钥。测试不构造或运行项目宿主、不访问用户密钥目录、不启动健康调度器、不发送 HTTP / DNS 请求；manager 的 CTS / 定时器与测试客户端在完成后释放。

真实文件监视、JSON 文件变更通知、网络 / DNS / TLS、主动 / 被动健康探测、多 provider 并发、平台 HttpSys、真实后台时序均未验收。未逐一触发的 transform / listener / endpoint 发布异常分支仅有生成器与输出证据，不把受控内存行为表述为生产运行验收。此前 Socket / QUIC 及出站网络验证限制继续有效。

## 验证结果

- 本批定向测试 **71 / 71 通过**，沙盒内执行；包含真实生成器身份、四模式输出和四模式内存配置行为。
- HTTP 错误边界、Kestrel / HTTPS / 证书、SignalR Core、运行日志适配与共享策略扩展回归 **844 / 844 通过**，包含本批 71 项；无失败、跳过或未运行，耗时约 26 秒。
- 扩展回归在沙盒内停在既有 `ApiErrorContractTest.ExceptionPipeline_ShouldBuildAndHandleUnknownApiException`，55 秒上限后仅停止本次测试进程，该次不计通过；获准最小范围提权后以相同进程内参数完整通过。没有为此改产品逻辑或跳过测试。
- `dotnet build Radish.slnx --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false`：**0 warning / 0 error**。
- `npm run check:logging-contract`：**27 / 27 通过**；`npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` 通过。既有 logging / records 索引仅有篇幅软上限提醒，未超过硬上限。
- 注册表对照精确增加 18 个编码，既有定义未改写，与三来源 15 个已知映射及三个未知摘要一致。

扩展回归命令（工作目录 `Radish.Api.Tests/bin/Debug/net10.0`）：

```bash
dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 \
  -class 'Radish.Api.Tests.ErrorHandling.*' \
  -class 'Radish.Api.Tests.SignalRFramework*' \
  -class 'Radish.Api.Tests.RuntimeLog*'
```

使用 xUnit 进程内入口，避免 VSTest 通信端口；未安装 / 恢复依赖。测试中的 `example.invalid` 仅为配置 URI 值，没有发出网络请求。

## 后续边界

条件性 SessionAffinity、主动 / 被动健康检查、HttpSys / DNS、Socket transport `.Client`、其他命名 HttpClient、认证 / 路由等剩余框架来源，以及 EventSource / Metrics 等非 Serilog 通道继续只读盘点、单独确认。未安装依赖、未启动项目服务、未绑定网络端口；本批不取得发布部署或生产切换授权。
