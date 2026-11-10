# NoDrive 来源补证与分发接线预审（2026-10-26）

> 承接[正式采用记录](./nodrive-adoption-2026-10-26.md)。本批只读核对上游、保存来源证据并形成[分发接线方案](../features/nuget-notices-distribution-plan.md)，尚未修改构建、依赖或运行行为。

## 1. 本轮新增证据

原 nuspec 的 `https://github.com/sunkaixuan/SqlSugar` 返回仓库迁移，跟随后确认当前仓库为 [DotNetNext/SqlSugar](https://github.com/DotNetNext/SqlSugar)，GitHub repository ID 保持 `38992233`。连接器不支持仓库 ID 重定向端点，因此该步使用公开 GitHub REST 请求跟随重定向；后续源码读取恢复使用连接器。

[结构化证据](./nodrive-source-provenance-2026-10-26.json)保存 NuGet catalog 地址、发布时间、包与 DLL 哈希、6 份上游文本及 Git blob 校验、候选提交和完整 tree 的扫描摘要。所有 6 份文本的 Git blob SHA1 均与上游匹配；两个本地 `.nupkg` 的实际 SHA512 均与 NuGet 官方 catalog 匹配，不仅比较缓存中的哈希声明。

| 对象 | 确认事实 | 不能据此宣称 |
| --- | --- | --- |
| NoDrive `5.1.4.207` | [提交 `7a7ba892`](https://github.com/DotNetNext/SqlSugar/commit/7a7ba892f30fd2f2e2b826721ed520f288104815)同时将 Core csproj、通用包及 NoDrive nuspec 更新到该版本；提交时间 `2025-10-28T03:15:11Z`，NuGet 发布时间 `03:19:43.827Z` | 相邻时间与版本一致不是已发布 DLL 的精确构建证明 |
| 同名后续提交 | `e873a481`、`a0e52cf1` 分别发生在 `03:20:51Z`、`03:21:23Z`，修改 `Src/Asp.Net` 下的旧 Framework 内容 | 不能按最新同名提交直接绑定 NoDrive 包 |
| NoDrive 打包入口 | [nuspec](https://github.com/DotNetNext/SqlSugar/blob/7a7ba892f30fd2f2e2b826721ed520f288104815/Src/Asp.NetCore2/SqlSugar/SqlSugarForCoreNoDrive.nuspec)从作者本机绝对路径读取 Debug `SqlSugar.dll`；bat 仅调用 NuGet pack | 没有可据此重建相同二进制的固定构建证明 |
| SystemCommon `8.5.0` | NuGet 发布时间为 `2024-09-16T14:22:47.117Z`；仓库对应打包文件最近一次变更为 [`f1f3b310`](https://github.com/DotNetNext/SqlSugar/commit/f1f3b3100f18fb286d6c177bcffd3fda893249d8)，时间为同日 `14:08:24Z`，但文件版本仍写 `8.3` | 不能把该源码版本或 2025 年 NoDrive 提交绑定为 `8.5.0` 的精确来源 |

NoDrive DLL SHA256 为 `38a361e486a809ee2242861ce5ecb94016dca99bcaa9541d8f54cda8b06e9d0b`；SystemCommon 内另有 SHA256 为 `062c457dc941dcd1f063ebc40d8c4286132fa8682f09762ba16c54f758be21f0` 的同名 DLL。正式采用已验证四宿主选择前者；本轮不再次变更程序集选择。

## 2. 许可声明差异与 NOTICE 检索边界

- 两个精确 NuGet 包仍以 nuspec 指向 Apache-2.0，保留作者 `sunkaixuan` 与 `Copyright 2016`。NoDrive 对应源码打包文件同样指向 Apache-2.0。
- 上述 NoDrive 候选提交的根 [LICENSE](https://github.com/DotNetNext/SqlSugar/blob/7a7ba892f30fd2f2e2b826721ed520f288104815/LICENSE)却为 MIT，署名 `Copyright (c) 2025 jacktang`；SystemCommon 相关历史提交的根 [LICENSE](https://github.com/DotNetNext/SqlSugar/blob/f1f3b3100f18fb286d6c177bcffd3fda893249d8/LICENSE)为 MIT，署名年份 `2022`。
- 保留这两种来源的原始声明，不擅自将已发 NuGet 包改标 MIT，不解释为已经确认的双许可选择，不推定所有传递包同许可。
- 检查了 `7a7ba892` 的 5,126 条及 `f1f3b310` 的 4,631 条完整 Git tree，两次均为 `truncated=false`；按文件名扫描 LICENSE / NOTICE / COPYING 仅命中根 LICENSE。未发现 NOTICE 文件只适用于这两个 tree；未完成逐份源文件头、未绑定二进制及其他发布材料的穷尽核对。

**源码对应关系与许可差异仍未关闭。** 后续可依据上游提供的精确打包 commit、构建记录或明确版本声明继续核实；本轮没有联系上游、发布 issue 或提交外部消息。现有证据不足以将发行状态标为完整。

## 3. 分发接线可行性

四宿主 Dockerfile 均先复制仓库构建上下文，再完整复制 `/app/publish` 到最终镜像；当前 `.dockerignore` 不排除拟定的 `ThirdParty/` 与 `Scripts/build/` 路径。可在共享 MSBuild 入口接线，无须修改 Dockerfile 或引入新运行时。

本机 SDK `10.0.300` 的 `ResolvePackageDependencies` 与 `GetFileHash` 可分别供包版本核对与材料完整性校验。只读诊断命令：

```bash
dotnet msbuild Radish.Gateway/Radish.Gateway.csproj \
  -target:RunResolvePackageDependencies -p:EmitLegacyAssetsFileItems=true \
  -getItem:PackageDefinitions -nologo
```

返回 57 个定义（含 project），重点包均具备 `Name` / `Version` / `ResolvedPath`。`EmitLegacyAssetsFileItems` 仅用于这次命令探测，未写入仓库；实施方案拟在独立 target 内直接调用 SDK 任务，避免全局改变 SDK item 行为。命令未 restore、未启动宿主。

11 个重点 runtime 包进入四宿主中的实际消费者；Mapperly / Container Tools 两个 build-only 包保留开发材料与用途说明。具体目录、文本格式、缺失失败策略及验证范围见[待确认方案](../features/nuget-notices-distribution-plan.md)。已有 226 条清单与 13 组件审查包是本轮输入，未改写其历史哈希。

## 4. 未关闭项

1. 批准并实现分发接线，随后实测四宿主 fresh publish 中的文件及哈希；当前尚无新 notices 交付结果。
2. 对 NoDrive / SystemCommon 的来源及许可差异取得更强版本证据；保留显式未关闭状态。
3. Hangfire 源码提供 / 可替换链接等发行条件、SNI 的实际发行方式以及其他常规包的全量声明仍按许可专题核对。正文复制不等于所有发行条件已满足。
4. Linux 镜像、Flutter / Web / Rust 全生态发行材料和 Valkey 迁移不属于本次接线范围；当前不执行发布。

## 5. 本批检查

`npm run check:docs` 通过：892 个文件、843 个 Markdown 文件与 1,029 个本地相对链接均通过。变更范围文本卫生与 `git diff --check` 通过；记录索引原有篇幅已超过建议 300 行，本次新增一条入口后为 320 行，仅提示，不扩入无关索引拆分。本批只有文档与证据文件改动，尚未提交或推送。
