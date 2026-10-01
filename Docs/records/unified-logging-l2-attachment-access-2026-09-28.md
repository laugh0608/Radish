# 统一日志 L2：附件下载、删除与下载计数

日期：2026-09-28（Asia/Shanghai）。本批承接[附件上传与图片处理](./unified-logging-l2-attachment-upload-2026-09-28.md)，稳定契约见[事件契约第 24 节](../features/unified-logging-contract.md)。

## 实现范围

- 删除附件缺失、正常软删除成功及下载权限拒绝的明细日志；正常结果与权限规则保持不变。
- 单项软删除未产生正数更新结果时输出 attachment.delete_rejected Warning，捕获异常时输出 attachment.delete_failed Error；继续返回 false，Controller 保持原响应。
- 下载存储返回空流时输出 attachment.download_unavailable Warning；Service 捕获查询、权限依赖或存储异常时输出 attachment.download_failed Error，仍返回空结果。下载计数消费异常单独记录 attachment.download_count_failed，仍返回已取得的文件流。
- 事件只包含固定说明与适用时的 failureKind，不包含附件 / 用户身份、文件路径、业务类型及异常正文。
- 批量删除保持逐项调用、失败继续和成功数量；无新增批次错误，单项失败各记录一次。资产权限预检原本上抛的异常继续由 API 最终处理。
- 已核对 LocalFileStorage 的布尔 / 空值返回及所有直接生产调用方，本批未修改底层存储接口、异常捕获或返回契约。空流不能区分缺失和捕获故障，日志不伪称其具体原因。

## 验证范围

新增 AttachmentAccessLoggingTests 共 28 项，覆盖旧 / 候选输出与 Development / Production 四种组合：

- 原图、缩略图、普通下载与令牌下载使用原文件流和 MIME，每次调用继续计数，日志安静。
- 缺失、删除、禁用、私有附件、Chat 与 Wiki 权限拒绝保持 404，不读取存储或增加计数。
- 查询、权限依赖、存储异常和空流返回仍为 404，各在相应消费点记录一次安全事件。
- 下载计数更新异常不影响成功流，不被外层重复记录；更新 false 与缺失记录保持原有安静语义。
- 软删除保持字段和 ID 条件，不触发物理删除；未更新 / 异常分别保留原 500 和安全事件，权限拒绝保持 403，Service 缺失记录保持 false。
- 混合批次含成功、缺失与异常，仍成功返回准确的删除数量；资产预检异常由 API 最终记录一次。
- 真实 LocalFileStorage 使用测试目录验证缺失、越界路径拒绝及成功文件读取；空值只记“不可用”，成功流位置与长度不变。

结果：

- .NET 定向回归 **187 项通过，0 失败，0 跳过**，含本批 28 项及既有附件上传、Controller、LocalFileStorage、Wiki 权限、令牌配额与 RuntimeLog 用例。
- Node 日志契约 **27 项通过**；API 构建 **0 警告、0 错误**。
- 文档、改动文件卫生与 `git diff --check` 通过；记录索引 306 行保留超过建议上限 300 行的非阻断提醒。
- VSTest 沙盒运行因本地通信端口 SocketException(13) 中止，最小范围提权后通过。测试项目保留既有 `ProducerLoggingTests.cs:261` 的 xUnit1051 提示。

执行命令：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~AttachmentAccessLoggingTests|FullyQualifiedName~AttachmentUploadLoggingTests|FullyQualifiedName~AttachmentServiceUploadContractTest|FullyQualifiedName~AttachmentControllerTest|FullyQualifiedName~LocalFileStorageTest|FullyQualifiedName~WikiAttachmentAccessServiceTest|FullyQualifiedName~FileTokenQuotaLoggingTests|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

新增测试使用真实 Service / Controller、mock 仓储 / ACL / 令牌依赖、内存 HTTP 管道，另有真实本地文件读取与路径限制；不替代真实数据库、权限故障、计数并发和浏览器验收。测试只创建并清理自身唯一临时目录。下载计数不是本批新增的原子计量保证，底层存储空值也不是故障原因诊断。

未安装依赖、启动项目服务 / 数据库服务 / 容器、写入真实业务数据、发布或部署。生产候选开关继续关闭，不宣称完整附件系统或 L2 整体收口。下一组按当前计划推进支付口令设置、修改、人工解锁与状态查询及直接消费者。
