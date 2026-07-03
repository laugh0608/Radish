# P3-12-D3 私域与作者态 Web 工作流设计源记录

> 日期：2026-06-25（Asia/Shanghai）
>
> 状态：私域 / 作者态设计源 `P01-P30` 已补齐；当前进入 `radish.client` 视觉实现前口径
>
> 结论：`private-web-workflows.pen` 已二次重构为 `P01-P30`，覆盖 `/workbench`、`/me` 系列、资产流水、订单、背包、通知、消息、圈子、宠物、论坛作者态、Docs 作者态和 10 个移动单任务页面。已同步 [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design) 作为 `radish.client` 视觉实现前口径。
>
> 2026-06-25 补充：按视觉规范审阅后已做一轮设计源优化：移除页面内面向开发者的停止线文案，替换为用户可见的任务、写作建议和状态说明；强化 `P01` 当前任务主动作；放大 `P04` 编辑正文区并压缩反馈卡片；关键小字号路由 / 说明文本改用 `rx-text-secondary`，`rx-*` 变量取值继续与公开 Web 设计源保持一致。
>
> 2026-06-28 补充：按 `web-ui-foundation.pen` / `F02` 公共壳层契约和用户复审反馈重审 private 业务设计源。上一版 `P01-P20` 页面仍存在模板化、移动页不足、部分页面脱离真实功能和右侧身份区外框空白过大的问题，已整体替换为真实路由驱动的 `P01-P30` 矩阵；已完成深层 `snapshot_layout`、全局布局检查与 PC / mobile 截图抽查。

## 背景

`P3-12-D1` 已定义统一 UI 设计拆分、页面矩阵和停止线。`P3-12-D2` 已将公开 Web `public-web-unified-experience.pen` 扩展为 `P01-P16` 公开社区 App 页面族，并完成公开端点信息密度收口。

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

### PC 页面族

| 画板 | 职责 |
| --- | --- |
| `P01 - Workbench Route Map` | `/workbench` 正式 Web 功能地图、继续处理队列和 WebOS 历史入口边界 |
| `P02 - Me Overview Dashboard` | `/me` 个人状态、公开身份、经验、资产摘要、宠物摘要和最近复访 |
| `P03 - My Content Tabs` | `/me/content` 帖子、评论、轻回应 tab 和选中内容预览 |
| `P04 - Browse History` | `/me/history` 浏览历史、公开详情来源返回和复访决策 |
| `P05 - Attachment Browser` | `/me/attachments` 按业务归属浏览头像、帖子、评论和文档附件 |
| `P06 - Experience Ledger` | `/me/experience` 等级进度、经验来源和经验流水 |
| `P07 - Assets Overview` | `/me/assets` 胡萝卜余额、近期流水和资产入口 |
| `P08 - Asset Transactions` | `/me/assets/transactions` 资产流水筛选、表格和选中流水详情 |
| `P09 - Orders List` | `/shop/orders` 订单状态分组、购买后回流和订单详情入口 |
| `P10 - Order Detail` | `/shop/order/:orderId` 支付、权益发放、商品回看和背包回流 |
| `P11 - Inventory Benefits` | `/shop/inventory` 背包权益、来源订单、激活 / 停用 / 使用入口 |
| `P12 - Notifications Center` | `/notifications` 未读通知、频道筛选和正式 Web 目标分流 |
| `P13 - Messages Workspace` | `/messages` 会话列表、聊天正文、回复输入和订单 / 用户上下文 |
| `P14 - Circle Feed` | `/circle` 关注动态、正在关注、关注者和公开详情来源返回 |
| `P15 - Pet Care` | `/pet` 宠物档案、基础照护动作、状态条和变化流水 |
| `P16 - Forum Compose` | `/forum/compose` 论坛发帖编辑器、分类 / 标签、发布检查和提交反馈 |
| `P17 - Forum Edit History` | `/forum/post/:id?intent=answer|edit|history` 作者编辑、问答回答和编辑历史 |
| `P18 - Docs Mine` | `/docs/mine` Docs 作者库、文档树、文档列表和低风险作者动作 |
| `P19 - Docs Compose Edit` | `/docs/compose`、`/docs/edit/:id` 文档编辑器、元信息和保存 / 预览动作 |
| `P20 - Docs Revisions` | `/docs/revisions/:id` 文档版本栈、只读预览和差异回看 |

