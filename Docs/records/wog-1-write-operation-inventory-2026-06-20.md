# WOG-1 写操作分级盘点记录

> 日期：`2026-06-20`（Asia/Shanghai）
>
> 范围：只读代码和文档，盘点当前写入口的可靠性、并发保护、审计和后续候选排序。
>
> 关联说明：[写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)

## 结论摘要

- `WOG-1` 已完成首轮只读盘点，覆盖资产 / 库存、奖励 / 玩法、内容互动、管理治理、通知 / 消息五类写入口。
- 商城购买和萝卜币转账已接入 `OperationIdempotencyRecord`、请求摘要绑定和终态响应重放；Web 购买与转账流程已传 `idempotencyKey`。
- 电子宠物照顾已有业务级 `IdempotencyKey` 与唯一索引；关注、投票、抽奖、Reaction 也有对应唯一键或唯一冲突处理。
- 当前首批代码候选不应继续扩完整钱包、风控平台、Redis 分布式锁或通用签名；最高价值候选是内容互动关系写入和计数一致性，其次是背包 / 权益发放的条件更新与重试发放幂等。
- Flutter 商城购买当前未传 `idempotencyKey`，应作为后续跨端承接契约项；不抢占本轮 Web / 后端治理首批代码顺位。

## 写入口矩阵

