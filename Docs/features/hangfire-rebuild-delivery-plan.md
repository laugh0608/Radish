# Hangfire 重建与本地交付材料实施方案

> 2026-10-31（Asia/Shanghai），**已批准并实施，本机验证与清理完成**。接续[旧 Core 试验](./hangfire-source-replacement-plan.md)，本页维护实施边界；准备时输入见[准备清单](../records/hangfire-rebuild-readiness-2026-10-31.json)，实际构建、18 组最终调用、五目标字节一致重放及未关闭项见[实施记录](../records/hangfire-rebuild-2026-10-31.md)。未 push、远端执行、发布或部署。

## 1. 本批目标与完成边界

建立可从明确保存的输入重放的本机重建工具链，覆盖实际交付的 Cronos 与四个 Hangfire DLL；在隔离 API 副本证明重建库及 Core 诊断修改库进入真实 DI、Dashboard、注册、任务执行和重试路径。

本机完成标准为输入封存、严格编译、接口 / 资源核对、18 组真实调用、一次新的干净目录重放及本地归档。与上游原包字节一致、Windows Razor 再生成、其他原生平台、正式源码获取入口和对外发行分别记录，不因本机成功宣称完整对应源码交付已经成立。

## 2. 已核实输入与新发现

| 项目 | 精确输入 | 核实程度 |
| --- | --- | --- |
| Radish 基线 | `9bc15795`，dev，业务依赖 219 项 | 当前提交；本轮准备开始工作区干净 |
| Hangfire 三组件 | `1.8.14`，`51647c20e36ab010f7dfc4fa8b1ed666070d7f4f` | Git tree 607 blobs，原包 SourceLink / PDB 的旧补证保留 |
| Cronos | `0.8.3`，`9d6e349e58fda615e47be10c3d9a539e9ce85073` | Git tree 38 blobs，原 7 个源码 checksum 旧证据保留 |
| PostgreSql | `1.20.12`，`a5a728233ac8feb447695acb01f607bd848b00f7` | Git tree 108 blobs，`refs/tags/1.20.12` 直接指向该 commit；原 DLL 无 SourceLink 绑定证据 |
| 当前本机 | SDK `10.0.300` / runtime `10.0.8`、Node `24.16.0` | 只读核对；当前没有 pwsh / Full Windows MSBuild |
| 本批私有 SDK | `10.0.403` / runtime `10.0.14`，osx-arm64 | 官方 archive / SHA512 核对后临时安装，验证结束已删除 |
| 合并工具 | ILRepack `2.0.27` | 静态审阅 archive SHA256 与旧记录一致 |
| 版本工具 | GitVersion.MsBuild `5.11.1` | archive / props / targets 已静态审阅，包含 osx-arm64 libgit2；私有 runtime 下执行，实际版本 1.20.12 已核对 |

准备期只读取 tree metadata 与 34 个选定文件；实施已取得三个完整 Git bundles，并核对 753 个 blob 与准备索引一致。准备清单中的 tree 响应 sha 实为查询 commit，实际 tree 值已在工具契约更正并留痕；未更换 commit。原 Core 试验目录不作为本批输入。

### 2.1 构建依赖与审计适配

旧清单的 27 项不是完整构建闭包。SDK metadata / 当前安装盘点还识别出三个 reference pack：`Microsoft.NETCore.App.Ref 3.0.0`、`Microsoft.AspNetCore.App.Ref 3.0.1`、`NETStandard.Library.Ref 2.1.0`，均已静态读取 archive 并记录 SHA256；这里只需要编译引用，不安装 .NET Core 3.0 runtime。

NuGet 官方漏洞快照在原始 / 静态候选图命中以下项，已批准在**隔离重建副本**调整：

