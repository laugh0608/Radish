# 聊天室 App 架构设计

> Radish Chat App 前端架构基线（正式 Web 与 WebOS 复用层）
>
> **版本**: v26.7.2
>
> **最后更新**: 2026.07.19
>
> **关联文档**：
> [聊天室 App 文档总览](./chat-app-index.md) ·
> [聊天室系统设计](./chat-system.md) ·
> [聊天室系统 - 前端架构与组件设计](./chat-frontend.md) ·
> [聊天历史搜索与消息定位设计](./chat-message-search-design.md) ·
> [聊天消息 Reaction 设计](./chat-message-reaction-design.md) ·
> [聊天消息置顶设计](./chat-message-pin-design.md) ·
> [聊天轻量阅读回执设计](./chat-message-read-receipt-design.md)

---

## 概述

聊天室 App 采用“正式 Web 工作区 / WebOS 窗口复用 + 实时连接 + 本地缓存状态”模式。`/messages` 是正式产品入口，`/desktop` 复用同一 `ChatApp`、API 与 Hub，不建立平行协议。

设计目标：
- 与现有 `notificationHub` 并行运行，互不干扰。
- 页面与数据解耦；一对一私聊、搜索、Reaction、置顶与轻量阅读回执均通过独立专题完成权威契约和正式 Web 页面。
- F4-C 至 F4-F 已完成 A-D 批并成为维护基线；后续功能完成线不在 Chat 内继续堆叠临时状态，也不改动已经稳定的 Store、REST / Hub 权威边界和隐私契约。

---

## 应用注册

在 `AppRegistry.tsx` 中新增 `chat` 应用定义：

```typescript
{
  id: 'chat',
  name: '聊天室',
  icon: 'mdi:message-text',
  component: ChatApp,
  type: 'window',
  defaultSize: { width: 1100, height: 750 },
  requiredRoles: ['User'],
  category: 'content',
}
```

---

## 目录结构

```text
Frontend/radish.client/src/apps/chat/
├── ChatApp.tsx
├── ChatApp.module.css
├── ChatChannelSidebar.tsx
├── ChatConversationHeader.tsx
├── ChatMessageList.tsx
├── ChatMessageContent.tsx
├── ChatMessageSearchPanel.tsx
├── ChatPinnedMessages.tsx
├── ChatReadReceiptIndicator.tsx
├── ChatMemberPanel.tsx
├── ChatMentionMenu.tsx
├── ChatComposerStatus.tsx
├── ChatProtectedImage.tsx
├── chatApp.helpers.ts
├── chatApp.types.ts
├── chatConversationPresentation.ts
├── chatMessageSearch.ts
├── chatReadReceipts.ts
├── useActiveChatReadSurface.ts
├── useChatMessageNavigation.ts
├── useChatMessagePins.ts
├── useChatMessageReactions.ts
├── useChatMessageSearch.ts
├── useChatReadReceipts.ts
└── useChatConversationWorkspace.ts
Frontend/radish.client/src/messages/
├── MessagesApp.tsx
└── MessagesApp.module.css
Frontend/radish.client/src/api/
└── chat.ts
Frontend/radish.client/src/services/
└── chatHub.ts
Frontend/radish.client/src/stores/
└── chatStore.ts
Frontend/radish.client/src/types/
└── chat.ts
```

> 注：`ChatApp.tsx` 仍承担页面编排，但消息列表、频道侧栏、会话头、成员面板、置顶列表和会话动作已经按真实职责拆分。F4-C 搜索与 F4-E 置顶使用独立 Hook 和组件，不回退为新的单文件集中实现。

---

## 分层边界

1. 视图层 `apps/chat/ChatApp.tsx + Chat*.tsx`
- `ChatApp` 负责页面编排，分离组件承担频道、消息、会话头、成员和输入状态展示。

2. 应用层 `ChatApp.tsx`
- 负责频道切换、分页触发、输入上报、滚动已读与 Hub 生命周期联动。

3. 状态层 `stores/chatStore.ts`
- 维护频道列表、未读数、消息 Map、当前激活频道，以及本地发送状态替换逻辑。

4. 基础设施层 `api/* + services/chatHub.ts`
- REST 负责持久化操作。
- ChatHub 负责实时推送与连接管理。

---

## 状态模型