| 业务域 | 写入口 | 调用层 | 现有保护 | 缺口风险 | 建议治理 | 验证入口 | 方案确认 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 资产 / 库存 | 商城购买 | `ShopController.Purchase` -> `OrderService.PurchaseAsync` | 支付口令校验、`idempotencyKey`、请求摘要、终态重放、事务、库存乐观锁、余额乐观锁、Web 生成 `shop:{uuid}` | 服务端仍兼容未传 key；Flutter 商城购买未传 key | 维持已落地实现；发布候选前回归；跨端承接时补 Flutter key | `dotnet test Radish.Api.Tests`、商城购买定向测试、Flutter 接入后测试 | Flutter 接入需要方案确认 |
| 资产 / 库存 | 萝卜币转账 | `CoinController.Transfer` -> `CoinService.TransferAsync` | 支付口令校验、`idempotencyKey`、请求摘要、终态重放、事务、余额乐观锁重试、Web 生成 `coin-transfer:{uuid}` | 旧客户端未传 key 时不承诺幂等 | 维持已落地实现；主要客户端完成后再评审强制 key | 转账幂等定向测试、资产回归 | 强制 key 需要方案确认 |
| 资产 / 库存 | 管理员调账 | `CoinController.AdminAdjustBalance` -> `CoinService.AdminAdjustBalanceAsync` | 管理权限、原因必填、余额乐观锁重试、交易流水和余额变动日志 | 重复提交会产生重复调账；目前无操作 key、确认参数或终态重放 | 暂不并入首批；后续作为高风险管理写入单独评审幂等 key / 二次确认 / 审计展示 | 调账定向测试、Console 权限测试 | 需要方案确认 |
| 资产 / 库存 | 订单取消 / 系统超时取消 | `ShopController.CancelOrder`、Hangfire -> `OrderService.CancelOrderAsync / CancelOrderBySystemAsync` | 事务、仅取消可取消订单、库存恢复、状态更新 | 并发取消可能重复尝试恢复库存，需确认 `CancelPendingOrderAsync` 条件更新覆盖面 | 补定向测试；若发现重复恢复风险，再改条件更新 | 订单取消定向测试 | 若改条件更新需方案确认 |
| 资产 / 库存 | 权益 / 背包发放 | `OrderService.RetryGrantBenefitAsync` -> `UserBenefitService.GrantBenefitAsync` | 购买主流程被订单幂等包裹；背包按用户 + 消耗品类型 + 值查询后叠加 | 管理员重试发放、并发购买同一消耗品时可能出现重复权益或背包数量读后写覆盖 | 首批候选 2：补 `SourceOrderId` 幂等判断、背包条件增量 / 唯一键评审和定向测试 | 权益重试、背包数量、购买回归测试 | 涉及数据库索引需方案确认 |
| 资产 / 库存 | 道具使用 | `ShopController.UseItem / UseRenameCard` -> `UserInventoryService` | 数量前置校验、发放经验 / 萝卜币后扣减道具 | 先发放奖励再普通扣减；并发使用可能重复发放或扣减失败后奖励已发 | 首批候选 3：将扣减改为条件更新，经验 / 萝卜币发放绑定使用流水或幂等业务键 | 背包使用定向测试、经验 / 萝卜币交易测试 | 涉及写入顺序需方案确认 |
| 奖励 / 玩法 | 经验发放 | `ExperienceService.GrantExperienceAsync` | 事务、用户经验乐观锁、每日上限、`ExpTransaction` 去重索引但非唯一 | 调用方未统一先查 `HasExperienceTransactionAsync`；重复任务可能重复发放直到日上限 | 补业务去重口径文档和关键奖励测试；不直接上通用幂等表 | 经验服务定向测试 | 代码批次需方案确认 |
| 奖励 / 玩法 | 萝卜币互动奖励 | `CoinRewardService` -> `CoinService.GrantCoinAsync` | 奖励存在性查询、每日点赞奖励上限、余额乐观锁 | `CheckRewardExistsAsync` 是读后写，`CoinTransaction` 缺少业务唯一约束；并发奖励可能重复发放 | 首批候选 4：给关键奖励业务键补唯一约束或服务端幂等，先覆盖点赞 / 神评 / 沙发奖励 | 评论高亮、点赞奖励定向测试 | 涉及唯一索引需方案确认 |
| 奖励 / 玩法 | 神评 / 沙发重算与奖励 | `CommentService.TriggerHighlightRecheckAsync`、后台 job | 稳定窗口、替换阈值、奖励存在性查询、当前高亮状态 | 高亮状态与奖励写入跨异步任务，重复 job 依赖读后写防重 | 发布候选前回归；如重复奖励命中，再收敛为唯一业务键 | 评论实时 / 高亮回归 | 需要代码时方案确认 |
| 奖励 / 玩法 | 电子宠物领取 / 照顾 | `PetController` -> `PetService` | `PetProfile.UserId` 唯一；照顾动作 `IdempotencyKey` 唯一；每日上限和冷却 | 照顾更新宠物状态与日志写入不是显式事务；并发同 key 依赖唯一索引异常路径 | 补并发 / 同 key 重放测试；暂不扩宠物经济参数 | 宠物服务定向测试 | 若改事务需方案确认 |
| 内容互动 | 发帖 / 评论 / 回答 | `PostService.PublishPostAsync`、`CommentService.AddCommentAsync`、`PostService.AddAnswerAsync` | 内容长度设置、事务、附件绑定、异步奖励 / 通知、编辑历史 | 重复提交会创建多条内容；计数为读后写；异步奖励无统一幂等调用口径 | 不直接服务端幂等；先补内容频率 / 去重窗口专题，当前只列候选 | 内容发布定向测试 | 需要产品策略确认 |
| 内容互动 | 帖子 / 评论点赞 | `PostService.ToggleLikeAsync`、`CommentService.ToggleLikeAsync` | 软删除点赞记录、奖励存在性查询、通知去重 | `UserPostLike` / `UserCommentLike` 未见唯一索引；并发 toggle 可能重复关系、计数漂移、重复奖励 | 首批候选 1：补用户-目标唯一约束、唯一冲突处理、计数条件更新 / 回算测试 | 点赞并发定向测试、评论 / 帖子计数测试 | 涉及数据库索引需方案确认 |
| 内容互动 | Reaction 表情回应 | `ReactionService.ToggleAsync` | `UserId + TargetType + TargetId + EmojiValue` 唯一索引、唯一冲突重试一次、事务 | 上限检查是读后写，但唯一键已覆盖同一 emoji；多 emoji 上限极端并发可能穿透 | 保持现状，补上限并发测试即可 | Reaction 定向测试 | 不需要 |
| 内容互动 | 投票 | `PostPollService.VoteAsync / CloseAsync` | `PollId + UserId` 唯一索引、事务、截止 / 已投校验 | 计数 `option.VoteCount++`、`poll.TotalVoteCount++` 为读后写；唯一冲突未显式转业务响应 | 补唯一冲突处理与原子计数测试，优先级低于点赞 | 投票定向测试 | 涉及计数改法需方案确认 |
| 内容互动 | 抽奖开奖 | `PostLotteryService.DrawAsync / AutoDrawByPostIdAsync` | `PostLottery.PostId` 唯一、`PostLotteryWinner.LotteryId + UserId` 唯一、事务、开奖前 `IsDrawn` 校验 | 双触发开奖可能在读后写窗口冲突；唯一约束可挡重复中奖用户，但不一定给出稳定响应 | 补并发开奖测试；必要时把开奖状态更新改为条件更新 | 抽奖开奖定向测试 | 若改条件更新需方案确认 |
| 内容互动 | 轻回应 | `PostQuickReplyService.CreateAsync` | 功能开关、内容长度设置、缓存冷却、重复内容窗口、事务 | 缓存不是数据库真值；多实例后短窗口防重可能失效 | 当前保持；多实例或真实刷屏后按 Redis 专题回拉 | 轻回应定向测试 | 不需要 |
| 内容互动 | 关注 / 取关 | `UserFollowService.FollowAsync / UnfollowAsync` | `TenantId + FollowerUserId + FollowingUserId` 唯一索引、软删除恢复、通知 | 并发关注可能触发唯一冲突但未显式捕获；关系真值有唯一键保护 | 保持现状，补唯一冲突友好响应 / 测试即可 | 关注关系测试 | 不需要 |
| 管理治理 | 系统设置更新 / 恢复默认 | `SystemConfigService.UpdateConfigAsync / RestoreConfigDefaultAsync` | 代码级设置定义、Low / Medium 风险限制、原因 / 确认参数、变更审计 | 无版本号；多管理员并发编辑可能后提交覆盖先提交 | 后续候选：评审覆盖语义和版本号，不作为首批 | 系统设置服务测试、Console 构建 | 需要方案确认 |
| 管理治理 | Console 角色授权保存 | `ConsoleAuthorizationService.SaveRoleAuthorizationAsync` | 资源归一化、软删除 / 恢复、全量保存 | 无版本号；多管理员并发保存会覆盖资源集合 | 后续候选：补快照版本 / 最后修改时间确认 | Console 授权测试 | 需要方案确认 |
| 管理治理 | 内容举报 / 审核 / 用户治理动作 | `ContentModerationService` | 待处理举报去重查询、已处理拒绝重复审核、治理动作会停用旧动作并写日志 | 举报待处理无唯一约束；审核和手动治理并发可能出现重复动作或覆盖 | 后续候选：补待处理举报唯一键、审核条件更新、治理动作活跃唯一约束评审 | 内容治理定向测试 | 涉及索引需方案确认 |
| 管理治理 | 商品管理 | `ProductService.Create / Update / PutOnSale / TakeOffSale / Delete` | 商品上下架配置校验、已有订单禁止删除、库存有版本字段 | 商品编辑无版本号；多管理员并发可能覆盖价格、库存、上下架状态 | 暂不首批；商品编辑进入运营前再补版本语义 | 商品服务测试、Console 产品页构建 | 需要方案确认 |
| 通知 / 消息 | 创建通知 | `NotificationService.CreateNotificationAsync` | 通知与用户通知分开写入、异步推送、调用方部分有去重 | 通知创建本身没有幂等；重复调用会重复通知，未读缓存依赖增量 | 调用方按业务去重；不做通用通知幂等 | 通知服务测试 | 不需要 |
| 通知 / 消息 | 通知已读 / 全部已读 / 删除 | `NotificationService.MarkAsReadAsync / MarkAllAsReadAsync / DeleteNotificationAsync` | 条件更新未读项、全部已读会强制缓存归零、删除未读时减量 | 删除前先查 `wasUnread`，并发已读 / 删除可能缓存减量不精确 | 补缓存校准测试；必要时删除改为条件更新并按 affected unread 计算 | 通知服务测试 | 不需要 |
| 通知 / 消息 | 聊天发送 / 撤回 / 已读 | `ChatService.SendMessageAsync / RecallMessageAsync / MarkChannelAsReadAsync` | 消息雪花 ID、频道成员唯一索引、撤回条件更新、已读游标收敛 | 发送无客户端幂等，网络重试会重复消息；频道最后消息和成员创建有读后写窗口 | 保持当前聊天产品口径；后续如支持重试发送，再加客户端消息 ID 唯一键 | 聊天服务测试 | 需要产品确认 |

