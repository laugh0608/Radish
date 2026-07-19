# 聊天室系统设计

> Radish 聊天室系统核心设计文档（数据模型 · SignalR · API）
>
> **版本**: v26.7.2
>
> **最后更新**: 2026.07.19
>
> **关联文档**：
> [聊天室 App 文档总览](./chat-app-index.md) ·
> [前端架构与组件设计](./chat-frontend.md) ·
> [正式 Web 一对一私聊与会话管理设计](./chat-direct-conversation-design.md) ·
> [聊天历史搜索与消息定位设计](./chat-message-search-design.md) ·
> [聊天消息置顶设计](./chat-message-pin-design.md) ·
> [聊天轻量阅读回执设计](./chat-message-read-receipt-design.md) ·
> [表情包与 Reaction 系统](./emoji-sticker-system.md) ·
> [文件上传设计](./file-upload-design.md)

---

## 概述

Radish 聊天室是正式 Web `/messages` 与 WebOS `/desktop` 共用的频道制消息系统：

- **频道（Channel）**：多个命名频道，按分类组织，用户自由进入公开频道
- **消息**：文本（轻量 Markdown + @mention + sticker）+ 图片，完整持久化
- **实时**：独立 `ChatHub`，与现有 `NotificationHub` 并行运行
- **Reaction**：复用表情包系统，`TargetType='ChatMessage'`（Phase 2）
- **私聊**：Phase 2 复用频道消息体系，专题边界见一对一私聊设计
- **搜索**：F4-C 在服务端按成员 ACL 检索当前可见会话，专题边界见聊天历史搜索设计

**关键约束**：
- 消息可撤回（软删除，显示"已撤回"占位），不可修改（简化冲突）
- 图片通过现有 `Attachment` 系统上传，消息中存 URL
- @mention 触发现有通知系统推送，无需新增通知类型字段
- 单频道每次加载 50 条消息，向上滚动分页加载历史

---

## 当前实现快照（2026-07-19）

- P0/P1 核心链路已落地：`Channel/GetList`、`ChannelMessage/GetHistory`、`Send`、`Recall`、`ChatHub`、`@mention`、引用回复、图片消息、在线成员列表、草稿恢复、重连补拉。
- `ChatHub` 频道组命名采用 `channel:{tenantId}:{channelId}`，用户组采用 `user:{userId}`。
- 未读同步采用服务端 `LastReadMessageId` 计算 + `ChannelUnreadChanged` 推送。
- 当前实现未单独落地 `ChannelCategory` 实体，先通过 `Channel.CategoryId` 预留分类位。
- 在线成员接口已可用；用户头像字段已接入用户档案来源。
- 发送接口当前支持透传 `clientRequestId`，用于前端乐观发送与失败重试的稳定关联。
- Controller 与 SignalR 的 `long / long?` 字段当前统一按字符串传输；前端仅保留乐观发送临时负数 ID，避免 Snowflake 精度丢失影响撤回、引用回复与历史分页。
- 聊天数据已切换至独立 `Chat` 连接（`Radish.Chat.db`），与主业务库/通知库分离。
- 聊天导航链路已收口到消息级定位：当前可通过统一导航载荷直接打开目标频道，并基于 `channelId + messageId` 精确定位到对应消息。
- 聊天提及通知、聊天窗口回跳与论坛/公开壳层的长整型参数传输当前已统一到同一套消息定位语义，避免不同入口对同一条消息出现不一致跳转。
- `radish.console` 的 `Moderation` 审核台已纳入 `ChatMessage` 真实回看能力；审核队列与治理动作日志可以直接回跳到目标消息，消息已撤回、删除或不可读时仍会保留失效降级展示与创建时快照。
- 2026-07-18 一对一私聊批次 A-D 已完成并关闭：继续复用 `Channel / ChannelMember / ChannelMessage`，`DirectConversation` 只承担参与者、请求和阻断状态；成员 ACL、幂等、附件访问、正式 Web 页面与成组验收均已落地。
- 2026-07-18 F4-C-A 已完成聊天历史搜索现状审计和专题设计：下一批建立派生 `SearchText`、SQLite / PostgreSQL migration、批量成员 ACL、快照 cursor 和 POST 搜索契约，不提前改页面。
- 2026-07-19 F4-F-A / B / C 已完成轻量阅读回执设计、服务端权威契约与正式 Web / WebOS 共用页面：复用 `LastReadMessageId` 原子单调游标，Public / Announcement 不对外展示，普通 Private 仅发送者查看人数与读者分页，Accepted Direct 展示对端已读边界；活跃阅读面通过 REST 提交精确可见游标，Hub 只广播无个人数据的失效提示，旧在线头像与 Hub 写命令口径均已退出。下一批进入 F4-F-D 定向回归与成组验收。

