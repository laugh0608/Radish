# 统一日志 L2：公开排行榜与用户排名

日期：2026-09-28（Asia/Shanghai）。承接公开发现流批次，稳定契约见[事件契约第 28 节](../features/unified-logging-contract.md)。

## 实现范围

- LeaderboardService 两个查询入口移除仅记录再重抛的 catch，不再把用户身份、榜单类型、分页参数与异常原文写入运行日志。
- 核对唯一直接生产消费者 LeaderboardController、排行榜仓储及头像装配依赖；无需新增 catch 或事件码。上抛异常继续由 API 最终边界处理一次，4xx 安静，5xx 输出安全 http.failed。
- 保留类型白名单、分页规范化、排名与资格复核、当前用户标记、头像 / 等级 / 商品信息装配以及原错误契约。DateTime.Now 参数未改动，不借本批调整时间语义。

## 验证范围

新增 LeaderboardLoggingTests **24 项**，覆盖旧 / 候选输出与 Development / Production 四种组合：

- 四类用户榜保留页码、总数、页大小、排名序号、资格复核后的名次空洞、头像、等级和当前用户标记；无效分页仍规范化到原范围。
- 热门商品保留名称、图标、销量、价格与分页排名；四类个人排名成功及无排名的 0 保持原语义，经验查询继续传入 Local 时间。
- 敏感 / 未知类型、商品个人排名与未登录拒绝保持原代码 / 结果，不查询仓储，不产生运行日志。
- 五类榜单和四类排名查询故障经真实 Controller 与内存 HTTP 管道只输出一次安全 http.failed；Service 单独调用继续传播同一异常且安静。
- 用户资格、头像、经验、等级与商品图标依赖故障仍返回 500，不成为部分成功或重复日志。
- 400 / 409 / 503 BusinessException 保留原错误码，只有最终 5xx 输出 Error。

结果：

- .NET 定向回归 **84 项通过，0 失败，0 跳过**，包含新增 24 项、既有排行榜 Service / Controller / 临时 SQLite 仓储与 RuntimeLog 用例。
- 过滤器明确排除 Database=PostgreSQL 用例；上述数量不含 PostgreSQL 验收。
- Node 日志契约 **27 项通过**；API 构建 **0 警告、0 错误**。
- 文档、改动文件文本卫生与 `git diff --check` 通过；记录索引 306 行的既有篇幅提醒为非阻断项。
- VSTest 沙盒因本地通信端口 SocketException(13) 中止，最小范围提权后通过；测试项目保留既有 ProducerLoggingTests.cs:261 的 xUnit1051 提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter '(FullyQualifiedName~Leaderboard|FullyQualifiedName~RuntimeLog)&Database!=PostgreSQL' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

新增用例使用 mock 仓储与附件依赖、真实 Service / Controller 和内存 HTTP 错误管道；既有仓储回归使用自身临时 SQLite。它们不替代 PostgreSQL、真实并发、权限中间件或浏览器验收，不表示所有底层生成端均完成。

未安装依赖、启动项目宿主 / 数据库服务 / 容器、访问生产、发布或部署。生产 RadishLogging.Enabled 继续 false，L2 尚未关闭。剩余清单确认 StatisticsService 的四类报表仍记录后重抛，Controller 再包装为 500；下一批聚焦该组，不改变统计口径或日期边界。
