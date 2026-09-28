# 统一日志 L2：系统赠送、权益操作与背包使用

日期：2026-09-28（Asia/Shanghai）。本批承接[商品管理与订单查询](./unified-logging-l2-shop-management-2026-09-28.md)，按[当前计划](../planning/current.md)推进商城权益与背包生成端；稳定契约见[事件契约第 20 节](../features/unified-logging-contract.md)。

## 实现范围

- UserBenefitService 的权益查询与系统赠送移除 5 处仅记录再重抛的 catch，正常赠送 / 激活 / 停用 / 撤销不再复制业务身份和操作明细。
- UserInventoryService 的查询与加减道具移除 5 处仅记录再重抛的 catch，正常道具使用及加减不再输出用户、背包项、数量与操作 ID。
- ShopController 消费激活 / 停用 / 撤销的 InvalidOperationException 时分别输出固定 Warning，保留原 400 / 400 / 409 响应；未处理异常继续由 API 最终边界记录。
- 道具使用和改名卡接口消费一般异常或 5xx BusinessException 时补齐一次安全 Error，仅输出固定事件与 failureKind；原失败结果及 400 响应不变，4xx BusinessException 和正常失败结果保持安静。
- 改名效果原有的 InvalidOperationException 包装点补一次安全 Warning；ArgumentException 包装保持安静，InnerException、消息及默认 400 语义保留。旧改名卡路由复用同一消费边界。
- 保持权益期限、激活选择、Changed 返回、撤销原因规范化、事务属性、背包合并及扣减、三类道具效果、权威操作流水和幂等完成顺序。直接依赖的改名服务与仓储没有需要移除的重复日志；币 / 经验发放的既有安全失败边界继续复用。

## 验证范围

新增 `ShopEntitlementLoggingTests` 共 40 项，覆盖旧 / 候选输出与 Development / Production 四种组合：

- 权益可用性与过期过滤、系统赠送有效期及创建审计、背包查询与用户隔离。
- 查询 / 赠送原异常对象传播，以及内存 API 错误管道中的单次 `http.failed`。
- 权益激活 / 停用 / 撤销的 Changed 返回、撤销原因处理及异常消费响应。
- 道具加减数量、返回值与异常传播；保留 ItemValue 首尾空格的既有语义。
- 币卡、经验卡和改名卡首次效果及权威流水写入；持久化成功重放不再次扣减、不重复新增流水或完成幂等记录。
- 使用接口的普通异常、5xx / 4xx BusinessException、改名包装异常及流水写入失败的日志数量与安全输出；私密哨兵值不进入日志，不出现 `runtime.unclassified`。

结果：

- .NET 定向回归 **207 项通过，0 失败，0 跳过**，包括本批 40 项及权益 / 背包 Service、Repository、ShopController、商城管理 / 履约、奖励日志和 RuntimeLog 的既有用例。
- Node 日志契约 **27 项通过**；API 构建 **0 警告、0 错误**。
- 文档、改动文件卫生与 `git diff --check` 通过；记录索引 306 行保留超过建议上限 300 行的非阻断提醒。
- 首轮新测试误认为 ItemValue 会去除首尾空格；校准断言后通过，未修改业务规则。测试项目编译保留既有 `ProducerLoggingTests.cs:261` 的 xUnit1051 提示。
- 本批测试与构建均在沙盒中完成。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~ShopEntitlementLoggingTests|FullyQualifiedName~UserBenefitServiceTest|FullyQualifiedName~UserInventoryServiceTest|FullyQualifiedName~UserBenefitRepositoryTest|FullyQualifiedName~UserInventoryRepositoryTest|FullyQualifiedName~ShopControllerTest|FullyQualifiedName~ShopManagementLoggingTests|FullyQualifiedName~ShopFulfillmentLoggingTests|FullyQualifiedName~RewardLoggingTests|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

新增日志测试使用真实 Service / Controller、mock 仓储与效果依赖、内存 HTTP 管道；关联回归包含 6 项既有 SQLite 仓储测试，但不替代完整道具事务、跨服务效果、PostgreSQL 并发或运行态验收。系统赠送及独立背包加减未新增生产调用入口，其 Service 级验证不代表真实 HTTP 调用链已验收。

本批未安装依赖、启动真实宿主 / 数据库服务 / 容器、写入真实业务数据、发布或部署。生产候选开关继续关闭，L2 尚未整体完成，也不宣称完整商城和用户资料入口收口。下一批按组推进附件上传 / 合并、令牌创建 / 验证 / 主动撤销及配额入口。
