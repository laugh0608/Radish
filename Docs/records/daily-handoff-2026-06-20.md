# 2026-06-20 收工回顾与明日事项

## 今日提交回顾

`git log --since="2026-06-20 00:00 +0800"` 在本记录提交前回顾到今日 14 个提交；本记录自身作为收工文档提交追加。

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `788e193e docs(planning): 完成写操作分级盘点` | WOG / 规划 | 输出写操作分级矩阵和首批候选排序，明确资产 / 库存、奖励 / 玩法、内容互动、管理治理、通知 / 消息五类入口的治理判断。 |
| `88ab277b docs(planning): 输出内容互动一致性方案` | WOG-2 / 方案 | 明确帖子 / 评论点赞关系唯一约束、计数条件更新、历史重复清理和奖励触发边界。 |
| `47914264 fix(forum): 加固点赞关系与计数一致性` | WOG-2 / 内容互动 | 论坛点赞关系与计数一致性完成首批代码治理，补迁移入口和定向测试。 |
| `8d729b04 fix(shop): 加固背包权益发放一致性` | WOG-3 / 背包权益 | 订单权益发放幂等、背包聚合唯一键、消耗品发放流水和条件扣减完成首批治理。 |
| `348e38f5 feat(rewards): 增加奖励业务键去重治理` | WOG-4 / 奖励 | 奖励流水与经验流水业务键、互动奖励和神评 / 沙发奖励去重完成首批治理。 |
| `8a278f92 docs(wog): 补充管理覆盖版本语义方案` | WOG-5 / 方案 | 明确系统设置、Console 授权、商品管理和内容审核的管理覆盖版本语义。 |
| `34b1fea2 feat(governance): 增加管理写入版本语义` | WOG-5 / 管理治理 | 系统设置覆盖版本、Console 授权旧快照拦截、商品版本条件更新和内容审核状态条件更新落地。 |
| `6546772d docs(wog): 输出跨端幂等契约方案` | WOG-6 / 方案 | 明确 Flutter 单商品购买幂等 key 承接方式和不扩展范围。 |
| `a8818131 feat(flutter): 接入商城购买幂等键` | WOG-6 / Flutter 商城 | Flutter 单商品购买生成并提交 `shop:` 幂等 key，失败重试复用同一 key。 |
| `7c5ff0ef feat(forum): 接入内容提交意图治理` | 论坛发布可靠性 | 新增 `ContentSubmissionRecord`，Web 发帖、评论、回答创建链路接入 `clientSubmissionId`、请求摘要和短窗口内容指纹。 |
| `de032ff4 feat(forum): 接入编辑重试幂等治理` | 论坛编辑可靠性 | Web 帖子编辑和评论编辑接入提交意图记录；编辑无变化不写历史、不递增编辑次数。 |
| `94183d74 feat(flutter): 接入论坛写入口提交意图` | Flutter 论坛 | Flutter 原生纯文本发帖、根评论 / 回复生成并复用 `clientSubmissionId`。 |
| `f304994b feat(forum): 接入创建类写入限频治理` | 论坛频率治理 | 发帖、同帖评论 / 回复和同帖回答基于近期成功提交记录做短窗口频率限制。 |
| `55cf4c28 feat(flutter): 承接论坛问答回答入口` | Flutter 问答 | Flutter 识别问答帖并接入回答提交，回答失败重试复用 `forum-answer:` 提交意图 key。 |

## 文档同步复核

- 已同步规划入口：[当前进行中](/planning/current) 已记录论坛内容发布可靠性当前确认范围完成，并把明日事项切到 Flutter 帖子编辑 / 评论编辑入口评审或下一批 P3-10 产品 / 治理增量重新排序。
- 已同步路线总览：[开发路线图](/development-plan) 已把 WOG 首轮、论坛发布可靠性、Flutter 论坛发帖 / 评论 / 回答承接写入最近结论和下一顺位。
- 已同步 P3-10 专题：[P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture) 已从论坛发布可靠性首批创建链路，更新为论坛发布可靠性与 Flutter 回答承接完成。
- 已同步治理说明：[写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance) 和 [论坛内容发布可靠性与编辑历史治理](/guide/forum-content-write-reliability-governance) 已覆盖 WOG 首轮、`ContentSubmissionRecord`、`clientSubmissionId`、创建类频率限制和 Flutter 当前写入口承接口径。
- 已同步 Flutter 说明：[Flutter 移动端交接说明](/guide/flutter-mobile-handoff) 与 `Clients/radish.flutter/README.md` 已记录 Flutter 当前承接范围和不做范围。
- 本次收工补同步开发日志：[2026 年 6 月第 3 周开发日志](/changelog/2026-06/week3)、[2026 年 6 月开发日志](/changelog/2026-06)、[2026 年开发日志](/changelog/2026) 和 [记录与验收索引](/records/)。
- 已复核无需跟随更新范围：今天没有新增 Pencil / 设计源文件要求；没有启动 Flutter 转账、完整移动商城、服务端强制资产写入口 key、独立频率限制平台、完整反垃圾系统、完整审核平台、完整钱包、Redis 分布式锁平台或 WebOS 新功能承接。

## 今日验证

WOG 与论坛可靠性代码批次已按影响面完成：

- `dotnet test Radish.Api.Tests`
- 相关后端定向测试
- `dotnet build Radish.slnx -c Debug`
- `npm run build --workspace=radish.client`
- `flutter test test/forum_page_test.dart test/forum_detail_page_test.dart`
- `flutter analyze`
- `flutter test`
- `git diff --check`
- `npm run check:repo-hygiene:changed`

收工文档批次执行：

- `npm run check:repo-hygiene:changed`
- `git diff --check`
- `npm run check:repo-hygiene:staged`
- `git diff --cached --check`

运行态说明：

- 今天未启动前后端服务，未执行真实 smoke。
- 后续真实 smoke 不沿用历史启动状态；只有用户在新会话明确说明前后端已经启动时，才执行 Gateway / 浏览器 PC + 移动复核。

## 明日事项

1. 先读取 [当前进行中](/planning/current)、[论坛内容发布可靠性与编辑历史治理](/guide/forum-content-write-reliability-governance)、[写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)、[论坛帖子/评论编辑历史设计与实现](/features/forum-edit-history) 和本记录，确认 WOG 首轮、论坛发布可靠性当前确认范围、Flutter 发帖 / 评论 / 回答承接均已完成。
2. 第一顺位：评审 Flutter 帖子编辑 / 评论编辑入口是否值得新增产品能力承接；评审时先盘点 Flutter 是否已有编辑产品入口、权限与失败态、`clientSubmissionId` 字段复用方式、失败重试 key 复用规则、成功后刷新入口和测试入口。
3. 如果 Flutter 编辑入口当前不适合立即实现，就切回 P3-10 后续产品 / 治理候选池，优先选择边界清楚、能降低真实风险、不会扩成大专题的候选项。
4. 明确不做范围：不扩展 Flutter 转账、完整移动商城、服务端强制资产写入口 key、独立频率限制平台、完整移动能力套件、回答采纳、富文本 / 附件创作器、完整反垃圾系统或完整审核平台。
5. 若触达架构、接口、数据库结构、运行时行为或范围不清的改动，先说明方案并等待确认；若只是文档盘点或现状说明，可直接同步。
6. 真实 smoke 只有在用户明确说明前后端已经启动后执行；开发中优先使用构建、测试、类型检查、`git diff --check` 和仓库卫生检查。
