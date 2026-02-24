# 表情包与 Reaction 系统设计

> Radish 表情包系统（表情选择器 + Reaction 回应）核心设计文档
>
> **版本**: v26.2.0
>
> **最后更新**: 2026.02.24
>
> **关联文档**：
> [Console 管理后台实现](./emoji-sticker-console.md) ·
> [UI 视觉与交互规范](./emoji-sticker-ui-spec.md) ·
> [论坛应用功能说明](./forum-features.md) ·
> [文件上传设计](./file-upload-design.md)

---

## 概述

Radish 表情包系统涵盖三层能力：

1. **表情选择器**：发帖/评论时内嵌 Unicode emoji 或官方表情包图片（含 GIF 动图）
2. **Reaction 回应**：对帖子、评论（未来：聊天消息）进行多种表情反应，Discord 风格叠加
3. **管理后台**：管理员在 Console 中维护表情包分组和图片，无用户上传流程

**关键决策**：

- 所有表情包均由管理员在 Console 后台维护，不提供用户自定义上传
- Reaction 支持叠加：同一用户可对同一内容添加多种不同表情（EmojiValue 不同即为独立记录）
- 无审核流程（省去 AuditStatus、StickerAuditList 等复杂度）
- GIF 动图：StickerPicker 展示静止缩略图（第一帧），插入正文后渲染完整 GIF，用于 Reaction 时展示缩略图

---

## 分阶段功能规划

| 模块 | 阶段 | 说明 |
|------|------|------|
| Emoji 选择器升级（MarkdownEditor + 评论框） | Phase 1 | 替换现有 96 个 emoji 简陋选择器，增加分类与搜索 |
| 官方表情包管理（Console CRUD + 批量上传） | Phase 1 | 管理员维护分组和贴图，支持 GIF |
| 帖子/评论内嵌表情包（`sticker://` 语法） | Phase 1 | 写作时插入，渲染时展示 inline 图/GIF |
| Reaction 回应系统（多种叠加，Discord 风格） | Phase 2 | 帖子/评论下方 Reaction 气泡条 |
| 聊天室 Reaction | Phase 3 | `TargetType='ChatMessage'`，不改数据库 |
| 表情商店 | Phase 3 | `GroupType='Premium'` + 萝卜币购买 |

---

## 数据模型设计

### StickerGroup（表情包分组）

```csharp
/// <summary>表情包分组</summary>
[SugarTable("StickerGroup")]
public class StickerGroup : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>唯一标识，仅 [a-z0-9_]，如 "radish_girl"、"tieba"</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Code { get; set; }

    /// <summary>显示名称</summary>
    [SugarColumn(Length = 100)]
    public string Name { get; set; }

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>分组封面图 URL（显示在 StickerPicker Tab 图标）</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? CoverImageUrl { get; set; }

    public StickerGroupType GroupType { get; set; } = StickerGroupType.Official;

    public bool IsEnabled { get; set; } = true;

    public int Sort { get; set; } = 0;

    // ITenantEntity
    public long TenantId { get; set; }

    // IDeleteFilter
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

public enum StickerGroupType
{
    Official = 1,   // 官方表情包
    Premium  = 2    // 付费表情包（Phase 3 扩展）
}
```

### Sticker（单个表情）

```csharp
/// <summary>单个表情贴图，支持静图（PNG/WebP）和 GIF 动图</summary>
[SugarTable("Sticker")]
public class Sticker : RootEntityTKey<long>, IDeleteFilter
{
    /// <summary>所属分组 FK → StickerGroup</summary>
    public long GroupId { get; set; }

    /// <summary>组内唯一标识，仅 [a-z0-9_]，如 "happy"、"sad_face"</summary>
    [SugarColumn(Length = 100)]
    public string Code { get; set; }

    /// <summary>显示名（鼠标悬浮 tooltip 文本）</summary>
    [SugarColumn(Length = 200)]
    public string Name { get; set; }

    /// <summary>原图 URL（GIF 时为完整动图）</summary>
    [SugarColumn(Length = 500)]
    public string ImageUrl { get; set; }

    /// <summary>
    /// 缩略图 URL（静图：压缩版；GIF：第一帧静止图）。
    /// 用于 StickerPicker 网格预览和 ReactionBar 气泡展示。
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>是否为 GIF 动图</summary>
    public bool IsAnimated { get; set; } = false;

    /// <summary>
    /// 是否允许内嵌正文（sticker:// 语法插入 Markdown）。
    /// false 时仅可用于 Reaction，不出现在 StickerPicker 的插入模式中。
    /// </summary>
    public bool AllowInline { get; set; } = true;

    /// <summary>关联附件 ID FK → Attachment（可空，批量上传时自动填充）</summary>
    public long? AttachmentId { get; set; }

    /// <summary>使用次数，插入正文 + Reaction 均累计</summary>
    public int UseCount { get; set; } = 0;

    public int Sort { get; set; } = 0;

    public bool IsEnabled { get; set; } = true;

    // IDeleteFilter
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
```

