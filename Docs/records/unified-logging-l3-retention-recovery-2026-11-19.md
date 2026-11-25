# 统一日志留存与备份恢复隔离联验（2026-11-19）

OrbStack / Linux arm64 的 SQLite 和 PostgreSQL 16.15 均通过停写留存、备份恢复及 503 后待投递重放联验。两轮复用 Fluent Bit 5.1.2 候选 `sha256:c548922a71a15b99503d43b57df28eedf8fcf263b18122f0d27fa6e218b6218c`，代码基线为 `c3deda8b` 加本批工作区改动。记录日期采用 Asia/Shanghai，表中时间均为 UTC。[执行范围与命令](../features/unified-logging-l3-ingest-plan.md#l3-retention-recovery)

## 到期清理与数据恢复

- 未启用留存时，维护命令退出非零，过期样本仍在。显式启用后，每轮删除 Info 203 条、Warning 1 条、Error 1 条，保留各等级的期限内样本。再次执行，各级删除均为 0。已过期样本在恢复实例重投时计入 `retention-expired`，入库 0 条。
- 每轮封存 collector 的活动 JSONL，清除 1 个到期片，容量移除均为 0。精确截止点、历史日志和审计隔离，以及缩小预算后的容量淘汰与到期原因区分，均有定向回归。
- SQLite 通过备份 API 生成一致性副本，恢复为新目录中的 `log.db`，`PRAGMA integrity_check` 为 `ok`。PostgreSQL 通过 `pg_dump --format=custom` 备份隔离 Log 库，用 `pg_restore --single-transaction --exit-on-error` 恢复至同一隔离实例中新建的 `radish_log_restored`。两轮各完整比对快照中的 26 条运行事件和 1 个拒收日桶，`DbMigrate verify` 通过。JSONL、持久 chunk 与采集配置恢复前后的逐文件 SHA-256 一致。
- 快照前的已交付对照事件完整恢复。待投递事件在 HTTP 返回 503 前已写入 JSONL，快照时 Log 库仍无该 ID，持久 chunk 被纳入备份。原实例和恢复实例均以相同请求体 SHA-256 重投成功。恢复库中该事件只有 1 行，再投计为重复 1 条。恢复后的新事件在两路各出现一次。

## 临时停机、重放与缺口

| 运行 | 快照点 | 快照后原实例独有的对照区间 | 恢复后两路共同缺口 | 恢复后待投递 ID 的 JSONL / Log 份数 |
| --- | --- | --- | --- | --- |
| SQLite `9be955c3` | 12:47:35.952 | 12:47:46.418–12:47:47.671 | 2 条，ID 两路完全相同 | 2 / 1 |
| PostgreSQL `4cdf231d` | 12:49:15.966 | 12:49:26.461–12:49:27.709 | 2 条，ID 两路完全相同 | 2 / 1 |

两个恢复实例的待投递 ID 各多出一条内容一致的 JSONL 副本。原实例从同一停写点恢复投递时也各写出两份。恢复中的 JSONL 文件均可按完整行解析。这个重复需要在部署处置中按 eventId 辨认。缺口为本轮可核对的合成 ID，生产者侧额外缺口仍为未知。

更早的 SQLite 试验 `c1ddaa0a` 在首次 flush 前强制终止了 collector。待投递 ID 已可在文件系统 chunk 中找到，重启后 collector 报 `[storage] format check failed`，该 ID 未恢复交付。此轮保持失败。通过轮改为先观察 JSONL 交付和 HTTP 503，再停写备份可重试的 chunk。因此仍需在目标部署冻结可备份状态和停写流程，并处理首次 flush 前强制终止的证据及 JSONL 重复。持续运行的容量上限、真实断电与完整站点跨版本恢复仍待相应门禁。[剩余退出条件](../features/unified-logging-l3-ingest-plan.md#l1-l3-exit-checklist)

## 报告与清理

原始 JSON 保留在本机 `artifacts/logging-retention-recovery-probe/`。通过轮分别记录 6 项和 7 项断言，均为 `passed`、`cleanup=true`。按本批六次运行的精确标签检查，容器、网络和卷均为 0，打印的临时目录均已删除，其中包括备份、恢复副本、测试库和凭据。

| 报告 | SHA-256 |
| --- | --- |
| `radish-logging-retention-recovery-9be955c3.json` | `127187f4668e68cf37612f363d777014b4f796d5d65366b728cdcf6335e340c9` |
| `radish-logging-retention-recovery-4cdf231d.json` | `d1f90fa13ba541c3b6f899cc925e05f575b06560bffb1bd5dac5621fc4b90937` |
| `radish-logging-retention-recovery-c1ddaa0a.json` | `7d59db1a341c174f1f702dbc22f05b1560ff02eaecdf16ba10c33141057efdac` |

60 项 .NET 定向回归、22 项探针回归和 73 项日志契约测试通过。
