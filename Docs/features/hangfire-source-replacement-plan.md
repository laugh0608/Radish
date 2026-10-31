# Hangfire 源码材料与 Core 替换验证方案

> 2026-10-26：第 3 节及第 3.4 节 SDK 分析器调整均已获批准并执行。Core 基线 / 诊断修改库严格构建与三组 SQLite 替换验证通过，详见[实施记录](../records/hangfire-core-replacement-2026-10-26.md)。Radish 正式依赖与默认构建未变；本批不等于完整官方发行重建或对应源码交付。

## 1. 已确定的输入

当前发布选择 `Hangfire.Core netstandard2.0`、`Hangfire.NetCore netstandard2.1`、`Hangfire.AspNetCore netcoreapp3.0` 与 `Hangfire.PostgreSql netstandard2.0`。

- Hangfire 三包版本 `1.8.14`，源码 commit `51647c20e36ab010f7dfc4fa8b1ed666070d7f4f`；实际 DLL 内 SourceLink 与该 commit 一致。
- Core 合入 `Cronos 0.8.3`，其包 nuspec、Core 内 SourceLink 都指向 `9d6e349e58fda615e47be10c3d9a539e9ce85073`；7 个 Cronos PDB 源码校验和全部匹配。
- LibLog、StackTraceFormatter、StackTraceParser 和 MoreLINQ 的已编译源码均在 Hangfire 仓库中；不能只用原始 NuGet `.pp` 模板替代这些已纳入仓库的文件。
- 三个目标与四个上游打包辅助包共涉及 27 个精确锁定条目，完整版本、contentHash、使用位置和缓存情况见[构建输入清单](../records/hangfire-build-inputs-2026-10-26.json)。该清单不是整个上游解决方案全部 TFM / 测试的 restore 闭包。
- 已只读取得 9 个重点包归档并用本机 SDK 自带 NuGet API 核对 contentHash。签名 nupkg 的 archive SHA512 与 lockfile contentHash 是不同口径，不能直接拿两者比较后放宽校验。

## 2. 为什么先做 Core 的受控替换

上游完整发行流程使用 `Hangfire.Build 0.4.4`、`psake 4.4.1`、`RazorGenerator.MsBuild 2.5.0` 和 `ILRepack 2.0.27`，包含 Windows Full MSBuild 的 Razor 再生成及 SQL Server / MSMQ 等测试。本机只有 .NET SDK `10.0.300`，没有 `pwsh`；不将普通单目标构建称为完整官方发行重建。

首批建议只针对实际交付的 Core `netstandard2.0`，复用提交中已有的 Razor 生成文件，保留未修改的原始源码归档，单独保存试验适配补丁。用原包的 Cronos DLL 按上游 `/target:library /internalize` 合并，再以保持公共接口的诊断标记证明修改库实际进入调用路径。Cronos 自身源码重建、全部 TFM、PostgreSql 插件独立重建和正式源码托管分别保留后续范围。

四个当前 Hangfire DLL 的 metadata 公钥均为空；试验仍须核对程序集身份和公共接口，不通过修改加载策略、关闭签名检查或失败后换回原 DLL 取得通过结果。

## 3. 已批准的精确试验范围

### 3.1 工具、源码与 restore

使用已有 .NET SDK `10.0.300`，全部操作放在 `/private/tmp/radish-hangfire-replacement-*`。通过既有 Git 获取上文 Hangfire commit 的浅克隆，保留正确 Git 来源；不修改 Radish 工作区或 NuGet 全局缓存。

仅允许 Core 锁文件 `netstandard2.0` 分组中的以下 **13 个包**进入隔离 restore，版本 / contentHash 原样取自清单。已有 7 个 archive 可复用，其余 6 个需从 NuGet 官方地址读取；写入隔离 feed 前验证全部 contentHash，出现额外包或不匹配即停止。

