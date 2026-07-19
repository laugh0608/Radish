# 聊天室 App 实时与同步设计

> Radish Chat App 的 ChatHub 事件流与同步策略
>
> **版本**: v26.7.2
>
> **最后更新**: 2026.07.19
>
> **关联文档**：
> [聊天室 App 文档总览](./chat-app-index.md) ·
> [聊天室系统设计](./chat-system.md) ·
> [聊天室 App 架构设计](./chat-app-architecture.md) ·
> [正式 Web 一对一私聊与会话管理设计](./chat-direct-conversation-design.md) ·
> [聊天消息 Reaction 设计](./chat-message-reaction-design.md) ·
> [聊天消息置顶设计](./chat-message-pin-design.md) ·
> [聊天轻量阅读回执设计](./chat-message-read-receipt-design.md)

---

## 目标

- 保证发送、撤回、会话状态和未读在正式 Web 与 WebOS 复用面及时同步。
- 保证多端登录场景下未读状态一致。
- 将业务写入与实时推送解耦：REST 负责写入，Hub 负责广播。
- 让多个壳层入口共享同一账号级连接，不因组件挂载顺序互相停止或重复启动。

---

## Hub 连接生命周期

1. WebOS `Shell` 与正式 Web `/messages` 各自以稳定 `owner` 调用 `chatHub.acquire(owner)`；同一 owner 重复取得不会创建第二条连接。
2. 只要仍有 owner 需要连接，`isConnectionDesired` 就保持为真；最后一个 owner 调用 `release(owner)` 后才停止连接。
3. 连接启动使用 `startRequestId` 区分请求世代。启动中发生释放、登出或新启动请求时，旧协商结果会被丢弃，避免 StrictMode effect 重放和路由切换形成 stop/start 竞态。
4. 连接成功后进入 `connected`，并自动尝试加入当前激活频道。
5. 断线按指数退避自动重连；重连成功后重新加入当前激活频道。
6. 若重连前存在当前激活频道，`ChatApp` 清理该频道的服务端消息缓存并重新加载最新历史或当前定位窗口，同时刷新在线成员；发送中 / 失败的本地消息会保留并合并。
7. 真实登出调用 `chatHub.stop()` 清空 owners、取消未完成启动并关闭连接；普通页面卸载只释放自己的 owner。Hub 服务不负责清除业务数据，新账号页面必须先重读服务端频道 / 会话列表，不能展示上一账号的 store 快照。

建议参数：

- `serverTimeoutInMilliseconds = 60000`
- `keepAliveIntervalInMilliseconds = 15000`
- 自动重连序列：`[0, 2000, 5000, 10000, 30000]`

---

## 事件契约

服务端到客户端：

| 事件 | 载荷 | 前端动作 |
|------|------|----------|
| `MessageReceived` | `ChannelMessageVo` | 追加消息、必要时滚动到底 |
| `MessageRecalled` | `{ channelId, messageId }` | 标记撤回占位 |
| `UserTyping` | `{ channelId, userId, userName }` | 更新输入中提示 |
| `ChannelUnreadChanged` | `{ channelId, unreadCount, hasMention }` | 更新频道未读与红点 |
| `ConversationStateChanged` | `{ channelId }` | 增加会话状态修订并重新读取服务端权威会话摘要 / 分区 |
| `MessageReactionsChanged` | 完整 `ChatMessageReactionStateVo` | 按 `messageId + revision` 合并消息回应；旧或相等 revision 不覆盖当前状态 |
| `MessagePinsChanged` | 完整 `ChatMessagePinStateVo` | 按频道 revision 合并完整置顶快照；重连后仍通过 HTTP 追平 |
| `ReadReceiptsChanged` | `{ channelId }` | 去抖重读当前自己所发消息的 HTTP 权威回执摘要，不从事件推断个人数据 |

客户端到服务端：

| 方法 | 参数 | 触发时机 |
|------|------|----------|
| `JoinChannel` | `channelId` | 进入频道 |
| `LeaveChannel` | `channelId` | 离开频道 |
| `StartTyping` | `channelId` | 输入节流上报 |

`JoinChannel` 与 `StartTyping` 都在服务端复用 `ChatChannelAccessService`。公开频道按租户与频道策略授权；私有频道只允许有效成员，管理员身份不自动穿透一对一私聊。F4-F-B 起 `JoinChannel` 只校验实时订阅权限，不再创建 / 恢复成员、清除归档或推进游标；前端隐藏入口不能替代 Hub / REST 自身的授权。

个人已读通过 REST `ChannelReadState/Advance(channelId, readThroughMessageId)` 提交精确消息游标；发送者受限摘要 / 读者分页同样通过 REST 读取。Hub 已不再提供 `MarkChannelAsRead` 写命令，只保留 `ReadReceiptsChanged` 无个人数据失效提示。

---

## REST 与 Hub 协同

发送链路（当前实现）：

