# 论坛投票 MVP 设计方案

> 面向 **P3 首个候选功能：投票最小首版** 的实现方案。
>
> **版本**: v26.3.0
> **最后更新**: 2026.03.13
>
> 关联文档：
> [开发路线图](/development-plan) ·
> [当前进行中](/planning/current) ·
> [论坛应用功能说明](./forum-features.md) ·
> [社区主线验收清单](./community-m12-p0-acceptance.md)

---

## 当前实施进度

### 截至 2026-03-12 已完成

- 方案已确认采用“论坛帖子附带投票”路径，不拆独立投票 App。
- 后端第 1 步已完成：`PostPoll`、`PostPollOption`、`PostPollVote` 及相关 DTO / Vo 已落地。
- 后端第 2 步已完成：发帖接口已支持附带投票创建，帖子详情与列表已补最小投票摘要字段。
- 当前实现已通过 `Radish.Api` 构建验证，可作为后续接口与前端接入基线。

### 下一步

- 第 5 步：继续补更完整回归测试，覆盖发帖附带投票、重复投票拦截、截止态拦截。

### 截至 2026-03-13 新增完成

- 第 3 步已完成：`PollController`、投票提交接口、按帖子查询投票结果接口已落地。
- 第 4 步已基本完成：欢迎 App 中论坛发帖弹窗已支持附带投票，帖子详情页已支持投票提交与结果展示。
- 最小后端回归测试已补充，覆盖首次投票成功、重复投票拦截、截止态拦截与非法选项拦截。
- `radish.client` 构建已通过，之前阻塞的 `PostDetail.tsx` 字段类型缺口已一并收口。
- 第 6 步已完成：帖子列表投票摘要已改为按页批量回填分类 / 标签 / 投票轻量摘要，不再逐条调用详情接口。
- 第 5 步持续推进：已补“发帖附带投票成功创建”“列表轻量 / 详情完整契约”“无效投票参数返回 400”回归。
- `Radish.Api.Forum.http` 已补论坛投票人工验收顺序，可串联“发帖附带投票 -> 列表识别 -> 详情查看 -> 提交投票 -> 重复投票拦截 -> 截止态拦截”。
- 当前下一步聚焦第 5 步：继续补更完整回归测试，并完成 MVP 验收收口。

---

## 目标

在现有论坛帖子主链路上增加一套**最小可用的投票能力**，满足以下目标：

- 用户可在发帖时附带一个单选投票。
- 用户可在帖子详情页查看投票、参与投票、查看结果。
- 帖子列表可识别“投票帖”，但不引入复杂交互。
- 尽量复用现有论坛发布、详情、权限与多租户链路，不新开独立 App。

本轮目标是“**小而快的闭环能力**”，不是一次性做完完整活动系统。

---

## 为什么先做投票

在 `抽奖 / 投票 / 问答` 三个候选方向中，投票最适合作为 `P3` 首个 MVP：

- **复用论坛主链路最多**：可直接挂在帖子上，复用发帖、详情、列表、评论与权限体系。
- **对现有模型冲击最小**：相比问答，不需要引入“问题 / 回答”双层内容模型。
- **业务风险最低**：相比抽奖，不涉及开奖公平性、奖品发放、审计与防刷。
- **更容易验收**：发帖、投票、查看结果三步即可形成闭环。

因此，本方案默认采用“**论坛帖子附带投票**”而非“独立投票应用”。

---

## MVP 范围

### 本轮包含

- 单选投票
- 发帖时附带投票
- 2 到 6 个投票选项
- 可选截止时间
- 帖子详情页投票与结果展示
- 已登录用户单人单票
- 帖子列表页显示投票标识与总票数

### 本轮不包含

- 多选投票
- 匿名投票
- 撤票 / 改票
- 投票评论区统计联动
- 通知系统联动
- 奖励 / 经验 / 萝卜币联动
- Console 投票管理页
- 独立投票 App

---

## 产品形态

### 位置选择

投票不做成独立应用，直接挂在论坛帖子中：

- **发布入口**：论坛发帖弹窗新增“附带投票”开关。
- **列表页**：帖子卡片显示“投票”徽标与票数摘要。
- **详情页**：正文下方、互动区上方插入投票卡片。

### 用户流

#### 发帖者

1. 打开发帖弹窗
2. 输入标题、正文、分类、标签
3. 开启“附带投票”
4. 填写投票问题、选项、截止时间
5. 发布帖子

#### 参与者

1. 在帖子列表识别投票帖
2. 打开帖子详情
3. 查看投票题目与选项
4. 选择一个选项并提交
5. 查看结果与自己所选项

---

## 业务规则

