# P3-12-D53 Public Web Pencil 首轮真实页面对齐收口

## 基本信息

- 日期：`2026-07-02`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 阶段：`P3-12-D` UI 专题
- 性质：Public Web 设计源到真实页面的首轮运行态对齐与代码修正
- 设计依据：[公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)、`Docs/frontend/design-sources/public-web-unified-experience.pen`

## 背景

D52 只完成 Public Web 移动公开任务流代码侧首批，不能等同于 Pencil 设计稿逐页验收。本批在用户明确 Pencil 已打开、前后端已启动后，先通过 Pencil MCP 读取 `public-web-unified-experience.pen`，确认当前设计源包含 `P01-P16`，并以发布前范围内的 `P02-P14` 为核验对象。

## 核验结论

真实 Gateway mobile `390x844` 复核命中以下与 Pencil 口径不一致的问题：

- `/forum`、论坛搜索 / 标签 / 类型流、`/docs`、Docs 搜索、`/shop`、`/leaderboard` 和 `/u/:id` 的阅读说明卡在首屏主体内容之前，压住真实列表、目录、商品、榜单或公开内容。
- 论坛详情的阅读说明卡插在帖子正文和轻回应 / 评论任务之间，不符合移动帖子详情的正文、互动、评论顺序。
- 商品详情在移动端先展示大图，商品名、价格和关键购买信息被推到首屏底栏之后。

本批未回拉 `P15 / P16` 公开聊天室，也未新增独立公开首页；`P01` 仍由 `/discover` 承接。

## 实现范围

- 将 `PublicReadingGuide` 从公开集合页首屏前置改为主体内容之后，覆盖论坛列表、搜索、标签、类型流、Docs 列表 / 搜索、商城首页 / 列表 / 详情、榜单和公开主页。
- 将论坛详情阅读说明移到轻回应和评论区之后，保留正文、帖子级反应、评论入口和真实 intent 顺序。
- 商品详情移动端改为商品信息优先，商品图降为紧凑预览，首屏可看到商品名、类型、价格和售出 / 库存信息。

## 保持不变

- 不新增业务 API、权限键、数据库结构、路由语义、登录回流或保存 / 提交载荷。
- 不修改论坛数据加载、评论 / 轻回应提交、Docs 详情解析、商品购买、榜单数据或公开主页数据契约。
- 不进入 `P3-12-E`，不创建 tag，不进入 M15 测试 / 生产部署流程。

## 验证记录

- Pencil MCP：已连接并读取 `public-web-unified-experience.pen` 当前编辑状态。
- `npm run check:host-runtime -- --details`：通过。
- Gateway mobile `390x844` 真实页面复核：覆盖 `/discover`、`/forum`、论坛详情、`/docs`、Docs 详情、`/shop`、商品详情、`/leaderboard` 和有效 `/u/:id` 公开主页；未发现真实横向溢出，说明卡前置问题已收敛。
- `npm run type-check --workspace=radish.client`：通过。
- `npm run build --workspace=radish.client`：通过。

补充：Playwright CLI 在 Vite dev 反复导航期间记录到 React dev 重复 `createRoot` / `removeChild` 控制台错误；本批未将其判定为本次 UI 顺序修正引入的业务阻断，若后续在常规运行态稳定复现，应另起运行时挂载治理。

## 下一步

继续留在 `P3-12-D` 做退出条件判断。D53 已完成 Public Web 设计源到真实 mobile 页面首轮对齐修正，但仍不直接进入 `P3-12-E`；后续应按 public / private / console 分组判断是否还需要 PC 视图、更多真实数据态或成组回归补验。
