# 聊天室 App 实时与同步设计

> Radish Chat App 的 ChatHub 事件流与同步策略
>
> **版本**: v26.3.0
>
> **最后更新**: 2026.03.02
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
3. 断线自动重连（指数退避）
4. 登出时调用 `chatHub.stop()` 并重置 `chatStore`

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

发送链路（P0 基本流程）：
1. 客户端调用 `POST /api/v1/ChannelMessage/Send`
2. 服务端落库成功，返回 `ChannelMessageVo`
3. 客户端收到 REST 响应后 `chatStore.addMessage` 插入消息
4. 服务端同时推送 `MessageReceived` 给频道全部在线端
5. 发送端收到回推时 `addMessage` 按 `messageId` 去重，自动忽略
6. **P1 乐观更新**：发送时先插入临时消息（`status: 'sending'`，使用负数临时 ID），REST 成功后以真实 `messageId` 替换；失败时标记 `status: 'failed'`

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
- 重连成功后重新 `JoinChannel`，并清空当前频道消息缓存，重新拉取最新 50 条历史防丢帧（避免实现复杂的增量 merge）。
- 若后续需要更精细的补全（如仅拉取断线期间缺失的消息），可在 API 层补充 `afterMessageId` 参数；P0/P1 阶段不做此优化。

2. REST 成功但 Hub 事件丢失
- P0 阶段：发送端以 REST 返回值为准直接插入消息，不依赖 Hub 回推自身确认。
- P1 阶段（乐观更新）：本地先渲染 `status: 'sending'` 临时消息。REST 成功后替换为真实消息。超时 5 秒未收到回推且 REST 也无响应时标记 `status: 'failed'`。

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
