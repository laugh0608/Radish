# P3-12-D1 统一 UI 设计准备记录

> 日期：2026-06-22（Asia/Shanghai）
>
> 状态：设计准备已启动；Pencil app 当前未连接，暂不创建或修改 `.pen` 设计源；不进入视觉代码实现
>
> 结论：B4 运行态 smoke 可与 D1 / D2 形成更大阶段后集中验收；当前先推进 P3-12-D 的页面矩阵、设计源拆分、停止线和后续实现顺序。页面级、端点级或跨页面视觉改造仍必须等 Pencil 设计稿和说明文档确认后再进入代码。

> 2026-06-22 补充：用户随后明确说明前后端已启动，已补 B4 / D1 阶段运行态 smoke，记录见 [P3-12 B4 / D1 阶段运行态 Smoke 记录](/records/p3-12-b4-d1-stage-smoke-record-2026-06-22)。`/messages` 已验证为正式 Web 消息 / 聊天入口；后续在 UI 设计专题前先插入 [P3-12-B5 Web 功能总入口设计](/records/p3-12-b5-web-workbench-entry-design-2026-06-22) 和 [P3-12-B6 身份语义二次收口设计](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22)，分别解决正式 Web 功能发现与身份字段混用问题。

> 2026-06-24 补充：B5 / B6 已完成代码侧与启动前验证收口；Pencil 可用后已创建 `public-web-unified-experience.pen` 并补公开 Web `P01-P02`，记录见 [P3-12-D2 公开 Web 统一体验设计源记录](/records/p3-12-d2-public-web-unified-design-source-2026-06-24)。后续继续补公开详情、公开集合页和 mobile 单列设计稿。

> 2026-06-25 补充：`public-web-unified-experience.pen` 已补齐公开 Web `P01-P05`，并新增 [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)。下一步转入 `private-web-workflows.pen` 与 Console 文档治理差异画板。

## 本轮背景

`P3-12-B1 / C1 / B2 / B3 / B4-1 / B4-2` 已完成首批代码或小阶段验收，正式 Web 已具备账户资产、商城交易、个人中心、论坛作者态、文档作者态和 Console 文档治理的主路径。

用户已确认：先继续推进开发，B4 的 Gateway PC / mobile smoke 等一个更大的阶段再集中验收。因此本轮不启动 API / Auth / Gateway / Vite，不执行真实浏览器 smoke。

已核对：

- [当前进行中](/planning/current)
- [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)
- [前端设计文档](/frontend/design)
- [视觉主题规范](/frontend/visual-theme-spec)
- [UI 设计灵感参考](/frontend/ui-design-inspiration)
- [Console 样式与 Token 使用说明](/frontend/console-style-guide)
- [设计源文件目录](/frontend/design-sources/README)

## Pencil 状态

本轮尝试调用 Pencil `get_editor_state(include_schema: true)`，工具返回无法连接运行中的 Pencil app。

因此本轮只做设计准备文档，不做以下事项：

- 不创建 `.pen` 文件。
- 不读取或修改已有 `.pen` 文件。
- 不修改页面 CSS / 组件视觉代码。
- 不把文档准备当成视觉实现完成。

后续进入实际设计稿阶段时，必须通过 Pencil 工具创建或更新 `.pen` 源文件。

## D1 设计源拆分建议

为避免一个设计源承载所有页面，D1 建议按端点拆分：

| 设计源 | 状态 | 职责 | 首批画板建议 |
| --- | --- | --- | --- |
| `public-web-unified-experience.pen` | 已创建，`P01-P05` 已完成 | 公开 Web 壳层、内容流、公开详情、公开搜索、公开个人页、榜单和公开商城浏览 | `P01` 公开壳层基座、`P02` 发现内容流、`P03` 详情阅读、`P04` 公开集合页、`P05` mobile 单列 |
| `private-web-workflows.pen` | 待 Pencil 可用后创建 | 登录态私域和作者态流程，包括 `/me`、资产、订单、背包、通知、消息、圈子、宠物、论坛作者态和文档作者态 | `P01` 私域首页、`P02` 资产 / 订单、`P03` 作者工作台、`P04` 编辑器、`P05` mobile 私域 |
| `console-governance-workbench.pen` | 已存在 | Console Case Desk 基座，后续只补文档治理差异画板，不重做 Console 全站 | `P09` 文档治理列表 / 版本 / 权限策略，沿用既有 `P01-P08` 基座 |

当前 `Docs/frontend/design-sources/README.md` 只记录真实存在的源文件；待 Pencil 实际创建新 `.pen` 后再把新文件加入“当前源文件”列表。

## 页面矩阵

### 公开 Web 壳层

