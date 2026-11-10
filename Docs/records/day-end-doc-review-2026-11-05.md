# 2026-11-05 日终代码与文档回顾

> 时间口径：Asia/Shanghai。审阅今天全部六个提交（`fee65b97` 至 `f04a0bce`，对照基线 `1551ad52`），含四个代码提交与两个方案提交；本次另作纯文档收尾。今天停止实现，下一次建议见[接下来的事项](../planning/current.md#接下来的事项)。

## 今日全部提交

按时间从早到晚排列。测试数量属于各批当时的筛选集，存在重叠，不累计为独立用例总数；日终未重跑代码测试。

| 提交 | 修改与完成结论 | 验证与事实来源 |
| --- | --- | --- |
| `fee65b97` | 浏览器共享日志入口、两应用固定目录、三级 / diagnostic、有界安全快照与 HTTP 调用迁移；保留回调、刷新、响应及错误对象语义 | [浏览器记录](./unified-logging-l2-browser-output-2026-11-05.md)：三 workspace 共 786 项通过，类型检查 / 构建 / Lint 通过；无真实浏览器 Smoke |
| `7002cd75` | 默认关闭的内网接收、凭据和监听隔离、有界 JSONL、分批事务 / 幂等 / 拒收聚合、Log migration 及防递归诊断 | [L3-A 记录](./unified-logging-l3-ingest-local-2026-11-05.md)：63 项专项、343 项扩展回归、52 项 Node；SQLite / 内存管道证据，不含 PostgreSQL 或真实监听 |
| `29e8b4f4` | 明确七个自有来源的事件、消费与输出边界，冻结下一批方案 | [衔接方案第 8 节](../features/unified-logging-l3-ingest-plan.md#8-已批准切片剩余七个自有来源)；方案提交不代表当时已完成实现 |
| `12f739f9` | 七个来源收口，补直接 ApiExceptionHandler 的最终输出，保留原限流 / 缓存 / 写入行为 | [自有操作记录](./unified-logging-l2-owned-operations-2026-11-05.md)：492 项扩展及 54 项补充回归、56 项 Node；不同筛选集有重叠 |
| `301dce8d` | 冻结 14 个 Controller 精确事件目录，复现 Reaction 旧链 scope 污染，核对命令输出例外 | [准备记录](./unified-logging-l2-controller-output-readiness-2026-11-05.md)；临时探针验证后删除，缺口复现不是修复验收 |
| `f04a0bce` | 14 个 Controller 固定来源 / 命名 EventId、逐事件最终输出与固定 unknown；保持响应构造和异常归属 | [Controller 收口记录](./unified-logging-l2-controller-output-2026-11-05.md)：911 项 .NET（含新增 41 项）、70 项 Node，构建 0 警告 / 0 错误 |

## 代码与文档对照

本次查看六个提交的改动目录，并沿公共日志实现、调用点、接收 / 存储 / migration、宿主接线和对应测试核对相关文档；不扩展为全仓代码审计。

1. **浏览器**：`@radish/http/logging` 导出公共实现，两应用 logger 绑定目录与 env；诊断要求 Development 且 debug，最终 console 只收到固定格式和序列化快照。前端日志指南、事件契约第 74 节与代码一致，无需再复制实现细节；敏感数据指南补充独立浏览器出口已完成及第三方 / 调用前求值限制。
2. **接收与存储**：核对 `RuntimeLogIngestOptions`、API Setup / Controller、Service / Repository、RuntimeLogIngestScope 及 migration / 实体。总纲原“拟新增”改为默认关闭的本地实现；区分 Common 传输契约 `RuntimeLogEvent` 与实体 `RuntimeLogEventRecord`，补拒收聚合模型。更正拒收保存 eventId 的旧描述，明确只保存受信来源 / 原因 / 日桶 / 计数；当前内网端口只允许接收路径，没有新探活接口。
3. **切换边界**：总纲明确 SQLite 事务 / 重放已有本地证据，正式传输上界、PostgreSQL 和 collector 隔离仍待验证；旧表只读是退出旧写入后的目标，当前默认旧链仍写入。配置与数据库指南已对应开关、独立凭据、Log scope 和 migration，本次保留；运行手册补充 404 隔离行为及配置入口。
4. **自有来源及 Controller**：核对 ApplicationOperationLogSink 与旧 WriteTo / AuditTo、候选接线，继续限定精确来源，不把同码 Service 自动纳入；命名 EventId、未知身份、逐事件字段与诊断门禁已对应契约第 76–77 节。日志指南更新到第 77 节并补来源索引，敏感数据指南补新来源与接收防递归边界。构造响应、Message 求值、事务与重试保持原规则。
5. **规划**：current 的“下一项工作”和“接下来的事项”聚焦已登记 Service / Job / Hub 的有限核对、共享 helper 分组和验证矩阵；移走重复的今日批次叙述，最新回顾链接改为本页。浏览器、七个来源、14 个 Controller 与 L3-A 本地实现不重新挂为待实施，Native P8-C 与客户端连接顺位不变。
6. **证据归属**：原批次记录保留当时测试数量、失败与修正、基线和限制，不回写本地 hash、结果文件或历史候选。根 AGENTS / CLAUDE、依赖 / lockfile、代码和运行配置本次未修改；没有为日终回顾启动服务、容器或访问真实存储。

## 未关闭边界与下次恢复

- 按现有事件契约核对 Service / 后台任务 / Hub 的精确来源、共享生成 helper、最终消费及三条出口，只列可复现缺口；已有完整证据直接引用，不恢复任意外部通道调查。
- 分组方案需同时说明事件 / 字段、scope 污染、异常所有者、原业务副作用与输出失败验证，确认整批范围后再实施。诊断栈帧 / 权威上下文和 Flutter 日志不自动混入该批。
- L1 传输上界 / 磁盘 / 平台、L3 真实监听 / PostgreSQL / collector / 留存恢复，以及查询、告警、正式切换仍未完成。生产候选开关继续关闭，未执行 PC / mobile Smoke、push、PR、tag、发布或部署。
- 既有可靠性问题继续沿 current 所链接的原专题处理。L3 批次发现的 Wiki `DateTime.Now` 时间语义 baseline 问题仍保留；今日没有刷新 baseline 或宣称全仓 baseline 通过。浏览器构建的大 chunk 提醒、Console 权限未引用提示和 日志指南 / records 索引篇幅提醒不冒充已解决。

## 本次文档验证

`npm run check:docs`、`npm run check:repo-hygiene:changed` 与 `git diff --check` 均通过；保留日志指南及 records 索引既有篇幅提醒。提交前复核仅包含 Docs 文档。未重跑代码构建 / 测试，代码验证引用上表各批记录。提交完成后今天停止，不继续下一批实现。