---

## 分阶段功能规划

| 功能 | 阶段 | 说明 |
|------|------|------|
| 频道分类 + 频道管理（Console） | Phase 1 | 管理员创建频道和分类 |
| 公开频道实时消息（文字 + sticker） | Phase 1 | ChatHub 核心 |
| 图片消息 | Phase 1 | 复用 Attachment 上传 |
| @mention 提及通知 | Phase 1 | 复用 NotificationHub 推送 |
| 消息撤回 | Phase 1 | 软删除，30 分钟内可撤回 |
| 消息引用（回复） | Phase 1 | 引用单条消息，非线程 |
| 在线成员列表 | Phase 1 | Hub 连接组维护 |
| 输入中状态（Typing indicator） | Phase 1 | 节流推送，3 秒超时 |
| Reaction 回应 | Phase 2 | 复用 Reaction 系统，`TargetType='ChatMessage'` |
| 私聊（私信） | Phase 2 | 复用频道消息模型，新增一对一会话元数据与成员 ACL |
| 消息搜索 | Phase 2 / F4-C | 关键字字面量包含、时间范围、当前 / 全部可见会话和消息上下文定位 |
| 语音消息 | Phase 3 | 录音上传，音频播放组件 |
| 频道权限细分 | Phase 3 | 角色 + 权限矩阵 |

---

## 数据模型

### ChannelCategory（频道分类）

```csharp
[SugarTable("ChannelCategory")]
public class ChannelCategory : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    [SugarColumn(Length = 100)]
    public string Name { get; set; }          // 如"综合"、"技术"

    public int Sort { get; set; } = 0;

    public long TenantId { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
```

### Channel（频道）

```csharp
[SugarTable("Channel")]
public class Channel : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    public long? CategoryId { get; set; }      // FK → ChannelCategory（可为空，表示未分类）

    [SugarColumn(Length = 100)]
    public string Name { get; set; }           // 显示名，如"闲聊"

    /// <summary>URL 标识符，仅 [a-z0-9-]，如 "general"、"tech-talk"</summary>
    [SugarColumn(Length = 100)]
    public string Slug { get; set; }

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(Length = 100, IsNullable = true)]
    public string? IconEmoji { get; set; }     // 频道图标，Unicode emoji，如 "💬"

    public ChannelType Type { get; set; } = ChannelType.Public;

    public bool IsEnabled { get; set; } = true;

    public int Sort { get; set; } = 0;

    /// <summary>最后一条消息 ID（冗余，用于快速获取未读状态）</summary>
    public long? LastMessageId { get; set; }

    /// <summary>最后一条消息时间（冗余，用于频道列表排序）</summary>
    public DateTime? LastMessageTime { get; set; }

    /// <summary>置顶集合 revision（F4-E，实际状态位于 ChatMessagePin）</summary>
    public long PinRevision { get; set; }

    public long TenantId { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

public enum ChannelType
{
    Public       = 1,  // 所有登录用户可见可发言
    Announcement = 2,  // 所有用户可见，仅管理员可发言
    Private      = 3,  // 仅有效成员可见；一对一语义由 DirectConversation 标识
}
```

### ChannelMessage（消息）

