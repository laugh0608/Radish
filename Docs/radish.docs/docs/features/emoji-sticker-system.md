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

### 软删除与唯一性策略（补充）

为避免历史 `sticker://` 引用歧义，`Code` 采用“软删除后不可复用”策略：

- `StickerGroup.Code`：同一 `TenantId` 下全局唯一，软删除后仍保留占位，不允许新建同 Code 分组
- `Sticker.Code`：同一 `GroupId` 下唯一，软删除后不允许新建同 Code 表情
- 需要“恢复旧表情”时，优先使用 `Restore` 而非“删后重建”

`Reaction` 的软删除为“取消回应”语义，唯一键 `(UserId, TargetType, TargetId, EmojiValue)` 与软删除配合规则如下：

- 已存在且 `IsDeleted=true`：`Toggle` 时恢复该行并更新审计字段
- 不存在：新建
- 已存在且 `IsDeleted=false`：`Toggle` 时软删除

> 这样可以保证同一用户同一目标同一表情始终复用同一业务行，避免重复记录和并发抖动。

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

### 历史内容兼容策略（补充）

`GetGroups` 仅用于选择器数据（启用分组 + 启用表情），不作为历史内容渲染的唯一数据源：

- 分组/表情被禁用后：**不影响已发布内容渲染**
- 分组/表情被软删除后：默认仍可被 `sticker://` 解析并展示，避免历史帖子“表情丢失”
- 若资源文件确实不可用：降级为 `[已下架表情: packCode/stickerCode]` 文本占位，不显示破损图

实现建议：

- 新增“渲染专用解析”接口（如 `BatchResolve`），支持按 `packCode/stickerCode` 批量解析
- 帖子/评论页渲染时先走本地 `stickerGroups` 索引，未命中再走解析接口补齐
- 解析结果可按 `pack/sticker` 做短 TTL 缓存，避免重复查库

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
}
```

**Toggle 响应**：返回该目标的最新 `ReactionSummaryVo[]`，前端直接替换本地状态，无需二次请求。

> `sticker` 类型的缩略图由后端根据 `emojiValue` 反查 `Sticker` 生成，避免信任前端传入 URL。

**Reaction 上限约束**：

| 约束项 | 限制值 | 说明 |
|--------|--------|------|
| 单条内容最多展示 Reaction 种类 | 20 种 | 超出时 `GetSummary` 仍返回全部，前端折叠展示 |
| 单用户对同一内容最多添加 Reaction 种类 | 10 种 | Service 层校验，超出返回 400 |
| `BatchGetSummary` 单次最大目标数 | 100 条 | 防止评论树过深导致查询超时 |

### Toggle 服务端并发与幂等流程（补充）

`Toggle` 需在事务内执行，推荐流程：

1. 校验 `targetType` 白名单、`emojiType` 合法性、目标是否存在
2. 若 `emojiType='sticker'`，按 `emojiValue` 反查表情并校验 `IsEnabled`
3. 查询 `(UserId, TargetType, TargetId, EmojiValue)`（包含软删除）
4. 按状态执行“恢复 / 软删除 / 新建”
5. 提交事务；若触发唯一索引冲突，执行一次短重试
6. 聚合返回最新 `ReactionSummaryVo[]`

这样可覆盖“同端双击”“多端同时点击”等并发场景。

### API 业务错误码约定（补充）

| 场景 | HTTP | Code | 说明 |
|------|------|------|------|
| 未登录调用 Toggle/RecordUse | 401 | `AuthRequired` | 需登录 |
| `emojiType` 或 `targetType` 非法 | 400 | `InvalidArgument` | 参数错误 |
| sticker 不存在/已禁用 | 404/400 | `StickerNotAvailable` | 表情不可用 |
| 用户 Reaction 种类超上限 | 400 | `ReactionLimitExceeded` | 达到 10 种上限 |
| 批量查询超过 100 条 | 400 | `BatchSizeExceeded` | 请求量超限 |
| 发生并发冲突且重试失败 | 409 | `ConcurrentConflict` | 建议前端重拉一次 |

### 高频接口限流建议（补充）

| 接口 | 维度 | 建议阈值 | 说明 |
|------|------|----------|------|
| `POST /Sticker/RecordUse` | UserId + IP | 60 次/分钟 | 防止刷热度 |
| `POST /Reaction/Toggle` | UserId + TargetId | 20 次/分钟 | 防止快速抖动 |
| `POST /Reaction/BatchGetSummary` | IP | 30 次/分钟 | 防止批量拉取滥用 |

### API DTO 与响应示例（联调补充）

为避免前后端联调歧义，以下示例统一基于后端 `MessageModel<T>`（前端经 `@radish/ui` 解析为 `ParsedApiResponse<T>`）。

**统一响应壳（后端原始）**：

```typescript
interface MessageModel<T> {
  isSuccess: boolean;
  statusCode: number;
  messageInfo: string;
  messageInfoDev?: string;
  responseData?: T;
  code?: string;
  messageKey?: string;
}
```

**前端解析后（`apiGet/apiPost` 返回）**：

```typescript
interface ParsedApiResponse<T> {
  ok: boolean;
  data?: T;
  message?: string;
  code?: string;
  statusCode?: number;
}
```

**核心 DTO（建议）**：

```typescript
interface GetStickerGroupsResponse {
  voId: number;
  voCode: string;
  voName: string;
  voDescription?: string;
  voCoverImageUrl?: string;
  voGroupType: 'Official' | 'Premium';
  voSort: number;
  voStickers: Array<{
    voId: number;
    voCode: string;
    voName: string;
    voImageUrl: string;
    voThumbnailUrl?: string;
    voIsAnimated: boolean;
    voAllowInline: boolean;
    voUseCount: number;
  }>;
}

