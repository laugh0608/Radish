# 统一日志 L2：分片上传与会话回写

日期：2026-09-28（Asia/Shanghai）。本批承接[文件令牌与上传配额](./unified-logging-l2-file-token-quota-2026-09-28.md)，按[当前计划](../planning/current.md)推进分片编排层；稳定契约见[事件契约第 22 节](../features/unified-logging-contract.md)。

## 实现范围

- ChunkedUploadService 移除创建会话、逐片上传、合并成功与取消的 4 处明细 Info，移除合并主流程的日志再重抛。
- 附件已持久化但 Completed 回写失败时，保留原有一次补写；补写成功输出一次 `upload.session.update_recovered` Warning，仍失败输出一次 `upload.session.update_failed` Error，替代此前两次重复错误。事件仅带安全 failureKind，不输出会话、附件、文件路径或异常原文。
- 主合并失败后的 Failed 状态补写失败仍单独消费，输出一次安全 Error；原始合并异常继续交给上层，两次不同故障分别可见。
- 参数校验、用户归属、keyed lock、磁盘分片回滚、失败状态与消息、配额释放 / 结算、目录清理、成功响应与已完成会话重放保持不变。未新增重试或改变持久化顺序。
- 前台目录 / 配额清理 helper 和后台过期清理汇总继续复用既有安全事件。普通上传和底层 AttachmentService 的其他生成点留待下一组，不宣称端到端附件上传已完成治理。

## 验证范围

新增 `ChunkedUploadLoggingTests` 共 24 项，覆盖旧 / 候选输出与 Development / Production 四种组合：

- 创建、逐片上传、合并成功、已完成会话重放、查询与重复取消保持安静；重放不重复持久化附件，目录按原流程清理。
- Completed 首次回写失败后恢复 / 持续失败两种结果：只补写一次，各输出一个对应事件；响应继续成功，配额结算一次且不释放，持久化状态保持原行为。
- 合并附件处理异常写入 Failed 状态，释放配额并删除分片；Controller 包装及 API 最终单次安全错误不变。
- Failed 状态补写故障与原始合并故障分别记录，原异常保留在 Controller 包装的 InnerException 中，清理继续。
- 配额结算失败不改变已保存附件的成功响应，输出既有安全 `upload.cleanup.failed`。
- 分片状态条件更新失败删除未提交文件；创建记录失败清理会话目录并释放预留；这两条上抛路径不新增本地错误日志。

结果：

- .NET 定向回归 **93 项通过，0 失败，0 跳过**，含本批 24 项及既有分片上传、服务内清理与 RuntimeLog 用例。
- Node 日志契约 **27 项通过**；API 构建 **0 警告、0 错误**。
- 文档、改动文件卫生与 `git diff --check` 通过；记录索引 306 行保留超过建议上限 300 行的非阻断提醒。
- 首轮新测试将条件更新拒绝精确断言为 InvalidOperationException，实际为既有派生类型 SessionUpdateRejectedException；调整为接受派生类型后通过，未修改业务异常。测试项目保留既有 `ProducerLoggingTests.cs:261` 的 xUnit1051 提示。
- VSTest 在沙盒中因本地通信端口 SocketException(13) 中止，最小范围提权重跑取得上述结果。

执行命令：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~ChunkedUploadLoggingTests|FullyQualifiedName~ChunkedUploadServiceTest|FullyQualifiedName~ServiceCleanupLoggingTests|FullyQualifiedName~RuntimeLog' --verbosity minimal
npm run check:logging-contract
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
npm run check:repo-quality:changed
git diff --check
```

## 证据限制与下一步

使用真实分片 Service / Controller、测试专用临时文件与 mock 仓储 / 附件 / 配额依赖；API 管道在内存中执行，不替代真实数据库、图片处理、Redis、并发跨实例或浏览器 Smoke。测试结束仅删除自身创建的唯一临时目录。底层 AttachmentService 尚有待治理日志，本批单次异常证据限定于分片编排及最终消费边界。

本批未安装依赖、启动项目服务 / 数据库服务 / 容器、写入真实业务数据、发布或部署。生产候选开关继续关闭，L2 尚未整体完成。下一组继续 AttachmentService 的上传、图片处理、下载、删除及普通上传 Controller 的直接消费边界。
