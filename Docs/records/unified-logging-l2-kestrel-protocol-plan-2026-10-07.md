# L2 Kestrel HTTP/2、HTTP/3 与 HTTPS 中间件核对及方案

日期：2026-10-07（Asia/Shanghai）。状态：项目所有者已确认方案，实现与验证见[批次记录](./unified-logging-l2-kestrel-protocol-2026-10-07.md)。承接[连接 / 坏请求治理](./unified-logging-l2-kestrel-connections-2026-10-07.md)；本记录保留实施前的只读核对与方案，完成证据以批次记录为准。

## 建议边界

下一批限定三个精确来源，不按 Kestrel 或 Https 前缀合并：

| 来源 | 已核对已知事件数 | 职责 |
| --- | ---: | --- |
| `Microsoft.AspNetCore.Server.Kestrel.Http2` | 17 | HTTP/2 连接、流、帧、HPACK 与输出队列 |
| `Microsoft.AspNetCore.Server.Kestrel.Http3` | 10 | HTTP/3 连接、流、帧、QPACK 与控制流 |
| `Microsoft.AspNetCore.Server.Kestrel.Https.Internal.HttpsConnectionMiddleware` | 9 | 普通 HTTPS 握手、协议回退与证书选择诊断 |

API / Auth / Gateway 的 Program 未显式配置 HTTP/2 / HTTP/3；Gateway 的启动配置包含 HTTPS profile。本次不据此推断部署现场已启用哪些协议，也不改变 endpoint、协议协商或证书配置。

TLS 并非只有第三个来源。已安装框架还包含独立 `CertificatePathWatcher` 日志；HTTP/3 的 TLS 由 QUIC transport 处理，不能用 HTTPS 中间件回归证明 QUIC 握手安全。本批排除证书文件监视 / 配置加载、Socket / QUIC transport 及其平台依赖，保留后续核对。

## 实际事件身份

来源依据官方 [HTTP/2 日志定义](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Infrastructure/KestrelTrace.Http2.cs)、[HTTP/3 日志定义](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Infrastructure/KestrelTrace.Http3.cs)及 [HTTPS 中间件及其日志定义](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Middleware/HttpsConnectionMiddleware.cs)，并与本机 ASP.NET Core 10.0.8 元数据和真实生成方法逐项交叉核对。

| 来源 | ID / EventName | 框架等级 |
| --- | --- | --- |
| Http2 | 29 Http2ConnectionError；30 Http2StreamError；31 HPackDecodingError；35 Http2StreamResetAbort | Debug |
| Http2 | 36 Http2ConnectionClosing；40 Http2MaxConcurrentStreamsReached；48 Http2ConnectionClosed；62 Http2ConnectionQueueProcessingCompleted | Debug |
| Http2 | 37 Http2FrameReceived；49 Http2FrameSending | Trace |
| Http2 | 38 HPackEncodingError | Information |
| Http2 | 60 Http2QueueOperationsExceeded；61 Http2UnexpectedDataRemaining；63 Http2UnexpectedConnectionQueueError | Critical |
| Http2 | 64 Http2TooManyEnhanceYourCalms；65 Http2FlowControlQueueOperationsExceeded；66 Http2FlowControlQueueMaximumTooLow | Debug |
| Http3 | 42 Http3ConnectionError；43 Http3ConnectionClosing；44 Http3ConnectionClosed；45 Http3StreamAbort | Debug |
| Http3 | 46 Http3FrameReceived；47 Http3FrameSending | Trace |
| Http3 | 50 Http3OutboundControlStreamError；51 QPackDecodingError；53 Http3GoAwayHighestOpenedStreamId | Debug |
| Http3 | 52 QPackEncodingError | Information |
| HTTPS | 1 AuthenticationFailed；2 AuthenticationTimedOut；3 HttpsConnectionEstablished | Debug |
| HTTPS | 4 Http2DefaultCiphersInsufficient；9 NoSubjectAlternativeName | Information |
| HTTPS | 5 LocateCertWithPrivateKey；6 FoundCertWithPrivateKey；7 FailToLocateCertificate；8 FailToOpenStore | Debug |