interface RecordStickerUseRequest {
  emojiType: 'unicode' | 'sticker';
  emojiValue: string; // "😀" 或 "radish_girl/happy"
}

interface BatchGetReactionSummaryRequest {
  targetType: 'Post' | 'Comment' | 'ChatMessage';
  targetIds: number[]; // <= 100
}

type BatchGetReactionSummaryResponse = Record<string, ReactionSummaryVo[]>;
// key 为 targetId 字符串，如 { "123": [...], "124": [...] }
```

**请求示例：`POST /api/v1/Sticker/RecordUse`**：

```json
{
  "emojiType": "sticker",
  "emojiValue": "radish_girl/happy"
}
```

**成功响应示例：`GET /api/v1/Sticker/GetGroups`**：

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "ok",
  "responseData": [
    {
      "voId": 1,
      "voCode": "radish_girl",
      "voName": "萝卜娘",
      "voCoverImageUrl": "https://cdn.example.com/stickers/radish_girl/cover.webp",
      "voGroupType": "Official",
      "voSort": 10,
      "voStickers": [
        {
          "voId": 1001,
          "voCode": "happy",
          "voName": "开心",
          "voImageUrl": "https://cdn.example.com/stickers/radish_girl/happy.gif",
          "voThumbnailUrl": "https://cdn.example.com/stickers/radish_girl/happy-thumb.webp",
          "voIsAnimated": true,
          "voAllowInline": true,
          "voUseCount": 321
        }
      ]
    }
  ]
}
```

**成功响应示例：`POST /api/v1/Reaction/BatchGetSummary`**：

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "ok",
  "responseData": {
    "101": [
      {
        "voEmojiType": "unicode",
        "voEmojiValue": "😀",
        "voCount": 2,
        "voIsReacted": false,
        "voThumbnailUrl": null
      }
    ],
    "102": [
      {
        "voEmojiType": "sticker",
        "voEmojiValue": "radish_girl/happy",
        "voCount": 5,
        "voIsReacted": true,
        "voThumbnailUrl": "https://cdn.example.com/stickers/radish_girl/happy-thumb.webp"
      }
    ]
  }
}
```

**成功响应示例：`POST /api/v1/Reaction/Toggle`**：

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "操作成功",
  "responseData": [
    {
      "voEmojiType": "unicode",
      "voEmojiValue": "😀",
      "voCount": 12,
      "voIsReacted": true,
      "voThumbnailUrl": null
    },
    {
      "voEmojiType": "sticker",
      "voEmojiValue": "radish_girl/happy",
      "voCount": 7,
      "voIsReacted": false,
      "voThumbnailUrl": "https://cdn.example.com/stickers/radish_girl/happy-thumb.webp"
    }
  ]
}
```

**失败响应示例：超过 Reaction 上限**：

```json
{
  "isSuccess": false,
  "statusCode": 400,
  "code": "ReactionLimitExceeded",
  "messageInfo": "你已对该内容添加了 10 种回应（上限）",
  "responseData": null
}
```

**失败响应示例：并发冲突（重试后仍失败）**：

```json
{
  "isSuccess": false,
  "statusCode": 409,
  "code": "ConcurrentConflict",
  "messageInfo": "操作过于频繁，请刷新后重试",
  "responseData": null
}
```

