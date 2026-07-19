# 聊天室系统 - 前端架构与组件设计

> Radish 聊天室前端实现设计文档
>
> **版本**: v26.7.2
>
> **最后更新**: 2026.07.19
>
> **关联文档**：
> [聊天室 App 文档总览](./chat-app-index.md) ·
> [系统总览与后端设计](./chat-system.md) ·
> [正式 Web 一对一私聊与会话管理设计](./chat-direct-conversation-design.md) ·
> [聊天历史搜索与消息定位设计](./chat-message-search-design.md) ·
> [聊天消息 Reaction 设计](./chat-message-reaction-design.md) ·
> [聊天消息置顶设计](./chat-message-pin-design.md) ·
> [聊天轻量阅读回执设计](./chat-message-read-receipt-design.md) ·
> [表情包 UI 规范](./emoji-sticker-ui-spec.md)

---

## 应用注册

> 实现状态说明（2026-03-11）：
> 当前聊天室已在 `ChatApp.tsx + chatStore.ts + chatHub.ts + api/chat.ts` 落地到 P1 核心交互，包括 `@mention`、引用回复、图片消息、草稿恢复、成员面板、重连补拉与状态条、乐观发送 + 失败重试。
> 本文目录与职责以当前 `apps/chat`、`services/chatHub.ts`、`stores/chatStore.ts` 和 `api/chat.ts` 为准；不再保留尚未落地的嵌套组件目录作为当前结构。
> `2026-07-08` 起，普通浏览器 `/messages` 收敛为正式 Web “聊天”工作区。它复用 `ChatApp`、聊天 API 与 `ChatHub`，支持 `channelId/messageId` 定位、公开个人页返回“聊天”、会话分区和移动端输入区适配；WebOS `/desktop?app=chat&channelId=...&messageId=...` 仍作为历史工作台深链保留。
> `2026-07-19` 一对一私聊与 F4-C 搜索、F4-D Reaction、F4-E 置顶、F4-F 轻量阅读回执均已完成 A-D 批并关闭。正式 `/messages` 与 WebOS 共用同一 `ChatApp`、API、Store 和 Hub 生命周期；搜索结果保持独立分页状态，Reaction / 置顶使用带 revision 的权威状态，回执使用服务端单调游标与受限摘要，不建立页面级重复真相源。

在 `AppRegistry.tsx` 中新增：

```typescript
{
  id: 'chat',
  name: '聊天室',
  icon: 'mdi:message-text',
  component: lazy(() => import('../apps/chat/ChatApp')),
  type: 'window',
  defaultSize: { width: 1100, height: 750 },
  requiredRoles: ['User'],
  category: 'content',
}
```

---

## 整体布局

