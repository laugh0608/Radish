# 聊天室 App 架构设计

> Radish Chat App 前端架构基线（WebOS 应用层）
>
> **版本**: v26.3.3
>
> **最后更新**: 2026.03.11
>
> **关联文档**：
> [聊天室 App 文档总览](./chat-app-index.md) ·
> [聊天室系统设计](./chat-system.md) ·
> [聊天室系统 - 前端架构与组件设计](./chat-frontend.md)

---

## 概述

聊天室 App 采用“窗口应用 + 实时连接 + 本地缓存状态”模式，在 WebOS 中提供低延迟、高并发消息交互体验。

设计目标：
- 与现有 `notificationHub` 并行运行，互不干扰。
- 页面与数据解耦，支持后续私聊、Reaction、搜索渐进扩展。
- 当前以 P1 体验收口为主，不做脱离主链路的大型重构。

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
Frontend/radish.client/src/api/
└── chat.ts
Frontend/radish.client/src/services/
└── chatHub.ts
Frontend/radish.client/src/stores/
└── chatStore.ts
Frontend/radish.client/src/types/
└── chat.ts
```

> 注：当前仍为“单入口集中编排”实现形态。`components/*` 与 `hooks/*` 拆分仍是后续可选重构，不再视为 P1 前置条件。

---

## 分层边界

1. 视图层 `apps/chat/ChatApp.tsx`（当前现状）
- 当前由单入口承载渲染与交互编排，已覆盖频道切换、引用回复、图片上传、成员面板、草稿恢复、重连状态条与乐观发送。

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
- `messageMap: Record<number, ChannelMessageVo[]>`
- `activeChannelId: number | null`
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
- `updateUnread`（对象载荷：`{ channelId, unreadCount, hasMention }`）
- `reset`

---

## 数据流

1. 首次进入 ChatApp
- 拉取频道列表（含未读）。
- 选择默认频道并拉取最新历史。
- 调用 `chatHub.start()` 建立连接。

2. 发送消息（当前流程）
- 前端先生成负数临时消息与 `clientRequestId`，立即插入本地列表。
- 前端调用 REST `/ChannelMessage/Send`，请求体携带 `clientRequestId`。
- REST 成功返回 `ChannelMessageVo` 后，前端按 `clientRequestId` 将临时消息替换为真实消息。
- 服务端同时通过 ChatHub 广播 `MessageReceived`，发送端和其他在线端统一按 `messageId / clientRequestId` 合并，避免重复气泡。
- 失败时保留原位消息并标记 `status: 'failed'`，支持重试 / 撤销；图片消息重试复用已上传附件信息。

3. 撤回消息
- 前端调用 REST `/ChannelMessage/Recall/{id}`。
- 服务端广播 `MessageRecalled`。
- `chatStore.recallMessage` 将目标消息标记为撤回展示。

---

## 与 WebOS 集成点

- Shell 登录后启动 `chatHub`，登出时统一停止并清空状态。
- `chatHub` 连接成功与重连成功后会自动尝试加入当前激活频道。
- 当前频道在重连恢复后会清空本地缓存并重新拉取最新 50 条，优先保证状态一致。
- Dock 未读气泡由 `chatStore.channels` 聚合计算，不额外轮询。
- 窗口最小化/恢复不重建连接，避免短时频繁重连。

---

## 扩展策略

- Phase 2：私聊分区、消息搜索、Reaction（复用现有系统）。
- Phase 3：语音消息、频道权限细分、跨设备同步增强。
- 所有扩展保持“先补契约文档，再落代码”的流程。
