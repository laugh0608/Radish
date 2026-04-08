# 论坛轻回应墙 Phase 1 设计

> 状态：联调收口中
>
> 最后更新：2026-04-08（Asia/Shanghai）
>
> 关联文档：
>
> - [当前进行中](/planning/current)
> - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
> - [论坛应用功能说明](/features/forum-features)
> - [前端设计文档](/frontend/design)

## 1. 背景

当前论坛帖子详情页已经具备：

- 帖子正文与帖子主互动区；
- 标准评论树与正式讨论输入链路；
- 帖子 / 评论 `Reaction`、举报、问答、投票、抽奖等既有互动能力。

但在“正文之后、正式评论之前”仍缺少一层低门槛、短反馈、弱打扰的轻互动层。

现有评论链路已经较重：

- 评论支持 Markdown、附件、图片、贴纸、`@提及`、两级树、点赞、Reaction、编辑历史；
- 评论模型和评论 UI 已经承担“正式讨论”的职责，不适合继续承接更轻、更快、更短的即时反馈。

因此，第二开发阶段的社区深化第一项，确定为在帖子详情页内新增独立的“轻回应墙 Phase 1”。

## 2. 当前代码事实

截至 `2026-04-08`，论坛轻回应墙的基础链路已经落地，仓库中的实际结构如下：

- 帖子轻回应后端独立链路已存在：
  - `Radish.Model/PostQuickReply.cs`
  - `Radish.Model/DtoModels/PostQuickReplyDto.cs`
  - `Radish.Model/ViewModels/PostQuickReplyVo.cs`
  - `Radish.Model/ViewModels/PostQuickReplyWallVo.cs`
  - `Radish.IService/IPostQuickReplyService.cs`
  - `Radish.Service/PostQuickReplyService.cs`
  - `Radish.Api/Controllers/PostQuickReplyController.cs`
- 轻回应治理与配置已接入：
  - `Radish.Api/appsettings.json` 中的 `ForumQuickReply`
  - `Radish.Service/ContentModerationService.cs`
  - `Frontend/radish.client/src/components/ContentReportModal.tsx`
- 前端帖子详情页已接入轻回应墙：
  - `Frontend/radish.client/src/apps/forum/views/PostDetailContentView.tsx`
  - `Frontend/radish.client/src/apps/forum/components/PostQuickReplyWall.tsx`
  - `Frontend/radish.client/src/apps/forum/hooks/useForumData.ts`
  - `Frontend/radish.client/src/apps/forum/hooks/useForumActions.ts`
  - `Frontend/radish.client/src/api/forum.ts`
  - `Frontend/radish.client/src/types/forum.ts`
  - `Frontend/radish.client/src/i18n.ts`

现有评论、Reaction 与举报相关链路仍是轻回应墙需要兼容但不复用的既有边界：

- 帖子详情主视图位于 `Frontend/radish.client/src/apps/forum/views/PostDetailContentView.tsx`
- 帖子正文与帖子主互动区位于 `Frontend/radish.client/src/apps/forum/components/PostDetail.tsx`
- 评论树位于 `Frontend/radish.client/src/apps/forum/components/CommentTree.tsx`
- 正式评论输入位于 `Frontend/radish.client/src/apps/forum/components/CreateCommentForm.tsx`
- 评论后端模型与接口位于：
  - `Radish.Model/Comment.cs`
  - `Radish.Model/DtoModels/CommentDto.cs`
  - `Radish.Api/Controllers/CommentController.cs`
  - `Radish.Service/CommentService.cs`
- 通用回应能力 `Reaction` 已独立建模，位于：
  - `Radish.Model/Reaction.cs`
  - `Radish.Api/Controllers/ReactionController.cs`
  - `Radish.Service/ReactionService.cs`
- 通用举报与治理链路已存在，位于：
  - `Radish.Model/ContentReport.cs`
  - `Radish.Api/Controllers/ContentModerationController.cs`
  - `Radish.Service/ContentModerationService.cs`
  - `Frontend/radish.client/src/components/ContentReportModal.tsx`

### 2.1 当前详情页结构判断

当前详情页已经不再停留在“是否适合插入轻回应墙”的判断阶段，而是已经形成稳定结构：

1. 帖子正文区和评论区已经分离，不需要推翻整个详情页。
2. 轻回应墙已经插入“帖子正文区之后、评论区之前”。
3. 当前真正需要继续收口的，不是根布局，而是联调细节和互动回流链路。

当前建议将帖子详情页的互动结构固定为：

