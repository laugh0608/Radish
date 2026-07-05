# P3-12-D62 圈子 / 宠物页面族首批实现记录

> 日期：2026-07-05（Asia/Shanghai）
> 范围：`radish.client` Private / Author `/circle`、`/pet`
> 设计源：`Docs/frontend/design-sources/private-web-workflows.pen`

## 背景

`P3-12-D62` 已完成 `/workbench`、`/me` 内容历史复访组、资产 / 订单 / 背包页面族和通知 / 消息页面族首批实现。按当前规划，下一顺位继续 Private / Author 设计源中的圈子 / 宠物页面族。

本批通过 Pencil MCP 只读抽查 `P14 - Circle Feed`、`P15 - Pet Care`、`P28 - Mobile Circle` 和 `P29 - Mobile Pet`。设计源强调：

- `/circle` 不是单纯关系列表，而是关注动态、关注 / 粉丝关系、当前页预览和公开帖子 / 公开主页路由边界。
- `/pet` 不是单纯照顾按钮列表，而是宠物状态、照护动作可用性、冷却 / 次数和公开资料边界。
- 移动端继续按单列任务流组织，优先展示当前上下文、状态摘要和下一步任务。

## 实现范围

### `/circle`

- 在原有动态 / 关注 / 粉丝 tab 基础上补关系上下文 rail。
- 使用现有 `getMyFollowSummary`、`getMyFollowingFeed`、`getMyFollowing` 和 `getMyFollowers` 结果生成当前范围、分页、总量、本页互动或互关指标。
- 增加当前页预览：
  - 动态页预览最近帖子，并保留公开帖子真实 `href`。
  - 关注 / 粉丝页预览当前页用户，并保留公开主页真实 `href`。
  - 公开目标继续使用既有来源转交，支持返回“我的圈子”。
- 增加公开路由边界说明和论坛、发现、我的状态入口。
- 桌面端使用内容列 + rail；窄屏和移动端将 rail 前置为单列任务上下文。

### `/pet`

- 在原有宠物 hero、状态洞察、三项状态、照护动作、资料和流水基础上补照护状态 rail。
- 使用现有 `resolvePetActionAvailability` 生成可执行、冷却中、已用完计数，不新增服务端协议。
- 增加照护优先级队列：
  - 按当前状态洞察的关注项优先，再按动作可用性和剩余次数排序。
  - rail 中的动作按钮继续复用原 `handleCare` 和 `idempotencyKey` 生成逻辑。
  - 冷却、次数用尽和执行中状态沿用既有动作可用性文案。
- 增加公开资料边界信息，展示公开状态和最近照顾时间，并保留返回 `/me` 的正式 Web 入口。
- 桌面端使用状态 / 操作主列 + rail；窄屏和移动端将 rail 前置到状态卡之前。

## 保持不变

- 不新增 API、权限、数据库结构或后端事件。
- 不修改圈子 tab、分页、公开帖子 / 公开主页来源返回语义。
- 不修改关注 / 取关、宠物领取、资料保存、照顾动作、幂等键、冷却或每日次数契约。
- 不启动推荐算法、ActivityPub / WebFinger、短动态、宠物经济、商城物品、社区任务奖励或 Console 宠物配置。

## 验证

- `npm run type-check --workspace=radish.client`：通过。
- `npm run build --workspace=radish.client`：通过。
- `npm run lint:changed`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

本批尚未执行 Gateway PC / mobile 真实页面 smoke。按协作规则，真实 smoke 需要在执行前由用户在本轮明确确认前后端已启动；当前先完成代码侧与静态验证。

## 后续

- 继续 `P3-12-D62 Private / Author` 论坛作者态和 Docs 作者态页面族。
- 圈子 / 宠物后续若需要扩展产品能力，应先补专题设计和接口边界，不在当前页面对齐批次中直接扩大范围。
