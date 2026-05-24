# P3-6 公开增长部署观察记录（2026-05-18）

## 基本信息

- 日期：2026-05-18（Asia/Shanghai）
- 环境：local
- 公开域名：`https://localhost:5000`
- 服务版本 / commit：`eb32c07f`
- 记录人：laugh0608

## 观察范围

| 范围 | 入口 | 结论 | 备注 |
| --- | --- | --- | --- |
| 公开 head smoke | `npm run check:public-head-smoke -- --base-url https://localhost:5000 --path ...` | 通过 | 覆盖 robots、sitemap index、`static / forum / docs / shop` 分片和 forum / docs / shop 三类详情 |
| 动态 sitemap | `/sitemap.xml`、`/sitemaps/*.xml` | 通过 | sitemap index 与四个分片均可达，分片 `<loc>` origin 与 Gateway base URL 一致 |
| head snapshot | forum / docs / shop 公开详情首包 HTML | 通过 | 三类详情均通过首包 head 断言，覆盖 title、description、canonical、OG、Twitter 和 JSON-LD |
| 公开域名配置 | `GatewayService:PublicUrl` / `RADISH_PUBLIC_URL` | 通过 | 本地 Gateway 输出 `https://localhost:5000` 口径，未再出现 API 内部 origin 外泄 |
| 分享预览 | testing 可用分享工具 / release 前真实平台预览 | 未执行 | 本轮仅做本地 Gateway smoke；真实平台分享预览放到 release 前置项 |
| 搜索抓取反馈 | release 前 Search Console / 爬虫日志 / 访问日志 | 未执行 | 本轮没有搜索抓取或爬虫日志样本；生产抓取反馈放到 release 前置项 |

## 命令与结果

```bash
npm run check:public-head-smoke -- --base-url https://localhost:5000 --path /forum/post/pst_019e304093b374a8a20c9461f31a2302 --path /docs/guide --path /shop/product/100061
```

- 结果：通过。
- 失败诊断摘要：无失败诊断输出。
- 未覆盖项：testing URL、release 前生产公开域名、真实分享平台预览、搜索引擎抓取反馈和生产运行日志。

通过项：

- `robots.txt`
- `sitemap.xml`
- `sitemap shard /sitemaps/static.xml`
- `sitemap shard /sitemaps/forum-1.xml`
- `sitemap shard /sitemaps/docs-1.xml`
- `sitemap shard /sitemaps/shop-1.xml`
- `/forum/post/pst_019e304093b374a8a20c9461f31a2302`
- `/docs/guide`
- `/shop/product/100061`

## 分流结论

| 问题 | 等级 | 处理方式 | 负责人 / 后续入口 |
| --- | --- | --- | --- |
| 本地 Gateway 公开 head smoke 未发现失败 | 暂不处理 | 保持观察 | 后续 testing 观察记录 |
| testing URL 尚未记录 | 暂不处理 | 等 testing 环境可用后按同一模板记录 | `P3-6` 观察线 |
| 生产分享预览与搜索抓取反馈暂无样本 | 暂不处理 | 作为 release 前置项处理 | release 前公开增长 smoke |

等级判断：

- 本轮未发现阻断公开访问、核心 head / sitemap、分享入口或回流的 `P0/P1`。
- 本轮也未发现需要成组排入下一小批次的 `P2`。
- 未执行项属于 testing / release 前置验证范围，不用当前批次扩成平台化工程。

## 结论

- 本轮是否发现新的 `P0/P1`：否。
- 是否需要切出修复小闭环：否。
- 是否保持观察：是，后续优先补 testing URL 观察记录；生产公开域名、真实分享预览和搜索抓取反馈放到 release 前置项。
- 明确不做：
  - 不启动 SSR / SSG 或正文预渲染。
  - 不启动完整 E2E 或运营平台。
  - 不启动全量 `PublicId` 迁移。
  - 不把生产域名部署作为日常继续开发的阻塞项。
