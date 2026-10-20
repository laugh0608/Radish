# L2 证书文件监视与配置加载治理

日期：2026-10-11（Asia/Shanghai）。项目所有者先确认只读核对，再批准本批实现、隔离测试与文档同步；明确不安装依赖、不启动项目服务、不访问生产或系统证书库。承接[HTTP/2 / HTTP/3 / HTTPS 批次](./unified-logging-l2-kestrel-protocol-2026-10-07.md)，生产候选开关仍关闭，L2 尚未整体完成。

## 实施范围与来源证据

在既有 `HttpServerFrameworkLogSink` 增加两个精确来源，保持上下文合并后、输出前的适配位置，覆盖旧链代码 / 配置 WriteTo / AuditTo 与候选输出：

- **Watcher**：`Microsoft.AspNetCore.Server.Kestrel.Core.Internal.CertificatePathWatcher`，16 个事件。
- **Config**：`Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServer`，10 个事件。`CertificateConfigLoader`、`TlsConfigurationLoader` 和开发证书检查使用该类型化 logger，不能按实现类名匹配，也不能与 Kestrel 主来源或 HTTPS 中间件类别混用。

使用官方固定版本源码与本机 ASP.NET Core 10.0.8 元数据交叉核对，调用真实日志生成方法确认实际来源、ID、Name 和等级。源码入口：[监视器](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/CertificatePathWatcher.cs)、[监视器事件](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/CertificatePathWatcherLoggerExtensions.cs)、[配置事件](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/LoggerExtensions.cs)、[证书加载](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Certificates/CertificateConfigLoader.cs)、[TLS 配置](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/TlsConfigurationLoader.cs)、[配置重载](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/KestrelConfigurationLoader.cs)及[重载消费边界](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/KestrelServerImpl.cs)。

## 事件契约

| 来源 | ID / Name | 公共事件编码 | 框架等级 |
| --- | --- | --- | --- |
| Watcher | 1 / `DirectoryDoesNotExist` | `http.certificate_watch_directory_missing` | Warning |
| Watcher | 2 / `UnknownFile` | `http.certificate_watch_file_unknown` | Warning |
| Watcher | 3 / `UnknownObserver` | `http.certificate_watch_observer_unknown` | Warning |
| Watcher | 4 / `CreatedDirectoryWatcher` | `http.certificate_watch_directory_created` | Debug |
| Watcher | 5 / `CreatedFileWatcher` | `http.certificate_watch_file_created` | Debug |
| Watcher | 6 / `RemovedDirectoryWatcher` | `http.certificate_watch_directory_removed` | Debug |
| Watcher | 7 / `RemovedFileWatcher` | `http.certificate_watch_file_removed` | Debug |
| Watcher | 8 / `LastModifiedTimeError` | `http.certificate_watch_timestamp_failed` | Debug |
| Watcher | 9 / `UntrackedFileEvent` | `http.certificate_watch_untracked_event` | Debug |
| Watcher | 10 / `ReusedObserver` | `http.certificate_watch_observer_reused` | Trace |
| Watcher | 11 / `AddedObserver` | `http.certificate_watch_observer_added` | Trace |
| Watcher | 12 / `RemovedObserver` | `http.certificate_watch_observer_removed` | Trace |
| Watcher | 13 / `ObserverCount` | `http.certificate_watch_observer_count` | Trace |
| Watcher | 14 / `FileCount` | `http.certificate_watch_file_count` | Trace |
| Watcher | 15 / `FlaggedObservers` | `http.certificate_watch_changed` | Trace |
| Watcher | 16 / `EventWithoutFile` | `http.certificate_watch_file_unavailable` | Trace |
| Config | 0 / `LocatedDevelopmentCertificate` | `http.certificate_config_development_selected` | Debug |
| Config | 1 / `UnableToLocateDevelopmentCertificate` | `http.certificate_config_development_missing` | Debug |
| Config | 2 / `FailedToLocateDevelopmentCertificateFile` | `http.certificate_config_development_file_missing` | Debug |
| Config | 3 / `FailedToLoadDevelopmentCertificate` | `http.certificate_config_development_load_failed` | Debug |
| Config | 4 / `BadDeveloperCertificateState` | `http.certificate_config_development_invalid` | Error |
| Config | 5 / `DeveloperCertificateFirstRun` | `http.certificate_config_development_setup_required` | Warning |
| Config | 6 / `MissingOrInvalidCertificateFile` | `http.certificate_config_file_invalid` | Error |
| Config | 7 / `MissingOrInvalidCertificateKeyFile` | `http.certificate_config_key_invalid` | Error |
| Config | 8 / `DeveloperCertificateNotTrusted` | `http.certificate_config_development_untrusted` | Warning |
| Config | 9 / `DeveloperCertificatePartiallyTrusted` | `http.certificate_config_development_partially_trusted` | Warning |
| Watcher | 未知 / 不匹配身份 | `http.certificate_watch_unclassified` | 保留输入等级 |
| Config | 未知 / 不匹配身份 | `http.certificate_config_unclassified` | 保留输入等级 |

