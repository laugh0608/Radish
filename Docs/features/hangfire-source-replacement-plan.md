# Hangfire 源码材料与 Core 替换验证方案

> 2026-10-26：源码 / PDB 核对和 Cronos 许可补充已完成，见[本批记录](../records/hangfire-source-inputs-2026-10-26.md)。第 3 节为待批准的下一批试验，不改变 Radish 正式依赖或默认构建；完整发行审核仍未关闭。

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

## 3. 待批准的精确试验范围

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

## 4. 仍待单独处理的发行工作

- PostgreSql `1.20.12` 对应仓库未提供 lockfile，GitVersion 还依赖完整历史；其 CI 的 `9.0.x` 也未固定 SDK patch。现有 5 个直接引用不是完整构建闭包，需另行冻结。
- 22 个 Razor 行映射在实际 PDB 中没有 checksum；源码与已生成 C# 都已归档，但尚未复现 Windows Razor 再生成。
- 审阅包包含来源档案及证据，不等于已经完成可以对外提供的完整 Corresponding Source。正式交付还要核对构建输入完整性、原生 / Web 内嵌材料、许可、源码获取入口与持续可用性。
- Core 替换试验不关闭 NoDrive / SystemCommon 询证、SNI 条款、全生态材料或其他平台验证，不触发发布或部署。
