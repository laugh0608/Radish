# 表情包系统 - Console 管理后台实现

> Radish 表情包系统 Console 管理后台详细设计
>
> **版本**: v26.3.0
>
> **最后更新**: 2026.03.01
>
> **关联文档**：
> [系统总览与数据模型](./emoji-sticker-system.md) ·
> [UI 视觉与交互规范](./emoji-sticker-ui-spec.md) ·
> [Reaction Phase 2 设计冻结稿](./emoji-sticker-reaction-phase2.md)

---

## 概述

表情包管理入口位于 Console 后台导航的"内容管理"分组下。管理员可以：

- 创建/编辑/删除表情包分组（StickerGroup）
- 在分组内批量上传表情图片，上传后通过内联表格确认每个表情的 Code 和显示名
- 管理单个表情（编辑、启用/禁用、排序、软删除）
- 切换表情的 `AllowInline` 属性（控制是否可内嵌正文）

## 当前实现状态（2026-02-27）

### 已实现

- Console 菜单已接入“表情包管理”
- 分组管理：
  - 列表查询（含禁用）
  - 新建/编辑弹窗
  - 软删除
- 分组内表情管理：
  - 列表查询（含禁用）
  - 单个新增/编辑弹窗
  - 软删除
  - 批量排序保存（`BatchUpdateSort`）
- 批量上传四步流（文件选择、上传进度、确认表格、冲突修复重提）
- 分组封面图上传组件（上传 + 预览 + 清空）
- API 封装：`Frontend/radish.console/src/api/stickerApi.ts`

### 进行中 / 未实现

- 网格模式与拖拽排序（当前为列表模式 + 数字排序）

### 与 Reaction Phase 2 的协作边界（2026-03-01）

- Console 本阶段无需直接实现 `ReactionBar`，但需确保 Sticker 元数据可稳定支撑 Reaction 场景：
  - `IsEnabled` 状态准确
  - `ThumbnailUrl` 可用
  - `Code` 稳定且不可复用
- 若后续接入 Console 侧 Reaction 运营页，接口契约以 [Reaction Phase 2 设计冻结稿](./emoji-sticker-reaction-phase2.md) 为准

---

## 页面结构

```
Console 导航
└── 内容管理
    └── 表情包管理
        ├── /stickers                  → StickerGroupList（分组列表 + 分组弹窗）
        └── /stickers/:groupId/items   → StickerList（分组内表情管理 + 表情弹窗）
```

文件目录：

```
Frontend/radish.console/src/pages/Stickers/
├── StickerGroupList.tsx       # 分组列表主页
├── StickerGroupForm.tsx       # 创建/编辑分组表单
├── StickerList.tsx            # 分组内表情管理（当前：列表 + 基础 CRUD）
├── StickerForm.tsx            # 单个表情创建/编辑弹窗
├── StickerGroupList.css
└── index.ts
```

---

## StickerGroupList - 分组列表

### 功能说明

- 表格展示所有分组（含已禁用），列：封面图、名称、Code、类型、表情数量、状态、排序、操作
- 支持排序管理（当前通过输入 `Sort` + 批量保存）
- 操作栏：编辑、启用/禁用、进入表情列表、删除（软删除，需二次确认）
- 右上角"新建分组"按钮 → 打开分组表单弹窗

### 关键交互

**当前排序实现**：通过排序字段输入 + 提交更新（后续可演进为拖拽排序）。

**删除确认**：分组删除前检查该分组内是否有已启用的表情，若有则提示"该分组含 N 个启用表情，删除后将一并禁用，确认删除？"。

---

## StickerGroupForm - 创建/编辑分组

### 表单字段

