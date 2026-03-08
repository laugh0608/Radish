# 表情包系统 Phase 2（Reaction）设计冻结稿

> Radish 表情包系统 Reaction 阶段设计冻结文档（用于直接指导开发与验收）
>
> **版本**: v26.3.0
>
> **最后更新**: 2026.03.01
>
> **关联文档**：
> [表情包与 Reaction 系统设计](./emoji-sticker-system.md) ·
> [UI 视觉与交互规范](./emoji-sticker-ui-spec.md) ·
> [Console 管理后台实现](./emoji-sticker-console.md)

---

## 1. 范围与目标

### 1.1 In Scope（本阶段必须完成）

- Post/Comment 的 Reaction 功能闭环（Unicode + Sticker）
- `ReactionBar` 组件落地（帖子与评论统一）
- `StickerPicker` 的 `reaction` 模式接入
- 后端接口闭环：`GetSummary` / `BatchGetSummary` / `Toggle`
- 前端乐观更新、失败回滚、并发防抖
- 基础回归：交互、移动端、无障碍、批量场景

### 1.2 Out of Scope（本阶段不做）

- ChatMessage Reaction（Phase 3）
- 表情商店（Premium 购买链路，Phase 3）
- 个性化常用表情配置

### 1.3 阶段目标（M11）

- Forum 帖子与评论均支持稳定 Reaction，且与现有 `sticker://` 渲染链路无冲突
- 评论树场景可通过批量接口拉取回应汇总，无明显重复请求与卡顿
- 关键接口具备错误码、限流与日志观测能力

---

## 2. 术语与数据契约

### 2.1 枚举与约束

- `TargetType`: `Post` | `Comment` | `ChatMessage`
- `EmojiType`: `unicode` | `sticker`
- `EmojiValue`:
  - `unicode`: 直接字符，如 `😀`
  - `sticker`: `groupCode/stickerCode`，如 `radish_girl/happy`

### 2.2 ReactionSummaryVo（冻结）

```csharp
public class ReactionSummaryVo
{
    public string VoEmojiType { get; set; } = string.Empty;      // unicode / sticker
    public string VoEmojiValue { get; set; } = string.Empty;     // 😀 / radish_girl/happy
    public int VoCount { get; set; }                             // 聚合计数
    public bool VoIsReacted { get; set; }                        // 当前登录用户是否已回应
    public string? VoThumbnailUrl { get; set; }                  // sticker 缩略图
}
```

约束：

- `VoCount <= 0` 的项不返回给前端
- `VoThumbnailUrl` 仅在 `VoEmojiType=sticker` 时有意义

---

## 3. API 合同冻结

统一响应壳沿用 `MessageModel<T>`，前端使用 `@radish/http` 解析为 `ParsedApiResponse<T>`。

### 3.1 GET `/api/v1/Reaction/GetSummary`

用途：单目标（帖子或评论）获取回应汇总。

请求参数：

- `targetType`: `Post | Comment | ChatMessage`
- `targetId`: `long`

响应体：

- `responseData: ReactionSummaryVo[]`

规则：

- 匿名访问时 `VoIsReacted=false`
- 默认按 `VoCount desc, VoEmojiValue asc` 返回

### 3.2 POST `/api/v1/Reaction/BatchGetSummary`

用途：评论树/列表批量获取回应汇总。

请求体：

```json
{
  "targetType": "Comment",
  "targetIds": [101, 102, 103]
}
```

约束：

- 单次 `targetIds` 最多 100 条，超出返回 `BatchSizeExceeded`

响应体：

- `responseData: Record<string, ReactionSummaryVo[]>`
- key 为 `targetId` 字符串

### 3.3 POST `/api/v1/Reaction/Toggle`

用途：添加/取消同一表情回应（软删除语义）。

请求体：

```json
{
  "targetType": "Post",
  "targetId": 9527,
  "emojiType": "sticker",
  "emojiValue": "radish_girl/happy"
}
```

响应体：

- `responseData: ReactionSummaryVo[]`（目标对象最新完整汇总）

### 3.4 错误码冻结

| 场景 | HTTP | Code |
|------|------|------|
| 未登录调用 Toggle | 401 | `AuthRequired` |
| 参数不合法 | 400 | `InvalidArgument` |
| Sticker 不存在/禁用 | 400/404 | `StickerNotAvailable` |
| 单用户超 10 种上限 | 400 | `ReactionLimitExceeded` |
| 批量查询超过 100 | 400 | `BatchSizeExceeded` |
| 并发冲突重试后失败 | 409 | `ConcurrentConflict` |

---

## 4. 服务端状态机与并发策略

### 4.1 Toggle 状态机

唯一键：`(UserId, TargetType, TargetId, EmojiValue)`。

状态转换：

1. 不存在：新建 `Reaction(IsDeleted=false)`
2. 存在且 `IsDeleted=false`：软删除（视为取消）
3. 存在且 `IsDeleted=true`：恢复（视为重新添加）

### 4.2 事务与重试

- `Toggle` 全流程在单事务中执行
- 遇到唯一键冲突时短重试 1 次
- 二次失败返回 `409 ConcurrentConflict`