```csharp
[SugarTable("ChannelMessage")]
public class ChannelMessage : RootEntityTKey<long>, IDeleteFilter
{
    public long ChannelId { get; set; }

    public long UserId { get; set; }

    [SugarColumn(Length = 100)]
    public string UserName { get; set; }       // 冗余，避免联查

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? UserAvatarUrl { get; set; } // 冗余，消息气泡直接使用

    public MessageType Type { get; set; } = MessageType.Text;

    /// <summary>
    /// 消息内容（Type=Text/Image 时的文本部分）。
    /// 支持：纯文本、@mention（@[用户名](userId)）、sticker://语法。
    /// 不支持完整 Markdown（无标题/表格/任务列表）。
    /// 支持：**粗体**、_斜体_、`行内代码`、```代码块```。
    /// Type=Image 时为可选的图片说明文字。
    /// Type=System 时为系统提示文案（如"xx 加入了频道"）。
    /// </summary>
    [SugarColumn(Length = 4000, IsNullable = true)]
    public string? Content { get; set; }

    /// <summary>引用的消息 ID（回复功能）</summary>
    public long? ReplyToId { get; set; }

    /// <summary>图片附件 ID（Type=Image 时有值）</summary>
    public long? AttachmentId { get; set; }

    public DateTime CreateTime { get; set; }

    // IDeleteFilter（软删除 = 撤回）
    // DeletedAt 即为撤回时间，DeletedBy 即为撤回人
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

public enum MessageType
{
    Text   = 1,   // 纯文字（可含 @mention 和 sticker）
    Image  = 2,   // 图片（含可选文字说明）
    System = 3,   // 系统消息（加入/离开/频道创建等）
    // Voice = 4, // Phase 3
}
```

### ChannelMember（成员与未读追踪）

```csharp
/// <summary>
/// 记录用户进入过的频道以及已读位置。
/// 公开频道无需预先加入，首次发言时自动创建记录。
/// </summary>
[SugarTable("ChannelMember")]
public class ChannelMember : RootEntityTKey<long>, IDeleteFilter
{
    public long ChannelId { get; set; }
    public long UserId { get; set; }

    public MemberRole Role { get; set; } = MemberRole.Member;

    /// <summary>
    /// 已读的最后一条消息 ID。
    /// 未读数 = 该频道 MessageId > LastReadMessageId 且 IsDeleted=false 的数量。
    /// </summary>
    public long? LastReadMessageId { get; set; }

    public DateTime JoinedAt { get; set; }

    // IDeleteFilter（软删除 = 离开频道，可重新加入）
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

public enum MemberRole
{
    Member = 1,
    Moderator = 2,  // 可删除他人消息
    Owner = 3,      // 频道创建者（通常是管理员）
}
```

### ViewModel

**ChannelVo**（频道列表项，含未读数）：

```csharp
public class ChannelVo
{
    public long VoId { get; set; }
    public long? VoCategoryId { get; set; }
    public string VoName { get; set; }
    public string VoSlug { get; set; }
    public string? VoDescription { get; set; }
    public string? VoIconEmoji { get; set; }
    public ChannelType VoType { get; set; }
    public int VoSort { get; set; }
    public int VoUnreadCount { get; set; }     // Service 层按当前用户填充
    public bool VoHasMention { get; set; }     // 未读中是否含 @我（红点高亮）
    public ChannelMessageVo? VoLastMessage { get; set; } // 最后一条消息预览
    public bool VoCanPinMessages { get; set; } // F4-E：服务端权威治理能力
}
```

**ChannelMessageVo**（单条消息）：

```csharp
public class ChannelMessageVo
{
    public long VoId { get; set; }
    public long VoChannelId { get; set; }
    public long VoUserId { get; set; }
    public string VoUserName { get; set; }
    public string? VoUserAvatarUrl { get; set; }
    public MessageType VoType { get; set; }
    public string? VoContent { get; set; }
    public long? VoReplyToId { get; set; }
    public ChannelMessageVo? VoReplyTo { get; set; }  // 引用消息快照（仅含基本字段）
    public long? VoAttachmentId { get; set; }
    public string? VoImageUrl { get; set; }           // 运行时由 attachmentId 派生
    public string? VoImageThumbnailUrl { get; set; }  // 运行时由 attachmentId 派生
    public bool VoIsRecalled { get; set; }            // IsDeleted=true 时为 true，内容置空
    public DateTime VoCreateTime { get; set; }
}
```

