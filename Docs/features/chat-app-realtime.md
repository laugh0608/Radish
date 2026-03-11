# 聊天室 App 实时与同步设计

> Radish Chat App 的 ChatHub 事件流与同步策略
>
> **版本**: v26.3.4
>
> **最后更新**: 2026.03.11
>
> **关联文档**：
> [聊天室 App 文档总览](./chat-app-index.md) ·
> [聊天室系统设计](./chat-system.md) ·
> [聊天室 App 架构设计](./chat-app-architecture.md)

---

## 目标

- 保证“发送即达、撤回同步、未读一致”的最小实时体验。
- 保证多端登录场景下未读状态一致。
- 将业务写入与实时推送解耦：REST 负责写入，Hub 负责广播。

---

## Hub 连接生命周期

1. 登录成功后启动 `chatHub.start()`
2. 连接成功后进入 `connected`
3. 连接成功后自动尝试加入当前激活频道
4. 断线自动重连（指数退避）
5. 重连成功后自动尝试重新加入当前激活频道
6. 若重连前存在当前激活频道，恢复后清空本地缓存并补拉最新 50 条消息
7. 登出时调用 `chatHub.stop()` 并重置 `chatStore`

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

客户端到服务端：

| 方法 | 参数 | 触发时机 |
|------|------|----------|
| `JoinChannel` | `channelId` | 进入频道 |
| `LeaveChannel` | `channelId` | 离开频道 |
| `StartTyping` | `channelId` | 输入节流上报 |
| `MarkChannelAsRead` | `channelId` | 打开频道并到达底部后 |

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

---

## 未读同步策略

- 频道未读以服务端 `LastReadMessageId` 为准。
- 进入频道并加载完成后，消息区到达底部时调用 `MarkChannelAsRead`（避免提前清零）。
- `ChannelUnreadChanged` 推送给 `user:{userId}` 组，实现多端同步。
- Dock 气泡使用 `channels` 聚合值，不单独缓存。

---

## 输入中状态

- 前端输入时每 2 秒最多上报一次 `StartTyping`。
- 收到 `UserTyping` 后展示 3 秒，超时自动移除。
- 仅当前频道显示输入中提示，不跨频道扩散。

---

## 异常与降级

1. Hub 断线
- 展示"重连中"状态条。
- 当前实现：重连成功后重新 `JoinChannel`，并清空当前频道缓存后补拉最新 50 条历史。
- 后续若需要更精细补全，可再扩展 `afterMessageId` 增量同步。

2. REST 成功但 Hub 事件丢失
- 当前实现：发送端以 REST 返回值完成临时消息替换，不依赖 Hub 回推自身确认。
- Hub 若同时到达，则依赖 `messageId / clientRequestId` 合并；若 Hub 丢失也不会影响发送端最终状态。

3. 历史分页失败
- 保留已加载消息，不清空 UI。
- 显示“加载失败，点击重试”条目。

---

## 可观测性

- 前端日志：
  - `log.debug('ChatHub', '连接成功')`
  - `log.warn('ChatHub', '重连中')`
  - `log.error('ChatHub', '事件处理失败', error)`
- 关键埋点：
  - 首次连接耗时
  - 发送到接收端到端时延
  - 重连成功率与平均重连时长