### 发帖规则

- 投票问题必填。
- 投票选项数量必须在 `2 ~ 6` 之间。
- 选项文本不能为空、不可重复。
- 截止时间可为空；为空表示长期有效，直到手动关闭或后续扩展阶段再补后台控制。

### 投票规则

- 仅登录用户可投票。
- 单个用户对单个投票只允许投一次。
- 已投票后不可重复投票。
- 已截止或已关闭时不可投票。
- 未登录用户可查看结果，但不能提交。

### 编辑与删除规则

- 带投票的帖子在 MVP 阶段**允许编辑标题 / 正文 / 分类 / 标签**。
- **不允许编辑投票问题与选项**，避免历史票数语义漂移。
- 删除帖子时，投票随帖子一并失效。
- MVP 不提供独立“删除投票”入口。

---

## 后端设计

### 数据模型

建议新增 3 个实体：

#### 1. `PostPoll`

表示一个帖子附带的投票主体。

建议字段：

- `PostId`
- `Question`
- `EndTime`
- `IsClosed`
- `TotalVoteCount`
- `TenantId`
- `IsDeleted`
- 审计字段

#### 2. `PostPollOption`

表示投票选项。

建议字段：

- `PollId`
- `OptionText`
- `SortOrder`
- `VoteCount`
- `TenantId`
- `IsDeleted`
- 审计字段

#### 3. `PostPollVote`

表示用户投票记录。

建议字段：

- `PollId`
- `PostId`
- `OptionId`
- `UserId`
- `UserName`
- `TenantId`
- `CreateTime`

### 模型约束

- `PostPoll`、`PostPollOption` 建议实现 `ITenantEntity`。
- `PostPoll`、`PostPollOption` 建议走软删除。
- `PostPollVote` 建议对 `(PollId, UserId)` 建唯一约束，保证单人单票。
- 投票写入时必须使用事务，同时更新：
  - `PostPoll.TotalVoteCount`
  - `PostPollOption.VoteCount`

### 与帖子关系

- 一个帖子最多挂一个投票。
- 投票归属于帖子，不单独对外暴露为独立内容类型。
- 帖子详情查询时附带投票聚合结果。

---

## ViewModel 与 DTO 设计

遵循当前项目 `Vo` 命名规范，建议新增：

### ViewModel

#### `PostPollVo`

- `VoPollId`
- `VoPostId`
- `VoQuestion`
- `VoEndTime`
- `VoIsClosed`
- `VoTotalVoteCount`
- `VoHasVoted`
- `VoSelectedOptionId`
- `VoOptions`

#### `PostPollOptionVo`

- `VoOptionId`
- `VoOptionText`
- `VoSortOrder`
- `VoVoteCount`
- `VoVotePercent`

#### `PollVoteResultVo`

- `VoPostId`
- `VoPoll`

### DTO

#### `CreatePollDto`

- `Question`
- `EndTime`
- `Options`

#### `PollOptionDto`

- `OptionText`
- `SortOrder`

#### `VotePollDto`

- `PostId`
- `OptionId`

### 对现有 DTO 的扩展

建议在 `PublishPostDto` 中新增可选字段：

```csharp
public CreatePollDto? Poll { get; set; }
```

`UpdatePostDto` 在 MVP 阶段先不扩展投票编辑字段。

---

## 接口设计

### 控制器划分

- 帖子主体继续由 `PostController` 承担。
- 投票动作建议新增 `PollController`，避免把帖子控制器继续堆大。

### 最小接口集

#### 1. 发布帖子（附带投票）

`POST /api/v1/Post/Publish`

- 继续复用现有发帖接口
- 请求中可选携带 `Poll`

#### 2. 获取帖子详情（包含投票）

`GET /api/v1/Post/GetById/{id}`

- 返回现有 `PostDetail`
- 若帖子带投票，则附带 `VoPoll`

#### 3. 提交投票

`POST /api/v1/Poll/Vote`

请求：

- `postId`
- `optionId`

返回：

- 最新 `PostPollVo`

#### 4. 按帖子获取投票详情

`GET /api/v1/Poll/GetByPostId?postId=`

用途：

- 前端局部刷新投票结果
- 详情页单独拉取或投票后刷新

### 错误语义

建议统一给出明确失败消息：

- 投票不存在
- 当前帖子未配置投票
- 你已经投过票
- 投票已截止
- 当前状态不允许投票
- 选项不存在

---

## Service / Repository 设计

### Service

建议新增：

- `IPostPollService`
- `PostPollService`

建议职责：

- 创建帖子投票
- 获取帖子投票详情
- 提交投票
- 计算投票结果与百分比

### Repository

