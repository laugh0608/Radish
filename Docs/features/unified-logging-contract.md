# 统一日志事件契约与实现进度

> 更新：2026-09-23。L1 生成契约与采集安全 / 故障可见性子项已实现。主方案见[统一日志专题](./unified-logging-governance-design.md)，首轮实测见[L1 记录](../records/unified-logging-l1-contract-and-transport-2026-09-19.md)。新增证据见[采集安全与故障边界](../records/unified-logging-l1-guarded-collector-2026-09-19.md)。L2 入口层已接入显式候选开关，见[生成入口记录](../records/unified-logging-l2-producer-entry-2026-09-19.md)；生产默认链路未切换。

## 1. 策略唯一来源

[`runtime-log-policy.v1.json`](../../Radish.Common/LogTool/Contracts/runtime-log-policy.v1.json) 维护版本、服务 / 分类注册表、原始级别映射、事件说明和属性类型约束。

- .NET：作为 `Radish.Common` 嵌入资源，由 `RuntimeLogPolicy` 读取；不依赖业务层。
- Node：`Frontend/scripts/logging/runtime-event.mjs` 读取同一文件；静态服务器已接入候选输出，Dockerfile 随运行脚本复制此策略。
- 两端运行同一组 [`runtime-events.json`](../../Scripts/logging/fixtures/runtime-events.json) 样本，不各写一份脱敏规则。
- Serilog 已提供与旧 sinks 互斥的候选路径，默认尚未启用；浏览器 / Flutter logger 和数据库接收仍待迁移。旧默认行为见[日志系统](../guide/logging.md)。

## 2. 生成契约 v1 首批字段

| 字段 | 当前实现 |
| --- | --- |
| `schemaVersion` | `1` |
| `eventId` | 在生成入口创建 UUID；同一个事件对象在不同介质 / 重试间复用；独立发生生成不同 ID |
| `eventCode` | 策略注册表里的稳定类别；未知值变为 `runtime.unclassified`，不输出原值 |
| `occurredAtUtc / observedAtUtc` | 生成时的 UTC 毫秒时间；.NET 使用可注入的 `TimeProvider`；不接受载荷伪造时间 |
| `deploymentId / service / instanceId / release` | 从宿主受信配置构造 `RuntimeLogSource`；请求 / 日志载荷不能覆盖 |
| `mode` | 仅 `Development / Production`，与 `test-latest` 镜像标签无关 |
| `level / diagnostic / isFatal` | 统一三级，诊断是独立标志，Critical / Fatal 保留 `isFatal=true` |
| `sourceCategory` | 注册表内的来源分类；未知分类回到 application 并标记裁剪 |
| `messageTemplate / message` | 首批仅登记的静态安全说明；没有自由文本插值 |
| `properties` | 有限数值 / 整数范围或固定枚举；尚不接受自由文本、数组及嵌套对象 |
| `traceId / spanId / operationId` | 可选、格式严格限制；trace / span 拒绝全零值 |
| `normalizationStatus / redacted / truncated` | 未分类或裁剪行为可辨识；当前是省略未知数据，不把省略伪称字符串截断 |

`containerId` 现由采集规范化层从 Docker FullID 标签补充；instanceId 只保留与 FullID 匹配的完整 / 短 ID，否则使用受信 FullID 并标记裁剪。`requestId / jobId / tenantId` 的权威上下文、异常类型 / 安全栈帧在宿主适配时补齐。当前最终处理边界只提供固定枚举 `failureKind`；未知异常归为 other，不回显 `Exception.Message / StackTrace / Data`。不能把首批字段子集称为最终全部 schema。

## 3. 安全与模式

