# L2 通知创建与实时推送日志治理

日期：2026-10-02（Asia/Shanghai）。本批承接关注通知入队治理，覆盖通知创建及其直接依赖的 SignalR 推送降级日志。

## 实现与保留边界

- `NotificationService` 删除按偏好抑制全部接收者的逐条 Info，移除无剩余用途的 logger 构造依赖；普通偏好或屏蔽抑制仍返回通知 ID、不写空通知、不推送，可靠任务正常完成。正常创建与幂等返回保持安静。
- `NotificationPushService` 保留 best-effort catch，用 `notification.push_failed` Warning 与受控 failureKind 替代原始异常、用户 ID 和 revision。分组访问、新 revision 事件、兼容角标事件失败均保持原有消费边界；前一个发送失败不会继续后一个发送。
- `ReliableTaskProcessor` 保留 Outbox 租户 / 发生时间覆盖、NotificationRequested 参数 / JSON 异常转永久失败的契约；其余异常继续由 ExecutionJob 交给 Outbox 处理。重试或死信日志只由完成状态写入的 Repository 记录，权威错误摘要不因运行日志治理被改写。
- 通知定义、接收者规范化、强制通知、偏好、屏蔽、目标 / 模板、身份快照、入箱持久化、幂等、revision 和两种推送载荷均未改变。推送降级不回滚持久化、不触发 Outbox 重试，不新增重发机制。
- 直接读取路径与 `NotificationHub` 生命周期日志尚未整体治理；不将本批视为整个通知或 SignalR 系统收口。

## 验证

- 后续校准：初轮只显式切换宿主环境，候选日志模式仍默认 Production；同日 [Hub 批次](./unified-logging-l2-notification-hub-2026-10-02.md)已补齐候选 Mode 并完成三批相关回归，开发 / 生产模式证据以该次复测为准。
- 定向 .NET 测试 **68 / 68** 通过，无跳过：新通知创建日志测试、既有通知 Service、屏蔽政策、可靠通知 Processor、SQLite 收件箱仓储、RuntimeLogPolicy / RuntimeLoggingAdapter。
- 新测试覆盖旧 / 候选 × Development / Production；真实 NotificationService、PushService、ReliableTaskProcessor、ExecutionJob、ReliableOutboxService / Repository 和内存 SQLite Outbox，mock 用户 / 收件箱仓储及 SignalR 客户端。
- 验证全部抑制不写不推且任务成功、混合接收者与源事件上下文、推送分组 / 第一事件 / 第二事件失败均不重试、通知依赖失败的重试耗尽 / 永久失败日志安全。收件箱 mock 的幂等返回验证 Service 不补推；真实仓储幂等由既有 SQLite 回归覆盖。
- 调试时修正新测试的时间夹具：固定源时间必须配合同一领取时间，不能依赖机器当前时刻；同时按现有实现将成功任务 AttemptCount 断言为 0（仅失败落库累加），移除 nullable 警告。没有为测试修改产品时间或重试规则。
- Node 日志契约 **27 / 27** 通过。
- API `--warnaserror` 构建通过，0 警告、0 错误；11 个改动文件文本卫生、文档链接和 `git diff --check` 通过。记录索引 309 行超过建议 300 行，属于非阻断篇幅提醒。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~NotificationCreationLoggingTests|FullyQualifiedName~NotificationServiceTest|FullyQualifiedName~NotificationUserBlockPolicyTest|FullyQualifiedName~ReliableTaskProcessorNotificationTest|FullyQualifiedName~NotificationInboxRepositoryTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

本批没有启动真实项目宿主、数据库服务或容器，没有安装依赖、访问生产或改变生产日志开关。SQLite / mock 证据不替代 PostgreSQL、真实 SignalR 连接、浏览器或部署验收。

## 下一批

`NotificationHub.OnDisconnectedAsync` 仍直接输出原始异常、用户 ID 和连接 ID。下一批先核对分组清理、身份标准化、连接初始化与 SignalR 框架最终处理边界，再治理该生成点；保留原有认证和连接行为，不扩为完整实时通信改造。L2 尚未关闭，L1 剩余门禁及 L3–L6 继续按专题推进。
