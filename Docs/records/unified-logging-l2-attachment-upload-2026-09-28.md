# 统一日志 L2：附件上传与图片处理

日期：2026-09-28（Asia/Shanghai）。本批承接[分片上传与会话回写](./unified-logging-l2-chunked-upload-2026-09-28.md)，稳定契约见[事件契约第 23 节](../features/unified-logging-contract.md)。

## 实现范围

- AttachmentService 的上传与图片处理移除正常明细、处理失败结果原文和包装再抛的重复日志；LocalFileStorage 上传移除重复错误，保持 StorageFailed 结果与失败目标清理。
- 普通上传 Controller 的 5xx BusinessException 与空附件结果在最终消费点记录安全 http.failed；4xx 安静，未处理异常继续上抛。配额结算 / 释放消费异常复用固定安全 upload.cleanup.failed，保留原响应语义。
- 去重物理文件缺失保留固定 Warning；文件替换保留原有最多 3 次尝试与 100ms 间隔，恢复后一次 Warning，最终失败交给上层。
- 失败上传清理仍逐项处理主文件、缩略图和预登记派生路径，仅将失败日志合并为本次清理的一条计数 Error。临时文件删除异常单独记录安全 Error。
- 未改变上传归属、类型 / 内容校验、去重筛选、图片处理、业务错误码、事务边界、配额计量或清理规则；下载、删除、计数及存储的其他操作留作下一组。

## 验证范围

新增 AttachmentUploadLoggingTests 共 32 项，覆盖旧 / 候选输出与 Development / Production 四种组合：

- 普通成功与去重命中安静；原归属、未绑定附件写入及缺失物理文件时的软删除保持原样。
- 大小 / 类型拒绝保持 413 / 415 和安静；StorageFailed 结果保持 500 与最终单次日志；未知存储异常保留原对象并由 API 最终处理。
- 四类图片处理失败保持 500，清理主文件 / 缩略图及多尺寸预登记路径，不写附件；成功组合保留派生路径、水印文本传给处理器、EXIF 替换结果且不输出明细。
- 清理 IO 异常与删除返回 false 且仍存在的两条路径汇总一次失败计数，原上传失败仍单独可见。
- 图片 / 文档两类接口：附件持久化后配额结算失败仍成功；空附件结果及配额释放失败保留 500，并分别输出安全事件。
- 真实 LocalFileStorage 读取异常转 StorageFailed，不输出重复异常；文件替换持续失败仍经既有尝试耗尽路径返回错误，不逐次日志刷屏。

结果：

- .NET 定向回归 **154 项通过，0 失败，0 跳过**，含本批 32 项及既有附件上传契约、Controller、LocalFileStorage、分片日志与 RuntimeLog 用例。
- Node 日志契约 **27 项通过**；API 构建 **0 警告、0 错误**。
- 文档、改动文件卫生与 `git diff --check` 通过；记录索引 306 行保留超过建议上限 300 行的非阻断提醒。
- VSTest 沙盒运行因本地通信端口 SocketException(13) 中止，最小范围提权后通过。测试项目保留既有 `ProducerLoggingTests.cs:261` 的 xUnit1051 提示。

执行命令：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~AttachmentUploadLoggingTests|FullyQualifiedName~AttachmentServiceUploadContractTest|FullyQualifiedName~AttachmentControllerTest|FullyQualifiedName~LocalFileStorageTest|FullyQualifiedName~ChunkedUploadLoggingTests|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

新日志测试使用真实附件 Service / Controller、mock 仓储 / 图片处理器 / 配额，以及测试专用临时文件；另有 LocalFileStorage 故障调用和既有存储回归。HTTP 管道仅内存执行。临时图片输出限定在测试唯一目录，结束后仅清理该目录；不替代真实数据库、Rust 动态库、图片视觉质量、文件权限故障或重试恢复时序验收。Rust 原生回退与清理事件未在此批修改，也不据此宣称所有附件路径全链路只产生一个事件。

未安装依赖、启动项目服务 / 数据库服务 / 容器、写入真实业务数据、发布或部署。生产候选开关继续关闭，L2 尚未整体完成。下一组继续附件下载、删除与下载计数及直接消费者。
