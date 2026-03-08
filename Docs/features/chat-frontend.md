# 聊天室系统 - 前端架构与组件设计

> Radish 聊天室前端实现设计文档
>
> **版本**: v26.3.1
>
> **最后更新**: 2026.03.03
>
> **关联文档**：
> [聊天室 App 文档总览](./chat-app-index.md) ·
> [系统总览与后端设计](./chat-system.md) ·
> [表情包 UI 规范](./emoji-sticker-ui-spec.md)

---

## 应用注册

> 实现状态说明（2026-03-03）：
> 当前 P0 已在 `ChatApp.tsx + chatStore.ts + chatHub.ts + api/chat.ts` 落地主链路。
> 本文中的 `components/*` 与 `hooks/*` 拆分方案为 P1 目标架构，不影响现有接口契约。

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
  // Dock 图标显示总未读气泡
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
│ │ [搜索频道]  │ ┌──────────────────────┐ │ 在线 (3)       │ │
│ │             │ │  消息历史             │ │ • 小萝卜       │ │
│ │ 综合         │ │  (虚拟滚动)           │ │ • 管理员       │ │
│ │  # 闲聊 ●3  │ │                      │ │               │ │
│ │  # 公告     │ │  [消息气泡...]        │ │ 离线 (12)      │ │
│ │             │ │                      │ │ • ...          │ │
│ │ 技术         │ └──────────────────────┘ │                │ │
│ │  # 前端      │ ┌──────────────────────┐ │                │ │
│ │  # 后端      │ │  [引用预览（可选）]    │ │                │ │
│ │             │ ├──────────────────────┤ │                │ │
│ │             │ │ 📎 [输入框] 😊 📷 发送│ │                │ │
│ └─────────────┴──────────────────────────┴────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

**三栏响应式**：窗口宽度 < 850px 时隐藏成员列表，< 650px 时频道侧边栏折叠为图标模式。

---

## 目录结构

```
Frontend/radish.client/src/apps/chat/
├── ChatApp.tsx                    # 应用入口，布局骨架
├── ChatApp.module.css
│
├── components/
│   ├── ChannelSidebar/
│   │   ├── ChannelSidebar.tsx     # 频道侧边栏（分类 + 频道列表）
│   │   ├── ChannelItem.tsx        # 单个频道条目（名称 + 未读气泡）
│   │   └── ChannelSidebar.module.css
│   │
│   ├── MessageArea/
│   │   ├── MessageArea.tsx        # 消息历史区（虚拟滚动容器）
│   │   ├── MessageGroup.tsx       # 同一用户连续消息分组
│   │   ├── MessageBubble.tsx      # 单条消息气泡
│   │   ├── MessageBubble.module.css
│   │   ├── RecalledMessage.tsx    # 撤回消息占位
│   │   ├── SystemMessage.tsx      # 系统消息（加入/离开等）
│   │   ├── ImageMessage.tsx       # 图片消息（缩略图 + 灯箱）
│   │   ├── ReplyPreview.tsx       # 引用消息预览条（输入框上方）
│   │   └── QuotedMessage.tsx      # 气泡内嵌的被引用消息片段
│   │
│   ├── MessageInput/
│   │   ├── MessageInput.tsx       # 消息输入区（输入框 + 工具栏）
│   │   ├── MentionDropdown.tsx    # @mention 用户搜索下拉
│   │   └── MessageInput.module.css
│   │
│   └── MemberList/
│       ├── MemberList.tsx         # 在线成员列表（右侧面板）
│       └── MemberList.module.css
│
├── hooks/
│   ├── useChatHub.ts              # ChatHub 连接封装（参照 notificationHub.ts）
│   ├── useChannelMessages.ts      # 消息列表状态（分页 + 实时更新）
│   ├── useTypingIndicator.ts      # 输入中状态管理
│   └── useMentionSearch.ts        # @mention 用户搜索
│
├── stores/
│   └── chatStore.ts               # 全局聊天状态（频道列表 + 未读数）
│
└── api/
    ├── channel.ts                 # getChannelList, getOnlineMembers
    └── channelMessage.ts          # getHistory, sendMessage, recallMessage
```

---

## 核心 Hook 设计

### useChatHub.ts

严格参照 `notificationHub.ts` 的单例 + Hook 模式：