优先策略：

- 先通过 `IBaseRepository<TEntity>` 与必要的仓储扩展完成
- 仅当投票聚合查询复杂时，再拆 `PostPollRepository`

### 事务边界

投票提交必须放在同一事务内完成以下操作：

1. 校验投票存在、未关闭、未截止
2. 校验用户未投票
3. 插入 `PostPollVote`
4. 更新 `PostPollOption.VoteCount`
5. 更新 `PostPoll.TotalVoteCount`

---

## 前端设计

### 发布入口

文件基线：

- `Frontend/radish.client/src/apps/forum/components/PublishPostModal.tsx`
- `Frontend/radish.client/src/types/forum.ts`
- `Frontend/radish.client/src/api/forum.ts`

建议新增：

- “附带投票”开关
- 投票问题输入框
- 动态选项编辑区
- 截止时间选择器

校验规则：

- 问题必填
- 至少 2 个选项
- 最多 6 个选项
- 选项不可重复

### 列表页

在帖子卡片中增加轻量摘要字段：

- `voHasPoll`
- `voPollTotalVoteCount`
- `voPollIsClosed`

展示策略：

- 显示“投票”徽标
- 显示票数
- 不在列表页直接投票

### 详情页

文件基线：

- `Frontend/radish.client/src/apps/forum/components/PostDetail.tsx`

新增投票卡片：

- 标题 / 问题
- 选项列表
- 截止状态
- 总票数

交互状态：

- **未登录**：显示结果，投票按钮置灰
- **已登录未投票**：可选择一个选项并提交
- **已投票**：显示结果并高亮自己的选项
- **已截止**：只显示结果

### 前端类型扩展

建议在 `types/forum.ts` 中补充：

- `PostPollVo`
- `PostPollOptionVo`
- 扩展 `PostItem`
- 扩展 `PostDetail`
- 扩展 `PublishPostRequest`

---

## 接口返回建议

### 帖子列表

为了避免 N+1，帖子列表不返回完整投票明细，只返回轻量摘要：

- `VoHasPoll`
- `VoPollTotalVoteCount`
- `VoPollIsClosed`

### 帖子详情

详情页返回完整 `VoPoll`：

- 问题
- 选项
- 票数
- 百分比
- 当前用户投票状态

### 为什么这样分层

- 列表页保持轻量，避免每个帖子都拉全量选项。
- 详情页承担完整交互，符合当前论坛架构。

---

## 权限与安全

### 权限边界

- 发帖权限继续复用现有 `PostController.Publish` 的发布权限拦截。
- 投票权限默认跟随“登录用户可参与”规则，不额外引入新的权限族。
- 管理员在 MVP 阶段不提供特殊投票管理操作。

### 风险点

#### 1. 并发重复投票

必须依赖：

- 数据库唯一约束
- 事务

不能只靠前端按钮置灰。

#### 2. N+1 查询

帖子列表不能对每条帖子单独查投票详情。

#### 3. 选项编辑导致历史漂移

因此 MVP 阶段直接禁掉投票编辑。

#### 4. 租户隔离

投票实体与投票记录必须带 `TenantId`，跟随帖子租户口径。

---

## 验收标准

满足以下条件即可认为投票 MVP 可进入验收：

- 可发布带投票的帖子
- 帖子详情可正确展示投票
- 登录用户可成功投票
- 重复投票会被拦截
- 截止后不能继续投票
- 帖子列表可识别投票帖
- 前后端字段口径一致，无匿名对象返回

---

## 最小测试清单

### 后端

- 创建带投票帖子成功
- 选项少于 2 个时失败
- 首次投票成功
- 重复投票失败
- 截止后投票失败
- 不存在选项时失败
- 跨租户不可投

### 前端

- 发布弹窗可创建投票
- 帖子详情能显示投票结果
- 未登录用户只能看不能投
- 已投票状态刷新后可回显
- 列表页投票摘要显示正常

### 人工验收资产

- `Radish.Api.Tests/HttpTest/Radish.Api.Forum.http` 已补论坛投票验收段，便于手工串联最小闭环。

---

## 后续扩展方向

若 MVP 通过并继续推进，可进入 `P3-ext`：

- 多选投票
- 匿名投票
- 撤票 / 改票
- 投票关闭管理
- 通知联动
- 排行榜 / 活动页
- Console 管理页

---

## 结论

本方案选择“**论坛帖子附带投票**”作为第一版实现路径，核心原因是：

- 与现有论坛主链最贴合
- 复用现有发帖与详情体系
- 体量可控
- 风险小、验收快

如果要在 `P3` 三个候选中先落一个最小首版，投票是当前最合适的切入点。
