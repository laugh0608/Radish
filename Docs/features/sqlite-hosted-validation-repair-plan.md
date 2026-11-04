# SQLite 托管验证四类失败的成组修正方案

> 2026-11-04（Asia/Shanghai），**候选 `ab37d19eaf83189ec5c556d14608c1c132dce022` 的单次完整七 job 已全部通过，证据与清理完整**。本页原四类修正、后续 Windows 夹具与模块识别的本次托管缺口已关闭，见[最新记录](../records/sqlite-hosted-native-module-run-2026-11-04.md)。矩阵与冻结输入见[托管执行方案](./sqlite-hosted-native-validation-plan.md)；初次失败与后续各批事实保留原记录，不改写历史。

2026-11-04 先完成[第 8 节](#windows-fixture-repair)夹具修正及候选 `44c13c50` 的获批单次运行；其新暴露的冷探针问题由[第 9 节](#windows-module-probe-followup)独立获批修正，再以 `ab37d19e` 完成另一次独立批准的七 job 运行。各次均未重试、发布或部署，不能据此重复派发或扩大范围。

## 1. 本批目标与批准边界

目标是使同一候选的 Windows、macOS 和四宿主探针使用一致、可验证的参数、编译环境、依赖与执行身份，完成原有三平台 / 四镜像验收。保留 SQLite 来源 / hash / 实际加载门禁、严格编译、完整 TRX、镜像漏洞策略和失败清理，不以降低检查强度取得通过。

本次已批准并完成的是**本地实现、编译和契约验证**，具体见第 6 节 A。第 6 节 B 已另行获批并按 `daf5a4d4` 单次执行，结果见[第二次记录](../records/sqlite-hosted-native-second-run-2026-10-31.md)；该一次性授权已经执行完毕。本地结果见[实施记录](../records/sqlite-hosted-validation-repair-2026-10-31.md)；本地未安装工具或启动服务 / 容器；随后候选 push 与远端执行按独立授权完成。

- 不更新 NuGet / Cargo / npm 依赖、lockfile、SQLite contract 或正式 Dockerfile，不给业务宿主增加测试依赖。
- 本地阶段不安装 SDK / C++ / PowerShell 工具，不启动业务服务器、Docker 容器、PG / Redis / Valkey，不开放端口；远端工具与容器仅在 B 的独立授权内执行。
- 不改 Razor、原 Native Images scope、required checks、生产配置或发布流程；本地实施不 push / dispatch，随后 B 的一次性 push / dispatch 已按独立授权完成；不重跑、发布或部署。
- 四类问题按一个工程批次实施；共享探针变更后，Linux 的历史成功只作为对照，不能替代新候选的完整回归。

## 2. Windows：在 MSBuild 调用边界明确路径表示

### 根因与采用方案

当前 `sqlite-native.targets` 把 `TargetDir / PublishDir` 直接放入带双引号的 Exec 字符串，目录末尾的 `\` 紧邻闭引号；真实 Windows run 中，四宿主均因此没有取得独立 deps 选项。Microsoft 文档说明了反斜杠紧邻引号时的参数解释规则，而 Exec 在 Windows 上还经过 cmd.exe。依据：[参数解析规则](https://learn.microsoft.com/en-us/cpp/c-language/parsing-c-command-line-arguments?view=msvc-170)、[MSBuild Exec](https://learn.microsoft.com/en-us/visualstudio/msbuild/exec-task?view=visualstudio)。

采用**在 target 执行时统一路径参数表示**的方案：Windows 上将传给 Node 的文件 / 目录路径转换为 Node 可识别的正斜杠形式，保留完整路径及其引号；Unix 保留原路径。转换发生在实际使用这些属性的 target 内，避免在 SDK 尚未确定 TargetDir / PublishDir 时提前求值。

- 同时覆盖 sources、build、publish 三个入口，重点覆盖 TargetDir / PublishDir；空 RID 仍是独立的 `--rid=` 参数。
- CLI 的 `--assets / --framework / --rid / --output / --deps` 语义保持；Node 端不猜测被吞并的参数、不搜索替代 deps 文件。
- 原 SDK 冲突消解前检查、五个 build 输出所有者及四宿主 publish 检查保持。没有失败后再换命令、跳过 checker 或抑制 MSB3073 的路径。

### 回归与验收

新增真实 `dotnet msbuild → Exec → shell → Node` 参数捕获夹具，使用实际 target 文件和临时 argv 记录脚本；夹具不引用业务包、不启动应用。覆盖普通 / RID、build / publish、带或不带末尾分隔符、空格、中文、括号路径，断言 output 和 deps 为两个独立参数且路径含义不变。Windows 路径表示另覆盖驱动器根和 UNC 字符串，不访问网络共享。

该夹具必须在真实 Windows runner 执行；本机 macOS 上的通过只证明本机链路。随后验证 Windows 双配置严格构建、定向测试、16 份 publish 和 48 个冷进程，保证修复经过真实产物门禁。

## 3. macOS：把 Developer Directory、SDK 和编译器作为同一套输入

### 根因与采用方案

当前脚本取得 SDK 路径后只写日志，直接调用 toolchain 内的 clang 编译样本。Apple 随系统提供的 `man xcrun` 明确区分 `--find` 与实际执行工具：执行工具时才会为其提供选定 SDK 的 SDKROOT。本次 `ld: library 'System' not found` 与 SDK 未进入编译环境相符，尚无证据表明 SDK 损坏。

采用**一次解析、任务内固定、所有编译阶段共用**：

1. 记录当前有效 Developer Directory，在本任务子进程环境内固定 DEVELOPER_DIR；不执行全局 `xcode-select --switch`。
2. 在该环境下使用 `xcrun --sdk macosx` 取得 SDK 路径 / 版本、clang、clang++ 和 Xcode 版本；所需路径或工具缺失直接失败，不转用另一套 Xcode / Command Line Tools。
3. 设置任务 SDKROOT；身份样本显式使用同一 sysroot。Rust / vendored C / C++ 构建继承同一 SDKROOT 和目标专用 CC / CXX / linker，沿用现有 macOS 13.0 deployment target。
4. **调用路径与身份路径分开记录**：保留 `xcrun --find clang++` 返回的调用名称；realpath 仅用于确认位置和计算 hash。避免把 clang++ 的调用名折叠为 clang 后丢失 C++ driver 语义。
5. 编译并链接两个不执行的样本：C 样本使用系统头 / 库，C++ 样本实际引用标准库；检查 Mach-O 架构。既验证 SDK，也验证 C++ 链接语义，不只编译空 main。

### 回归与验收

本地契约覆盖 SDK / 编译器选择一致、调用名保留、缺失 SDK 及不匹配架构拒绝；在现有本机工具链上可编译 / 链接样本，但不得记为 Intel 验收。真正的 `macos-15-intel` 运行须完成上述样本、Rust 双配置、应用双配置、16 份 publish、48 个冷进程和 4 次图片自检，并保存完整 SDK / 编译器身份。

## 4. 探针：按固定宿主能力编译，使用每份被验产物的原依赖

### 根因与采用方案

原探针整体编译依赖 API / Hangfire / EF，只是在运行时按存在的 DLL 选择 mode。Docker job 各自从非 API 输出编译该源码，编译依赖和被验宿主不一致。

采用**公共逻辑与确有不同依赖的验证职责分离**：

| 宿主 | 必须执行的 mode | 编译部分 |
| --- | --- | --- |
| Radish.Api | microsoft、sqlsugar、ef、hangfire | 公共 + EF + Hangfire / API 注册 |
| Radish.Auth | microsoft、sqlsugar、ef | 公共 + EF |
| Radish.Gateway | microsoft、sqlsugar | 公共 |
| Radish.DbMigrate | microsoft、sqlsugar、ef | 公共 + EF |

- `RuntimeProbe.cs` 保留公共入口、Microsoft / SqlSugar、实际模块枚举和 hash 校验；EF、Hangfire / API recurring registration 与 job 类型移入对应源码文件。使用小范围编译条件连接入口，不建立动态插件或反射兜底系统。
- Node 端共用上述固定能力表；生成临时 csproj 时选择相应源码 / 编译常量，从原 deps 的托管 runtime 资产选择**该份宿主输出**已有 DLL，仍无 PackageReference；Windows 原生 DLL 不进入编译引用，其 hash 仍纳入绑定清单。
- compile 阶段也必须提供明确的 `--host`；运行阶段使用同一宿主能力表。必需程序集缺失、宿主与清单不符或请求不支持的 mode 均失败，禁止因为文件缺失自动缩减预期 mode。
- native 每份 publish 分别编译对应探针，Docker 每个 final 分别编译对应探针；移除“首个 API 编译结果供所有产物复用”的隐式缓存。三个平台共 48 次编译、四 final 再 4 次；不新增跨 job DLL 下载或复制其他宿主程序集。
- 每次编译写明 host、能力、SDK、选中源码 hash、原 deps / runtimeconfig hash、编译引用清单及探针 DLL hash；执行前核对匹配。不同产物的 DLL 不再因同名而混用。
- 运行继续使用被验宿主原 deps / runtimeconfig；探针仅临时放在输出旁或以只读 mount 加入 final，不产生替代的宿主依赖图，也不改包版本。
- Microsoft / SqlSugar 基础断言、EF 回滚、API recurring registration、Hangfire 成功 / 重试行为和实际模块校验全部保留。补齐 `job-states.json` 等已由源码产出的已知诊断文件白名单，失败时也留存，禁止泛化为上传整个 scratch。

### 接口影响与回归

这是对**内部验证 CLI 和探针编译契约**的明确调整，不是产品 API 变更。同步更新当前验证指南、托管方案和 Linux 操作示例；旧执行记录保持历史事实，不添加缺少 host 时默认为 API 的兼容分支。

回归包括四宿主真实引用集合的编译、固定 mode 数量、必需 DLL 缺失、跨宿主 / 跨产物复用拒绝、未知 mode 拒绝，以及失败 JSON / 日志 / job-states 留存。真实执行仍须在每个平台完成 16 份 publish / 48 个冷进程，在四 final 完成 4 + 3 + 2 + 3 个 mode。

没有采用“给非 API 宿主补 Hangfire / EF 包”或“每个镜像 job 额外构建完整 API”方案：前者改变业务依赖，后者保留测试工具与无关宿主的耦合并增加构建输入。新拆分仅对应已经存在的公共 / EF / Hangfire 三类职责。

## 5. 容器：以明确 uid / gid 绑定可写证据目录

### 根因与采用方案

原 API final 默认 User 为空，探针没有 `--user`，由 runner 创建的 bind 目录与容器身份未建立写权限契约；同时 cap-drop ALL 下不能依赖 root 绕过目录权限。官方机制允许显式指定数字 uid / gid，并单独限制 capabilities，见 [Docker run](https://docs.docker.com/engine/containers/run/)。

采用**所有需要写入宿主任务目录的验证容器使用当前 runner uid:gid**：

- 创建全新的 case 目录，由当前 runner 持有，设置所需的私有目录权限；记录 host uid / gid、目录 uid / gid / mode、final Config.User 及本次实际 `--user`。
- 在同一 final、同一 uid:gid、同一挂载与限制下先执行写入 / 回读 / 删除检查，记录容器内身份与宿主侧新文件所有者。检查不符即停，不再尝试换 root、chown 或放宽权限后重跑。
- 所有四宿主冷探针一致使用该身份；API 图片 self-test 与既有 scanner 的身份策略一并核对，不能只修首个 microsoft mode。
- 保留 `--network none`、只读根文件系统、cap-drop ALL、no-new-privileges、资源限额、私有 /tmp 和只读探针 DLL。只有明确的 case / 扫描输出目录可写；不 chmod 777、不挂载 Docker socket、不修改 daemon / user namespace 配置。
- 该身份属于验证工具运行条件，不变更正式镜像 USER 或宣称完成生产服务用户策略迁移。

### 回归与验收

本地检查命令构造、目录所有权及失败结果留痕；真实 Linux Docker 上必须在最终镜像内证明可写，并通过一个有意不可写的独立目录夹具证明拒绝路径。随后完成全部 12 个 mode、API 图片自检及四 final 的 Trivy / 既有漏洞策略。输出目录的可写性不得用宿主进程自行写文件代替。

## 6. 实施范围与分层验证

### A. 本次已批准完成的本地实现与验证

修改范围：

| 位置 | 内容 |
| --- | --- |
| `Scripts/build/sqlite-native.targets` | MSBuild 三个调用入口的路径参数表示 |
| `Scripts/ci/validate-sqlite-platform.mjs` 及其测试 | Xcode / SDK 一致性、每输出探针编排、容器身份、失败证据与清理 |
| `Scripts/run-sqlite-runtime-probe.mjs`、`Scripts/sqlite/` | 固定宿主能力、按职责分源编译、产物绑定清单及对应测试 |
| 新的 MSBuild 参数行为夹具 | 实际 Exec → Node argv 验证；不使用只匹配源码字符串的测试代替 |
| `.github/workflows/native-images.yml` 的 SQLite 两矩阵 job | 将新增行为检查接入实际目标 runner，保留固定 SHA / action 与其余 job |
| 当前 `Docs/` 专题 / 验证指南 | 同步内部 CLI、编译次数、权限与证据边界 |

本批在本机使用已有工具执行：Node 契约与语法检查、实际 MSBuild 参数夹具、四宿主探针编译检查、现有平台 C / C++ 样本编译链接、YAML / 原 job 对照、文档卫生和 diff 检查。需要生成的 .NET 探针工程无 PackageReference，依赖仅来自已检查的现有宿主输出；如需补齐本机输出，只执行现有依赖下的严格构建 / publish。资源缺失则记录缺口，不擅自安装或换版本。

典型验证入口保留 `node --test`、`dotnet msbuild`、`dotnet build / publish --no-restore --warnaserror`、`npm run check:repo-quality:changed`、`git diff --check`；新增夹具文件和精确调用在实现记录列出。本地不运行 SQLite 冷探针、Hangfire worker 或 Docker 容器，也不把编译成功称为真实运行验收。

所有夹具和编译样本使用本批全新临时目录，避免覆盖现有 bin / obj 或用户输出；清理只处理登记的本批目录 / 子进程，保留日志与结果。本机 SDK 与目标平台不一致时只记录本机结论，目标平台行为在 B 验收。

### B. 已另行批准并执行的远端验收

按新完整 SHA 执行一次完整 `sqlite-platforms`，不重跑旧 run，不只选择失败 job。仍为三个原生 x64 平台和四个原生 AMD64 final：

- 三个平台：新行为检查、双配置严格构建 / 定向测试、48 份 publish、144 个冷进程、12 次图片 self-test；测试发现数量以实际 TRX 为准，当前预期每平台每配置 60 项。
- 四个 final：build / final 图与文件比对、固定宿主探针编译、身份 / 目录正反向夹具、12 个冷 mode、API 图片 self-test、四次漏洞扫描与策略评估。
- 新增前置夹具单独计数，不混入业务测试 / 冷进程计划数。新候选七项均通过且证据 / 清理完整，才关闭本批；任何一项失败按实际停止位置记录，不自动重试或放宽门禁。
- 工具安装范围继续使用原方案的 Node 24.22.2、.NET SDK 10.0.403、Rust 1.96.0 与锁定镜像；不因本批修正扩大依赖或平台。保留 45 分钟 job / 38 分钟验证步骤上限，不通过加时隐藏失败；如实测资源不足，另行说明范围。
- 保留完整 SHA、job / attempt、实际工具身份、完整 TRX、编译输入清单、模块结果、notices、扫描 DB、权限前置结果、失败诊断和清理报告；继续校验 artifact ZIP digest、内部 manifest 与本地总归档。

## 7. 完成标准

四类修正均须有对应真实行为证据：Windows 参数实际到达 Node、macOS 系统库和 C++ 标准库正确链接、各宿主探针在本身引用集合下编译 / 执行、final 中以明确身份完成受限目录读写。Linux 新候选也须回归通过。

代码、验证指南与执行矩阵一致；包图、宿主 deps、正式镜像和业务行为没有为测试让步；失败诊断可保留，资源清理可核对。第二次运行的 Windows 临时 SQLite 清理失败由第 8 节修正，后续模块计数问题由第 9 节修正；最终 `ab37d19e` 七 job 通过并完成证据 / 清理核验，满足本批标准。未来代码或候选变更仍需重新界定验证与运行授权，当前文档不是重复执行授权。

<a id="windows-fixture-repair"></a>

## 8. Windows 临时 SQLite 夹具修正（2026-11-04）

### 批准范围与实现

本批处理 `AuthOpenIddictMigrationTest` 三个文件型 SQLite 用例：空库初始化与幂等、完整旧 schema 接纳、不完整旧 schema 拒绝。三个用例共用 `SqliteConnectionStringBuilder` 构造临时连接，显式设置 `Pooling = false`，且 `RuntimeDatabaseConfig` 与 EF 使用同一个连接字符串。原有 `await using` 保证 DbContext 在外层 finally 删除数据库前完成释放；接纳旧 schema 用例的两个上下文按先后顺序分别释放。

相邻 `SqliteNativeContractTests` 的文件型 WAL 夹具已关闭连接池，其所有连接与命令也在目录删除前释放；内存 SQLite 夹具没有临时文件删除边界。本批只修改上述三个用例的共享配置，不建立通用夹具框架，不修改 PostgreSQL 测试、生产连接池或依赖图。

保留全部迁移 / schema 断言和直接删除失败可见性；不增加删除重试、异常吞并、全局 pool 清空或 GC 等补偿路径。默认池继续持有句柄与历史 Windows 失败相符，但本机 macOS 验证不能证明原 Windows 句柄所有者或关闭该平台缺口。

### 本地验证与完成条件

- 使用已有依赖，执行 Tests 项目及其宿主引用的 Debug / Release `--no-restore --warnaserror` 构建。
- 双配置运行托管方案已有五组定向测试，显式排除 `Database=PostgreSQL`，保留完整 TRX；检查三个迁移用例实际执行且通过，不能只统计聚合成功。
- 执行 `npm run check:repo-quality:changed` 与 `git diff --check`；验证事实、环境和证据见[本批记录](../records/sqlite-windows-fixture-local-repair-2026-11-04.md)。
- 本地结果只证明夹具实现与本机验证；Windows 双配置、publish、冷进程加载由后续 `ab37d19e` 的独立托管记录关闭，不以本地结果替代。

### 下一次远端执行前必须明确

确认新完整提交 SHA、固定工具输入、Windows 双配置 / 16 份 publish / 48 个冷进程及必要关联回归、证据留存与精确清理，再单独取得 push / 单次执行授权。现有 `sqlite-platforms` 入口仍运行三原生平台和四个 final job，没有仅选择 Windows 的入口；本批不修改 workflow 或编排器，也不把上述 Windows 验收目标当作已有独立入口或整套七 job 的执行授权。Hangfire inventory / verify、工具安装、服务 / 容器启动、发布和部署均不在本批范围。

### 已另行批准的候选运行

所有者随后明确批准候选 `44c13c5091f51621d475107fbb5834ceedef12f4` 的 push 与单次完整七 job `sqlite-platforms`，冻结工具输入、证据归档、清理和文档同步均在该次范围内；没有重试、发布或部署授权。[运行记录](../records/sqlite-hosted-native-fixture-run-2026-11-04.md)绑定该次执行，后续运行不沿用其授权。Windows Release 60 项定向测试及三个迁移用例通过，首个 API 冷探针出现新的模块计数失败；不能将夹具修正的通过推定为平台已通过。

<a id="windows-module-probe-followup"></a>

## 9. Windows 模块识别与失败诊断（本地与本次托管完成）

首个 Windows 冷探针报告命中两个 SQLite 模块，但没有保存两个模块的名称 / 路径。旧名称子串过滤也可能匹配托管 provider；这不足以确认旧 run 的实际原因，也不足以宣称有两份原生库，事实与推断见[运行记录第 3 节](../records/sqlite-hosted-native-fixture-run-2026-11-04.md#3-windows迁移测试通过首个冷探针出现新失败)。所有者随后批准先提交该轮结果文档，再完成本节本地实现与验证；结果文档由 `1e1d0ff1` 保存，实施见[本批记录](../records/sqlite-probe-module-local-repair-2026-11-04.md)。

### 模块身份与失败证据

- `SqliteProbeModuleInspector.cs` 属于验证工具：Windows / Linux 继续读取当前进程的 `Process.Modules`，macOS 继续读取 dyld 已加载映像，增加加载地址；不主动加载候选库来建立身份。
- 名称子串仅用于收集诊断候选。原生库必须同时满足目标平台精确文件名、模块名与格式：Windows `e_sqlite3.dll` 的原生 PE DLL，Linux `libe_sqlite3.so` 的 ELF，macOS `libe_sqlite3.dylib` 的 Mach-O。Windows 名称按 OrdinalIgnoreCase，Unix 按 Ordinal。
- 仅在 PE / CLR 确认 ILOnly、程序集名为 `SQLitePCLRaw.provider.e_sqlite3` 且模块名 / 文件名匹配时，将候选归为托管 provider。未知 SQLite 相关名称、格式或托管身份仍直接失败，不用名称白名单静默忽略潜在原生库。
- 原生候选必须恰好一个；同路径且同加载地址重复出现时单独报告重复枚举，两个路径或加载地址仍按多模块拒绝。两种情况都保留原始条目，不做去重或数量放行。
- `module-inventory.json` 在判断数量前写出完整枚举列表、候选名称 / 路径 / 加载地址及可读取的 SHA256；逐项记录分类、托管程序集身份、最终状态和错误。文件无法读取 / 解析时保留已取得的证据并失败；诊断写入二次失败与原错误一起上抛。
- 主探针失败结果增加阶段与清单位置；编排器的既有白名单同步保留该 JSON。原成功字段、唯一依赖来源、原 deps / runtimeconfig、实际加载路径 / hash、SQLite 版本 / source ID 及编译输入绑定门禁保留，另复核检查期间 native hash 没有变化。

### 本地验证与后续停止线

识别组件作为固定公共源码进入四宿主临时探针编译清单，并由 Tests 项目直接链接同一源码。`SqliteNativeContractTests` 增加 22 项行为用例，覆盖三平台名称 / 格式、实际托管 provider 元数据、空 / 多模块、重复条目、伪装 / 错误身份、损坏 PE 与 IO 失败留痕；沿用五组 filter 的定向集合由 60 增至 82 项，不改 CI job 或 mode 矩阵。

本机 Debug / Release 严格构建与各 82 项定向测试、32 项 Node 契约、四宿主双配置的 8 次探针编译 / 24 个 macOS arm64 冷 mode 已通过，诊断保留与清理完成。本机 Windows / Linux 识别夹具只验证受控条目与文件判断，不替代真实平台枚举；旧 Windows 两个实际命中项仍未知。

最初本地授权不包含 push / 远端验收；随后所有者确认候选 `ab37d19eaf83189ec5c556d14608c1c132dce022` 的一次完整七 job。三原生平台双配置各 82 项、48 份 publish、144 个原生冷进程及四 final 的 12 个 mode 全部通过，156 份清单与证据 / 清理已核验，见[托管记录](../records/sqlite-hosted-native-module-run-2026-11-04.md)。Windows 每份清单明确区分一个托管 provider 与一个原生模块；本次平台缺口关闭，旧 run 未留存的身份仍不回填。

该一次性授权已执行完毕；未来新 SHA 或运行范围仍单独确认。旧 run 不重试，不换库、不降低门禁、不发布部署。
