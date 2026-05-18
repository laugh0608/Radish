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

失败时会输出定位信息：

- `request-url`：实际请求 URL。
- `stage`：失败阶段，例如 `request`、`status`、`robots assertion`、`sitemap xml assertion`、`sitemap loc assertion` 或 `public head assertion`。
- `status-code` 与 `content-type`：用于判断是否被 Gateway 路由、API、前端静态服务或反代错误接管。
- `suspected-spa-shell`：响应是否疑似回落到通用 SPA shell。
- `body-preview`：响应 body 前段，便于快速分辨 XML、HTML shell、错误页或 API 异常信息。
- `assertions`：本次失败的关键断言。

常见分诊方向：

- `stage=request`：优先检查 Gateway 是否可达、证书 / TLS、DNS、端口和反代链路。
- `stage=status`：优先看 Gateway route、API 转发、公开详情是否存在或是否满足公开过滤。
- `suspected-spa-shell=yes` 且目标是 `robots.txt` / `sitemap.xml`：优先检查 Gateway 顶层路由优先级，避免被前端 catch-all 覆盖。
- `sitemap loc assertion` 失败：优先检查 `GatewayService:PublicUrl` / `RADISH_PUBLIC_URL`、`X-Forwarded-Proto` 与 `X-Forwarded-Host`。
- `public head assertion` 失败：优先检查 API head snapshot、Gateway 注入中间件、Frontend 入口 HTML 可达性和详情公开状态。

仅验证脚本自身解析逻辑时执行：

```bash
npm run check:public-head-smoke -- --self-test
```

## 人工确认

- 直接查看三类详情的页面源代码，确认 head 内容已经在首包 HTML 中，不依赖浏览器执行 JS 后才出现。
- 确认 canonical 与页面运行时复制链接、动态 sitemap 中的 URL 口径一致。
- 确认未公开内容返回原 SPA 链路或 404 业务状态，不输出草稿、未发布文档、未上架商品的详情 head。
- 确认 `robots.txt` 指向的 sitemap URL 与生产 Gateway 实际返回一致。
