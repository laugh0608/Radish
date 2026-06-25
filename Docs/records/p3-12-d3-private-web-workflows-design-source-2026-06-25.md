# P3-12-D3 私域与作者态 Web 工作流设计源记录

> 日期：2026-06-25（Asia/Shanghai）
>
> 状态：私域 / 作者态设计源 `P01-P05` 已补齐；不进入视觉代码实现
>
> 结论：`private-web-workflows.pen` 已写入 `P01-P05`，覆盖私域首页、资产 / 订单 / 背包、作者工作台、编辑器 / 版本回看和移动私域单列。已新增 [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design) 作为实现前口径；当前下一顺位是补 `console-governance-workbench.pen` 的文档治理差异画板。
>
> 2026-06-25 补充：按视觉规范审阅后已做一轮设计源优化：移除页面内面向开发者的停止线文案，替换为用户可见的任务、写作建议和状态说明；强化 `P01` 当前任务主动作；放大 `P04` 编辑正文区并压缩反馈卡片；关键小字号路由 / 说明文本改用 `rx-text-secondary`，`rx-*` 变量取值继续与公开 Web 设计源保持一致。

## 背景

`P3-12-D1` 已定义统一 UI 设计拆分、页面矩阵和停止线。`P3-12-D2` 已完成公开 Web `public-web-unified-experience.pen` 的 `P01-P05`，并完成公开端点信息密度收口。

本轮继续按 D1 顺序补 `private-web-workflows.pen`。该设计源承接登录态私域和作者态流程，不进入视觉代码实现，不修改 Console 设计源。

## 设计源

文件：

```text
Docs/frontend/design-sources/private-web-workflows.pen
```

已同步登记：

- [设计源文件目录](/frontend/design-sources/README)
- [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design)

变量：

- 已写入与 `public-web-unified-experience.pen` 一致的 `rx-*` 变量。
- 变量继续沿用 [视觉主题规范](/frontend/visual-theme-spec) 与 [视觉颜色参考](/frontend/visual-color-reference) 的纸色底、低饱和边框、克制品牌色和淡雅新中式口径。

## 已完成画板

### `P01 - Private Home Workbench`

职责：

- `/workbench` 私域首页与正式 Web 功能地图。
- 用户状态、关键动作、资产 / 内容 / 经验摘要、最近待处理队列。
- 右侧身份、通知、宠物状态和 WebOS 边界提示。

设计口径：

- “工作台”进入 `/workbench`，不直接打开 `/desktop`。
- 私域首页聚合正式 Web 已迁移入口，不重建 WebOS 桌面。
- 复访队列只指向正式 Web 路由。

### `P02 - Assets Orders Inventory`

职责：

- `/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/:id` 和 `/shop/inventory`。
- 资产摘要、资产流水表、订单详情、订单时间线和背包权益。

设计口径：

- 资产 / 订单页面偏数据面密度，表格使用固定列层级。
- 订单详情展示支付、发放、确认收货和商品回流。
- 不扩展转账、支付口令设置、退款、权益激活或完整钱包平台。

### `P03 - Author Workbench`

职责：

- 论坛作者态与文档作者态统一工作台。
- 草稿队列、筛选、发布前检查、作者复访入口、提交反馈和身份展示。

设计口径：

- 论坛写作和文档写作共享作者节奏，但不混入 Console 治理。
- `DisplayName / DisplayHandle` 作为普通展示身份。
- `clientSubmissionId`、草稿同步、保存状态和发布失败重试作为作者态显性状态。

### `P04 - Editor Version Review`

职责：

- 编辑器主区、标题 / 正文 / 元信息、保存 / 发布反馈。
- 版本时间线、只读差异预览和编辑器边界说明。

设计口径：

- 编辑与版本回看可共屏，发布动作只归作者态。
- 版本回看只做阅读和差异对比。
- 当前不加入回滚、完整富文本、投票、导入导出或治理动作。

### `P05 - Mobile Private Single Column`

职责：

- 移动私域单列基线。
- 状态栏、私域头部、用户状态、关键入口、复访消息、状态槽和底部 tab。

设计口径：

- 移动端保持单列连续工作流，不缩小桌面三栏。
- 关键入口纵向展示，不依赖横向滚动。
- 底部 tab 只保留工作台、资产、创作和消息，不复刻 Dock 或 WebOS app 外壳。

## 验证

Pencil 侧：

- `P01`：首次检查命中顶部引导区和右侧 rail 固定高度裁切；已调整引导区高度、指标 / 网格 / 右侧卡片密度，复查 `snapshot_layout` 返回 `No layout problems.`
- `P01`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P01`：视觉审阅后强化“继续当前任务”，将右侧边界说明替换为“今日节奏”；复查无布局问题。
- `P02`：首次生成后命中 lucide 图标 `clock` 不存在；已替换为 `circle`。
- `P02`：`snapshot_layout` 返回 `No layout problems.`
- `P02`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P03`：首次检查命中左侧作者模式切换和右侧停止线局部裁切；已压缩队列 / 筛选密度并扩展停止线空间，复查 `snapshot_layout` 返回 `No layout problems.`
- `P03`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P03`：视觉审阅后将作者态停止线改为用户可见的写作建议和身份 / 链接状态说明；复查无布局问题。
- `P04`：首次检查命中编辑卡片底部反馈条轻微裁切；已压缩正文输入区和内部 gap，复查 `snapshot_layout` 返回 `No layout problems.`
- `P04`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P04`：视觉审阅后放大正文编辑区，压缩反馈卡片，并将编辑器边界说明改为下一步建议；复查无布局问题。
- `P05`：`snapshot_layout` 返回 `No layout problems.`
- `P05`：截图目检未发现明显裁切、坍塌或横向溢出，底部 tab 位于单列末尾。
- `P05`：视觉审阅后将移动边界说明改为小屏提示，状态槽文案转为用户可见说明；复查无布局问题。
- 全局：`private-web-workflows.pen` 全局 `snapshot_layout` 返回 `No layout problems.`

仓库侧：

```bash
git diff --check
rg -n "[ \t]+$" Docs/frontend/private-web-workflows-design.md
rg -n "[ \t]+$" Docs/records/p3-12-d3-private-web-workflows-design-source-2026-06-25.md
```

结果：通过。

## 后续顺序

1. 以 [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design) 和 `P01-P05` 作为私域 / 作者态视觉实现前口径。
2. 在 `console-governance-workbench.pen` 补文档治理差异画板，对齐 Console 治理密度、权限边界和版本治理。
3. 公开、私域 / 作者态和 Console 文档治理画板确认后，再进入视觉代码实现。

## 当前不做

- 不进入 `radish.client` 视觉代码实现。
- 不修改公开 Web 设计源。
- 不修改 Console 设计源。
- 不把 `/desktop`、Dock、窗口系统或 `openApp` 纳入正式 Web 视觉基线。
- 不扩完整钱包、转账、支付口令、退款、完整通知中心、完整聊天平台或 Flutter 完整承接。