1. Production 拒绝启用 diagnostics，Debug / Trace / Verbose 在映射输出前丢弃；Development 也须显式打开 diagnostics。
2. Warning / Error 不自动放开载荷。普通 `message`、OAuth URL、Header、body、exception 和未登记键在生成前省略。
3. 即使键被允许，值也须通过类型和范围校验；例如 `count` 不能携带对象、`outcome` 不能携带任意字符串。
4. 生产 stdout 序列化须使用 `RuntimeLogEvent.ToJsonLine()` / `serializeRuntimeLogEvent()`；最终 UTF-8 JSON 上限 **8 KiB**，超限拒绝序列化。此上限从原建议 32 KiB 下调，防止单条应用事件越过已观察到的 Docker 长行分片边界。
5. 该限制不解决第三方容器的任意长行。采集层已选择拒收分片、把解析失败变为安全摘要；不重组敏感长行，也不保留 Docker `log` 原文。
6. .NET / Node 输出适配已将策略 / 序列化 / 写入错误转入限频应急摘要 `pipeline.output_failed`，不携带原始异常；每个输出实例至多每分钟一次，并累计失败数。应急介质也失败时不递归。

## 4. 采集协议校准结论

首轮固定候选为 Fluent Bit `5.1.2`，只取得 OrbStack / Linux arm64 证据，尚未冻结 amd64 或生产选型。

- Forward、parser、file、HTTP、filesystem buffer 路径已运行；API 模拟失败不会阻止文件写入，已持久化队列可以在 `SIGKILL` 后恢复。
- 原建议的 **200 条 / 2 MiB HTTP 硬限制不可直接使用**。插件按 chunk 发送，413 属于不可重试失败，会丢弃 HTTP 支路中的整个 chunk。
- 校准方向为：HTTP 使用有界流式大信封，内部按最多 200 条处理 / 提交；最后一次提交成功后才确认请求。部分提交后重试依赖事件唯一键。16 MiB / 32768 条的信封仅通过当前代表样本，尚需最坏情况上界及真实入库验证。
- file 轮转按 chunk 生效，`rotate_max_size` 是触发阈值，不能视为严格单片上限；总容量预算要计入每片最大超出量。
- 默认实验现使用 preflight / parser / normalize / UUID / finalize 链；原 parser-only 实验仅保留为 `--raw-transport`。两者都不是 production Compose 配置。
- 文件打开失败有丢弃指标，恢复后可继续写新事件，但不会自动补写故障事件；HTTP 队列满的旧记录丢弃同样有指标。后续查询状态必须呈现这些介质缺口。

这些是 L1 的实测修订，不改变“stdout → 独立 collector → 文件 + 内网 API → 日志库”的架构。

## 5. 验证入口与剩余门禁

无需启动业务服务：

```bash
npm run check:logging-contract
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter FullyQualifiedName~RuntimeLogPolicyTests
```

`node Scripts/logging/collector-probe.mjs` 会启动隔离容器，必须取得当前任务授权；固定镜像、端口及清理边界见脚本与 L1 记录。默认报告输出 `.tmp/logging-l1/boundary-report.json`；原始传输报告仍为 `collector-report.json`。`guarded-boundaries-observed` 仅表示本机采集边界实验通过，不是生产发布成功。

采集输入安全、API 不存在时启动、文件路径故障及队列满时丢弃可见性已取得本机证据。L2 入口与 SQL / AOP / 事务 / DbMigrate 入口子项已落地，下一步治理 seed / 具体 migration / 后台任务 / Rust 与剩余业务事件；L1 的正式传输上界、目标部署平台和完整磁盘故障验证仍须关闭。L3 的真实 API / SQLite / PostgreSQL 幂等入库、L4 Console 查询、L5 告警、L6 迁移仍未完成，生产链路保持不变。

## 6. L2 候选生成入口

`RadishLogging.Enabled` 默认 false；这是一项阶段性迁移开关，不是新增日志模式。API / Auth / Gateway 在配置加载完成后创建 `RuntimeLoggingSession`，引导和运行共用一个 Serilog 实例；启用时不再读取旧 Serilog sink / MinimumLevel 配置。DbMigrate 已将命令报告与诊断分开；seed、具体 migration 与其他裸输出旁路尚未收口，禁止据此提前切换生产。

