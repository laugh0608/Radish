# Hangfire Windows Razor 托管验证方案

> 2026-10-31（Asia/Shanghai），**本地实施已批准并完成，Windows 实际库存 / 验证待单独授权**。接续[本机重建](../records/hangfire-rebuild-2026-10-31.md)；精确源码、11 项包 / hash、SDK、23 对模板与生成文件、包内 24 DLL 及官方 runner 清单快照见[准备 JSON](../records/hangfire-windows-razor-readiness-2026-10-31.json)。实际接线与本地验证见[实施记录](../records/hangfire-windows-razor-local-implementation-2026-10-31.md)；未执行 Razor、安装新 SDK / 包、push 或触发远端运行。

## 1. 目标与授权分段

证明固定 Hangfire 源码中的 23 个 Dashboard 模板确实由固定 RazorGenerator 再生成，并进入 Core 编译及真实片段渲染；明确 Git LF、Windows CRLF、既有生成 C#、PE / PDB 和实际输出之间的关系。

本次确认已覆盖并完成**本地实施与验证**：新增 Windows 专题编排、只读库存 / 比较 / 渲染工具、限定手动 CI job、契约检查及文档；不在本机安装 Windows 工具、不执行 Windows 任务、不 push、不触发 runner。之后单独批准：

1. 将指定提交 push 到 `dev`，不自动执行其他工作流。
2. 手动运行 `inventory`，采集实际 VM / Full MSBuild 精确版本和 hash；不执行上游 task。
3. 审阅库存，冻结期望 image / VS / MSBuild 后，另行确认指定提交的 `verify` 及证据上传。

官方 runner 清单不是实际 VM 执行证据。本机成功也不替代本批 Windows 验证；不把本批解释为原包字节重现、正式对应源码入口或完整发行审查已经完成。

## 2. 已核对工具链与缺口

| 工具 | 固定输入或官方快照 | 使用与停止线 |
| --- | --- | --- |
| Runner | `windows-2025`，x64；清单 Image `20261021.299.1` / OS `10.0.26100 Build 33650` | label 无法锁定 image revision；实际 `ImageVersion` 漂移先采集，不直接执行上游 task |
| VS 2022 | Enterprise `17.14.37806.7` | 用 `vswhere 3.1.7` 找实际实例；不安装 / repair VS，不切到 Framework 目录里的旧 MSBuild |
| Full MSBuild | VS `MSBuild\Current\Bin\amd64\MSBuild.exe` | 官方清单未列精确 executable patch；inventory 必须记录 `/version`、FileVersion、SHA256。verify 要求与已审阅值完全匹配 |
| .NET Framework | 清单提供 `4.8 / 4.8.1` | 运行 RazorGenerator 的 CLR 边界；记录 Release registry 值、任务进程路径 / 位数；不修改 registry 或全局绑定策略 |
| PowerShell | 清单 `7.6.7` | 仅 PowerShell 子进程环境变量；记录实际版本，不修改机器 ExecutionPolicy / profile |
| 私有 .NET SDK | `10.0.403`，runtime `10.0.14`，win-x64 ZIP | 官方 URL / SHA512 已取得；本轮未下载 / 安装。只解压到任务目录，不用全局 installer 或浮动 `10.0.x` |
| RazorGenerator.MsBuild | NuGet `2.5.0`，archive SHA256 `1f2c549056cff967cdf4d1c92f9fa78e7f5a2d292a15d5a427bf76caa542c064` | 已静态读取 nuspec、targets 和全部 24 DLL 的 PE 元数据；不作为正式产品 PackageReference |
| ILRepack | `2.0.27` | 保持本机重建的 Core / Cronos 合并边界 |

