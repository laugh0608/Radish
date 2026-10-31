# Windows Razor 托管验证工具

对应[执行方案](../../../Docs/features/hangfire-windows-razor-validation-plan.md)，首次 inventory 因 VS 2022 查询为空失败并清理；后续本地修复已改为 `windows-2022` + VS 2022，未再次运行。Windows Full MSBuild 完整库存、Razor 再生成和完整矩阵须在单独获批的 runner 上执行，不能用本机结果代替。

- `inputs.json` 固定 11 项包、两份 source commit / tree、私有 Windows SDK、23 对模板和包内 DLL；`core-contract.json` 为原 Core 公开契约 / 资源的确定性摘要。
- `Invoke-Validation.ps1 -Phase Inventory` 只读取实际 Windows 工具库存；`Verify` 另外要求四个已审阅工具身份；两个模式都要求 `ExpectedCommit` 与当前 run SHA 一致。
- `Common / Inventory / Contracts / Acquire / Sources / Verification / Cleanup.ps1` 处理所有权、净化子进程环境、严格包图、直接调用 Full MSBuild、两根目录 / 四类 Core、16 个冷进程片段探针和失败留痕。
- `inventory-progress.json` 在任何契约子进程 / VS 查询前记录 runner 与工作流身份，并分阶段保存查询及已取得工具身份；`complete=false` 不是成功库存。只有全部检查通过才生成 `inventory.json` / `complete=true`。
- `Contracts.ps1` 在同一入口、实际采集前执行 8 组 PowerShell 行为契约：成功 / 预期 exit 23 命令身份，VS 零 / 一 / 多实例、错误 major、非法 JSON 证据，以及不完整库存拒绝。仅两个已有 pwsh 子进程、虚构 VS 数据和任务私有 fixture，报告标记 `fixtureOnly=true`；不获取包、不执行 MSBuild 或 Razor。
- `Tools/` 使用 SDK 自带 Roslyn / NuGet / PE API，无 PackageReference；比较有效 C# token，折叠保持值不变的字符串常量拼接，核对 PE / PDB 和嵌入源码。不会加载执行被检查的 DLL。
- `CaptureRazorAssemblies.cs` 为 Full MSBuild 内联 task，记录实际 CLR / 引擎路径和 hash；不落入 Core 的 Compile glob。
- `Probe/` 显式引用 Core 与 Newtonsoft，实际执行内部 ErrorAlert 的渲染，核对路径、hash 和模板标记；无服务、端口或数据库。

本地检查（复用现有 Node / npm workspace 与 .NET 10 SDK，不安装依赖）：

```bash
node --test Scripts/hangfire/windows/contracts.test.mjs Scripts/ci/validate-sqlite-platform.test.mjs Scripts/hangfire/toolchain.test.mjs
node Scripts/hangfire/windows/check-local.mjs
```

第一条包含 PowerShell 接线与源码静态契约，不替代 PowerShell parser / 行为执行。本次修复未修改 C#，只重跑第一条及文档 / 卫生检查；新增 8 组 PowerShell 契约留待新提交获批的 Windows inventory。第二条在系统临时目录复制自有工具、以空 NuGet feed 严格构建 / 自测；从既有本地缓存读取原 Core / Newtonsoft 验证片段探针，并读取已保存的 source bundles 检查适配和生成门禁。缺少这些已审阅缓存 / bundle 会失败，不联网补齐或换版本。结束保存本地证据后删除工作目录，不执行 Razor 或 Windows / PowerShell。

未来 Windows 命令中的 TaskRoot 必须为 `RUNNER_TEMP/radish-hangfire-razor-<run_id>-<attempt>`，ExpectedCommit 必须是获批完整 SHA；原方案省略的该参数在当前入口为必填。工作流的 inventory 默认模式不获取 SDK 或包。

清理后的 `publish/` 仅保留白名单证据及 manifest，失败也保留；SDK、包、产品 DLL / PDB 和业务源代码不会上传。工作流本地接线不授权 push、dispatch、公开源码发行、发布或部署。
