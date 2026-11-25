# 统一日志 L1 / L3 PostgreSQL 隔离联验

2026-11-17（Asia/Shanghai），以 `dev` 的 `f5233cfe` 为产品代码基线，在 OrbStack / Linux arm64 上执行 `node Scripts/logging/collector-ingest-probe.mjs --run --database=postgresql`。完整 API 和 DbMigrate 使用 Debug 构建与 ASP.NET Core 10.0.14。采集器使用 Fluent Bit 5.1.2，中转使用 Node.js 24.16.0。PostgreSQL 实测版本为 `16.15 (Debian 16.15-1.pgdg13+2)`，固定镜像 digest 为 `sha256:a902560563d3a5661228585f53474c5065e7f273b21ffd77e555a8b5682c5690`。[运行范围](../features/unified-logging-l3-ingest-plan.md#l1-l3-postgres-runtime)

| 检查 | 实测结果 |
| --- | --- |
| 迁移、时间和端口 | DbMigrate apply 在空白实例初始化并通过严格校验。拒收计数字段为 `bigint`，持久化的发生时间和观察时间与事件的 UTC 值一致。公开端口访问入库路径、入库端口访问业务健康路径均返回 404。 |
| 提交确认和重放 | 5xx 的响应反馈 ID 与文件 / PostgreSQL 入库事件一致。API 已确认提交后丢弃响应，Fluent Bit 重投同一批次，API 报告重复且新增为零，目标事件仅一行。缺省反馈 ID 的旧格式 5xx 同样落盘和入库。 |
| 标准流与独立应急 | DbMigrate stdout 报告经采集器分流，stderr 的启动、完成事件正常落盘和入库。独立启动故障和入库故障各留下有界 stderr 诊断，诊断未进入同一入库链。 |
| Log 库断连与恢复 | 禁止临时 `radish_log` 的新连接并终止已有会话后，业务分类查询仍为 200，文件分支收到事件，入库 API 返回 503 且确认新增为零。恢复连接后，Fluent Bit 重投与失败请求具有相同批次摘要，API 确认成功，目标事件入库恰好一行。 |

Docker Hub 的大镜像层下载迟滞，经公开镜像缓存补齐同一 digest，再由原 Docker Hub 引用校验。临时镜像引用与分段下载目录已清理，原固定镜像缓存保留。运行 `radish-logging-postgresql-80a3c50d` 的八个断言均通过，本机报告为 `artifacts/logging-postgresql-probe/radish-logging-postgresql-80a3c50d.json`。按任务标签复核容器、网络和卷均无残留，临时数据库与凭据目录已删除。

证据覆盖当前 Linux arm64 隔离环境中的 PostgreSQL 持久化和连接恢复。正式传输上界、坏批隔离、磁盘 / 队列故障、留存和备份恢复继续按[剩余退出清单](../features/unified-logging-l3-ingest-plan.md#l1-l3-exit-checklist)执行。