| 字段 | 类型 | 验证规则 | 说明 |
|------|------|----------|------|
| 名称（Name） | 文本 | 必填，1–100 字符 | 显示名，任意字符 |
| 标识符（Code） | 文本 | 必填，`[a-z0-9_]`，1–100 字符，全局唯一 | 编辑时只读（不可修改） |
| 类型（GroupType） | 单选 | 必填 | Official / Premium |
| 描述（Description） | 文本域 | 可选，最长 500 字符 | |
| 封面图（CoverImageUrl） | 图片上传 | 可选，≤ 2MB，PNG/WebP/JPG | 上传后展示预览，显示于 StickerPicker Tab |
| 是否启用（IsEnabled） | 开关 | 默认开启 | |

### Code 字段行为

- 新建时：用户输入名称后，自动将名称拼音化（可选，不强制）或由用户手填；输入时实时显示合法性提示
- 编辑时：Code 字段显示为只读文本（灰色），下方提示"标识符创建后不可修改"
- 提交前调用后端唯一性验证：`GET /api/v1/Sticker/CheckGroupCode?code=xxx`

### 封面图上传

使用 `@radish/ui` `FileUpload` 组件，配置：

```typescript
<FileUpload
  accept=".png,.webp,.jpg,.jpeg"
  maxSize={2 * 1024 * 1024}
  uploadUrl="/api/v1/Attachment/UploadImage"
  onSuccess={(attachment) => form.setValue('coverImageUrl', attachment.url)}
  withAuth
/>
```

上传成功后在表单内展示 64×64 预览图。

---

## StickerList - 分组内表情管理

### 布局说明

页面分为两个区域：

1. **顶部**：分组信息摘要（名称、Code）+ "批量上传"按钮 + "单个添加"按钮
2. **主体**：表情网格/列表，支持视图切换（网格模式和列表模式）

### 表情网格/列表展示

**网格模式**（默认）：

- 每行 6 列，每格显示缩略图（64×64）+ Code + 操作图标（悬浮显示）
- GIF 表情在网格中展示静止缩略图，鼠标 hover 时播放动图

**列表模式**：

- 表格列：预览图（40×40）、名称、Code、是否 GIF、AllowInline、UseCount、状态、排序、操作
- 支持列排序（按 UseCount 降序可快速找到热门表情）

### 批量上传流程

#### 第一步：选择文件

点击"批量上传"按钮，弹出上传区域（Modal 或页内展开区域）：

```
┌─────────────────────────────────────────┐
│  拖拽文件到此处，或点击选择文件            │
│                                         │
│  支持格式：PNG / WebP / JPG / GIF        │
│  静图 ≤ 2MB，GIF ≤ 5MB                  │
│  尺寸：16×16 ~ 512×512                  │
│  可同时选择多个文件                       │
└─────────────────────────────────────────┘
```

文件选中后展示待上传列表（文件名、大小、格式图标、移除按钮），点击"开始上传"。

#### 第二步：逐文件上传（带进度）

使用 `@radish/ui` `FileUpload` 组件的批量上传模式，或直接调用 `POST /api/v1/Attachment/UploadImage` 并发上传（最大并发 3 个）：

```
正在上传 (3/8)...

[图标] happy_face.png       ████████████░░░░  75%  ✓
[图标] sad_face.png         ████████████████  100% ✓
[图标] angry.gif            ████░░░░░░░░░░░░  25%  ⟳
[图标] surprised.png        ░░░░░░░░░░░░░░░░  0%   -
...
```

上传失败的文件单独标记红色，提供"重试"按钮，不阻塞其他文件。

#### 第三步：上传完成后的 Code/Name 确认表格

所有文件上传完成后（或用户点击"完成上传，继续配置"），展示 **BatchUploadConfirmTable** 组件：

