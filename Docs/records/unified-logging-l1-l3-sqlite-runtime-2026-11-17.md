# 统一日志 L1 / L3 SQLite 首轮联验

2026-11-17（Asia/Shanghai），以 `dev` 的 `f5233cfe` 为代码基线，在 OrbStack / Linux arm64 上运行完整 API 宿主、DbMigrate、Fluent Bit 5.1.2 和临时 SQLite。API / DbMigrate 使用 Debug 程序集和 ASP.NET Core 10.0.14 镜像。Node.js 24.16.0 中转负责在真实入库响应返回后模拟客户端丢失响应。运行命令、镜像引用和网络边界见[专项方案](../features/unified-logging-l3-ingest-plan.md#l1-l3-first-runtime-verification)。

| 判据 | 实测结果 |
| --- | --- |
| API 5xx 反馈 ID 与重投 | 临时更名 SQLite 分类表后，真实分类请求返回 500。响应正文 `traceId`、`X-Correlation-ID`、采集文件与入库 JSON 的 `requestId` 相等。中转丢弃 API 已确认提交的响应。Fluent Bit 重投同一批次，API 确认重复且新增为零，目标 `eventId` 在 SQLite 中恰好一行。缺省 `requestId` 的旧格式 5xx 样本在文件与 SQLite 均保留缺省字段。 |
| DbMigrate 命令报告 | 实际 `help` 文本保留在 stdout，由 Docker tag 与标准流元数据在 JSON parser 前分流。stderr 的 `dbmigrate.started`、`dbmigrate.completed` 分别出现在文件和 SQLite，SQLite 中该容器仅有这两条事件。 |
| 独立 stderr 与入库恢复 | 启动配置的端口冲突和临时 SQLite 写入触发器分别产生一条有界 stderr 诊断。写入故障返回 503，目标事件未入库，应急事件未进入采集文件或 SQLite。撤销触发器后，同一 API 宿主生成的新事件恢复文件输出和入库。 |

先完成基础联验，随后将缺省反馈 ID 样本从启动事件换为旧格式 5xx 并整组复验。最终运行 `radish-logging-sqlite-50cda0fc` 的四个检查均通过，报告保留在本机 `artifacts/logging-sqlite-probe/radish-logging-sqlite-50cda0fc.json`。两次运行的临时容器、专用网络、SQLite、日志和凭据均已清理。辅助脚本、隔离配置与本文作为复验入口。

本次证据覆盖 Linux arm64 和 SQLite。PostgreSQL、正式传输上界、坏批隔离、磁盘 / 队列故障和备份恢复继续按[剩余退出清单](../features/unified-logging-l3-ingest-plan.md#l1-l3-first-runtime-verification)进入后续联验。
