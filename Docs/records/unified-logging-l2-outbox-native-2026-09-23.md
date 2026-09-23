# 日志 L2 Outbox / Rust 调用边界记录

> 日期：2026-09-23（Asia/Shanghai）。承接[seed / migration 批次](./unified-logging-l2-seed-migration-2026-09-23.md)。实现规则见[日志契约第 9 节](../features/unified-logging-contract.md)。本记录只关闭已验证的 .NET 子项，不关闭 Rust 原生门禁或 L2 整体。

## 改动与边界

- Outbox 空分派安静，非空分派一次摘要。执行 Job 移除每次失败的原文 Error；Repository 根据实际影响行的状态更新记录重试 Warning 或死信 Error，保留稳定 operationId、库范围与尝试次数。
- 保留重试 / 抖动 / 最大次数、状态过滤、租约、审计错误码和永久失败摘要及内容治理失败回写。瞬时失败的固定提示同步为查看错误码及安全摘要，Service 仅补受控 failureKind，不输出完整异常。重试耗尽以 Repository 真正选择的状态为准；未借日志修改既有租约归属风险。Job 不再注入未使用的 logger，相关构造测试同步更新，服务与 Repository 接口不变。
- Rust FFI 显式 `eprintln!` 已移除，错误码和导出签名保持不变。.NET Rust wrapper / 工厂改用安全事件，去掉路径、异常原文与重复工厂 Info；图片水印正常使用 C#，无需能力降级警告。
- 水印恢复只调用一次，C# 结果 / 异常仍向上交付；原生输出读取流及时释放。未扩展到 AttachmentService 上层日志、Rust panic / 指针安全、其余定时任务或 Hangfire 框架治理。

## 已执行验证

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~OutboxNativeLoggingTests|FullyQualifiedName~ReliableOutboxJobTest|FullyQualifiedName~ReliableOutboxServiceTest|FullyQualifiedName~ReliableOutboxRepositoryTest|FullyQualifiedName~RuntimeLog|FullyQualifiedName~AttachmentServiceUploadContractTest|FullyQualifiedName~ImageProcessorTest'
dotnet build Radish.Api/Radish.Api.csproj --no-restore --warnaserror
npm run check:logging-contract
rustfmt --edition 2024 --config skip_children=true --check Lib/radish.lib/src/lib.rs
npm run check:repo-quality:changed
git diff --check
```

- .NET 最终定向回归：93 通过、0 失败、0 跳过。覆盖 Main / Chat 实际 SQLite 重试与死信、无效重复状态、永久失败审计保持、空 / 非空分派、旧 / 候选日志安全、宿主失败返回值、正常 C# 路由及上传 / 图片处理原有契约。
- Node 共用日志契约：27 通过。API 构建 0 警告、0 错误；Rust 源文件格式、仓库卫生、文档链接和 diff 检查通过，records 索引保留篇幅软提醒。
- 测试项目编译保留上一批 `ProducerLoggingTests.cs` 的 1 条 xUnit1051 提示；本批新增测试没有该提示。
- .NET 首轮测试发现两个 mock 使用小写库名而 Dispatcher 传入常量 `Main / Chat`，已修正测试替身；最终回归通过。
- 初次沙盒测试遇到 MSBuild 命名管道权限错误，后续仅对无 restore 的构建 / 测试最小范围提权；失败进程已退出。未启动业务宿主、数据库容器或浏览器 Smoke。

## Rust 原生验证待办

已尝试：

```bash
cargo test --offline --manifest-path Lib/radish.lib/Cargo.toml --target-dir /private/tmp/radish-l2-native-target
```

离线缓存缺少 `aligned 0.4.3` 等包，Cargo 在编译前停止。未下载依赖、未改 Cargo.toml 或提交锁文件；已询问项目所有者是否允许本任务下载现有声明所需依赖，尚待答复。

源文件已补真实 FFI 子进程 stderr 捕获、无效 UTF-8 / 不存在文件、buffer-too-small、已知 SHA-256 值等测试；尚未编译执行，不能称原生测试通过。当前 .NET 失败路径测试不替代原生库加载后的全链路验证。

获准后按现有声明下载并执行 Cargo 测试，再构建临时动态库验证 .NET P/Invoke 的原生返回码路径；产物限定 `/private/tmp`，不复制到产品部署目录，不启动宿主。随后继续其余后台任务与 Hangfire 最终失败边界。

`RadishLogging.Enabled=false` 保持不变；没有新增生产运行、发布或部署证据。
