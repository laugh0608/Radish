# P3-12-D13 radish.client 私域 / 作者态第二批视觉收口检查记录

日期：2026-06-29

## 背景

`P3-12-D9` 至 `P3-12-D12` 已连续完成 `radish.client` 私域交易数据面、通知 / 消息、圈子 / 宠物、论坛作者态和 Docs 作者态的第二批视觉实现。本记录用于在进入真实 Gateway PC / mobile 阶段验收前，先收口静态一致性、共享状态槽边界和任务卡视觉节奏。

本批只做 D9-D12 视觉实现后的收口检查，不扩大到 Console 视觉代码实现，也不修改资产、商城、通知、消息、圈子、宠物、论坛或 Docs 的后端契约。

## 收口结论

- 通知、消息、圈子和宠物入口的空态 / 加载 / 错误状态统一由共享 `WebStateSlot` 承担卡片视觉，不再由页面外层 `stateShell` 叠加边框、背景、圆角和阴影。
- 论坛发帖作者摘要与 Docs 作者台摘要统一回到 `8px` 卡片半径，与 D9-D11 私域任务卡和项目当前视觉规则保持一致。
- 本批未修改通知中心、聊天协议、SignalR 连接、圈子关注关系、公开来源返回、宠物动作幂等、论坛发布器、提交幂等、Docs 保存 / 修订、Markdown 编辑器、权限判断或后端接口。
- D9-D12 第二批视觉实现已具备进入阶段验收的静态前置条件；真实 Gateway PC / mobile smoke 仍需等用户明确说明前后端已启动后执行。

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

未执行真实 Gateway PC / mobile smoke。该项需要先提醒用户启动前后端，并等待用户明确说明服务已经启动。

## 后续事项

- 准备 D9-D13 作为一个成组阶段做真实 Gateway PC / mobile 页面复核。
- 阶段验收通过后，再判断是否进入 Console 视觉代码实现，或先补 `radish.client` 中真实验收暴露的同类问题。
- 若实机联调命中状态槽、移动单列、任务摘要或页面入口问题，优先按共享组件和专题设计口径收敛，避免继续在单页面分叉实现。
