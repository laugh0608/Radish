# P3-12-D8 radish.client 视觉实现首批记录

> 日期：2026-06-28（Asia/Shanghai）
>
> 范围：`radish.client` 公共壳层、共享状态槽、公开页面容器宽度与移动单列留白首批代码对齐

## 背景

`P3-12-D2` 至 `P3-12-D7` 已完成公开 Web、私域 / 作者态、共享 UI 基座、Console 治理工作台和移动底栏的 Pencil 设计源与说明收口。按当前阶段顺序，Console 代码实现后移，`radish.client` 先进入视觉实现。

本批不启动 Console、Flutter、后端接口、发布流程或 WebOS 形态迁移。

## 今日实现范围

- 新增并启用 `Frontend/radish.client/src/components/web-shell/` 共享壳层组件：
  - `WebShellHeader`
  - `WebStateSlot`
  - `index.ts`
- 将私域复访页面首批接入共享壳层 / 状态槽节奏：
  - `/workbench`
  - `/me`
  - `/messages`
  - `/notifications`
  - `/pet`
  - `/circle`
  - `shop` Web 私域页面
  - Docs 作者态页面
- 将公开页面状态卡首批统一到 `WebStateSlot`：
  - `/discover`
  - `/docs`
  - `/leaderboard`
  - `/shop`
  - `/u/:id`
  - 公开 forum 状态入口已在前一批共享壳层调整中接入。
- 在 `theme-tokens.css` 补充公开内容宽度 token：
  - `--rx-content-max-width`
  - `--rx-content-reading-width`
  - `--rx-content-narrow-width`
- 为公开页面移动单列补齐底部 WebOS 导航安全留白，避免内容被移动浮动导航遮挡。

## 对齐口径

- 继续以 [Web UI 共享基座设计说明](/frontend/web-ui-foundation-design)、[公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design) 和 [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design) 为视觉实现依据。
- `WebStateSlot` 承接加载、空态、错误、权限限制、登录恢复和来源返回动作，不再让各公开页面长期维护分叉状态卡。
- 公开页面容器宽度通过语义 token 表达，不继续新增页面级硬编码宽度。
- 移动端保持单列任务流和底部浮动导航留白，不迁入 WebOS Dock、窗口系统、桌面背景或窗口几何记忆。

## 验证

```bash
npm run build --workspace=radish.client
npm run check:repo-hygiene:changed
git diff --check
```

结果：均通过。

未执行真实 Gateway PC / mobile smoke；本轮未收到前后端已启动的明确确认。该类运行态复核仍按阶段验收或用户明确要求执行。

## 后续顺位

1. 继续 `radish.client` 第二批视觉实现，优先处理私域 / 作者态真实页面的数据面与任务流：资产 / 流水、订单 / 背包、通知 / 消息、圈子 / 宠物、论坛作者态和 Docs 作者态。
2. 把 `WebShellHeader`、`WebStateSlot` 和 `rx-*` token 的落地差异继续回看 public / private 设计说明，必要时先修说明再改代码。
3. 代码完成后继续使用 `radish.client` 类型检查 / 构建、仓库卫生检查和 `git diff --check`；真实 Gateway PC / mobile smoke 只在用户确认前后端已启动后执行。
4. Console 视觉代码实现继续后移，不把 D6 盘点误判为当前第一开发项。
