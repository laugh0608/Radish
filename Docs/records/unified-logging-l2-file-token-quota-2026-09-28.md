# 统一日志 L2：文件访问令牌与上传配额

日期：2026-09-28（Asia/Shanghai）。本批承接[权益与背包使用](./unified-logging-l2-shop-entitlements-2026-09-28.md)，按[当前计划](../planning/current.md)将附件相关入口分组推进；稳定契约见[事件契约第 21 节](../features/unified-logging-contract.md)。

## 实现范围

- FileAccessTokenService 移除 4 处正常创建 / 消费 / 撤销 Info 和 4 处正常拒绝 Warning，删除仅为日志提供哈希片段的 MaskHash。运行日志不再复制令牌记录、附件身份和凭据哈希片段。
- UploadRateLimitService 移除预留拒绝的用户 / 文件大小 Warning 与重置计数的用户 Info。
- 保留哈希存储、令牌有效期、用户 / IP / 次数限制、当前 Wiki ACL、原子消费、撤销冲突与查询摘要；保留 Redis Lua、内存 keyed lock、预留 / 结算 / 释放 / 重置及业务日规则。
- 两个 Service 未新增异常捕获；令牌 Controller 既有 500 包装、按 ID 撤销直接传播及 API 最终安全日志继续复用。普通上传与分片创建的配额申请异常继续向上传播；配额正常拒绝继续返回原结果 / 429。
- 已核对 FileAccessTokenRepository 与 WikiAttachmentAccessService 没有重复日志生成点。既有清理批次汇总保持不变，不宣称整个附件上传与下载链路已完成治理。

## 验证范围

新增 `FileTokenQuotaLoggingTests` 共 20 项，覆盖旧 / 候选输出与 Development / Production 四种组合：

- 创建响应保留原始令牌，持久化只写哈希；有效期与 IP 规范化不变。成功消费调用原子仓储，两个撤销入口及摘要查询保持安静。
- 空令牌、缺失、撤销、到期、用户 / IP 不匹配、次数耗尽、附件删除、Wiki 拒绝与原子消费未命中的 10 类拒绝；验证响应为 403，前置拒绝不消费次数。撤销冲突仍为 409 且日志安静。
- 创建、消费、兼容撤销、查询的存储异常保留 500 包装和 InnerException；按 ID 撤销保留原异常；内存 API 管道仅记录一次安全 `http.failed`。
- 真实内存配额的重复预留、并发 / 日容量 / 分钟频率拒绝、普通上传 429、重复结算、失败释放与用户隔离重置；正常流程不输出日志。
- 缓存 IO 故障经普通上传或配额重置传播，保留原异常对象；内存 API 最终记录一次 Error，不输出私密哨兵或 `runtime.unclassified`。

结果：

- .NET 定向回归 **127 项通过，0 失败，0 跳过**，包括本批 20 项及令牌 Service / Repository、配额、AttachmentController、Wiki 访问权限、服务内清理和 RuntimeLog 的既有用例。
- Node 日志契约 **27 项通过**；API 构建 **0 警告、0 错误**。
- 文档、改动文件卫生与 `git diff --check` 通过；记录索引 306 行保留超过建议上限 300 行的非阻断提醒。
- 首轮测试编译修正了新夹具的缓存生命周期假设及下载 DTO 类型；生产代码没有因此改动。测试项目保留既有 `ProducerLoggingTests.cs:261` 的 xUnit1051 提示。
- 沙盒中的 VSTest 因本地通信端口 SocketException(13) 中止，最小范围提权重跑后取得上述通过结果。

执行命令：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~FileTokenQuotaLoggingTests|FullyQualifiedName~FileAccessTokenServiceTest|FullyQualifiedName~FileAccessTokenRepositoryTest|FullyQualifiedName~UploadRateLimitServiceTest|FullyQualifiedName~AttachmentControllerTest|FullyQualifiedName~WikiAttachmentAccessServiceTest|FullyQualifiedName~ServiceCleanupLoggingTests|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

新增日志测试使用真实 Service / Controller、mock 令牌仓储与下载依赖、真实内存缓存以及内存 HTTP 管道；关联回归含既有 SQLite 令牌仓储并发及迁移测试。不替代 Redis Lua 运行、PostgreSQL 多实例或真实宿主验收。配额重置未新增生产入口，其内存错误管道仅验证最终异常所有权。

本批未安装依赖、启动项目服务 / 数据库服务 / 容器、写入真实业务数据、发布或部署。生产候选开关继续关闭。下一批继续 AttachmentService、普通上传后续处理与分片创建 / 上传 / 合并 / 取消的其余日志；L2 尚未整体完成。
