# 2026-11-12 日终文档回顾

日期为 2026-11-12（Asia/Shanghai）。复核 `dev` 上从 11 月 10 日收口规划 `546b0968` 到诊断与本地输出提交 `4728e977` 的 19 个提交，含 12 个实现提交与 7 个方案或记录提交。实现和本次文档复核分别提交。

## 代码与契约对照

| 实现提交 | 代码与当前依据 |
| --- | --- |
| `0ed03b5f`、`4a5555a2`、`f9a5f751`、`960a27ef`、`38d15bd3` | 共享业务 Job、抽奖批次、文件与记录清理、服务内清理、Outbox 分派及状态写入。核对生成来源、适配器接线及业务状态口径。Outbox 由服务显式传失败分类，仓储状态事件使用派生关联。见[契约第 78–82 节](../features/unified-logging-contract.md#l2-business-job-final-output) |
| `4200e6d9`、`edd47101` | 奖励、币、经验、商城、库存及口令。核对精确来源、逐事件白名单与重试、事务和输出失败边界。见[契约第 83–84 节](../features/unified-logging-contract.md#l2-shop-final-output) |
| `818ba299`、`e613bbb7`、`c15eb0a0`、`8d02535a` | 公开查询与报表、通知推送、两个 Hub 异常断开、内容及表情。核对 Serilog 类型来源、命名 EventId、scope 隔离与旧 WriteTo / AuditTo、候选 JSONL 三条出口。见[契约第 85–88 节](../features/unified-logging-contract.md#l2-public-query-final-output) |
| `4728e977` | API 成功写入 5xx 后绑定响应反馈 ID，.NET、Node、Lua 与接收端兼容缺省字段。Flutter 应用引导和 DbMigrate 两条标准流的安全边界已经本地回归。见[契约第 89 节](../features/unified-logging-contract.md#l2-diagnostic-output-boundaries)与[实施记录](./unified-logging-l2-diagnostic-output-boundary-audit-2026-11-12.md#implementation) |

方案与记录提交 `546b0968`、`dd827817`、`1fcf433f`、`17fe77e3`、`dd892696`、`419513d6`、`cb7c03c0` 的阶段判断已和后续实现对照。已实施切片以当前契约为准，准备记录保留当时的基线和证据。日志 → Native 平台 → 客户端连接的顺位沿用已确认的整体路线。[收口规划记录](./planning-closeout-2026-11-10.md)

## 更新与恢复顺序

- 日志指南的覆盖索引更新到第 89 节。API 指南明确响应反馈 ID 和 W3C 关联的对应关系。敏感数据指南补齐任务关联、Flutter 与 CLI 的实际边界。
- 总纲和衔接方案同步已实施的诊断裁决、`ConsoleHelper` 移除及内网端口的现行接收范围。历史审计快照保留原日期。契约中早期错误结果构造的历史待办已指向第 57 节修复。
- 下次先有限核对已登记来源与证据，确认隔离环境及运行范围，再以 API 5xx、DbMigrate stdout 和独立应急 stderr 验证真实 collector → 文件 / 内网 API → SQLite，随后接续 PostgreSQL、传输上界、磁盘 / 队列故障与恢复。索引、受权查询和原生设备分别按 L3、L4 与 Native 批次推进。[接下来的事项](../planning/current.md#接下来的事项) · [联验顺序](../features/unified-logging-l3-ingest-plan.md#l1-l3-first-runtime-verification)

## 验证

`git diff --check` 与 `npm run check:repo-quality:changed` 通过，保留日志指南、事件契约与记录索引的篇幅提醒。代码回归沿用各实现记录。本次复核只修改 Docs 文档，真实服务、容器、存储和设备验收留在对应批次。