### Reaction（表情回应记录）

```csharp
/// <summary>
/// 表情回应记录，统一表 + TargetType 鉴别。
/// 未来扩展 ChatMessage 无需改表结构。
/// 同一用户可对同一目标添加多种不同表情（EmojiValue 不同即为独立行）。
/// 唯一索引：(UserId, TargetType, TargetId, EmojiValue) - 保障幂等性。
/// </summary>
[SugarTable("Reaction")]
public class Reaction : RootEntityTKey<long>, IDeleteFilter
{
    public long UserId { get; set; }

    /// <summary>冗余字段，避免联查</summary>
    [SugarColumn(Length = 100)]
    public string UserName { get; set; }

    /// <summary>"Post" / "Comment" / "ChatMessage"</summary>
    [SugarColumn(Length = 50)]
    public string TargetType { get; set; }

    public long TargetId { get; set; }

    /// <summary>"unicode" / "sticker"</summary>
    [SugarColumn(Length = 20)]
    public string EmojiType { get; set; }

    /// <summary>unicode: "😀" | sticker: "radish_girl/happy"</summary>
    [SugarColumn(Length = 200)]
    public string EmojiValue { get; set; }

    /// <summary>
    /// sticker 类型时存储缩略图 URL（GIF sticker 使用第一帧静止图）。
    /// 冗余存储，避免每次聚合查询时联表获取图片 URL。
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ThumbnailUrl { get; set; }

    public DateTime CreateTime { get; set; }

    // IDeleteFilter（软删除 = 取消 Reaction）
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
```

### ViewModel

**StickerGroupVo**（含分组内表情列表）：

```csharp
public class StickerGroupVo
{
    public long VoId { get; set; }
    public string VoCode { get; set; }
    public string VoName { get; set; }
    public string? VoDescription { get; set; }
    public string? VoCoverImageUrl { get; set; }
    public StickerGroupType VoGroupType { get; set; }
    public int VoSort { get; set; }
    public List<StickerVo> VoStickers { get; set; } = new();
}
```

**StickerVo**：

```csharp
public class StickerVo
{
    public long VoId { get; set; }
    public string VoCode { get; set; }
    public string VoName { get; set; }
    public string VoImageUrl { get; set; }
    public string? VoThumbnailUrl { get; set; }
    public bool VoIsAnimated { get; set; }
    public bool VoAllowInline { get; set; }
    public int VoUseCount { get; set; }
}
```

**ReactionSummaryVo**（聚合结果，由 Service 手动组装，非实体直接映射）：

```csharp
public class ReactionSummaryVo
{
    public string VoEmojiType { get; set; }      // "unicode" / "sticker"
    public string VoEmojiValue { get; set; }     // "😀" / "radish_girl/happy"
    public int VoCount { get; set; }             // 反应总数
    public bool VoIsReacted { get; set; }        // 当前登录用户是否已反应
    public string? VoThumbnailUrl { get; set; }  // sticker 时有值（GIF 使用第一帧）
}
```

**AutoMapper 策略**：

| 映射 | 策略 | 原因 |
|------|------|------|
| `StickerGroup → StickerGroupVo` | `RecognizeDestinationPrefixes("Vo")` 自动映射 | 字段名对应，仅前缀差异 |
| `Sticker → StickerVo` | `RecognizeDestinationPrefixes("Vo")` 自动映射 | 同上 |
| `ReactionSummaryVo` | Service 手动组装 | 聚合查询结果，非实体直接映射 |

---

## 图片规格约束

### 上传限制

| 规格项 | 静图（PNG / WebP / JPG） | GIF 动图 |
|--------|--------------------------|----------|
| 最大文件大小 | 2 MB | 5 MB |
| 最大尺寸（像素） | 512 × 512 | 512 × 512 |
| 最小尺寸（像素） | 16 × 16 | 16 × 16 |
| 允许格式 | `.png` `.webp` `.jpg` `.jpeg` | `.gif` |
| 缩略图生成 | 服务端压缩至 96 × 96，格式 WebP | 提取第一帧，生成 96 × 96 WebP |

> 缩略图生成由后端在 `StickerService.AddSticker` 调用 `ImageProcessorService` 处理，
> 结果存入 `Sticker.ThumbnailUrl`。

### Code 命名规范

`StickerGroup.Code` 和 `Sticker.Code` 遵循统一规则：