**AutoMapper 策略**：
- `Channel → ChannelVo`：自动映射（Vo 前缀），`VoUnreadCount`/`VoHasMention`/`VoLastMessage`/`VoCanPinMessages` 由 Service 手动填充
- `ChannelMessage → ChannelMessageVo`：自动映射，`VoIsRecalled` 映射自 `IsDeleted`；`VoContent` 在 `IsDeleted=true` 时映射为 `null`（AutoMapper 条件映射）

---

## 消息内容格式

聊天消息**不支持完整 Markdown**，仅支持以下轻量语法（与论坛编辑器区分）：

| 语法 | 渲染效果 | 说明 |
|------|----------|------|
| `**文字**` | **粗体** | |
| `_文字_` | _斜体_ | |
| `` `代码` `` | `行内代码` | |
| ` ```\n代码块\n``` ` | 代码块（含高亮） | |
| `@[用户名](12345)` | @用户名（蓝色高亮，可点击） | 发送时由前端构造 |
| `![name](sticker://pack/code)` | inline sticker 图 | 复用 sticker:// 协议 |
| URL | 可点击链接（自动识别） | 不展开预览（Phase 2 考虑） |

**禁止**的 Markdown：标题（#）、表格、任务列表、HTML 标签、图片 `![]()` 语法（图片通过独立 `Type=Image` 消息发送）。

### @mention 格式说明

```
存储格式：@[小萝卜](10086) 今天天气不错
渲染格式：@小萝卜（蓝色，点击打开该用户 profile）
触发通知：发送时 Service 层解析所有 @mention，
          为被提及用户创建 Notification（复用现有通知系统）
```

### 撤回消息展示

```
消息撤回后（IsDeleted=true）：
- VoContent → null，VoImageUrl → null
- 前端展示灰色文字："[已撤回]"
- 撤回规则：自己的消息 30 分钟内可撤回；
            Moderator 可撤回他人消息（无时间限制）
```

---

## ChatHub 设计

### 路由与注册

```csharp
// Program.cs
app.MapHub<ChatHub>("/hub/chat");
```

### Hub 实现结构

```csharp
[Authorize(Policy = "Client")]
public class ChatHub : Hub
{
    // 连接时：加入 user:{userId} 组（支持多端）
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        // 通知该用户在线的其他连接（可选）
    }

    // 断开时：从组移除，触发 UserOffline 推送（如无其他连接）
    public override async Task OnDisconnectedAsync(Exception? exception)

    // 加入频道（进入频道视图时调用）
    public async Task JoinChannel(long channelId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"channel:{channelId}");
        // 仅校验访问并加入实时组，不创建/恢复成员，不修改已读或归档状态
    }

    // 离开频道（切换频道或关闭聊天室时调用）
    public async Task LeaveChannel(long channelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"channel:{channelId}");
    }

    // 输入中（前端节流后调用，每 2 秒最多一次）
    public async Task StartTyping(long channelId)
    {
        await Clients.OthersInGroup($"channel:{channelId}")
            .SendAsync("UserTyping", new { channelId, userId, userName });
    }

}
```

### 服务端 → 客户端推送事件

| 事件名 | 触发时机 | 数据 | 推送目标 |
|--------|----------|------|---------|
| `MessageReceived` | 新消息发送成功 | `ChannelMessageVo` | `channel:{channelId}` 组所有连接 |
| `MessageRecalled` | 消息被撤回 | `{ channelId, messageId }` | `channel:{channelId}` 组 |
| `UserTyping` | 用户输入中 | `{ channelId, userId, userName }` | `channel:{channelId}` 组其他连接 |
| `ChannelUnreadChanged` | 频道未读数变化 | `{ channelId, unreadCount, hasMention }` | `user:{userId}` 组（多端同步） |
| `ConversationStateChanged` | 请求、阻断或归档等会话状态变化 | `{ channelId }` | 相关参与者的 `user:{userId}` 组；客户端收到后重读权威会话摘要 |
| `MessagePinsChanged` | 置顶集合发生实际变化（F4-E） | 完整 `ChatMessagePinStateVo`，包含 `channelId + revision + items` | `channel:{tenantId}:{channelId}` 组 |
| `ReadReceiptsChanged` | 成员游标实际前进且频道允许回执（F4-F） | `{ channelId }` 失效提示，不含个人阅读数据 | 当前频道组；客户端重读权威摘要 |