```typescript
class ChatHubService {
  private connection: signalR.HubConnection | null = null;
  private startRequestId = 0;

  async start(): Promise<void> {
    const requestId = ++this.startRequestId;
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${getSignalrHubUrl()}/hub/chat`, {
        accessTokenFactory: () => tokenService.getValidAccessToken(),
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    this.connection.serverTimeoutInMilliseconds = 60_000;
    this.connection.keepAliveIntervalInMilliseconds = 15_000;

    // 注册推送事件
    this.connection.on('MessageReceived', (msg: ChannelMessageVo) => {
      useChatStore.getState().addMessage(msg);
    });
    this.connection.on('MessageRecalled', ({ channelId, messageId }) => {
      useChatStore.getState().recallMessage(channelId, messageId);
    });
    this.connection.on('UserTyping', (payload: { channelId: number; userId: number; userName: string }) => {
      // 通知 useTypingIndicator（统一对象载荷，便于后续扩展字段）
    });
    this.connection.on(
      'ChannelUnreadChanged',
      (payload: { channelId: number; unreadCount: number; hasMention: boolean }) => {
        useChatStore.getState().updateUnread(payload);
      }
    );

    if (requestId !== this.startRequestId) return; // StrictMode 防双重启动
    await this.connection.start();
  }

  async joinChannel(channelId: number) {
    await this.connection?.invoke('JoinChannel', channelId);
  }

  async leaveChannel(channelId: number) {
    await this.connection?.invoke('LeaveChannel', channelId);
  }

  async startTyping(channelId: number) {
    await this.connection?.invoke('StartTyping', channelId);
  }

  async markChannelAsRead(channelId: number) {
    await this.connection?.invoke('MarkChannelAsRead', channelId);
  }
}

export const chatHub = new ChatHubService();
export function useChatHub() { /* 返回连接状态 */ }
```

### useChannelMessages.ts

```typescript
function useChannelMessages(channelId: number) {
  const messages = useChatStore(s => s.getMessages(channelId));
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [hasMore, setHasMore] = useState(true);

  // 首次加载：拉最新 50 条
  useEffect(() => {
    loadInitial();
    chatHub.joinChannel(channelId);
    return () => { chatHub.leaveChannel(channelId); };
  }, [channelId]);

  // 进入频道且消息区到达底部后标记已读（避免提前清零）
  const markAsReadWhenAtBottom = useCallback(() => {
    chatHub.markChannelAsRead(channelId);
  }, [channelId]);

  // 加载更多历史
  async function loadMore() {
    if (isLoadingMore || !hasMore) return;
    const oldest = messages[0];
    const result = await getHistory(channelId, oldest?.id);
    if (result.length < 50) setHasMore(false);
    useChatStore.getState().prependMessages(channelId, result);
  }

  return { messages, isLoadingMore, hasMore, loadMore, markAsReadWhenAtBottom };
}
```

### useTypingIndicator.ts

```typescript
function useTypingIndicator(channelId: number) {
  const [typingUsers, setTypingUsers] = useState<string[]>([]);

  // 收到 UserTyping 事件后添加，3 秒后自动移除
  useEffect(() => {
    const unsub = chatHub.onUserTyping(payload => {
      if (payload.channelId !== channelId) return;
      setTypingUsers(prev => [...new Set([...prev, payload.userName])]);
      setTimeout(() => {
        setTypingUsers(prev => prev.filter(n => n !== payload.userName));
      }, 3000);
    });
    return unsub;
  }, [channelId]);

  // 本地输入时节流推送
  const sendTyping = useCallback(
    throttle(() => chatHub.startTyping(channelId), 2000),
    [channelId]
  );

  return { typingUsers, sendTyping };
}
```

---

## 状态管理（chatStore.ts）

使用与现有 `notificationStore.ts` 相同的模式（Zustand 或 Context，参照项目实际选择）：

```typescript
interface ChatState {
  // 频道列表（含未读数）
  channels: ChannelVo[];
  // 每个频道的消息列表，key 为 channelId
  messageMap: Record<number, ChannelMessageVo[]>;
  // 当前激活频道
  activeChannelId: number | null;

  // Actions
  setChannels: (channels: ChannelVo[]) => void;
  updateUnread: (channelId: number, count: number, hasMention: boolean) => void;
  addMessage: (msg: ChannelMessageVo) => void;
  prependMessages: (channelId: number, msgs: ChannelMessageVo[]) => void;
  recallMessage: (channelId: number, messageId: number) => void;
  setActiveChannel: (channelId: number) => void;
}
```

**总未读数**（Dock 图标气泡）：

```typescript
// Dock.tsx 中订阅
const totalUnread = useChatStore(s =>
  s.channels.reduce((sum, c) => sum + c.voUnreadCount, 0)
);
```

---

## 消息气泡（MessageBubble）

### 视觉规范

```
[头像 32×32]  小萝卜  14:23
              消息内容文本，支持轻量 Markdown
              [图片缩略图（如有）]
                                  [😀 3] [❤️ 1]  ← Reaction 气泡（Phase 2）