## 首批候选排序

1. **WOG-2 内容互动关系写入与计数一致性**
   - 范围：`UserPostLike`、`UserCommentLike`、帖子 / 评论点赞计数、点赞奖励触发。
   - 理由：用户主路径高频，当前缺少用户-目标唯一索引，重复关系会放大为计数漂移、重复奖励和重复通知。
   - 建议方案方向：先评审唯一索引与历史重复数据清理，再改 Toggle 为唯一冲突可恢复流程，计数用条件更新或关系表回算守护。

2. **WOG-3 背包 / 权益发放可靠性**
   - 范围：`RetryGrantBenefitAsync`、`GrantBenefitAsync`、`GrantConsumableItemAsync`、`DeductItemAsync`、经验卡 / 萝卜币红包使用。
   - 理由：属于资产 / 权益链路，重复发放或先发奖励后扣减失败会造成真实资产偏差。
   - 建议方案方向：定义订单权益发放幂等键、背包用户-道具唯一键、条件增减数量和使用流水。

3. **WOG-4 奖励业务键唯一性**
   - 范围：互动萝卜币奖励、经验奖励、神评 / 沙发奖励。
   - 理由：奖励当前多处依赖读后写判断；并发任务或重复异步触发可能重复发放。
   - 建议方案方向：先统一奖励业务键，不直接扩完整经济系统或风控平台。