依据：[Windows 2025 官方软件清单](https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-Readme.md)、[GitHub runner 规格](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)、[.NET 10 官方发行元数据](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)、[NuGet RazorGenerator 2.5.0](https://www.nuget.org/packages/RazorGenerator.MsBuild/2.5.0)。软件清单文件 Git blob 为 `84e8dbd2b71809e9315cf0085ce5c3f37c0ffa75`，本地内容 hash 进入 JSON。

SDK 精确获取输入：

```text
https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-win-x64.zip
SHA512 7a114c318fa7f1d9822991b534b2814d0e78386ceda816b371f5141acefe741d87ccb606ede06cadac8f9ac5070e32b708a67bbd1619ff5e5a7858a27874ef48
```

RazorGenerator 的包版本为 2.5.0，内部 Core / MsBuild 程序集版本仍为 **2.0.0.0**，不能据此误判包版本。包内 v1 / v2 / v3 引擎及 MVC / WebPages 都有精确 hash；例如 MVC 程序集为 3.0.0.0 / 4.0.0.0 / 5.2.3.0。nuspec 声明无依赖不代表没有这些内嵌组件，也不代表审计覆盖它们。实际载入哪套引擎须由任务内程序集快照证明，不预设 v3、不从机上 GAC 自动替代。MsBuild task 引用 `Microsoft.Build.Utilities.v4.0 / Microsoft.Build.Framework 4.0.0.0`，五个 RazorGenerator 程序集均引用 mscorlib 4.0.0.0；库存还须核对这些 CLR 4 / MSBuild 兼容绑定，不能仅看到 VS 版本就推定旧 task 可运行。当前 master task 源码仅作辅助阅读，未证明其与 2.5.0 包绑定。

本机无 pwsh / Windows Full MSBuild。准备期复用了仓库自有 PE 读取器，以现有 SDK 10.0.300 在临时目录严格构建，只读取包元数据，未加载执行上游 DLL；该临时读取器与 Git 对象库已删除。

## 3. 精确输入及范围

固定 Radish 准备基线 `762f5fbd227c46232539ee21ff93ad14a8225af4`；实施完成后再冻结新的完整提交供未来远端执行，不在方案里猜未来 SHA。

| 源码 | Commit | Tree |
| --- | --- | --- |
| Hangfire 1.8.14 | `51647c20e36ab010f7dfc4fa8b1ed666070d7f4f` | `b5ee5d721d64442d4bff4d9592bd05e9f5fda421` |
| Cronos 0.8.3 | `9d6e349e58fda615e47be10c3d9a539e9ce85073` | `d6fe40daf2b07b26f837fa36fb47002ca38ca9c7` |

源码从官方仓库固定 commit 获取，核对 tree、607 / 38 blobs 和文件模式。浅获取可用于本批固定 commit 的完整文件树核对，但不冒充完整历史 bundle。准备已从上批 bundles 静态读取相关文件；CI 不依赖本机未提交的 artifacts，直接按冻结值获取官方输入。

本批只需要上一批 42 项中的 Core / Cronos / 合并工具 10 项，加 RazorGenerator 1 项，**合计 11 项**：

| 包 | 版本 | 包 | 版本 |
| --- | --- | --- | --- |
| Cronos | 0.8.3 | ILRepack | 2.0.27 |
| LibLog | 1.5.0 | Microsoft.CSharp | 4.4.0 |
| Microsoft.NETCore.Platforms | 1.1.0 | NETStandard.Library | 2.0.3 |
| MoreLinq.Source.MoreEnumerable.Pairwise | 1.0.1 | Newtonsoft.Json | 13.0.4 |
| StackTraceFormatter.Source | 1.1.0 | StackTraceParser.Source | 1.3.0 |
| RazorGenerator.MsBuild | 2.5.0 | | |

每项 URL、archive SHA256、NuGet contentHash 见 JSON；先核对完整 feed 再允许 restore。RazorGenerator ZIP 只展开到每份 Hangfire 根下 `packages\RazorGenerator.MsBuild.2.5.0`，供原始 `src\Hangfire.Core\Razor.build` 的 `..\..\packages\...` import 使用。其他包走私有 NuGet cache，不往仓库根或全局 cache 安装。

不运行上游 `build.bat` / psake / NuGet.exe，因此不引入 Hangfire.Build 0.4.4、psake 4.4.1 或签名 / pack / 发布链。沿用已批准的所选 netstandard2.0、SDK 分析器 / SourceLink、Core Newtonsoft 13.0.4、JsonStats 等价适配；不扩大到 NetCore、AspNetCore 或 PostgreSql 重建，也不重跑本机 18 组应用矩阵。正式 219 项图、默认 SDK 和 ThirdParty 不改。

## 4. 模板与生成产物关系

静态核对 **23 个 `.cshtml` ↔ 23 个同路径 `.cshtml.cs`**，包括 15 个完整页面与 8 个局部模板。模板首行声明 Template / Internal / GeneratePrettyNames，部分还 TrimLeadingUnderscores；输入 BOM 保留。`_ErrorAlert.cshtml` 生成内部 `Hangfire.Dashboard.Pages.ErrorAlert`，其 Execute 只写固定 HTML，适合作为无数据库渲染与标记证明。

原 Git 模板为 LF，既有生成 C# 的字符串包含 CRLF。`.gitattributes` 没有给 cshtml 固定 eol，只标记 text；本批不能把 Windows 默认 checkout 结果当成 Git blob 原字节。初始 checkout 明确 `core.autocrlf=false` 并按文件 hash 核对，再建立：

| 组 | 模板 / 生成 C# | 目的 |
| --- | --- | --- |
| `checked-in` | 原 Git LF 模板 + 既有生成 C# | 保留原基线，禁止执行再生成来覆盖它 |
| `regenerated-lf` | 原 Git LF 模板，先删除全部 23 个既有生成文件再生成 | 核对原 Git 字节输入的结果 |
| `regenerated-crlf` | 仅将 23 个模板的 LF 确定性转换为 CRLF，保留 BOM；删除 23 个生成文件再生成 | 对照原 Windows 生成习惯；每项派生输入 hash 已列入清单 |
| `marked-crlf` | 在 CRLF `_ErrorAlert.cshtml` 的固定 HTML 中加入 `RADISH_RAZOR_SOURCE_PROBE_20261031` 后再生成 | 证明修改模板确实进入编译与渲染；不直接手改生成 C# |

每组都必须产出精确 23 文件，无额外文件、漏项或旧文件。记录模板 hash → task / 引擎 hash → 生成 C# hash → 所编译 Core / PDB hash → 冷进程实际加载路径 / hash → HTML hash / 标记。

对照采用固定 SDK 自带 Roslyn 的 C# token / ValueText 比较，先折叠值不变的字符串常量拼接以处理 CodeDom 拆行，保留字符串、标识符、数值和访问修饰符语义；仅将注释、外层文本换行和已核对的 `#line` 路径归类为生成元数据。不用删除空白的正则掩盖 C# 差异。要求 CRLF 组与既有 C# 的有效 token 一致；LF / CRLF 组只允许明确列出的字符串换行差异，未知 token / 资源 / 接口差异即停。若两种输入均不能解释既有生成结果，暂停相应结论，不放宽成“编译过就通过”。

`marked-crlf` 只允许一个模板增加固定标记；生成 token 差异必须局限于对应 HTML 字符串。两次独立生成的同组结果要求字节一致；路径差异若影响生成内容须先定位，不自动规范化后宣称字节一致。

## 5. 命令与实施文件

已新增 `Scripts/hangfire/windows/`：PowerShell 阶段入口、独立输入契约、只读库存 / 包验证、23 对生成检查、SDK Roslyn token / PE / PDB 比较器、无 PackageReference 的片段渲染探针及清理 / 契约测试。既有 macOS 工具保留原平台边界，不把它的 Task 初始化伪装成支持 Windows。

以下是已实现入口的**Windows 命令**，本轮未执行。任务根固定在 `$env:RUNNER_TEMP`，所有权包含 run ID / attempt / commit，禁止使用共享根目录：

```powershell
$TaskId = "radish-hangfire-razor-$env:GITHUB_RUN_ID-$env:GITHUB_RUN_ATTEMPT"
$TaskRoot = Join-Path $env:RUNNER_TEMP $TaskId
pwsh -NoProfile -File Scripts/hangfire/windows/Invoke-Validation.ps1 `
  -Phase Inventory -TaskRoot $TaskRoot -ExpectedCommit $ApprovedSha
```

库存使用已安装 vswhere，不通过 PATH 猜 MSBuild：

```powershell
$VsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
& $VsWhere -products '*' -version '[17.0,18.0)' `
  -requires Microsoft.Component.MSBuild -format json -utf8
# 从结果取唯一满足要求的实例，明确选择 amd64\MSBuild.exe；缺失 / 多候选即停。
& $FullMsbuild /nologo /version
Get-FileHash -Algorithm SHA256 $FullMsbuild
```

只有未来 `verify` 获批且期望 image、VS、MSBuild 版本及 MSBuild SHA256 都非空并与实际一致，才进入获取 / 执行：

```powershell
pwsh -NoProfile -File Scripts/hangfire/windows/Invoke-Validation.ps1 `
  -Phase Verify -TaskRoot $TaskRoot -ExpectedCommit $ApprovedSha -ExpectedImageVersion $ReviewedImage `
  -ExpectedVsVersion $ReviewedVs -ExpectedMsbuildVersion $ReviewedMsbuild `
  -ExpectedMsbuildSha256 $ReviewedMsbuildSha256
```

获取后只对子进程设置私有 DOTNET_ROOT / PATH、CLI home、TMP、NuGet / HTTP cache，禁节点复用和共享编译，并设置 DOTNET_GENERATE_ASPNET_CERTIFICATE=false / 遥测关闭，不改 HOME / 用户 profile 或生成证书。SDK ZIP 校验 SHA512 后解压；record `--info`，global.json 固定 10.0.403 / rollForward disable。11 项包逐个验证后建立 file feed；auditSources 单列 nuget.org，保留审计与 warnaserror。

Razor 原项目不 import SDK；在 Core 同目录生成自有 `Razor.Verification.proj`，仅 import 原始 `Razor.build`，自有 task C# 放在任务 tools 目录，不能落入 Core 默认 Compile glob；附加生成前后文件断言与 `AfterTargets=PrecompileRazorFiles` 的只读程序集快照 task。捕获同一 MSBuild AppDomain 实际载入的 Razor / MVC / WebPages 路径、版本及 hash；若无法取得引擎载入证据，不用包文件清单代替。禁止修改 / 替换包 DLL、开启全局 Fusion logging 或安装诊断工具。

```powershell
# $Core 为某一隔离组的 src\Hangfire.Core；执行前核实 23 个输出已不存在。
Push-Location $Core
try {
  & $FullMsbuild .\Razor.Verification.proj /t:PrecompileRazorFiles `
    /nologo /m:1 /nr:false /v:diagnostic "/bl:$Evidence\razor.binlog;ProjectImports=None"
  if ($LASTEXITCODE -ne 0) { throw 'Razor generation failed' }
} finally { Pop-Location }
```

不能调用 Core 的可选 GenerateRazorClasses target 后仅看退出码：原 target 在找不到 Full MSBuild 时可能只 warning。这里直接调用明确的 Full MSBuild 与原 Razor import，缺包 / task 不支持必须失败，不退回既有生成文件。

各独立目录分别构建一次 Cronos，再构建四组 Core，均为 Release / netstandard2.0：

```powershell
& $Dotnet restore $Project --configfile $NugetConfig --use-lock-file `
  --force-evaluate --warnaserror -p:NuGetAuditMode=all
# 校验 assets / downloads 只属于 11 项允许清单，保存派生锁。
& $Dotnet restore $Project --configfile $NugetConfig --locked-mode `
  --warnaserror -p:NuGetAuditMode=all
& $Dotnet build $Project -c Release --no-restore --warnaserror `
  -p:UseSharedCompilation=false
& $Dotnet "$IlRepack\tools\ILRepack.exe" "/out:$Merged\Hangfire.Core.dll" `
  "/lib:$CoreBin" /target:library /internalize $CoreDll $CronosDll
```

输出文件仍为 Hangfire.Core.dll，合并使用本次源码 Cronos。路径映射延续独立的 `/_/sources/hangfire/`、`/_/sources/cronos/` 和包根；23 个再生成 C#、JsonStats 与标记修改源码嵌入 PDB，不再尝试无差别嵌入所有 Compile / cshtml。

## 6. 验证矩阵与结论门禁

| 验证 | 数量 / 要求 |
| --- | --- |
| 独立输入 / 缓存 | 两个全新根 A / B；共享封存只读 feed，各自全新源码、obj / bin、NuGet 展开 cache |
| 模板生成 | 每根 LF、CRLF、marked 三组各 23 个输出；每次生成前删输出，不能靠文件时间戳“已最新”通过 |
| 严格编译 | 每根 Cronos 1 次、Core 四组各 1 次，合计 2 + 8 次；restore / locked / audit / warning 门禁不放宽 |
| 原包对照 | Core 公开身份 / 接口 / 参数默认值 / 资源与已封存原包元数据一致；不要求与原包字节相同 |
| 组间对照 | checked-in 与 CRLF 的有效生成 token 相同；LF 差异只能是已解释的 HTML 换行；标记只能影响对应模板字符串 |
| 重放 | A / B 同组生成 C#、DLL / MVID / 资源 / 方法 IL 字节一致；未知差异即停，跨组不混称字节一致 |
| 冷进程渲染 | 四组 × A / B × Debug / Release 探针，共 16 组；Core 都是上行 Release 产物，不称作 Core 双配置构建 |
| 实际路径 | 探针只从本 case 目录载入预期 Core / Newtonsoft，记录 hash；ErrorAlert.ToString 实际执行生成类 |
| 标记证明 | 仅 marked 输出有固定 marker，其他三组不得有；普通 HTML、id / role / class 与基线对照，LF 换行差异单列 |
| 反向拒绝 | 缺 / 多模板、保留旧生成文件、改包 / 引擎、错误 Full MSBuild / SDK、漏产物、错 DLL、缺标记、越界清理均须失败 |

渲染探针通过非公开构造创建内部 ErrorAlert，调用公开 RazorPage.ToString，不创建 JobStorage、DI 宿主或监听器。探针是无 PackageReference 的 net10.0 命令行程序，显式引用待测 Core / Newtonsoft，使用私有 runtime；不伪造一个假 Dashboard 服务或扩大为 API Smoke。

PDB 逐文件核对生成 C# 的实际 checksum、嵌入字节、SourceLink 路径 / commit。原 RazorGenerator 不提供 `.cshtml` checksum，上批源码基线 PDB 的 22 个无 checksum 文档及无对应 Razor 文档的 ErrorAlert 均单列；用 23 对生成来源清单和标记证据补充关系，不写入伪 checksum，也不把零诊断或缺报告写成通过。完整 Dashboard / worker 行为继续引用上批 18 组证据，不能称为本批重新执行。

## 7. CI 接线与远端边界

已在本地复用默认分支原有的 `.github/workflows/native-images.yml`，增加 `validation_scope=hangfire-razor` 及 `razor_mode=inventory|verify`（默认 inventory），只新增一个 `windows-2025` job，超时 60 分钟。门禁为 `workflow_dispatch && validation_scope == hangfire-razor`，禁止 workflow_call / push / PR 顺带执行；原 native / SQLite 条件、默认选项与行为保持不变。

增加 Razor 专用 `expected_head_sha` 输入，inventory / verify 都要求它非空且与 `github.sha` 一致，再 checkout 当前 run SHA；不只依赖可移动的 dev 名称。新 job 只用 `contents: read`，checkout 禁 persist-credentials；沿用已核对的 checkout `fbc6f3992d24b796d5a048ff273f7fcc4a7b6c09` 和 upload-artifact `ea165f8d65b6e75b540449e92b4886f43607fa02`。不需要 setup-msbuild、Node、npm、Rust、Docker、数据库或任何 secrets。verify 的四个库存期望值由后续审阅填入，空值绝不默认开始上游执行。

[GitHub 手动运行规则](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow)要求 workflow 存在于默认分支，再用 ref 选择其他分支；因此不新建只能通过修改 master 才可发现的孤立工作流。未来实际触发前仍核实远端默认分支 / workflow 存在、dev 已推送提交与输入 schema，不从本地 origin/master 快照推定当时远端状态，不为执行试验自动合并 master。

下列只是未来授权后的命令形态，**本轮及仅批准本地实施后均不执行**：

```bash
gh workflow run native-images.yml --repo laugh0608/Radish --ref dev \
  -f validation_scope=hangfire-razor -f razor_mode=inventory \
  -f expected_head_sha="$hfApprovedSha"
# hfApprovedSha 须为另行获批的完整 40 位提交；执行前后核对 dev HEAD，不在运行期间移动 ref。
# verify 须另行授权、传入已审阅的 image / VS / MSBuild 版本及 MSBuild SHA256。
```

远端时记录 workflow_sha / head_sha / run_id / attempt，取得证据后再次核对。Artifact 名 `hangfire-razor-<run_id>-<attempt>`，保留 7 天，上传 manifest / hash、命令日志 / binlog、库存、23 对清单、生成 C# / 差异、必要 LGPL / Cronos 许可和比较 / 渲染结果。原 SDK、nupkg、完整包 cache、产品 DLL / PDB、业务源码及任何凭据不上传；这份验证材料不成为正式源码发行入口。证据上传本身也是远端写入，须纳入之后的远端执行确认范围。

## 8. 运行影响、回退、清理与停止线

- **端口：无；数据服务：无。** 不启动 API、Gateway、Auth、PG、SQLite、Redis / Valkey，不执行数据库迁移或 worker，不触达生产、证书 / 用户 Keychain 或既有 Windows 服务。
- 所有新 SDK、11 项 feed、源码与八组产物均在任务私有根；不删除 / 覆写全局 VS、SDK、NuGet 或其他任务目录。预检至少 8 GiB 剩余磁盘；一次生成 5 分钟、工程 restore / build 各 10 分钟、单探针 30 秒、job 60 分钟，到期保留诊断并终止本任务进程树，不静默换工具 / 版本。
- 命令启动前保存所有权与 PID / CreationTime；finally / CI always 阶段只按这些身份终止仍存活的本任务子进程，不用全局 taskkill / dotnet build-server shutdown。helper、MSBuild、编译器和 probe 都禁复用，清理前核对进程实际路径和时间，防 PID 复用误杀。
- 先生成独立证据目录与 SHA256 manifest，保存成功 / 失败分类及缺失项，再删除 owned working 子目录中的 SDK、cache、源码、生成文件、bin / obj。汇总结果不把被删文件假写为仍可下载。
- inventory / verify / 失败都执行同一清理；清理失败会令 job 失败，完整证据仍按安全白名单上传并标明失败，不因缺成功报告跳过失败留痕。upload 步骤后由 runner 销毁 VM 清除最后证据目录，不声称工作流可以在上传前主动删除待上传证据。
- 回退只撤销新增专题工具与对应 job / scope input，不回写产品 DLL、不回退正式依赖。不 reset / force push；已取得验证记录保留。本轮准备证据 `artifacts/source-readiness/hangfire-windows-razor-20261031` 继续保留。
- 未审阅的 image / MSBuild、缺 Full Framework、package / source hash 漂移、旧输出残留、引擎从不明路径载入、审计 / 分析器失败、未知 C# token / IL / 资源差异、PDB 映射不符、标记缺失或清理越界均停止受影响阶段，不加 NoWarn、换包、改 renderer 或补默认成功结果。

NoDrive 询证、SNI、Valkey、SQLite 其他平台矩阵、正式源码托管 / 发行、master PR、tag、Release、发布和部署均不在本次本地实施确认范围。
