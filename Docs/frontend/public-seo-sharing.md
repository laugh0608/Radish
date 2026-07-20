# 公开内容 SEO 与分享基线

> 本文从 [前端设计文档](/frontend/design) 拆出，承载专题细节；设计入口只保留当前结论、边界和导航。

### 10.5 公开内容 SEO 与分享基线

> 仅 `radish.client` 的公开内容壳层需要对搜索引擎和外链分享友好；`radish.console`、登录后 WebOS 工作台和治理类页面默认不做 SEO 要求。
>
> 当前已完成首批公开增长基线：运行时 head / canonical、运行时公开域名配置、公开详情分享入口、公开个人页复制链接入口、公开详情与集合页运行时 JSON-LD、API + Gateway 动态 sitemap、浏览器可见资源 URL 归一，以及公开集合页和 forum / docs / shop 公开详情首包 head snapshot 注入。完整正文 SSR / SSG、预渲染和公开个人页动态 sitemap 继续后置。

##### 10.5.1 当前公开 URL 范围

- 公开内容壳层当前覆盖：
  - `/discover`
  - `/forum`、`/forum/search`、`/forum/category/:categoryId`、`/forum/tag/:tagSlug`、`/forum/question`、`/forum/poll`、`/forum/lottery`
  - `/forum/post/:postId`
  - `/docs`、`/docs/search`、`/docs/:slug`
  - `/u/:identifier`，其中 `identifier` 优先为 `User.PublicId`，旧 LongId 字符串保留兼容读取
  - `/leaderboard`
  - `/shop`、`/shop/products`、`/shop/product/:productId`
  - `/legal`（公开承诺与社区边界页）
- 登录后功能不进入公开 SEO 范围，例如发帖、编辑、订单、背包、设置、Console 与治理后台。
- `/workbench` 是正式 Web 功能地图，不进入 sitemap、canonical 或公开详情分享范围；其“公开阅读”分组提供真实 `/legal` 入口。`/legal` 当前具备运行时 head / JSON-LD，但尚未进入 Gateway 首包 snapshot 与静态 sitemap；若后续确认正式索引，再与收录策略一并补齐。
- 桌面工作台入口继续由 `/desktop` 承载；公开 URL 命中时优先渲染公开内容壳层，而不是完整 WebOS 桌面 Shell。
- `/circle` 是登录后“我的圈子”关系流入口，不进入公开 SEO、sitemap、canonical 或分享页范围；它可以在当前标签页中把来源状态一次性交接给后续公开详情，但不能把圈子来源写入公开 URL。

##### 10.5.2 当前 head 契约

`Frontend/radish.client/src/public/PublicHeadLifecycle.tsx` 是公开壳层 head 和 JSON-LD 的唯一运行时 DOM writer，`publicHead.ts` 和 `publicStructuredData.ts` 只提供构建与 DOM helper。公开页面进入或路由切换时，owner 先按当前 UI locale 写入路由基线；搜索词、分类、标签、榜单类型、商品、文档、帖子与用户名称等用户或运营内容保留原文。页面取得真实数据后只通过 `usePublicHeadSnapshot()` 提交 data-driven 快照：集合页可只覆盖 head，详情页同时提交 head + JSON-LD；任何页面都不得直接写 `document.head` 或 `document.title`。owner 统一维护：

- `document.title`
- `meta[name="description"]`
- `link[rel="canonical"]`
- `meta[property="og:title"]`
- `meta[property="og:description"]`
- `meta[property="og:url"]`
- 可选 `meta[property="og:image"]`
- `meta[name="twitter:card"]`、title / description 与可选 image

`Frontend/radish.client/index.html` 只提供 SPA 首包的中文语言、站点默认 title、description 与 Open Graph 基线；页面级标题、摘要、canonical 和分享 URL 由运行时 head helper 根据公开路由补齐。

