# P3-12-D13 radish.client 私域 / 作者态第二批视觉收口检查记录

日期：2026-06-29

## 背景

`P3-12-D9` 至 `P3-12-D12` 已连续完成 `radish.client` 私域交易数据面、通知 / 消息、圈子 / 宠物、论坛作者态和 Docs 作者态的第二批视觉实现。本记录用于收口静态一致性、共享状态槽边界、任务卡视觉节奏，并记录 D9-D13 成组 Gateway PC / mobile 实机联调结论。

本批只做 D9-D12 视觉实现后的收口检查，不扩大到 Console 视觉代码实现，也不修改资产、商城、通知、消息、圈子、宠物、论坛或 Docs 的后端契约。

## 收口结论

- 通知、消息、圈子和宠物入口的空态 / 加载 / 错误状态统一由共享 `WebStateSlot` 承担卡片视觉，不再由页面外层 `stateShell` 叠加边框、背景、圆角和阴影。
- 论坛发帖作者摘要与 Docs 作者台摘要统一回到 `8px` 卡片半径，与 D9-D11 私域任务卡和项目当前视觉规则保持一致。
- 本批未修改通知中心、聊天协议、SignalR 连接、圈子关注关系、公开来源返回、宠物动作幂等、论坛发布器、提交幂等、Docs 保存 / 修订、Markdown 编辑器、权限判断或后端接口。
- D9-D13 成组 Gateway PC / mobile smoke 已完成，当前未发现阻断级页面问题。

## 代码范围

- `Frontend/radish.client/src/notifications/NotificationsApp.module.css`
- `Frontend/radish.client/src/messages/MessagesApp.module.css`
- `Frontend/radish.client/src/circle/CircleApp.module.css`
- `Frontend/radish.client/src/pet/PetApp.module.css`
- `Frontend/radish.client/src/public/forum/PublicForumApp.module.css`
- `Frontend/radish.client/src/docs/DocsAuthorApp.module.css`

## 验证记录

- `npm run build --workspace=radish.client`：通过
- `npm run check:repo-hygiene:changed`：通过
- `git diff --check`：通过

### Gateway PC / mobile smoke

执行条件：用户已明确说明前后端服务已启动。

工具与账号：

- 工具：Codex 内置 Browser 插件
- Gateway：`https://localhost:5000`
- 登录账号：本地开发种子 `admin@radishx.com / admin123456`
- PC 视图：`1920x1080 @ DPR 1`
- 移动视图：`390x844 @ DPR 3`

覆盖入口：

- `/workbench`
- `/me/assets`
- `/me/assets/transactions`
- `/shop/orders`
- `/shop/inventory`
- `/notifications`
- `/messages`
- `/circle`
- `/pet`
- `/forum/compose`
- `/docs/mine`

结论：

- PC 与移动视图均能通过 Gateway 正常渲染，关键页面未出现空白页、加载卡死、控制台错误或页面级横向溢出。
- 未登录访问 `/me/assets`、`/me/assets/transactions`、`/shop/orders` 会进入 `Radish 统一登录`，使用开发种子账号登录后可回流到 `/shop/orders`。
- `/me/assets` 与 `/me/assets/transactions` 展示真实资产余额和注册奖励流水；PC / mobile 均无横向溢出。
- `/shop/orders` 当前本地账号无订单，验证到订单空态；未覆盖订单详情页，因为页面没有可进入的真实订单链接。
- `/shop/inventory` 当前为空态，PC / mobile 均可渲染；移动端已验证“道具”tab 可切换。
- `/notifications` 当前为空态，PC / mobile 均可渲染；移动端已验证“未读”筛选可点击。
- `/messages` 可进入公共闲聊频道，历史消息加载完成后进入空消息态；未发送消息，避免写入数据。
- `/circle` 当前关注动态为空态，PC / mobile 均可渲染；移动端已验证“关注”tab 可切换到 `/circle?tab=following`。
- `/pet` 当前账号处于首次领取态；未执行领取宠物动作，避免写入数据。
- `/forum/compose` 与 `/docs/mine` 在 `Admin` 登录态下均可进入作者态页面，作者任务摘要、设置区和列表区在 PC / mobile 视图下无横向溢出。

## 后续事项

- D9-D13 作为一个成组阶段已完成真实 Gateway PC / mobile 页面复核。
- 下一步根据整体计划判断是否进入 Console 视觉代码实现，或先补 `radish.client` 中后续真实使用暴露的同类问题。
- 若实机联调命中状态槽、移动单列、任务摘要或页面入口问题，优先按共享组件和专题设计口径收敛，避免继续在单页面分叉实现。
