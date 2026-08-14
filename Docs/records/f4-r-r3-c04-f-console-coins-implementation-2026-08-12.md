# F4-R R3-C04-F Console Coins 权威调账与流水治理实现

> 日期：2026-08-12（Asia/Shanghai）
>
> 状态：代码实现与静态门禁完成；按当轮约束未启动服务、浏览器或运行态验收
>
> 范围：Console 权威目标查询、管理员调账、幂等重放 / 冲突、余额版本 CAS、资产流水与 PC / Mobile 单任务边界

## 本批结论

R3-C04-F 已关闭“表单目标与当前展示用户脱离”的资产写入风险。Console 调账只能绑定刚取得的权威用户、余额和版本，提交前显式复核目标、方向、金额与原因；服务端以独立管理员调账操作域处理同键重放、异载荷冲突、处理中状态和缺失回放结果，并用余额版本 CAS 阻断陈旧确认。流水查询、URL 回访与 PC / Mobile 同快照同步收口。既有 `console.coins.view / adjust` 权限与资产模型保持不变；未新增数据库、migration、权限键、资产类型或 Pencil 画板。

## 服务端契约

- `UserBalanceVo` 增加权威显示名和 `VoVersion`；余额查询仍拒绝不存在的用户，不以 Console 查询隐式制造账户。
- `AdminAdjustBalanceDto` 必须携带 `ExpectedVersion + IdempotencyKey`。公开 `AdminAdjustBalanceAsync` 使用 `[UseTran(Required)]`，在同一 Main 事务路径内完成幂等 Begin、`CoinTransaction` pending 写入、余额版本 CAS、流水 success 与幂等结果完成。
- 幂等唯一范围为 `TenantId + OperatorId + CoinAdminAdjustment + IdempotencyKey`，摘要绑定目标用户、正负金额、规范化原因和预期余额版本。同键同摘要成功重放返回原流水号；不同摘要、处理中、无可用回放结果与无效 key 均返回稳定结构化错误。
- 幂等记录首次创建复用现有 UnitOfWork savepoint；PostgreSQL 同键并发触发 `23505` 后先回滚到保存点，再读取竞争者记录，避免外层资产事务进入 aborted 状态。
- 余额扣减先校验可用余额，增加先校验 `long` 上溢；CAS 更新数量、流水状态更新数量必须恰好为 `1`，否则失败并回滚 Main 权威写入，不自动更换 key 或重试陈旧版本。
- `BalanceChangeLog` 继续沿用既有 Log 仓储边界，并写入稳定 `SourceEventKey`；其写入失败会阻断成功返回，但本批不把现有跨库边界夸大为分布式 exactly-once。
- 管理流水分页入口拒绝非法页码与 `pageSize > 100`，服务查询固定 `CreateTime desc + Id desc`，并回填交易双方权威显示名。

## Console 页面事实

- 用户、交易类型、状态、业务类型、业务 ID、页码和页大小进入 URL 权威状态；刷新、历史前进后退和订单排障回跳不丢失 LongId 或已应用筛选。
- 余额与流水使用独立请求代际；同查询失败保留旧快照并标记 `stale`，新查询失败为 `unavailable`。只有余额状态为 `ready` 且页面未 busy 时才允许进入调账。
- 调账表单不再包含第二份用户 ID。目标变化会清空金额、原因、既有结果和幂等 key；未保存草稿离开页面或切换目标前必须确认。
- 一份金额 / 原因草稿持有一枚稳定 `coin-admin-adjust:{uuid}`；失败重试保留原 key，载荷变化或成功后才生成新 key。版本冲突、Processing 或回放结果不确定时冻结当前余额写入，要求先刷新权威余额与流水。
- 提交前 Modal 明确展示权威用户名 / ID、余额版本、增减方向、金额与原因；成功后展示流水号，并刷新余额和流水。
- PC 使用连续流水表格；Mobile 使用同一快照的流水卡、轻量分页和筛选 Bottom Sheet，调账保持目标确认后的单任务顺序。

## 静态验证

| 门禁 | 结果 |
| --- | --- |
| Coin / OperationIdempotency 定向测试 | `43 / 43` 通过，覆盖权限接口、稳定分页、版本冲突、幂等重放 / 冲突、savepoint 并发收敛、CAS 与流水 / 幂等完成 |
| `dotnet test Radish.Api.Tests` | `1253 passed / 39 skipped`，总计 `1292` |
| `dotnet build Radish.slnx -c Debug --no-restore` | 通过，`0 warning / 0 error` |
| `npm run test --workspace=radish.console` | `106 / 106` 通过，含 URL、权威目标、稳定 key、冻结写入与 PC / Mobile 同快照合同 |
| Console type-check、strict type-check、Lint、production build | 全部通过 |
| 权限与 LongId | `check:console-permissions` 与 `check:long-id-safety` 通过；权限扫描只保留既有 `console.hangfire.replay` 未引用警告 |

`check:time-semantics` 仍只被本批外既有 `Radish.Repository/SystemConfigStorageCoordinator.cs` 三处 `DateTime.Now` 漂移拦截；本批新增资产时间使用 `DateTime.UtcNow`，没有扩大该维护债务。

## 停止线与未执行项

- 未新增转账、退款、提现、批量调账、冻结治理、万能资产编辑器、数据库、migration、权限键或 Pencil。
- 未启动 API、Gateway、Auth、Console dev server 或浏览器；PC / Mobile 真实页面、最小权限账号、同键真实重放和真实并发版本冲突留到后续成组运行态验收。
- R3-C04 六批普通资源代码与静态门禁至此全部关闭。下一顺位先做 `R3-C05 Console 仪表与治理派生` 设计前审计，反查 Dashboard、Documents、Experience 与 Channel Discoverability 的主任务、证据 / 事件和写权限边界，再裁决子批顺序，不直接复制页面或启动 Pencil。