### 客户端 → 服务端调用

| 方法名 | 参数 | 说明 |
|--------|------|------|
| `JoinChannel` | `channelId` | 进入频道视图 |
| `LeaveChannel` | `channelId` | 离开频道视图 |
| `StartTyping` | `channelId` | 输入中（前端节流） |

> 发送消息与个人已读游标推进均通过 **REST API**；Hub 只负责实时订阅与失效提示。
> 这样业务写入可以使用完整的请求验证、限流、持久化与权威响应。

### ChatHub 与 NotificationHub 并行

Shell.tsx 中同时管理两个 Hub 连接：

```typescript
// NotificationHub 仍按账号级单例管理；ChatHub 由各壳层取得连接所有权
void notificationHub.start();
void chatHub.acquire(chatHubOwner);

// 普通卸载只释放当前 ChatHub owner；真实登出才统一停止账号级连接
void chatHub.release(chatHubOwner);
void notificationHub.stop();
void chatHub.stop();
```

两个 Hub 独立连接，互不影响。WebOS `Shell` 与正式 Web `/messages` 可能同时需要 ChatHub，因此不得由某个组件直接 `stop()` 另一入口仍在使用的连接。ChatHub URL：`${getSignalrHubUrl()}/hub/chat`

---

## REST API 设计

### ChannelController

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| GET | `/api/v1/Channel/GetList` | 登录 | 获取所有可见频道列表（含未读数） |
| GET | `/api/v1/Channel/GetDetail/{id}` | 登录 | 获取频道详情 |
| GET | `/api/v1/Channel/GetOnlineMembers/{id}` | 登录 | 获取当前在线成员列表 |

### ChannelMessageController

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| GET | `/api/v1/ChannelMessage/GetHistory` | 登录 | 分页获取历史消息（`channelId + beforeMessageId + pageSize=50`） |
| POST | `/api/v1/ChannelMessage/Send` | 登录 | 发送消息（文字或图片），成功后 Hub 广播 `MessageReceived` |
| DELETE | `/api/v1/ChannelMessage/Recall/{id}` | 登录 | 撤回消息（软删除），Hub 广播 `MessageRecalled` |
| GET | `/api/v1/ChannelMessagePin/GetState?channelId=...` | `CanView` | 获取最多 20 条置顶与频道 revision（F4-E） |
| POST | `/api/v1/ChannelMessagePin/Set` | `CanPinMessages` | 按目标状态置顶 / 取消，实际变化后广播完整 `MessagePinsChanged`（F4-E） |
| PUT | `/api/v1/ChannelReadState/Advance` | `CanView` | 以精确消息 ID 原子单调推进个人阅读游标并返回权威未读（F4-F） |
| POST | `/api/v1/ChannelReadReceipt/GetSummaries` | 发送者受限 | 批量读取最多 20 条消息的隐私裁剪回执摘要（F4-F） |
| GET | `/api/v1/ChannelReadReceipt/GetReaders` | Private 消息发送者 | 以不透明 `cursor` 分页读取当前有效读者，每页最多 50 人（F4-F） |

### 发送消息请求体

```typescript
interface SendMessageRequest {
  channelId: number;
  type: 'Text' | 'Image';
  content?: string;        // 文字内容（Type=Text 必填；Type=Image 为可选说明）
  replyToId?: number;      // 引用消息 ID
  attachmentId?: number;   // Type=Image 时必填
}
```

当前口径说明：

- `ChannelMessage` 实体只持久化 `AttachmentId`，不再冗余保存 `ImageUrl` / `ImageThumbnailUrl`。
- 前端发送图片消息时只向服务端提交 `attachmentId`。
- `VoImageUrl` / `VoImageThumbnailUrl` 只是服务端基于 `attachmentId` 与当前资源解析规则返回的展示字段。

### GetHistory 分页策略