EventName 不总等于方法名：HTTP/3 的 `Http3GoAwayStreamId`、HTTPS 的 `LocatingCertWithPrivateKey / FailedToFindCertificateInStore / FailedToOpenStore` 均存在差异。不得只匹配编号或猜测名称。

帧日志包含帧类型、流 ID、长度及 HTTP/2 flags 等元数据；这里核对的是这些生成方法，不声称帧日志记录了原始请求正文。错误日志可能附带异常，证书诊断可能含 thumbprint / store 名称，均不应原样透传到已治理输出。

## 协议与异常所有权

### HTTP/2

[Http2Connection](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Http2/Http2Connection.cs)区分流错误、连接错误、HPACK 解码失败和输入异常：流错误报告后发送 RST_STREAM；连接级收尾涉及 GOAWAY、终止剩余流、等待请求结束和释放输入 / 帧写入器。应用请求异常仍可能属于已治理的 Kestrel 主来源，不能统一归入 Http2ConnectionError。

连接循环的其他异常使用 Kestrel 主类别 `0 / Name=null / Warning`，继续由已有 `http.server_unclassified` 处理。HTTP/2 子类别的 60 / 61 / 63 为 Critical，而 65 的流控队列超限为 Debug；不能因为名称相似就统一升降级。适配只裁剪日志，不改变帧、错误码、流存续或排队限制。

### HTTP/3

[Http3Connection](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/Servers/Kestrel/Core/src/Internal/Http3/Http3Connection.cs)通过多路复用 transport 接收流。输入 IO / reset 的部分事件仍属于 BadRequests；协议连接错误属于 Http3。出站控制流失败可以先报告该阶段，再报告连接错误并中止连接；不得据此进行全局去重。

连接循环的通用 catch 保存错误供收尾使用，并不保证单独产生一个 ILogger Error。本批不增补框架 catch 或改变消费行为。流重置、控制流、GOAWAY、等待活跃请求结束、关闭连接等需按实际阶段验证，不用 HTTP/1 状态码代表 HTTP/3 协议结果。

### HTTPS 中间件

已核对中间件的握手分支：OperationCanceledException 进入 AuthenticationTimedOut，IO / AuthenticationException 进入 AuthenticationFailed；这些分支释放 SSL stream 后返回，不调用后续委托。事件名称沿用框架含义，不能把所有 OperationCanceledException 推断为真实计时器超时。

握手成功后才替换 transport 并进入后续委托，finally 恢复原 transport；其他异常仍按原边界传播。缺少 SAN 是 Information，证书查找 / 存储错误多为 Debug。证书包装方法可能在 ILogger 前读取 thumbprint；sink 安全化不阻止框架此前读取。

同一源码中的 HTTP/3 TLS options 构造不意味着 QUIC 握手会经过该中间件的 OnConnectionAsync。证书库访问、实际 QUIC / UDP、ALPN 平台兼容和系统信任不是本批日志适配的验收结论。

## 本次探针证据

- 使用已安装 SDK 10.0.300 和 ASP.NET Core 10.0.8 引用编译临时探针，未安装包。核对 36 个已知事件的 ID / Name / 等级，并调用真实生成方法，确认三个实际 SourceContext 和异常附件。
- Http2 / Http3 通过真实 KestrelTrace 包装方法验证类别；HTTPS 通过真实类型化 logger 与生成方法验证。捕获器只输出元数据，不格式化原消息、异常或属性。
- 另发现 CertificatePathWatcher 的 16 个独立事件，但未调用文件监视器、打开证书库或扩大本批目标来源。
- 只读确认测试入口：公共 KestrelServer 支持普通内存连接工厂；内部 Http3Connection 接受 HttpMultiplexedConnectionContext，内部 KestrelServerImpl 支持多路复用连接工厂。反射类型存在不等于协议运行已验证，后续测试必须绑定本机框架版本。
- 探针全部正常退出，没有启动 Kestrel 引擎、项目宿主或网络 listener。官方源码网页部分不可用，经批准只读下载对应固定版本源码；没有访问生产或改变运行配置。

