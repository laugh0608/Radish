# 2026-09-23 统一日志 L2：SQL、异常与 CLI 生成端治理

## 范围与结论

承接 [L2 入口层](./unified-logging-l2-producer-entry-2026-09-19.md)，按项目所有者本批授权实施 SQL、AOP / 事务、最终异常与 DbMigrate Program / Runner / Doctor 治理。稳定契约见[事件契约](../features/unified-logging-contract.md)和[日志系统](../guide/logging.md)。本批关闭上述子项，不将整个 L2 标记完成。

## 实现

- SQL 生成器只接收操作类别、参数数量和耗时，表 / 用户只用于开发诊断筛选；不读取参数值或输出 SQL 文本。普通诊断默认关闭且要求显式 Development 诊断；慢操作 / 连接独立启用，阈值分别 1000 / 500ms。旧数据库 sink 的 SELECT 诊断过滤不再屏蔽慢查询。Log 库递归排除、PostgreSQL 时间参数规范化和 SQLite PRAGMA 均保留。
- 移除 Service AOP 与注册，消除参数 / 返回对象快照及额外异步包装；事务 AOP、UnitOfWork 删除裸输出和重新抛出前的重复 Error。提交、回滚、保存点及既有异常传播代码保持原有语义。
- API 已处理 5xx 生成一次 `http.failed`，普通业务 4xx 不逐条 Warning；保留响应码、消息和错误契约。异常对象、路径与消息不进入旧 sink；受控 `failureKind` 提供固定异常类别。框架已处理异常诊断被抑制，无法处理的异常继续由框架负责。
- 四个入口通过 `RuntimeProcess` 覆盖配置加载前至运行退出。失败输出安全 Fatal JSONL 到 stderr，退出 1；成功退出 0。原先依赖 CLR 未处理异常的失败现在明确为 1，不依赖平台特定的崩溃退出码；doctor / verify 的成功与失败判定保持不变。`HostAbortedException` 继续交给测试 / EF 工具。Host 的 EventId 11 仅在顶层边界接管时抑制，避免重复 Error 和旧 sink 输出异常原文。
- DbMigrate 引用已有 `Radish.Extension.Log` 项目，无新增包或版本变更。CLI 诊断固定复用统一 JSONL 策略写 stderr，命令报告保留 stdout；命令类别、阶段、变更数量、受控索引名和耗时可追溯。doctor 的连接目标、缺失文件与第三方异常使用安全说明。业务审计和 schema ledger 没有改为异步日志，也未改变迁移顺序。
- 三个 Web Program 的主体缩进变化来自增加统一顶层边界；逻辑差异仅为该边界及 `RunAsync`。

## 验证

- .NET 定向回归：74 通过、1 跳过。覆盖生成策略、旧 / 新输出、慢查询独立性、SQL 时间规范化、API 异常响应与单次记录、同步 / Task / Task<T> 异常传播、CLI 报告与退出判定、论坛 / Wiki 回滚、SQLite 保存点和提交失败回滚。
- 唯一跳过项是 PostgreSQL 保存点集成测试：本任务未配置 `RADISH_TEST_POSTGRES_CONNECTION_STRING`，也未授权启动隔离数据库；不能据 SQLite 结果宣称 PostgreSQL 运行验收通过。
- Node 共享契约：最终策略变更后复核，27 通过。
- 四个宿主及测试依赖零警告构建通过（0 warning / 0 error）；schema ledger 回归 7 通过。`check:repo-quality:changed` 与 `git diff --check` 通过；日志指南收缩到硬上限以内，仍有指南和 records 索引两项非阻断篇幅提醒。

沙盒拒绝 MSBuild 命名管道后，按限定范围提权运行构建 / 测试。API pipeline 测试只构造内存请求管道；启动失败测试使用不监听端口的内存 Host；事务测试使用临时 / 内存 SQLite。没有启动真实业务服务、PostgreSQL、collector 或浏览器 Smoke，没有安装依赖、操作生产数据或发布镜像。

## 后续边界

`RadishLogging.Enabled` 继续默认 false，生产 Compose 未切换。下一批继续 seed、具体 migration 内输出、Auth seed、后台任务、Rust 与业务事件分类；异常安全栈帧与框架来源治理尚未全部完成。L1 正式传输上界 / 磁盘故障、L3 入库、L4 查询、L5 告警与 L6 切换仍按主专题推进。
