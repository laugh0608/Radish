# 统一日志候选镜像的 SQLite 传输联验（2026-11-19）

OrbStack / Linux arm64 的固定 Fluent Bit 5.1.2 候选通过传输联验。六项断言覆盖监听隔离、正常负载两路交付、503 同批重投、终止错误隔离、接收端门限及传输预算。采样覆盖就绪、正常负载、重试积压、恢复及接收门限。所有时间按 Asia/Shanghai 记录。[传输范围及判据](../features/unified-logging-l3-ingest-plan.md#l1-l3-transport-budget)

## 运行范围

工作区基线为 `f1290e1f`，使用本批传输探针、现有 API / DbMigrate Debug 产物和固定 ASP.NET Core 10.0.14 / Node.js 24.16.0 镜像。原样复用[满卷扩容验收](./unified-logging-l1-forward-growth-acceptance-2026-11-18.md)的本地 collector 镜像 ID。传输轮启用 `radish_jsonl`，collector 使用镜像默认 UID 0 和临时目录挂载。Forward 只发布至 `127.0.0.1:32784`，HTTP 接收和指标留在本轮专用网络。

```bash
image_id=sha256:c548922a71a15b99503d43b57df28eedf8fcf263b18122f0d27fa6e218b6218c
node Scripts/logging/collector-ingest-probe.mjs --run --database=sqlite --suite=transport --collector-image="$image_id"
```

## 实测结果

| 项目 | 结果 |
| --- | --- |
| Forward | 六组合计 6,406 条合成日志，每条对应一个 Docker Message frame，最大帧 3,167 字节。测试所见无额外 metadata、分组及残缺连接。 |
| HTTP 请求及两路交付 | 最大请求 2,439,841 字节、单请求最多 2,935 条、最大单行 3,060 字节，三项分别低于推导上界。5,200 条精简事件和 1,200 条最大字段组合各分成 3 个 HTTP 请求。两组在 JSONL 和 SQLite 逐条核对事件 ID，全部交付。 |
| 接收端门限 | 单行 8,192 字节、请求体 16 MiB、请求 32,768 条，各自边界均返回 200，越界均返回 413。16 MiB 用例同 ID 入库 1 条、判重 2,047 条。语义拒收进入有限桶，坏信封返回 400 并保留已提交的前 200 条。 |
| 503 同批重投 | 隔离 SQLite 的写入触发器使混合批返回 503、接收 0 条。撤销触发器后，请求体 SHA-256 相同的重投返回 200、接收 3 条。两条有效事件和一条 `pipeline.rejected` 摘要在 JSONL 和 SQLite 各出现一次。检查的缓冲、HTTP、JSONL 与 SQLite 中未发现合成原文。 |
| 终止错误与恢复 | 两个单事件用例分别触发 400 和 413，HTTP 支路各丢弃 1 条，JSONL 保留两条。两个请求均未重投，后续新事件成功在两路交付。 |
| 峰值 RSS | Fluent Bit `22,696 KiB`（22.16 MiB），API `282,896 KiB`（276.27 MiB）。503 待重试时的当前 RSS 分别为 `20,540 KiB` 和 `278,116 KiB`，恢复后分别为 `20,552 KiB` 和 `278,504 KiB`。 |

内存峰值取自 Linux `VmHWM`，适用于这轮的 daemon、镜像和负载。部署目标仍需明确架构、digest、daemon 回环路径、来源与实际并发及内存条件。下一步验证日志留存、备份恢复与数据保全，再沿既定顺位验收查询、站内告警和升级回滚。[剩余门禁](../features/unified-logging-l3-ingest-plan.md#l1-l3-exit-checklist) · [当前规划](../planning/current.md#接下来的事项)

## 报告、回归与清理

本机报告为 `artifacts/logging-transport-probe/radish-logging-transport-a31ae9e3.json`，其 SHA-256 为 `df1a7da6a2ab5e386205a54905d0c9a5e7ba34363e8f700a412a224832ca9e92`。`transport.retry` 保留 503 和恢复后 200 的请求体摘要、接收数及两路实收。报告为 `passed`，`cleanup=true`。退出后按本次标签独立检查容器、网络和卷均为 0，打印的临时目录已移除。保留报告及本地镜像。

18 项探针定向测试及 73 项日志契约测试通过。离线回归覆盖原版默认配置、候选 ID 与 `radish_jsonl`、非法镜像参数、SQLite 限制以及 `--trace-growth` 的磁盘套件边界。
