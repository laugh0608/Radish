# 日志 L2 Outbox / Rust 调用边界记录

> 日期：2026-09-23（Asia/Shanghai）。承接[seed / migration 批次](./unified-logging-l2-seed-migration-2026-09-23.md)。实现规则见[日志契约第 9 节](../features/unified-logging-contract.md)。本记录关闭已验证的 .NET 子项及本机 Rust 返回码 / 安全输出验证，不关闭跨平台原生验收或 L2 整体。

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

## Rust 原生验证补完

首轮 `cargo test --offline` 因缺少 `aligned 0.4.3` 等缓存停止。项目所有者随后明确回复“允许下载”，本次按既有 Cargo.toml 和本地锁定解析下载依赖，使用 `--locked` 保持解析不变；没有改依赖声明或提交锁文件。

当前 Xcode 工具链因许可未确认而无法链接；使用已安装且可运行的独立 Command Line Tools，并仅对本次命令设置 `DEVELOPER_DIR`，没有代接受许可或修改全局 xcode-select。

```bash
DEVELOPER_DIR=/Library/Developer/CommandLineTools cargo test --locked --offline --manifest-path Lib/radish.lib/Cargo.toml --target-dir /private/tmp/radish-l2-native-target
DEVELOPER_DIR=/Library/Developer/CommandLineTools cargo build --locked --offline --manifest-path Lib/radish.lib/Cargo.toml --target-dir /private/tmp/radish-l2-native-target
RADISH_TEST_NATIVE_LIBRARY=/private/tmp/radish-l2-native-target/debug/libradish_lib.dylib dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~OutboxNativeLoggingTests'
```

- Rust：7 通过、0 失败。包含真实 FFI 子进程 stderr 捕获、无效 UTF-8 / 不存在文件、buffer-too-small 和已知 SHA-256 值；错误码保持 0 / -1 / -2。
- 动态库构建成功；保留原有 `watermark.rs` 未使用 `RgbaImage` 导入警告，没有借验证批次扩展清理。
- .NET 真实库定向：13 通过、0 跳过。新增 Native trait 用例明确加载指定绝对路径的产物，验证原生可用、hash 成功与原生错误、有效 PNG 直接水印成功，以及 wrapper 原生返回码触发的安全 fallback。没有配置 `RADISH_TEST_NATIVE_LIBRARY` 时只跳过该真实库用例，不把库缺失当作原生成功。
- 动态库解析仅存在于测试进程，临时测试文件在 finally 清理。Cargo 产物位于 `/private/tmp/radish-l2-native-target`；没有复制动态库到产品部署目录，没有启动业务宿主。

## 新发现与后续边界

复测相同有效 PNG 字节：直接调用原生水印时 `.png` 输入返回 0，`.tmp` 输入返回 -1。现有 .NET wrapper 写入 `.tmp`，而 Rust 使用按扩展名识别格式的 `image::open`，会触发 C# fallback。这是既有图片处理边界问题；本批记录证据，没有改变解码逻辑，也不宣称 wrapper 原生水印加速成功。后续修复应以内容识别格式并补真实 wrapper 成功路径回归，先确认该运行行为调整范围。

日志顺位继续其余后台任务与 Hangfire 最终失败边界。`RadishLogging.Enabled=false` 保持不变；没有新增生产运行、发布或部署证据。