公开壳层离开时，owner 必须清理 canonical、公开路由 Open Graph / Twitter 状态和 JSON-LD，并恢复站点级 title / description 基线，避免私域页面继承上一个公开详情的元信息。

所有 data-driven 快照注册同时绑定 owner token 和当前规范 route key；提交快照前还必须验证实体身份与当前 route 一致，不能把旧商品、用户、文档或帖子数据注册到新路由。路由切换后旧 key 快照立即失效，旧组件 cleanup 只能清除自己的 token，不得清掉新页 owner；request id、cancelled guard 和 keyed remount 负责阻止旧异步结果回写，但不能替代 source identity 校验。该顺序同时必须能承受 React StrictMode 的 effect setup / cleanup 重放。

公开集合页和 forum / docs / shop 三类公开详情还会在 Gateway 层做首包 head snapshot 注入：API 提供公开详情 head snapshot，Gateway 在返回前端入口 HTML 前注入 `<title>`、description、canonical、Open Graph、Twitter card 与 JSON-LD。该能力只改首包 `<head>`，不渲染正文 HTML，不改变 React hydrate，也不替代运行时 `publicHead`。

当前 Gateway 注入范围如下：

- 静态公开集合页：`/discover`、`/forum`、`/docs`、`/leaderboard`、`/shop`，对应 `/api/public-head/static/{routeKey}`。
- 公开详情页：`/forum/post/{postPublicId 或旧 postId}`、`/docs/{slug}`、`/shop/product/{productId}`。
- `GET` / `HEAD` 且 `Accept` 允许 HTML 的请求才进入注入；WebSocket、API、静态资源和非 HTML 请求不进入该链路。
- `/docs/search`、公开个人页 `/u/:identifier`、登录后页面、Console 和治理后台暂不进入 Gateway 首包 head snapshot。

API 侧根据 `GatewayService:PublicUrl` / `RADISH_PUBLIC_URL` 生成公开 canonical；缺省时允许使用安全的 `X-Forwarded-Proto / X-Forwarded-Host` 回推公开 origin。Gateway 调 API 时会根据同一公开 URL 配置补 forwarded headers，避免 API 在反代后生成内部地址。

缓存和回退边界如下：

- API head snapshot 通过统一缓存入口缓存 `20` 分钟；Gateway 前端入口 HTML 缓存 `5` 分钟；Gateway 已注入 HTML 按 `Host + snapshot API path` 缓存 `10` 分钟。
- API snapshot 返回 `404`、下游请求失败、前端入口 HTML 不可达、注入异常或快照为空时，Gateway 必须失败开放到原 SPA 链路，不输出不完整或错误的 `<head>`。
- 失败开放只保证用户仍可访问页面，不代表 SEO / 分享预览合格；部署或发布前仍需跑 public head smoke。

##### 10.5.3 canonical 与分享入口

