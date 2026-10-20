# L2 用户关系失效推送日志治理

日期：2026-10-02（Asia/Shanghai）。范围为 UserInteractionRealtimeNotifier 双 Hub 降级日志与 ReliableTaskProcessor 直接消费边界。

## 实现与保留行为

- 原两个 catch 输出原始异常、动态 Hub 名、用户 ID 与关系版本；现分别记录 `user_interaction.chat_push_failed / user_interaction.notification_push_failed` Warning，仅保留固定类别 failureKind。
- 保留有效接收者过滤、输入顺序去重、每人先 Chat 再 Notification、InvariantCulture 字符串版本和不含屏蔽方向的载荷。分组访问或发送失败后继续后续尝试，每个失败保留一条日志；单次最多四次发送，不改变失败数量口径。
- 直接消费者仍先在 Blocked 路径抑制双方通知，Unblocked 不恢复历史通知，再调用关系失效推送。推送 best-effort 失败被消费后 Outbox 可成功完成；抑制数据库失败原样上抛，由 Outbox 按原重试策略处理；未知关系事件仍为永久失败。
- 没有修改屏蔽规则、事务、分组名、权限、关系版本、权威失败摘要、重试、租约或审计，也未接管 ChatHub / SignalR 框架日志。

## 验证

- 定向 .NET 测试 **51 / 51** 通过，无跳过；包含 UserInteractionLoggingTests、既有 notifier / Processor 用户屏蔽测试，以及 RuntimeLogPolicy / RuntimeLoggingAdapter。
- 新测试覆盖旧 / 候选 × Development / Production；候选模式显式配置并从输出核对。验证非正 ID 过滤与去重、纯字符串版本载荷、两 Hub / 两接收者 / 分组与发送失败后的继续处理、安全事件顺序及四次降级仍完成任务。
- 使用真实 notifier、ReliableTaskProcessor、ExecutionJob、ReliableOutboxService / Repository 和内存 SQLite Outbox，配合 mock SignalR / 收件箱；验证 Blocked 双向抑制、Unblocked 不恢复、前置失败重试及未知事件死信。既有 Processor 回归验证抑制失败保留原异常实例。
- Node 日志契约 **27 / 27** 通过。
- API `--warnaserror` 构建通过，0 警告、0 错误；8 个改动文件文本卫生、文档链接与 `git diff --check` 通过。记录索引 311 行超过建议 300 行，为非阻断篇幅提醒。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~UserInteractionLoggingTests|FullyQualifiedName~UserInteractionRealtimeNotifierTest|FullyQualifiedName~ReliableTaskProcessorUserBlockTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

未安装依赖、启动真实宿主 / 数据库 / 容器、访问生产或切换日志开关。上述证据不替代真实 SignalR、屏蔽事务、PostgreSQL、浏览器或框架调度验收。

## 下一批

ChatHub 连接、断开及加入 / 离开频道的自有日志仍含用户、连接或频道信息。下一批先核对 presence、分组清理、直接 Service 与异常所有权，保留认证、频道权限、租户组名、调用顺序和输入中广播。SignalR 框架来源继续后置，L2 尚未关闭。
