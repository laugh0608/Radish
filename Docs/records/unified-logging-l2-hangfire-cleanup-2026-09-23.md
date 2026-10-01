# 日志 L2 Hangfire / 清理任务记录

> 日期：2026-09-23（Asia/Shanghai）。承接 Outbox / Rust 批次，经项目所有者确认继续 Hangfire 重试边界与清理任务。实现规则见[日志契约第 10 节](../features/unified-logging-contract.md)，不关闭 L2 整体。

## 实现与行为边界

- Hangfire 状态观察器在现有 AutomaticRetry 之后记录安全决定：继续重试为 Warning，保留 Failed 或转为 Deleted 为 Error；不修改状态、次数、延迟和存储审计。普通调度、成功与无失败状态的停机重入队安静。关联使用稳定 operationId，不输出任务参数与异常原文。
- 原 AutomaticRetry 日志关闭；独立 provider 不求值框架消息工厂，保留其他框架 Warning / Error / Fatal 的固定事件与 failureKind。框架 Info 及以下作为 Debug 诊断处理，候选 Production 下丢弃。API 注册在 AddHangfire 配置回调中，覆盖框架默认 provider；旧 / 候选 sink 均接收安全摘要。
- 观察器处理的是**状态选举结果，发生在存储提交之前**。它不代表提交完成，不承诺存储故障、崩溃或重放情况下全局只输出一次。Hangfire 原状态 Reason / Exception 审计保持原义，不属于本批运行日志输出。
- 收件箱、Wiki 草稿与 Chat 回应清理只在有变更时记录完成；收件箱关系单独删除也有摘要，容量警告聚合为数量。清理失败仍抛出给 Hangfire；保留方法级 2 次重试与 60 / 300 秒延迟，Outbox 仍禁用自动重试。
- 文件四类清理保留返回值、保留期、引用保护和分片路径排除。单项失败继续、外层失败返回 0 的既有行为未变；由消费异常的本层汇总一次 Error。日志区分实际移动文件数与原有处理记录数，缺失文件和目录异常汇总 Warning，纯空批次安静。
- 未安装 / 更新依赖，未启动业务宿主、数据库容器或浏览器 Smoke，未切换生产日志。商城、抽奖、神评、奖励及服务内其他清理分支仍待治理。

## 框架核对依据

核对项目锁定的 Hangfire.Core 1.8.14 官方源码：[AutomaticRetryAttribute](https://github.com/HangfireIO/Hangfire/blob/v1.8.14/src/Hangfire.Core/AutomaticRetryAttribute.cs)、[StateMachine](https://github.com/HangfireIO/Hangfire/blob/v1.8.14/src/Hangfire.Core/States/StateMachine.cs)、[Worker](https://github.com/HangfireIO/Hangfire/blob/v1.8.14/src/Hangfire.Core/Server/Worker.cs) 及 [AddHangfire 注册](https://github.com/HangfireIO/Hangfire/blob/v1.8.14/src/Hangfire.NetCore/HangfireServiceCollectionExtensions.cs)。存储状态变更错误仍可能由 Worker 单独处理，因此只抑制 AutomaticRetry 的已接管日志，不全局丢弃框架 Error。

## 验证

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~CleanupLoggingTests|FullyQualifiedName~FileCleanupJobTest|FullyQualifiedName~ReliableOutboxJobTest|FullyQualifiedName~RuntimeLog|FullyQualifiedName~ProducerLoggingTests|FullyQualifiedName~SeedLoggingTests'
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror
npm run check:logging-contract
npm run check:repo-quality:changed
git diff --check
```

- .NET 定向回归：87 通过、0 失败、0 跳过。新增 CleanupLoggingTests 共 18 项，使用真实 AutomaticRetry 状态选举与 mock 存储验证继续重试、零延迟入队、耗尽保留 / 删除和 Attempts=0；核对次数与 60 / 300 秒延迟、无日志层提交或状态修改。测试不构成真实 Hangfire Server / 数据库存储提交验收。
- 覆盖旧 / 候选输出安全、消息工厂不求值、空批次、关系单独清理、容量汇总、文件缺失 / 成功 / 部分失败、外层异常消费、保留期钳制与仓储异常继续传播。原有文件分片排除、引用保护和 Outbox 行为回归通过；临时文件在测试结束清理。
- Node 共用日志契约 27 项通过；API 构建 0 警告、0 错误。测试编译仅保留上一批 ProducerLoggingTests 的 xUnit1051 提示，本批未新增警告。
- 首次沙盒构建等待 5 分钟后退出，未给出编译诊断；沙盒外无 restore 构建与测试正常完成，未改工程配置规避问题。
- 仓库卫生、文档链接与 diff 检查通过；logging 手册与 records 索引保留已有篇幅软提醒。

## 后续

继续商城 / 抽奖 / 神评 / 保留奖励与服务内清理分支，再补剩余业务事件、完整安全栈帧与其他框架来源。Rust `.tmp` 水印回退仍为独立待确认修复项。`RadishLogging.Enabled=false` 保持不变。
