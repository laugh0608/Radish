# P3-11-B 轻量复访缺口只读审计记录

> 日期：2026-06-21（Asia/Shanghai）
>
> 范围：`P3-11-B` 轻量复访缺口只读盘点
>
> 结论：未命中需要立即进入代码回修的 `P0/P1` 或高信号阻断；`P3-11-C` 暂不启动。

## Git 范围

- 分支：`dev`
- 审计时 HEAD：`0e0cccd8 docs(planning): 启动 P3-11 发布候选整备`
- `master..dev`：`50` 个提交
- 工作区：审计开始时干净；`46b9e8b2` 之后新增影响面为 P3-11 文档启动提交，未发现运行时代码、配置或验证数据变化。

## 审计目标

本轮只读筛查 P3-11 允许的轻量复访候选，不追加新功能，不做 P3-10-D 第五批链接扫尾，不执行真实页面 smoke。

覆盖面：

- 纯 Web 登录后入口：`/notifications`、`/messages`、`/me`、`/circle`。
- 论坛详情复访：公开详情到登录后评论 / 快速回应参与、评论定位、来源返回和评论提交重试状态。
- Flutter 成熟工作流：论坛发帖、评论 / 回复、问答回答、作者帖子编辑、作者评论编辑和单商品购买重试。
- 现有测试资产：路由白名单、公开论坛详情参数、`/circle` 路由、Flutter 论坛 / 商城幂等测试。

## 只读审计结论

### 纯 Web 私域入口

- `authReturnPath` 已对 `/notifications`、`/messages`、`/me`、`/circle` 做明确白名单：
  - `/notifications` 不接受额外 query/hash，避免通知入口状态漂移。
  - `/messages` 仅在合法 `channelId` 存在时保留 `messageId`。
  - `/me` 固定为个人状态聚合入口，不保留未设计的 `tab` 状态。
  - `/circle` 只保留 `feed / following / followers` 与正整数页码。
- `NotificationsApp`、`MessagesApp`、`MeApp`、`CircleApp` 均有路径规范化、登录态初始化和未登录回跳；未发现私域入口会把非法参数带入登录恢复。
- 通知目标分流已覆盖聊天、论坛详情、公开个人页和商城订单工作台入口；论坛 / 个人页来源转交仍按公开壳层语义登记。
- `/me` 最近访问只为公开详情页生成可返回来源状态，不把普通公开浏览页误标为详情返回。
- `/circle` 关注动态、关系链用户、tab 和分页均保留真实 `href`，普通点击仍保持当前壳层语义。

### 论坛详情复访

- 公开论坛详情路由保留大整数帖子 ID、`pst_...` PublicId、`commentId` 与 `intent=comment|quickReply`。
- 评论 / 快速回应登录动作会生成明确 return path；存在来源状态时会随登录回跳登记。
- 评论提交使用 `clientSubmissionId`，失败后保留提交意图，成功后清空；评论定位失败时保留可感知提示并继续打开帖子详情。
- 公开详情轻回应仍沿用既有接口，不在本轮扩展为 `clientSubmissionId` 写入治理范围；如后续出现重复轻回应真实问题，应单独评估接口契约，而不是在 P3-11-B 中顺手改动。

### Flutter 成熟工作流

- Flutter 论坛仓储接口已要求发帖、评论 / 回复、问答回答、帖子编辑、评论编辑携带 `clientSubmissionId`，并拒绝空提交意图 ID。
- Flutter 论坛列表发帖在登录前保留草稿，登录后返回表单继续发布；失败后同一草稿重试复用 `forum-post:` 提交意图。
- Flutter 论坛详情回答、评论 / 回复、帖子编辑和评论编辑均按用户、目标和内容生成提交指纹；失败重试复用同一 `forum-answer:`、`forum-comment:`、`forum-post-edit:` 或 `forum-comment-edit:` ID，成功后清空。
- Flutter 单商品购买已生成 `shop:` 幂等键，失败后继续停留详情页并复用同一 key 重试；仓储层拒绝空幂等键。

## 已参考的验证资产

- Web 路由 / 回跳测试：
  - `Frontend/radish.client/tests/authReturnPath.test.ts`
  - `Frontend/radish.client/tests/forumNavigation.test.ts`
  - `Frontend/radish.client/tests/publicRouteState.test.ts`
  - `Frontend/radish.client/tests/circleRouteState.test.ts`
- Flutter 论坛 / 商城测试：
  - `Clients/radish.flutter/test/forum_page_test.dart`
  - `Clients/radish.flutter/test/forum_detail_page_test.dart`
  - `Clients/radish.flutter/test/shop_product_detail_page_test.dart`

## 未执行项与剩余风险

- 本轮未执行 Gateway / 浏览器真实页面 smoke；按项目规则，真实页面复核前必须先由用户明确确认前后端已启动，本轮未要求也未沿用历史启动状态。
- 本轮未重跑 Flutter 全量测试、Android 真机回退链路或后端完整 baseline；当前结论来自代码只读审计和既有测试资产。
- 私域入口的真实账号数据、通知样本和聊天频道样本仍需在恢复 PR 或发布候选整备时通过运行态复核确认。
- 公开详情轻回应接口暂不具备与评论相同的 `clientSubmissionId` 治理；当前未判定为阻断，但应作为后续真实缺口触发条件保留。

## 决策

- `P3-11-B` 可视为完成。
- 当前不建议启动 `P3-11-C` 代码回修批次。
- 下一步应进入 `P3-11-D`：刷新 `master..dev` 范围，结合 P3-11-A 验收矩阵和本记录，决定是否恢复 `dev -> master` PR 准备或继续等待发布候选决策。
