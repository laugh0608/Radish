# SQLite 原生依赖统一与构建产物一致性方案

> 2026-10-31（Asia/Shanghai），**待项目所有者确认，尚未实施**。本轮只核对已有依赖图、包内文件、SDK 规则和本机既有输出，并读取官方候选包；未 restore、build、publish、访问数据库或启动服务。
>
> 建议：采用 Microsoft SQLite `.Core` 包、SQLitePCLRaw `config 3.0.3` 与唯一 native 包 `SQLitePCLRaw.lib.e_sqlite3 [3.53.3]`，移除引入第二套 native 的 `bundle_e_sqlite3 / SourceGear.sqlite3` 链。保留现有 ORM、EF、Hangfire provider 和数据库支持边界。

## 1. 核对事实与根因

核对基线为 `dev / 29e70301`、SDK `10.0.300`。机器可读证据见[依赖链与文件核对](../records/sqlite-native-chain-inspection-2026-10-31.json)：包含 19 份既有 assets 的 SHA256 / 解析图、两个现用 native 包、10 份本机既有 build 输出，以及拟采用 native 包的文件哈希和只读探针。已有 assets / bin 可能包含前批构建结果，本轮没有重新还原或构建，不能据此宣称新候选已通过。

### 1.1 三个引入入口、两套 native 文件

| 引入入口 | 当前解析链 | 结果 |
| --- | --- | --- |
| `Directory.Build.props` | 除 Core / Shared / Extension 外，逐项目直接引用 `SQLitePCLRaw.lib.e_sqlite3 3.50.3`，`PrivateAssets=all` | 旧 native 在多个层级重复声明；`PrivateAssets` 不表示本项目不会复制该资产 |
| `Radish.Extension` | `bundle_e_sqlite3 3.0.3 → config.e_sqlite3 3.0.3 + SourceGear.sqlite3 3.50.4.5` | 后者提供实际 SQLite **3.50.4** |
| Microsoft / Hangfire 传递链 | `Microsoft.Data.Sqlite 10.0.0` 与 EF SQLite 包要求 `bundle_e_sqlite3 >= 2.1.11`；`Hangfire.Storage.SQLite 0.4.2 → sqlite-net-pcl 1.8.116 → bundle_green 2.0.4 → lib.e_sqlite3 >= 2.0.4` | Microsoft 的 bundle 在不同项目图解析为 2.1.11 或 3.0.3；Hangfire 保留旧 native 包 ID 的要求 |

当前 net10.0 图中，旧 native 包有 29 项 runtimeTargets，SourceGear 包有 31 项，其中 **29 个路径重叠**。因此，只删除全局旧包引用，API / Tests 仍会因 Hangfire 引回旧 native；只把 SourceGear 加到 Gateway，也会扩大双链，均不能作为统一方案。

| 当前项目图 | bundle / core / provider.e_sqlite3 | native 包 |
| --- | --- | --- |
| API、Auth、DbMigrate、Tests | 3.0.3 | lib.e_sqlite3 3.50.3 + SourceGear 3.50.4.5 |
| Gateway、Common 及多数下层库 | 2.1.11 | lib.e_sqlite3 3.50.3 |
| Extension 单独图 | 3.0.3 | SourceGear 3.50.4.5 |

API / Tests 另保留 `bundle_green 2.0.4`、`provider.dynamic_cdecl 2.0.4`；不能将上表理解为整条 SQLitePCLRaw 链都已经是 3.0.3。

### 1.2 文件与复制规则

