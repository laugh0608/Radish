# 商城、库存与支付口令最终日志输出核验

日期：2026-11-11（Asia/Shanghai）。基线：`dev` 的 `4200e6d9`（奖励、币与经验最终输出修复）。本轮只核验缺口并提出有限修复范围，未修改产品代码或正式测试。待确认方案见[衔接计划第 10.9 节](../features/unified-logging-l3-ingest-plan.md#l2-shop-output)。

## 实际调用与有限范围

| 生成来源 | 实际入口与消费边界 |
| --- | --- |
| `Radish.Service.ProductService` | `CheckCanBuyAsync` 对不完整商品配置返回固定拒绝结果；`DeductStockAsync` 经 `ExecuteWithRetryAsync` 在即将重试时记录 attempt / delayMs。原最多五次尝试及 50 / 100 / 200 / 400ms 退避、按异常消息识别冲突、耗尽上抛保留 |
| `Radish.Service.OrderService` | `PurchaseAsync` 支付或履约 catch 记录 `order.purchase_failed`；外层包装为默认 400 BusinessException 前记录 `order.purchase_interrupted`。补偿或后续写入的独立失败可以同时有分支和外层事件，不按同一订单全局去重 |
| `Radish.Service.UserInventoryService` | `UseItemAsync / UseRenameCardAsync` 进入改名效果；将 InvalidOperationException 包装为 400 前记录 `inventory.rename_rejected`，ArgumentException 包装仍安静。未包装的 IO 等异常继续由 ShopController 使用 / 改名接口消费 |
| `Radish.Service.PaymentPasswordService` | `ClearExpiredLocksAsync` 在仓储返回正数量后记录 `payment.locks_cleared`，只由现有管理接口调用，不是新增后台任务。`GenerateSecuritySuggestionsAsync` 消费异常并记录 `payment.suggestions_failed`，由建议接口及状态查询调用 |

四个产生日志的来源合计七个事件码，其中 `order.purchase_failed` 有 payment / fulfillment 两个合法阶段。前三个使用全局 Serilog 且未绑定 SourceContext；支付口令使用 `ILogger<PaymentPasswordService>`，已有固定类型来源，但两个事件码和 business 分类来自局部 scope，没有命名 EventId。

`UserBenefitService` 没有保留运行日志，正常权益发放 / 重放与错误上抛沿原契约。商品管理、订单查询 / 备注、权益操作、背包加减与口令正常操作沿已有安静性和最终消费回归，不添加日志。ShopController、PaymentPasswordController 及 API 异常消费者的已完成输出适配不重做；其中改名 IO 异常的 Controller 输出同时在本探针中复核。

## 最终出口证据

临时探针通过真实 Service / Controller 方法和既有依赖 fixture，使用正式日志配置接到 Development / Production 的旧链配置 WriteTo / AuditTo 和候选 JSONL；两环境另核验代码 console。四种上下文为 clean、payload、source 和 diagnostic。使用合成标记，不访问业务或生产数据。

| 条件 | 已复现结果 |
| --- | --- |
| 正常上下文 | 七个事件码及本次阶段 / 数值 / failureKind 正常。商城与库存的旧链事件没有 SourceContext；支付口令保留类型来源，旧链分类为 business，候选公共策略将这个未登记分类规范化为 application |
| 字段污染 | 四个来源的旧链都保留外层 privatePayload；候选剔除私有字段，但接受外层合法格式的 operationId、字符串 traceId / spanId 及无关 statusCode。没有消费异常的商品配置拒绝、库存重试、过期锁定清理会继承外层 failureKind=database；已绑定的 io / invalid-operation 仍优先 |
| 来源冲突 | 外层 SourceContext=Radish.Service.RewardRuntimeLog 使商城与库存五个事件码（六种业务分支）被奖励适配器重建为 runtime.unclassified，原字段消失。代码 console 同样显示奖励 unknown 模板。支付口令的类型化来源保持，两个事件未被误归 |
| 仅 Diagnostic=true | Production 候选链丢掉七类事件，包括真实已消费失败；其他路线仍输出但带诊断标记。业务返回与调用次数保持，所以仅检查业务结果或无污染的生成端测试不能发现该缺口 |
| 已完成的直接消费者 | 改名 ArgumentException 仍返回 400 且安静；IO 异常由 ShopController 记录一次 inventory.rename_failed、failureKind=io，在同样 scope 污染下仍为普通事件。这个 Controller 切片不需重复修复 |

结果断言同时核对：配置拒绝的原文字；库存冲突后第二次尝试成功；支付失败回补库存、订单失败阶段及权益不发放；履约失败保留已扣币证据、失败返回、订单更新和已售计数调用；早期失败包装 400 并保留 InnerException；改名已调用条件扣减后传播失败、操作流水和幂等完成尚未写入；过期锁定数量响应；建议降级文字和状态查询的成功返回。

以上写入通过 mock 调用或内存对象验证，不证明真实库存、资金账本、事务回滚、数据库并发或生产审计。污染条件为主动注入的合成输入，不表示生产中已发生同类事件。

## 输出故障与不改变的既有语义

WriteTo 消费 sink 故障；候选 writer 失败只产生有限的 `pipeline.output_failed`。AuditTo 保留原 sink 故障的 AggregateException 传播，实际观察到：

- 库存冲突第一条重试日志写失败时，只有一次库存更新调用，不再进入后续尝试；WriteTo / 候选链仍沿五次尝试后传播原冲突。
- 购买支付异常的日志写失败会先进入外层 catch，再因中断日志也失败而传播输出异常；此前扣库存和创建订单调用已发生，库存回补与订单失败更新尚未执行。无输出故障或 WriteTo / 候选消费故障时，仍完成原回补并返回失败结果。
- 改名拒绝的日志写失败时，原 400 BusinessException 包装尚未执行；无输出故障或消费输出故障时仍按原包装传播。
- 过期锁定清理已调用仓储并得到 3，但 AuditTo 故障使方法不再返回该数量；安全建议的日志故障使原回退文字不再返回。

这些是当前输出故障行为，不在下一批日志隔离修复中静默改变，也不把直接调用 mock 的先后顺序表述为真实事务最终状态。

## 验证与证据边界

- 临时探针位于 `/tmp/radish-shop-output-audit/ShopOutputProbe.cs`，通过同目录 `Probe.targets` 的 `CustomAfterMicrosoftCommonTargets` 导入。fixture 从现有商城履约、权益、支付口令测试复制，仅调整临时类型名称和可调用的服务引用；不反射调用私有业务方法。
- 严格编译并执行探针：82 项通过、0 失败、0 跳过。包括三组业务链 × 六种出口环境路线 × 四种上下文条件的 72 项、6 项输出故障、4 项两环境代码 console 正常 / 来源冲突。通过代表缺口与原行为可复现，不代表修复完成。
- 移除临时导入参数后，正式测试项目以 `--no-restore --warnaserror` 重新构建通过，0 warning、0 error。既有商城履约 / 管理 / 权益与口令日志，以及五个相关 Service 的回归在宿主环境 212 项全部通过，0 失败、0 跳过。
- 既有回归先在沙盒执行，60 项通过后在 `PaymentPasswordLoggingTests.StorageFailures_ShouldReachApiOnce` 内无进展；30 秒超时诊断终止该运行并保留 sequence，未采集内存转储。该次中止不计通过。随后相同已编译程序集在宿主环境约 17 秒完成全部 212 项；底层等待原因未进一步确定。
- `npm run check:repo-quality:changed` 与 `git diff --check` 通过；记录索引有非阻断篇幅建议提醒。
- 未安装依赖、启动项目服务 / 容器或执行浏览器 Smoke。真实数据库事务 / 并发、生产介质、调度和 collector 验收继续沿原门禁。

复跑入口（最后一条测试命令在宿主执行；正式项目不再导入临时探针）：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --warnaserror -p:CustomAfterMicrosoftCommonTargets=/tmp/radish-shop-output-audit/Probe.targets --filter 'FullyQualifiedName~ShopOutputProbe' --logger 'trx;LogFileName=shop-output-audit.trx' --results-directory /tmp/radish-shop-output-audit/results
dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --warnaserror
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~ShopFulfillmentLoggingTests|FullyQualifiedName~ShopManagementLoggingTests|FullyQualifiedName~ShopEntitlementLoggingTests|FullyQualifiedName~PaymentPasswordLoggingTests|FullyQualifiedName~Services.OrderServiceTest|FullyQualifiedName~Services.ProductServiceTest|FullyQualifiedName~Services.UserBenefitServiceTest|FullyQualifiedName~Services.UserInventoryServiceTest|FullyQualifiedName~Services.PaymentPasswordServiceTest' --logger 'console;verbosity=normal' --logger 'trx;LogFileName=shop-existing-host.trx' --results-directory /tmp/radish-shop-output-audit/results
```

## 结论与停止线

需同时固定前三个来源的生成身份、明确支付口令的事件身份，并在最终输出逐事件约束字段与诊断属性。有限方案只包含四个来源、七个事件码和既有两条输出链，不扩入无日志的权益服务、已完成 Controller、业务补偿改造或口令算法。第 10.9 节待所有者确认后实施；本次核验不关闭本组最终输出门禁或 L2 整体。
