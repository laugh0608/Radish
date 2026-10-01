# 统一日志 L2：公开发现流

日期：2026-09-28（Asia/Shanghai）。承接公开 head 与 sitemap 批次，稳定契约见[事件契约第 27 节](../features/unified-logging-contract.md)。

## 实现范围

- PublicDiscoverService 删除逐请求成功摘要及仅供日志使用的 Stopwatch，删除来源异常包装前的原文 Error。
- 保留非 BusinessException 包装为 503 的既有代码、错误码、消息与 MessageKey；既有 BusinessException 原样上抛。包装不新增 InnerException，不伪称最终安全日志保留了底层故障类型。
- 核对唯一直接生产调用方 PublicDiscoverController 和 Main / Chat 仓储；Controller 继续无 catch，由 API 最终边界记录 5xx 的 http.failed，4xx 安静。无新增事件码。
- 保留七个来源并发查询、整页失败、来源窗口、排序、游标、分页、纯文本映射、Pulse 与 no-store；同步发现流专题的日志说明。

## 验证范围

新增 PublicDiscoverLoggingTests **24 项**，覆盖旧 / 候选输出与 Development / Production 四种组合：

- 时间相同的频道 / 成员候选稳定排序，成员同类按 SourceId 倒序；续页保留截止时间、最后排序键、窗口和 pageSize + 1 上界，Pulse 与 no-store 保持原样。
- 非法页大小、损坏 / 超长游标保持 400 和原错误码，不调用仓储，不输出日志。
- 逐一注入七个来源的异常，每次仍启动全部任务，整页返回 503 和原错误契约，最终只有一条安全 http.failed。
- 400 / 409 / 503 BusinessException 原样传播；HTTP 最终边界仅记录 5xx。
- 受控异步任务验证所有来源先启动、等待完成再包装异常；Service 单独上抛时无重复日志，包装保持无 InnerException。
- 无效 ID 与晚于截止时间的候选被过滤，空页保持成功、无后续游标且安静。

结果：

- .NET 定向回归 **63 项通过，0 失败，0 跳过**，含新增 24 项、既有 Service、临时 SQLite 仓储与 RuntimeLog 用例。
- Node 日志契约 **27 项通过**；API 构建 **0 警告、0 错误**。
- 文档、改动文件文本卫生与 `git diff --check` 通过；记录索引 306 行的既有篇幅建议为非阻断提醒。
- VSTest 沙盒本地通信端口受限，最小范围提权后执行；测试项目保留既有 ProducerLoggingTests.cs:261 的 xUnit1051 提示。
- 首轮 4 个新用例的标题预期忽略既有 Markdown 下划线清理，修正测试预期后重跑；未为通过测试更改纯文本映射。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~PublicDiscover|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

新增日志测试使用 mock 仓储、真实 Service / Controller 与内存 HTTP 错误管道。既有仓储测试使用自身临时 SQLite 数据库验证资格和 keyset，不能替代 PostgreSQL、生产数据或真实宿主 / 浏览器验收；本批不修改查询实现。

没有安装依赖、启动项目服务 / 数据库服务 / 容器、发布或部署。生产 RadishLogging.Enabled 继续 false，L2 尚未关闭。下一批推进 LeaderboardService 的公开榜单与用户排名查询及直接消费者，不借日志治理调整排名口径或既有时间语义。