### 4.3 服务端校验

- `targetType` 白名单校验
- `targetId` 必须存在且可见
- `emojiType=sticker` 时，反查 `Sticker` 且必须 `IsEnabled=true`
- 单用户同目标最多 10 种 `EmojiValue`

---

## 5. 前端交互状态机（冻结）

### 5.1 ReactionBar 组件状态

- `idle`：可点击
- `loading`：按钮短暂禁用，防止重复点击
- `success`：使用服务端返回汇总替换本地
- `error`：回滚到操作前状态并提示 toast

### 5.2 乐观更新规则

- 点击已选气泡：本地 `count - 1`，`isReacted=false`
- 点击未选气泡：本地 `count + 1`，`isReacted=true`
- 若 `count` 变成 0，本地临时隐藏该气泡

### 5.3 失败回滚规则

- 任一失败（401/409/5xx）均回滚操作前快照
- 401 统一走登录跳转或登录提示

### 5.4 Picker 模式规则

- `mode='insert'`：`AllowInline=false` 禁选
- `mode='reaction'`：忽略 `AllowInline`，都可选

---

## 6. Forum 集成时序（冻结）

### 6.1 PostDetail

1. 帖子详情加载完成
2. 并行请求 `GetSummary(targetType=Post,targetId=postId)`
3. 渲染 `ReactionBar(Post, postId)`

### 6.2 CommentTree

1. 评论列表首屏返回后，收集可见 `commentIds`
2. 调用一次 `BatchGetSummary(Comment, ids)`
3. 分发至每个 `CommentNode` 的 `ReactionBar`
4. 分页加载更多评论时，只增量请求新增 ID

### 6.3 数据刷新策略

- `Toggle` 成功后直接用返回值覆盖本目标 summary，不追加二次 `GetSummary`
- 新开页面/刷新页面时重新拉取

---

## 7. 性能、限流与降级

### 7.1 限流建议（服务端）

- `POST /Reaction/Toggle`：`UserId + TargetId`，20 次/分钟
- `POST /Reaction/BatchGetSummary`：`IP`，30 次/分钟
- `POST /Sticker/RecordUse`：`UserId + IP`，60 次/分钟

### 7.2 前端降级策略

- 批量接口失败：保留主体内容，`ReactionBar` 显示“加载失败，点击重试”
- 评论树超深时分批请求（每批 <= 100）

### 7.3 性能目标

- `GetSummary` P95 <= 120ms
- `BatchGetSummary(100)` P95 <= 200ms
- `Toggle` P95 <= 150ms

---

## 8. 可观测性与日志

### 8.1 日志字段

- `TraceId`
- `UserId`
- `TargetType`
- `TargetId`
- `EmojiType`
- `EmojiValue`
- `Result`（success/failed/conflict/limited）
- `ElapsedMs`

### 8.2 关键指标

- Toggle 成功率
- Toggle 并发冲突率（409 比例）
- 限流命中率（429 比例）
- 三个核心接口 P95

---

## 9. 测试与验收清单

### 9.1 后端

- Toggle 三态切换（新建/取消/恢复）单测
- 并发冲突与重试单测
- `ReactionLimitExceeded` 单测
- `BatchGetSummary` 上限单测（100/101）

### 9.2 前端

- 气泡点击乐观更新与失败回滚
- `StickerPicker` reaction 模式选择
- 评论树批量加载与增量分页加载
- 移动端 `+` 点击展开与收起

### 9.3 验收标准（通过即可上线）

- 帖子/评论都能稳定显示与切换 Reaction
- 无明显重复请求风暴
- 失败场景可回滚且无状态错乱
- `radish.client` type-check 通过
- `Radish.Api.Tests` 通过

---

## 10. 发布与回滚

### 10.1 发布顺序

1. 后端先发（接口与数据层）
2. 前端开启 Reaction UI（同版本或后续版本）

### 10.2 回滚策略

- 前端可先隐藏 `ReactionBar` 开关（不影响正文 `sticker://`）
- 后端 `Toggle` 异常时不影响现有帖子/评论主链路

---

## 11. 实施任务拆分（可直接开工）

### 11.1 后端（BE）

- `W11-BE-1`：Reaction 实体与索引确认（唯一键 + 软删除策略）
- `W11-BE-2`：`ReactionService` 完成 `GetSummary/BatchGetSummary/Toggle`
- `W11-BE-3`：`ReactionController` 与错误码标准化
- `W11-BE-4`：单测与 HTTP 联调文件补齐

### 11.2 前端（FE）

- `W11-FE-1`：`@radish/ui` 完成 `ReactionBar` 组件
- `W11-FE-2`：`StickerPicker` 完成 `reaction` 模式
- `W11-FE-3`：Forum 接入（PostDetail + CommentNode）
- `W11-FE-4`：`useReactions` Hook（乐观更新 + 回滚）

### 11.3 测试与文档（QA/Docs）

- `W11-QA-1`：回归清单执行（桌面/移动/无障碍）
- `W11-DOC-1`：同步更新 `development-plan` 与 `2026-03` 周志