4. **WOG-5 管理覆盖类写入版本语义**
   - 范围：系统设置、Console 角色授权、商品编辑、内容审核。
   - 理由：多管理员并发会出现后写覆盖；现阶段不是最高频用户主路径，但上线前需要更清晰的覆盖语义。
   - 建议方案方向：评审版本号 / 最后修改时间确认，不直接给全部表加版本列。

5. **WOG-6 跨端幂等契约补齐**
   - 范围：Flutter 商城购买、未来 Flutter 转账 / 资产写入。
   - 理由：后端已支持幂等，但 Flutter 当前购买未传 key。
   - 建议方案方向：等 Web 默认入口和 API 契约稳定后，按跨端承接批次补齐。

## 当前不做

- 不启动完整钱包、经济系统、资产风控平台或开放 API 签名平台。
- 不把 Redis 分布式锁作为所有写操作的默认前置。
- 不把浏览器通用 `sign`、字段级加密、安全会话或设备绑定并入本批。
- 不一次性给所有写入口加幂等表或版本号。
- 不把前端按钮禁用、loading 或本地防抖写成服务端可靠性方案。
- 不把 Flutter 商城购买 key 补齐排到内容互动与背包 / 权益后端治理之前。

## 后续执行建议

下一步如果进入代码，应先做 `WOG-2 内容互动关系写入与计数一致性` 的方案确认。该候选涉及数据库唯一索引、历史数据清理、服务层并发处理和定向测试，不能直接开改。
