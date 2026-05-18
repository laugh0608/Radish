# P3-6 公开增长真实部署观察记录模板

> 用途：记录真实部署或生产公开域名下的公开增长观察事实，并分流为修复、配置、后续专题或暂不处理。
>
> 复制本模板时建议命名为 `p3-6-public-growth-observation-record-YYYY-MM-DD.md`。

## 基本信息

- 日期：YYYY-MM-DD（Asia/Shanghai）
- 环境：local / testing / production
- 公开域名：
- 服务版本 / commit：
- 记录人：

## 观察范围

| 范围 | 入口 | 结论 | 备注 |
| --- | --- | --- | --- |
| 公开 head smoke | `npm run check:public-head-smoke -- --base-url <url> --path ...` | 未执行 / 通过 / 失败 | 覆盖 robots、sitemap、分片和 forum / docs / shop 详情 |
| 动态 sitemap | `/sitemap.xml`、`/sitemaps/*.xml` | 未执行 / 通过 / 失败 | 重点看 `<loc>` origin、分片可达性和 XML 类型 |
| head snapshot | forum / docs / shop 公开详情首包 HTML | 未执行 / 通过 / 失败 | 重点看 title、description、canonical、OG、Twitter、JSON-LD |
| 公开域名配置 | `GatewayService:PublicUrl` / `RADISH_PUBLIC_URL` | 未执行 / 通过 / 失败 | 重点看 canonical 与 sitemap origin 是否一致 |
| 分享预览 | 真实分享工具 / 平台预览 | 未执行 / 通过 / 失败 | 记录平台、URL 和异常截图或描述 |
| 搜索抓取反馈 | Search Console / 爬虫日志 / 访问日志 | 未执行 / 通过 / 失败 | 记录抓取状态、错误类型和样本 URL |

## 命令与结果

```bash
npm run check:public-head-smoke -- --base-url <url> --path /forum/post/<postKey> --path /docs/<slug> --path /shop/product/<productId>
```

- 结果：
- 失败诊断摘要：
- 未覆盖项：

## 分流结论

| 问题 | 等级 | 处理方式 | 负责人 / 后续入口 |
| --- | --- | --- | --- |
| 示例：`/sitemap.xml` 被 SPA shell 覆盖 | P1 | 当前批次修复 Gateway 路由优先级 |  |
| 示例：分享平台缓存旧标题 | P2 | 记录观察，等待缓存刷新或后续专题 |  |

等级口径：

- `P0/P1`：阻断公开访问、核心 head / sitemap、分享入口、资产安全、购买 / 订单 / 转账或权限授权。
- `P2`：影响增长质量但不阻断主路径，可成组排入后续小批次。
- `暂不处理`：缺少真实证据、影响很低或属于已明确后置的 SSR / SSG、正文预渲染、全量迁移、平台化工程。

## 结论

- 本轮是否发现新的 `P0/P1`：
- 是否需要切出修复小闭环：
- 是否保持观察：
- 明确不做：
  - 不启动 SSR / SSG 或正文预渲染。
  - 不启动完整 E2E 或运营平台。
  - 不启动全量 `PublicId` 迁移。