```

**连续消息分组**（MessageGroup）：
- 同一用户 5 分钟内的连续消息合并为一组
- 只有第一条显示头像和用户名，后续消息仅显示时间（hover 时显示）
- 组内消息间距 `2px`，组间距 `16px`

**消息操作菜单**（hover 右上角出现）：
- 所有人：😊（添加 Reaction，Phase 2）、↩ 引用回复
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
        │                │  点击打开全屏灯箱
        └────────────────┘
```

- 上传中：显示进度条覆盖在图片区域
- 加载失败：显示占位图 + "图片加载失败"
- 复用 `@radish/ui` `ImageLightbox` 组件

---

## 消息输入区（MessageInput）

### 布局

```
┌──────────────────────────────────────────────────┐
│ [ReplyPreview（引用时显示）]                       │
├──────────────────────────────────────────────────┤
│ 📎 图片   │  输入框（contenteditable div）  │ 😊  │
│           │                                │     │
│           │  @mention 下拉（浮层）          │ 发送 │
└──────────────────────────────────────────────────┘
输入中：小萝卜 正在输入...
```

### 输入框实现

使用 `contenteditable div`（而非 `<textarea>`），原因：
- 支持 @mention 渲染为蓝色标签（`<span>` 节点）
- 支持 sticker 图片嵌入预览（`<img>` 节点）
- 提交时序列化为存储格式字符串

**@mention 触发流程**：
1. 用户输入 `@` 字符
2. `useMentionSearch` 捕获后续输入，搜索用户列表（`GET /api/v1/User/Search?keyword=`）
3. `MentionDropdown` 显示搜索结果（头像 + 用户名），最多 8 条
4. 用户选择后，将 `@用户名` 渲染为蓝色 `<span data-user-id="xxx">`
5. 提交时序列化为 `@[用户名](userId)` 格式

**快捷键**：
- `Enter`：发送消息
- `Shift+Enter`：换行
- `Escape`：取消引用 / 关闭 @mention 下拉
- `↑`（输入框为空时）：快速编辑/引用上一条自己的消息（Phase 2）

### 图片上传

点击 📎 图片按钮：

```
1. 弹出文件选择框（accept="image/*"）
2. 选择后立即预览（ObjectURL）+ 显示上传进度
3. 调用 POST /api/v1/Attachment/UploadImage
4. 上传完成后自动发送 Type=Image 消息
5. 支持粘贴板图片（监听 paste 事件，clipboard.items 中过滤 image/* 类型）
```

---

## 虚拟滚动

消息历史区使用虚拟滚动，防止长时间在线积累大量 DOM：

- 使用 `react-virtual`（`useVirtualizer`）
- 消息高度可变（文字消息约 40–80px，图片消息约 200–350px）：使用动态测量模式（`measureElement`）
- 初始化时滚动到底部，新消息到达时：
  - 若用户当前在底部（距底 100px 内）：自动滚动到底部
  - 若用户正在查看历史：不自动滚动，显示"X 条新消息 ↓"提示条
- 向上滚动到顶部时触发 `loadMore()`（加载历史）

---

## Shell.tsx 集成

```typescript
// 在现有 notificationHub 连接管理代码旁添加：
useEffect(() => {
  if (isAuthenticated && !chatStartedRef.current) {
    chatStartedRef.current = true;
    void chatHub.start();
    // 启动后加载频道列表（含未读数）
    void loadChannelList();
  } else if (!isAuthenticated && chatStartedRef.current) {
    chatStartedRef.current = false;
    void chatHub.stop();
    useChatStore.getState().reset();
  }
}, [isAuthenticated]);
```

频道列表在 Shell 层预加载，这样 Dock 图标气泡和 ChatApp 内部都能直接读取已有数据。

---

## Dock 图标未读气泡

```typescript
// Dock.tsx 中
const chatUnread = useChatStore(s =>
  s.channels.reduce((sum, c) => sum + c.voUnreadCount, 0)
);
const chatHasMention = useChatStore(s =>
  s.channels.some(c => c.voHasMention)
);

// Dock 图标渲染：
// - 有未读且有 @mention：红色气泡（高优先级）
// - 有未读无 @mention：主题色气泡
// - 无未读：无气泡
```

---

## 频道侧边栏（ChannelSidebar）