```
已上传 8 个文件，请确认每个表情的标识符和名称后保存。

┌──────┬──────────────────┬──────────────────┬──────────────────┬──────────────┬──────────────┐
│ 预览 │ 原文件名           │ 标识符 (Code) *  │ 显示名 (Name) *  │ 允许内嵌正文  │ 操作         │
├──────┼──────────────────┼──────────────────┼──────────────────┼──────────────┼──────────────┤
│  🖼  │ happy_face.png   │ happy_face       │ 开心             │  ☑          │ [删除]       │
│  🖼  │ sad_face.png     │ sad_face         │ 难过             │  ☑          │ [删除]       │
│ GIF │ angry.gif         │ angry            │ 生气             │  ☑          │ [删除]       │
│  🖼  │ HappyFace2.png   │ happyface2       │ 开心2            │  ☑          │ [删除]       │
│ ⚠️  │ héllo!.png       │ hllo ⚠ 已自动清洗 │ (请填写)         │  ☑          │ [删除]       │
└──────┴──────────────────┴──────────────────┴──────────────────┴──────────────┴──────────────┘

[取消]                                                             [保存全部 (8)]
```

表格行为细节：

- **Code 列**：初始值为后端清洗后的文件名（调用 `/api/v1/Sticker/NormalizeCode` 预览），可直接行内编辑；输入时实时校验 `[a-z0-9_]` 格式；若与分组内已有 Code 或本次其他行重复，显示红色边框和提示
- **Name 列**：初始值为清洗后的 Code 值（如 `happy_face`），管理员通常需要改为中文名；空值时提示必填
- **AllowInline 列**：默认勾选；若图片风格不适合正文嵌入（如过于夸张的大表情），管理员可取消勾选
- **预览图**：静图直接展示，GIF 展示第一帧（标注 "GIF" badge）；hover 时 GIF 播放动图
- **文件名清洗提示**：若原文件名被清洗（有字符被去除或转换），在 Code 列下方显示橙色 "⚠ 已自动清洗" 标注
- **删除行**：移除该行（对应的已上传 Attachment 不立即删除，由后台定时清理孤立附件）

#### 第四步：保存

点击"保存全部"后调用：

```
POST /api/v1/Sticker/BatchAddStickers
```

请求体：

```typescript
interface BatchAddStickersRequest {
  groupId: number;
  stickers: Array<{
    attachmentId: number;   // 第二步上传得到的附件 ID
    code: string;
    name: string;
    allowInline: boolean;
  }>;
}
```

后端处理逻辑：
1. 校验每个 Code 在组内的唯一性（批量校验，返回哪些 Code 冲突）
2. 从 Attachment 获取 `ImageUrl`，调用 `ImageProcessorService` 生成缩略图（GIF 取第一帧）
3. 设置 `IsAnimated`（根据文件扩展名或 `Attachment.ContentType`）
4. 批量 `INSERT` Sticker 记录
5. 失效 Redis 缓存 `sticker:groups:{tenantId}`

**Code 冲突处理**：若部分 Code 冲突，接口返回冲突列表（`[{code: "happy", message: "与已有表情重复"}]`），前端高亮对应行，不关闭表格，管理员修改后重新提交。

#### 第四步补充：事务边界与失败处理

`BatchAddStickers` 建议采用“单请求单事务”：

- **事务内**：Code 唯一性校验（最终校验）、缩略图生成、`Sticker` 批量写入
- **事务外**：Attachment 上传（第二步已完成）

失败处理策略：

- 若缩略图生成或数据库写入任一步失败：本次 `BatchAddStickers` 整体回滚，不产生部分成功 `Sticker`
- 前端保留确认表格内容与上传结果，管理员可修正后直接重试
- 已上传 Attachment 不立即删除，交由孤儿附件清理任务处理（避免误删）

> 这样可避免“分组内只写入一半表情”的脏数据状态，简化前端恢复逻辑。

---

## 单个表情编辑

点击表情操作中的"编辑"，弹出编辑 Modal：

| 字段 | 说明 |
|------|------|
| 预览图 | 展示当前图片（GIF 播放动图），可重新上传替换 |
| 标识符（Code） | 只读（创建后不可修改）|
| 显示名（Name） | 可编辑 |
| 允许内嵌正文（AllowInline） | 开关 |
| 是否启用（IsEnabled） | 开关 |
| 排序（Sort） | 数字输入框 |