```
┌─────────────────────────────────────────────────────────────┐
│  ChatApp 1100×750（可调整）                                   │
│ ┌─────────────┬──────────────────────────┬────────────────┐ │
│ │ 频道侧边栏   │  消息主区域              │ 成员列表（可收起）│ │
│ │  240px      │  flex-grow               │  200px         │ │
│ │             │                          │                │ │
│ │ 会话分区     │ ┌──────────────────────┐ │ 在线 (3)       │ │
│ │             │ │  消息历史             │ │ • 小萝卜       │ │
│ │ 综合         │ │  (历史分页)           │ │ • 管理员       │ │
│ │  # 闲聊 ●3  │ │                      │ │               │ │
│ │  # 公告     │ │  [消息气泡...]        │ │ 离线 (12)      │ │
│ │             │ │                      │ │ • ...          │ │
│ │ 技术         │ └──────────────────────┘ │                │ │
│ │  # 前端      │ ┌──────────────────────┐ │                │ │
│ │  # 后端      │ │  [引用预览（可选）]    │ │                │ │
│ │             │ ├──────────────────────┤ │                │ │
│ │             │ │ 📎 [输入框]      发送│ │                │ │
│ └─────────────┴──────────────────────────┴────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

**三栏响应式**：Chat 工作区不高于 `860px` 时隐藏成员列表，不高于 `680px` 时收紧频道侧栏和输入区；正式 `/messages` 页面不高于 `720px` 时切换为列表 / 详情单列视图。

当前实现补充：
- 右侧成员面板已支持展开 / 收起与在线成员头像展示。
- 输入区已支持引用预览、`@mention` 搜索、图片上传进度、草稿恢复提示。
- 服务端 `long / long?` 主键在 REST 与 SignalR 中按字符串传输，前端当前保留服务端字符串 ID，仅对乐观发送临时消息使用负数本地 ID，避免 Snowflake 精度丢失导致撤回 / 引用失败。
- 输入区已修正为“左侧输入区 + 右侧固定按钮列”布局，避免 `textarea` 挤压“图片 / 发送”按钮。
- 已发送消息支持 `sending / failed / sent` 状态展示，失败后可直接重试、撤销或复制诊断上下文。
- 连接提示条会区分连接中、恢复中和离线状态；草稿和本地失败消息保留在当前页面，连接恢复后可继续重试。
- `/messages` 是正式 Web 主导航中的“聊天”入口，不改变聊天协议；它负责登录恢复、URL 参数规范化、启动 / 停止 `ChatHub`、把通知消息定位交给 `ChatApp`，以及把成员点击转为 `/u/:id` 公开个人页来源返回。
- 会话列表按 `互相关注 / 陌生人私信 / 好友群组 / 公共频道` 分区展示。后端明确返回 `voConversationKind` 时按该字段归类；字段缺失时，`Public` / `Announcement` 归入公共频道，`Private` 归入陌生人私信，不通过频道名称或描述猜测关系。
- 移动端输入区使用短占位文案，不展示 `Enter / Shift+Enter / Esc` 这类桌面快捷键提示；桌面端仍保留快捷键说明。

---

## 目录结构

```
Frontend/radish.client/src/apps/chat/
├── ChatApp.tsx                    # 应用入口与页面编排
├── ChatApp.module.css
├── ChatChannelSidebar.tsx         # 会话分区、频道列表与搜索入口
├── ChatConversationHeader.tsx     # 会话状态与动作
├── ChatMessageList.tsx            # 消息、回应、回执与动作
├── ChatMessageContent.tsx         # 正文、mention 与附件展示
├── ChatMessageSearchPanel.tsx     # F4-C 搜索工作区
├── ChatPinnedMessages.tsx         # F4-E 置顶条与完整列表
├── ChatReadReceiptIndicator.tsx   # F4-F Direct / Private 回执
├── ChatMemberPanel.tsx            # 在线成员面板
├── ChatMentionMenu.tsx            # @mention 候选
├── useChatConversationWorkspace.ts
├── useChatMessageNavigation.ts
├── useChatMessageSearch.ts
├── useChatMessageReactions.ts
├── useChatMessagePins.ts
├── useChatReadReceipts.ts
└── useActiveChatReadSurface.ts

