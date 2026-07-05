# P3-12-E3-C 通知行动队列深化记录

> 日期：2026-07-05（Asia/Shanghai）
>
> 范围：`Frontend/radish.client`。本批不新增后端 API、数据库、权限或通知偏好契约。

## 目标

把 `/notifications` 从“近期通知时间线 + 简单目标分流”继续推进为社区复访行动队列，让用户能按帖子、评论、问答、聊天、关注、治理、订单、Docs、宠物和成长反馈理解待处理事项；同时补清无 Web 目标通知的回看口径，避免点击无反馈。

## 实现摘要

- `notificationActionQueue.ts` 新增帖子、问答 scope，并把评论、聊天、关注、治理、订单等分类继续集中到同一 helper。
- 新增 `buildNotificationActionGroups()`，按行动域生成分组队列，并统计未读、可跳转和需手动查看数量。
- 新增 `getNotificationTargetHintKey()`，对治理、系统和目标缺失通知给出不同提示，避免统一显示“无目标”。
- `/notifications` 右侧队列从平铺最近 4 条改为按行动域分组；有目标的通知继续跳转，无目标通知回到通知中心上下文。
- 通知范围 chip 改为复用 `getNotificationActionScope()` 统一计数，避免页面内硬编码 matcher 与 Workbench / helper 漂移。
- 右栏路由规则补充关注与治理说明；移动端取消三列右栏，保证分组后的可读性。

## E1 缺口收口

| E1 发现 | 本批结论 | 证据 | 用户影响 | 后续批次 | 验证口径 |
| --- | --- | --- | --- | --- | --- |
| 通知中心只是时间线，未按社区任务组织 | 发布前建议已完成首批 | `NotificationsApp.tsx` 使用 `buildNotificationActionGroups()` 渲染评论、问答、消息、关注、治理、订单等分组 | 用户能先按待回复、待查看、待治理解释、交易 / 权益变化处理通知，而不是扫描纯时间线 | `E3` 继续通知偏好、归档和打扰控制设计；`E6` 做旅程级进入判断 | `notificationNavigation.test.ts` 分组测试；Gateway PC / mobile 页面复核 |
| 发帖 / 评论 / 问答未从通知行动域拆清 | 发布前建议已完成首批 | `notificationActionQueue.ts` 新增 `posts`、`answers` scope，并保留 `comments` 独立分类 | 社区内容生产和讨论参与在通知入口有独立识别，方便后续做待回复 / 待采纳 / 待编辑队列 | `E3 / E5` 后续补发帖失败恢复、草稿、上传失败和问答旅程 smoke | 单测覆盖帖子动态与问答采纳分类 |
| 通知目标缺失时点击无反馈，用户不知道下一步 | 发布前建议已完成首批 | 无目标队列项改为 `#notification-center`，并通过 `getNotificationTargetHintKey()` 展示治理 / 系统 / 目标恢复提示 | 用户不会误以为通知可直接打开；可回到通知中心查看上下文和已读 / 删除动作 | `E5` 继续错误恢复和诊断编号，`E4` 审计治理证据链 API 是否需要补目标回看 | 单测覆盖治理无目标提示；浏览器复核右栏文案与布局 |
| Workbench 与通知页可能出现分类口径漂移 | 已满足当前批次 | `NotificationsApp.tsx` 顶部 chip 和右栏队列都通过 `getNotificationActionScope()` 计算 | 用户在 Workbench 和通知中心看到的行动类型一致 | 后续新增通知类型时继续先扩展 helper 和测试 | `npm run test --workspace=radish.client` |
| 通知偏好、归档、静音和打扰控制未产品化 | 后置专题但不阻断发布 | 本批未新增偏好 API；E3-B / E3-C 已先完成关键复访任务分类和回跳 | 首发用户能处理核心社区复访任务；缺少个性化打扰控制影响长期留存体验，但不阻断正式版首发闭环 | 独立通知偏好 / 归档专题，需先定义后端契约、默认值和设置页真实保存能力 | E1 矩阵保留；发布前确认设置页不展示假保存 |

## 不纳入本批

- 不新增通知偏好、归档、静音、批量已读或批量删除 API。
- 不改通知 Hub、后端通知类型、推送策略或移动系统通知。
- 不把治理证据链的原内容回看 API 合入本批；当前只改善无目标通知的用户提示和回看入口。
- 不把完整聊天平台、私聊、搜索或消息反应并入通知队列批次。

## 验证

- `npm run test --workspace=radish.client`
- `npm run build --workspace=radish.client`
- `npm run check:repo-hygiene:changed`
- `git diff --check`
- Gateway 运行态复核：已尝试通过应用内浏览器访问 `https://localhost:5000/notifications`，因应用内浏览器无登录态被 Auth 登录页拦截，未形成通知页本体 PC / mobile 结论；不属于前后端启动问题，后续登录态就绪后补跑。

后续补验：

- Gateway PC / mobile 页面复核（应用内浏览器或 Chrome 已登录后）
