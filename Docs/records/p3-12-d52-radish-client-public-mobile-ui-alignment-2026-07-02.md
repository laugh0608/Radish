# P3-12-D52 radish.client Public Web 移动公开任务流 UI 对齐

## 基本信息

- 日期：`2026-07-02`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 阶段：`P3-12-D` UI 专题
- 性质：`radish.client` Public Web 移动公开任务流 UI 代码实现
- 设计依据：[公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)、[P3-12-D50 UI 实现缺口复盘与下一批实现排序](/records/p3-12-d50-ui-gap-recheck-and-next-implementation-order-2026-07-02)、[P3-12-D51 radish.client 私域 / 作者态移动任务流 UI 对齐首批](/records/p3-12-d51-radish-client-private-author-mobile-ui-first-closure-2026-07-02)

## 背景

`P3-12-D50` 已确认 D49 不能作为 UI 专题退出结论，D51 已完成私域 / 作者态移动任务流首批。D52 继续留在 `P3-12-D` UI 实现阶段，按公开 Web 设计说明中的 `P10-P14` 公开移动任务流和现有真实路由推进代码侧对齐。

本批未通过文件系统读取或修改 `.pen` 文件。Pencil MCP 已暴露，但 `get_editor_state(include_schema: true)` 当前无法连接到运行中的 Pencil app，因此本批按已经同步到 `Docs/` 的设计说明和既有阶段记录执行。

## 实现范围

本批覆盖 `radish.client` 已有 Public Web 真实路由，不新增业务能力：

| 页面范围 | 本批处理 |
| --- | --- |
| `/discover` | 移动 hero、摘要卡、公开分发 route card、内容流、状态槽和底部前留白收紧；内容流保持单列，低频入口不挤占底栏 |
| `/forum` | 移动筛选 / 分类从横向 rail 风险改为可换行任务区，帖子列表卡片密度、分页和状态槽收紧 |
| 论坛详情 | 帖子正文、轻回应墙、参与动作区、评论区、评论节点、投票 / 抽奖 / 问答块移动密度收紧，保留真实 intent `href` |
| `/docs` | 文档目录、最新文档、搜索结果、状态槽和分页区移动密度收紧，目录项降低横向挤压风险 |
| Docs 详情 | 阅读页 topbar、元信息卡、正文容器和分享 / 编辑入口移动密度收紧，正文继续保持 dominant region |
| `/shop` | 商城首页精选商品和商品列表在移动端改为更紧凑的任务列表节奏，分类 / 商品卡片降低高度 |
| 商品详情 | 商品图、价格、元信息、购买回流 panel 和说明区移动密度收紧；购买回流契约不变 |
| `/leaderboard` | 移动 tab 从横向滚动改为两列网格，说明区、榜单行、商品榜单指标和分页收紧 |
| `/u/:id` | 公开主页摘要、统计、阅读引导、tab、公开内容项和分页收紧，保持公开资料 / 内容 / 关注回流边界 |

## 代码范围

- `Frontend/radish.client/src/public/discover/PublicDiscoverApp.module.css`
- `Frontend/radish.client/src/public/forum/PublicForumApp.module.css`
- `Frontend/radish.client/src/public/docs/PublicDocsApp.module.css`
- `Frontend/radish.client/src/public/shop/PublicShopApp.module.css`
- `Frontend/radish.client/src/public/leaderboard/PublicLeaderboardApp.module.css`
- `Frontend/radish.client/src/public/profile/PublicProfileApp.module.css`
- `Frontend/radish.client/src/apps/forum/components/PostCard.module.css`
- `Frontend/radish.client/src/apps/forum/components/PostDetail.module.css`
- `Frontend/radish.client/src/apps/forum/components/CommentNode.module.css`
- `Frontend/radish.client/src/apps/forum/components/PostQuickReplyWall.module.css`
- `Frontend/radish.client/src/apps/shop/pages/ShopHome.module.css`
- `Frontend/radish.client/src/apps/shop/pages/ProductList.module.css`

## 保持不变

- 不新增业务 API、权限键、数据库结构、路由语义、登录回流或保存 / 提交载荷。
- 不修改公开论坛数据加载、评论提交、轻回应提交、商品购买、Docs 详情解析、榜单数据、公开主页数据或分享链接逻辑。
- 不回拉公开聊天室、内部调度中心、内部 Jobs 平台或独立移动 Console。
- 不进入 `P3-12-E`，不创建 tag，不进入 M15 测试 / 生产部署流程。

## 验证记录

- `npm run build --workspace=radish.client`：通过
- `npm run type-check --workspace=radish.client`：通过
- `npm run check:repo-hygiene:changed`：通过
- `git diff --check`：通过

本批是代码侧移动 UI 对齐，未执行 Gateway PC / mobile 真实页面 smoke。若后续准备阶段验收或用户明确要求运行态复核，应先等待用户明确说明前后端已经启动，再按 [浏览器联调与 Smoke 指南](/guide/browser-smoke) 覆盖 PC 与移动视图。

## 下一步

继续留在 `P3-12-D` UI 专题内，根据后续源码复核或真实页面验收结果判断是否需要 `P3-12-D53` 做公开移动任务流运行态补验 / 同类扫尾；当前不进入发布候选。