### 移动页面族

| 画板 | 职责 |
| --- | --- |
| `P21 - Mobile Workbench` | 移动端 `/workbench`，正式 Web 功能地图和今日队列 |
| `P22 - Mobile Me` | 移动端 `/me`，个人状态、经验、资产摘要和我的入口 |
| `P23 - Mobile Content History` | 移动端 `/me/content`、`/me/history`、`/me/attachments` 的复访任务 |
| `P24 - Mobile Assets Ledger` | 移动端 `/me/assets`、`/me/assets/transactions`，余额和近期流水 |
| `P25 - Mobile Orders Inventory` | 移动端 `/shop/orders`、`/shop/order/:id`、`/shop/inventory` 订单背包任务 |
| `P26 - Mobile Notifications` | 移动端 `/notifications`，未读通知和目标跳转 |
| `P27 - Mobile Messages` | 移动端 `/messages`，会话正文和回复输入 |
| `P28 - Mobile Circle` | 移动端 `/circle`，关注动态和关系链 |
| `P29 - Mobile Pet` | 移动端 `/pet`，宠物状态、照护动作和状态流水 |
| `P30 - Mobile Author` | 移动端 `/forum/compose`、`/docs/mine`、`/docs/edit/:id` 的继续创作入口 |

设计口径：

- PC 私域页面统一使用 `F02` private header：左侧 Radish 品牌、居中私域导航、右侧身份与设置；右侧身份操作组使用内容贴合宽度，不再出现多余外框空白。
- 私域导航固定为工作台、我的状态、资产、创作和消息；业务页通过当前激活态表达所属主入口。
- 移动端统一使用状态栏、私域头部、单列内容和底部 tab；底部 tab 为工作台、资产、创作、消息和我的。
- 资产 / 订单 / 背包只展示真实正式 Web 路由和当前已有能力，不扩展完整钱包、转账、支付口令、独立退款平台或资产风控平台。
- 作者态不新增独立“作者工作台”路由；论坛作者态和 Docs 作者态按各自真实路由拆分，Console 继续承接治理、权限、审核、回收站和高风险操作。

## 验证

Pencil 侧：

- 2026-06-28 首轮重审时发现旧版页面数量和 private header 均未达到 `F02` 标准；随后用户复审继续指出上一版 `P01-P20` 存在模板化、脱离真实功能、移动页不足和右侧身份外框空白问题。
- 旧 `P01-P20` 已删除，改为当前真实路由驱动的 `P01-P30` 矩阵。
- 生成过程中命中的无效 Lucide 图标名、移动端面板高度不足和 PC 表格列宽溢出均已修正。
- 深层全局 `snapshot_layout(problemsOnly: true, maxDepth: 8)` 返回 `No layout problems.`
- 全局顶层检查显示当前保留 30 个新版画板，PC `P01-P20` 与移动 `P21-P30` 无根级重叠。
- 截图抽查 `P01 - Workbench Route Map` 与 `P21 - Mobile Workbench`，未发现明显裁切、坍塌、横向溢出或非标准导航；PC 右侧身份区已改为内容贴合按钮组。

仓库侧：

```bash
git diff --check
rg -n "[ \t]+$" Docs/frontend/private-web-workflows-design.md Docs/records/p3-12-d3-private-web-workflows-design-source-2026-06-25.md Docs/frontend/design-sources/README.md Docs/planning/current.md Docs/planning/p3-12-web-completion-webos-retirement.md
```

结果：通过。

## 后续顺序

1. 以 [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design) 和 `P01-P30` 作为私域 / 作者态视觉实现前口径。
2. public / private 业务设计源已具备进入 `radish.client` 视觉实现的设计基础；后续优先抽共享壳层、token、公开 / 私域页面结构和移动单列节奏。
3. Console 公共壳层代码实现继续按 [P3-12-D6 Console 视觉代码实现前盘点](/records/p3-12-d6-console-visual-code-prep-2026-06-27) 后移承接。

## 当前不做

- 不进入 `radish.client` 视觉代码实现。
- 不修改公开 Web 设计源。
- 不修改 Console 设计源。
- 不把 `/desktop`、Dock、窗口系统或 `openApp` 纳入正式 Web 视觉基线。
- 不扩完整钱包、转账、支付口令、退款、完整通知中心、完整聊天平台或 Flutter 完整承接。