```
GET /api/v1/ChannelMessage/GetHistory?channelId=1&beforeMessageId=&pageSize=50

首次加载：不传 beforeMessageId，返回最新 50 条（倒序，前端反转展示）
加载更多：传当前最早消息的 messageId 作为 beforeMessageId
          返回 ID < beforeMessageId 的最新 50 条
```

---

## 在线状态设计

- **当前在线判定**：用户的某个 ChatHub 连接已执行 `JoinChannel` 且尚未离开 / 断开，视为在该频道在线。
- **当前存储**：`IChatPresenceService` 使用 API 进程内静态并发字典，按 `tenantId + channelId + userId` 维护连接计数；可以处理单进程内多标签，但没有 Redis、跨实例汇总或 SignalR Backplane。
- **在线列表**：`GetOnlineMembers` 读取当前进程 Presence 中的用户 ID，再从用户档案批量装配显示名与头像；前端每 15 秒轮询。
- **专题边界**：F4-F 阅读回执不依赖在线状态，也不在本专题把 Presence 重构为跨实例平台。

---

## 限流与安全约束

| 约束项 | 限制 | 说明 |
|--------|------|------|
| 消息发送频率 | 每用户每秒最多 2 条 | Service 层基于 Redis 滑动窗口 |
| 消息内容长度 | 最大 4000 字符 | 与数据库字段一致 |
| 图片消息频率 | 每用户每分钟最多 5 张 | 单独限流 |
| 撤回时间窗口 | 30 分钟（自己的消息） | Service 层校验 |
| SignalR 最大消息体 | 32 KB | `AddSignalR` 配置已有 |
| Announcement 频道发言 | 仅 Moderator/Owner | Service 层校验 |

---

## 数据库索引建议

| 表 | 索引 | 用途 |
|----|------|------|
| `Channel` | `(TenantId, Slug)` 唯一索引 | Slug 查找 |
| `ChannelMessage` | `(ChannelId, Id DESC)` | 历史消息分页（主查询） |
| `ChannelMessage` | `(ChannelId, IsDeleted, Id)` | 未读数计算 |
| `ChannelMember` | `(ChannelId, UserId)` 唯一索引 | 快速查找成员记录 |
| `ChannelMember` | `(UserId, IsDeleted)` | 加载用户所有频道 |

---

## Console 管理

新增路由：`src/pages/Channels/`

| 页面 | 说明 |
|------|------|
| `ChannelCategoryList.tsx` | 分类管理（名称、排序） |
| `ChannelList.tsx` | 频道列表（含分类、类型、启用/禁用、排序） |
| `ChannelForm.tsx` | 创建/编辑频道（名称、Slug、类型、分类、描述、图标 emoji） |

管理端专用 API：

| 方法 | 路由 | 说明 |
|------|------|------|
| POST | `/api/v1/Channel/Admin/CreateCategory` | 创建分类 |
| PUT | `/api/v1/Channel/Admin/UpdateCategory/{id}` | 编辑分类 |
| DELETE | `/api/v1/Channel/Admin/DeleteCategory/{id}` | 软删除分类 |
| POST | `/api/v1/Channel/Admin/Create` | 创建频道 |
| PUT | `/api/v1/Channel/Admin/Update/{id}` | 编辑频道 |
| DELETE | `/api/v1/Channel/Admin/Delete/{id}` | 软删除频道 |
| PUT | `/api/v1/Channel/Admin/BatchUpdateSort` | 批量更新排序 |
| GET | `/api/v1/Channel/Admin/GetMessageList` | 管理端消息列表（含软删除，用于审核） |
| DELETE | `/api/v1/Channel/Admin/RecallMessage/{id}` | 管理员撤回任意消息 |

---

## 未来规划

### F4-E：消息置顶；F4-F：轻量阅读回执

消息置顶：
- 旧草案所称 `Channel.PinnedMessageId`、`ChannelMessage.IsPinned/PinnedAt/PinnedBy` 预留字段在真实代码中不存在，不再作为实现依据。
- 权威方案见 [F4-E 聊天消息置顶](./chat-message-pin-design.md)：独立 `ChatMessagePin`、频道 `PinRevision`、最多 20 条、目标状态和完整 Hub 快照。
- Public / Announcement、普通 Private 与 Direct 使用不同的服务端 `CanPinMessages` 规则；消息撤回在同一事务中移除活跃置顶。

