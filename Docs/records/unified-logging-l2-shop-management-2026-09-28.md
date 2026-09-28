# 统一日志 L2：商品管理 / 浏览与订单查询 / 备注

日期：2026-09-28（Asia/Shanghai）。本批承接[经验查询与治理批次](./unified-logging-l2-experience-governance-2026-09-28.md)，按[当前计划](../planning/current.md)将商城剩余入口分组推进；稳定契约见[事件契约第 19 节](../features/unified-logging-contract.md)。

## 实现范围

- ProductService 的分类、公开与管理商品查询，以及创建、更新、上下架和删除，移除 11 处仅记录再重抛的 catch 与逐项成功明细。
- OrderService 的用户订单、详情、按订单号查询、购买计数、管理查询和备注，移除 7 处仅记录再重抛的 catch；正常备注保存不再复制订单和操作员身份。
- 五个商品管理入口与订单备注的 Controller 原本消费 InvalidOperationException 并返回错误响应。本批补齐安全 Warning，只含固定事件和 failureKind；创建 / 更新保留 400，删除 / 上下架 / 备注保留 409。
- 上述 Controller 消费的 4xx BusinessException 安静，5xx BusinessException 记录一次安全 `http.failed` Error。其他异常继续上抛，由既有 API 最终边界记录一次。
- 商品公开可用性、资源校验、筛选排序、租户 / 版本条件、软删除、订单禁止删除保护与订单备注审计均保留，未改变响应状态、业务错误码、消息或返回结果。

## 验证范围

新增 `ShopManagementLoggingTests` 共 40 项，覆盖旧 / 候选日志与 Development / Production 四种组合：

- 分类与商品 / 订单查询结果、空值、用户隔离、软删除可见性、分类名称及购买计数。
- 12 条查询路径原异常对象传播，真实 Service / Controller 与内存 API 错误管道中最终单次 `http.failed`。
- 商品创建保持默认下架及创建审计；更新 / 上下架保持版本条件和写入值；删除保持软删除、下架与操作员审计。
- 版本冲突和已有订单禁止删除保持安静；订单备注去除首尾空白、修改人审计、false 返回及缺失订单响应保持原样。
- 六个管理入口分别覆盖 InvalidOperationException、4xx / 5xx BusinessException 和未处理 IO 异常，验证响应与安全事件数量。

结果：

- .NET 定向回归 **169 项通过，0 失败，0 跳过**，包含本批 40 项以及 ProductService、OrderService、ShopController、ShopFulfillmentLogging、CoinMovementLogging 与 RuntimeLog 的既有用例。
- Node 日志契约 **27 项通过**；API 构建 **0 警告、0 错误**。
- 文档、改动文件卫生与 `git diff --check` 通过；记录索引 306 行保留超过建议上限 300 行的非阻断提醒。
- 首轮新测试将更新商品的既有 InvalidOperationException 响应误期望为 409；校准为实际 400 后回归通过，未修改业务响应。测试项目编译保留既有 `ProducerLoggingTests.cs:261` 的 xUnit1051 提示。
- 关联回归在沙盒中因 VSTest 本地通信端口 SocketException(13) 中止，最小范围提权重跑后取得上述通过结果。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~ShopManagementLoggingTests|FullyQualifiedName~ProductServiceTest|FullyQualifiedName~OrderServiceTest|FullyQualifiedName~ShopControllerTest|FullyQualifiedName~ShopFulfillmentLoggingTests|FullyQualifiedName~CoinMovementLoggingTests|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

业务仓储、映射器与外部依赖使用 mock；HTTP 管道仅在内存中调用，不替代真实数据库事务 / 并发、浏览器 Smoke 或生产验收。本批未安装依赖、启动真实宿主 / 数据库服务 / 容器、写入真实业务数据、发布或部署。生产候选开关保持关闭。

L2 尚未整体完成，商城也未全链路收口。下一批继续系统赠送、权益查询 / 激活 / 停用 / 撤销与背包查询 / 使用 / 加减及直接消费者。
