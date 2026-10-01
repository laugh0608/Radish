# 日志 L2 服务内清理分支治理

> 日期：2026-09-23（Asia/Shanghai）。本批承接项目所有者“继续推进下一步”，范围为分片会话 / 文件访问令牌清理及直接配额结算调用链。契约见[统一日志第 13 节](../features/unified-logging-contract.md)，下一顺位见[当前进行中](../planning/current.md)。

## 实现与行为边界

- `ChunkedUploadService` 将过期状态更新、目录对账及终态配额重放合并为一次安全批次摘要；目录 / 配额 helper 在前台复用时只记录固定操作类型的已消费失败。移除 helper 中仅用于原始日志的附件 ID 参数，不改变公开接口。
- `FileAccessTokenService` 根据仓储撤销返回值统计实际更新与并发跳过，不再用查询数冒充成功撤销数。
- `UploadRateLimitService` 完成 / 释放预留不再逐项输出用户、上传标识及大小；内存与 Redis 分支调用、幂等与容量结算逻辑不变。
- 已消费异常聚合为安全 Error；未消费异常仍传播。中断前若有变更，记录此前进度，不声明整批完成，也不重复记录该传播异常的 Error。
- 结算计数是 Task 正常返回次数，不能代表实际新增结算；仅成功重放、空扫描及仅条件更新跳过均保持安静。
- 共享策略新增 `job.cleanup.interrupted / upload.cleanup.failed`、两个 jobKind、cleanupOperation 固定枚举及 settlementCount 数值属性。旧与候选 sink 均只接收安全模板、数字及固定枚举。
- 保留跨租户条件更新、keyed lock、目录查询批次 500、孤立目录宽限 30 分钟、终态重放窗口 8 天 / 上限 2000、批次结算去重、令牌到期条件、文件生命周期及异常消费 / 传播位置。

## 验证

- 新增 `ServiceCleanupLoggingTests`：旧 / 候选 sink 安全输出、令牌实际撤销数 / 并发跳过、部分完成后传播异常、分片混合失败聚合、结算去重、孤立目录宽限与终态目录规则、空批次 / 仅重放安静、前台取消 / 合并重放失败、真实内存配额幂等结算。
- 定向 .NET 回归 **114 项通过、0 失败、0 跳过**：新日志用例，既有 ChunkedUploadService、FileAccessTokenService / Repository、UploadRateLimitService、CleanupLogging 与 RuntimeLog 用例。
- `npm run check:logging-contract`：**27 项通过**，覆盖共享策略与 Node 输出回归。
- API `dotnet build --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false`：**0 警告、0 错误**。
- 定向测试先在沙盒编译；修正新测试缺失的 Memory 命名空间后，VSTest 因本地通信端口 SocketException(13) 中止，按最小范围提权重跑取得上述结果。测试项目仍有既有 `ProducerLoggingTests.cs:261` xUnit1051 提示，本批不改动该用例。
- 文档、仓库卫生及 `git diff --check` 通过；记录索引现有 310 行，保留超过建议 300 行的非阻断提醒。不将定向测试描述为全仓回归。

复现命令：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~ServiceCleanupLoggingTests|FullyQualifiedName~ChunkedUploadServiceTest|FullyQualifiedName~FileAccessTokenServiceTest|FullyQualifiedName~FileAccessTokenRepositoryTest|FullyQualifiedName~UploadRateLimitServiceTest|FullyQualifiedName~CleanupLoggingTests|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 未执行与下一步

本批没有安装依赖、启动业务服务 / 隔离容器、修改真实文件或令牌数据、发布或部署；目录测试仅使用自建临时目录并清理。仓储回归使用既有本地测试路径；未执行真实 PostgreSQL / Redis 服务验收或线上定时任务 Smoke。

分片创建 / 合并的其他业务日志、令牌创建 / 验证 / 主动撤销、配额申请 / 重置和 AttachmentService 等入口仍未覆盖。本批仅关闭服务内清理子项，L2 仍未完成，`RadishLogging.Enabled=false` 不变；后续优先推进币扣除 / 转账及其外层消费者日志治理。