| 包 | 版本 | 已有 archive |
| --- | --- | --- |
| Cronos | 0.8.3 | 本批审阅包 |
| LibLog | 1.5.0 | 本批审阅包 |
| MoreLinq.Source.MoreEnumerable.Pairwise | 1.0.1 | 本批审阅包 |
| StackTraceFormatter.Source | 1.1.0 | 本批审阅包 |
| StackTraceParser.Source | 1.3.0 | 本批审阅包 |
| Microsoft.CSharp | 4.4.0 | 待取得 |
| Newtonsoft.Json | 11.0.1 | 本机缓存 |
| NETStandard.Library | 2.0.3 | 待取得 |
| Microsoft.NETCore.Platforms | 1.1.0 | 本机缓存 |
| Microsoft.CodeAnalysis.NetAnalyzers | 8.0.0 | 待取得 |
| Microsoft.SourceLink.GitHub | 8.0.0 | 待取得 |
| Microsoft.SourceLink.Common | 8.0.0 | 待取得 |
| Microsoft.Build.Tasks.Git | 8.0.0 | 待取得 |

这些是精确上游源码试验输入，不用于回退 Radish 的正式包版本。保留上游已有诊断设置，不新增审计禁用、`NoWarn` 或“忽略失败源”；无法在此边界内构建时报告真实错误，再确认调整。

在临时源码副本中将 Core 目标缩小为 `netstandard2.0`，派生仅含该分组的锁文件，记录这两项适配的完整 diff。先验证无行为修改的基线，再加入诊断标记；两者使用同一套依赖和构建步骤。命令形态：

```bash
git init <run>/source/Hangfire
git -C <run>/source/Hangfire fetch --depth=1 https://github.com/HangfireIO/Hangfire.git 51647c20e36ab010f7dfc4fa8b1ed666070d7f4f
git -C <run>/source/Hangfire checkout --detach FETCH_HEAD
dotnet restore <run>/source/Hangfire/src/Hangfire.Core/Hangfire.Core.csproj --locked-mode --packages <run>/packages --source <run>/feed
dotnet build <run>/source/Hangfire/src/Hangfire.Core/Hangfire.Core.csproj -c Release -f netstandard2.0 --no-restore --warnaserror
dotnet <run>/tools/ILRepack.exe /out:<run>/merged/Hangfire.Core.dll /target:library /internalize <core-output>/Hangfire.Core.dll <core-output>/Cronos.dll
```

ILRepack 使用本批已核对的 `2.0.27` archive，其 runtimeconfig 声明 `.NET 6.0` 与 `rollForward=major`；仅尝试已有 runtime，不安装 .NET 6、PowerShell、Mono 或 Windows 工具链。如果实际运行不兼容，停止并记录，不临时改用其他版本。

### 3.2 真实调用与资源边界

- 使用实际 API publish 的隔离副本，分别放入原包 Core、源码重建基线 Core、带诊断标记 Core。其它 Radish / Hangfire / SQLite / 第三方 DLL 逐文件哈希不变；不改正式 bin、publish 或 NuGet 缓存。
- 在临时测试入口中调用真实 `ApiRecurringJobRegistration.Register`，核对 16 项启用注册的 ID、表达式、时区和入库结果；诊断标记设在实际 Core 注册 / 入队路径，验证修改库确实被调用。
- 使用现有 `Hangfire.Storage.SQLite 0.4.2`，只创建本批临时 SQLite 文件。清除注册的 Radish 周期任务后，仅在专用队列启动一个进程内 Hangfire worker，执行无业务依赖的探针任务，验证入队、执行与成功状态。不得执行真实清理 / 商城 / Outbox 业务任务。
- 不启动 API / Gateway / Auth 宿主，不监听端口，不使用 Docker 或外部数据库，不接生产配置。worker 与测试子进程设有超时，并在 `finally` 中释放；清理本批数据库与临时进程，保留可审阅日志、patch 和哈希证据。
- 原包、重建基线与修改库三组业务观测一致，且修改组诊断标记存在，才形成这个目标 / provider 的替换证据。失败不隐藏，不能据此宣称其他平台 / provider 已验收。

### 3.3 SQLite 基线必须按实际产物记录

本次发现测试目录 `osx-arm64` SQLite 为 `3.50.3`，API publish 为 `3.50.4`；来源分别是 `SQLitePCLRaw.lib.e_sqlite3 3.50.3` 与 `SourceGear.sqlite3 3.50.4.5`。两条原生资产链同时存在，普通 publish 的 deps 冲突索引与实际复制文件也不能混为一谈。