`chatStore` 最小字段：
- `channels: ChannelVo[]`
- `messageMap: Record<string, ChannelMessageVo[]>`
- `pinStateMap: Record<string, ChatMessagePinStateVo>`
- `activeChannelId: string | null`
- `connectionState: 'disconnected' | 'connecting' | 'connected' | 'reconnecting'`

内存管理现状：
- 当前未引入全局 LRU 裁剪，`messageMap` 由会话内状态自然增长。
- 后续若消息量持续增长，再引入“非活跃频道裁剪 + 全局上限”策略。

核心动作（当前实现）：
- `setChannels`
- `setActiveChannel`
- `setConnectionState`
- `setChannelMessages`
- `prependChannelMessages`
- `addMessage`（按 `messageId / clientRequestId` 合并，兼容替换 `status: 'sending'` 临时消息）
- `removeMessage`（撤销失败临时消息）
- `recallMessage`
- `setPinState` / `applyPinBroadcast`（按频道 revision 替换权威完整快照）
- `updateUnread`（对象载荷：`{ channelId, unreadCount, hasMention }`）
- `reset`

---

## 数据流

1. 首次进入 ChatApp
- 拉取频道列表（含未读）。
- 选择默认频道并拉取最新历史。
- 正式 Web `/messages` 或 WebOS `Shell` 使用稳定 owner 调用 `chatHub.acquire(owner)`；最后一个 owner 释放后才关闭共用连接。

2. 发送消息（当前流程）
- 前端先生成负数临时消息与 `clientRequestId`，立即插入本地列表。
- 前端调用 REST `/ChannelMessage/Send`，请求体携带 `clientRequestId`；图片消息只提交 `attachmentId`，本地预览用到的 `imageUrl / imageThumbnailUrl` 仅存在于前端临时状态，不再回写服务端。
- REST 成功返回 `ChannelMessageVo` 后，前端按 `clientRequestId` 将临时消息替换为真实消息。
- `ChannelMessageVo.voImageUrl / voImageThumbnailUrl` 为服务端基于 `attachmentId` 派生的展示字段，不是消息实体的持久化真值。
- 服务端同时通过 ChatHub 广播 `MessageReceived`，发送端和其他在线端统一按 `messageId / clientRequestId` 合并，避免重复气泡。
- 失败时保留原位消息并标记 `status: 'failed'`，支持重试 / 撤销；图片消息重试复用已上传附件信息。

3. 撤回消息
- 前端调用 REST `/ChannelMessage/Recall/{id}`。
- 服务端广播 `MessageRecalled`。
- `chatStore.recallMessage` 将目标消息标记为撤回展示。

---

## 与正式 Web / WebOS 集成点

- `/messages` 与 WebOS `Shell` 登录后分别取得 `chatHub` 所有权，普通卸载只释放自身 owner；登出时统一停止连接，新账号进入工作区后先以服务端列表替换账号相关 store 快照。
- `chatHub` 连接成功与重连成功后会自动尝试加入当前激活频道。
- 当前频道在重连恢复后会清空本地缓存并重新拉取最新 50 条，优先保证状态一致。
- `ConversationStateChanged` 只推动 `chatStore` 会话修订号变化，工作区随后重读服务端会话摘要，不用本地事件载荷推导请求、阻断或归档状态。
- Dock 未读气泡由 `chatStore.channels` 聚合计算，不额外轮询。
- 窗口最小化/恢复不重建连接，避免短时频繁重连。

---

## 扩展策略

- Phase 2：私聊分区已完成；F4-C 搜索采用独立搜索状态和服务端 cursor，不把跨频道命中写入 `messageMap`。
- 搜索结果点击统一进入 `useChatMessageNavigation`，继续调用现有 `GetMessageWindow(channelId, messageId)`；通知深链和搜索定位共用这一条导航链路。
- 搜索面板与成员面板在 PC 右栏互斥；移动端由 `MessagesApp` URL 状态切换单列搜索视图，浏览器返回可恢复会话。
- Reaction 与置顶已按独立专题落地；F4-F 阅读回执同样保持独立契约，已完成服务端与共用页面实现，运行态验收按 F4-F-D 单独执行。
- Phase 3：语音消息、频道权限细分、跨设备同步增强。
- 所有扩展保持“先补契约文档，再落代码”的流程。
