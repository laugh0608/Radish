# P3-8-D Flutter 下一批主路径候选矩阵（2026-05-28）

> 本记录用于衔接 `P3-8-D 多端主路径缺口补齐与验收矩阵`。本批只做候选评估与下一步排序，不直接修改 Flutter 运行时代码。

## 评估依据

- 当前总路线已收敛为 `纯 Web + Flutter` 主线；WebOS `/desktop` 仅保留为历史入口和迁移线，PC / Tauri 后置。
- 移动 Web 公开视图矩阵已阶段收口，后续除公开访问、分享预览、购买 / 订单 / 背包、权限授权等阻断外，不再逐页小循环式打磨 Web 公开页。
- Flutter 已完成 `discover / forum / docs / profile / leaderboard / shop` 的首批公开只读与关键来源返回小闭环，下一批应从真实移动复访缺口中只选一个一天级闭环。
- 当前仍处于产品建设期，不能把“等待真实使用观察”作为默认主线；但也不应启动完整移动商城、完整通知中心、完整创作器或移动版 WebOS。

## 当前 Flutter 能力快照

- 已具备匿名公开阅读：发现、论坛、文档、榜单、公开主页、公开商品只读详情。
- 已具备登录与回流：OIDC 回调、会话恢复、论坛轻回应登录回流、公开主页来源返回。
- 已具备复访入口：我的轻回应、最近论坛阅读、最近公开帖子 / 评论、最近文档。
- 已具备最小通知回流：已登录态读取最新一条可跳 forum 的通知，并打开原生 forum detail。
- 尚未具备：多条通知列表、完整通知中心、系统通知栏推送、完整移动商城、订单 / 背包、创作器、聊天、治理工作台。

## 候选矩阵

| 候选 | 主路径价值 | 实现边界 | 风险 | 结论 |
| --- | --- | --- | --- | --- |
| Flutter 轻量 forum 通知列表 | 高。通知是登录用户从移动端复访帖子 / 评论上下文的自然入口，当前只展示最新一条，容易丢失多条上下文。 | 只展示最近少量可跳 forum 通知，点击复用既有 `ForumDetailHandoffTarget` 打开帖子 / 评论；不做已读、删除、分类筛选、系统推送和完整通知中心。 | 中。需要扩展 Flutter notification repository contract 和壳层 UI，属于运行时行为变更，应先说明方案并获批后实现。 | **推荐下一批实现** |
| Flutter 商品详情后的购买 / 订单 / 背包 | 中。商品只读详情之后的下一步确实是消费闭环。 | 会跨到完整移动商城、支付口令、权益激活、订单和背包状态承接。 | 高。容易突破当前“不启动完整移动商城”的边界。 | 暂不进入实现，只保留维护线阻断评估 |
| 纯 Web 登录后轻量链路继续补强 | 中。Web 公开与工作台回流已有多轮闭环，仍可处理暴露出的购买 / 订单 / 背包阻断。 | 仅处理真实暴露的回流断点。 | 低到中。继续默认推进会回到 Web 小循环。 | 作为备选，不抢占 Flutter 下一批 |
| Profile / Docs / Forum 继续微调 | 低。已覆盖来源返回、复访、搜索、详情阅读和轻回应回流。 | 多为局部文案、密度或边界优化。 | 低，但收益下降。 | 不作为主线 |

## 推荐下一批

推荐进入下一批实现的是：`P3-8-D Flutter 轻量 forum 通知列表`。

建议实现边界：

- 在 Flutter 已登录壳层中，把当前“最新 forum notification”能力扩展为最近 `3-5` 条可跳 forum 通知。
- 列表项展示通知标题和目标上下文；点击后打开原生 forum detail，并保留打开前 tab，返回后回到原位置。
- 继续复用 `/api/v1/Notification/GetNotificationList` 与现有 forum notification payload 解析，不新增后端接口。
- 仅处理 `app=forum` 且能解析 `postPublicId / postId` 的通知； malformed 或非 forum 通知继续跳过。
- 不做完整通知中心、已读 / 删除、通知设置、系统通知栏推送、聊天通知、商城通知或后台治理通知。

需要先确认的原因：

- 该批次会改变 Flutter `NotificationRepository` 的公开契约，从单条 `latestForumTarget` 扩展到列表能力。
- 该批次会调整 Flutter 壳层已登录状态区 UI，属于用户可见运行时行为变更。

## 验证建议

进入实现后，最小验证应覆盖：

```powershell
cd Clients/radish.flutter
flutter test
flutter analyze
```

文档和仓库卫生验证：

```powershell
npm run check:repo-hygiene:changed
git diff --check
```

