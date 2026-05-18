# 公开详情 Head Smoke 验收

> 用途：部署或发布前快速确认 `P3-5-D1` 公开详情 HTML head 注入、动态 sitemap 与 robots 入口没有被前端 SPA shell 或反代配置覆盖。

## 前提

- Gateway、API、Frontend 均已启动或已部署。
- `GatewayService:PublicUrl` / `RADISH_PUBLIC_URL` 指向当前公开域名。
- `DownstreamServices:ApiService:BaseUrl` 与 `FrontendService:BaseUrl` 对 Gateway 可达。
- 至少准备三条真实公开详情路径：
  - forum：`/forum/post/{postPublicId 或旧 postId}`
  - docs：`/docs/{slug}`
  - shop：`/shop/product/{productId}`

## 自动 Smoke

```bash
npm run check:public-head-smoke -- --base-url https://radishx.com --path /forum/post/pst_xxx --path /docs/guide --path /shop/product/1001
```

本机 HTTPS 自签名证书默认对 `localhost / 127.0.0.1` 放宽；如需严格校验证书，追加：

```bash
npm run check:public-head-smoke -- --base-url https://localhost:5000 --path /forum/post/pst_xxx --path /docs/guide --path /shop/product/1001 --strict-tls
```

如生产 canonical 域名和检查用 base URL 不一致，追加：

```bash
npm run check:public-head-smoke -- --base-url https://gateway.internal --path /forum/post/pst_xxx --allow-external-canonical
```

脚本会检查：

- `/robots.txt` 包含 `Sitemap:`，且未被 SPA shell 覆盖。
- `/sitemap.xml` 返回 sitemap XML，且未被 SPA shell 覆盖。
- `/sitemap.xml` 与 sitemap index 中各分片的 `<loc>` 均为合法绝对 URL，默认要求 origin 与 `--base-url` 一致。
- 每条详情首包 HTML 的 `<head>` 包含非通用标题、description、canonical、Open Graph、Twitter card 和 `radish-public-jsonld`。
- JSON-LD 是合法 JSON，且包含 `https://schema.org`、`@type` 与 `url` 或 `mainEntityOfPage`。

仅验证脚本自身解析逻辑时执行：

```bash
npm run check:public-head-smoke -- --self-test
```

## 人工确认

- 直接查看三类详情的页面源代码，确认 head 内容已经在首包 HTML 中，不依赖浏览器执行 JS 后才出现。
- 确认 canonical 与页面运行时复制链接、动态 sitemap 中的 URL 口径一致。
- 确认未公开内容返回原 SPA 链路或 404 业务状态，不输出草稿、未发布文档、未上架商品的详情 head。
- 确认 `robots.txt` 指向的 sitemap URL 与生产 Gateway 实际返回一致。
