# 统一日志事件契约与实现进度

> 2026-09-19：L1 生成契约与采集安全 / 故障可见性子项已实现。主方案见[统一日志专题](./unified-logging-governance-design.md)，首轮实测见[L1 记录](../records/unified-logging-l1-contract-and-transport-2026-09-19.md)。新增证据见[采集安全与故障边界](../records/unified-logging-l1-guarded-collector-2026-09-19.md)。本页不表示宿主或生产采集链已经切换。

## 1. 策略唯一来源

[`runtime-log-policy.v1.json`](../../Radish.Common/LogTool/Contracts/runtime-log-policy.v1.json) 维护版本、服务 / 分类注册表、原始级别映射、事件说明和属性类型约束。

- .NET：作为 `Radish.Common` 嵌入资源，由 `RuntimeLogPolicy` 读取；不依赖业务层。
- Node：`Frontend/scripts/logging/runtime-event.mjs` 读取同一文件；尚未接入静态服务器或复制到生产镜像。
- 两端运行同一组 [`runtime-events.json`](../../Scripts/logging/fixtures/runtime-events.json) 样本，不各写一份脱敏规则。
- 尚未替换现有 Serilog、浏览器 logger、Flutter logger 或数据库 sink；旧运行行为仍见[日志系统](../guide/logging.md)。

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

`containerId` 现由采集规范化层从 Docker FullID 标签补充；instanceId 只保留与 FullID 匹配的完整 / 短 ID，否则使用受信 FullID 并标记裁剪。`requestId / jobId / tenantId` 的权威上下文、异常类型 / 安全栈帧在宿主适配时补齐。目前未知异常整体省略，不回显 `Exception.Message / StackTrace / Data`。不能把首批字段子集称为最终全部 schema。

## 3. 安全与模式

1. Production 拒绝启用 diagnostics，Debug / Trace / Verbose 在映射输出前丢弃；Development 也须显式打开 diagnostics。
2. Warning / Error 不自动放开载荷。普通 `message`、OAuth URL、Header、body、exception 和未登记键在生成前省略。
3. 即使键被允许，值也须通过类型和范围校验；例如 `count` 不能携带对象、`outcome` 不能携带任意字符串。
4. 生产 stdout 序列化须使用 `RuntimeLogEvent.ToJsonLine()` / `serializeRuntimeLogEvent()`；最终 UTF-8 JSON 上限 **8 KiB**，超限拒绝序列化。此上限从原建议 32 KiB 下调，防止单条应用事件越过已观察到的 Docker 长行分片边界。
5. 该限制不解决第三方容器的任意长行。采集层已选择拒收分片、把解析失败变为安全摘要；不重组敏感长行，也不保留 Docker `log` 原文。
6. 后续生成 sink 负责将策略 / 序列化错误转入有限应急摘要，不能让日志错误破坏业务请求；本批尚未连接宿主 sink。

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

采集输入安全、API 不存在时启动、文件路径故障及队列满时丢弃可见性已取得本机证据。下一步可在此契约上推进 L2 生成端；L1 的正式传输上界、目标部署平台和完整磁盘故障验证仍须关闭。L3 的真实 API / SQLite / PostgreSQL 幂等入库、L4 Console 查询、L5 告警、L6 迁移仍未完成，生产链路保持不变。
