# L2 用户关注通知入队日志治理

日期：2026-10-02（Asia/Shanghai）。本批承接已确认的 L2 首项，范围为 `UserFollowService` 通知准备 / 入队及其直接消费边界。

## 实现与异常所有权

- 原日志将异常及关注者 / 目标用户 ID 交给 Warning sink 后重抛。本批只对通知阶段的 `ArgumentException / InvalidOperationException` 输出一次 `user_follow.notification_enqueue_failed` Error，仅含受控 `failureKind`，不传递 Exception 对象。
- 唯一直接生产消费者 `UserFollowController.Follow` 将这两类异常转换为原有 400 / 404 业务响应，因此不能依赖全局异常日志。普通参数拒绝、自关注、目标不可用、屏蔽拒绝不因本次治理新增事件。
- 其余通知异常不在 Service 记录，原样传播到 API：一般异常及 5xx BusinessException 由最终边界输出一次 `http.failed`；4xx BusinessException 保持安静。Controller 捕获范围、错误码、MessageKey 和原文响应保持不变；本批不宣称响应正文也已完成安全治理。
- 事件码登记在共享策略；旧 / 候选日志都不含用户 / 租户身份、通知正文、业务键或原始异常文本。正常关注与重复关注保持安静。

## 保留的业务与可靠性边界

- `UserFollowRepository.FollowAsync` 自行提交关注事务后，Service 才读取通知资料并调用 `ReliableOutboxService.AddAsync`。当前两次写入不是同一事务，本批没有移动任何事务边界。
- 关注已提交而通知准备 / 入队失败时，异常按原路径处理；再次关注因关系未变更而不补投。该可靠性限制不能因本次日志改造视为修复，也不新增重试、补偿或吞异常。
- 通知类型、优先级、标题 / 内容、接收者、actorName、UserProfile 目标、身份快照、租户、UTC 时间、通知业务键和任务幂等键均保留。Outbox 业务载荷与运行日志分别处理。
- 入队后的 `ReliableTaskProcessor` 与通知持久化 / 推送不属于本批改动；Outbox 领取、租约、重试、最终失败与审计语义没有变化。

## 验证

- 后续校准：初轮只显式切换宿主环境，候选日志模式仍默认 Production；同日 [Hub 批次](./unified-logging-l2-notification-hub-2026-10-02.md)已补齐候选 Mode 并完成三批相关回归，开发 / 生产模式证据以该次复测为准。
- 定向 .NET 测试 **63 / 63** 通过，无跳过：新增关注日志测试、既有关注 Service / Controller / SQLite 仓储测试及 RuntimeLogPolicy / RuntimeLoggingAdapter 测试。
- 新增测试覆盖旧 / 候选 × Development / Production 四种组合；使用真实 Service、Controller、ReliableOutboxService，mock 仓储及内存 HTTP 错误管道。验证通知资料 / 头像读取 / Outbox 写入的消费与上抛异常、原异常实例、原响应、缺失 Outbox、成功载荷、重复关注及安全输出。
- 首轮 2 项失败是新测试将来源类名 `ApiExceptionHandler` 误判为异常泄漏；修正过宽断言后全部通过。
- Node 日志契约测试 **27 / 27** 通过。
- API `--warnaserror` 构建通过，0 警告、0 错误；文档链接、8 个改动文件的文本卫生与 `git diff --check` 通过。记录索引 308 行超过建议 300 行，保留为非阻断篇幅提醒。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~UserFollow|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

测试证据不替代真实宿主、PostgreSQL、并发、权限中间件或浏览器验收；未安装依赖、启动宿主 / 数据库 / 容器、访问生产、发布或部署。生产候选开关保持 false，L2 尚未整体关闭。

## 下一批

继续通知创建及直接消费链：`NotificationService.CreateNotificationAsync` 在按偏好抑制全部接收者时仍逐条输出通知 ID 与分类，直接消费者为 `ReliableTaskProcessor`。下一批先核对通知创建、收件箱与 Outbox 重试 / 最终失败边界，保留偏好、屏蔽、租户、幂等和推送规则；本批只定位，不提前扩入实现。
