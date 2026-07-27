# 论坛内容赞赏（胡萝卜 +1）设计方案

> **状态**：F4-N-B 服务端与 migration 已完成；等待批准进入 F4-N-C
>
> **更新日期**：2026-07-27
>
> **适用主线**：正式 Web 论坛；PC / mobile 浏览器共同验收
>
> **阶段关系**：F4-N 当前唯一功能专题；服务端权威链路已完成，进入 Pencil 或正式 Web 前必须再次取得明确批准

> [!IMPORTANT]
> 首批只支持 `Post / Comment` 和预设理由。`PostAnswer`、自定义理由文本、自定义金额、重复赞赏、排行榜和独立赞赏中心均不进入首批。Main DB 保存唯一资产真相，Log DB 的 `BalanceChangeLog` 只是由 Reliable Outbox 驱动的可靠、幂等审计投影，不参与 Main 资产事务。

## 摘要

“胡萝卜 +1”是基于现有萝卜币账本的**内容赞赏**：用户从自己的可用余额中送出固定 `1 胡萝卜`，目标内容作者收到 `1 胡萝卜`，并选择一项预设理由。

它不是新积分、风评值、声望系统、点赞、系统奖励或通用转账的别名。首版固定金额、免手续费、每人对同一内容最多成功一次，并由专用 Repository、Main 原子事务、操作幂等、数据库唯一约束和 Reliable Outbox 共同保护。

权威结论如下：

1. 首批目标仅为 `Post / Comment`；回答级定位和治理闭环完成前不支持 `PostAnswer`。
2. 首批理由仅为预设码，不保存用户自定义文本，不扩张举报和文本治理面。
3. `ContentReward`、双方 `UserBalance`、`TIP` 类型 `CoinTransaction`、成功幂等终态和两类 Outbox 请求在 Main 同一事务中提交。
4. `BalanceChangeLog` 由 Log DB 消费者按稳定投影键写入；重复投递不得生成重复分录，投影失败不得回滚或改写 Main 资产事实。
5. 内容赞赏使用专属 Repository，不复用需要支付密码的通用转账接口，也不顺手修复通用转账现有跨库日志维护债。
6. 通知固定为 `ContentRewardReceived + Reaction + ForumPost`，逐笔投递、不聚合，并受双向屏蔽策略抑制。

产品文案统一使用：

- 入口按钮：`送胡萝卜`
- 成功反馈：`已送出 1 胡萝卜`
- 公开记录：`胡萝卜 +1`
- 产品与领域名称：`内容赞赏`
- 代码领域名：`ContentReward`

## 1. 产品定位

### 1.1 要解决的问题

论坛已有点赞、轻回应和评论，但仍缺少一种介于“轻量表达”和“正式回复”之间、能让作者感受到真实认可的互动：

- 读者可以明确表达“这条内容值得我付出一点已有资产”；
- 作者可以看到是谁、以哪项理由给予认可；
- 社区把萝卜币从静态余额延伸到真实内容互动；
- 资产流转保持可审计，不凭空增加胡萝卜总量。

### 1.2 与相邻能力的区别

| 能力 | 是否涉及资产 | 主要语义 | 理由 |
|---|---:|---|---|
| 点赞 / 轻回应 | 否 | 低成本表达态度 | 无 |
| 内容赞赏 | 是，固定 `1 胡萝卜` | 对具体内容的公开认可 | 必选预设理由 |
| 系统奖励 | 是，系统增发 | 任务、活动或运营奖励 | 系统决定 |
| 用户转账 | 是，金额可变 | 用户之间的通用资产流转 | 私域备注 |
| 精华 / 采纳 | 否 | 管理或问答状态认定 | 无 |

内容赞赏不得替代点赞、问答采纳或管理员精选，也不得借“赞赏”入口开放任意金额转账。

## 2. 首版范围

### 2.1 支持对象

首版仅支持正式 Web 论坛中的：

- `Post`：帖子正文；
- `Comment`：帖子评论。

目标必须属于当前租户、真实存在、未删除、未被治理隐藏，且当前访问者具备读取权限。评论所属帖子也必须满足同样条件。接收者必须由服务端根据目标内容解析，客户端不得提交作者 ID。

`PostAnswer` 明确后置，直到以下能力同时形成闭环：

