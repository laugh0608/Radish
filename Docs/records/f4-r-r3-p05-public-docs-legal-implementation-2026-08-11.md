# F4-R R3-P05 Public Docs 与 Legal 正式实现

> 日期：2026-08-11（Asia/Shanghai）
>
> 状态：正式代码、静态回归与 Gateway PC / mobile 成组验收已完成，R3-P05 关闭
>
> 范围：`/docs` 列表、搜索、详情、保留 slug 与 Legal；不修改 API、数据库、migration、权限、Pencil 或文档业务状态机

## 结论

R3-P05 已完成正式页面实现、静态门禁和 Gateway 运行态验收。Public Docs 由单个 `1554` 行组件拆为轻量编排器、列表、搜索、详情、从属栏、状态组件与滚动 / 链接支持逻辑；页面继续复用原路由与公开壳层，没有建立万能卡片或第二套导航。Docs 与 Legal 均形成单一主阅读轴，Mobile 主体先于辅助信息且不依赖整列锚点按钮。

浏览器验收同时关闭三个路由 / head 根因：Legal hash 现由 route state 往返并在 React 内容挂载后定位；Docs 详情路由在权威数据确认 Public 前默认 `noindex`；只有 Public 文档快照会显式恢复 canonical、公开 head 与 JSON-LD。匿名、种子 Admin、PC / mobile、双语和代表主题均通过，服务与临时视口已经清理，R3-P05 正式关闭。

## 已完成范围

### Public Docs 职责所有权

- `PublicDocsApp.tsx` 从 `1554` 行降至 `279` 行，只保留集合读取、诊断、浏览滚动快照、来源返回与路由分发。
- 列表、搜索和详情分别由 `PublicDocsList.tsx`、`PublicDocsSearch.tsx`、`PublicDocsDetail.tsx` 承担；共享 rail、状态卡与滚动 / 链接逻辑按真实职责拆分。
- 列表 PC 从三列竞争结构收敛为“最新文档 + 紧凑目录”的连续主列和统计 / 作者辅助栏；搜索主结果先于阅读说明；详情正文主栏先于阅读说明与相关文档。
- 删除搜索、返回、元数据和编辑动作在主区与 rail 的重复展示；作者动作统一消费双语资源。
- `PublicDocsApp.module.css` 从 `1039` 行降至 `937` 行，删除未使用的旧状态卡、旧列表布局和重复元数据样式；颜色继续只消费语义 token。

### Legal 阅读顺序

- 页面顺序固定为 Hero、承诺摘要、安全边界、章节导航、正文。
- Mobile 章节导航改为单行横向滚动 pill，不再把每个章节入口堆成整宽按钮；正文仍维持单列阅读。

### 稳定契约

- 保留 `/docs`、搜索分页、`/__documents__` 保留 slug、正文锚点、Markdown 内链、来源返回、canonical、公开 head / JSON-LD 与 keyed remount。
- 保留 F4-L-C 的可选认证通用 `Wiki/GetList`、`Wiki/GetTree`、`Wiki/GetBySlug`：匿名只读取 Public，登录用户按服务端 ACL 扩展到 Authenticated / Restricted 已发布文档。
- 非 Public 详情继续不提交公开 SEO head；非 Public 封面、正文图片和文件继续使用认证 Blob 契约。
- 有权作者仍可从只读详情回流正式 Author 编辑入口；发布、版本和治理继续留在 Author / Console。
- Legal `/legal#section` 深链保留 hash；首次从其他路由进入时，在内容挂载后按受控章节锚点定位，canonical 继续去除 fragment。

## 文档口径修正

- `document-system.md` 已把 `/docs` 修正为按当前身份读取已发布文档的统一只读面。
- `public-web-unified-experience-design.md` 保留 2026-07-11 `PublicGet*` 历史记录，并新增 2026-07-26 F4-L-C 后续口径说明，明确后者取代前者的数据源实现结论。
- R3 静态契约新增职责拆分、主区顺序、Legal 层级、Mobile 锚点、当前身份读取、受保护附件和主题 token 守卫。

## 静态验证

- `radish.client` 测试：`545 / 545` 通过。
- `radish.client` type-check、Lint、production build：通过。
- `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check`：通过。
- `npm run validate:baseline:quick`：本批相关门禁通过，但总入口仍被既有 `Radish.Repository/SystemConfigStorageCoordinator.cs` 三处 `DateTime.Now` baseline 漂移拦截；该文件不在本批差异中，继续留在独立维护线。
- production build 继续报告仓库既有的大 chunk 提示，本批没有新增依赖或扩张对应载荷。

## Gateway 运行态验收

- 通过 `https://localhost:5000` 覆盖 PC `1920 × 1080` 与 Mobile `390 × 844`；Browser 视口能力不支持配置 DPR，实际分别为 `2 / 1`，不把工具限制写成产品结论。
- 匿名覆盖 `/docs` 列表、`q=文档` 搜索、普通详情、搜索来源返回、前进 / 后退和 `__documents__` 兼容路由；兼容路由归一到正式 `/docs/:slug` 且保留 fragment。
- 种子 Admin 登录态比匿名多读取一篇 `Authenticated + Draft` 文档，列表总数由 `643` 增至 `644`；作者台 `/docs/mine` 与有权文档 `/docs/edit/:id` 回流通过。
- 非 Public 种子文档稳定为 `noindex, nofollow`，无 canonical、`og:url` 或 JSON-LD；Public 文档 canonical 与一份 JSON-LD 正常恢复。种子数据没有受保护附件，认证 Blob 继续由静态合同和既有 F4-L 回归守卫。
- Legal 的 Hero—摘要—安全边界—章节导航—正文 DOM 顺序通过；Mobile 页面宽度保持 `390px`，章节 rail 为 `nowrap + overflow-x: auto`，自身可滚动但不造成页面横向溢出。
- `zh-CN / en-US`、`default / guofeng` 代表状态通过；Docs 列表 / 搜索 / 详情和 Legal 均无页面横向溢出，最终稳定详情页 `0 warning / 0 error`。
- 验收发现并修正 Legal fragment 被 canonical 规范化抹除、首次深链早于 React 挂载无法定位，以及非 Public Docs 仍沿用公开详情基线 head 三项问题；定向测试与浏览器复验均通过。
- 本轮启动的 Gateway、API、Auth、Client 与 Console 已停止，`5000 / 5100 / 5200 / 3000 / 3100` 端口均释放。

R3-P05 关闭，下一顺位为 `R3-P06 Public Shop 浏览与 Leaderboard` 实施方案。
