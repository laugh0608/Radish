# 统一日志传输上界与坏批隔离联验

2026-11-17（Asia/Shanghai），在 OrbStack / Linux arm64 上执行 `node Scripts/logging/collector-ingest-probe.mjs --run --database=sqlite --suite=transport`。代码基线为 `ae372804`，使用工作区的传输探针、现有 Debug 程序集、固定 Fluent Bit 5.1.2 / ASP.NET Core 10.0.14 / Node.js 24.16.0 镜像和临时 SQLite。Forward 仅发布 daemon 回环端口 `32771`，内网 API 和指标端口留在专用 Docker 网络。[联验范围](../features/unified-logging-l3-ingest-plan.md#l1-l3-transport-budget)

| 项目 | 实测结果 |
| --- | --- |
| Forward 追加形状 | 六组共 6,406 条合成日志。每行对应一个普通 Docker Message frame，每帧一条，未观测到 metadata、分组或残缺连接。最大帧 3,167 字节。结合固定版入口源码，该路径逐条追加。 |
| HTTP 负载 | 最大请求 2,439,841 字节，单请求最多 2,935 条，最大单行 3,060 字节。这三个极值单独统计。5,200 条精简事件分成 3 个请求，1,200 条最大字段组合分成 2 个请求，两组文件与 SQLite 的事件 ID 全部对账。 |
| 进程 RSS 峰值 | Fluent Bit `33,520 KiB`（约 32.73 MiB），API `308,220 KiB`（约 301.00 MiB）。503 待重试时的 RSS 分别为 `31,416 KiB` 和 `301,896 KiB`。就绪、负载后、重试中、恢复后和门限用例后的 RSS / 累计峰值见 JSON 报告。 |
| 来源坏记录与短暂故障 | 格式坏记录转换为一条 `pipeline.rejected` 摘要，相邻两条有效事件与摘要均写入文件和 SQLite。合成原文未见于检查的待重试缓冲、HTTP、文件和入库结果。临时写入阻断返回 503，解除后同一批次重投并成功交付。 |
| 终止错误与后续交付 | 对两条独立测试事件的 HTTP 请求分别注入语法错误与单行超限，真实接收端返回 400 / 413。HTTP 支路各计 1 条丢弃，文件保留对应记录，两组均未重试，后续正常事件落盘并入库。 |
| 接收器门限 | 单行 8,192 字节、请求 16 MiB、32,768 条各自边界返回 200，各自越界返回 413。语义无效事件计入有限拒收桶，同请求有效事件提交。后段语法失败返回 400，前 200 条已提交记录仍保留。 |

运行 `radish-logging-transport-aeccfe5f` 的六项断言通过。独立核对容器、网络和卷标签清单为空，打印的临时目录已删除。保留本机镜像缓存及 `artifacts/logging-transport-probe/radish-logging-transport-aeccfe5f.json`。

本结果覆盖该镜像、daemon 和负载下的候选传输配置。内存数字是 Fluent Bit 和 API 实测的进程累计高水位。实际 ENOSPC、文件与缓冲共享磁盘竞争、持续队列压力、留存与备份恢复继续按[退出清单](../features/unified-logging-l3-ingest-plan.md#l1-l3-exit-checklist)执行。
