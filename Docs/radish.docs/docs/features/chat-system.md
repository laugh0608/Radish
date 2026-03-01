# 聊天室系统设计

> Radish 聊天室系统核心设计文档（数据模型 · SignalR · API）
>
> **版本**: v26.2.0
>
> **最后更新**: 2026.02.24
>
> **关联文档**：
> [前端架构与组件设计](./chat-frontend.md) ·
> [表情包与 Reaction 系统](./emoji-sticker-system.md) ·
> [文件上传设计](./file-upload-design.md)

---

## 概述

Radish 聊天室是 WebOS 桌面上的独立 App，采用 Discord 风格的频道制设计：

- **频道（Channel）**：多个命名频道，按分类组织，用户自由进入公开频道
- **消息**：文本（轻量 Markdown + @mention + sticker）+ 图片，完整持久化
- **实时**：独立 `ChatHub`，与现有 `NotificationHub` 并行运行
- **Reaction**：复用表情包系统，`TargetType='ChatMessage'`（Phase 2）
- **私聊**：Phase 2 实现

**关键约束**：
- 消息可撤回（软删除，显示"已撤回"占位），不可修改（简化冲突）
- 图片通过现有 `Attachment` 系统上传，消息中存 URL
- @mention 触发现有通知系统推送，无需新增通知类型字段
- 单频道每次加载 50 条消息，向上滚动分页加载历史

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
| 私聊（私信） | Phase 2 | 独立数据模型，复用消息组件 |
| 消息搜索 | Phase 2 | 全文检索 |
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

    public long TenantId { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

public enum ChannelType
{
    Public       = 1,  // 所有登录用户可见可发言
    Announcement = 2,  // 所有用户可见，仅管理员可发言
    Private      = 3,  // 仅受邀用户可见（Phase 3）
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

    /// <summary>图片 URL（冗余，避免联查 Attachment 表）</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ImageUrl { get; set; }

    /// <summary>图片缩略图 URL</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ImageThumbnailUrl { get; set; }

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
    public string? VoImageUrl { get; set; }
    public string? VoImageThumbnailUrl { get; set; }
    public bool VoIsRecalled { get; set; }            // IsDeleted=true 时为 true，内容置空
    public DateTime VoCreateTime { get; set; }
}
```

**AutoMapper 策略**：
- `Channel → ChannelVo`：自动映射（Vo 前缀），`VoUnreadCount`/`VoHasMention`/`VoLastMessage` 由 Service 手动填充
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
        // 更新 ChannelMember.LastReadMessageId 为最新消息 ID
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
            .SendAsync("UserTyping", userId, userName);
    }

    // 标记已读（切换到该频道时调用）
    public async Task MarkChannelAsRead(long channelId)
    {
        // 更新 ChannelMember.LastReadMessageId = 当前最新消息 ID
        // 推送 UnreadCountChanged 给该用户所有端（多端同步）
        await Clients.Group($"user:{userId}")
            .SendAsync("ChannelUnreadChanged", channelId, 0);
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
| `MemberOnline` | 用户进入频道 | `{ channelId, userId, userName }` | `channel:{channelId}` 组 |
| `MemberOffline` | 用户离开频道 | `{ channelId, userId }` | `channel:{channelId}` 组 |

### 客户端 → 服务端调用

| 方法名 | 参数 | 说明 |
|--------|------|------|
| `JoinChannel` | `channelId` | 进入频道视图 |
| `LeaveChannel` | `channelId` | 离开频道视图 |
| `StartTyping` | `channelId` | 输入中（前端节流） |
| `MarkChannelAsRead` | `channelId` | 标记已读 |

> 发送消息通过 **REST API** 而非 Hub 方法，Hub 仅负责实时推送。
> 这样发送逻辑可以有完整的请求验证、限流和持久化处理。

### ChatHub 与 NotificationHub 并行

Shell.tsx 中同时管理两个 Hub 连接：

```typescript
// 登录后同时启动两个 Hub
void notificationHub.start();
void chatHub.start();

// 登出时同时停止
void notificationHub.stop();
void chatHub.stop();
```

两个 Hub 独立连接，互不影响。ChatHub URL：`${getSignalrHubUrl()}/hub/chat`

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

### 发送消息请求体

```typescript
interface SendMessageRequest {
  channelId: number;
  type: 'Text' | 'Image';
  content?: string;        // 文字内容（Type=Text 必填；Type=Image 为可选说明）
  replyToId?: number;      // 引用消息 ID
  attachmentId?: number;   // Type=Image 时必填
  imageUrl?: string;       // 上传后的图片 URL（与 attachmentId 二选一提交）
  imageThumbnailUrl?: string;
}
```

### GetHistory 分页策略

```
GET /api/v1/ChannelMessage/GetHistory?channelId=1&beforeMessageId=&pageSize=50

首次加载：不传 beforeMessageId，返回最新 50 条（倒序，前端反转展示）
加载更多：传当前最早消息的 messageId 作为 beforeMessageId
          返回 ID < beforeMessageId 的最新 50 条
```

---

## 在线状态设计

- **在线判定**：用户在 ChatHub 中有活跃连接 = 在线
- **在线列表**：ChatHub `OnConnectedAsync` / `OnDisconnectedAsync` 时，通过 `IChatPresenceService` 维护 Redis Set：`chat:online:{tenantId}`（存储 userId）
- **频道在线**：额外维护 `chat:channel:{channelId}:online`（进入/离开频道时更新）
- **GetOnlineMembers** 接口：从 Redis Set 读取，匹配 `ChannelMember` 返回用户信息

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

### Phase 2：私聊

新增实体：
- `PrivateConversation`：两个用户之间的会话（`UserAId`、`UserBId`、`LastMessageId`）
- `PrivateMessage`：私信内容（结构与 `ChannelMessage` 基本一致）

Hub 扩展：
- 私信通过 `user:{userId}` 组推送（已有基础）
- 无需新增 Hub 方法，发送仍走 REST，Hub 仅推送

前端：
- 聊天 App 左侧面板新增"私信"分区
- 复用消息气泡组件，仅布局和标题不同

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
| `Docs/radish.docs/docs/features/emoji-sticker-system.md` | Reaction Phase 2 集成 |
