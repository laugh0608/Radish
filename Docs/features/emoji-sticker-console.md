# 表情包系统 - Console 管理后台实现

> Radish 表情包系统 Console 管理后台详细设计
>
> **版本**: v26.3.0
>
> **最后更新**: 2026.07.16
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

## 当前实现状态（2026-07-16）

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
- API 封装：`Frontend/radish.console/src/api/stickerApi.ts`（当前仍自行解析部分响应并抛出普通 `Error`，统一错误迁移见 F3-C9）

### 当前设计 / 视觉状态

- 网格模式与拖拽排序（当前为列表模式 + 数字排序）
- `StickerGroupList` 与 `StickerList` 已保留表格 CRUD 功能闭环，但尚未迁入 `P3-12-D14` 新增的 `ConsolePageHeader / ConsoleMetricGrid / ConsoleToolbar` 语义组件。
- 下一批视觉迁移前应先判断页面边界：如果只迁移页头、指标和筛选工具条，可按普通表格 CRUD 收尾；如果要调整图片预览、分组详情、批量上传确认表、批量排序或素材状态流，应按媒体资产列表与分组详情拆分。

### 与 Reaction Phase 2 的协作边界（2026-03-01）

- Console 本阶段无需直接实现 `ReactionBar`，但需确保 Sticker 元数据可稳定支撑 Reaction 场景：
  - `IsEnabled` 状态准确
  - `VoThumbnailUrl` 可稳定解析
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
| 封面图（CoverAttachmentId） | 图片上传 | 可选，≤ 5MB；JPEG/PNG/GIF/BMP/WebP/ICO | 上传后回填字符串附件 ID，并通过 `voCoverImageUrl` 预览，显示于 StickerPicker Tab |
| 是否启用（IsEnabled） | 开关 | 默认开启 | |

### Code 字段行为

- 新建时：用户输入名称后，自动将名称拼音化（可选，不强制）或由用户手填；输入时实时显示合法性提示
- 编辑时：Code 字段显示为只读文本（灰色），下方提示"标识符创建后不可修改"
- 提交前调用后端唯一性验证：`GET /api/v1/Sticker/CheckGroupCode?code=xxx`

### 封面图上传

当前由 `StickerGroupForm` 使用 Ant Design `Upload` 的 `customRequest` 接管上传，通过共享
`attachmentImageAccept / isSupportedAttachmentImageFile` 校验 JPEG、PNG、GIF、BMP、WebP、ICO，
再调用 `uploadAttachmentImage(file, { businessType: 'StickerCover' })`。页面限制为 5MB；上传成功后回填字符串附件 ID，并展示封面预览。

---

## StickerList - 分组内表情管理

### 布局说明

页面分为两个区域：

1. **顶部**：返回分组、搜索、刷新、"批量上传"按钮、"单个添加"按钮和排序保存动作
2. **主体**：表情列表表格，展示预览、名称、Code、动画类型、内嵌状态、启用状态、使用次数、排序和操作
3. **弹层**：单个表情创建 / 编辑弹窗与批量上传弹窗

### 表情网格/列表展示

**当前实现：列表模式**

- 表格列：预览图、名称、Code、是否 GIF、AllowInline、UseCount、状态、排序、操作。
- 排序通过数字输入修改草稿，再由“保存排序”批量提交。
- 当前不提供网格模式、拖拽排序或 hover 播放 GIF 的独立视图；这些属于后续媒体资产体验增强，不是 D20 迁移前置条件。

**后续可评估：网格模式**

- 每格显示缩略图、Code、状态和操作图标。
- GIF 可考虑 hover 播放动图。
- 若新增网格模式，应先确认批量上传、排序、启停、删除和移动端列表密度是否仍可被稳定承载。

### 批量上传流程

#### 第一步：选择文件

点击"批量上传"按钮，弹出上传区域（Modal 或页内展开区域）：

```
┌─────────────────────────────────────────┐
│  拖拽文件到此处，或点击选择文件            │
│                                         │
│  支持：JPEG / PNG / GIF / BMP / WebP / ICO│
│  单文件 ≤ 5MB                            │
│  可同时选择多个文件                       │
└─────────────────────────────────────────┘
```

文件选中后展示待上传列表（文件名、大小、格式图标、移除按钮），点击"开始上传"。

#### 第二步：逐文件上传（带进度）