1. 客户端生成负数临时消息，并为本次发送生成 `clientRequestId`
2. 客户端立即插入 `status: 'sending'` 的本地消息
3. 客户端调用 `POST /api/v1/ChannelMessage/Send`，请求体携带 `clientRequestId`
4. 服务端落库成功，返回带 `clientRequestId` 的 `ChannelMessageVo`
5. 服务端同时推送 `MessageReceived` 给频道全部在线端
6. 前端按 `messageId / clientRequestId` 合并消息，发送端自身不会因 REST + Hub 双通道出现重复
7. 失败时原位标记 `status: 'failed'`，允许重试 / 撤销

ID 约束（当前实现）：

- 服务端 `long / long?` 字段在 Controller 与 SignalR 中统一按字符串传输，避免 JS `number` 精度丢失。
- 前端仅将“乐观发送临时消息”保留为负数本地 ID；一旦拿到服务端返回，就按字符串主键参与比对、撤回和引用回复。
- `MessageRecalled`、历史分页 `beforeMessageId`、`replyToId` 均按服务端字符串 ID 透传，避免出现“消息不存在或无权撤回”“引用消息不存在”的假失败。

撤回链路：

1. 客户端调用 `DELETE /api/v1/ChannelMessage/Recall/{id}`
2. 服务端软删除并校验权限
3. 推送 `MessageRecalled`
4. 前端显示“已撤回”

会话状态链路：

1. 接受、拒绝、阻断、解除阻断或陌生人首条请求消息通过 REST 落库。
2. 服务端向双方账号组发送 `ConversationStateChanged`，事件只提示哪个会话可能发生变化，不携带可直接覆盖本地状态的完整快照。
3. `chatStore` 增加会话状态修订，`useChatConversationWorkspace` 重新调用服务端会话接口。
4. 会话分区、可发送状态、归档与对方可用性始终以服务端响应为准，不根据消息正文、频道名称或本地动作推导。

---

## 未读同步策略

- 频道未读以服务端 `LastReadMessageId` 为准。
- 正式 Web / WebOS 共用活跃阅读面判定；只有页面可见、有焦点、Chat 窗口位于前台、消息区到达尾部且没有更新页时，才把实际可见的最高持久消息 ID 提交给 REST。
- `ChannelUnreadChanged` 推送给 `user:{userId}` 组，实现多端同步。
- 会话侧栏和 Workbench 需要总未读 / 提及摘要时直接从服务端频道列表 `channels` 聚合，不另建可自行增减的计数缓存；WebOS Dock 当前角标保留给通知收件箱，不复用为聊天未读。
- 正式 Web 与 WebOS 共用 `chatStore`；Hub 事件按服务端 ID 合并，不能在某个入口本地清零另一个入口的权威未读。

F4-F 阅读回执以[权威专题](./chat-message-read-receipt-design.md)为准：

- 客户端提交真实展示到的精确消息 ID，服务端原子单调推进 `LastReadMessageId`。
- Public / Announcement 不对外展示回执；普通 Private 仅发送者读取自己消息的摘要和读者分页；Accepted Direct 展示对端已读边界。
- `ReadReceiptsChanged` 只作为频道级失效提示，不携带读者、头像、计数或游标快照；客户端去抖重读 HTTP 权威摘要。
- 回执不依赖在线状态，断线重连后通过服务端重读恢复。
- F4-F 已完成 A-D 批并关闭：服务端、`@radish/http`、Store、失效去抖、页面表现、精确 REST 游标和旧 Hub 写命令退役均已进入维护基线，运行态矩阵已覆盖多标签、隐藏态、真实断线重连与 WebOS 前台窗口。

---

## 输入中状态

- 前端输入时每 2 秒最多上报一次 `StartTyping`。
- 收到 `UserTyping` 后展示 3 秒，超时自动移除。
- 仅当前频道显示输入中提示，不跨频道扩散。

---

## 异常与降级

1. Hub 断线

- 展示"重连中"状态条。
- 重连成功后重新 `JoinChannel`，并清空当前频道的服务端消息缓存后补拉最新历史或深链定位窗口；本地待发送消息不被清除。
- 后续若需要更精细补全，可再扩展 `afterMessageId` 增量同步。

2. REST 成功但 Hub 事件丢失

- 当前实现：发送端以 REST 返回值完成临时消息替换，不依赖 Hub 回推自身确认。
- Hub 若同时到达，则依赖 `messageId / clientRequestId` 合并；若 Hub 丢失也不会影响发送端最终状态。

3. 历史分页失败

- 保留已加载消息，不清空 UI。
- 显示“加载失败，点击重试”条目。

4. 多入口生命周期交错

- `Shell` 与 `/messages` 只释放自身 owner；一方卸载时另一方仍可保持连接。
- 启动中收到停止请求不会把已取消的协商误报为业务错误，也不会在所有 owner 已释放后自动复活连接。

---

## 可观测性

- 前端日志：
  - `log.debug('ChatHub', '连接成功')`
  - `log.warn('ChatHub', '重连中')`
  - `log.error('ChatHub', '事件处理失败', error)`
- Hub 的 `access_token` 由 SignalR 传输层通过查询参数使用时，统一 Serilog 查询凭据脱敏器会在所有 sink 写入前替换为 `[REDACTED]`；前端日志也不得主动输出 token 或完整握手 URL。
- 关键埋点：
  - 首次连接耗时
  - 发送到接收端到端时延
  - 重连成功率与平均重连时长
