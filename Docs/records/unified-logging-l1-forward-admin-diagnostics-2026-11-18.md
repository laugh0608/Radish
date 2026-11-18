# Forward 满卷断连的管理员诊断联验（2026-11-18）

**管理员能独立确认 1 条输入追加失败。** 故障窗口内，默认级别记录了写入错误。Forward 调到 `warn` 后还报告消息处理失败。两种配置均未直接报告断连或尾段数量。对本候选的单 worker 同步追加路径，可据此判断连接随错误关闭，将后续影响记为“尾段数量未知”。恢复后的新事件全部落盘入库，原候选扩容用例仍为 `failed`。

## 环境与取证

在 OrbStack 的隔离网络内复用 Fluent Bit 5.1.2 Linux arm64 候选，本机镜像 ID 为 `sha256:c548922a71a15b99503d43b57df28eedf8fcf263b18122f0d27fa6e218b6218c`。真实 API 写入临时 SQLite。collector 使用 UID 10001，缓冲和 JSONL 共用 24 MiB 临时卷，文件与 HTTP 队列分别限 4M。Docker 以异步、无 ACK 的 Forward Message mode 发送。每轮先发送 128 条，再在有待写 chunk 的共享卷上填至 `ENOSPC`，随后发送同来源的另 512 条。[隔离配置与故障注入](../features/unified-logging-l3-ingest-plan.md#l1-l3-disk-queue-followup)

管理员可在释放空间前读取以下两路独立证据，采集路径均绕开共享卷上的 JSONL 和故障链下游的 SQLite。本次在扩容前、保持满卷时和恢复排空后三次取样。

- 使用 `docker logs --since` 读取 collector 自身的 stderr，按扩容窗口排除上一个创建 chunk 用例的错误。日志驱动为 Docker `local`，`max-size=1m`、`max-file=2`。
- 从隔离内网直读 collector 的 `/api/v2/metrics/prometheus`，比较同一 `guarded_input` 实例上 `fluentbit_input_storage_append_failed_records_total` 的 `chunk_create` 和 `chunk_write` 增量。从 `/api/v1/storage` 读取活跃 chunk 数。两个端点均只在测试内网访问。

默认轮继承服务的 `error` 级别。对照轮使用探针已有的 `--trace-growth`，仅将 Forward 输入调到 `warn`，同时启用合成序号取证。后者的 Docker 缓存、中转连接、Lua 序号和缓冲文件用于核实断点。管理员诊断只使用上面两路证据中的原生输出。

```bash
probe_image=sha256:c548922a71a15b99503d43b57df28eedf8fcf263b18122f0d27fa6e218b6218c
node Scripts/logging/collector-ingest-probe.mjs --run --database=sqlite --suite=disk-queue --collector-image="$probe_image"
node Scripts/logging/collector-ingest-probe.mjs --run --database=sqlite --suite=disk-queue --collector-image="$probe_image" --trace-growth
```

## 管理员在故障当时能看到什么

| 扩容窗口 | 默认 `error`（`0b16c4dc`） | Forward `warn`（`943fb48d`） |
| --- | --- | --- |
| 扩容前的累计数（创建、写入） | 1、0 | 1、0 |
| 保持满卷时的累计数（创建、写入） | 1、1 | 1、1 |
| 窗口内的原生 stderr | `error writing data from guarded_input instance` 1 次 | 同一写入错误 1 次，另有 `could not ingest Forward message: -1` 1 次 |
| 满卷时的输入连接指标 / 显式关闭日志 | 无 / 0 条 | 无 / 0 条 |
| 恢复后的累计数（创建、写入） | 1、1 | 1、1 |

创建计数的基线 1 来自前一个满卷创建用例。两轮扩容窗口均只有 `chunk_write +1`，当时即可确认 1 条记录写入失败。故障期间两个端点均可读。存储接口的活跃 chunk 取样值已是 0，而实际卷上仍有非空 chunk。单个活跃数不足以宣布排空。

本次两个输入级别均无“连接已关闭”日志，观察到的 `guarded_input` 指标也没有断连或未知尾段计数。`fluentbit_output_upstream_*connections` 属于输出连接，`fluentbit_output_radish_file_unknown_batches_total` 只表示文件批次写入结果未知。根据固定镜像的实现，同步追加错误返回至 Forward 后，协议处理结束并关闭当前连接。无 ACK 时，尾段可能尚未进入计数点。此时记录“已知写失败 1 条，连接按固定版错误路径关闭，尾段数量未知”。精确计数限于同一输入实例和取样窗口。[固定版错误返回与关闭路径](./unified-logging-l1-forward-gap-2026-11-18.md#失败如何传导)

## 合成对账与恢复

| 运行 | 合成发送 | 中转完整帧 | JSONL / SQLite 已交付 | 两路共同缺口 | 恢复新发 / JSONL / SQLite | 最终活跃 / 非空 chunk |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 默认 `0b16c4dc` | 640 | 640 | 171 / 171 | 469 | 2 / 2 / 2 | 0 / 0 |
| 对照 `943fb48d` | 640 | 623 | 171 / 171 | 469 | 2 / 2 / 2 | 0 / 0 |

对照轮的 Docker 缓存包含 1–640。中转收到同一连接的 1–623，记录到采集器端 `ECONNRESET`，关闭时尚有 486 个字节未构成完整帧。请求 ACK 数为 0。采集器写入前的过滤点记录到 1–172，故障时缓冲文件与恢复后两路最终交付均为 1–171。由此核实第 172 条写失败，173–640 共 468 条未到写入前过滤点。这个数量需要同一来源的合成序号。历史上的 467 还混入过另一来源的创建失败。[历史差额的归因](./unified-logging-l1-forward-gap-2026-11-18.md#467-的来源)

释放空间后，每轮两个新事件的完整 ID 均与 JSONL 和真实 API 写入的 SQLite 记录逐条匹配。扩容批次的两路输出丢弃增量、单支路缺口、文件重复行、坏行和不完整行尾均为 0。释放占位文件后和新事件交付后均核实活跃及非空 chunk 为 0，缓冲文件占用为 0 字节。两轮各剩 1 个零长度 chunk，随临时卷回收。故障窗口的 469 条共同缺口仍保留。

## 验收结果与剩余问题

两轮的监听隔离、文件拒写、文件满卷、满卷创建 chunk、HTTP 队列隔离与恢复检查均通过。原扩容断言完整保留。默认轮中转收到全部 640 帧，在“共同缺口等于输入追加失败”处失败（469 对 1）。对照轮先在“中转必须看全生产者记录”处失败（623 对 640）。两份报告均在这些断言前保存了管理员取样与恢复对账。

下一步需评审固定版本推断是否足以支持管理员处置断连。若需直接识别，再评估在关闭路径输出有界、固定原因的诊断，并标明该连接尾段数量未知。扩容用例也需按同一来源分别验收已确认失败和无 ACK 尾段。当前实例级计数包含该实例处理的所有来源，无法独立核算某个来源的端到端缺口。候选仍待验收，传输与内存复核、留存及备份恢复按项目顺位接续。

## 原始报告与清理

两份 JSON 均保存在本机 `artifacts/logging-disk-queue-probe/`。管理员的三个快照位于 `diskQueue.adminDiagnostics`，恢复事件完整 ID 位于 `diskQueue.bufferGrowth.recovery.eventIds`。两轮结束后按各自的任务标签复查，容器、网络与卷均为 0。临时目录、SQLite、配置和凭据已清理。原候选镜像保留供复验。

| 报告文件 | SHA-256 |
| --- | --- |
| `radish-logging-disk-queue-0b16c4dc.json` | `8690b19560d9b2d02de716c653fe93ebeb08ba5d60a2ca587de00ddbff958433` |
| `radish-logging-disk-queue-943fb48d.json` | `29573cc3c4e87fe3e429e49743db7949f9de57899121c64ffd220868476a9d50` |