---

## 排序管理

**分组内表情排序**：

- 列表模式下：拖拽行改变顺序，或直接编辑 Sort 数值
- 网格模式下：暂不支持拖拽（网格交互复杂），通过切换到列表模式进行排序
- 排序变更后调用 `PUT /api/v1/Sticker/UpdateSticker/{id}` 单条更新，或提供"批量保存排序"按钮一次性提交

**分组排序**：在 `StickerGroupList` 页面拖拽整行，同上。

---

## BatchAddStickers 接口约束

| 约束项 | 限制值 | 说明 |
|--------|--------|------|
| 单次批量上传文件数 | ≤ 50 | 前端文件选择时限制；超出时提示分批上传 |
| 单次 BatchAddStickers 请求表情数 | ≤ 50 | 与上传文件数一致 |
| 并发上传数 | 3 | 前端控制，防止同时大量请求 |

### BatchAddStickers 错误返回（补充）

| 场景 | HTTP | Code | 前端处理 |
|------|------|------|---------|
| 分组不存在/已删除 | 404 | `StickerGroupNotFound` | 关闭弹窗并提示刷新页面 |
| 部分 Code 冲突 | 409/400 | `BatchCodeConflict` | 高亮冲突行，不清空表格 |
| 图片处理失败（缩略图生成失败） | 500/400 | `ImageProcessFailed` | 保留已上传列表，允许重试 |
| 超过单次上限（>50） | 400 | `BatchSizeExceeded` | 在前端文件选择阶段拦截 |
| 权限不足 | 403 | `Forbidden` | 提示无权限 |

### BatchAddStickers 联调 JSON 示例（补充）

统一采用后端 `MessageModel<T>` 响应壳：

```typescript
interface MessageModel<T> {
  isSuccess: boolean;
  statusCode: number;
  messageInfo: string;
  code?: string;
  responseData?: T;
}
```

请求示例：

```json
{
  "groupId": 12,
  "stickers": [
    {
      "attachmentId": 9001,
      "code": "happy_face",
      "name": "开心",
      "allowInline": true
    },
    {
      "attachmentId": 9002,
      "code": "sad_face",
      "name": "难过",
      "allowInline": true
    }
  ]
}
```

成功响应示例（全部写入成功）：

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "批量保存成功",
  "responseData": {
    "voGroupId": 12,
    "voCreatedCount": 2,
    "voStickerIds": [10021, 10022],
    "voConflicts": [],
    "voFailedItems": []
  }
}
```

失败响应示例（部分 Code 冲突）：

```json
{
  "isSuccess": false,
  "statusCode": 409,
  "code": "BatchCodeConflict",
  "messageInfo": "存在重复标识符，请修改后重试",
  "responseData": {
    "voConflicts": [
      {
        "voRowIndex": 0,
        "voCode": "happy_face",
        "voMessage": "与该分组内已有表情重复"
      }
    ],
    "voFailedItems": []
  }
}
```

失败响应示例（图片处理失败）：

```json
{
  "isSuccess": false,
  "statusCode": 500,
  "code": "ImageProcessFailed",
  "messageInfo": "缩略图生成失败，请稍后重试",
  "responseData": {
    "voConflicts": [],
    "voFailedItems": [
      {
        "voRowIndex": 1,
        "voAttachmentId": 9002,
        "voCode": "sad_face",
        "voMessage": "GIF 第一帧提取失败"
      }
    ]
  }
}
```

### 孤儿 Attachment 清理策略（补充）

“孤儿附件”指已上传成功但未被 `Sticker.AttachmentId` 引用的文件。

- **产生场景**：管理员在确认表格删除行、取消保存、或 `BatchAddStickers` 失败回滚
- **清理方式**：后台定时任务按 `Attachment` 引用关系扫描清理
- **建议延迟**：仅清理创建时间超过 `24h` 的孤儿附件，避免管理员短时间重试时文件丢失
- **审计日志**：记录清理数量与附件 ID 列表（debug/info 级别）
- **白名单排除**：仅清理表情包上传目录/业务标记的 Attachment，避免误删其他业务图片

---

## API 补充：管理端专用

在主文档 `StickerController` 基础上，Console 额外调用：

| 方法 | 路由 | 说明 |
|------|------|------|
| GET | `/api/v1/Sticker/CheckGroupCode?code=xxx` | Code 唯一性预检（新建分组时） |
| GET | `/api/v1/Sticker/CheckStickerCode?groupId=&code=xxx` | Sticker Code 唯一性预检 |
| GET | `/api/v1/Sticker/NormalizeCode?filename=xxx` | 预览文件名清洗结果（返回清洗后的 Code） |
| GET | `/api/v1/Sticker/GetGroupStickers/{groupId}` | 获取分组内所有表情（含禁用，管理端专用） |
| GET | `/api/v1/Sticker/GetAdminGroups` | 获取管理端分组（含禁用） |
| PUT | `/api/v1/Sticker/BatchUpdateSort` | 批量更新表情 Sort 字段 |

### 管理端预检接口联调示例（补充）

统一响应壳：

```typescript
interface MessageModel<T> {
  isSuccess: boolean;
  statusCode: number;
  messageInfo: string;
  code?: string;
  responseData?: T;
}
```

**1) `GET /api/v1/Sticker/CheckGroupCode?code=radish_girl`**

成功（可用）：

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "ok",
  "responseData": {
    "voAvailable": true,
    "voCode": "radish_girl"
  }
}
```