| 原输入 | 批准适配 | 影响与约束 |
| --- | --- | --- |
| NetAnalyzers `8.0.0` | 移除上游显式引用，使用 SDK `10.0.403` 内置分析器 | 继续 EnableNETAnalyzers 和上游 AnalysisMode；不放宽诊断 |
| SourceLink.GitHub / Common / Build.Tasks.Git `8.0.0` | 移除上游 SourceLink 显式引用，使用固定 SDK 内置组件 | Git 8.0.0 有 Moderate advisory；SourceLink 不被禁用，须核对实际任务身份、PDB 和源码映射 |
| Newtonsoft.Json `11.0.1` | Core 与私有 PostgreSql 工程固定 `[13.0.4]` | 原版有 High advisory；Core 所选目标的旧 `NU1903` 包级豁免一并移除 |
| PostgreSql 静态候选的 System.Text.Json `6.0.0` | 私有插件工程显式固定 `[6.0.10]` | 原版有 High advisory；保留 netstandard2.0 及 Npgsql `6.0.11` 的上游编译边界 |

依据：[Microsoft SourceLink advisory](https://github.com/dotnet/sourcelink/security/advisories/GHSA-23fw-v26w-5fgq)、[System.Text.Json advisory](https://github.com/dotnet/runtime/security/advisories/GHSA-8g4q-xg66-9fp4)、[NuGet 官方漏洞索引](https://api.nuget.org/v3/vulnerabilities/index.json)、[SDK 内置 SourceLink 说明](https://github.com/dotnet/sourcelink)。Newtonsoft 的命中范围与公告 URL 保存在准备清单。

不直接把三个 SourceLink PackageReference 升为 10.x：其已审阅 nuspec 的依赖组为 net472 / net8.0，与旧 TFM 组合的 restore 兼容性尚未实测，不仅凭版本号推定可用或不可用。本方案选择官方支持的 SDK 内置组件并保留运行库 TFM。私有 SDK archive 单独校验，运行前核对其 reference pack 定义与实际 SourceLink / 编译器文件；与清单不同即停止。

**批准私有包 / 版本共 42 项**，覆盖三组件 / Cronos、插件、ILRepack 和三个 ref pack，逐项见准备 JSON。准备期 PostgreSql 的 22 项来自 nuspec / netstandard2.0 下界与显式适配；实施已在封存 feed 生成 / 核对派生锁。五工程 assets / ref pack 联合为 41 项，另 1 项为合并工具 ILRepack；真实 restore 仍限制在这 42 项内。新增项或不同版本即停，不临时扩包。

当前快照对拟定 42 项的声明版本范围匹配为零；这不等于已完成 NuGet audit，也不覆盖 GitVersion 内嵌原生库或 SDK 的全部安全结论。实际执行保留 NuGet 审计，出现问题不新增 NoWarn / WarningsNotAsErrors / 审计禁用或自动升级。

正式 Radish csproj、NuGet.Config、219 项应用图、默认 SDK 策略及 ThirdParty 分发文件均不改。上面的 6.0.11 / 6.0.10 是插件重建输入，注入测试仍用正式应用的 Npgsql `10.0.0` 与现有 deps。

### 2.2 三份可审阅补丁

准备证据中封存的三个 patch 已按 hash 核对并应用到私有副本，原始输入仍单独留存：

- Hangfire：Core → netstandard2.0、NetCore → netstandard2.1、AspNetCore → netcoreapp3.0；去掉旧分析器 / SourceLink 显式引用，Core JSON 固定 13.0.4。
- Cronos：netstandard2.0，去掉旧分析器 / SourceLink 显式引用。保留其 `AnalysisMode=All`，失败时不改成较宽模式。
- PostgreSql：保留 GitVersion、Core 1.8.0、Npgsql 6.0.11 等上游直接引用；加两项 JSON 固定版本，仅将 GeneratePackageOnBuild 改为 false，避免私有 DLL 编译自动产包。

原文件 BOM / 换行与字节 hash 先留存，应用补丁时按源文件格式处理；不得用重编码掩盖源码差异。上游 .gitattributes 会将 csproj 导出为 CRLF，因此 Git blob hash 与 checkout / archive 文件 hash 分别记录并按实际属性验证，不要求两者字节相同。单独新增的 Core 诊断补丁只允许内部标记，实施后保存完整 diff 并检查公共接口。

### 2.3 执行期确认的源侧与调试适配

- JsonStats 的弃用 `CamelCaseText` 改为 `CamelCaseNamingStrategy`；枚举 / EnumMember 及真实 stats 路径证明等价。
- NetCore 被 CA1873 阻断后单独说明并获批：仅在 `ILogger.IsEnabled` 时求值消息，关闭时返回 false；补测启用、关闭、级别查询及异常传播。正式产品不应用该补丁，不加 NoWarn。
- PathMap 与 SourceLink 使用相同虚拟根并按 SourceRoot.Identity 分配仓库路径；修改源码嵌入 PDB。逐文件映射、checksum、嵌入字节和相邻 PDB 身份都作为门禁；修正前的过渡结果另存，不计入最终 DLL 矩阵。

## 3. 工具链实现范围

已新增 `Scripts/hangfire/` 专题工具，分清下列职责，不挂入默认 build / CI / 发布：

1. 机器输入契约：三个 commit / tree、SDK archive hash、42 项允许版本、source patch hash、原 DLL / 程序集身份与平台边界。
2. Node 编排入口：`rebuild.mjs --phase=<acquire|build|verify|archive|cleanup> --task-root=<本批路径>`；处理命令日志、超时、输入封存、阶段结果、所有权与清理，不自动猜版本或失败换原包。
3. 无 PackageReference 的 C# 元数据工具：使用固定 SDK 自带 API 读取 NuGet contentHash、PE / PDB、公共接口与嵌入资源；不以反射加载执行待审阅程序集。
4. 无 PackageReference 的调用探针：引用隔离 API 产物，在真实 storage 下调用 DI / HostedService、内存 Dashboard 和既有 API 注册；保存程序集路径 / hash、状态及诊断标记。
5. 针对输入漂移、锁图、错误 DLL、PDB 映射、越界连接、清理所有权及缺失报告的契约测试；输出本地审阅 catalog / 重放说明。

同一工具链要能在新的干净目录从归档输入再次构建，并记录两次产物 hash / 接口 / 资源差异。若不能字节一致，须定位编译 / 合并元数据或实质差异，不能把调用通过当作字节一致证明；未知 IL / 资源 / 接口差异阻断本批结论。

## 4. 目录、命令与阶段顺序

```bash
hfTask=radish-hangfire-source-20261031-c9e4
hfRun="${TMPDIR%/}/$hfTask"
hfArchive="$PWD/artifacts/source-bundles/$hfTask"
hfDotnet="$hfRun/tools/dotnet/dotnet"
```

上述目录必须全新且带本任务 owner marker；冲突即停。准备证据保留在 `artifacts/source-readiness/hangfire-20261031`，不可作为可丢弃缓存清理。

### 4.1 获取、校验和封存

- 先以 mirror 获取三个未修改的 Git 输入并记录当次完整 refs，验证固定 commit 与 PostgreSql 1.20.12 tag。创建并校验本地 git bundle，后续从 bundle 建工作副本，避免新远端状态改变重放输入；不把浅克隆当作无前置对象的完整 bundle。
- 下载 SDK `10.0.403` 官方 osx-arm64 archive，按准备 JSON 的 SHA512 校验后只解压到 tools/dotnet；不用 installer，不写全局 SDK / shell 配置。
- 仅获取 42 项允许版本的 nupkg，复用已核实的缓存 / 审阅 archive；校验已知 archive SHA256、上游 contentHash 与 NuGet SDK 算出的 contentHash，生成精确文件索引后封存 feed。旧上游锁与适配后的锁分别保存，不把签名 archive SHA512 当 contentHash。
- 原始许可证 / NOTICES、Razor 输入与已生成 C#、Dashboard 资源及上游构建脚本均随源码保存并盘点。上游自带 NuGet.exe、psake、签名 / 发布脚本只归档，不运行。

获取 Git 的命令形态：

```bash
git clone --mirror https://github.com/HangfireIO/Hangfire.git "$hfRun/mirrors/hangfire.git"
git clone --mirror https://github.com/HangfireIO/Cronos.git "$hfRun/mirrors/cronos.git"
git clone --mirror https://github.com/hangfire-postgres/Hangfire.PostgreSql.git "$hfRun/mirrors/postgresql.git"
git -C "$hfRun/mirrors/hangfire.git" bundle create "$hfRun/inputs/hangfire.bundle" --all
git -C "$hfRun/mirrors/cronos.git" bundle create "$hfRun/inputs/cronos.bundle" --all
git -C "$hfRun/mirrors/postgresql.git" bundle create "$hfRun/inputs/postgresql.bundle" --all
```

完整获取与 hash 核对后才允许 eval / restore。保留 source tree 索引并校验所有 blob，拒绝未知 submodule、逃逸路径或丢失文件。历史包 / 脚本只作为输入，不通过上游一键 build / sign / publish 执行额外操作。

### 4.2 私有 SDK 与 source build

只对子进程设置 `DOTNET_ROOT / DOTNET_ROOT_ARM64`、私有工具 PATH、CLI home、TMPDIR、NuGet cache / HTTP cache；不改 HOME。临时 global.json 固定 `10.0.403 / rollForward=disable / allowPrerelease=false`，关闭本批 MSBuild 节点复用和共享编译服务器，记录子进程 PID。构建网络访问只用于明确的 NuGet audit / 输入准备，不把本机进程描述成 OS 级断网沙箱。

source 专属 NuGet.Config 只列封存 file feed，auditSources 另列 nuget.org；不能以本地 feed 没有审计数据为由关闭 audit。先按静态契约生成 / 校验派生锁，再严格编译：

```bash
"$hfDotnet" --info
"$hfDotnet" restore "$hfProject" --configfile "$hfRun/config/source.NuGet.Config" --use-lock-file --force-evaluate --warnaserror -p:NuGetAuditMode=all
# 独立核对完整图、派生锁、下载型 ref pack 与实际 analyzer / SourceLink 身份后继续。
"$hfDotnet" restore "$hfProject" --configfile "$hfRun/config/source.NuGet.Config" --locked-mode --warnaserror -p:NuGetAuditMode=all
"$hfDotnet" build "$hfProject" -c Release --no-restore --warnaserror -p:UseSharedCompilation=false
```

`hfProject` 固定为 Cronos、Core、NetCore、AspNetCore、PostgreSql 五个选择目标；不是整个上游解决方案。先 Cronos，再 Core / NetCore / AspNetCore，最后插件。复用原包 Cronos 做编译引用，合并阶段必须使用本次源码重建 Cronos：

```bash
"$hfDotnet" "$hfRun/tools/ILRepack/tools/ILRepack.exe" \
  /out:"$hfRun/outputs/source/merged/Hangfire.Core.dll" \
  /lib:"$hfRun/sources/hangfire/src/Hangfire.Core/bin/Release/netstandard2.0" /target:library /internalize \
  "$hfCoreDll" "$hfCronosDll"
```

合并输出仍命名 Hangfire.Core.dll，移回 source 根后检查元数据，避免输出文件名改变程序集身份。ILRepack runtimeconfig 自带 major roll-forward；GitVersion 原 props 默认调用 `dotnet --roll-forward Major tools/netcoreapp3.1/gitversion.dll`，包含 ARM64 libgit2。只尝试固定私有 runtime，不安装 3.1 / 5 / 6 runtime、不设置全局 roll-forward。GitVersion 必须启用，使用本地附着 master、位于冻结 commit 的工作副本及 `GitVersion_NoFetchEnabled=true / GitVersion_NoNormalizeEnabled=true / GitVersion_NoCacheEnabled=true`；校验输出确为 1.20.12 与原程序集版本，不手工将 1.9.4 改名冒充原包。

标记组只对 Core 的实际注册 / 创建入口加入内部诊断。改动 / 新增 C# 必须嵌入其实际源码并核对 PDB checksum；SourceLink 指向原 commit 不足以证明修改文件对应，不能以旧 URL 冒充新源码。

### 4.3 隔离 Radish 基线与实际调用

使用 git archive 导出 `9bc15795` 的干净应用副本；只复制允许的 219 项缓存和验证过的 osx-arm64 两配置 native。以私有 SDK restore / audit、Debug / Release 严格构建，再各 publish 一份 generic API。应用原图保持 219 项，SQLite / notices 原门禁继续执行；不复制 Local 配置，不重新构建 Rust 或改前端依赖。

```bash
"$hfDotnet" restore Radish.slnx --force-evaluate --warnaserror
"$hfDotnet" package list --project Radish.slnx --vulnerable --include-transitive --format json --no-restore
"$hfDotnet" build Radish.slnx -c "$hfConfiguration" --no-restore --warnaserror -p:UseSharedCompilation=false
"$hfDotnet" publish Radish.Api/Radish.Api.csproj -c "$hfConfiguration" --no-restore --warnaserror -o "$hfApiOutput"
```

每个 case 从原 API 输出创建新副本，仅允许替换 Core / NetCore / AspNetCore / PostgreSql 四个 DLL；其余文件 hash 和原 deps / runtimeconfig 不变。额外只新增本组探针 DLL，不覆盖任何同名原文件；`hfProbeDll` 必须位于本组 hfCase 内，使用该输出原 deps / runtimeconfig，不重新生成依赖描述以掩盖替换问题：

```bash
"$hfDotnet" exec --runtimeconfig "$hfCase/Radish.Api.runtimeconfig.json" \
  --depsfile "$hfCase/Radish.Api.deps.json" "$hfProbeDll" "$hfProvider" "$hfCaseEvidence"
```

## 5. 验证矩阵与数据影响

| 验证 | 原包 | 四 DLL 源码基线 | 源码基线 + Core 诊断 |
| --- | --- | --- | --- |
| Debug / Release × SQLite | 2 组 | 2 组 | 2 组 |
| Debug / Release × PG17 | 2 组 | 2 组 | 2 组 |
| Debug / Release × PG16 | 2 组 | 2 组 | 2 组 |

合计 **18 组**，每组必须核对：

- 四 DLL 路径 / hash / 程序集身份与预期组一致；对比公开类型、方法 / 参数 / 默认值、接口与资源。JSON 依赖引用版本的预期变化单独记录并核对当前运行时实际绑定，未知公共契约差异阻断。
- 真实 `ApiRecurringJobRegistration.Register` 的 16 项契约；之后移除全部周期项。合计 288 项注册核对。
- AddHangfire / AddHangfireServer 的实际 DI 与单 HostedService 生命周期；专用队列一个成功任务、一个失败重试后成功任务，两个均 Succeeded 且重试尝试为 2。合计 36 个最终成功任务。
- `UseHangfireDashboard` 在内存 RequestDelegate / DefaultHttpContext 中处理 GET，并检查 HTML / 嵌入资源；不启动 Kestrel / API / Gateway / Auth，不开放 Dashboard 端口。
- 诊断组须出现来自新 Core 的实际注册 / 创建标记；其他两组无该标记。失败不换回原 DLL，不将缺少 JSON、零用例或只有退出码 0 算通过。

SQLite 数据只在 case 目录内，版本 / 实际模块继续按 3.53.3 契约核对。PG 采用两个已缓存 ARM64 digest：

| 服务 | 固定镜像 |
| --- | --- |
| PG17 | `postgres@sha256:a63c1ea3f6a3a5a1621f97d2aba3b30faecfab4224c1b0000cc85ced57bb1939` |
| PG16 | `postgres@sha256:a902560563d3a5661228585f53474c5065e7f273b21ffd77e555a8b5682c5690` |

本次只读复核镜像存在、无容器、15432 未见监听；执行前重查，冲突即停。PG17 的六组结束并清理后再 PG16，`--pull=never`、`--platform linux/arm64`、2 CPU / 1 GiB / pids 256 / shm 256 MiB，新卷挂 `/var/lib/postgresql/data`，仅 `127.0.0.1:15432:5432`。账号 / 初始库 `radish_hf_source`，公开合成密码 `radish-hf-source-test-only-20261031`；不接既有实例。

容器 / 卷名称为 `$hfTask-pg{17|16}` / `$hfTask-pg{17|16}-data`，标签 `io.radish.hangfire-source=$hfTask`。每组 schema 限定 `radish_hf_20261031_c9e4_pg{17|16}_{debug|release}_{original|source|marked}`，拒绝其他名称；释放 worker / pool 后逐个 DROP 并查询确认。清除继承的业务连接与 RADISH_TEST_*，只注入本组测试连接。没有 Redis / Valkey、业务迁移或生产数据操作。

每轮 hfImage 取上表完整 digest，hfContainer / hfVolume 取该轮精确名称：

```bash
docker --context orbstack volume create --label "io.radish.hangfire-source=$hfTask" "$hfVolume"
docker --context orbstack run -d --pull=never --platform linux/arm64 \
  --name "$hfContainer" --label "io.radish.hangfire-source=$hfTask" \
  --cpus 2 --memory 1g --pids-limit 256 --shm-size 256m -p 127.0.0.1:15432:5432 \
  --mount "type=volume,source=$hfVolume,target=/var/lib/postgresql/data" \
  -e POSTGRES_USER=radish_hf_source -e POSTGRES_DB=radish_hf_source \
  -e POSTGRES_PASSWORD=radish-hf-source-test-only-20261031 \
  --health-cmd='pg_isready -U radish_hf_source -d radish_hf_source' \
  --health-interval=2s --health-timeout=3s --health-retries=30 "$hfImage"
```

最多等 60 秒 healthy，再核对 image / container ID、ARM64、回环 PortBindings 与 `SELECT version(), current_database(), current_user`，不只依赖 pg_isready。

## 6. 归档、回退、清理与停止线

- 在首个运行操作前注册 finally / trap，记录展开命令、PID、退出码、容器 / 卷 ID 和时间。预检可用磁盘至少 8 GiB；单次获取 10 分钟、单工程构建 15 分钟、单调用组 120 秒作为诊断停止点；超过即保留日志并停止本任务子进程，不能静默重试到通过。
- 归档到全新 `artifacts/source-bundles/radish-hangfire-source-20261031-c9e4`：原始 Git bundles / refs、42 项输入 catalog / local feed、工具 URL / hash、原始锁 / 派生锁、适配 / 标记补丁、源码索引、许可证 / NOTICES / Dashboard 资源映射、构建与矩阵结果、重放说明及明确的未关闭项。
- 这是项目所有者的本地审阅材料；不把 SDK、包缓存或业务源码自动装入正式公开发行包。SDK 保留官方获取 URL / hash 与本地输入身份，正式源码获取入口及可再分发范围另行确认。
- 两组数据库退出后按 ID + 标签删除本任务容器 / 卷，检查 schema / 卷 / 端口和进程残留；不 prune 或删除共享 PG 镜像，不改代理。验证归档索引后删除本任务源码工作副本、安装的私有 SDK、包缓存、bin / obj、SQLite / PG 合成数据和临时凭据文件。
- 回退无需回写产品 DLL：实验只发生在隔离副本。仓库新增工具若撤销，使用普通 revert 保存历史；已归档证据保留，不 reset / force push。
- 输入 hash、42 项契约、219 项应用图、GitVersion 输出、审计、公共接口、资源 / PDB、运行状态、数据边界或清理不符时停止受影响阶段。不自动换 SDK / 包、增加豁免、重定向 provider 或修改业务实现。

## 7. Windows 与外部事项

Windows Razor 需要单独准备的 Full MSBuild 与 RazorGenerator.MsBuild `2.5.0`，并核对 `Razor.build` 固定的 packages 路径；本机没有这些工具。本批保留 .cshtml 与已生成 .cshtml.cs 的 hash，**不声称重现了 Razor 生成**。后续优先 windows-2025 托管 runner，先形成实际工具库存 / 精确命令与证据方案，再单独确认 CI 改动、push 和远端执行；不复用 SQLite 手动矩阵触发它。

NoDrive 询证、SNI、Valkey、公开源码托管、对外通信、master PR、tag / Release、发布和部署均不在本批授权请求内。