26 个已知事件与两个未知摘要，共 28 个编码加入共享 JSON policy。保留原时间、等级、类型化 trace/span、固定来源与受控 failureKind，舍弃目录、证书 / 密钥路径、证书主题 / 指纹、原消息、异常详情和任意 scope，不修改原始 LogEvent。

Serilog.Extensions.Logging 对 ID=0 的 `LocatedDevelopmentCertificate` 只保留 Name；仅上述 Config 类别、该 Name、显式 0 或省略 Id 可以命中。Id 的 null / 字符串 / long / 对象 / 序列、错误 Name、错误来源不被当作缺省零。事件 6 / 7 的生成方法名与 EventName 不同，按真实 EventName 匹配。未知事件不读取模板进行猜测。

旧链保持原等级与最低等级控制；候选链继续将 Trace / Debug 映射为 Info + diagnostic，Production 或 diagnostics 关闭时过滤过程诊断，Warning / Error / Critical 保持可见，Critical 按公共规则变为 Error。没有升级正常监视过程、吞异常、增加重试或跨阶段去重。

## 隔离行为验证

### 监视器

真实 CertificatePathWatcher 使用其测试构造函数，传入可控 IFileProvider；没有创建 PhysicalFileProvider、文件系统轮询或真实文件监听。文件信息实现禁止枚举、读取内容及读取时间，防止测试意外越界。

- 同一个 CertificateConfig 重复添加不增加观察者；同一文件不同配置共享 watcher，最后一个观察者移除后释放文件与目录。
- 未知观察者 / 文件、缺失目录保留 Warning；提供器抛出的原异常继续传播，不虚构框架错误事件。
- 文件暂时不可用不设置 FileHasChanged、不触发 reload token；文件恢复可用后变更标记所有观察者、更换 token；未跟踪路径事件被忽略。
- 保持框架只监视 Path、未独立监视 KeyPath 的行为；Dispose 后活跃回调为零，后续 token 触发不再通知。

### 证书与配置

- 测试只在唯一临时目录生成自签证书、公钥 PEM、普通 / 加密私钥 PEM 和故障文件。真实 CertificateConfigLoader 加载公钥 PEM 与证书链，验证不含私钥及身份一致；缺失、损坏、同时指定文件与 Subject 的冲突按原语义传播，冲突在访问证书库前被拒绝。
- 直接调用真实 LoadCertificateKey，验证普通 / 加密私钥组合后的签名与验证能力，以及错误密码、格式、缺失、不匹配的异常传播。该入口绕过 Windows PersistKey，不导入 PFX 或系统密钥容器；不以此证明完整带私钥配置加载的所有平台行为。
- 首次加载失败到达既有 RuntimeProcess，退出码为 1，输出单个安全 `runtime.failed`；缺失文件分类为 io，不输出路径或异常文本。
- 真实 KestrelConfigurationLoader 和 KestrelServerImpl 的 RebindAsync 使用唯一内存 IConnectionListenerFactory。显式 Default 公钥证书与 HTTP endpoint 保证不走开发证书回退；没有 Socket / QUIC transport 或系统网络 bind。
- 重载证书文件缺失时，原 HTTP endpoint / listener 保持；恢复有效配置并改变地址后，释放旧 listener、建立新内存 listener。绑定阶段故障由 Kestrel 主类别 `0 / Name=null / Critical` 记录，保持 `http.server_unclassified` 与 io 分类。
- 重载测试直接等待真实 RebindAsync 完成；监视 token 行为另测，不把它宣称为真实磁盘变更到 TLS 热换证书的端到端验收。所有 listener、token、配置容器、证书与临时文件均清理。