Frontend/radish.client/src/api/chat.ts
Frontend/radish.client/src/services/chatHub.ts
Frontend/radish.client/src/stores/chatStore.ts
```

---

## 核心 Hook 设计

### useChatHub.ts

当前 `chatHub.ts` 是账号级连接服务，不再只是 `notificationHub.ts` 的复制模板：

- `/messages` 与 WebOS `Shell` 分别以稳定 owner 调用 `acquire / release`；最后一个 owner 释放后才停止连接。
- `startRequestId` 与 `isConnectionDesired` 协调 StrictMode、路由卸载和启动 / 停止竞态。
- `MessageReceived / MessageRecalled / UserTyping / ChannelUnreadChanged / ConversationStateChanged` 写入对应 Store 状态。
- `MessageReactionsChanged / MessagePinsChanged` 携带完整权威状态并按 revision 合并。
- `ReadReceiptsChanged` 只增加频道失效 revision，由页面去抖重读 HTTP 摘要。
- 业务 ID 全部使用 `EntityIdValue` 并先规范化；不得把服务端 Snowflake ID 转成 JavaScript `number`。

个人已读不再由 `ChatHub` 写入。正式 Web 与 WebOS 共用 `useActiveChatReadSurface`，向 `PUT /api/v1/ChannelReadState/Advance` 提交实际展示到的 `readThroughMessageId`；Hub 不携带个人阅读数据。

### useChannelMessages.ts

当前消息与会话编排集中在 `useChatConversationWorkspace`：初始历史、向前 / 向后分页、当前频道订阅、乐观消息、草稿、会话状态和重连恢复在同一边界协调。跨分页目标定位由 `useChatMessageNavigation` 复用 `GetMessageWindow`，搜索结果不写入普通 `messageMap`。已读推进独立交给活跃阅读面 Hook，不能在“历史加载成功”或“加入频道”时顺带清零。

### useTypingIndicator.ts

输入状态由 `chatHub.startTyping` 按 2 秒节流上报，`chatStore.typingMap` 按频道保存，收到事件后 3 秒自动移除。它只表示当前连接的输入活动，不参与在线成员、未读或阅读回执推断。

---

## 状态管理（chatStore.ts）

当前使用 Zustand `chatStore.ts`。所有映射键先通过 LongId 规范化为字符串，主要状态包括：

- `channels / messageMap / activeChannelId`：频道、历史消息和当前会话；
- `reactionStateMap`：按消息保存带 revision 的回应完整状态；
- `pinStateMap`：按频道保存带 revision 的置顶完整快照；
- `readReceiptSummaryMap / readReceiptInvalidationMap`：HTTP 权威摘要与 Hub 失效修订；
- `conversationStateRevision / typingMap / connectionState`：会话重读提示、输入状态和连接状态。

未读只使用服务端频道 Vo 和 `ChannelUnreadChanged` 权威结果，不由 Reaction、置顶或回执组件自行增减。WebOS Dock 当前角标归通知收件箱，不把聊天未读复制成另一份 Dock 真相源。登出或账号变化必须调用 `reset` 清除上述状态。

---

## 消息气泡（MessageBubble）

### 视觉规范

```
[头像 32×32]  小萝卜  14:23  ↩ 引用  撤回
              消息内容文本，支持可点击 @mention
              [图片缩略图（如有）]
                                  [😀 3] [❤️ 1]  ← Reaction 气泡（F4-D）

自己消息采用镜像布局：

             回复  撤回  14:23  小萝卜  [头像 32×32]
             消息内容文本，支持可点击 @mention
```

**连续消息分组**（MessageGroup）：
- 同一用户 5 分钟内的连续消息合并为一组
- 只有第一条显示头像和用户名，后续消息仅显示时间（hover 时显示）
- 组内消息间距 `2px`，组间距 `16px`

**当前已落地的基础布局**：
- 头像固定在消息块侧边，不与消息文本挤在同一行
- 用户名 / 时间 / 回复 / 撤回 / 举报位于消息气泡上方
- 当前用户消息与他人消息左右镜像，保证信息层次一致

**消息操作菜单**（hover 右上角出现）：
- 具备 `CanReact`：😊（添加 Reaction）；所有可发送上下文：↩ 引用回复
- 自己的消息（30 分钟内）：🗑 撤回
- Moderator/Owner：🗑 撤回他人消息

### 引用消息（QuotedMessage）

在消息气泡上方展示被引用消息的缩略预览：

```
┌─ 引用 小萝卜 ──────────────────┐
│ 今天天气不错哦                  │   ← 截断 50 字符
└────────────────────────────────┘
正式回复内容...
```

左侧 `3px` 主题色竖线，背景略深于气泡背景。

### 图片消息（ImageMessage）

```
[头像]  小萝卜  14:25
        可选说明文字（如有）
        ┌────────────────┐
        │   图片缩略图   │  最大 300×300，保持比例
        │                │  认证读取私有附件
        └────────────────┘