- `NotificationTargetData` 和正式 Web 支持回答级 canonical 定位、高亮与失效处理；
- 内容治理、举报、证据和申诉支持 `PostAnswer`；
- 回答删除、采纳和失效后的赞赏公开展示与审计边界明确；
- PC / mobile 能完成同等质量的回答级验收。

### 2.2 暂不支持

- `PostAnswer` 和 `PostQuickReply`；
- 聊天消息、Docs、商品评价等非论坛对象；
- 自定义理由文本；
- 匿名赞赏；
- 自定义金额、连续多次赞赏和批量赞赏；
- 赞赏排行榜、等级、徽章或新的声望体系；
- 独立赞赏中心；
- Flutter 新入口、Tauri 专用能力或 WebOS 专用交互；
- 法币充值、提现、兑换或任何现金价值承诺。

WebOS `/desktop` 仅为历史兼容入口。若其论坛窗口复用正式 Web 组件，可自然获得同一能力，但不单独维护第二套业务实现或验收口径。

## 3. 核心业务规则

### 3.1 资产规则

- 每次固定扣除发送者 `1 胡萝卜`，接收者增加 `1 胡萝卜`；
- 手续费固定为 `0`，交易前后系统胡萝卜总量不变；
- 金额由服务端固定，创建请求不接收 `amount`；
- 接收者由目标内容解析，创建请求不接收 `recipientUserId`；
- 不使用系统奖励和增发链路；
- 发送者可用余额不足时直接失败，不允许负余额、单边入账或先记后补；
- 内容删除、治理隐藏、普通用户反悔或屏蔽关系变化均不自动退款；
- 重复扣款、系统错误或确认欺诈只能通过受权补偿新增反向流水，不得修改原成功事实。

现有通用用户转账继续保持独立接口及支付密码保护。内容赞赏是受固定金额、固定业务对象、单次唯一、频率限制和公开记录约束的专用资产动作，不得调用或包装通用转账 Controller / Service 来绕过支付密码。

### 3.2 次数与业务日

- 同一发送者对同一目标内容终身最多成功一次；
- 发送者每日成功赞赏上限默认 `20` 次；
- 同一发送者每日向同一接收者成功赞赏上限默认 `5` 次；
- 次数只统计 Main 中已成功提交的 `ContentReward`，不统计失败、处理中、通知或 Log 投影；
- 上限属于可治理业务配置，但首批不向普通 Console 用户开放修改。

“每日”必须复用系统 `BusinessCalendar`，把业务日换算为 UTC 半开区间 `[startUtc, endUtc)`。客户端本地时区、数据库服务器本地时间和 `23:59:59` 尾端拼接均不得成为计数依据。

### 3.3 预设理由

首批必须选择一个预设理由：

| `reasonCode` | 展示文案 |
|---|---|
| `Helpful` | 很有帮助 |
| `Insightful` | 很有启发 |
| `WellWritten` | 写得真好 |
| `Detailed` | 内容详实 |
| `Warm` | 温暖了我 |

规则如下：

- 创建请求只接收稳定 `reasonCode`，服务端拒绝未知、空值或已停用编码；
- 展示文案由前端 i18n 和通知模板按编码解析，业务事实不保存本地化文案；
- 首批不接收、存储、搜索或展示 `reasonText`；
- 未来若开放自定义文本，必须作为包含举报、隐藏、证据、申诉、通知净化和历史兼容的独立治理增强重新设计；
- 首批不支持隐藏发送者身份。

## 4. 资格与治理矩阵

内容赞赏同时是资产写和用户互动。Service 负责组织用例，专属 Repository 在进入资产写前基于 Main 权威状态再次校验，不能信任客户端快照。

### 4.1 发送者与接收者

| 维度 | 发送者 | 接收者 | 结果 |
|---|---|---|---|
| 认证 | 必须是当前登录用户 | 由目标作者解析 | 未登录拒绝 |
| 租户 | 必须属于当前租户 | 必须与目标、发送者同租户 | 任一不一致按目标不可用处理 |
| 账号生命周期 | `IsEnable=true`、`IsDeleted=false`、`StatusCode=Normal` | 同左 | 任一不可用均不产生资产写 |
| 自赞赏 | 不得等于内容作者 | — | 返回稳定自赞赏错误 |
| 余额 | 可用余额至少为 `1` | 有有效余额账户；缺失时按现有规则原子初始化 | 发送者不足时拒绝 |
| 用户治理 `Mute / Ban` | 不额外冻结资产 | 不额外冻结收款 | 沿用既有“限制内容发布而非资产冻结”语义 |
| 未来资产冻结 / 全局互动限制 | 若权威策略明确禁止则拒绝 | 若权威策略明确禁止收款则拒绝 | F4-N 不自行发明平行状态 |
| 双向屏蔽 | 任一方向存在有效屏蔽均不可互动 | 同左 | 失败关闭并返回通用不可互动 |

