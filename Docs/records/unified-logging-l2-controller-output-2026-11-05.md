# 2026-11-05 API Controller 最终输出本地收口

## 范围与结论

项目所有者批准连续完成[衔接方案第 9 节](../features/unified-logging-l3-ingest-plan.md#9-下一批api-controller-最终输出收口)的 14 个 Controller，准备方案提交为 `301dce8d`，代码基线为 `12f739f9`。本批不安装依赖、不启动项目服务或容器、不连接真实存储。

已补齐精确来源身份、逐来源事件目录与最终输出重建。此前 Reaction 旧链继承 Authorization scope 的缺口，现由永久回归验证剔除；不是仅依赖候选公共策略裁剪。事件目录与字段以[契约第 77 节](../features/unified-logging-contract.md#77-api-controller-最终输出边界2026-11-05)和方案表格为准。L2 整体仍未关闭。

## 改动与证据

- 13 个 Serilog Controller 在原调用位置增加固定类型 SourceContext；Attachment 两处 ILogger 使用固定命名 EventId。未移动日志与响应构造、未改变 catch 类型、异常包装、本地化、状态或业务调用。
- `ApplicationOperationLogSink` 扩展精确来源目录，按来源及事件重建；未知事件不继承 failureKind 或其他字段。statusCode 只属于 http.failed，cleanupOperation 只属于 upload.cleanup.failed；来源邻接名称与 Service 相同事件不匹配。
- 原代码 console、配置 WriteTo / AuditTo 与候选接线复用。保留 Timestamp、Level、强类型 trace/span；丢弃原异常、任意属性、字符串关联键与跨事件字段。原有六来源没有放宽规则。
- 共享策略新增 14 个固定 `controller.{owner}.unknown` 说明，Node / .NET 共享样本同步；候选链不再把这些已知来源的 unknown 降为 runtime.unclassified。生产仍不输出低等级 unknown；Warning 以上保留安全摘要。

| 验证层 | 实际证据与边界 |
| --- | --- |
| 完整 action + 服务替身 → 正式输出 | Coin 查询拒绝、PaymentPassword 503、Shop 取消、QuickReply 查询、Post 置顶、Comment 点赞；断言状态 / 结果、调用次数及污染剔除 |
| 真实私有消费方法 → 正式输出 | 反射调用 Reaction / Poll / Lottery / Question / Tag / Category 结果构造，Sticker 消费，Attachment 上传 / 记账，Shop 权益 / 商品 / 库存 helper；覆盖响应先构造、Message 再失败时无提前日志；不宣称 HTTP 端到端 |
| 每个登记事件及对抗输入 | 全部来源 / 事件经过正式旧 / 候选链；跨来源 / 缺失 / 未知身份、错误命名 EventId、所有等级诊断门禁、有效及非法状态、跨事件字段与强类型关联 |
| 原业务与异常链回归 | CoinMovement、PaymentPassword、AttachmentUpload、ShopFulfillment / Management / Entitlement，Reaction、QuickReply、Poll、Lottery、Question、Post、Comment、Tag、Category、Sticker；沿用原事务 / mutation、专用拒绝安静性、响应二次失败、消息读取及异常归属断言 |
| 共享边界防回归 | ApplicationOperation、RateLimitOperation、策略 / adapter、外层 HTTP / 异常管道、API 错误契约及 L3 本地接收；明确排除 Database=PostgreSQL |

新增两组测试分别为 `ControllerFinalOutputTests`、`ControllerConsumerOutputTests`。正式输出覆盖旧链 Development / Production 的 WriteTo / AuditTo、候选 Development / Production，另有代码 console。注入 Authorization、嵌套对象、伪造 SourceContext / EventCode / Diagnostic / SourceCategory、字符串 operationId；输出不含敏感哨兵。

旧链 WriteTo sink 抛错仍返回原消费结果，AuditTo 抛错仍传播；不会重试服务调用。候选 writer 失败仍返回原 404，安全应急输出只写一条限频摘要。真实 File / DB sink 故障未执行，不能由内存替身推定介质恢复已验收。

原 Tag / Category 的生成端字段断言同步新增 SourceContext，并精确核对类型；Reaction / Sticker 的生成端夹具补识别前批 ApiExceptionHandler 固定命名 EventId，只在该精确来源允许并核对 Id / Name，不放开任意额外字段。新正式输出测试仍要求最终事件剔除 EventId 载荷。

## 验证结果

- 全解决方案 Debug 构建、warn-as-error：通过，0 warning / 0 error。
- 本批及相关业务 / 共享边界定向 .NET 回归：911 项通过，0 失败 / 0 跳过，其中包含新增最终输出与消费点测试 41 项；不重复相加。
- `npm run check:logging-contract`：70 项通过，0 失败 / 0 跳过。
- 身份 Claim 与 Console 权限扫描通过；保留既有 `console.hangfire.replay` 未被前端入口引用的提示。
- 文档 / 链接、changed-only 文本卫生与 `git diff --check`：通过；records 索引保留既有篇幅提醒。

.NET 回归使用 `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore`；过滤器包含上表的 Controller / 业务类、`ControllerFinalOutputTests`、`ControllerConsumerOutputTests`、`ApplicationOperation`、`RateLimitOperation`、`RuntimeLogPolicyTests`、`RuntimeLoggingAdapterTests`、`OuterHttpFrameworkSinkTests`、`HttpExceptionPipelineLoggingTests`、`ApiErrorContractTest`、`RuntimeLogIngest`，并附加 `Database!=PostgreSQL`。构建命令为 `dotnet build Radish.slnx --no-restore -c Debug --warnaserror`。

## 保留边界与接续

- 使用内存管道、服务 / repository 替身及原夹具自管临时 SQLite；未 Start / Run、绑定端口、访问真实 Redis / PostgreSQL、执行实际业务迁移或 collector 联验。
- DbMigrate stdout 仍属于命令协议；ConsoleHelper 及其扩展未检出外部 C# 调用的核对结论沿用准备记录，没有修改工具或执行数据库命令。
- 下一步按已登记 Service / 后台任务 / Hub 的共享生成与真实消费链组织有限批次，不重启本批 Controller；诊断栈帧 / 权威上下文及 Flutter 保留独立边界。
- L1 传输上界 / 磁盘 / 平台，L3 真实监听 / PostgreSQL / collector / 留存恢复，后续查询、告警和切换仍未关闭。候选生产开关保持关闭；没有 push、PR、发布或部署。
- 未改动 Wiki 时间语义 baseline 问题，也未宣称全仓 baseline 或真实运行 Smoke 通过。