以上证据仅证明源码边界与实际生成身份，不代替下一批四模式协议 / 握手回归。

## 实施方案

1. 沿用现有上下文合并后、输出前的适配位置，为三个精确来源重建安全事件，覆盖旧链代码 / 配置 WriteTo / AuditTo 与候选链。36 个身份建立受控映射，各来源保留未知事件摘要；不读取原消息来分类。
2. 保留原时间、等级、类型化 trace/span、固定来源与受控 failureKind；不复制连接 / 流标识、请求头、证书指纹 / 路径、任意 scope 或异常详情。公共事件编码在实现时同步契约。
3. 保留 Warning / Error / Critical 可见性和现有 diagnostics 规则；旧链原等级保持，候选 Trace / Debug 按既有规范映射和过滤。帧过程诊断不提升为 Error，TLS 握手失败也不自动提升。
4. 不修改协议、证书验证、错误码、响应、异常消费、连接 / 流清理、限流或全局去重。已治理的主类别 / Connections / BadRequests 继续承担原来的阶段事件，生产候选开关保持关闭。

## 验证矩阵与执行边界

- **共同矩阵**：旧 / 候选 × Development / Production，36 个已知身份、未知事件与错误 Name、不可读取异常 / 属性、原事件不变、时间 / 等级 / trace、配置 WriteTo / AuditTo 和非目标类别。Critical 三事件在 diagnostics 关闭时仍须可见；候选 Fatal → Error 沿用公共规范。
- **HTTP/2**：真实 Kestrel 处理器与内存双向 Pipe，发送真实 preface / SETTINGS / HEADERS / 控制帧；覆盖正常请求、流级拒绝后连接可继续、连接级错误 / GOAWAY、HPACK 解码失败、应用异常和关闭清理。不得只手工调用 LogError 冒充协议路径。
- **HTTP/3**：真实 HTTP/3 处理器接入可控内存 MultiplexedConnectionContext / 流 feature，覆盖请求流、控制流错误 / 连接关闭、流中止及既有应用异常归属。该层替代 QUIC transport，不使用 MsQuic / UDP，也不代表真实 QUIC 握手或网络验收。若框架入口限制使某分支无法实际覆盖，明确记录缺口，不以生成方法代替该分支证据。
- **TLS**：真实 HTTPS 中间件 / SslStream 配合内存 transport；在测试中生成并释放临时自签证书，不导入用户证书库 / 信任存储，不读取真实私钥。验证握手成功、失败、取消分支、后续委托异常与 transport 恢复。客户端仅在该测试对生成证书作限定验证，不改变产品证书策略。
- **稀有分支**：HTTP/2 输出队列 Critical、HPACK / QPACK 编码失败、平台协议回退及证书库诊断至少验证真实生成方法、元数据与安全映射；未实际触发的队列故障、平台和证书库路径逐项列明，不声称全部运行分支验收。
- **清理与回归**：仅初始化测试引擎 / 处理器，唯一 transport 为内存连接 / 流；不执行系统 bind，不启动 API / Auth / Gateway。释放 SSL stream、证书 / 密钥、连接、流和管道，等待取消及处理任务结束。回归现有 HTTP/1、HTTP / SignalR 适配与日志契约，执行 .NET warnings-as-errors、Node 契约、Docs、文本卫生和 diff 检查。

不安装依赖、不启动项目服务 / 容器、不修改信任存储、不访问生产。证书监视 / 配置加载、Socket / QUIC transport、Http.Connections、协议 resolver、其他框架来源及非 Serilog 通道保留后续范围；三个来源完成不等于 TLS 或 L2 整体治理完成。
