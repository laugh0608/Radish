# 统一日志 L2：公开 head 与 sitemap

日期：2026-09-28（Asia/Shanghai）。承接支付口令批次，稳定契约见[事件契约第 26 节](../features/unified-logging-contract.md)。

## 实现范围

- PublicHeadSnapshotService 缓存读取 / 解析与写入使用固定 Warning 事件；PublicSitemapService 缓存、生成及栏目统计消费异常分别使用稳定事件。仅输出固定 failureKind，删除缓存键、异常原文等明细。
- Gateway 的 PublicHeadSnapshotClient 使用稳定事件区分快照 / HTML 响应不可用与请求失败；分别只带整数状态码或 failureKind。快照 404、成功和缓存命中保持安静。
- PublicHeadSnapshotMiddleware 在既有注入 / 响应写入 catch 中使用安全 Warning；日志作用域在调用后续处理前结束，避免把事件码带到后续管道。
- 两个 API Controller 不改动；head 查询异常仍上抛至 API 最终边界。业务可见性、缓存键 / TTL、分片、正常响应、上次成功 / 空 XML 回退及 Gateway 转发、注入与异常捕获范围不变。

## 验证范围

新增 PublicMetadataLoggingTests **40 项**，覆盖旧 / 候选输出与 Development / Production 四种组合：

- 缓存命中、未知路由与缺失帖子保持安静；返回快照 / XML 内容不被日志脱敏影响。
- head 缓存读失败、损坏 JSON 和缓存写失败继续生成快照，20 分钟 TTL 不变；查询异常经真实 Controller 和内存 API 错误管道仅记录一次 http.failed。
- sitemap 缓存故障继续返回 XML，30 分钟 TTL 与 MIME 不变；生成异常分别回退到相同键的上次成功 XML、首次失败的空 urlset，失败不写缓存。
- 索引单项统计失败只省略该栏目，保留其他栏目与分片；消费点一个 Warning，不重复升级为生成 Error。
- Gateway 快照 404 安静，403 / 503 与 HTTP 请求异常 / 取消保持原 null / 后续管道回退，事件不携带 URL 或异常原文。
- 真实 HttpClient 配合内存 handler 验证成功 HTML 注入与缓存复用；损坏 JSON 仍原样上抛，不新增吞异常行为。
- 受控响应流写入失败验证一个安全 Warning 和后续管道调用。

结果：

- .NET 定向回归 **93 项通过，0 失败，0 跳过**，包含本批 40 项、既有 Service / Controller、Gateway 注入与 RuntimeLog 用例。
- Node 日志契约 **27 项通过**；API 与 Gateway 构建均 **0 警告、0 错误**。
- 文档、改动文件文本卫生与 `git diff --check` 通过；记录索引 306 行的建议上限提醒为既有非阻断项。
- VSTest 沙盒因本地通信端口 SocketException(13) 中止，最小范围提权执行；测试项目保留既有 ProducerLoggingTests.cs:261 的 xUnit1051 提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~PublicMetadataLoggingTests|FullyQualifiedName~PublicHead|FullyQualifiedName~PublicSitemap|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
dotnet build Radish.Gateway/Radish.Gateway.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

测试使用 mock 缓存 / 仓储、真实 Service / Controller、内存 HTTP 管道和 HttpClient handler，不替代真实缓存、数据库、网络故障或浏览器验收。每个 sitemap 回退测试使用独立基址，避免进程静态成功缓存互相污染。

Gateway 的 JSON 解析、缓存命中后的响应写入等原本位于局部 catch 之外的异常仍按原边界传播；本批不宣称所有框架最终处理来源已完成。跨宿主 API Error 与 Gateway 响应不可用 Warning 各自描述所在边界，不承诺分布式全链唯一事件。

未安装依赖、启动项目服务 / 数据库 / 容器、访问生产、发布或部署。生产 RadishLogging.Enabled 保持 false，L2 尚未关闭。下一批聚焦 PublicDiscoverService 的逐请求摘要、异常包装与直接消费者，后续再按剩余来源清单推进。