| 页面组 | 路由 | D1 目标 | 不做 |
| --- | --- | --- | --- |
| 发现内容流 | `/discover` | 统一公开壳层、内容卡片、分区标题、筛选和移动单列密度 | 不改成营销首页，不新增宽幅能力介绍 band |
| 论坛公开 | `/forum`、`/forum/category/:id`、`/forum/search`、`/forum/type/:type`、`/forum/post/:id` | 统一列表 / 搜索 / 详情阅读层级，保留登录参与和作者态入口边界 | 不做完整论坛重写，不引入 WebOS 三栏工作台 |
| 文档公开 | `/docs`、`/docs/search`、`/docs/:slug` | 保持只读，统一目录、搜索、详情元信息和正文阅读宽度 | 不加入编辑、发布、回收站、权限策略或版本治理 |
| 公开个人页 | `/u/:identifier` | 统一公开资料、公开帖子 / 评论、来源返回与移动阅读 | 不加入资料编辑、浏览历史、附件管理或账号设置 |
| 榜单与商城公开 | `/leaderboard`、`/leaderboard/:type`、`/shop`、`/shop/products`、`/shop/product/:id` | 统一只读浏览、商品详情、购买登录回流提示和移动卡片密度 | 不把订单、背包或完整钱包塞回公开页 |

### 登录态私域与作者态

| 页面组 | 路由 | D1 目标 | 不做 |
| --- | --- | --- | --- |
| 我的状态 / 个人中心 | `/me`、`/me/content`、`/me/history`、`/me/attachments`、`/me/experience` | 统一个人状态、内容、历史、附件和经验的分区节奏 | 不启动完整账号安全设置或公开主页治理 |
| 资产与商城私域 | `/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/:id`、`/shop/inventory` | 统一资产流水、订单详情、背包和商品回流信息结构 | 不做转账、支付口令设置、退款和权益激活扩展 |
| 复访入口 | `/notifications`、`/messages`、`/circle`、`/pet` | 统一私域复访页面壳层、状态卡、列表和空态 | 不启动私聊、搜索、Reaction、完整通知中心或宠物经济系统 |
| 论坛作者态 | `/forum/compose`、`/forum/post/:id?intent=answer|edit|history` | 统一作者入口、编辑器、历史回看和提交状态 | 不扩富文本、附件、投票、治理和审核 |
| 文档作者态 | `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id` | 统一作者文档列表、编辑器、只读内置文档提示和版本回看 | 不加入发布、回收站、权限策略、导入导出和回滚 |

### Console

| 页面组 | 路由 | D1 目标 | 不做 |
| --- | --- | --- | --- |
| 文档治理 | `/console/documents` | 在既有 Case Desk / 表格 CRUD 体系下补文档治理画板，对齐状态治理、权限策略和版本治理密度 | 不重做 Console 全站，不把 Console 做成文档创作器 |
| 既有治理页 | `/console/moderation`、`/console/experience`、`/console/coins` 等 | 只作为视觉基座参考和回归比较 | 不借 D1 顺手大改历史页面 |

## 设计原则

1. 正式 Web 是主路径，`/desktop` 是历史维护入口。D1 不迁移 Dock、窗口系统、桌面背景、窗口几何记忆或 `openApp` 语义。
2. 公开页优先内容密度、阅读和分享，不做营销化首屏。
3. 作者态优先编辑效率、提交反馈和版本回看，不把治理动作混入普通作者流程。
4. Console 优先治理密度、权限边界和操作留痕，不复刻公开 Web 的阅读壳层。
5. 淡雅新中式只作为纸色底、弱纹样、标题气质、边缘收边和控件层级，不牺牲文本可读性。
6. 新增视觉 token 必须先在设计说明中定义语义，再进入 CSS；不在页面里扩散硬编码颜色。

## 后续执行顺序

1. 先完成 `P3-12-B5` 的 `/workbench` 功能总入口和公共壳层入口调整，避免 UI 设计阶段继续以 `/desktop` 作为用户找功能的默认路径。
2. 再完成 `P3-12-B6` 身份语义二次收口，避免 UI 设计阶段继续围绕 `LoginName`、旧 `UserName`、`UserRealName` 或 `usr_...` 可见文本做界面固化。
3. `public-web-unified-experience.pen` 的 `P03` 公开详情阅读、`P04` 公开集合页和 `P05` mobile 单列关键画板已补齐。
4. 再创建 `private-web-workflows.pen`，并在 `console-governance-workbench.pen` 补 `P09` 文档治理画板。
5. 每个 `.pen` 首批只画 PC 和 mobile 关键帧，不追求一次覆盖所有状态。
6. 根据设计稿更新 `Docs/frontend/design.md`、`Docs/frontend/visual-theme-spec.md` 或新增专题说明。
7. 再进入代码实现，优先沉淀共享 token、公开壳层和作者态通用结构。
8. B5 / B6 / D 的 Gateway PC / mobile smoke 合并到较大阶段执行，等用户明确说明前后端已启动后再做。

## 验证口径

设计准备阶段：

- `git diff --check`
- `npm run check:repo-hygiene:changed`

Pencil 设计稿阶段：

- Pencil `snapshot_layout` 检查画板结构。
- 必要时导出 PC / mobile 关键画板截图。
- 设计源文件只通过 Pencil 工具修改。

代码实现阶段：

- `npm run type-check --workspace=radish.client`
- `npm run build --workspace=radish.client`
- 若触达 Console：`npm run build --workspace=radish.console`
- 准备阶段验收时，用户确认前后端已启动后再覆盖 Gateway PC / mobile 页面 smoke。

## 当前不做

- 不启动真实浏览器 smoke。
- 不启动前后端服务。
- 不创建或修改 `.pen` 文件。
- 不直接改页面视觉代码。
- 不启动 Flutter 完整能力套件。
- 不扩 WebOS 残留清扫。
- 不恢复 PR、tag 或发布流程。