- canonical URL 默认以 `https://radishx.com` 作为公开域名 seed；运行时可通过 `window.__RADISH_RUNTIME_CONFIG__.publicUrl` 覆盖，避免本地、测试、生产环境复制出错误公开域名。
- `buildPublicCanonicalUrl()` 只接受规范化后的公开路径，不把登录后工作台路径写入 canonical。
- `buildPublicCanonicalUrl()` 会移除 hash anchor；`buildPublicShareUrl()` 保留公开路径中的 hash anchor，用于 docs 详情复制当前章节链接。
- canonical、sitemap、分享链接和 Gateway 首包 head snapshot 都必须基于服务端可见的公开真实路径，不基于 `/shell#...`、`/desktop#...` 或其他仅客户端可见的 hash 状态。
- forum detail、shop detail、docs detail 和公开个人页都应复制 canonical 公共链接，而不是复制当前浏览器里可能带有临时状态、来源参数或工作台上下文的 URL。
- Flutter 原生 forum / docs / shop detail 的“公开链接”展示和复制也沿用同一口径：当前 Gateway Base URL 加 Web 公开路由，不复制 Flutter 内部 handoff、`radish://` deep link、API 地址、来源 tab 或评论定位状态。
- Flutter forum detail 只有在 `PostVo.VoPublicId` 可用时生成 `/forum/post/:postPublicId` 复制链接；旧 long `postId` 仍可作为打开兼容 fallback，但不作为面向用户的分享地址。
- Flutter docs detail 使用 `/docs/:slug` 复制链接，并继续避免把 16 位以上纯数字旧 long 路径当作公开可读 slug；shop detail 使用 `/shop/product/:productId`。
- 公开详情来源返回当前使用 `history.state` 保存，不写入 URL query；刷新或浏览器历史恢复后可继续返回原公开来源，但复制链接、canonical、Open Graph 与 sitemap 都必须忽略该状态。
- 公开详情加载完成后如果需要把旧标识或非规范路径替换为 canonical 路径，例如 `/u/:longId -> /u/usr_...`、docs slug 归一或 forum 旧 long 路径归一，`replaceState` / 应用内 `replace` 必须保留当前标签页的来源返回状态；规范化 URL 不能把“返回社区发现 / 我的圈子 / 我的状态 / 消息”等语义清空。
- forum detail 在 `PostVo.VoPublicId` 可用时使用 `/forum/post/:postPublicId` 作为 canonical 和复制链接；旧 long 版 `/forum/post/:postId` 继续兼容读取。
- 旧 long 版 forum detail 如果加载成功并拿到 `PostVo.VoPublicId`，运行时 canonical、Open Graph URL 与 JSON-LD 也必须刷新到 `/forum/post/:postPublicId`，避免同一帖子同时暴露两套分享预览主口径。
- 公开壳层内部生成 forum detail 链接时也应优先使用 `PostVo.VoPublicId`，包括 `/discover` 摘要卡、forum 列表 / 搜索 / 标签页、类型流、个人公开页内容入口和圈子内容跳转；只有缺少 PublicId 时才回退旧 long 字符串。
- 公开内容卡片必须输出真实公开 `href`。普通点击可用 `history.state` 或当前标签页一次性转移状态保留来源返回；右键打开新标签、复制链接、canonical、OpenGraph、JSON-LD、sitemap 和 Gateway 首包 head snapshot 只基于真实公开路径。
- forum detail 登录参与参数只允许用于认证回流，例如 `/forum/post/:postId?intent=comment` 或 `/forum/post/:postId?commentId=:commentId&intent=quickReply`；这些参数不属于 canonical、分享链接、OpenGraph、JSON-LD 或 sitemap。
- 公开个人页在 `UserPublicProfileVo.VoPublicId` 可用时使用 `/u/:publicId` 作为 canonical 和复制链接；旧 LongId 版 `/u/:userId` 继续兼容读取，加载成功后运行时应规范化到 `/u/usr_...`。
- 公开个人页的可见名称、title、description、Open Graph 和 JSON-LD 优先使用 `UserPublicProfileVo.VoDisplayHandle`，缺失时再由 `VoDisplayName + VoPublicIndex` 派生；不得使用 `LoginName`、`Email`、内部 `VoUserId` 或旧 LongId 路径作为普通用户可见名称。
- shop detail 当前仍以 `/shop/product/:productId` 作为 canonical 兼容路径，但运行时 title、description 与页面说明不应直接回显 `productId`；docs detail 同理优先展示 slug、标题或正文摘要，旧 long 兼容路径只承担打开能力，不作为普通用户可读文案。

##### 10.5.4 结构化数据

`Frontend/radish.client/src/public/publicStructuredData.ts` 负责运行时 JSON-LD 的构建与 DOM helper；实际注入、替换和清理由 `PublicHeadLifecycle` 与同一份页面快照协调。

