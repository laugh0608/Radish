# 统一日志 L2：统计报表

日期：2026-09-28（Asia/Shanghai）。承接公开排行榜批次，稳定契约见[事件契约第 29 节](../features/unified-logging-contract.md)。

## 实现范围

- StatisticsService 四类报表移除仅记录再重抛的 catch；不再复制查询参数与异常原文，异常仍原样上抛。
- 核对唯一直接生产消费者 StatisticsController 与统计仓储；Controller 保留含 InnerException 的原 500 包装，最终由 API 记录一次安全 http.failed，无新增事件码。
- 保留 DashboardView 权限、统计和收入聚合口径、参数范围规范化、本地日期边界、逐日查询顺序、等级 0 补足与名称回退；没有改变 DTO、查询或响应。

## 验证范围

新增 StatisticsLoggingTests **20 项**，覆盖旧 / 候选输出与 Development / Production 四种组合：

- 仪表盘聚合数值、金额精度与用户软删除筛选保持不变；销量条数仍使用原默认值 / 上限，商品名称和销量 / 收入映射不变。
- 等级分布保留等级 0 的无经验用户补足、排序、配置名称及缺失名称的 Lv 回退。
- 趋势参数规范化为默认 30 天 / 最大 90 天；日期保持本地零点、连续日历日与排他结束时间，每天仍按原范围查询数量和收入。
- 用户数、订单数、商品数、收入、销量、等级分布、经验用户数、等级配置各依赖失败：Service 传播同一异常，Controller 保留 InnerException 和固定 500 契约，最终只记录一条安全 Error。
- 趋势第二天的订单 / 收入查询失败后不继续查询第三天，不返回前一天的部分成功结果。
- 四个 Controller 入口遇到底层 400 / 409 / 503 BusinessException 仍统一包装为原 500，不借日志重构修改错误映射。

结果：

- .NET 定向回归 **56 项通过，0 失败，0 跳过**，包含新增 20 项、既有 StatisticsService 与 RuntimeLog 用例。
- Node 日志契约 **27 项通过**；API 构建 **0 警告、0 错误**。
- 文档、改动文件文本卫生与 `git diff --check` 通过；记录索引 306 行的既有篇幅提醒为非阻断项。
- VSTest 沙盒因本地通信端口 SocketException(13) 中止，最小范围提权后通过；测试项目保留既有 ProducerLoggingTests.cs:261 的 xUnit1051 提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~Statistics|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

测试使用 mock 仓储、真实 Service / Controller 与内存 HTTP 错误管道，不验证真实数据库聚合、权限中间件执行或浏览器页面；本批没有修改这些实现。现有 Controller 将底层业务异常统一包装为 500 的行为保留，不宣称该错误契约已重新设计。

未安装依赖、启动项目宿主 / 数据库 / 容器、访问生产、发布或部署。生产 RadishLogging.Enabled 继续 false，L2 尚未关闭。下一批聚焦 UserFollowService 关注通知入队及直接调用链，先核对事务和可靠 Outbox 异常所有权，保留业务行为。