---

## 缓存策略

`GetGroups` 是整个选择器的基础数据，属于高频只读、低变更接口，适合 Redis 缓存。

| 缓存键 | 内容 | TTL | 失效触发 |
|--------|------|-----|---------|
| `sticker:groups:{tenantId}` | `StickerGroupVo[]`（含所有表情） | 30 分钟 | 任意 Admin 写操作（CreateGroup / UpdateGroup / DeleteGroup / BatchAddStickers / UpdateSticker / DeleteSticker） |

**实现位置**：`StickerService.GetGroupsAsync()` 中先查缓存，未命中再查库并写入。写操作方法末尾调用 `cache.RemoveAsync($"sticker:groups:{tenantId}")` 主动失效。

### Phase 3 缓存演进预留（Premium）

Phase 3 引入 `VoIsOwned` 后，避免将“用户态”塞入当前公共缓存：

- 保持 `sticker:groups:{tenantId}` 作为公共基础数据缓存（分组 + 贴图静态信息）
- 新增 `user:sticker:owned:{tenantId}:{userId}` 记录已购分组 ID 列表
- `GetGroups` 组装响应时进行“公共数据 + 用户拥有关系”叠加，降低缓存基数
- 购买/退款时仅失效用户态缓存，不失效全局分组缓存

---

## 数据库索引建议

| 表 | 索引 | 用途 |
|----|------|------|
| `StickerGroup` | `(TenantId, Code)` 唯一索引 | 防重复，快速按 Code 查找 |
| `Sticker` | `(GroupId, Code)` 唯一索引 | 防重复，快速按 Code 查找 |
| `Reaction` | `(UserId, TargetType, TargetId, EmojiValue)` 唯一索引 | 幂等保障，防止同一用户重复添加相同表情 |
| `Reaction` | `(TargetType, TargetId, IsDeleted)` 联合索引 | 快速聚合查询单目标所有 Reaction |

> 说明：`StickerGroup.Code` / `Sticker.Code` 的唯一性默认覆盖软删除记录，保持历史 `sticker://` 地址稳定。

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

## Phase 1 任务拆分与验收标准（补充）

| 子任务 | 主要内容 | 验收标准 |
|------|------|------|
| 后端实体与接口 | `StickerGroup/Sticker` + Admin API + `RecordUse` | `Radish.Api.http` 可完整走通 CRUD/批量上传/记录使用；软删除与唯一性规则生效 |
| 渲染链路 | `sticker://` 解析、历史兼容降级、MarkdownRenderer 接入 | 历史帖子在分组禁用后仍正常显示；资源失效时显示占位文本不破版 |
| Console 管理页 | 分组管理、批量上传确认、冲突修正重提 | 批量上传 50 张内流程可完成，冲突行高亮可修复后再次提交 |
| UI 组件 | `StickerPicker`（insert/reaction 模式） | Markdown 编辑器与评论框都可插入；`AllowInline=false` 在插入模式禁用 |
| 性能与稳定性 | 缓存、限流、并发冲突重试 | `GetGroups` 命中缓存；`Toggle` 快速双击无脏数据；高频接口触发限流返回明确错误码 |

## 实现落地文件清单（补充）

以下为建议落地位置，便于直接拆工单：

### 后端（Phase 1 + Phase 2）

| 目标 | 文件（建议） | 说明 |
|------|--------------|------|
| 实体 | `Radish.Model/StickerGroup.cs` | 新增分组实体（`ITenantEntity + IDeleteFilter`） |
| 实体 | `Radish.Model/Sticker.cs` | 新增贴图实体（含 `ThumbnailUrl` / `AllowInline`） |
| 实体（Phase 2） | `Radish.Model/Reaction.cs` | 新增回应实体 |
| ViewModel | `Radish.Model/ViewModels/StickerGroupVo.cs` | 含 `VoStickers` |
| ViewModel | `Radish.Model/ViewModels/StickerVo.cs` | Sticker 输出模型 |
| ViewModel | `Radish.Model/ViewModels/ReactionSummaryVo.cs` | 聚合输出模型 |
| Service 接口 | `Radish.IService/IStickerService.cs` | GetGroups/RecordUse/Admin CRUD |
| Service 接口（Phase 2） | `Radish.IService/IReactionService.cs` | Summary/BatchSummary/Toggle |
| Service 实现 | `Radish.Service/StickerService.cs` | 缓存、清洗、批量入库 |
| Service 实现（Phase 2） | `Radish.Service/ReactionService.cs` | 事务 + 并发幂等 |
| Controller | `Radish.Api/Controllers/StickerController.cs` | Public + Admin 接口 |
| Controller（Phase 2） | `Radish.Api/Controllers/ReactionController.cs` | 汇总与 Toggle |
| AutoMapper | `Radish.Extension/AutoMapperExtension/CustomProfiles/ForumProfile.cs` | 增加 Sticker 相关映射 |
| 示例请求 | `Radish.Api/Radish.Api.http` | 增加接口请求样例 |