消息阅读回执：
- 权威方案见 [F4-F 聊天轻量阅读回执](./chat-message-read-receipt-design.md)。
- 继续复用 `ChannelMember.LastReadMessageId`，但必须由客户端提交实际展示到的精确消息 ID，并由 Repository 原子单调推进。
- Public / Announcement 不对外展示；普通 Private 仅发送者查看人数与读者分页；Accepted Direct 展示对端已读边界。
- REST 负责游标写入和回执读取，Hub 只广播无个人数据的失效提示；不扩展 `GetOnlineMembers`，不依赖在线头像。
- F4-F-B 已完成 migration、原子 Repository、Service / REST、稳定错误契约与无个人数据 Hub 失效提示；F4-F-C 已完成共用页面、Store、活跃阅读面与旧 Hub 写命令退役，运行态成组矩阵留给 F4-F-D。

### Phase 2：私聊

一对一私聊以 [正式 Web 一对一私聊与会话管理设计](./chat-direct-conversation-design.md) 为权威说明：

- 复用 `Channel / ChannelMember / ChannelMessage / ChatHub`，不新增 `PrivateMessage`；
- 新增 `DirectConversation` 保存规范化参与者对、请求状态和阻断状态；
- 先补全列表、历史、发送、已读、Hub 加组、举报和附件的服务端成员 ACL，再开放入口；
- 正式入口为公开个人页“发消息”和 `/messages`，WebOS 只复用同一组件保持兼容；
- 私聊批次 A-D 已完成并关闭；消息搜索、Reaction、置顶和逐条阅读回执继续保持独立专题边界。

### Phase 2：聊天历史搜索与消息定位

F4-C 以 [聊天历史搜索与消息定位设计](./chat-message-search-design.md) 为权威说明：

- 搜索当前会话或当前账号全部可见会话，服务端一次性装配访问快照，不接受客户端频道白名单作为授权依据；
- `ChannelMessage.Content` 继续保存原始消息，派生 `SearchText` 统一清理 mention 存储标记、控制字符和纯资源协议后用于检索；
- SQLite / PostgreSQL 使用参数化字面量包含语义，`%`、`_` 和 `\\` 不成为通配符；
- cursor 绑定作用域、条件指纹、快照最大消息 ID 和最后一条 `(CreateTime, Id)`，失效时显式返回 `Chat.SearchCursorInvalid`；
- 搜索结果只返回安全片段与结构化频道 / 消息 / 用户元数据，点击继续复用 `GetMessageWindow`；
- 正式 Web 提供 PC 右侧面板和 mobile 单列搜索视图，WebOS 只复用同一能力；Console 私聊浏览、模糊检索、OCR / 转写和外部搜索平台不在本专题。

### Phase 3：语音消息

- 新增 `MessageType.Voice`
- 前端录音：Web Audio API + `MediaRecorder`
- 音频上传复用 Attachment 系统（新增 `BusinessType.VoiceMessage`）
- 消息中存 `AudioUrl`、`DurationSeconds`
- 渲染：自定义音频播放条组件（进度条 + 时长 + 播放/暂停）

---

## 参考文件

| 文件 | 参考用途 |
|------|----------|
| `Radish.Api/Hubs/NotificationHub.cs` | ChatHub 实现模式 |
| `Radish.Api/Services/NotificationPushService.cs` | IChatPushService 设计参考 |
| `Radish.Api/Program.cs` | Hub 注册方式（`app.MapHub<ChatHub>("/hub/chat")`） |
| `Radish.Model/ViewModels/PostVo.cs` | Vo 设计规范参考 |
| `Radish.Model/ViewModels/AttachmentVo.cs` | 图片消息附件字段参考 |
| `Frontend/radish.client/src/services/notificationHub.ts` | chatHub.ts 实现模板 |
| `Frontend/radish.client/src/desktop/Shell.tsx` | Hub 连接生命周期管理 |
| `Docs/features/emoji-sticker-system.md` | Reaction Phase 2 集成 |