`Mute / Ban` 当前只约束内容发布能力，F4-N 不得把它们悄然扩张为登录阻断或资产冻结。发送者处于该状态时仍只能选择预设理由，不能借赞赏发布自由文本；接收者能否收到赞赏继续取决于账号、目标内容、关系和未来明确资产策略。

`IUserInteractionPolicyService` / Main `UserBlock` 是双向屏蔽唯一真相。关系读取异常时资产写失败关闭，返回可重试的关系服务暂不可用错误；不得采用“查不到就允许”或缓存过期继续允许。

### 4.2 目标内容

| 目标状态 | 是否允许新赞赏 | 已有资产与公开记录 |
|---|---:|---|
| 正常公开且发送者可读 | 是 | 正常展示 |
| 不存在、跨租户或父帖不可用 | 否 | 不泄露存在性 |
| 已删除、未发布或治理隐藏 | 否 | Main 资产事实保留；公开列表停止展示 |
| 作者账号不可用 | 否 | 历史资产事实保留 |
| 之后被删除或治理下线 | 不再允许 | 不退款；资产与审计关联保留 |
| 之后发生双方屏蔽 | 不再允许 | 不删除既有资产事实；互动通知按屏蔽策略抑制 |

目标可读性应复用论坛现有权威查询与治理状态，不在 `ContentRewardService` 复制一套可见性判断。

## 5. 用户路径与交互

### 5.1 详情页位置

内容赞赏位于帖子正文或评论的主要操作区之后。帖子详情整体层级为：

```text
正文与主要操作
  -> 内容赞赏摘要
  -> 轻回应墙
  -> 评论区（每条评论含自己的赞赏入口与摘要）
```

内容赞赏是有成本的明确动作，不与无成本点赞挤在同一个即时切换控件中。

### 5.2 默认展示

- 未赞赏时显示 `送胡萝卜` 和累计人数；
- 已赞赏时显示稳定完成态 `已送胡萝卜`，不可再次点击扣款；
- 有记录时默认展示最近 `3` 条，可展开分页查看；
- 固定金额下人数与胡萝卜数量相等，但接口仍使用明确的 `totalCount` 语义；
- PC 使用紧凑行式布局，移动端使用纵向卡片，不压缩成多列小表格。

### 5.3 确认与反馈

点击 `送胡萝卜` 后打开确认浮层或对话框，明确展示：

- `本次将花费 1 胡萝卜`；
- 当前可用余额；
- 预设理由单选；
- `确认送出` 主按钮。

首批不显示自定义理由输入框。提交期间禁用重复操作，不对资产金额做乐观更新；只有收到服务端成功或幂等回放结果后，才更新余额、累计数和公开记录。

成功动效可使用一次约 `300ms` 的胡萝卜上浮与 `+1` 反馈；`prefers-reduced-motion` 下只保留颜色与文字反馈。动效不得改变布局高度或遮挡正文。

未登录时进入登录流程并保留当前内容返回地址；余额不足时给出资产说明并可跳转 `/me/assets`，不把购买或充值设为强制下一步。

## 6. Main 权威数据模型

### 6.1 `ContentReward`

新增独立成功事实实体：

| 字段 | 说明 |
|---|---|
| `Id` | 内部主键，雪花 ID；通过 JSON 时按字符串传输 |
| `TenantId` | 租户 ID |
| `TargetType` | 首批仅 `Post / Comment` |
| `TargetId` | 目标内容内部 ID |
| `PostId` | canonical 论坛根帖子 ID；评论赞赏也固化父帖 |
| `SenderUserId` | 发送者用户 ID |
| `RecipientUserId` | 内容作者用户 ID 快照 |
| `Amount` | 固定为 `1` |
| `ReasonCode` | 预设理由编码 |
| `CoinTransactionId` | 对应 Main `CoinTransaction.Id` |
| `CreateTime` | 成功时间，UTC |

