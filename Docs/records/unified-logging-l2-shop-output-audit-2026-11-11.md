# 商城、库存与支付口令最终日志输出核验与实施

日期：2026-11-11（Asia/Shanghai）。基线：`dev` 的 `4200e6d9`（奖励、币与经验最终输出修复）。初次核验只确认缺口并提出有限范围，未修改产品代码或正式测试；随后所有者批准并完成下文[本地实施](#implementation)。范围见[衔接计划第 10.9 节](../features/unified-logging-l3-ingest-plan.md#l2-shop-output)。

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

需同时固定前三个来源的生成身份、明确支付口令的事件身份，并在最终输出逐事件约束字段与诊断属性。有限方案只包含四个来源、七个事件码和既有两条输出链，不扩入无日志的权益服务、已完成 Controller、业务补偿改造或口令算法。核验时第 10.9 节待确认，该轮探针不关闭最终输出门禁。随后所有者明确批准实施，完成情况见下节；L2 整体与真实链路门禁继续保留。


<a id="implementation"></a>

## 已批准范围的本地实施

实施基线为 `cb7c03c0`。2026-11-11 所有者确认按第 10.9 节完成修复、回归与文档同步；本批只改变日志身份与输出适配，保留前述业务和输出故障边界。

- 三个 Serilog 来源绑定自身类型；支付口令两个生成点改用命名 EventId。`ShopLogSink` 已接入旧链代码 / 配置 WriteTo、AuditTo 与候选 JSONL，在上下文合并后按精确来源、身份、等级和购买阶段重建。登记事件固定 application / 非诊断属性，保留允许字段、原等级 / Serilog 时间和类型化关联。
- 配置拒绝、库存重试和清理数量不继承外层 failureKind；未知或错配身份输出安全 unknown，数值 / 分类非法则丢弃。支付口令 EventId 的数值与名称必须同时匹配，普通 EventCode 不能补齐或覆盖身份。字段契约见[第 84 节](../features/unified-logging-contract.md#l2-shop-final-output)。
- 正式回归复用商城履约、权益与支付口令 fixture，分别用 Serilog / MEL scope 注入合成来源、身份、诊断、关联和载荷污染。覆盖真实库存重试恢复 / 耗尽、正常购买、支付 / 履约失败、独立库存补偿和后续订单写入失败、改名包装与直接 Controller 消费、口令清理数量与建议回退。
- 输出故障测试确认 AuditTo 仍可中断重试、回补、包装及回退，而 WriteTo / 候选仍消费输出故障；同时检查仓储调用、原 InnerException、400 包装和已发生操作。结构化断言逐事件检查字段集合，不只比较渲染文本。代码 console、错误身份 / 等级 / 阶段、非法数值 / 分类、原时间与类型化 trace / span、邻接来源另有矩阵回归。
- 支付口令原测试的旧链只接直接 formatter sink，不能执行新增 EventId 映射；本批将该 capture 改接正式日志配置，并按结构化 processedCount 检查数量。其他原业务断言保持，不通过添加生成端 EventCode scope 来兼容绕过正式接线的测试。

### 实施验证

- 严格构建 `dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --warnaserror` 通过，0 warning、0 error。初次新增测试在 params 数组参数处使用目标类型 new 导致编译失败，改为显式 Expected 类型后重新构建通过。
- 新增最终输出范围在沙盒 93 项全部通过，0 失败、0 跳过：24 项事件 / 非法身份矩阵、1 项字段类型与时间 / 关联、2 项代码 console、48 项真实方法 × 出口 / scope 组合、18 项输出故障。覆盖四个精确来源、七个事件码及两个购买阶段。
- 关联回归先在沙盒执行，71 项通过后在既有 `PaymentPasswordLoggingTests.StorageFailures_ShouldReachApiOnce` 中无进展；30 秒诊断终止并保留 sequence，未采集内存转储。该次中止不计通过，不据此认定产品缺陷。
- 相同已编译程序集在宿主环境约 36 秒完成 437 项关联回归，0 失败、0 跳过；包含上述新增 93 项、商城 / 口令日志、五个 Service、Shop / PaymentPassword Controller、Controller 最终输出及直接消费者、Outbox 与奖励输出回归。沙盒等待的底层原因未进一步确定。
- `npm run check:repo-quality:changed`、`git diff --check` 通过。事件契约及记录索引有非阻断篇幅建议提醒，未达到硬上限；本批未扩展为其他专题的文档拆分。

本地切片完成不等于真实账本 / 库存事务、数据库并发、生产 File / DB sink、collector 或 L2 整体验收。没有安装依赖、启动项目服务 / 容器或执行浏览器 Smoke；生产开关保持关闭。

复跑入口（关联范围在宿主使用相同已编译程序集）：

```bash
dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --warnaserror
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~ShopOutputLoggingTests|(FullyQualifiedName~FinalOutput_Should&(FullyQualifiedName~ShopFulfillmentLoggingTests|FullyQualifiedName~ShopEntitlementLoggingTests|FullyQualifiedName~PaymentPasswordLoggingTests))'
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~ShopFulfillmentLoggingTests|FullyQualifiedName~ShopManagementLoggingTests|FullyQualifiedName~ShopEntitlementLoggingTests|FullyQualifiedName~PaymentPasswordLoggingTests|FullyQualifiedName~ShopOutputLoggingTests|FullyQualifiedName~Services.OrderServiceTest|FullyQualifiedName~Services.ProductServiceTest|FullyQualifiedName~Services.UserBenefitServiceTest|FullyQualifiedName~Services.UserInventoryServiceTest|FullyQualifiedName~Services.PaymentPasswordServiceTest|FullyQualifiedName~OutboxNativeLoggingTests|FullyQualifiedName~ControllerFinalOutputTests|FullyQualifiedName~ControllerConsumerOutputTests|FullyQualifiedName~ApplicationOperationOutputTests|FullyQualifiedName~Controllers.ShopControllerTest|FullyQualifiedName~Controllers.PaymentPasswordControllerTest|FullyQualifiedName~RewardOutputLoggingTests' --logger 'console;verbosity=normal' --logger 'trx;LogFileName=shop-related-host.trx' --results-directory /tmp/radish-shop-implementation-results
npm run check:repo-quality:changed
git diff --check
```
