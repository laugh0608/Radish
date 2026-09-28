# 统一日志 L2：支付口令管理与查询

日期：2026-09-28（Asia/Shanghai）。承接附件下载与删除批次，稳定契约见[事件契约第 25 节](../features/unified-logging-contract.md)。

## 实现范围

- PaymentPasswordService 的状态、设置、修改、管理员重置 / 解锁、统计与过期锁定清理移除重复重抛日志；正常操作不再复制用户、操作员、口令强度与原因到运行日志。
- 安全建议已消费异常使用 payment.suggestions_failed Error，仅输出固定 failureKind；保留建议回退及状态查询继续成功的语义。
- 手动清理实际清除记录时使用 payment.locks_cleared Info，仅包含 processedCount；零进展安静，没有新增调度或后台运行。
- Controller 设置 / 修改消费的 5xx BusinessException 由消费点记录 http.failed，4xx 安静；其他异常继续由 API 最终处理一次。
- 六位校验、哈希算法、版本升级、失败计数与锁定、仓储写入顺序、UTC 时间及原响应不变；管理员重置保留修改字段和 Remark。已核对安全记录查询和 AuditLogService，未修改审计中间件、审计存储或增加事务保证。

## 验证

新增 PaymentPasswordLoggingTests **32 项**，覆盖旧 / 候选与 Development / Production 四种组合：

- 设置 / 修改使用真实 Argon2id 校验新旧口令，写入时间、版本、清空 Salt 和失败计数调用保持不变，成功无明细日志。
- 重复数字、确认不一致、未设置、已设置和第五次错误分别保留原状态；第五次错误仍写入 30 分钟锁定，失败不写新哈希。
- 管理员重置清空凭据与锁定状态，保留操作员与原因记录；解锁 true / false 结果保持原样且安静。
- 未设置状态、安全建议、统计比例和清理数量不变；清理为零安静，为正仅一个安全摘要。
- 建议读取异常直接或经状态查询调用都只记录一次安全事件，保留既有回退。
- 状态、设置、修改、重置、解锁、统计、清理和安全记录查询的存储异常由内存 HTTP 管道最终记录一次；被设置 / 修改消费的 400 / 409 / 429 与 500 / 503 保留响应和错误码，只有 5xx 记录 Error。
- 安全记录查询继续按当前用户限制，规范页码与页大小，过滤管理员路径，保留 DTO 内容与分页元数据，运行日志不复制数据。

结果：

- .NET 定向回归 **98 项通过，0 失败，0 跳过**，包含新增 32 项、既有支付口令 Service / Controller、币调用链和 RuntimeLog 用例。
- Node 日志契约 **27 项通过**；API 构建 **0 警告、0 错误**。
- 沙盒 VSTest 因本地通信端口 SocketException(13) 中止，最小范围提权后执行。首轮新断言未适配旧模板将数量渲染在正文、候选级别为 Info 的格式差异，修正断言后通过。
- 文档、改动文件文本卫生与 `git diff --check` 通过；记录索引 306 行保留超过建议上限 300 行的非阻断提醒。
- 测试项目保留既有 ProducerLoggingTests.cs:261 的 xUnit1051 提示。

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~PaymentPassword|FullyQualifiedName~CoinMovementLoggingTests|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

测试使用 mock 仓储、真实 Service / Controller / 哈希算法与内存 HTTP 错误管道，不代表真实数据库、并发锁定、完整审计中间件或浏览器验收。没有安装依赖、启动项目宿主 / 数据库 / 容器，未写入真实用户数据。

本批保留原有 UpdateAsync false 不影响部分方法 true 返回的语义，不借日志治理修正持久化结果契约。人工解锁未新增持久化原因字段；既有通用审计机制不因删除运行明细升级为事务审计保证。

剩余生成点只读检索发现 PublicHeadSnapshotService / PublicSitemapService 的缓存与生成失败仍输出缓存键、异常原文；下一批聚焦该组及直接消费者。未修改其行为，不把局部调用链完成视为 L2 全部关闭。生产 RadishLogging.Enabled 继续 false，无发布或部署。
