# P3-5-D 详情首包 HTML 可见性方案评审

> 完成日期：2026-05-17（Asia/Shanghai）

## 结论

- 不启动完整 SSR / SSG / 预渲染工程。
- 若继续实施，先做 `P3-5-D1 公开详情 HTML head 快照注入`。
- 首批只解决 head 可见性，不做正文 SSR；范围限定为 forum / docs / shop 公开详情。

## 现状判断

- `radish.client` 当前是纯 Vite SPA：`index.html` 只有站点级默认 title / description / Open Graph，详情标题、摘要、canonical 和 JSON-LD 都依赖前端运行时加载后写入。
- Gateway 当前只负责静态文件、Razor 门户页、健康检查和 YARP 代理；没有 React SSR、Node SSR 宿主或 HTML 渲染流水线。
- API 已有公开可读详情接口：forum 支持 long / `Post.PublicId` 双读，docs 支持 slug，shop 支持公开商品详情；这些可以作为首包 head 快照的数据来源。
- `P3-5-B` 和 `P3-5-C` 已分别补齐运行时 JSON-LD 与动态 sitemap。剩余问题集中在“不执行 JS 的爬虫 / 分享工具只能看到通用 SPA shell”。

## 路线评估

| 路线 | 做法 | 优点 | 风险 | 结论 |
| --- | --- | --- | --- | --- |
| 完整 SSR | 为 React 应用新增服务端入口，服务端渲染公开详情，再由浏览器 hydrate | 首包内容最完整，长期 SEO 能力最好 | 需要 Node 或 .NET SSR 宿主、浏览器 API 兼容治理、路由 / i18n / 数据预取重构、Tauri 与 WebOS 壳层兼容复核；当前会牵动过大 | 不作为首批路线 |
| 构建期 SSG / 预渲染 | 构建或发布时枚举公开 URL，预生成详情 HTML | 运行时稳定，CDN 友好 | 需要生产数据、构建凭据和重新生成机制；内容发布后容易过期；forum / shop 高频变化不适合塞进前端构建 | 不作为首批路线，仅保留为夜间离线导出候选 |
| Gateway HTML rewrite | Gateway 识别公开详情路径，读取 SPA `index.html`，注入详情 title / meta / canonical / JSON-LD 后返回 | 不改前端构建形态，能直接解决无 JS head 可见性 | Gateway 会承担 HTML 拼接和 API 数据依赖；若无缓存和严格范围，容易演化成隐性 SSR 层 | 可作为窄方案，但必须限于 head 注入 |
| API 快照 + Gateway head 注入 | API 提供公开详情 head snapshot；Gateway 只做缓存、路径识别和 head 占位替换，不拼业务正文 | 数据边界在 API，Gateway 职责较窄；可复用公开过滤和 canonical 口径；回滚简单 | 仍需新增 Gateway 中间件、缓存 TTL、失败回退和生产域名配置校验 | 推荐作为后续首批实施路线 |

## 推荐方案

若继续推进，建议命名为 `P3-5-D1 公开详情 HTML head 快照注入`，首批只解决 head 可见性：

- API 新增公开详情 head snapshot 能力，按目标类型返回 `title / description / canonicalUrl / ogType / ogImage / publishedAt / modifiedAt / jsonLd`。
- Gateway 只对公开详情路径生效：`/forum/post/{postKey}`、`/docs/{slug}`、`/shop/product/{productId}`。
- Gateway 命中公开详情路径时读取前端 `index.html`，把默认 head 替换或插入为 snapshot head；`#root` 仍保持空容器，由现有 SPA 接管交互。
- 缓存 TTL 首批建议 `10-30` 分钟，按目标类型 + route key + 公开域名建 key；API 或 snapshot 失败时返回原始 SPA `index.html`，不影响页面可打开。
- robots、sitemap、canonical、运行时 `publicHead` 和 JSON-LD 需要保持同一 URL 口径；浏览器运行时加载详情后可以覆盖同名 head，但不得与首包 canonical 冲突。

## 范围与边界

首批纳入：

- forum detail：只对公开可读帖子生成 head，优先使用 `Post.PublicId` canonical。
- docs detail：只对公开已发布 slug 文档生成 head。
- shop detail：只对公开可见商品生成 head。

首批不纳入：

- 公开个人页 `/u/:id`：仍涉及用户展示意愿、隐私和 long 用户 ID 公开契约，需单独评审。
- discover、forum 列表、docs 搜索、shop 列表、标签组合页和分页组合页。
- 登录态路径、Console、Auth、API 文档、Scalar、Hangfire、订单 / 背包 / 治理路径。
- 详情正文 SSR、评论 SSR、React hydrate 改造、Vite SSR 架构迁移和 Node SSR 运行时。

## 部署风险与护栏

- Gateway 必须只在生产 / 预生产公开入口启用，开发环境默认可关闭或返回原始 SPA，避免影响 Vite HMR。
- `RADISH_PUBLIC_URL` / `GatewayService:PublicUrl` 必须是生产域名，head snapshot 不允许输出 `localhost` canonical。
- 注入应使用固定占位或可靠 HTML parser / encoder，不用脆弱字符串拼接覆盖任意 `<head>` 片段；所有用户内容必须 HTML 转义，JSON-LD 必须安全序列化。
- API snapshot 查询必须沿用公开过滤，不得暴露草稿、私有、认证可见、下架、禁用或软删除内容。
- 回退策略必须简单：snapshot 超时、404、异常或缓存不可用时返回原始 SPA shell，并记录日志；不因 SEO 增强阻断用户访问。

## 后续实施完成条件

- 访问 `/forum/post/{publicId}`、`/docs/{slug}`、`/shop/product/{id}` 的首包 HTML 中能看到对应 title、description、canonical、Open Graph 和 JSON-LD。
- 无 JS 环境下首包 head 与运行时 `publicHead` / `publicStructuredData` 输出口径一致。
- API snapshot、Gateway 注入、缓存命中、异常回退和 HTML / JSON 转义均有测试覆盖。
- `dotnet test Radish.Api.Tests`、`dotnet build Radish.slnx -c Debug`、`npm run type-check --workspace=radish.client` 和文本卫生检查通过。