- **字符集**：仅允许 `[a-z0-9_]`（小写字母、数字、下划线）
- **长度**：1–100 字符
- **禁止**：空格、连字符、大写字母、特殊字符
- **唯一性**：`Sticker.Code` 在同一 `GroupId` 内唯一（数据库唯一索引保障）
- **文件名清洗规则**（批量上传时自动提取）：
  1. 去掉文件扩展名：`happy_face.png` → `happy_face`
  2. 转全小写：`HappyFace` → `happyface`
  3. 空格/连字符/点替换为 `_`：`happy-face 2` → `happy_face_2`
  4. 去除非 `[a-z0-9_]` 字符：`héllo!` → `hllo`
  5. 合并连续下划线：`happy__face` → `happy_face`
  6. 截断至 100 字符
  7. 若清洗结果为空，使用上传序号作为 fallback：`sticker_001`

> 清洗逻辑在后端 `StickerService.NormalizeCode()` 中实现，前端仅做格式提示，不做清洗。

---

## 内嵌表情包语法

### 格式规范

- **Unicode emoji**：直接插入字符，如 `😀`（编辑器/评论框原生支持）
- **官方表情包**：标准 Markdown img 语法，`sticker://` 协议

```markdown
![表情名](sticker://pack_code/sticker_code)

示例：
![开心](sticker://radish_girl/happy)
![惊讶](sticker://tieba/surprised)
```

### 为何选择 `sticker://` 协议

- 复用标准 Markdown img 语法，编辑器无需特殊 parser
- 与现有 `#radish:` URL 元数据机制一致，`MarkdownRenderer` 扩展点明确
- 降级友好：不支持此协议的渲染器显示 alt 文本而非破损图标
- 与 Unicode emoji 字符并存，无冲突

### MarkdownRenderer 扩展

```typescript
interface MarkdownRendererProps {
  content: string;
  // 新增：由父组件一次性拉取后向下传递，避免每个实例单独请求
  stickerGroups?: StickerGroupData[];
}

interface StickerGroupData {
  code: string;
  stickers: Array<{
    code: string;
    imageUrl: string;      // GIF 时为完整动图 URL
    thumbnailUrl?: string; // GIF 时为第一帧静止图
    isAnimated: boolean;
    allowInline: boolean;
  }>;
}
```

渲染行为：
- `sticker://` 图片**不触发灯箱**（与普通图片区分），阻止 click 冒泡至灯箱逻辑
- `isAnimated = true` 时：`<img src={imageUrl}>` 直接展示 GIF 动画
- 显示尺寸规范见 [UI 规范文档](./emoji-sticker-ui-spec.md)

---

## API 设计

### StickerController

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| GET | `/api/v1/Sticker/GetGroups` | AllowAnonymous | 获取所有启用分组（含各组表情），走缓存 |
| GET | `/api/v1/Sticker/GetGroupDetail/{code}` | AllowAnonymous | 单个分组详情 |
| POST | `/api/v1/Sticker/RecordUse` | 登录 | 记录表情使用次数（UseCount++）|
| POST | `/api/v1/Sticker/Admin/CreateGroup` | System/Admin | 创建分组 |
| PUT | `/api/v1/Sticker/Admin/UpdateGroup/{id}` | System/Admin | 编辑分组（自动失效缓存） |
| DELETE | `/api/v1/Sticker/Admin/DeleteGroup/{id}` | System/Admin | 软删除分组（自动失效缓存） |
| POST | `/api/v1/Sticker/Admin/AddSticker` | System/Admin | 单个新增表情 |
| POST | `/api/v1/Sticker/Admin/BatchAddStickers` | System/Admin | 批量新增表情（含 Code/Name 确认，见 Console 文档） |
| PUT | `/api/v1/Sticker/Admin/UpdateSticker/{id}` | System/Admin | 编辑表情（自动失效缓存） |
| DELETE | `/api/v1/Sticker/Admin/DeleteSticker/{id}` | System/Admin | 软删除表情（自动失效缓存） |

### ReactionController

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| GET | `/api/v1/Reaction/GetSummary` | AllowAnonymous | 单目标 Reaction 汇总（`targetType` + `targetId` 查询参数） |
| POST | `/api/v1/Reaction/BatchGetSummary` | AllowAnonymous | 批量获取多目标汇总（评论树场景，单次上限 100 条） |
| POST | `/api/v1/Reaction/Toggle` | 登录 | 添加/取消 Reaction，返回最新汇总 |

**Toggle 请求体**：

```typescript
interface ToggleReactionRequest {
  targetType: 'Post' | 'Comment' | 'ChatMessage';
  targetId: number;
  emojiType: 'unicode' | 'sticker';
  emojiValue: string;       // "😀" 或 "radish_girl/happy"
  thumbnailUrl?: string;    // sticker 时传入（GIF 传第一帧缩略图 URL）
}
```

**Toggle 响应**：返回该目标的最新 `ReactionSummaryVo[]`，前端直接替换本地状态，无需二次请求。

**Reaction 上限约束**：