`ContentReward` 只保存成功事实，不增加没有实际状态迁移的 `Pending / Failed / ReasonHidden` 状态，也不保存自定义理由文本。

必要约束与索引：

- 唯一约束：`TenantId + SenderUserId + TargetType + TargetId`；
- 唯一约束：`TenantId + CoinTransactionId`；
- 目标列表索引：`TenantId + TargetType + TargetId + CreateTime`；
- 发送者业务日索引：`TenantId + SenderUserId + CreateTime`；
- 接收者业务日索引：`TenantId + SenderUserId + RecipientUserId + CreateTime`；
- canonical 导航索引：`TenantId + PostId + CreateTime`；
- `Amount` 在应用与 migration verify 中固定为 `1`；
- 资产事实不物理删除。

首批不在 `Post / Comment` 上新增冗余计数。详情和批量状态接口通过数据库聚合获得数量；出现明确性能证据后，再设计可重建聚合投影。

### 6.2 `CoinTransaction` 与余额

成功赞赏写入：

- `CoinTransaction.TransactionType = TIP`；
- `Status = SUCCESS`；
- `FromUserId = SenderUserId`；
- `ToUserId = RecipientUserId`；
- `Amount = 1`；
- `Fee = 0`；
- `BusinessType = ContentReward`；
- `BusinessId = ContentReward.Id`；
- `RewardBusinessKey = null`。

`RewardBusinessKey` 只用于系统发放 / 奖励业务，内容赞赏属于用户出资转移，不得占用该字段充当幂等键。

发送者与接收者 `UserBalance` 是 Main 余额真相。`ContentReward` 与 `CoinTransaction` 是业务事实和交易事实，任何缓存、通知、前端余额或 Log 投影都不得反向覆盖它们。

## 7. 专属 Repository 与 Main 原子事务

### 7.1 职责边界

新增 `IContentRewardService` 和 `IContentRewardRepository`：

- Controller 只负责认证上下文、DTO 校验和统一 `MessageModel` 返回；
- Service 负责请求规范化、业务日、用户路径和错误映射；
- 专属 Repository 负责需要同一 Main 连接、事务、锁与唯一约束保护的资产写；
- Service 不直接访问 `_repository.Db` / `_repository.DbBase`；
- 不调用通用转账入口，也不把多个 BaseRepository 写入在 Service 中拼成“看似事务”。

Repository 命令必须携带服务端解析后的租户、当前用户、目标、业务日区间、预设理由和幂等摘要，但 Repository 仍需在事务中复核会影响资产写的 Main 权威状态。

### 7.2 成功事务

成功请求必须在 Main 同一事务中完成：

1. 锁定并复核幂等记录仍为本次 `Processing`；
2. 复核目标、父帖、作者、双方账号、双向屏蔽和业务日次数；
3. 按确定性顺序读取或创建双方 `UserBalance`；
4. 校验发送者可用余额；
5. 创建 `ContentReward`；
6. 创建 `TIP / SUCCESS` 的 `CoinTransaction`；
7. 扣减发送者余额并增加接收者余额；
8. 将幂等记录完成为 `Succeeded`，绑定资源和安全响应快照；
9. 写入审计投影 Outbox；
10. 写入逐笔通知 Outbox；
11. 一次提交。

任一步失败均回滚 Main 业务写，不得留下单边余额、孤立赞赏、孤立交易、成功幂等终态或缺失 Outbox 的成功事实。

### 7.3 确定性锁序与并发

- 两个余额账户始终按 `min(SenderUserId, RecipientUserId)` 到 `max(...)` 的顺序处理；
- PostgreSQL 使用行级锁或等价的条件更新保证余额与次数校验不会并发穿透；
- SQLite 使用显式写事务串行化同一资产写临界区，同时保持相同逻辑顺序；
- 余额不存在时依赖用户余额唯一约束完成原子初始化，唯一冲突后重新读取，不创建重复账户；
- 余额更新继续保留现有并发版本 / 条件更新保护，不允许仅靠进程内锁；
- 同目标唯一约束是“不同幂等键并发请求仅一次成功”的最终数据库保护；
- 每日上限查询、唯一插入和余额写必须处于同一事务隔离边界。