通过 `Frontend/radish.console/src/api/attachmentApi.ts` 的 `uploadAttachmentImage()` 调用 `POST /api/v1/Attachment/UploadImage`，使用 `Sticker` 业务类型并发上传（最大并发 3 个）；页面不得自行复制 XHR、认证、语言或结构化错误解析：

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
  groupId: string;
  stickers: Array<{
    attachmentId: string;   // 第二步上传得到的雪花 ID
    code: string;
    name: string;
    allowInline: boolean;
  }>;
}
```

后端处理逻辑：
1. 校验请求、Code 格式及组内/批次内唯一性
2. 在写入前逐项校验附件并构建实体；失败项汇总到 `voFailedItems`，本批次不插入任何 `Sticker`
3. 缩略图生成是 best-effort：失败会记录日志并继续，不会单独判定整批失败
4. 所有实体构建成功后写入数据库并失效 `sticker:groups:{tenantId}` 缓存

**Code 冲突处理**：若存在冲突，接口通过 `voConflicts` 返回 `voRowIndex / voCode / voMessage`，前端高亮对应行，不关闭表格，管理员修改后重新提交。

#### 第四步补充：事务边界与失败处理

`BatchAddStickers` 当前由事务特性包裹，Attachment 上传在调用该接口前已经完成。

失败处理策略：

- 请求/冲突校验或附件验证、实体构建失败时，在插入前返回 `voConflicts / voFailedItems`，不产生部分成功 `Sticker`
- 缩略图生成失败只记录告警并继续；数据库写入抛出异常时才由事务回滚已执行的写入
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

- 当前列表通过数字输入修改 Sort 草稿，再由“保存排序”调用 `PUT /api/v1/Sticker/BatchUpdateSort`
- 网格与拖拽排序尚未实现，后续若引入需继续保留可审阅的批量保存边界

**分组排序**：当前通过编辑分组的 Sort 数值保存，不提供拖拽。

---


## 与 Console 权限治理对齐（2026-03）

当前表情包后台已纳入 Console 权限治理 V1，口径如下：

- 页面访问：`console.stickers.view`
- 分组创建 / 单个表情新增：`console.stickers.create`
- 分组编辑 / 表情编辑：`console.stickers.edit`
- 分组启停：复用 `console.stickers.toggle`
- 删除：`console.stickers.delete`
- 排序：`console.stickers.sort`
- 批量上传：`console.stickers.batch-upload`

本轮已补齐并纳入资源映射与默认角色种子的辅助接口：

- `GET /api/v1/Sticker/CheckGroupCode`
- `GET /api/v1/Sticker/CheckStickerCode`
- `GET /api/v1/Sticker/NormalizeCode`

说明：

- 上述三项属于 Console 已真实使用的辅助接口，因此已进入 `ConsolePermissions + DbMigrate` 的治理范围。
- `Attachment/UploadImage` 继续保持共享上传接口定位，但已按方案 B 做最小收口：仅 `Sticker` / `StickerCover` 业务类型在后端复用现有 `console.stickers.*` 权限校验，不扩张为独立上传权限模型。

## BatchAddStickers 接口约束

| 约束项 | 限制值 | 说明 |
|--------|--------|------|
| 单次批量上传文件数 | ≤ 50 | 前端文件选择时限制；超出时提示分批上传 |
| 单次 BatchAddStickers 请求表情数 | ≤ 50 | 与上传文件数一致 |
| 并发上传数 | 3 | 前端控制，防止同时大量请求 |

### 当前错误与联调契约

`StickerController` 当前未标注 `[ApiErrorContract]`。因此业务校验、冲突和附件构建失败通常仍以 HTTP 200
返回，真实业务状态位于 `MessageModel.statusCode`；认证和权限拦截仍使用真实 HTTP 401/403。Console 的
`stickerApi.ts` 仍有自行解析响应、抛普通 `Error` 的兼容实现，F3-C9 需迁移到共享
`ParsedApiResponse / ApiResponseError`，并同步把 Sticker 领域失败切换为真实 HTTP 状态。

`MessageModel<T>` 已支持 `messageKey / messageArguments / traceId`，但 Sticker 领域当前通常未填充这些字段；
F3-C9 应补齐稳定 `code`、本地化键、参数与追踪标识，前端不得依赖中文 `messageInfo` 分支。

批量请求中的雪花 ID 必须使用 JSON 字符串：

```json
{
  "groupId": "12",
  "stickers": [
    {
      "attachmentId": "9001",
      "code": "happy_face",
      "name": "开心",
      "allowInline": true
    }
  ]
}
```

前端按 `voConflicts / voFailedItems` 的 `voRowIndex` 回填行级提示。`voGroupId`、`voStickerIds[]`、
`voAttachmentId` 等 LongId 响应字段同样是字符串；缩略图 best-effort 失败不会生成 `voFailedItems`，
该列表表示附件校验或实体构建失败。

### 孤儿 Attachment 清理策略（补充）

清理候选是创建超过 24 小时、`BusinessId == null` 的全部未绑定附件，不局限于 Sticker 上传目录或业务类型。

- **产生场景**：管理员在确认表格删除行、取消保存，或 `BatchAddStickers` 返回失败/发生异常
- **清理方式**：`FileCleanupJob` 调用跨域 `AttachmentReferenceInspector`，排除仍被结构化字段、消息快照、正文 Markdown 等业务真值引用的附件
- **延迟**：仅清理创建时间超过 `24h` 的候选，避免管理员短时间重试时文件丢失
- **日志**：记录被引用候选的跳过数、清理总数及逐文件路径

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

### 管理端联调口径

- 预检接口只提供即时提示，提交时仍以后端最终校验为准。
- `NormalizeCode` 仅展示清洗结果，不替代前端输入校验。
- `groupId`、路由 `{id}`、排序请求 `items[].id` 及所有 LongId 响应字段均按字符串传输。
- `BatchUpdateSort` 失败时回滚本地排序草稿并提示用户。

---

## 参考文件

| 文件 | 参考用途 |
|------|----------|
| `Frontend/radish.console/src/pages/Users/` | Console 页面结构参考 |
| `Frontend/radish.console/src/pages/Products/` | 含图片上传的表单页面参考 |
| `Frontend/radish.console/src/pages/Stickers/StickerGroupForm.tsx` | Ant `Upload.customRequest` 封面上传实现 |
| `Frontend/radish.console/src/api/attachmentApi.ts` | Console 共享图片上传适配器 |
| `Radish.Api/Controllers/AttachmentController.cs` | 图片上传接口，`POST /api/v1/Attachment/UploadImage` |
