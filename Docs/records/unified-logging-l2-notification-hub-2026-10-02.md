# L2 通知 Hub 连接生命周期日志治理

日期：2026-10-02（Asia/Shanghai）。本批范围为 `NotificationHub` 自有生成点及其连接 / 断开直接行为，承接通知创建与推送治理。

## 实现与行为保留

- 异常断开原来携带 Exception、用户 ID 和连接 ID 输出 Warning；现使用 `notification.connection_closed` Warning，仅记录受控 failureKind。正常连接 / 断开安静，身份、凭据、异常 Message / Data 不进入本事件。
- 日志位置仍在身份标准化与分组移除之后。身份解析或清理失败原样上抛，不提前记录传入的断开原因，不吞错误、不补重试。
- 连接初始化仍依次执行身份检查、入组、读取权威 summary、发送 NotificationInboxChanged、发送 UnreadCountChanged、基类回调；保留 revision / 未读计数 / Connected reason / 禁止预览等载荷。
- 保留 query access_token 优先、header Bearer 大小写与 Trim、原始 header 及缺少 HTTP context 的既有读取边界；认证仍使用统一 ClaimsPrincipalNormalizer。连接时非正用户 ID 仍拒绝，断开时仍跳过非正 ID 的分组移除。

## 框架所有权与限制

本机 `Microsoft.AspNetCore.App` 为 10.0.8。核对同版本 [HubConnectionHandler 源码](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.8/src/SignalR/server/Core/src/HubConnectionHandler.cs)：

- 初始化回调失败由框架记录 ErrorDispatchingHubEvent，发送关闭消息后返回。
- 消息处理失败由框架记录 ErrorProcessingRequest，并将异常交给断开回调；客户端 CloseMessage 也可携带断开原因。
- 断开回调自身失败由框架记录 ErrorDispatchingHubEvent 后继续抛出。

因此，本事件只承诺 Hub 自有生成点安全；不将同一连接故障可能存在的框架事件隐藏为“全链只记一次”。本批未接管 SignalR handler、dispatcher 或传输层日志；旧路径的框架异常输出仍待治理，候选路径对未登记来源的安全摘要也不能当作迁移完成。框架判断来自版本源码核对，不是运行态框架调度验收。

## 验证

- 首轮 Hub 定向测试 **53 / 53** 通过。复核发现新测试只切换宿主环境而候选 Mode 仍默认 Production，已为本日三批新测试显式配置 `RadishLogging:Mode`，扩大到三批相关测试后 **124 / 124** 通过，无跳过；包含关注、通知创建 / 收件箱、通知 Hub 与 RuntimeLogPolicy / RuntimeLoggingAdapter。
- 新回归使用真实 Hub 方法、mock SignalR / 业务依赖，覆盖旧 / 候选 × Development / Production；验证正常调用顺序与完整初始化字段、异常类别安全输出、日志在清理之后、失败保留原实例、身份拒绝、凭据选择及清理失败不掩盖。
- Node 日志契约 **27 / 27** 通过。
- API `--warnaserror` 构建通过，0 警告、0 错误。最终 12 个改动文件文本卫生、文档链接与 `git diff --check` 通过，记录索引 310 行超过建议 300 行，为非阻断提醒。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~UserFollow|FullyQualifiedName~NotificationHub|FullyQualifiedName~NotificationCreationLoggingTests|FullyQualifiedName~NotificationServiceTest|FullyQualifiedName~NotificationUserBlockPolicyTest|FullyQualifiedName~ReliableTaskProcessorNotificationTest|FullyQualifiedName~NotificationInboxRepositoryTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

未安装依赖、启动真实宿主 / 数据库 / 容器、访问生产或切换日志开关。没有执行真实 WebSocket、框架调度、认证中间件或浏览器验收；上述边界未改动，也不以 mock 结果宣称已验收。

## 下一批

`UserInteractionRealtimeNotifier` 向 Chat / Notification 双 Hub 推送关系版本失效时仍在 catch 中输出异常、用户 ID 和关系版本；直接消费者为 ReliableTaskProcessor。下一批核对发送失败后的继续处理和 Outbox 完成边界，保留 best-effort、接收者去重、关系版本载荷与屏蔽规则。ChatHub 自有日志及 SignalR 框架来源继续作为 L2 剩余项。