达到数据库死锁、忙碌或并发冲突时只做有界重试；重试耗尽返回稳定可重试错误，不吞异常、不无限重试。

## 8. 幂等与失败恢复

### 8.1 操作幂等

内容赞赏复用 `OperationIdempotencyRecord` 的资产写治理，不使用 `ContentSubmissionRecord`。

- 新增 `OperationType = ContentReward`；
- 新增 `ResourceType = ContentReward`；
- 客户端键格式：`content-reward:{uuid}`，创建请求必须提供；
- 请求摘要只包含规范化后的 `targetType`、`targetId` 和 `reasonCode`；
- 同键同摘要且已成功时返回保存的同一终态响应；
- 同键不同摘要返回幂等冲突；
- 未过期的处理中请求不得再次进入余额写；
- 不同键并发命中同一目标时，由 `ContentReward` 唯一约束保证仅一次成功。

现有 `IOperationIdempotencyService` 可复用键规范化、摘要和响应序列化规则，但成功终态必须由专属 Repository 在资产事务内完成，不能在资产事务提交后再调用一次独立的 `CompleteSuccessAsync`。

### 8.2 恢复矩阵

| 故障点 | 权威结果 | 恢复方式 |
|---|---|---|
| 参数或认证校验失败 | 无幂等与资产写 | 修正请求后重新提交 |
| 终态业务拒绝 | 无资产写；返回稳定错误 | 同一请求可按既有资产幂等失败策略处理 |
| Main 事务提交前失败 | 全部回滚 | 过期 `Processing` 经确认无成功事实后才能重开 |
| Main 已提交但响应丢失 | 成功事实完整 | 同键重试回放保存的成功响应 |
| 唯一约束竞争 | 仅一个成功事实 | 赢家返回成功；败者映射为已赞赏或幂等回放 |
| 通知消费失败 | Main 资产已成功 | Outbox 重试 / dead letter / 受权 replay，不回滚资产 |
| Log 投影失败 | Main 资产已成功 | Outbox 重试 / dead letter / 受权 replay，投影键防重 |
| 消费者写入后、标记 Outbox 前崩溃 | Main 资产已成功 | 重投时由通知去重键或 Log 投影键返回已有结果 |

过期 `Processing` 不得仅凭时间直接重置。恢复逻辑必须在 Main 查询关联的 `ContentReward`、`CoinTransaction` 和 Outbox：

- 若成功事实存在，则修复幂等终态并回放响应；
- 若事务事实全部不存在，才允许重新进入处理；
- 若出现部分事实，视为完整性故障并告警，不用 fallback 猜测结果。

## 9. Log DB 审计投影

### 9.1 定位

`BalanceChangeLog` 是方便审计、对账和按月查询的 Log DB 投影，不是余额真相，也不与 Main 形成分布式事务。

Main 成功事务写入一条审计投影 Outbox，payload 固化：

- `TenantId`、`ContentRewardId`、`IdempotencyRecordId`、`CoinTransactionId`；
- 发送者与接收者 ID；
- 双方 `BalanceBefore / BalanceAfter / ChangeAmount`；
- 发送方 `ChangeType = TRANSFER_OUT`；
- 接收方 `ChangeType = TRANSFER_IN`；
- Main 成功时间 `OccurredAtUtc`；
- 两个稳定投影键。

不得在消费者中重新读取“当前余额”来推算历史前后值。

### 9.2 幂等写入

`BalanceChangeLog` 增加可空稳定投影键，例如 `SourceEventKey`。F4-N 生成：

```text
content-reward:{coinTransactionId}:{userId}:out
content-reward:{coinTransactionId}:{userId}:in
```

要求：

- Log 对 `TenantId + SourceEventKey` 提供数据库唯一保护；
- 两条分录在同一 Log 事务中写入；
- `OccurredAtUtc` 决定稳定的月分表路由，重试不得漂移到其他分表；
- 同键同载荷重试视为成功；
- 同键异载荷视为完整性冲突并进入 dead letter / 告警；
- 消费完成后才能标记 Main Outbox 成功；
- 对账以 Main `ContentReward + CoinTransaction + UserBalance` 为准，缺失投影可以补投，Log 不得反向修正 Main。

