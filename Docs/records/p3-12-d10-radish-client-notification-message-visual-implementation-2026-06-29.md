# P3-12-D10 radish.client 通知与消息任务面视觉实现记录

> 日期：2026-06-29（Asia/Shanghai）
>
> 范围：`radish.client` 通知 / 消息私域任务面视觉实现

## 背景

`P3-12-D9` 已完成资产、订单、订单详情和背包等交易数据面。按 [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design)，下一顺位应继续处理通知 / 消息、圈子 / 宠物、论坛作者态和 Docs 作者态。

本批先收口通知 / 消息这组即时反馈链路；不重写 `NotificationCenter`、`ChatApp`、SignalR 启停、通知跳转协议或聊天发送协议。

## 实现范围

- `/notifications`
  - 增加通知任务摘要区，展示未读数、近期通知数和回跳链路状态。
  - 登录 / 初始化状态放入入口级 `WebStateSlot` 容器。
  - 页面宽度、高度和移动端布局改为 Web 私域任务面节奏。
- `/messages`
  - 增加消息任务摘要区，展示频道数、聚焦会话状态和实时连接状态。
  - 登录 / 初始化状态放入入口级 `WebStateSlot` 容器。
  - 保留 `ChatApp` 内部会话、成员、发送、图片上传和公开个人页跳转逻辑。
- 国际化
  - 补充通知 / 消息任务摘要的中英文文案键，避免入口文案硬编码。

## 验证

```bash
npm run build --workspace=radish.client
```

结果：通过。

本批尚未执行仓库卫生检查、`git diff --check` 和真实 Gateway PC / mobile smoke。真实 smoke 仍按阶段验收或用户明确要求执行，并需用户先确认前后端已启动。

## 后续顺位

1. 继续 `radish.client` 第二批视觉实现：圈子 / 宠物、论坛作者态和 Docs 作者态。
2. 批次收口前补 `npm run check:repo-hygiene:changed` 与 `git diff --check`。
3. 准备阶段验收时，在用户确认前后端已启动后再补 Gateway PC / mobile 页面复核。