失败（已存在）：

```json
{
  "isSuccess": false,
  "statusCode": 409,
  "code": "CodeAlreadyExists",
  "messageInfo": "分组标识符已存在",
  "responseData": {
    "voAvailable": false,
    "voCode": "radish_girl"
  }
}
```

**2) `GET /api/v1/Sticker/CheckStickerCode?groupId=12&code=happy_face`**

成功（可用）：

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "ok",
  "responseData": {
    "voAvailable": true,
    "voGroupId": 12,
    "voCode": "happy_face"
  }
}
```

失败（组内重复）：

```json
{
  "isSuccess": false,
  "statusCode": 409,
  "code": "CodeAlreadyExists",
  "messageInfo": "该分组内表情标识符已存在",
  "responseData": {
    "voAvailable": false,
    "voGroupId": 12,
    "voCode": "happy_face"
  }
}
```

**3) `GET /api/v1/Sticker/NormalizeCode?filename=héllo!.png`**

成功：

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "ok",
  "responseData": {
    "voOriginalFileName": "héllo!.png",
    "voNormalizedCode": "hllo",
    "voIsChanged": true,
    "voChangeReasons": ["removed_non_ascii", "trim_input"]
  }
}
```

**4) `PUT /api/v1/Sticker/BatchUpdateSort`**

请求示例：

```json
{
  "items": [
    { "id": 10021, "sort": 10 },
    { "id": 10022, "sort": 20 },
    { "id": 10023, "sort": 30 }
  ]
}
```

成功响应：

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "排序已更新",
  "responseData": {
    "voUpdatedCount": 3
  }
}
```

前端处理建议：

- 预检接口用于**即时提示**，提交时仍以后端最终校验为准
- `NormalizeCode` 仅用于展示清洗结果，不替代前端输入校验
- `BatchUpdateSort` 失败时回滚本地排序并 toast 提示

---

## 参考文件

| 文件 | 参考用途 |
|------|----------|
| `Frontend/radish.console/src/pages/Users/` | Console 页面结构参考 |
| `Frontend/radish.console/src/pages/Products/` | 含图片上传的表单页面参考 |
| `Frontend/radish.ui/src/components/FileUpload/FileUpload.tsx` | 批量上传组件，支持多文件、进度、分片 |
| `Radish.Api/Controllers/AttachmentController.cs` | 图片上传接口，`POST /api/v1/Attachment/UploadImage` |