现有通用转账直接跨库写 `BalanceChangeLog` 的假设保留为独立维护债。F4-N 不借机重构通用转账，也不因旧路径尚未治理而降低新路径标准。

## 10. API 契约

### 10.1 创建赞赏

```http
POST /api/v1/ContentReward/Create
```

请求：

```json
{
  "targetType": "Post",
  "targetId": "123456789012345678",
  "reasonCode": "Helpful",
  "idempotencyKey": "content-reward:7d8705ad-6cd4-48a0-a51d-a09e30047b91"
}
```

禁止接收 `senderUserId`、`recipientUserId`、`amount`、`reasonText` 和 `paymentPassword`。

成功返回 `ContentRewardMutationVo`，至少包含：

- `VoRewardId`；
- `VoTargetType / VoTargetId`；
- `VoReasonCode`；
- `VoTotalCount`；
- `VoViewerRewarded = true`；
- `VoSenderAvailableBalance`；
- `VoTransactionNo`。

所有 Long ID 在 JSON 中按字符串传输。Controller 不返回匿名对象。

### 10.2 查询目标状态与记录

详情分页：

```http
GET /api/v1/ContentReward/GetTargetRewards
    ?targetType=Post
    &targetId=123456789012345678
    &pageIndex=1
    &pageSize=20
```

批量摘要：

```http
POST /api/v1/ContentReward/GetTargetStates
```

批量接口用于帖子详情和评论树避免 N+1，限制单次目标数量并只接受 `Post / Comment`。响应包含目标累计数量和当前登录用户是否已赞赏；匿名读取时 `viewerRewarded=false`。

公开记录只返回发送者公开资料白名单、`reasonCode` 和时间，不返回内部用户 ID、余额、交易流水号、幂等键或 Log 投影键。目标变为不可读后，公开查询按目标不可用处理。

### 10.3 稳定错误语义

至少固定：

| HTTP | 错误码 | 语义 |
|---:|---|---|
| 400 | `ContentReward.InvalidArgument` | 目标类型、ID、理由或幂等键无效 |
| 401 | 统一未认证错误 | 需要登录 |
| 503 | `ContentReward.Unavailable` | schema 尚未部署完成或功能开关关闭 |
| 404 | `ContentReward.TargetUnavailable` | 目标不存在、不可读、跨租户或治理隐藏 |
| 409 | `ContentReward.SelfNotAllowed` | 不允许赞赏自己的内容 |
| 409 | `ContentReward.AlreadyRewarded` | 同一用户已赞赏该目标 |
| 409 | `ContentReward.InsufficientBalance` | 可用余额不足 |
| 409 | `ContentReward.DailyLimitExceeded` | 达到总量或同接收者业务日上限 |
| 409 | `ContentReward.AccountUnavailable` | 发送者或接收者账号不可用 |
| 409 | `UserBlock.InteractionUnavailable` | 双向屏蔽；不暴露方向 |
| 409 | `ContentReward.Processing` | 同一幂等请求处理中 |
| 409 | `ContentReward.IdempotencyConflict` | 同键异摘要 |
| 503 | `ContentReward.ConcurrentConflict` | 有界重试后仍存在数据库并发冲突 |
| 503 | `UserBlock.RelationshipTemporarilyUnavailable` | 无法确认关系真相，失败关闭 |

接口继续使用项目统一 `MessageModel`、HTTP 状态、错误码、i18n key 和 `TraceId` 契约。

## 11. 通知、回流与资产自查

### 11.1 通知定义

新增通知定义：

- `Kind = ContentRewardReceived`；
- `Category = Reaction`；
- `DefaultPriority = Normal`；
- `TargetKind = ForumPost`；
- `SuppressWhenInteractionBlocked = true`；
- `AggregationWindow = null`；
- 必要模板参数包含发送者公开名、预设理由和目标标题。

每笔成功赞赏产生一条独立通知请求，不按目标、发送者或时间窗聚合。通知 Outbox 幂等键以 `ContentRewardId` 为自然事实，重复消费不得重复创建持久通知。

### 11.2 定位与屏蔽