1. 正文与帖子主互动区
2. 轻回应墙
3. 正式评论区

### 2.2 当前阶段判断

截至 `2026-04-08`，轻回应墙 `Phase 1` 的当前判断为：

- “独立建模 / 独立接口 / 独立治理边界 / 三段式插入位”已经成立；
- 当前不再讨论“是否继续复用 Comment / Reaction 临时拼装”；
- 当前主线重心已从“基础实现”转向“联调收口 + 最小回流链路补齐”；
- 其中最值得优先补齐的回流项，是“个人内容回看 -> 跳回帖子详情”的最小闭环；通知跳转继续保留为同批次预留项。
- 当前“我的轻回应”回看入口已作为最小回流链路落地到个人主页，通知跳转继续保留为同批次预留项。
- 论坛现有帖子 / 评论类通知当前已完成统一导航载荷收口，可稳定跳回帖子详情；轻回应专属通知仍保留为后续项。

## 3. 目标

- 为帖子详情页提供一层低门槛即时互动。
- 缩短用户从“想参与”到“留下痕迹”的路径。
- 在正文后形成热度氛围，但不侵入标准评论树。
- 从第一版开始就具备独立治理边界，而不是后补兜底。

## 4. 非目标

- 不替代标准评论、楼中楼、问答回答或帖子 `Reaction`。
- 不承担长文本讨论、结构化回复、历史编辑等职责。
- 不在每条评论下重复渲染轻回应入口。
- 不引入贴纸、表情包、附件、图片、Markdown、`@提及`。
- 不把轻回应并入评论树、评论计数、神评 / 沙发链路或抽奖参与统计。

## 5. 名称与用户文案

内部统一命名：`轻回应墙`

首版用户侧文案建议优先使用：`轻回应`

原因：

- “轻回应”最接近当前产品语义；
- “弹幕”容易让用户误解为全屏飞行弹幕；
- “路过留一句”更适合运营文案，不适合作为正式能力名。

## 6. 产品结构与交互

## 6.1 放置位置

放在帖子详情页的以下结构之间：

1. 帖子正文 / 标签 / 帖子主互动区之后
2. 标准评论树之前

这样可以明确区分三层语义：

- 正文：作者主表达
- 轻回应墙：围观用户的即时情绪和短反馈
- 评论区：正式讨论与回复

## 6.2 展示形态

- 使用 2 到 3 条轻量轨道承载胶囊形 `pill`。
- 真实内容先按轨道横向铺排：
  - 先填满第 1 排；
  - 再填第 2 排；
  - 再填第 3 排。
- 只有真实内容超过三排可视承载后，超宽轨道才进入缓慢漂移。
- 不复制同一条轻回应来制造“假滚动”，滚动内容必须来自真实数据。
- 每条轻回应包含：
  - 发送者头像
  - 昵称或昵称首字
  - 短内容文本
- 桌面端轨道允许缓慢漂移，形成弱弹幕感，但不能做全屏飞行弹幕。
- 默认展示最近一批内容，不引入分页入口。
- 移动端降级为横向滑动列表或紧凑堆叠列表。

## 6.3 发送形态

- 使用独立紧凑输入区，不复用正式评论编辑器。
- 输入区只支持：
  - 纯文本
  - 默认 Unicode emoji
- 明确不支持：
  - 贴纸
  - 自定义表情包
  - Markdown
  - 图片
  - 附件
  - `@提及`
- 未登录用户点击输入或发送时，沿用当前论坛登录引导。
- 发送成功后本地乐观插入，再与服务端返回结果对齐。

## 6.4 Emoji 边界

Phase 1 明确支持默认 emoji，但不支持其他表情包或贴纸。

原因：

- 轻回应墙需要保留“短、快、轻”的输入特征；
- 当前论坛已经有独立 `Reaction` 和贴纸体系，轻回应墙不应重复承担；
- 若首版同时开放贴纸和自定义表情包，会明显放大内容治理、展示对齐和移动端降级复杂度；
- Unicode emoji 已足够满足“即时情绪表达”的首版目标。

实现口径：

- 前端允许用户直接输入系统默认 emoji；
- 后端按普通文本存储，不为 emoji 单独拆字段；
- 不接入现有贴纸选择器，不接入 `ReactionBar`。

## 7. 与现有能力的边界

## 7.1 与评论的边界

轻回应不是评论，不复用 `Comment`：

