# 论坛帖子/评论编辑历史设计与实现

> Radish 论坛编辑历史能力专题文档
>
> **版本**: v26.1.1
>
> **最后更新**: 2026.06.20

---

## 概述

论坛编辑历史用于解决“内容被多次修改后不可追溯”的问题，支持：

- **帖子编辑历史**：记录每次编辑前后标题与内容。
- **评论编辑历史**：记录每次编辑前后内容。
- **配置化限制**：通过后端配置控制编辑次数上限、历史写入次数、历史保留上限。
- **管理员覆盖**：普通用户达到上限后禁止继续编辑，管理员可按配置绕过。

该能力首期仅提供“记录 + 查询”，**不提供版本回滚**。

`2026-06-20` 补充：论坛发布、评论、回答和编辑重试语义已进入 [论坛内容发布可靠性与编辑历史治理](/guide/forum-content-write-reliability-governance)。本页继续作为 `PostEditHistory` / `CommentEditHistory` 的功能说明；提交意图去重、短窗口内容指纹和频率限制以治理专题为准。

---

## 核心规则

### 1. 普通用户编辑权限

- 帖子：作者本人可编辑，受“最大编辑次数”限制。
- 评论：作者本人可编辑，受“编辑时间窗口 + 最大编辑次数”限制。

### 2. 管理员编辑权限

- 角色为 `Admin` 或 `System` 的用户可编辑他人帖子/评论。
- 是否绕过次数与时间窗口，由 `ForumEditHistory.AdminOverride` 决定。

### 3. 历史写入与裁剪

- 每次成功编辑后，按配置决定是否写入历史。
- 单条帖子/评论历史超过上限时，删除最旧记录，仅保留最近 N 条。
- 编辑前后内容没有变化时直接返回业务提示，不写历史，不递增 `EditCount`。
- 同一 `ClientSubmissionId` 的成功编辑重试返回既有编辑结果，不写第二条历史。
- 同一 `ClientSubmissionId` 但请求摘要不同视为冲突，不覆盖内容。

---

## 配置设计

配置文件：`Radish.Api/appsettings.json`

```json
"ForumEditHistory": {
  "Enable": true,
  "Post": {
    "EnableHistory": true,
    "HistorySaveEditCount": 10,
    "MaxEditCount": 20,
    "MaxHistoryRecords": 20
  },
  "Comment": {
    "EnableHistory": true,
    "HistorySaveEditCount": 5,
    "MaxEditCount": 10,
    "MaxHistoryRecords": 10,
    "EditWindowMinutes": 5
  },
  "AdminOverride": {
    "BypassEditCountLimit": true,
    "BypassCommentEditWindow": true
  }
}
```

字段说明：

- `Enable`：总开关。
- `HistorySaveEditCount`：最多前 N 次编辑写入历史（超过后仍可编辑，但不新增历史）。
- `MaxEditCount`：普通用户最多可编辑次数。
- `MaxHistoryRecords`：单条内容最多保留历史条数。
- `EditWindowMinutes`：评论编辑时间窗（普通用户）。

---

## 数据模型

### PostEditHistory

位置：`Radish.Model/PostEditHistory.cs`

关键字段：

- `PostId`：帖子 Id
- `EditSequence`：编辑序号
- `OldTitle` / `NewTitle`
- `OldContent` / `NewContent`
- `EditorId` / `EditorName`
- `EditedAt`
- `TenantId`

### CommentEditHistory

位置：`Radish.Model/CommentEditHistory.cs`

关键字段：

- `CommentId`：评论 Id
- `PostId`：冗余帖子 Id
- `EditSequence`
- `OldContent` / `NewContent`
- `EditorId` / `EditorName`
- `EditedAt`
- `TenantId`

### 编辑次数统计

- `Post.EditCount` 与 `Comment.EditCount` 用于独立统计总编辑次数。
- `MaxEditCount` 校验优先依赖 `EditCount`，不依赖历史条数，避免“历史写入次数较小导致次数失真”。

### 数据初始化

新增历史表与编辑计数字段后，请执行：

```bash
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- init
```

---

## 后端接口

### 1) 获取帖子编辑历史

- `GET /api/v1/Post/GetEditHistory?postId={id}&pageIndex=1&pageSize=10`
- 返回：`VoPagedResult<PostEditHistoryVo>`

### 2) 获取评论编辑历史

- `GET /api/v1/Comment/GetEditHistory?commentId={id}&pageIndex=1&pageSize=10`
- 返回：`VoPagedResult<CommentEditHistoryVo>`

### 3) 编辑接口行为变化

- `PUT /api/v1/Post/Update`：支持管理员编辑他人帖子；新增次数上限校验与历史记录；请求体可携带 `ClientSubmissionId` 表示同一次编辑提交意图。
- `PUT /api/v1/Comment/Update`：支持管理员编辑他人评论；时间窗和次数按配置校验并记录历史；请求体可携带 `ClientSubmissionId` 表示同一次编辑提交意图。
- `ClientSubmissionId` 不替代权限、时间窗或编辑次数校验；它只用于网络失败、超时或用户重试时识别同一次编辑请求。

---

## 前端接入

客户端：`Frontend/radish.client`

- 帖子详情新增“历史”按钮，弹窗分页查看帖子编辑历史。
- 评论节点新增“历史”按钮，弹窗分页查看评论编辑历史。
- 展示内容：
  - 帖子：编辑前后标题 + 编辑前后正文。
  - 评论：编辑前后正文。

---

## 测试建议

### API 场景

1. 普通用户在 `MaxEditCount` 内可编辑，超过后被拒绝。
2. 管理员在超限后仍可编辑（开启覆盖时）。
3. 评论超过 `EditWindowMinutes`：普通用户失败，管理员可通过（开启覆盖时）。
4. 历史写入次数超过 `HistorySaveEditCount` 后不再新增历史。
5. 历史记录超过 `MaxHistoryRecords` 后自动裁剪。
6. 帖子 / 评论编辑无变化时不写历史、不递增编辑次数。
7. 同一 `ClientSubmissionId` 成功重试不新增历史，同 key 不同请求摘要返回冲突。

### 联调文件

- `Radish.Api.Tests/HttpTest/Radish.Api.Forum.Core.http`
- `Radish.Api.Tests/HttpTest/Radish.Api.Forum.Comment.http`

---

## 已知边界

- 首期不支持“回滚历史版本”。
- 目前无独立后台可视化配置页，配置依赖 `appsettings`。
- 历史内容为完整快照，内容较大时应关注存储增长。