- Post 赞赏使用 `PostId + PostPublicId`；
- Comment 赞赏使用 `PostId + PostPublicId + CommentId`；
- 打开通知后进入 canonical forum 路由，评论目标滚动并高亮；
- 首批不扩展 `NotificationTargetData` 的 Answer 定位；
- Outbox 消费时若双方已形成屏蔽，通知按注册定义抑制并将消费视为完成；
- 屏蔽发生在 Main 交易提交后，不撤销赞赏、不退款，也不删除双方资产流水；
- Unblock 不补发被抑制通知。

发送者和接收者都可在现有资产流水中看到 `TIP / ContentReward`。Console 资产流水可按 `TIP` 和 `ContentReward` 筛选并回看仍可访问的目标内容。

首批不新增独立赞赏中心，追查路径固定为通知中心、内容详情、个人资产流水和 Console 资产审计。

## 12. 迁移与兼容

数据库结构必须通过 `Radish.DbMigrate` 有序 schema ledger 迁移，不恢复 Code First 或运行时静默补表。

### 12.1 Expand

1. Main 新增 `ContentReward`、索引和唯一约束；
2. 增加 `ContentReward` 幂等操作 / 资源常量和 Reliable Outbox 任务类型；
3. Log `BalanceChangeLog` 增加可空 `SourceEventKey` 及唯一保护；
4. 更新 SQLite / PostgreSQL migration、doctor / apply / verify；
5. 旧 `CoinTransaction`、余额和 `BalanceChangeLog` 数据保持可读，不回填虚构赞赏事实。

### 12.2 Apply

- 部署新 schema 后再启用 Service / Repository 和 Outbox 消费者；
- 消费者必须识别 schema version，未知版本进入稳定失败，不猜测 payload；
- 功能开关关闭时不展示入口、不接受创建，但查询旧成功事实仍可用；
- 不将旧 `TIP` 流水推断为 `ContentReward`。

### 12.3 Verify

- Main 约束、索引、幂等常量和 Outbox 任务已就绪；
- Log 新列与唯一保护覆盖当前和迁移管理范围内的月分表；
- SQLite / PostgreSQL 重复 apply 无额外副作用；
- 严格 verify 能发现缺表、缺列、缺索引、部分迁移和错误 schema version；
- 回滚应用版本时新表与新列可保留，不物理删除已成功资产事实。

## 13. F4-N A–D 批次

### F4-N-A：权威设计

- 完成现状审计和本文修订；
- 固定首批对象、理由、Main / Log 真相、资格矩阵、通知与停止线；
- 复核预计代码和 migration 范围；
- 完成后先汇报，等待明确批准进入 F4-N-B。

### F4-N-B：服务端与 migration

- 新增模型、DTO / Vo、常量、映射、接口和专属 Repository；
- 完成 Main 原子事务、幂等、确定性锁序、业务日上限和 Outbox；
- 完成 Log 幂等投影、通知消费者和迁移编排；
- 覆盖 SQLite / PostgreSQL 的事务、并发、重试、投影和严格 verify；
- B 批不提前实现正式 Web 页面。

完成事实：Main 权威事务、过期 `Processing` 成功事实核对与修复回放、Log 双分录幂等投影、逐笔通知、默认关闭的发布开关、Main / Log migration、HTTP 示例和定向测试均已落地；PostgreSQL migration 用例进入环境门禁，F4-N-C 获批前不实现页面。

### F4-N-C：Pencil 与正式 Web

- 先更新权威 Pencil 的 PC / mobile 论坛详情状态；
- 在 `@radish/http` 增加统一类型和客户端；
- 接入 Post / Comment 入口、确认、状态、公开列表、资产反馈和通知定位；
- 覆盖四主题、i18n、键盘、焦点、窄屏、长名字、空态、错误态和 reduced-motion；
- 正式路由与 WebOS 复用同一实现，不建设壳层分叉。

### F4-N-D：Gateway 成组验收

- 在当前任务重新取得服务启动授权后执行；
- 通过 Gateway 同时复核 PC / mobile；
- 覆盖匿名、发送者、接收者、被屏蔽双方、账号不可用和治理目标等身份矩阵；
- 复核并发、响应丢失重试、通知抑制、Outbox replay、Log 防重和资产总量；
- 清理临时用户、余额、赞赏、交易、通知、Outbox、Log 投影、凭据、浏览器状态和备份；
- 检查六库完整性并执行严格 migration verify，形成批次级验收记录。

## 14. 验证矩阵

### 14.1 Main 资产与并发

