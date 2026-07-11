# P3-12-E3-B Workbench 社区活动中心与通知行动队列首批补强

> 日期：2026-07-05（Asia/Shanghai）
>
> 范围：`Frontend/radish.client`。本批不新增后端 API、数据库、权限或路由契约，不执行真实 Gateway smoke。

## 目标

把 `/workbench` 从静态功能地图推进为社区活动中心首批实现，让帖子、聊天、关注、通知、订单 / 权益、宠物 / 经验和治理结果能按真实状态进入“今日队列”；同时把通知页的行动队列分类抽成可复用口径，避免 Workbench 与通知中心各自维护分流规则。

## 实现摘要

- 新增 `src/notifications/notificationActionQueue.ts`，统一通知 VO 到 store 项映射、通知预览生成、Web 目标解析和行动 scope 分类。
- `/notifications` 复用同一套分类，scope 从评论 / 订单 / 文档 / 系统扩展到消息、关注、宠物、成长和治理。
- `/workbench` 登录态水合后并行读取未读通知、近期通知和聊天频道列表，更新通知 / 聊天 store，并读取本地论坛发帖草稿和聊天草稿。
- Workbench 今日队列优先展示真实待处理事项：未读通知、@我 / 未读消息、论坛草稿、订单 / 权益变化、关注动态、宠物 / 成长反馈和治理结果；数量不足时再补正式 Web 基础入口。
- Workbench 右侧路径状态改为活动指标：通知、消息、草稿和同步状态；接口部分失败时标记“部分同步”，但保留正式 Web 入口可用。

## E1 缺口收口

| E1 发现 | 本批结论 | 证据 | 用户影响 | 后续批次 | 验证口径 |
| --- | --- | --- | --- | --- | --- |
| Workbench 只是静态入口导航，未承接社区复访任务 | 发布前建议已完成首批 | `WorkbenchApp.tsx` 接入 `notificationApi.getUnreadCount`、`getMyNotifications`、`getChannelList`、论坛 / 聊天草稿读取和动态队列 | 登录后能先看到未读、@我、草稿、订单 / 权益、关注、宠物 / 成长和治理提醒，再进入对应正式 Web 路径 | `E3-C` 继续深化通知行动队列；`E5` 做运行态恢复和弱网验证 | `radish.client` test / type-check / build；后续 Gateway PC / mobile 旅程级 smoke |
| 通知分类分散在页面内，范围偏窄 | 发布前建议已完成首批 | `notificationActionQueue.ts` 统一 `messages/orders/docs/follow/pet/experience/governance` 等 scope，`NotificationsApp.tsx` 复用 | 通知页和 Workbench 使用同一套行动分类，避免用户在不同入口看到不一致的任务类型 | `E3-C` 增加可归档、已处理、偏好和目标缺失反馈 | `notificationNavigation.test.ts` 新增分类和 VO 映射测试 |
| 聊天 / @我未进入 Workbench 复访中心 | 发布前建议已完成首批 | Workbench 读取 `ChatStore.channels` 和 `getChannelList()`，按 `voUnreadCount` / `voHasMention` 生成消息队列 | 用户能从工作台优先回到被提及频道或未读会话 | `E3-C / E5` 后续补消息定位运行态 smoke 和 Hub 恢复验证 | 类型检查、构建；后续真实 Gateway 登录态复核 |
| 创作草稿未进入社区活动中心 | 发布前建议已完成首批 | Workbench 读取 `forum_post_draft` 和 `radish.chat.drafts.v1` | 用户从工作台能继续发帖草稿或消息草稿，减少创作中断 | `E5` 后续覆盖上传失败 / 发布失败草稿保留旅程 | 静态测试与人工本地草稿复核 |
| 宠物、经验、订单 / 权益与社区复访关系弱 | 后置专题但不阻断发布 | 本批已按通知 scope 把 pet / experience / orders 映射到 Workbench；深层激励规则仍由后续专题定义 | 首发可以把相关反馈作为复访提醒展示；完整任务奖励和经济闭环不是当前正式版阻断项 | `E3 / E4` 继续审计激励默认值、资产边界和法务承诺 | E1 矩阵跟踪，发布候选前覆盖代表旅程 |
| 通知偏好、归档和打扰控制未完整产品化 | 后置专题但不阻断发布 | 本批只做行动分类和回跳队列，不新增偏好 API 或设置保存 | 用户能处理关键复访任务；无法自定义通知打扰是发布前体验建议，不阻断首发社区闭环 | `E3-C` 或独立通知偏好专题 | 需先补产品范围和后端契约，再做验证 |

## 不纳入本批

- 不新增通知偏好、归档、静音或批量处理 API。
- 不改聊天 Hub 生命周期，不在 Workbench 额外启动实时连接，避免与消息页 / 通知页连接管理互相干扰。
- 不新增宠物 / 经验 / 订单业务通知类型；当前先从已有通知 `businessType`、`type`、标题、内容和目标路由做发布前行动分类。
- 不执行 Gateway 真实页面 smoke；本轮用户未声明前后端已启动，按 E 期普通开发批次使用静态验证。

## 验证

- `npm run test --workspace=radish.client`
- `npm run type-check --workspace=radish.client`

后续提交前继续执行：

- `npm run build --workspace=radish.client`
- `npm run check:repo-hygiene:changed`
- `git diff --check`