## 验证结果

新增测试位于 `Radish.Api.Tests/ErrorHandling/Certificate*LoggingTests.cs`，共 **65 / 65 通过**；覆盖旧 / 候选 × Development / Production，26 个真实生成身份、未知 / 畸形身份、安全输出、WriteTo / AuditTo、监视行为、证书文件与密钥、启动失败和内存重载。非目标类别透传回归增加 Core.KestrelServer 子类别和 CertificateConfigLoader 实现类别。

- 相关 .NET 回归 **531 / 531 通过，0 跳过**，启用 `--no-restore` 与 `TreatWarningsAsErrors=true`。
- Node 公共日志契约 **27 / 27 通过**。
- `dotnet build Radish.slnx -c Debug --no-restore --warnaserror -m:1 -nr:false` 在沙盒内通过，**0 warning / 0 error**。
- `npm run check:docs`、`npm run check:repo-hygiene:changed` 和 `git diff --check` 通过；日志指南 768 行、记录索引 311 行有篇幅软提醒，均未超过硬上限。

首轮修正了测试夹具对框架非公开构造函数的调用及一次性 token 活跃订阅计数；没有为通过测试改变框架消费行为。后续 MSBuild 在沙盒内创建本地 IPC 受限，获最小范围提权运行同一隔离回归；失败进程按精确 PID 中断。提权未扩大项目服务、依赖、生产或证书库访问范围。

定向回归命令：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -p:TreatWarningsAsErrors=true --filter 'FullyQualifiedName~CertificateFrameworkLoggingTests|FullyQualifiedName~CertificateWatcherLoggingTests|FullyQualifiedName~CertificateConfigurationLoggingTests|FullyQualifiedName~CertificateReloadLoggingTests|FullyQualifiedName~Http2ProtocolLoggingTests|FullyQualifiedName~Http3ProtocolLoggingTests|FullyQualifiedName~KestrelProtocolGenerationTests|FullyQualifiedName~KestrelConnectionLoggingTests|FullyQualifiedName~OuterHttp|FullyQualifiedName~KestrelHttp|FullyQualifiedName~HttpException|FullyQualifiedName~ApiNotFoundPipelineLoggingTests|FullyQualifiedName~ApiErrorContractTest|FullyQualifiedName~ProducerLoggingTests|FullyQualifiedName~SignalRFramework|FullyQualifiedName~RuntimeLoggingAdapterTests|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~SensitiveQuery'
npm run check:logging-contract
```

## 证据边界与后续

- 系统证书库、用户开发证书、信任检查、Windows 私钥持久化、PFX、真实磁盘轮询 / 符号链接、TLS 握手及热换证书不在本批运行证据中。没有执行既有真实 TLS 握手回归，避免系统信任链访问；HTTPS 日志生成方法的身份与安全映射仍在定向回归内。
- Config 事件 6 / 7、开发证书事件和 Watcher 的 LastModifiedTimeError 具有真实生成方法、元数据和安全映射证据，未逐个触发真实环境分支。当前监视器源码未见 LastModifiedTimeError 调用点，不能虚构时间读取失败验收。
- 非 Serilog 通道不在适配范围；适配器也不能阻止框架在 ILogger 之前读取证书主题 / 指纹。Auth / API 自有 OIDC / JWT 证书加载未变更。
- 下一步先只读复核早期轻回应 / 评论 / 帖子在响应构造再次失败时的日志所有权，再确认方案。Socket / QUIC transport、Http.Connections、协议 resolver 等继续后续分批治理。生产 `RadishLogging.Enabled=false` 保持，本批不关闭 L2 整体或启动发布。