```

- 上传中：显示进度条覆盖在图片区域
- 加载失败：显示占位图 + "图片加载失败"
- 已发送图片由 `ChatProtectedImage` 使用认证附件请求取得可回收 object URL；当前不宣称已接入灯箱

---

## 消息输入区（MessageInput）

### 布局

```
┌──────────────────────────────────────────────────┐
│ [ReplyPreview（引用时显示）]                       │
├──────────────────────────────────────────────────┤
│ 📎 图片   │  输入框（textarea）                   │
│           │                                │     │
│           │  @mention 下拉（浮层）          │ 发送 │
└──────────────────────────────────────────────────┘
输入中：小萝卜 正在输入...
```

### 输入框实现

当前实现使用 `<textarea>`，原因：
- 保持移动端键盘、换行、光标和输入法行为稳定；
- `@mention` 使用浮层搜索和文本插入，不在输入框内渲染富文本节点；
- 图片先进入待发送附件区，发送时与文本一起提交，不嵌入输入框正文。

**@mention 触发流程**：
1. 用户输入 `@` 字符
2. `useMentionSearch` 捕获后续输入，搜索用户列表（`GET /api/v1/User/Search?keyword=`）
3. `MentionDropdown` 显示搜索结果（头像 + 用户名），最多 8 条
4. 用户选择后，在 textarea 中插入 `@[显示名](用户Id)` 存储标记
5. 消息展示时由 `ChatMessageContent` 渲染为可访问的 `@显示名` 用户入口；搜索派生文本只保留可见名称，不索引内部标记

**桌面快捷键**：
- `Enter`：发送消息
- `Shift+Enter`：换行
- `Escape`：取消引用 / 关闭 @mention 下拉

移动端不把这些快捷键写进占位提示，避免把桌面键盘操作暴露给手机用户。

### 图片上传

点击 📎 图片按钮：

```
1. 弹出文件选择框（accept="image/*"）
2. 选择后立即预览（ObjectURL）+ 显示上传进度
3. 调用 POST /api/v1/Attachment/UploadImage
4. 上传完成后进入当前频道待发送附件区，不自动发送
5. 支持粘贴板图片（监听 paste 事件，clipboard.items 中过滤 image/* 类型）
6. 用户点击发送时，将当前文字与待发送附件统一提交；切换频道后结果回写原频道草稿
```

### 失败恢复与诊断

消息发送失败时，前端继续保留本地失败消息，不自动从列表移除。用户可执行：

- **重试**：复用当前消息内容和附件上下文再次发送。
- **撤销**：仅移除本地失败消息，不影响服务端已成功落库的消息。
- **复制诊断**：复制频道 ID、消息 ID 或本地请求 ID、发送状态、消息类型、创建时间、错误摘要、是否有正文 / 附件和当前路径。

诊断内容用于支持人员定位发送失败，不包含 access token、refresh token、聊天频道成员列表、完整附件内容或后端堆栈。连接断开时，页面只提示实时同步暂不可用；历史消息仍可阅读，输入草稿和失败消息继续留在当前会话。

---

## 消息历史分页与滚动

当前消息历史区使用服务端分页和普通 DOM 渲染，尚未接入虚拟列表：

- 每次通过 `GetHistory` 加载一页，向上到达边界时继续请求更早消息；
- 初始化时滚动到底部，新消息到达时：
  - 若用户当前在底部（距底 100px 内）：自动滚动到底部
  - 若用户正在查看历史：不自动滚动，显示"X 条新消息 ↓"提示条
- 定位消息复用 `GetMessageWindow`，替换当前窗口后高亮目标，不尝试按未加载全量消息计算虚拟索引；
- F4-C 搜索结果使用独立 cursor 列表，只有点击结果时才载入目标消息窗口。

若成组回归证明长会话普通 DOM 已产生持续性能问题，再单独设计虚拟列表、动态高度和焦点恢复；当前不把未落地方案记作实现事实。

---

## Shell.tsx 集成

```typescript
const chatHubOwnerRef = useRef(Symbol('webos-shell'));

useEffect(() => {
  const owner = chatHubOwnerRef.current;
  if (!isAuthenticated) {
    void chatHub.release(owner);
    return;
  }

  void chatHub.acquire(owner);
  return () => {
    void chatHub.release(owner);
  };
}, [isAuthenticated]);
```

WebOS `Shell` 与正式 Web `/messages` 必须使用各自稳定的 owner。任一壳层卸载只释放自己的所有权，最后一个 owner 离开时才关闭账号级连接；真实登出再由统一认证流程执行 `chatHub.stop()`。Hub 停止不等于业务 store 已清空，账号边界上的页面必须先读取服务端频道 / 会话列表再展示摘要，不能沿用上一账号的未读或会话状态。

---

## 共享未读与提及摘要

```typescript
const messageUnreadTotal = channels.reduce(
  (total, channel) => total + Math.max(0, channel.voUnreadCount),
  0,
);
const mentionChannelCount = channels.filter(channel => channel.voHasMention).length;
```

频道侧栏展示每个频道的服务端未读与提及状态，Workbench 从同一 `channels` 快照聚合总未读、提及频道数和首个可继续处理的会话。当前 WebOS Dock 角标用于通知收件箱，不再声明聊天专属气泡；若未来新增聊天全局角标，也必须继续读取服务端频道摘要，不能建立本地加减计数。

---

## 频道侧边栏（ChannelSidebar）

```
┌─────────────────────────┐
│ [搜索消息]               │  ← 打开 F4-C 服务端权威搜索
├─────────────────────────┤
│ ▼ 综合                  │  ← 分类折叠（可点击折叠/展开）
│   # 闲聊           ●3   │  ← 3 条未读，圆形数字气泡
│   # 公告            @   │  ← 含 @mention，红色标记
│ ▼ 技术                  │
│   # 前端                │
│   # 后端                │
└─────────────────────────┘
```

- 未读数 > 99 显示 "99+"
- 当前激活频道：左侧 `3px` 主题色竖线 + 背景高亮
- 频道类型图标：`Public = #`，`Announcement = 📢`
- 搜索关键字、结果和 cursor 不写入 `localStorage`，也不进入跨标签消息 Store。

---

## chatHub.ts 环境配置

`.env.development` 新增：

```bash
# ChatHub URL（默认与 SignalR Hub URL 相同基地址）
# 无需单独配置，chatHub.ts 直接复用 getSignalrHubUrl()
```

`env.ts` 无需新增配置项，`getSignalrHubUrl()` 直接复用。

---

## API 文件（`src/api/chat.ts`）

聊天前端统一从 `@radish/http` 导入 `apiGet / apiPost / apiPut / apiDelete`、`createApiResponseError` 和 Chat 契约类型，不从 `@radish/ui` 获取 HTTP 方法，也不新建 fetch / axios 封装。

| 前端方法族 | 服务端入口 | 状态边界 |
| --- | --- | --- |
| `getChannelList / getChannelDetail` | `Channel/*` | 只消费服务端可见频道、会话分区、未读和动作开关 |
| `getChannelHistory / getChannelMessageWindow` | `ChannelMessage/GetHistory / GetMessageWindow` | LongId 字符串分页和共用消息定位 |
| `searchChannelMessages` | `ChannelMessage/Search` | 搜索结果独立 cursor，不进入普通消息 Store |
| `getChatMessageReactionStates / setChatMessageReaction` | `ChannelMessageReaction/*` | 目标状态、operation ID 和消息 revision |
| `getChatMessagePinState / setChatMessagePin` | `ChannelMessagePin/*` | 频道 revision 与完整置顶快照 |
| `advanceChannelReadState` | `ChannelReadState/Advance` | 精确消息游标、原子单调推进和权威未读 |
| `getChatReadReceiptSummaries / getChatReadReceiptReaders` | `ChannelReadReceipt/*` | 发送者受限摘要和不透明读者 cursor |

所有 `channelId / messageId / attachmentId / revision` 使用 `EntityIdValue` 输入并经 `normalizeEntityId` 转为字符串。错误通过 `ApiResponseError` 保留 HTTP status、稳定 `Code / MessageKey` 与 `TraceId`；页面不得通过中英文展示文案决定权限、cursor 或并发分支。

---

## 组件复用关系

| 复用来源 | 复用内容 | 用途 |
|----------|----------|------|
| `@radish/ui` `ReactionBar` / `StickerPicker` | 直接引用展示层，由 Chat Hook 注入状态和动作 | 消息气泡下方回应及更多表情 |
| `@radish/ui` `BottomSheet` | 直接引用模态与焦点基线 | mobile 置顶列表与 Private 读者详情 |
| `ChatMessageContent` | Chat 内部组件 | 保持消息原文并把存储格式 mention 渲染为公开个人页入口 |
| `ChatProtectedImage` | Chat 内部组件 | 认证读取 Private / Direct 图片并回收 object URL |

---

## 分阶段实现路线图

| 阶段 | 后端 | 前端 |
|------|------|------|
| **Phase 1** 核心 | Channel / ChannelMessage / ChannelMember、Service / Controller、ChatHub、进程内 Presence | ChatApp 布局、侧边栏、消息历史、输入区、chatHub.ts 与 chatStore.ts |
| **Phase 2** 一对一私聊 | `DirectConversation` 元数据、频道成员 ACL、消息幂等、私聊附件访问 | 已完成公开个人页入口、请求处理、会话分区、归档与成组验收 |
| **Phase 2 / F4-C** 消息搜索（已完成） | `SearchText`、成员 ACL、跨库查询、快照 cursor | PC 右侧面板、mobile 单列视图、结果定位与恢复状态 |
| **Phase 2 / F4-D** Reaction（已完成） | Chat 专属状态、operation ledger、消息 revision | 共享 `ReactionBar`、Chat Hook、Store 与 Hub 合并 |
| **Phase 2 / F4-E** 置顶（已完成） | 独立置顶集合、频道 revision、20 条容量 | 置顶条、PC 面板、mobile Bottom Sheet 与消息定位 |
| **Phase 2 / F4-F** 阅读回执（已完成） | 单调游标、隐私裁剪摘要、读者 cursor | 活跃阅读面、Direct 边界、Private 读者详情与失效重读 |
| **Phase 3** 语音 | MessageType.Voice、音频 Attachment 处理 | 录音组件、音频播放条 |

---

## 参考文件

| 文件 | 参考用途 |
|------|----------|
| `Frontend/radish.client/src/services/chatHub.ts` | owner 生命周期、Hub 事件与重连 |
| `Frontend/radish.client/src/stores/chatStore.ts` | 频道、消息、回应、置顶、回执和 reset 状态 |
| `Frontend/radish.client/src/apps/chat/useChatConversationWorkspace.ts` | 会话与消息主编排 |
| `Frontend/radish.client/src/apps/chat/useChatMessageNavigation.ts` | 通知、搜索与置顶共用定位 |
| `Frontend/radish.client/src/apps/chat/useChatMessageReactions.ts` | 回应权威状态 |
| `Frontend/radish.client/src/apps/chat/useChatMessagePins.ts` | 置顶权威状态 |
| `Frontend/radish.client/src/apps/chat/useChatReadReceipts.ts` | 回执摘要、分页与失效重读 |
| `Frontend/radish.client/src/apps/chat/useActiveChatReadSurface.ts` | 活跃阅读面与精确游标提交 |
