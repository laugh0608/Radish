# P3-12-D62 Private / Author Workbench 首批实现记录

> 日期：2026-07-04（Asia/Shanghai）
>
> 状态：已进入 `P3-12-D62 Private / Author Pencil 逐页 UI 与功能缺口实现`，首批对齐 `/workbench` 的私域工作台结构

## 背景

`P3-12-D61 Public Web` 当前发布前范围 `P01-P14` 已完成首轮前端对齐与 Gateway PC / mobile CSS 视口真实 smoke；`P15-P16` 公开聊天室 / 移动聊天回复流继续作为 Public 小专题内产品与 API 后置缺口，不在 D61 前端硬造。

因此，本批按 D60 拆分顺序进入 `P3-12-D62 Private / Author`，不进入 `P3-12-E`。

## 设计源读取

- 通过 Pencil MCP 读取当前编辑器状态，确认当前设计源为 `Docs/frontend/design-sources/private-web-workflows.pen`。
- `snapshot_layout` 抽查：
  - `P01 - Workbench Route Map`：PC 工作台包含私域壳层、主继续处理区、右侧状态 rail、三组任务入口和历史兼容边界。
  - `P21 - Mobile Workbench`：移动工作台为 `390px` 单列任务页，顺序为状态栏 / 私域头部 / 今日队列 / 功能入口 / 私域移动底栏。
- 本批未修改 `.pen` 设计源。

## 实现范围

- `Frontend/radish.client/src/workbench/WorkbenchApp.tsx`
  - 新增 `/workbench` 继续处理队列，指向现有正式 Web 路由：`/notifications`、`/shop/orders`、`/docs/mine`、`/pet`。
  - 新增私域状态 rail，说明登录态任务、公开承接和作者态边界，并提供资产 / 创作快捷入口。
  - 移除 `/workbench` 对公开移动底栏的覆盖，恢复 `private` shell 默认移动底栏：工作台、资产、创作、消息、我的。
- `Frontend/radish.client/src/workbench/WorkbenchApp.module.css`
  - PC 下新增主任务区 + 右侧 rail 结构，任务队列使用紧凑行式布局，避免继续只有 summary + 功能卡片。
  - 移动端按单列顺序先展示今日队列，再展示状态 rail 和功能入口；底部留白继续使用 `--rx-mobile-shell-offset`。
- `Frontend/radish.client/src/i18n.ts`
  - 补齐工作台继续处理队列、状态 rail、资产 / 创作快捷入口中英文文案。

## 保持不变

- 不新增业务 API。
- 不新增权限键。
- 不修改数据库结构。
- 不修改路由语义。
- 不修改保存 / 提交载荷。
- 不迁移 WebOS Dock、窗口系统、桌面背景或窗口几何语义。
- 不扩完整钱包、退款 / 售后、支付口令、资产风控、完整聊天平台或复杂安全设置。
- 不进入 `P3-12-E`。

## 后续 D62 待办

1. 下一批建议进入 `/me` 内容历史复访组，对照 `private-web-workflows.pen` 的 `P03-P06 / P23`，覆盖 `/me/content`、`/me/history`、`/me/attachments`、`/me/experience` 的 PC 与 mobile 任务流、tab / 筛选、选中内容预览、公开详情来源返回、附件归属、经验进度和流水可读性。
2. `/me` 内容历史复访组完成后，再进入资产 / 订单 / 背包 `P07-P11 / P24-P25`，随后处理通知 / 消息、圈子 / 宠物、论坛作者态和 Docs 作者态。
3. 只处理现有 API / 路由 / 前端状态内可承接的 UI 与功能状态；无法在现有契约内完成的能力记录为 D62 内后置产品 / API 缺口。
4. 若需要真实 Gateway smoke，仍必须先由用户当轮明确说明前后端已启动；浏览器复核优先使用 `@浏览器` 插件。

## 验证

- `npm run build --workspace=radish.client`：通过。

## 未执行

- 本轮未执行 Gateway 真实页面 smoke；用户本轮未重新明确说明前后端已经启动。
