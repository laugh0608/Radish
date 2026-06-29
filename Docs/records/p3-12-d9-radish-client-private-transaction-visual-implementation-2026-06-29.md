# P3-12-D9 radish.client 私域交易数据面视觉实现记录

> 日期：2026-06-29（Asia/Shanghai）
>
> 范围：`radish.client` 私域交易数据面第二批视觉实现

## 背景

`P3-12-D8` 已完成 `radish.client` 共享壳层、状态槽、公开内容宽度 token 和移动底部留白首批代码对齐。按 [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design)，下一步优先处理资产、订单和背包等真实数据面，避免私域页面停留在旧式独立容器和桌面压缩移动布局。

本批不启动 Console、Flutter、后端接口、WebOS 形态迁移或真实 Gateway smoke。

## 实现范围

- `/me/assets` 与 `/me/assets/transactions`
  - 资产流水加载、错误和空态改为复用 `WebStateSlot`。
  - 保留原有余额摘要、筛选、分页和刷新动作。
- `/shop/orders`
  - 订单列表页头改为私域数据面节奏，返回按钮使用图标按钮形态。
  - 订单列表增加摘要条、图片缺省图标、键盘激活和共享空态 / 加载状态。
  - 移动端按单列任务流展示页头、摘要、订单卡和分页。
- `/shop/order/:orderId`
  - 加载与未找到状态改为 `WebStateSlot`。
  - 订单详情页头、状态块、商品卡片和动作区对齐 `rx-*` token。
  - 移动端商品信息、状态和取消动作改为稳定单列。
- `/shop/inventory`
  - 背包页头、权益 / 道具摘要和 tab 样式对齐私域卡片节奏。
  - 权益 / 道具空态改为 `WebStateSlot`。
  - 移动端卡片动作、来源按钮和使用弹窗按钮改为单列可触控布局。

## 验证

```bash
npm run build --workspace=radish.client
npm run check:repo-hygiene:changed
git diff --check
```

结果：均通过。

未执行真实 Gateway PC / mobile smoke；本轮未收到前后端已启动的明确确认。该类运行态复核仍按阶段验收或用户明确要求执行。

## 后续顺位

1. 继续 `radish.client` 第二批视觉实现的剩余私域 / 作者态页面：通知 / 消息、圈子 / 宠物、论坛作者态和 Docs 作者态。
2. 若继续推进交易数据面，可再补 PC / mobile Gateway 运行态复核；执行前需用户明确说明前后端已启动。
3. Console 视觉代码实现继续后移，不把 D6 盘点误判为当前第一开发项。
