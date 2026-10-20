# L2 ChatHub 自有日志治理

日期：2026-10-02（Asia/Shanghai）。范围为 ChatHub 连接 / 断开和加入 / 离开频道的自有日志及其直接行为。

## 实现与保留边界

- 移除逐连接 Info 及加入 / 离开频道 Debug；异常断开使用 `chat.connection_closed` Warning，仅记录受控 failureKind，不传递用户、租户、频道、连接身份或 Exception 对象。
- 保留加入时 Service 校验 → 入组 → presence、离开时 Service → 退组 → presence、断开时 presence → 用户组清理的顺序。断开日志只在清理成功后生成，清理自身抛错仍原样传播，不覆盖原错误。
- ChatService.JoinChannelAsync 仍只校验 CanJoinRealtime，不新增成员或推进游标；LeaveChannelAsync 仍为原返回。输入中 CanSend 校验、租户组名、OthersInGroup 及广播载荷不变。未改变凭据、认证、权限、presence 计数算法或重复加入语义。
- SignalR 框架来源未迁移；其边界依据沿用[通知 Hub 批次](./unified-logging-l2-notification-hub-2026-10-02.md)，不以本事件宣称连接全链无重复或框架日志安全已收口。

## 验证

- 定向 .NET 测试 **79 / 79** 通过，无跳过：ChatHubLoggingTests、既有 ChatHubIdentityTest、ChatChannelAccessServiceTest、ChatServiceTest、RuntimeLogPolicyTests 和 RuntimeLoggingAdapterTests。
- 旧 / 候选 × Development / Production；候选显式设置 mode，Development 同时开启 diagnostics。验证安静的正常路径、租户组名与调用顺序、输入中载荷、身份 / 参数 / 权限拒绝、各层失败保留原异常实例，以及清理之前不输出断开事件。
- 使用真实 Hub 方法与 mock SignalR / 业务依赖；另用真实 ChatPresenceService 验证双连接共享同用户，一方离开不使另一方下线，另一方断开后移除在线状态；测试 finally 精确清理自身连接。
- Node 日志契约 **27 / 27** 通过。
- API `--warnaserror` 构建通过，0 警告、0 错误；变更文件卫生、文档检查和 `git diff --check` 通过。记录索引 312 行仅触发篇幅建议，不阻塞本批检查。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~ChatHub|FullyQualifiedName~ChatChannelAccessServiceTest|FullyQualifiedName~ChatServiceTest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests'
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

未安装依赖、启动真实项目 / 数据库 / 容器、访问生产或切换生产日志开关。未执行真实 WebSocket、框架调度、浏览器或权限中间件验收；L2 尚未整体完成。

## 下一批

CommentRealtimePushService 的失败日志仍含原始异常、事件名和帖子 ID；下一批核对 CommentController 等直接消费者，保留各评论事件、无效输入短路、载荷、组名和 best-effort 行为，再治理该生成点。
