# Windows Razor 托管验证工具

对应[执行方案](../../../Docs/features/hangfire-windows-razor-validation-plan.md)，当前仅完成本地实现 / 契约验证。Windows Full MSBuild 库存、Razor 再生成和完整矩阵须在单独获批的 runner 上执行，不能用本机结果代替。

- `inputs.json` 固定 11 项包、两份 source commit / tree、私有 Windows SDK、23 对模板和包内 DLL；`core-contract.json` 为原 Core 公开契约 / 资源的确定性摘要。
- `Invoke-Validation.ps1 -Phase Inventory` 只读取实际 Windows 工具库存；`Verify` 另外要求四个已审阅工具身份；两个模式都要求 `ExpectedCommit` 与当前 run SHA 一致。
- `Common / Inventory / Acquire / Sources / Verification / Cleanup.ps1` 处理所有权、净化子进程环境、严格包图、直接调用 Full MSBuild、两根目录 / 四类 Core、16 个冷进程片段探针和失败留痕。
- `Tools/` 使用 SDK 自带 Roslyn / NuGet / PE API，无 PackageReference；比较有效 C# token，折叠保持值不变的字符串常量拼接，核对 PE / PDB 和嵌入源码。不会加载执行被检查的 DLL。
- `CaptureRazorAssemblies.cs` 为 Full MSBuild 内联 task，记录实际 CLR / 引擎路径和 hash；不落入 Core 的 Compile glob。
- `Probe/` 显式引用 Core 与 Newtonsoft，实际执行内部 ErrorAlert 的渲染，核对路径、hash 和模板标记；无服务、端口或数据库。

本地检查（复用现有 Node / npm workspace 与 .NET 10 SDK，不安装依赖）：

```bash
node --test Scripts/hangfire/windows/contracts.test.mjs
node Scripts/hangfire/windows/check-local.mjs
```

第二条在系统临时目录复制自有工具、以空 NuGet feed 严格构建 / 自测；从既有本地缓存读取原 Core / Newtonsoft 验证片段探针，并读取已保存的 source bundles 检查适配和生成门禁。缺少这些已审阅缓存 / bundle 会失败，不联网补齐或换版本。结束保存本地证据后删除工作目录，不执行 Razor 或 Windows / PowerShell。

未来 Windows 命令中的 TaskRoot 必须为 `RUNNER_TEMP/radish-hangfire-razor-<run_id>-<attempt>`，ExpectedCommit 必须是获批完整 SHA；原方案省略的该参数在当前入口为必填。工作流的 inventory 默认模式不获取 SDK 或包。

清理后的 `publish/` 仅保留白名单证据及 manifest，失败也保留；SDK、包、产品 DLL / PDB 和业务源代码不会上传。工作流本地接线不授权 push、dispatch、公开源码发行、发布或部署。