- 公开集合页：`discover / leaderboard / forum / docs / shop` 的非详情路由输出 `CollectionPage`；其他公开非详情路由输出 `WebPage`。fallback JSON-LD 必须复用 owner 最终解析的本地化 head，保证 `name / description` 与页面当前语言一致。
- forum detail：输出 `BlogPosting`，优先使用 `PostVo.VoPublicId` canonical。
- docs detail：输出 `Article`，使用 `/docs/:slug` canonical。
- shop detail：输出 `Product`，不把积分价格伪装成法币 offer。
- 公开个人页：输出 `ProfilePage / Person`，canonical 优先使用 `User.PublicId`，`name` 优先使用 `DisplayHandle`，不把长数字用户 ID、登录名或邮箱当作可见名称。
- 路由切换、详情数据缺失或组件卸载时必须清理旧 JSON-LD，避免公开详情之间残留错误结构化数据。

##### 10.5.5 robots 与动态 sitemap

- `Frontend/radish.client/public/robots.txt` 仍作为静态抓取入口约束，允许公开内容壳层，禁止 Console、认证回调、桌面工作台和登录后治理类路径，并指向公开域名下 `/sitemap.xml`。
- `/sitemap.xml` 与 `/sitemaps/{fileName}` 当前由 Gateway 高优先级路由转发到 API，不应被前端 SPA catch-all 覆盖。
- API 输出 sitemap index 与 `static / forum / docs / shop` 分片；forum 优先使用 `/forum/post/{VoPublicId}`，docs 使用 `/docs/{slug}`，shop 使用 `/shop/product/{productId}`。
- sitemap `<loc>` 必须使用公开 Gateway origin。配置优先使用 `GatewayService:PublicUrl` / `RADISH_PUBLIC_URL`；经 Gateway 转发到 API 时，允许使用安全的 `X-Forwarded-Proto / X-Forwarded-Host` 回推公开 origin。
- 首批不把 `/u/:identifier` 公开个人页纳入动态 sitemap；即使 User PublicId 已落地，动态 sitemap 仍需先完成用户隐私、展示意愿和收录策略评审。

##### 10.5.6 浏览器可见资源 URL

公开页面里的图片、头像、favicon、商品媒体和 Markdown 附件应通过统一资源解析工具进入浏览器，不应在 HTTPS Gateway 页面中继续暴露本地 HTTP 资源。

- 当前通过 `normalizeBrowserVisibleUrl()` 做本地开发态归一：当页面运行在 `https://localhost:5000` 这类 HTTPS Gateway origin 下，且资源地址是 `http://localhost`、`http://127.0.0.1` 或 `http://[::1]` 时，前端会把资源 origin 改写为当前 Gateway origin，并保留 path / query / hash。
- 该归一只处理本地 HTTP 资源到当前 HTTPS Gateway 的可见地址，不改写公网、CDN、非 localhost 或非 HTTPS 当前入口。
- 该能力用于减少浏览器 mixed content、Schemeful Same-Site Cookie 与资源地址漂移提示；它不是 CDN 代理、鉴权绕过或附件权限放宽机制。

##### 10.5.7 SSR / SSG 后置边界

当前已落地的是“静态 SPA + 运行时 head + 动态 sitemap + 详情首包 head 注入”的公开内容基线，不要求立即改成 SSR / SSG。

若后续进入 SSR / SSG 专题，可选方向包括 Vite SSR、Next.js、Remix 或 Astro；但在正式立项前，前端不得假定存在 `start:ssr` 脚本或 Node SSR 服务。只有搜索抓取、分享预览或真实增长数据证明正文首包缺失造成核心问题时，才重新评估正文 HTML 预渲染或完整 SSR / SSG。

##### 10.5.8 验证入口

- `npm run check:public-head-smoke`：部署后检查 robots、sitemap 与公开集合页 / 详情页首包 head。
- `Docs/guide/public-head-smoke.md`：记录 sitemap `<loc>` public origin 抽查、外部 canonical 允许参数和本地 HTTPS 自签名证书说明。
- `node --test --test-isolation=none ./tests/publicStructuredData.test.ts ./tests/publicHead.test.ts`：覆盖运行时 JSON-LD 与公开 head helper。