- 不进入评论树；
- 不支持回复层级；
- 不支持评论点赞；
- 不支持评论 `Reaction`；
- 不支持评论编辑历史；
- 不计入 `Post.CommentCount`；
- 不参与神评 / 沙发逻辑；
- 不参与当前抽奖参与统计。

## 7.2 与 Reaction 的边界

轻回应不是 `Reaction`：

- `Reaction` 继续承载帖子 / 评论上的表情聚合回应；
- 轻回应承载短文本和 Unicode emoji 混合的即时短反馈；
- 不把短文本塞进 `Reaction` 模型。

## 7.3 与帖子详情页的边界

- 帖子正文区继续承载作者表达和帖子主互动；
- 轻回应墙只承载“围观即留一句”的轻反馈；
- 正式评论区继续承载完整讨论；
- 当前详情页中的正式讨论 CTA 应同步收口，避免结构上仍像“正文 -> 评论树 -> 再打开正式输入”。

## 8. 数据模型

Phase 1 新增独立模型：`PostQuickReply`

建议字段：

```text
PostQuickReply
- Id
- PostId
- AuthorId
- AuthorName
- Content
- NormalizedContent
- Status
- IsDeleted
- DeletedAt
- DeletedBy
- TenantId
- CreateTime
- CreateBy
- CreateId
- ModifyTime
- ModifyBy
- ModifyId
```

说明：

- `Status` 首版至少保留 `Visible / Hidden` 两态；
- `NormalizedContent` 用于重复发送拦截；
- `IsDeleted + DeletedAt + DeletedBy` 从第一版就齐全，不沿用评论的旧式软删口径；
- 头像不建议冗余存储到表中，沿用现有用户头像回填方式。

## 9. API 设计

首版新增 `PostQuickReplyController`，建议接口如下：

### 9.1 查询

- `GET /api/v1/PostQuickReply/GetRecentByPostId?postId={id}&take={n}`

返回：

- 最近可见轻回应列表
- 当前总数

首版默认：

- `take` 默认值走配置
- 仅返回 `Visible && !IsDeleted`
- 按 `CreateTime DESC`

### 9.2 创建

- `POST /api/v1/PostQuickReply/Create`

请求体：

```json
{
  "postId": 123,
  "content": "好耶🙂"
}
```

返回：

- 新建后的 `PostQuickReplyVo`

### 9.3 删除

- `DELETE /api/v1/PostQuickReply/Delete?quickReplyId={id}`

权限：

- 作者本人
- 管理员

行为：

- 软删除

### 9.4 举报

不单独开新举报控制器，直接扩展现有 `ContentModeration/Report` 的目标类型，增加：

- `PostQuickReply`

这样可直接复用：

- 提交举报
- 重复待处理举报拦截
- 自举报拦截
- 现有审核队列

## 10. 治理边界

这是 Phase 1 的正式范围，不后补。

## 10.1 内容长度

- 单条长度上限走集中配置，默认：`10`
- 前端显示剩余字数或当前字数
- 最终以后端校验为准

服务端在校验前应统一：

- `Trim`
- 折叠连续空白
- 将换行归一为单个空格

## 10.2 频率限制

- 单用户单帖冷却走集中配置，默认：`30 秒 / 1 条`

实现建议：

- 使用缓存做冷却键
- 不依赖前端节流作为唯一约束

## 10.3 重复发送限制

- 单用户同帖、相同 `NormalizedContent` 的重复发送窗口走集中配置，默认：`5 分钟内禁止重复发送`

目的：

- 避免机械刷屏
- 避免用户通过微小时间间隔绕过冷却

## 10.4 删除治理

- 作者本人可删除自己的轻回应
- 管理员可删除任意轻回应
- 删除使用软删除
- 普通读取链路不返回已删除项

## 10.5 举报与最小审核预留

- 轻回应首版就支持举报
- 首版不单独做新审核台 UI
- 服务端保留 `Status` 字段，为后续“隐藏但不删”预留空间

## 11. 前端实现建议

建议新增组件：

- `PostQuickReplyWall`
- `PostQuickReplyComposer`

首版不建议继续拆出更复杂的滚动引擎组件，避免一开始把结构做重。

### 11.1 状态策略

- 进入帖子详情时加载最近轻回应
- 发送时本地乐观插入
- 删除后本地移除并与服务端同步
- 举报复用当前通用举报弹窗

### 11.2 样式口径

- 维持论坛当前浅色、轻卡片风格
- 墙体边界要清楚，但不能比评论区更重
- 视觉重点是“轻”和“快”，不是“热闹到失控”
- 不引入贴纸面板、表情包入口或复杂悬浮工具条