- 本轮读取四宿主及 Tests 的 Debug / Release 共 10 份 `runtimes/osx-arm64/native/libe_sqlite3.dylib`，均匹配旧包 SHA256 `c2e6eb6f3acdd204502111f158400e65bf7af73c30869a238e7dc9e5c704265c`，只读调用返回 **3.50.3**。
- SourceGear 本机文件 SHA256 为 `7b319cd32435ab28c97041fad74b892be218e6f0f74790802105309c1ec515a9`，返回 **3.50.4**。上批 API publish 的实际文件已匹配它，见[历史产物核对](../records/hangfire-source-inputs-2026-10-26.md#4-新发现sqlite-测试与发布基线不同)；本轮没有重新生成 publish。
- 两包选中的 `buildTransitive/net9.0/*.targets` 只为 `browser-wasm` 设置 NativeFileReference，没有给正式服务平台定义优先复制顺序；正式平台的 native 主要由 NuGet runtime assets 和 SDK 处理。
- SDK `Microsoft.NET.ConflictResolution.targets` 对 build 与 publish 分别处理冲突；其注释明确指出资产集合可能因 PrivateAssets 等条件不同。历史日志已经出现同名 native 文件版本相等时的选择，不能依赖导入顺序、包名排序或缓存状态固定结果。
- `Directory.Build.targets` 的 Rust `native/` 内容接线属于另一条已建立的生产 / 消费契约，本方案不改变它。API Dockerfile 现有 `ErrorOnDuplicatePublishOutputFiles=false` 不是 SQLite 身份保证；本批不把它扩大到一般发布冲突重构。

根因是**同一目标图含两个提供同名 native 文件的包，同时 native 所有权分散在全局引用和功能层 bundle 中**。本轮未重新执行 SDK target，不把历史差异唯一归因于某一个 MSBuild 属性。

## 2. 推荐方案与精确输入

### 2.1 在 Common 集中声明现有数据库运行依赖

| 位置 | 拟定修改 | 精确版本 / 预期解析 |
| --- | --- | --- |
| `Radish.Common.csproj` | 将 `Microsoft.Data.Sqlite` 换成 `Microsoft.Data.Sqlite.Core` | `[10.0.0]`，保留同一 `Microsoft.Data.Sqlite.dll` 实现 |
| 同上 | 显式加入 provider 初始化配置包 | `SQLitePCLRaw.config.e_sqlite3 [3.0.3]` |
| 同上 | 显式加入唯一 native 包，正常向消费者传递 | `SQLitePCLRaw.lib.e_sqlite3 [3.53.3]`，不设置 `PrivateAssets=all` |
| `Radish.Auth.Persistence.csproj`、`Radish.Auth.Migrations.Sqlite.csproj` | 将 `Microsoft.EntityFrameworkCore.Sqlite` 换成 `.Sqlite.Core` | `[10.0.0]`，不更换 EF provider 程序集或迁移模型 |
| `Radish.Extension.csproj` | 删除 `bundle_e_sqlite3 3.0.3` 引用 | 既有 `Batteries_V2.Init()` 从 Common 传递的 config 包取得 |
| `Directory.Build.props` | 删除旧 native 版本属性与逐项目 PackageReference ItemGroup | native 来源归 Common，不再对无关项目注入全局覆盖 |
| 托管传递输入 | core / provider.e_sqlite3 由 config 引入 | 两者预期均为 **3.0.3**；与批准图不符即停止 |
| Hangfire 传递输入 | 保留现用链 | Storage.SQLite **0.4.2**、sqlite-net-pcl **1.8.116**、bundle_green **2.0.4**、dynamic_cdecl **2.0.4** |

方括号为 NuGet 精确版本范围。Common 已承担正式数据库驱动声明；此处只是集中这条既有依赖，不新增数据库抽象、跨层服务或初始化框架。Npgsql、SqlSugarCoreNoDrive、SystemCommon、Hangfire Core 和 PostgreSql provider 的版本与业务行为不变。

目标图预期不再包含 `SourceGear.sqlite3`、`SQLitePCLRaw.bundle_e_sqlite3`、`Microsoft.Data.Sqlite` 元包和 `Microsoft.EntityFrameworkCore.Sqlite` 元包；相应 `.Core` 包保留。Hangfire 的 green 包仍在图中，其 native 要求由唯一的 `[3.53.3]` 满足，**不宣称已移除 green / dynamic provider**。

green 与 config 仍各带同名 `SQLitePCLRaw.batteries_v2.dll`，这是已有托管兼容边界：必须核对最终复制 / 加载的是 config 3.0.3 的程序集及哈希，不能把“native 只有一个包”扩大为“所有托管文件都不存在重名候选”。本批不通过 ExcludeAssets 隐藏它，也不重打第三方包。

Microsoft 官方支持使用 [Microsoft.Data.Sqlite.Core 自选原生库](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions)；[EF SQLite Core 10.0.0](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite.Core/10.0.0) 也明确提供相同 provider，并由应用选择 native 和初始化方式。现有 `.Core` 包已在本机缓存与解析图中。

### 2.2 为什么选 native 3.53.3

采用 [SQLitePCLRaw.lib.e_sqlite3 3.53.3](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/3.53.3)，既满足 Hangfire 的旧包 ID 约束，又可单独配合 config 3.0.3。选用公开、非加密、稳定包，不接私有 feed、付费构建服务或预览版。

- 本轮直接读取官方 archive，SHA256 为 `72cb724779218e024833a21e2903c795a9e90c0bfa01520f27990c36413f32d4`；包内 repository 指向 `sourcegear/sqlite-builds`，commit `e7d537a1576bac9d7452411c4468640fa730de3e`。
- 包内 osx-arm64 文件只读探针返回 **3.53.3**；source ID 为 `2026-06-26 20:14:12 d4c0e51e4aeb96955b99185ab9cde75c339e2c29c3f3f12428d364a10d782c62`。这证明所检查文件的身份，不等于已经进入 Radish 的 .NET 加载路径。
- 本机候选与现用包的 compile options 除编译器版本外一致：`clang-15.0.0 → clang-17.0.0`，保留 THREADSAFE、FTS、JSON 所需基础能力及既有默认设置。其他平台还需各自验证。
- 版本升级有直接维护理由：[SQLite 官方 WAL 说明](https://sqlite.org/wal.html#wal_reset_bug) 列出的 WAL-reset 问题影响旧版本，修复进入 3.51.3 / 3.53 系列；项目已有 WAL 使用路径。因此不把统一到 3.50.3 / 3.50.4 作为长期收口。
- 此候选是旧 native 包 ID 下的稳定输入，**不是 SQLite 最新版**。官方另有 [3.53.4 补丁](https://sqlite.org/releaselog/3_53_4.html)及 [3.54.1](https://sqlite.org/changes.html)；它们不能仅靠升级现有 bundle 就消除 green 引出的另一包 ID。本批冻结 3.53.3，执行前仍须完成最新漏洞审计；若发现阻断问题，重新裁决 native 版本 / 路线，不压低审计门槛。

### 2.3 其他路线为何不作为本批推荐

| 路线 | 判断 |
| --- | --- |
| 只删全局旧包，保留 SourceGear 3.50.4.5 | Hangfire 仍引入旧 native，不能根除双链 |
| 两个包都保留，通过覆盖顺序、PreferredPackages 或 ExcludeAssets 指定胜者 | 继续维护竞争与选择规则；不符合本批单一 native 来源目标 |
| 全部退回 2.x bundle | 托管层从 3.0.3 回退，仍需处理版本 / 已修缺陷；没有必要为了 native 统一回退托管层 |
| 最新 `SQLite` 包 + 改造 Hangfire 传递链 | 需要替换 / 重建上游包或另设资产排除契约，扩大调度与供应链边界，另行评审 |

## 3. 影响范围与实现边界

- **四宿主**：API 的业务 SQLite / Hangfire；Auth 的业务 SQLite / EF OpenIddict；DbMigrate 的 schema / OpenIddict；Gateway 继承 Common 的数据库驱动资产，即使不主动打开 SQLite，也必须核对其独立输出。
- **下层库与测试**：触达 Common 消费者和两个 EF 项目，Tests 同时引用四宿主，必须补独立进程 / 产物验证，避免聚合测试图掩盖 Gateway 单独依赖错误。
- **托管初始化**：保留 `SqlSugarSetup.EnsureSqliteProviderInitialized`；验证 Microsoft.Data.Sqlite 反射发现 batteries、EF 先用、Hangfire 先用及 SqlSugar 先用的独立冷进程路径。若需改宿主启动逻辑或 provider 注册顺序，视为方案变化，先报告再确认。
- **正式服务平台**：`osx-arm64 / osx-x64 / win-x64 / linux-x64 / linux-arm64`。Linux 最终容器沿用 glibc 路线。通用输出中其他 RID 文件也须逐项匹配唯一包，但不因此承诺新增平台支持。
- **产品边界**：不修改 Web / Flutter 依赖、移动端 SQLite、Rust 图片 ABI、数据库配置语义、业务 schema、根 LICENSE、Compose 或生产数据库。
- **分发材料**：native 包自带 `LICENSE.txt`（指向 SQLite Public Domain），config / core / provider 为 Apache-2.0，Microsoft `.Core` 为 MIT；按精确包分别补原文 / attribution 和四宿主材料映射，复用既有 notices 系统。退出的 SourceGear 不再列作候选 runtime；历史 CSV / 材料证据保留。

来源 commit 与文件身份不等于完成可重复源码构建或整个许可专题；本批不关闭 Hangfire Corresponding Source、NoDrive 询证、SNI、Valkey 或全生态分发审核。

## 4. 构建与产物门禁设计

实施时新增限定到 SQLite 的检查脚本及必要 MSBuild 接线，复用现有仓库脚本风格，不新增包管理 / 原生加载框架：

1. 在还原图中检查精确输入、退出包与同名 native 来源。所有消费 Common 的图只允许一个 native 包；green / dynamic provider 保留项单独列明。
2. 在 SDK 冲突消解前检查 SQLite native 的来源，不能只查看已经过滤后的胜出文件；发现第二个来源或不同 hash 的同目标路径立即失败，不设置新 override。
3. build 与 publish 分别核对实际输出，记录源包、RID、路径、size、SHA256；通用输出检查所有实际复制的 native，RID 输出检查对应目标文件，拒绝旧输出残留。
4. 检查 `.deps.json` 与复制文件一致；它是辅助证据，不能代替文件 hash。额外核对实际加载的 batteries / core / provider 来源。
5. 独立进程在原输出旁运行探针，记录 .NET 实际加载模块路径 / hash、`sqlite3_libversion()`、source ID，并通过 Microsoft.Data.Sqlite / SqlSugar / EF / sqlite-net 查询 `sqlite_version()`。不能从临时目录显式加载包文件后，反推宿主加载已经正确。
6. 同 RID 的 Debug、Release、Tests、四宿主 build / publish，以及普通 publish / `--no-build` 的 native hash 必须一致；跨 RID 只比较各自批准 hash，不能要求不同架构逐字节相同。

门禁应带有“缺库 / 错包 / 错 hash / 第二来源”的负例，证明会真实失败；不以新增静态断言镜像实现逻辑代替数据库行为回归。MSBuild 接点若无法在计划范围内可靠覆盖，先报告，不通过改 SDK 内部文件补洞。

## 5. 验证矩阵与结论分层

所有条目目前均为**拟执行**。先完成本机开发验证；其余平台只在对应条件及运行授权齐备后执行。未执行项明确保持未关闭。

| 层次 | 目标 / 配置 | 核对内容与通过条件 |
| --- | --- | --- |
| 依赖 | 解决方案 / 各独立项目图 | 精确版本、退出包、唯一 native 来源、NuGet 审计；无新增未批准包 / 版本，无阻断级漏洞 |
| 严格构建 | 本机 Debug / Release | 四宿主及 Tests 构建成功；不新增 warning suppression；保留现有 Rust native 门禁 |
| 本机产物 | osx-arm64，两配置 | Tests + 四宿主 build；四宿主全新普通 publish 与 `--no-build` publish；所有 native hash / version / source ID 一致 |
| 冷进程加载 | 四宿主独立输出、Tests、两配置 | 模块实际路径及 hash 正确；Microsoft / EF / SqlSugar / Hangfire 先初始化的路径均成功；不靠 Tests 聚合图补依赖 |
| ADO.NET / ORM | 内存与临时 SQLite | 参数化 Int64、NULL / Unicode / 时间、读写、事务回滚、连接释放 / 重开、schema ledger、分库租户隔离；复用 DatabaseDriverContractTests / DatabaseProviderBoundaryTests / TenantIsolationRegressionTests 及已有仓储迁移回归 |
| WAL / 数据兼容 | 临时文件及专用夹具副本 | 多连接写入、checkpoint / 重开、busy timeout、提交 / 回滚与 integrity_check；3.50.3 / 3.50.4 创建的夹具由新库读取，新库正常操作后的副本由旧库复核，不使用生产数据，不将压力测试当作 WAL 缺陷数学证明 |
| EF / DbMigrate | 临时独立 SQLite | AuthOpenIddictMigrationTest、DbMigrateBootstrapTests：空库、幂等、既有完整 schema 接管、残缺 schema 明确失败；DbMigrate 子进程仅指向本批目录，禁止默认路径落入既有数据库 |
| Hangfire SQLite | 当前发布 DLL + 临时独立 storage | 真实 Register 的 ID / cron / 时区 / 方法 / 参数；探针入队、成功、失败后重试及 worker 停止；专用队列，无业务清理任务、无端口。注册断言复用 ProgramRecurringRegistrationTests，真实 storage 测试单独覆盖 |
| 后端回归 | 本机 Debug / Release | 后端全量测试按实际数量记录；PostgreSQL / Redis 等未授权服务相关跳过单独报告，不当作已通过 |
| 目标 RID 产物 | 五个正式服务 RID，Debug / Release | 四宿主 RID build / publish 与 Tests 构建产物的文件身份；API / Tests 需要对应既有 Rust artifact。各平台 native 入口 / 架构 / 动态依赖正确 |
| 平台执行 | 五个正式服务 RID | 在对应 OS / 架构执行加载、SQLite / EF / Hangfire 核心回归；交叉 publish 只证明产物，不替代运行。Windows / Intel Mac / Linux 运行资源另确认 |
| 最终镜像 | linux-x64 / linux-arm64 Release | 四个后端镜像最终层 native、模块实际加载、版本、notices 及现有镜像漏洞策略；构建 / 容器启动另按命令和清理清单授权 |
| notices / 仓库 | 当前候选 | `check:nuget-notices`、相关脚本自测、文档 / 卫生与 diff 检查；材料与实际四宿主一致 |

现有 PostgreSQL provider 版本不变；其配置与 provider 边界测试纳入本机回归。接数据库的 PostgreSQL 回归在后续批次 / 合并前用授权资源执行，不为本方案自动启动 PG 或沿用历史连接授权。

**结论规则**：本机通过可关闭本机开发验证；五平台产物、目标运行和最终镜像分别留结论。缺少某平台条件时保留待办，不能把本机替换成功、旧图片门禁或历史测试数量扩大为全部平台 Go。

## 6. 执行顺序、命令与授权边界

确认本方案后才进行以下实现。本轮没有执行这些命令：

1. 保存本批基线引用 / assets / native hash；在 `dev` 修改第 2 节声明、限定门禁、回归和 notices；不创建新分支。
2. 包声明通过审阅后直接编辑 XML，不额外执行 `dotnet add package`。执行 `dotnet restore Radish.slnx --force-evaluate --warnaserror`，仅使用仓库 NuGet.Config 的 nuget.org 与 `.nuget/packages`；HTTP 缓存放本批临时目录。不新增永久 lockfile 策略、不写用户全局缓存。
3. 保存实际依赖差异及 `dotnet package list --project Radish.slnx --vulnerable --include-transitive --format json --no-restore` 报告。后续构建 / 测试 / 发布使用 `--no-restore`，输入变化先重新审查。
4. 本机执行 `dotnet build Radish.slnx -c <Debug|Release> --no-restore --warnaserror`，以及 `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -c <Debug|Release> --no-build --no-restore`；定向数据库 / Hangfire 夹具只创建本批临时文件，不启动宿主或监听端口。
5. 四宿主分别执行 `dotnet publish <host>/<host>.csproj -c <Debug|Release> --no-restore --warnaserror -o <本批全新目录>`，再向另一全新目录执行 `--no-build --no-restore` 对照；实施时展开精确目录，不覆盖既有发布目录。
6. 新平台 / RID 的 restore 只限五个已列正式服务 RID 及相同批准依赖闭包；缺少工具链 / Rust artifact 不自动安装或下载。Docker、VM 启动、外部数据库、CI 外部触发、宿主启动仍需独立列清命令、端口、运行影响与清理后获得授权。
7. notices 使用既有 `node Scripts/check-nuget-notices.mjs --write --assets` 生成，随后只读校验；执行文档、卫生和 `git diff --check`，记录结果与未执行矩阵。

若 restore 改变批准清单外的直接 / 传递版本，出现新的兼容警告、High / Critical 漏洞、托管 DLL 选择变化超出预期或平台不兼容，则暂停受影响步骤，报告具体差异并重新裁决；不静默升级依赖、修改 NoWarn 或降低安全门槛。

## 7. 回退与清理

- **源码回退**：本批改动形成独立可追溯提交；必要时通过普通 revert 撤销该批声明、门禁和材料接线，重新还原 / 构建旧候选。保留调查记录并标注撤回；不 reset、rebase 或 force push。
- **产物回退**：候选发布到全新目录，禁止将新旧 native / DLL 拼接。回退采用完整旧候选，恢复第 1 节的旧图 / 逐文件基线；旧图本身仍有本方案揭示的差异，不称为问题已解决。
- **数据库回退**：本批仅接触临时夹具，不更改既有数据库。停止并释放所有连接 / worker 后，清理本批数据库、WAL / SHM 和探针文件；不删除其他数据库的 WAL / SHM。将来若部署新 native，必须另做停写备份与恢复方案，不能把文件格式兼容等同数据恢复保证。
- **资源清理**：保留候选 hashes、依赖图、探针源码 / 日志和审阅材料；精确清理本批临时进程与运行数据。NuGet 包是正常缓存，不通过删除 / 改写缓存内容控制选择。

## 8. 本轮待确认的具体范围

请求确认第 2 节精确依赖与元包替换、第 4 节限定门禁及 notices，以及第 5 节本机无服务回归 / 第 6 节仓库 restore、build、test、publish 范围。**确认前不实施**；确认本方案不自动授权安装工具链、启动服务 / 容器 / VM、访问生产、推送、PR、tag、镜像发布或部署。

本轮只读调查已完成；正式源码 / 依赖 / 构建接线未改。下一步由项目所有者确认上述方案后进入实施，其他独立发行与服务迁移事项继续按[当前顺位](../planning/current.md)推进。