- 正常赞赏后发送者 `-1`、接收者 `+1`、总量不变；
- 余额不足、自赞赏、目标不可见、账号不可用和双向屏蔽均不产生资产写；
- 同一幂等键重试只产生一条赞赏、一条交易和两类 Outbox；
- 同键不同摘要稳定冲突；
- 不同幂等键并发赞赏同一目标仅一次成功；
- 并发赞赏不同目标不会超扣、负余额或穿透业务日上限；
- 双方反向同时发生资产动作时，确定性锁序不形成稳定死锁；
- `ContentReward`、双方余额、`CoinTransaction`、成功幂等终态和 Outbox 始终原子一致；
- `RewardBusinessKey` 保持 `null`，交易类型固定 `TIP`；
- 业务日使用 `[startUtc, endUtc)`，边界时刻不重算或漏算；
- Long ID 序列化为字符串。

### 14.2 Log 与通知

- 两条 `BalanceChangeLog` 与 Main 成功快照一致；
- 重复消费、消费者崩溃后重投和受权 replay 均不重复分录；
- Log 暂停或失败不影响 Main 成功，恢复后可补齐；
- 同投影键异载荷进入完整性告警，不覆盖旧分录；
- 每笔赞赏只创建一条 `ContentRewardReceived`，不聚合；
- Post / Comment 通知分别稳定定位正文和评论；
- 提交后新增屏蔽会抑制未投递通知，但不改变资产；
- 通知失败或被抑制不回滚资产。

### 14.3 正式 Web

- 未登录回流、余额不足、提交中、成功、重复赞赏和幂等处理中状态清晰；
- 不出现自定义理由输入框；
- 资产数量不做乐观更新；
- PC 行式布局与移动卡片均不溢出；
- 键盘操作、焦点返回、错误提示和 reduced-motion 可用；
- Post / Comment 累计数和当前用户状态一致；
- 四主题和中英文文案均使用语义 token 与 i18n；
- 从通知和资产流水能够稳定回到仍可访问的目标。

### 14.4 开发期验证

开发中按风险执行定向后端测试、前端测试、type-check、build、changed-only lint、repo hygiene 和 `git diff --check`。真实 smoke 只在 F4-N-D 成组验收执行；启动服务前必须取得当前任务明确授权。

## 15. 停止线

- 不创建“胡萝卜之外的萝卜币”、独立风评值或新声望；
- 不允许客户端决定金额、接收者或交易类型；
- 不调用或包装通用转账接口绕过支付密码；
- 不把 Log DB 写入描述为 Main 原子事务或分布式事务；
- 不顺手重构现有通用转账跨库日志；
- 不在首批支持 `PostAnswer`、自定义理由、自定义金额、重复赞赏或批量赞赏；
- 不引入手续费、匿名赞赏、自动退款、排行榜、等级、勋章或任务奖励；
- 不新增 `ContentReward` 文本举报目标、隐藏状态或平行治理后台；
- 不把 `Mute / Ban` 扩张为未定义的资产冻结；
- 不新增独立赞赏中心；
- 不为 Flutter、Tauri 或 WebOS 建立第二套服务契约；
- 不在 F4-N-B 后直接进入 Pencil 或正式 Web，必须先汇报范围并等待批准；
- 不因单独专题文档频繁创建 `dev -> master` PR，待形成完整功能或成组维护批次后统一集成。

## 16. 关联文档

- [当前进行中](/planning/current)
- [发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)
- [萝卜币系统设计方案](/guide/radish-coin-system)
- [萝卜币核心概念](/guide/radish-coin-core-concepts)
- [萝卜币对账与数据库设计](/guide/radish-coin-finance)
- [萝卜币安全性与技术实现](/guide/radish-coin-security-tech)
- [支付与资产写幂等治理](/guide/payment-idempotency-governance)
- [论坛内容发布可靠性与编辑历史治理](/guide/forum-content-write-reliability-governance)
- [用户屏蔽与关系交互隔离](/features/user-block-relationship-isolation-design)
- [内容治理、案件、证据与动作闭环](/features/content-moderation-case-evidence-action-design)
- [论坛轻回应墙设计](/features/forum-quick-reaction-wall)
- [论坛问答 MVP 设计](/features/forum-qa-mvp)
- [浏览器 Smoke 规则](/guide/browser-smoke)