## 12. 后端实现建议

- 新增独立 Service / Controller，不挂到 `CommentService`
- 独立接口、独立 DTO、独立 Vo
- 配置集中收口到 `ForumQuickReply` 配置段，不在服务中硬编码长度、查询条数和限频窗口
- 独立治理校验：
  - 长度限制
  - 冷却限制
  - 重复内容限制
  - 删除权限
- 复用现有治理能力：
  - 发布权限校验
  - 举报队列
  - 管理端审核

## 13. 已落地范围与当前后续范围

### 13.1 已落地范围

前端已落地：

- `Frontend/radish.client/src/apps/forum/views/PostDetailContentView.tsx`
- `Frontend/radish.client/src/apps/forum/views/PostDetailContentView.module.css`
- `Frontend/radish.client/src/apps/forum/hooks/useForumData.ts`
- `Frontend/radish.client/src/apps/forum/hooks/useForumActions.ts`
- `Frontend/radish.client/src/api/forum.ts`
- `Frontend/radish.client/src/types/forum.ts`
- `Frontend/radish.client/src/api/contentModeration.ts`
- `Frontend/radish.client/src/components/ContentReportModal.tsx`
- `Frontend/radish.client/src/i18n.ts`
- `Frontend/radish.client/src/apps/forum/components/PostQuickReplyWall.tsx`
- `Frontend/radish.client/src/apps/forum/components/PostQuickReplyWall.module.css`

后端已落地：

- `Radish.Model/PostQuickReply.cs`
- `Radish.Model/ViewModels/PostQuickReplyVo.cs`
- `Radish.Model/ViewModels/PostQuickReplyWallVo.cs`
- `Radish.Model/DtoModels/PostQuickReplyDto.cs`
- `Radish.IService/IPostQuickReplyService.cs`
- `Radish.Service/PostQuickReplyService.cs`
- `Radish.Api/Controllers/PostQuickReplyController.cs`
- `Radish.Extension/AutoMapperExtension/CustomProfiles/ForumProfile.cs`
- `Radish.Service/ContentModerationService.cs`
- `Radish.Api/Controllers/ContentModerationController.cs`
- `Radish.Model/DtoModels/ContentModerationDto.cs`
- `Radish.Shared/CustomEnum/ContentModerationEnums.cs`

### 13.2 当前后续范围

当前批次的后续重点不再是继续证明“轻回应墙能否独立成立”，而是：

1. 联调收口：
   - 空态、加载态、报错态、登录引导、删除与举报链路是否一致
   - 少量内容静态铺排、超宽轨道才漂移的视觉表现是否稳定
   - 移动端降级形态是否自然，不把桌面交互硬搬到小屏
2. 最小回流链路：
   - 优先补齐“个人内容回看 -> 跳回帖子详情”
   - 为轻回应相关通知跳转保留最小接口与路由边界，但不立即扩张成完整通知体系改造
3. 验收与留痕：
   - 把当前已落地的基础链路从“实现完成”推进到“主线可验收”

## 14. 最小验证方案

开发批次最小验证建议：

- `dotnet build Radish.slnx -c Debug`
- `npm run build --workspace=radish.client`

接口烟雾验证至少覆盖：

1. 获取帖子轻回应列表
2. 创建轻回应
3. 配置化冷却拦截
4. 配置化重复发送拦截
5. 作者删除轻回应
6. 举报轻回应成功

联调时额外确认：

- 详情页结构是否稳定为“正文 -> 轻回应墙 -> 评论区”
- 轻回应是否未污染评论树和评论统计
- 默认 emoji 输入是否正常展示
- 贴纸 / 表情包入口是否未暴露
- 少量内容是否静态铺排，只有实际超宽轨道才开始漂移
- 删除 / 举报 / 登录引导链路是否与论坛其他轻交互能力一致
- 若补“个人内容回看”或通知跳转，还应额外确认是否能稳定回到对应帖子详情

## 15. 当前决定

截至 `2026-04-08`，论坛轻回应墙 Phase 1 的正式口径固定为：

- 独立数据模型
- 独立接口
- 独立治理边界
- 放在帖子正文之后、评论区之前
- 支持纯文本和默认 Unicode emoji
- 不支持贴纸、表情包、附件、Markdown、`@提及`
- 基础链路已落地，当前执行重点转向联调收口与最小回流链路补齐

后续若要支持贴纸型轻回应，应进入后续阶段单独评估，不并入当前 Phase 1。