| 约束项 | 限制值 | 说明 |
|--------|--------|------|
| 单条内容最多展示 Reaction 种类 | 20 种 | 超出时 `GetSummary` 仍返回全部，前端折叠展示 |
| 单用户对同一内容最多添加 Reaction 种类 | 10 种 | Service 层校验，超出返回 400 |
| `BatchGetSummary` 单次最大目标数 | 100 条 | 防止评论树过深导致查询超时 |

---

## 缓存策略

`GetGroups` 是整个选择器的基础数据，属于高频只读、低变更接口，适合 Redis 缓存。

| 缓存键 | 内容 | TTL | 失效触发 |
|--------|------|-----|---------|
| `sticker:groups:{tenantId}` | `StickerGroupVo[]`（含所有表情） | 30 分钟 | 任意 Admin 写操作（CreateGroup / UpdateGroup / DeleteGroup / BatchAddStickers / UpdateSticker / DeleteSticker） |

**实现位置**：`StickerService.GetGroupsAsync()` 中先查缓存，未命中再查库并写入。写操作方法末尾调用 `cache.RemoveAsync($"sticker:groups:{tenantId}")` 主动失效。

---

## 数据库索引建议

| 表 | 索引 | 用途 |
|----|------|------|
| `StickerGroup` | `(TenantId, Code)` 唯一索引 | 防重复，快速按 Code 查找 |
| `Sticker` | `(GroupId, Code)` 唯一索引 | 防重复，快速按 Code 查找 |
| `Reaction` | `(UserId, TargetType, TargetId, EmojiValue)` 唯一索引 | 幂等保障，防止同一用户重复添加相同表情 |
| `Reaction` | `(TargetType, TargetId, IsDeleted)` 联合索引 | 快速聚合查询单目标所有 Reaction |

---

## 扩展性设计

### Phase 3：聊天室 Reaction

使用 `TargetType = 'ChatMessage'`，无需修改数据库结构，仅需：
1. `ReactionController.Toggle` 接受 `targetType = 'ChatMessage'`
2. 前端 SignalR Hub 广播 Reaction 变更事件，实现多端实时更新
3. 聊天室消息气泡下方挂载 `ReactionBar` 组件

### Phase 3：表情商店

1. 新增 `GroupType = 'Premium'` 分组
2. 新增 `UserStickerGroup` 实体（记录购买记录：用户ID、分组ID、购买时间、萝卜币消耗）
3. 复用萝卜币支付体系，付费后解锁对应分组
4. `GetGroups` 接口增加 `VoIsOwned` 字段标记用户已购分组

### UseCount 热度统计

`Sticker.UseCount` 在以下场景自增：
- 插入正文（`RecordUse` 接口）
- 用于 Reaction（`Toggle` 接口，仅 sticker 类型）

可基于此实现 Phase 3 "热门表情"快捷入口（`StickerPicker` 首屏展示 Top N）。

---

## 分阶段实现路线图

| 阶段 | 内容 | 后端范围 | 前端范围 |
|------|------|----------|----------|
| **Phase 1** 核心能力 | StickerGroup/Sticker 实体 + Console 管理 + StickerPicker 组件 + MarkdownEditor/评论框集成 + `sticker://` 渲染 | 2 个实体、2 个 Service、1 个 Controller | 2 个新组件；修改 MarkdownEditor、MarkdownRenderer、CreateCommentForm |
| **Phase 2** Reaction 系统 | Reaction 实体 + ReactionController + ReactionBar 组件 + PostDetail/CommentNode 集成 | 1 个实体、1 个 Service、1 个 Controller | 1 个新组件；修改 PostDetail、CommentNode |
| **Phase 3** 扩展 | 聊天室 Reaction、表情商店、UseCount 排行、实时同步 | 按需，不影响 Phase 1/2 结构 | 按需 |

---

## 参考文件

| 文件 | 参考用途 |
|------|----------|
| `Radish.Model/Models/Attachment.cs` | 实体字段规范、IDeleteFilter 实现模式 |
| `Radish.Extension/AutoMapperExtension/CustomProfiles/ForumProfile.cs` | AutoMapper Profile 编写方式 |
| `Frontend/radish.ui/src/components/MarkdownEditor/MarkdownEditor.tsx` | 替换内嵌 emoji 选择器 |
| `Frontend/radish.ui/src/components/MarkdownRenderer/MarkdownRenderer.tsx` | 扩展 `sticker://` 协议渲染逻辑 |
| `Frontend/radish.client/src/apps/forum/components/CreateCommentForm.tsx` | 集成 StickerPicker，复用 `insertTextAtCursor` |
| `Frontend/radish.client/src/apps/forum/components/PostDetail.tsx` | Phase 2：集成 ReactionBar |
| `Frontend/radish.client/src/apps/forum/components/CommentNode.tsx` | Phase 2：集成 ReactionBar |