此试验以实际 publish 的 SHA256 和 `sqlite3_libversion()` 为基线，不能继承“发布也固定为 3.50.3”的假设。统一 SQLite 原生依赖、修正 build / publish 一致性属于独立依赖方案；本批试验授权不包含该修复。

### 3.4 首次构建失败与已批准调整

13 个包的内容 hash、实际 restore 图和隔离缓存路径均已核对。原方案第一次 `--warnaserror` 构建在 NetAnalyzers 包的 `_ReportUpgradeNetAnalyzersNuGetWarning` 失败：该 target 直接比较分析器程序集版本，与 `AnalysisLevel` 无关；现有包为 8.0.0，SDK 自带为 10.0.300。没有设置提示中的 `_SkipUpgradeNetAnalyzersNuGetWarning`，也没有使用失败构建产生的 DLL 作为通过产物。

项目所有者随后单独批准只调整临时 Hangfire 源码：去掉 `src/Directory.Build.props` 的 `Microsoft.CodeAnalysis.NetAnalyzers 8.0.0` 引用，并从派生锁文件移除对应一项，使用现有 SDK 自带分析器。其余 12 个包、`EnableNETAnalyzers=true`、`AnalysisLevel=latest`、`AnalysisMode=Recommended`、既有 `NoWarn` 和 `--warnaserror` 保持原样。不安装新 SDK / 包，不关闭分析器或审计；新分析器若报告其他错误，继续按原停止线处理。

该调整改变试验的分析器来源和 restore 输入，已在取得单独批准后应用；保留的 patch 位于 `/private/tmp/radish-hangfire-replacement-5bb6rlfu/evidence/proposed-sdk-analyzers.patch`。该改动未回写 Radish 工程；实际恢复为 12 个包，基线与完整双标记修改库严格构建均为 0 警告 / 0 错误，不代表重建结果与上游原包逐字节一致。

## 4. 仍待单独处理的发行工作

- PostgreSql `1.20.12` 对应仓库未提供 lockfile，GitVersion 还依赖完整历史；其 CI 的 `9.0.x` 也未固定 SDK patch。现有 5 个直接引用不是完整构建闭包，需另行冻结。
- 22 个 Razor 行映射在实际 PDB 中没有 checksum；源码与已生成 C# 都已归档，但尚未复现 Windows Razor 再生成。
- 审阅包包含来源档案及证据，不等于已经完成可以对外提供的完整 Corresponding Source。正式交付还要核对构建输入完整性、原生 / Web 内嵌材料、许可、源码获取入口与持续可用性。
- Core 替换试验不关闭 NoDrive / SystemCommon 询证、SNI 条款、全生态材料或其他平台验证，不触发发布或部署。

## 5. 2026-10-31 留存复核与接续范围

本次仅只读核对已记录路径，结果见[准备快照](../records/postgres-native-readiness-2026-10-31.json)。旧审阅包路径仍有少量文件，但在其记录目录未找到 `hangfire-source-input-review-2026-10-26.zip`（原记录 SHA256 `55e4bba119a3c1a975935425ff5a9c7438c55388e0fc50dab95d88c963b87fe6`）；旧 replacement 目录存在，feed / downloads 为空。没有扩大到全盘搜索，也不据此宣称文件已从所有位置丢失。目录存在不能证明完整材料仍可取得，历史试验结果也不改写为失败。

下一源码材料批次应先重取并校验已冻结的 Hangfire / Cronos / PostgreSql commit、包与工具，生成完整文件索引，归档到明确的长期保存位置；临时目录只用于执行。随后分别冻结 Cronos 重建、NetCore / AspNetCore / PostgreSql 的完整构建闭包，以及 Windows Razor 再生成所需工具和命令；不得把 Core 的 12 包试验闭包扩写成全组件闭包。源码持续获取入口与对外提供方式独立确认。

本机 [PG 回归方案](./postgres-regression-execution-plan.md)使用当前正式包，不依赖本轮源码重建；其获批的四组 PostgreSQL worker 已通过并清理，见[执行记录](../records/postgres-regression-2026-10-31.md)。[剩余原生平台方案](./sqlite-hosted-native-validation-plan.md)也不将 CI artifact 当作正式源码发行入口。本节留存复核未重新获取上游源码 / 工具、未重建或替换 DLL，不把正式包接库通过计为源码重建完成。