```
┌─────────────────────────┐
│ [搜索频道...]            │  ← 过滤本地已加载的频道列表
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
- 分类折叠状态存储在 `localStorage`（刷新后保持）

---

## chatHub.ts 环境配置

`.env.development` 新增：

```bash
# ChatHub URL（默认与 SignalR Hub URL 相同基地址）
# 无需单独配置，chatHub.ts 直接复用 getSignalrHubUrl()
```

`env.ts` 无需新增配置项，`getSignalrHubUrl()` 直接复用。

---

## API 文件（chat/api/）

**`channel.ts`**：

```typescript
import { apiGet } from '@radish/ui';

export async function getChannelList(): Promise<ChannelVo[]> {
  const res = await apiGet<ChannelVo[]>('/api/v1/Channel/GetList', { withAuth: true });
  if (!res.ok || !res.data) throw new Error(res.message || '获取频道列表失败');
  return res.data;
}

export async function getOnlineMembers(channelId: number) {
  const res = await apiGet(`/api/v1/Channel/GetOnlineMembers/${channelId}`, { withAuth: true });
  if (!res.ok || !res.data) throw new Error(res.message);
  return res.data;
}
```

**`channelMessage.ts`**：

```typescript
import { apiGet, apiPost, apiDelete } from '@radish/ui';

export async function getHistory(channelId: number, beforeMessageId?: number) {
  const params = new URLSearchParams({ channelId: String(channelId), pageSize: '50' });
  if (beforeMessageId) params.set('beforeMessageId', String(beforeMessageId));
  const res = await apiGet<ChannelMessageVo[]>(
    `/api/v1/ChannelMessage/GetHistory?${params}`,
    { withAuth: true }
  );
  if (!res.ok || !res.data) throw new Error(res.message);
  return res.data;
}

export async function sendMessage(req: SendMessageRequest): Promise<ChannelMessageVo> {
  const res = await apiPost<ChannelMessageVo>('/api/v1/ChannelMessage/Send', req, { withAuth: true });
  if (!res.ok || !res.data) throw new Error(res.message || '发送失败');
  return res.data;
}

export async function recallMessage(messageId: number): Promise<void> {
  await apiDelete(`/api/v1/ChannelMessage/Recall/${messageId}`, { withAuth: true });
}
```

---

## 组件复用关系

| 复用来源 | 复用内容 | 用途 |
|----------|----------|------|
| `@radish/ui` `StickerPicker` | 直接引用 | 消息输入工具栏的表情按钮 |
| `@radish/ui` `ReactionBar` | 直接引用（Phase 2） | 消息气泡下方 Reaction |
| `@radish/ui` `MarkdownRenderer` | 消息内容渲染（轻量模式） | 新增 `mode='chat'` prop 禁用标题/表格等 |
| `@radish/ui` `ImageLightbox` | 直接引用 | 点击图片消息全屏展示 |
| `@radish/ui` `FileUpload` | 图片上传 | 消息输入区图片按钮 |
| `radish.client` `notificationHub.ts` | 代码模式参考 | `chatHub.ts` 实现模板 |

**MarkdownRenderer `mode='chat'` 新增 prop**：

禁用以下渲染：`h1-h6`、表格、任务列表、HTML 内联；
保留：粗体、斜体、行内代码、代码块、链接、sticker://。

---

## 分阶段实现路线图

| 阶段 | 后端 | 前端 |
|------|------|------|
| **Phase 1** 核心 | 4 个实体（ChannelCategory/Channel/ChannelMessage/ChannelMember）、3 个 Service、2 个 Controller、ChatHub、在线状态 Redis | ChatApp 全套（布局 + 侧边栏 + 消息历史 + 输入区）、chatHub.ts、chatStore.ts、Dock 气泡集成 |
| **Phase 2** 扩展 | PrivateConversation + PrivateMessage 实体、Reaction 接入（无需改表） | 私信 UI（复用消息组件）、ReactionBar 集成、消息搜索 |
| **Phase 3** 语音 | MessageType.Voice、音频 Attachment 处理 | 录音组件、音频播放条 |

---

## 参考文件

| 文件 | 参考用途 |
|------|----------|
| `Frontend/radish.client/src/services/notificationHub.ts` | chatHub.ts 完整实现模板 |
| `Frontend/radish.client/src/desktop/Shell.tsx` | Hub 启动/停止生命周期集成位置 |
| `Frontend/radish.client/src/desktop/AppRegistry.tsx` | chat app 注册入口 |
| `Frontend/radish.client/src/apps/forum/` | 前端 App 目录结构参考 |
| `Frontend/radish.ui/src/components/StickerPicker/` | Phase 1 消息输入表情按钮 |
| `Frontend/radish.ui/src/components/MarkdownRenderer/` | 新增 `mode='chat'` prop |
