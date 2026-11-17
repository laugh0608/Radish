# 2026-11-17 日终文档回顾

日期按 Asia/Shanghai。复核 `dev` 从 `4f3923db` 到 `9ae19df7` 的五个开发提交，逐项核对探针、镜像构建输入、运行报告与专题记录。本次文档回顾独立提交。

## 提交与证据

| 提交 | 核对结果 |
| --- | --- |
| `4f3923db` | 真实 API 与 DbMigrate 分别在临时 SQLite、PostgreSQL 16.15 上运行。核对 5xx 反馈 ID、新旧事件兼容、提交后断连重放、DbMigrate stdout 分流及独立应急 stderr。PostgreSQL 用例另核对空库迁移、UTC、端口隔离、Log 库断连和同批恢复。[SQLite](./unified-logging-l1-l3-sqlite-runtime-2026-11-17.md) · [PostgreSQL](./unified-logging-l1-l3-postgres-runtime-2026-11-17.md) |
| `ae372804` | 复算单条追加条件下的候选上界。单行 4,212 B，请求 12,307,560 B、5,172 条，均在接收预算内。推导脚本给出待实测项目，实测证据单独按镜像记录。[传输预算](../features/unified-logging-l3-ingest-plan.md#l1-l3-transport-budget) |
| `b65b8fba` | OrbStack 实测 6,406 个单条 Message frame。最大 HTTP 请求 2,439,841 B，单请求最多 2,935 条，最大单行 3,060 B。Fluent Bit 和 API 的进程 RSS 峰值分别为 33,520 KiB、308,220 KiB。400 / 413 各只终止对应 HTTP 批次，503 同批重投，合成原文按实际路径检查。[传输联验](./unified-logging-l1-l3-transport-runtime-2026-11-17.md) |
| `f718c396` | 原版 Fluent Bit 在 24 MiB 共享 tmpfs 上两次触发真实 ENOSPC。文件缺 123 条、丢弃计数为零，恢复首条另缺 1 条。分流前缺失为同一条事件，两条支路均缺。HTTP 队列缺 7,200 条、计数 7,200，解除 503 后存活的 4,800 条完成重投。复验排空时区分 0 B 残留文件、活跃 chunk 和有数据文件。[磁盘与队列联验](./unified-logging-l1-l3-disk-queue-runtime-2026-11-17.md) |
| `9ae19df7` | 5.1.2 候选补丁检查 JSONL 短写、回滚和关闭结果，限制尾部恢复扫描，分别记录 chunk 创建和写入失败。六项离线文件故障检查、macOS arm64 文件插件与核心静态目标编译通过。探针已准备候选镜像 ID、拒写、输入扩容失败及轮转断言，Linux arm64 镜像和运行态验收仍待执行。[候选入口](../features/unified-logging-l3-ingest-plan.md#l1-l3-disk-queue-followup) |

复核 SQLite、PostgreSQL、传输与磁盘四份最终 JSON，断言和任务清理字段均为通过。实测数字绑定 OrbStack、原版 Fluent Bit 镜像及对应套件配置。候选构建更换编译配置和文件写入路径，须补相应的运行证据。

## 文档同步与接续

- [日志总纲](../features/unified-logging-governance-design.md)及[事件契约](../features/unified-logging-contract.md#75-l3-a-内网接收与幂等持久化2026-11-05)同步真实监听和两种数据库的隔离证据，说明输入侧失败和输出支路丢弃各自的口径。
- [衔接方案](../features/unified-logging-l3-ingest-plan.md)同步原版与候选的磁盘门禁、已验证端口分流及目标部署待验项。[日志指南](../guide/logging.md)与[敏感数据指南](../guide/logging-sensitive-data.md)更新隔离联验和安全样本检查范围。
- 下次先确认 OrbStack 构建与隔离启动范围。按候选镜像 ID 核对文件短写恢复、权限、两类输入失败、轮转、两路计数及资源清理。候选验证通过后复核适用的传输和内存证据，再进入留存与备份恢复。[接下来的事项](../planning/current.md#接下来的事项)

## 检查

传输预算复算、9 项 Node 探针测试、仓库质量与文档检查及 `git diff --check` 均通过。事件契约、日志指南和记录索引仍超过文档篇幅建议值。本次实测复核使用已归档的 JSON 报告。
