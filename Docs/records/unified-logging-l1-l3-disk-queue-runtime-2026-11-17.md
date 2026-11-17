# 统一日志 L1 / L3 磁盘与队列故障联验（2026-11-17）

在 OrbStack / Linux arm64 执行 `node Scripts/logging/collector-ingest-probe.mjs --run --database=sqlite --suite=disk-queue`。基线为 `b65b8fba` 加工作区探针变更，使用本机固定镜像 Fluent Bit 5.1.2、ASP.NET Core 10.0.14 和 Node.js 24.16.0。SQLite 经 DbMigrate 初始化，和文件输出及 Fluent Bit 缓冲使用独立挂载。复验四项通过，覆盖监听隔离、两种 ENOSPC 阶段和 HTTP 队列溢出。[联验入口与边界](../features/unified-logging-l3-ingest-plan.md#l1-l3-disk-queue-runtime)

## 共享卷与支路对账

文件与缓冲共用一个 24 MiB（25,165,824 B）的 Linux tmpfs 卷，设备号均为 55。两次写满时系统的 `write` 均返回 `ENOSPC`（报告中的 `errno=-28`），剩余容量为 0 B。

| 故障窗口 | 共享卷实测 | 文件交付 / 实际缺口 / 丢弃计数增量 / 差额 | HTTP 入库 / 实际缺口 / 丢弃计数增量 / 差额 |
| --- | --- | --- | --- |
| 缓冲已有 128 条后填满 | 文件与缓冲分别占 20,480 B 和 102,400 B，文件侧占位写入剩余 25,042,944 B。随后文件输出也报告 ENOSPC | 5 / 123 / 0 / +123 | 128 / 0 / 0 / 0 |
| 卷满后追加一条 | 文件已用 24,576 B，缓冲占用为 0 B。占位写入剩余 25,141,248 B 后，Fluent Bit 无法创建 chunk | 0 / 1 / 0 / +1 | 0 / 1 / 0 / +1 |
| SQLite 触发 503，HTTP 队列溢出 | 分 5 波送 12,000 条。该窗口采样时卷仍有 13,901,824 B 可用，文件与缓冲分别占 8,306,688 B 和 2,957,312 B | 12,000 / 0 / 0 / 0 | 4,800 / 7,200 / 7,200 / 0 |

差额为实际缺口减去 Fluent Bit 对应输出的 `dropped_records` 增量。首个 ENOSPC 窗口的文件输出 `errors` 增量也为 0。第二个窗口两个支路缺少同一条事件，collector 保持运行，并记录 chunk 创建失败及无可用 chunk 两条诊断。队列故障窗口中 SQLite 最终保留最后两波，前三波与 HTTP 丢弃计数一致。

## 恢复与排空

- 第一次释放占位文件后，原有截断尾成为一条坏行。两条恢复探针中第一条在文件中缺失，第二条完整落盘，SQLite 两条都入库。这一条恢复探针缺口单独记录。
- 第二次释放占位文件后，两条恢复探针均落盘并入库。活跃 chunk 与非空 `.flb` 文件均为 0，卷上剩 1 个 0 B 的 `.flb`。
- 撤销 503 后，4,800 条存活积压完成重投，两条新恢复探针均落盘并入库。活跃 chunk 和非空 `.flb` 文件均为 0，空文件随测试卷清理。复验运行期间 collector 未重启。

首次运行 `4da7082b` 的探针因空 `.flb` 文件误判队列未排空并超时。复验前将判定改为活跃 chunk 数和非空 `.flb` 文件数均为零，另外报告空文件数。复验 `50a41f19` 通过。L1 仍需核对文件丢弃计数和截断尾恢复，以及入队前失败的可观测性。本批证据覆盖该 tmpfs 的容量边界，块设备性能、留存与备份恢复另行验证。

## 清理与报告

两次运行均按完整任务标签复核。各自的容器、网络和共享卷已删除，打印的两个临时目录均已移除，镜像缓存与本地 JSON 报告保留。

- 首次报告 `artifacts/logging-disk-queue-probe/radish-logging-disk-queue-4da7082b.json`
- 复验报告 `artifacts/logging-disk-queue-probe/radish-logging-disk-queue-50a41f19.json`