| 配置 | 默认 / 约束 |
| --- | --- |
| `Enabled` | false；正式切换在 L6 统一收口 |
| `Mode` | Production；Development 只允许在 Development 宿主 / Node `NODE_ENV=development` 使用 |
| `MinimumLevel` | Info；只接受 Info / Warning / Error |
| `Diagnostics` | false；Production 下 true 为配置错误 |
| `DeploymentId / Release` | local / unversioned；部署接入须设置明确身份 |
| `InstanceId` | .NET MachineName / Node hostname；采集器再按 Docker 标签校验 |

Node 读取相同名称的 `RadishLogging__Enabled / Mode / MinimumLevel / Diagnostics / DeploymentId / Release / InstanceId` 环境变量；不会根据 test-latest 或 NODE_ENV 自动开启诊断。.NET 非敏感默认值仍放共享 appsettings，本地仅通过原有 appsettings.Local.json 覆盖。

自有调用通过结构化属性 `EventCode`、`SourceCategory`、`Diagnostic` 标明意图，数值 / 枚举属性使用策略中的原名（如 `statusCode`）。ILogger 可用 BeginScope，存量 Serilog 可用 ForContext；消息模板不承担安全契约。未登记的普通 Info 归入诊断，未知 Warning / Error 则保留安全未分类摘要，后续调用点必须逐项迁移，不能长期依靠未分类摘要。

新路径使用同一策略生成 JSONL stdout，不自建文件或数据库写入。应急事件写 stderr，仍用规范 JSON，但计数保存在进程内。静态服务器健康检查保持安静，拒绝 / 失败只记录受控 HTTP method 和 statusCode。详细验证与未关闭边界见[L2 入口记录](../records/unified-logging-l2-producer-entry-2026-09-19.md)。

## 7. L2 SQL、异常与 CLI 生成端治理

2026-09-23 本批推进 SQL / AOP / 事务、API 已处理异常与 DbMigrate Program / Runner / Doctor；[批次记录](../records/unified-logging-l2-producer-governance-2026-09-23.md)维护验证证据。

- SQL 的普通开发诊断与慢链路分开；`SqlAopLog.Enabled` 默认 false，独立慢操作 / 连接默认 true，阈值分别 1000 / 500ms。普通诊断额外要求 Development 宿主、Development 日志模式和 Diagnostics。CRUD 与表 / 用户排除不影响慢链路。
- SQL 不再接受正文或参数值作为日志输入；事件只包含安全操作枚举、参数数与耗时。SQL `OnError`、Service AOP、事务和 UnitOfWork 的重复异常输出移除；数据库规范化、提交、回滚、保存点语义保留。
- API 已处理 5xx 只生成一次 `http.failed`；正常业务 4xx 不逐条记录 Warning，返回契约不变。响应已开始和非 API 等无法处理路径仍由框架拥有记录责任。
- 顶层 `RuntimeProcess` 捕获配置加载前及运行失败，输出 stderr 的 `runtime.failed` Fatal，退出 1；正常结束为 0。该终止事件使用独立 `bootstrap / unversioned` 来源，不读取可能损坏的配置，不附带异常原文。HostAbortedException 作为工具控制信号继续传播。
- DbMigrate 诊断固定使用同一 JSONL 生成策略写 stderr，候选开关仍控制 Web 宿主切换；CLI 的 doctor / verify / help 报告使用 stdout，保留成功 / 失败判定。阶段事件使用登记的 `dbmigrate.*` 代码与变更数量；`command` 只允许 apply / doctor / verify / init / seed / help。
- CLI 连接目标和探测失败消息使用安全说明，不显示连接串或第三方异常文本；具体 migration 与 seed 的输出仍待下一批治理。权威 schema ledger 与业务审计写入没有迁移到运行日志。
- 完整异常安全类型 / 栈帧、后台任务、Rust、完整业务事件分类及框架来源治理仍未全部完成。本批不声明 L2 或 L1–L6 整体关闭。

`failureKind` 只允许固定异常类别；`schemaIndex` 只允许 Runner 中登记的仓库自有索引名，便于保留迁移动作对象而不开放自由文本属性。宿主 `HostedServiceStartupFaulted`（EventId 11）仅在 `RuntimeProcess` 正在接管时抑制重复输出；未进入该边界时不静默丢弃。
