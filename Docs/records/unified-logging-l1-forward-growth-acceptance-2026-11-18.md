# Forward 满卷扩容的 SQLite 候选验收（2026-11-18）

默认级别和 `--trace-growth` 两轮均为 `passed`。扩容用例按新口径通过，已确认的追加失败、实际交付和管理员无法计数的尾段分别留证。两轮恢复后各新发 2 条，文件与 SQLite 逐条匹配，引擎和磁盘缓冲均排空。

## 运行范围与判据

本批复用 OrbStack / Linux arm64 的 Fluent Bit 5.1.2 候选，镜像 ID 为 `sha256:c548922a71a15b99503d43b57df28eedf8fcf263b18122f0d27fa6e218b6218c`。每轮独立初始化 SQLite，collector 以 UID 10001 使用 24 MiB 的临时共享卷，两条输出队列各限 4M。生产者先发送 128 条，在已有 chunk 的卷上写至 `ENOSPC` 后再发送 512 条。Docker 使用单条 Message、无 ACK，collector 同步追加。复验入口如下。[验收口径](../features/unified-logging-l3-ingest-plan.md#forward-disconnect-acceptance)

```bash
image_id=sha256:c548922a71a15b99503d43b57df28eedf8fcf263b18122f0d27fa6e218b6218c
node Scripts/logging/collector-ingest-probe.mjs --run --database=sqlite --suite=disk-queue --collector-image="$image_id"
node Scripts/logging/collector-ingest-probe.mjs --run --database=sqlite --suite=disk-queue --collector-image="$image_id" --trace-growth
```

- **管理员诊断**在释放空间前读取 collector 自身的有界 stderr 和内网阶段计数，比较同一进程的故障前、满卷和恢复后三次取样。报告在 `diskQueue.adminDiagnostics.assessment` 中保存已知的输入实例失败数、固定版错误路径推断的断连，以及 `tailCount: null`。
- **来源对账**在逐条取证轮核对 Docker 缓存、测试连接上的完整帧和未完成帧边界、采集器写入前序号、满卷时缓冲记录、JSONL 和 SQLite 的实际 ID。在 `diskQueue.bufferGrowth.sourceAssessment` 中单列测试来源的断点与尾段条数。只有同窗其他 API 连接和阶段证据能解释额外创建失败时，才单列其数量。
- **恢复**按两条新事件的 ID 分别核对 JSONL 和 SQLite 的唯一交付，读取引擎活跃 chunk 并检查实际卷上的非空 chunk。扩容阶段同时检查两支路丢弃、文件重复、坏行和不完整行尾。

定向测试覆盖漏计和重复计数、其他来源失败被误归到测试连接、写入前与输出前缀缺号、单路漏交和等量错 ID。完整帧、截断帧和附带另一 API 创建失败的输入均有正例。历史的 `943fb48d` 和 `f11bdc5b` 两份逐条证据也离线核对了来源划分。它们当时的整套验收结论仍为 `failed`。[原管理员联验](./unified-logging-l1-forward-admin-diagnostics-2026-11-18.md) · [历史混合来源](./unified-logging-l1-forward-gap-2026-11-18.md#467-的来源)

## 故障窗口

默认级别一轮（`5fc94d39`）中，`guarded_input` 的累计值从创建 1、写入 0 变为创建 2、写入 1。满卷时 stderr 各记录一次创建和写入错误。管理员据此登记本窗口实例级的创建失败 1 条、写入失败 1 条及数量未知的尾段。中转观察到测试来源的 500 个完整帧，同窗另有其他来源的 2 帧。该轮没有来源级断点证据，两个阶段的增量只报告到输入实例。

逐条取证轮（`a27aa27b`）的实例增量为创建 0、写入 1，另有 1 条 Forward 原生处理失败警告。同窗其他来源帧数为 0。生产者的 1–640 均以完整帧到达中转，ACK 请求数和关闭时未组成完整帧的字节数均为 0。唯一测试连接在采集器端发生 `ECONNRESET`，随后生产者端关闭。写入前过滤点只看到 1–172，满卷时缓冲文件、最终 JSONL 与 SQLite 均只有 1–171。由此确认第 172 条写入失败，173–640 共 468 条没有到达写入前过滤点。这个数来自测试来源的完整序号。两轮原生诊断均无显式断连消息，管理员的尾段数量在故障期和恢复后均为未知。

## 交付、排空与清理

| 运行 | 合成发送 | JSONL / SQLite 实收 | 两路共同缺口 | 恢复新发 / JSONL / SQLite | 最终活跃 / 非空 chunk |
| --- | ---: | ---: | ---: | ---: | ---: |
| 默认 `5fc94d39` | 640 | 171 / 171 | 469 | 2 / 2 / 2 | 0 / 0 |
| 逐条 `a27aa27b` | 640 | 171 / 171 | 469 | 2 / 2 / 2 | 0 / 0 |

两轮均从落盘 JSONL 和真实 API 写入的 SQLite 读取完整 ID。扩容批次单路缺口、两支路丢弃增量、文件重复、坏行和不完整行尾均为 0。解除故障后以及新事件交付后，两次均核实活跃 chunk、非空 chunk 和缓冲文件分配字节数为 0。默认轮余 2 个零长度 chunk，逐条轮余 1 个，随临时卷回收。排空后，故障批次仍各有 469 条共同缺口。

监听隔离、文件拒写与恢复、文件满卷与回滚、满卷创建和 HTTP 队列隔离与恢复均通过。独立的满卷创建用例各确认 1 条创建失败。HTTP 503 队列压力用例各有入库缺口 7,200 条，与 HTTP 支路丢弃增量一致，文件缺口为 0。

两份原始 JSON 保存在本机 `artifacts/logging-disk-queue-probe/`。完整 ID 在 `diskQueue.bufferGrowth.outcome.eventIds` 和 `recovery.eventIds` 中。两份报告均为 `cleanup=true`，退出后按各自标签复核，容器、网络和临时卷均为 0，包含 SQLite、配置和凭据的临时目录已移除。候选镜像仍可按原 ID 读取。

| 本机报告 | SHA-256 |
| --- | --- |
| `radish-logging-disk-queue-5fc94d39.json` | `d1866eef58086721902d8f4c3e0a3b6b44dfacd13d1985fdcd4c02f630d5b5d3` |
| `radish-logging-disk-queue-a27aa27b.json` | `042d636e5e8715fd1b26e32c95987c25b958fc1277c638a1377f38cca6b79761` |

## 剩余问题

本结论覆盖固定候选、单 worker 和无 ACK 的 Docker Message mode。管理员可据输入实例增量处置满卷，精确到来源的丢失数量仍需同源证据。下一步复核同一候选镜像的传输上界和进程内存。现有探针只允许 `disk-queue` 显式选择候选镜像，传输套件还需支持按镜像 ID 运行。之后接续留存、备份恢复及目标部署条件。[当前规划](../planning/current.md#接下来的事项)
