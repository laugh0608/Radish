# 公开内容 SEO 与分享基线

> 本文从 [前端设计文档](/frontend/design) 拆出，承载专题细节；设计入口只保留当前结论、边界和导航。

### 10.5 公开内容 SEO 与分享基线

> 仅 `radish.client` 的公开内容壳层需要对搜索引擎和外链分享友好；`radish.console`、登录后 WebOS 工作台和治理类页面默认不做 SEO 要求。

##### 10.5.1 当前公开 URL 范围

- 公开内容壳层当前覆盖：
  - `/discover`
  - `/forum`、`/forum/search`、`/forum/category/:categoryId`、`/forum/tag/:tagSlug`、`/forum/question`、`/forum/poll`、`/forum/lottery`
  - `/forum/post/:postId`
  - `/docs`、`/docs/search`、`/docs/:slug`
  - `/u/:userId`
  - `/leaderboard`
  - `/shop`、`/shop/products`、`/shop/product/:productId`
- 登录后功能不进入公开 SEO 范围，例如发帖、编辑、订单、背包、设置、Console 与治理后台。
- 桌面工作台入口继续由 `/desktop` 承载；公开 URL 命中时优先渲染公开内容壳层，而不是完整 WebOS 桌面 Shell。

##### 10.5.2 当前 head 契约

`Frontend/radish.client/src/public/publicHead.ts` 是公开壳层的统一 head helper。公开页面进入、路由切换和详情数据加载后，应通过该 helper 统一维护：

- `document.title`
- `meta[name="description"]`
- `link[rel="canonical"]`
- `meta[property="og:title"]`
- `meta[property="og:description"]`
- `meta[property="og:url"]`
- 可选 `meta[property="og:image"]`

`Frontend/radish.client/index.html` 只提供 SPA 首包的中文语言、站点默认 title、description 与 Open Graph 基线；页面级标题、摘要、canonical 和分享 URL 由运行时 head helper 根据公开路由补齐。

##### 10.5.3 canonical 与分享入口

- canonical URL 当前统一以 `https://radishx.com` 作为公开域名 seed。
- `buildPublicCanonicalUrl()` 只接受规范化后的公开路径，不把登录后工作台路径写入 canonical。
- forum detail、shop detail 和 docs detail 都应复制 canonical 公共链接，而不是复制当前浏览器里可能带有临时状态、来源参数或工作台上下文的 URL。
- forum detail 在 `PostVo.VoPublicId` 可用时使用 `/forum/post/:postPublicId` 作为 canonical 和复制链接；旧 long 版 `/forum/post/:postId` 继续兼容读取。

##### 10.5.4 robots 与 sitemap seed

- `Frontend/radish.client/public/robots.txt` 当前作为静态抓取入口约束，允许公开内容壳层，禁止 Console、认证回调、桌面工作台和登录后治理类路径。
- `Frontend/radish.client/public/sitemap.xml` 当前是静态 seed，只包含公开入口和公开列表页，不包含动态详情页。
- 动态详情 sitemap、按热度 / 时间生成详情 URL、结构化数据和详情首包 HTML 可见性仍是后续专题，不并入当前 SPA 运行时 head 基线。

##### 10.5.5 SSR / SSG 后置边界

当前已落地的是“静态可见 + SPA 运行时一致”的公开内容基线，不要求立即改成 SSR / SSG。SSR / SSG、JSON-LD 和详情首包完整 HTML 属于后续增长专题，需要与后端查询、缓存、部署和爬虫策略一起评估。

若后续进入 SSR / SSG 专题，可选方向包括 Vite SSR、Next.js、Remix 或 Astro；但在正式立项前，前端不得假定存在 `start:ssr` 脚本或 Node SSR 服务。

##### 10.5.6 结构化数据后置示例

后续若为帖子详情页输出 JSON-LD，可参考：

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "DiscussionForumPosting",
  "headline": "{帖子标题}",
  "datePublished": "{ISO 时间}",
  "author": {
    "@type": "Person",
    "name": "{作者昵称}"
  }
}
</script>
```

##