### 前端（`@radish/ui` + `radish.client` + `radish.console`）

| 目标 | 文件（建议） | 说明 |
|------|--------------|------|
| 组件 | `Frontend/radish.ui/src/components/StickerPicker/StickerPicker.tsx` | Picker 容器 |
| 组件 | `Frontend/radish.ui/src/components/StickerPicker/EmojiTab.tsx` | Emoji 分类网格 |
| 组件 | `Frontend/radish.ui/src/components/StickerPicker/StickerTab.tsx` | Sticker 网格 |
| 组件 | `Frontend/radish.ui/src/components/ReactionBar/ReactionBar.tsx` | 气泡条（Phase 2） |
| 组件 | `Frontend/radish.ui/src/components/ReactionBar/ReactionPickerPopover.tsx` | 快速选择器（Phase 2） |
| 渲染扩展 | `Frontend/radish.ui/src/components/MarkdownRenderer/MarkdownRenderer.tsx` | `sticker://` 渲染 |
| 编辑器接入 | `Frontend/radish.ui/src/components/MarkdownEditor/MarkdownEditor.tsx` | 插入模式接入 |
| Forum 集成 | `Frontend/radish.client/src/apps/forum/components/CreateCommentForm.tsx` | 评论框接入 |
| Forum 集成（Phase 2） | `Frontend/radish.client/src/apps/forum/components/PostDetail.tsx` | ReactionBar 接入 |
| Forum 集成（Phase 2） | `Frontend/radish.client/src/apps/forum/components/CommentNode.tsx` | ReactionBar 接入 |
| API | `Frontend/radish.client/src/api/sticker.ts` | getGroups/recordUse |
| API（Phase 2） | `Frontend/radish.client/src/api/reaction.ts` | summary/toggle |
| Console 页面 | `Frontend/radish.console/src/pages/Stickers/` | 管理后台页面 |

## 联调顺序建议（补充）

推荐按照“先静态资源，再交互，再并发”的顺序：

1. 后端落地 `StickerGroup/Sticker` 实体、基础 Admin CRUD、`GetGroups`
2. Console 先做分组 CRUD + 单图上传，确认基础链路
3. 完成 `BatchAddStickers` 与冲突修正重提
4. `@radish/ui` 完成 `StickerPicker`，先打通插入模式
5. `MarkdownRenderer` 接入 `sticker://` 与历史降级逻辑
6. Forum 发帖/评论接入并验证 `RecordUse`
7. Phase 2 再接 `Reaction`（`GetSummary/BatchGetSummary/Toggle`）
8. 最后做并发压测、限流验证、缓存命中验证

## 测试与回归清单（补充）

### 后端测试

- `NormalizeCode`：覆盖中文/重音字符/空串/超长/纯符号文件名
- `BatchAddStickers`：成功、部分冲突、图片处理失败、事务回滚
- `Toggle`：新增、取消、恢复、并发双击、唯一键冲突重试
- 缓存：`GetGroups` 命中/失效路径正确
- 权限：Admin 接口仅 `System/Admin` 可访问

### 前端测试

- StickerPicker：Tab 切换、搜索、插入、禁用态、键盘导航、移动端点击
- MarkdownRenderer：`sticker://` 正常渲染、异常地址降级显示
- ReactionBar：乐观更新、失败回滚、折叠/展开、上限禁用
- Console：批量上传流程、冲突行高亮、失败重试、排序回滚

### 回归重点

- 不影响现有图片灯箱与普通 Markdown 图片渲染
- 不影响评论发布/编辑历史/通知等既有论坛链路
- 大量表情数据（>200）时滚动流畅，无明显卡顿

## 上线与观测要点（补充）

- 日志：记录 `BatchAddStickers` 成功数、冲突数、失败原因分布
- 监控：关注 `Toggle` 的 400/409 比例和 `BatchGetSummary` 响应耗时
- 告警：`ImageProcessFailed` 异常比例超过阈值时告警
- 回滚：保留 `sticker://` 文本降级策略，保证即使服务异常页面也不破版

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
