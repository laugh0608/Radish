# P3-6 公开增长部署观察记录（2026-05-20）

## 基本信息

- 日期：2026-05-20（Asia/Shanghai）
- 环境：production
- 公开域名：`https://radishx.com`
- 服务版本 / commit：`26.5.5`（用户确认 2026-05-19 已部署；本轮未从公开接口核对 commit）
- 记录人：Codex

## 观察范围

| 范围 | 入口 | 结论 | 备注 |
| --- | --- | --- | --- |
| 公开 head smoke | `npm run check:public-head-smoke -- --base-url https://radishx.com --path ...` | 通过 | 覆盖 robots、sitemap index、`static / forum / docs / shop` 分片和 forum / docs / shop 三类详情 |
| 动态 sitemap | `/sitemap.xml`、`/sitemaps/*.xml` | 通过 | sitemap index 暴露 `static / forum / docs / shop` 四个分片，脚本已校验分片可达与 `<loc>` origin |
| head snapshot | forum / docs / shop 公开详情首包 HTML | 通过 | 三类详情均通过首包 head 断言，覆盖 title、description、canonical、OG、Twitter 和 JSON-LD |
| 公开域名配置 | `https://radishx.com` | 通过 | 本轮断言未发现 API 内部 origin 外泄或 canonical origin 偏移 |
| 分享预览 | 真实平台分享卡片 / 外部分享调试工具 | 未执行 | 本轮只执行公开 head smoke；未使用微信、社交平台或搜索平台的分享预览工具 |
| 搜索抓取反馈 | Search Console / 爬虫日志 / 访问日志 | 未执行 | 本轮没有搜索抓取或爬虫日志样本；继续作为 release / 运营观察项 |

## 命令与结果

```bash
npm run check:public-head-smoke -- --base-url https://radishx.com --path /forum/post/pst_019e452945b873dcb7658377d39c8a26 --path /docs/changelog-2026-04-week4 --path /shop/product/100062
```

- 结果：通过。
- 失败诊断摘要：无站点断言失败。首次沙盒内执行因公网访问权限返回 `EACCES`，使用同一命令授权公网访问后通过。
- 未覆盖项：真实平台分享预览、Search Console 反馈、爬虫访问日志和生产服务端 commit 核对。

通过项：

- `robots.txt`
- `sitemap.xml`
- `sitemap shard /sitemaps/static.xml`
- `sitemap shard /sitemaps/forum-1.xml`
- `sitemap shard /sitemaps/docs-1.xml`
- `sitemap shard /sitemaps/shop-1.xml`
- `/forum/post/pst_019e452945b873dcb7658377d39c8a26`
- `/docs/changelog-2026-04-week4`
- `/shop/product/100062`

## 分流结论

| 问题 | 等级 | 处理方式 | 负责人 / 后续入口 |
| --- | --- | --- | --- |
| 生产域名公开 head smoke 未发现失败 | 暂不处理 | 保持观察 | `P3-6` 观察线 |
| 真实平台分享预览暂无样本 | 暂不处理 | release / 运营前按平台补测 | release 前公开增长 smoke |
| 搜索抓取反馈暂无样本 | 暂不处理 | 等 Search Console、爬虫日志或访问日志有样本后补记 | 运营观察记录 |

等级判断：

- 本轮未发现阻断公开访问、核心 head / sitemap、分享入口或回流的 `P0/P1`。
- 本轮也未发现需要成组排入下一小批次的 `P2`。
- 未执行项属于真实平台与搜索抓取反馈范围，不用当前批次扩成平台化工程。

## 结论

- 本轮是否发现新的 `P0/P1`：否。
- 是否需要切出修复小闭环：否。
- 是否保持观察：是，继续等待真实平台分享预览、搜索抓取反馈和运行日志样本。
- 明确不做：
  - 不启动 SSR / SSG 或正文预渲染。
  - 不启动完整 E2E 或运营平台。
  - 不启动全量 `PublicId` 迁移。
  - 不把生产域名部署作为日常继续开发的阻塞项。
