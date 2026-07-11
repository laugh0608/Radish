# P3-12-D62 `/me` 内容历史复访组首批实现记录

> 日期：2026-07-05（Asia/Shanghai）
>
> 状态：已完成 `P3-12-D62 Private / Author Pencil 逐页 UI 与功能缺口实现` 中 `/me/content`、`/me/history`、`/me/attachments`、`/me/experience` 首批对齐

## 背景

`P3-12-D62` 已完成 `/workbench` 私域工作台首批实现。按 [P3-12-D60 Pencil 逐页 UI 与功能缺口复盘](/records/p3-12-d60-pencil-page-ui-and-function-gap-review-2026-07-04) 和当前规划，下一顺位进入 `/me` 内容历史复访组，对照 `private-web-workflows.pen` 的 `P03-P06 / P23`。

本批继续留在 `P3-12-D` UI 专题，不进入 `P3-12-E`，不启动发布或生产部署流程。

## 设计源读取

- 通过 Pencil MCP 确认当前活动文件为 `Docs/frontend/design-sources/private-web-workflows.pen`。
- `snapshot_layout` 抽查：
  - `P03 - My Content Tabs`：PC 为顶部私域壳层、页面说明、内容 tab、主列表和右侧预览区。
  - `P04 - Browse History`：PC 为历史列表与右侧来源返回上下文。
  - `P05 - Attachment Browser`：PC 为附件筛选 / 列表与右侧附件归属上下文。
  - `P06 - Experience Ledger`：PC 为等级进度、流水列表和能力 / 状态摘要区。
  - `P23` 移动私域任务页：移动端为身份 / 任务摘要、快捷 tab、指标、继续处理条目和紧凑任务表的单列结构。
- 本批未修改 `.pen` 设计源。

## 实现范围

- `Frontend/radish.client/src/me/MeApp.tsx`
  - `/me/content`、`/me/history`、`/me/attachments`、`/me/experience` 改为统一私域子页任务面：顶部子导航、页面指标、主内容区和右侧上下文 rail。
  - 内容页按当前 tab 展示当前页首条帖子 / 评论 / 轻回应预览，保留公开详情来源返回。
  - 历史页展示当前页首条浏览记录预览，保留论坛 / 文档 / 商城等正式公开路由回流语义。
  - 附件页展示当前筛选下首个附件预览，保留现有业务归属、下载、复制链接和删除动作。
  - 经验页通过现有经验详情数据回调展示等级 / 流水预览，保留等级进度与分页流水语义。
- `Frontend/radish.client/src/me/MeApp.module.css`
  - 子页从单一大卡片改为 `hero + main + rail` 结构，PC 下主列表与右侧上下文并列，移动端收为单列任务面。
  - 补齐子导航、指标、预览卡、上下文 rail 和移动端堆叠规则。
- `Frontend/radish.client/src/apps/profile/components/*`
  - 给帖子、评论、轻回应、浏览历史、附件列表补只读 `onItemsLoaded` 回调，供父页面生成当前页预览。
  - 不改变列表请求参数、排序、分页、点击目标或写入动作。
- `Frontend/radish.client/src/apps/experience-detail/ExperienceDetailApp.tsx`
  - 补只读 `onDataLoaded` 回调，供 `/me/experience` 外层展示等级和流水上下文。
  - 顺手收口该组件的 hooks lint 警告。
  - Gateway smoke 发现父页面内联 `onDataLoaded` 与子组件 `loadData` 依赖形成 effect 循环后，改为使用 `useRef` 保存最新回调，数据加载只随页码变化触发。
- `Frontend/radish.client/src/i18n.ts`
  - 补齐 `/me` 子页任务面、预览、指标和上下文 rail 的中英文文案。

## 保持不变

- 不新增业务 API。
- 不新增权限键。
- 不修改数据库结构。
- 不修改路由语义。
- 不修改保存 / 提交载荷。
- 不迁移 WebOS Dock、窗口系统、桌面背景或窗口几何语义。
- 不扩完整钱包、退款 / 售后、支付口令、资产风控、完整聊天平台或复杂资料 / 安全设置。
- 不进入 `P3-12-E`。

## 后续 D62 待办

1. 下一批建议进入资产 / 订单 / 背包组，对照 `private-web-workflows.pen` 的 `P07-P11 / P24-P25`，覆盖 `/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/:orderId`、`/shop/inventory` 的余额摘要、流水筛选、订单状态分组、订单详情时间线、权益状态和背包回流。
2. 资产 / 订单 / 背包完成后，再处理通知 / 消息、圈子 / 宠物、论坛作者态和 Docs 作者态。
3. 完整钱包、退款 / 售后、支付口令、资产风控、完整聊天平台和复杂资料 / 安全设置继续作为 D62 内后置产品 / API 缺口记录。
4. 若需要真实 Gateway smoke，仍必须先由用户当轮明确说明前后端已启动。

## 验证

- `npm run type-check --workspace=radish.client`：通过。
- `npm run build --workspace=radish.client`：通过。
- `npm run lint:changed`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。
- Gateway 真实页面 smoke：通过。
  - 账号：本地开发种子账号 `admin@radishx.com / admin123456`。
  - PC：`1920x1080` 覆盖 `/me/content`、`/me/content?tab=comments`、`/me/content?tab=quick-replies`、`/me/history`、`/me/attachments`、`/me/experience`；确认子导航、指标、主列表 / 空态、右侧 rail、公开详情跳转和浏览器返回均正常，无横向溢出。
  - 移动：`390x844 @ DPR 3` 覆盖 `/me/content`、`/me/history`、`/me/attachments`、`/me/experience`；确认子导航两列堆叠、上下文 rail 单列化、主列表和表单控件无横向溢出。
  - 截图留存在 `output/playwright/me-content-desktop-1920.png`、`output/playwright/me-content-mobile-390-dpr3.png`、`output/playwright/me-experience-mobile-390-dpr3.png`。

## 未执行

- 未执行发布 / 部署级回归。
