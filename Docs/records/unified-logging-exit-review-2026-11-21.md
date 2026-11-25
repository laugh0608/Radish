# 统一日志出口评审（2026-11-21）

以 `dev@9349e611` 为基线。**OrbStack / Linux arm64 隔离候选通过出口评审**，关闭现有登记范围的功能门禁，工程主线转入 Flutter Native P8-C Windows readiness。依据为 11 月 19 日独立 Compose、双挂载和留存联验，以及 11 月 21 日 Console 和跨版本联验。评审复核既有证据和源码，未重启环境。[收口条件](../features/unified-logging-governance-design.md#closeout-batches) · [最终联验](./unified-logging-exit-console-2026-11-21.md)

## 登记来源与诊断

| 核对面 | 裁决及证据范围 |
| --- | --- |
| L2 登记来源 | 事件契约第 7–89 节与调用清单覆盖三宿主、Common / DbMigrate、已登记框架、Service / Controller / Job / Hub，以及浏览器本地公共出口。按各事件的精确来源、身份、等级和字段白名单核对；由固定模板重建事件，维持事务、响应、重试和异常归属。旧链 WriteTo / AuditTo 和候选出口的证据以各切片为准。 |
| 七类容器来源 | API、Auth、Gateway、DbMigrate、Frontend、PostgreSQL、Redis 在独立 Compose 中均获得真实 daemon 标签、事件键与 JSONL / Log 库两路确认。联验覆盖已选真实链路及合成故障；逐个业务来源仍以各自的本地回归为证。 |
| 首版关联 | API 已写出的 5xx 使用同一服务端反馈 `requestId`，W3C trace / span 采用独立类型化来源，任务只保留 Hangfire / Outbox 的登记派生 `operationId`。真实接收已验证反馈 ID 在响应、JSONL 与 canonical JSON 中一致，Console 详情显示该字段。独立列、索引和按反馈 ID 精确筛选仍待交付，当前首版可用查询维度以 L4 契约为准。 |
| CLI 与应急 | DbMigrate stdout 保留受控命令报告，按可信服务和流元数据在解析前分出，stderr 阶段诊断与退出码沿既有契约。collector 默认 `error` 的独立有界诊断足以识别 ENOSPC 与坏块拒读；入库应急绕开失败链。处置使用实例、窗口、计数差、保全块与两路交付。 |
| 边界外通道 | 浏览器公共出口与 Flutter 框架引导摘要属于本地输出。Flutter root isolate、引擎、插件与子 isolate 随目标设备核对；外部 EventPipe / DiagnosticListener / Activity / metrics、未注册 OpenIddict 集成、Socket `.Client` 和 HttpSys delegation 沿原边界处理。 |

[事件契约](../features/unified-logging-contract.md) · [调用清单](../features/unified-logging-l3-ingest-plan.md#l2-final-output-first-slice) · [真实监听与 CLI 分流](./unified-logging-l1-l3-sqlite-runtime-2026-11-17.md) · [七类容器证据](./unified-logging-l1-l3-compose-recovery-2026-11-19.md)

## 故障处置与数据保全

- 满卷轮两路共有 167 个已交付键，输入创建失败增量 0、写入失败增量 1。计数归属输入实例与窗口；未观测的 Forward 连接尾段数量为**未知**，合成发送数与两路交集之差不能作为实际损失计数。释放空间后两条新事件分别到达两路，活动缓冲归零，同一告警保留人工确认与系统恢复轨迹。PC 和 mobile 查询、权限、告警及确认冲突通过。[11 月 21 日联验](./unified-logging-exit-console-2026-11-21.md#隔离联验结果)
- 沿用所有者已确认的普通运行日志首次同步前崩溃丢失裁决。两种挂载在首次同步前强杀各有一个可定位的合成键缺口，原坏块和 CRC 证据保全。无法归属的生产者缓冲与网络在途范围保持未知；联验 flush 间隔不作为实际损失上限。独立审计与业务流水沿各自持久化契约。[双挂载联验](./unified-logging-l1-l3-storage-recovery-2026-11-19.md) · [裁决](../features/unified-logging-l3-ingest-plan.md#l1-l3-storage-recovery)
- 计划停写先截止生产者输入，保留维护接收排空；503 窗口保全校验有效的待投递块，停写后取一致快照及配置清单。恢复按键与内容摘要对账，仅对已证实重放窗口容许 JSONL 同内容副本，Log 库保留唯一键。最终候选核对了恢复后的 219 条事件、告警与轨迹、JSONL、缓冲和采集配置。旧版登录、旧令牌跨升级、旧数据只读及前向 schema 回滚均有证据；回滚保全新旧数据。[维护停写与重放](./unified-logging-l1-l3-compose-recovery-2026-11-19.md) · [升级恢复记录](./unified-logging-exit-console-2026-11-21.md)

已复核最终 `report.json`、`SHA256SUMS` 和清单中八张截图摘要，最终报告为 `passed`、清理为通过。独立 Compose 报告、双挂载报告和两份合成坏块摘要均与原记录相符。

## 独立后续项

- **实际部署预检**。核实目标架构、七类源及 collector 的实际镜像 digest、daemon 到 Forward 的路径、逐容器日志配置、文件系统和容量、停写与快照顺序。复核目标处置与真实断电边界，保留在途尾段未知。正式候选制品与目标部署各留证，生产候选开关维持关闭。[部署预检](../features/unified-logging-l3-ingest-plan.md#l1-l3-compose-preflight)
- **慢 SQL 安全指纹**。同语句聚合仍等待稳定、可分组且不暴露原始 SQL、参数和值的安全指纹，另行审查输入、产出与回归，再启用相应聚合。[首轮查询和告警契约](../features/unified-logging-governance-design.md#首轮查询和告警入口)
