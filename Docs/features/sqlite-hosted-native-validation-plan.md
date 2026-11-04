# SQLite 剩余原生平台的 GitHub 托管执行方案

> 2026-11-04（Asia/Shanghai），**候选 `ab37d19eaf83189ec5c556d14608c1c132dce022` 已按独立批准完成单次完整七 job，全部通过，证据与清理完整**。三原生平台双配置共 492 项测试、48 份 publish、144 个原生冷进程及四 AMD64 final 的 12 个 mode 通过，Windows 实际模块身份已核对，本批平台缺口关闭，见[最新记录](../records/sqlite-hosted-native-module-run-2026-11-04.md)。未重试、发布或部署；历史失败保留原记录，本次不关闭其他依赖发行边界。

首轮本地实现及验证见[本批记录](../records/postgres-regression-2026-10-31.md#4-托管-runner-本地接线)。执行前复核发现 SHA 授权、失败证据和清理结果存在缺口；所有者已另行批准本地成组修正，现已完成并通过 38 项检查，见[执行契约修正记录](../records/sqlite-hosted-execution-contracts-2026-10-31.md)。候选 `915f63591bfbfeff0978c8fb2de3b64fd997cce9` 已按授权 push，并完成[首次托管执行](../records/sqlite-hosted-native-run-2026-10-31.md)：Linux x64 双配置各 60 项、16 份发布、48 个冷进程通过；Windows argv、macOS SDK、非 API 探针编译依赖和 API 容器写权限问题使其余六项失败。七项证据与清理均完成；随后四类问题已完成本地修正，并按授权完成上述第二次独立运行；历史首次结果不改写。

四类失败的修正范围集中在[成组方案](./sqlite-hosted-validation-repair-plan.md)，原本地实施见[10 月 31 日记录](../records/sqlite-hosted-validation-repair-2026-10-31.md)。Windows 夹具与模块识别分别由第 8 / 9 节独立批准推进，本地结果见[夹具记录](../records/sqlite-windows-fixture-local-repair-2026-11-04.md)与[模块记录](../records/sqlite-probe-module-local-repair-2026-11-04.md)；最终 `ab37d19e` 单次运行完成本方案既有矩阵。旧候选 `44c13c50` 的模块计数失败仍以[原记录](../records/sqlite-hosted-native-fixture-run-2026-11-04.md)为准，不回填旧身份；本页不授权重复执行。

## 1. 资源核对与目标

- 本机仍是 ARM64；上轮 Linux x64 属于仿真，不覆盖原生 AMD64。仓库 self-hosted runner API 返回 0 个，不将标签定义写成已获得执行资源。
- 本机存在 UTM.app；已知 CLI 不在 PATH，直接 `--help` 以 -6 退出；读取注册偏好遇到权限限制，未确认 VM 的架构 / 状态。没有启动 VM、解析 bookmark、扫描 SSH / 网络或修改权限；不能据此宣称本机没有 VM。
- 所有者已明确优先 GitHub 托管 runner。GitHub 官方列明以下三个标签对应 x64 / Intel，仍需每次 job 验证实际架构与 runner image metadata：[托管 runner 参考](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)。
- 准备阶段远端 `dev` 只读核对为 `29e7030122bc2462262ebd714964106a00242c65`，本地为 `174dd1ba`；该远端快照不含 SQLite 统一代码。默认分支是 `master`。随后只实施获批的本地工作流 / 编排器改动，没有 push、分支创建或 dispatch。

| 目标 | 固定 runner 标签 | 必须验证的真实架构 | RID |
| --- | --- | --- | --- |
| Linux AMD64 | `ubuntu-24.04` | runner / OS / dotnet / Node 为 x64，ELF 为 x86-64 | linux-x64 |
| Windows x64 | `windows-2025` | runner / OS / dotnet / Node 为 x64，MSVC target 为 x64 | win-x64 |
| Intel macOS | `macos-15-intel` | runner / uname / dotnet / Node 为 x86_64 / x64，Mach-O 为 x86_64 | osx-x64 |

不是 `*-latest`，也不以 Windows ARM 的 x64 翻译或 Apple Silicon 的 Rosetta 执行替代上述原生目标。Hosted runner 的镜像由 GitHub 更新，不能按这些标签锁定某个历史 ImageVersion；每次记录 ImageOS / ImageVersion、编译器和 SDK，实际架构或所需工具不符则停止，不自动换平台。

## 2. 工具与依赖输入

| 输入 | 本次拟定值 | 边界 |
| --- | --- | --- |
| .NET SDK | `10.0.403` | 官方 release metadata 已核对三个 RID 的 archive URL / SHA512，见准备清单 |
| .NET / ASP.NET runtime | `10.0.14` | 不自动选择预装的其他 SDK / preview |
| Node | `24.22.2` | 官方三个 x64 archive 的 SHA256 已取得；不 `npm ci / install` |
| Rust | `1.96.0`，minimal + rustfmt / clippy | 仅 CI runner，目标分别 gnu / msvc / apple-darwin；不更新 Cargo.lock |
| .NET 应用包图 | 219 项批准 package/version | SQLite / NoDrive / Npgsql / Hangfire 版本沿用当前源码，漂移即停 |
| Linux native build / runtime | 上轮冻结的 amd64 Node / Rust / SDK / ASP.NET manifest digest | 只重新执行原生 AMD64 四 final，不重复 ARM64 镜像 |
| 扫描器 | Trivy 0.70.0 | 使用同版本 amd64 scanner manifest `sha256:85e87be1a96459c38a4eea47dc64eb2d342bb14cd4b4cef96adcf6ff03378b7c`；该 digest 来自上轮 registry metadata，执行前再核对 config / 版本 |

Action 版本在本次只读 API 查询时解析为以下 commit；实施不回退浮动 tag：

| Action | 精确 commit |
| --- | --- |
| `actions/checkout` v5 | `fbc6f3992d24b796d5a048ff273f7fcc4a7b6c09` |
| `actions/setup-node` v5 | `a0853c24544627f65ddf259abe73b1d18a591444` |
| `actions/setup-dotnet` v4 | `67a3573c9a986a3f9c594539f4ab511d57bb3ce9` |
| `actions/upload-artifact` v4 | `ea165f8d65b6e75b540449e92b4886f43607fa02` |

为避免 runner 预装的更高 SDK 抢占，拟在 job 的一次性工作副本中生成限定 `10.0.403 / rollForward=disable / allowPrerelease=false` 的 global.json，保存其内容 / hash；不提交为仓库永久 SDK 策略。Node / Rust / MSVC / Xcode / gcc 实际版本均在还原前记录，缺少必需组件不临时扩大安装范围。Windows 从完整且带 C++ 组件的 VS 实例加载 x64 开发环境，只提取编译所需的十项白名单变量，定位并核对同目录的 cl.exe / link.exe，保存版本及 hash；不固定 VS 2022，锁定的 find-msvc-tools 0.1.15 已含 VS 2026 支持。Unix 记录实际 cc / c++ 或 Xcode clang 路径与 hash。Windows / Linux 编译并链接不执行的 C++ 样本；macOS 在任务内固定 Developer Directory，经 `xcrun --sdk macosx` 取得同一 SDK / clang / clang++，设置 SDKROOT 和 macOS 13.0 deployment target，分别链接实际引用系统库和 C++ 标准库的 C / C++ 样本。保留 clang++ 调用名称，realpath 仅用于身份与 hash。所有样本核对目标 x64 架构；目标专用 CC / CXX / Rust linker 与后续 Rust、vendored C / C++ 共用这套输入。

## 3. 已批准的本地 CI 改动

实施仅覆盖两类执行入口及配套契约测试：

1. `Scripts/ci/validate-sqlite-platform.mjs`：便携的任务编排器，调用既有 native producer、SQLite gate、runtime probe 和漏洞策略；负责独立目录 / case、结果文件存在性、完整 TRX、超时、资源所有权与 artifact 过滤。原有 checker / contract / 业务启动代码不改，不为平台增加 provider 兜底。
2. `.github/workflows/native-images.yml`：复用已存在的 workflow_dispatch 入口，新增可选 `validation_scope`，默认 `native-images` 保持既有 workflow_call / 普通手动行为；选 `sqlite-platforms` 时仅运行本方案的三个 native job 与四个 amd64 Docker job，避免连带执行旧 ASan / 五平台重复矩阵。现有 Repo Quality / required checks / 发布 workflow 不改。

新模式仅手动执行，`permissions: contents: read`，checkout 不持久化 credentials，不使用生产 secrets、package write、GHCR 登录、tag、Release 或 deploy。不要使用 `pull_request_target`。

不选择“先创建 PR 再决定是否执行”：现有 Repo Quality 对 `dev / master` 的 PR 自动运行，会额外安装 npm 依赖、启动 PG 并执行其他门禁。普通 `push → dev` 不自动 CI，因此先完成并审阅本地实现，再单独确认 `push dev + 指定手动运行` 的外部范围。

GitHub 要求 dispatch 的 workflow 存在于默认分支；这里复用默认 master 已有的 `native-images.yml` 名称 / ID，不通过本批将新 workflow 强行合入 master。参考 [workflow_dispatch 条件](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_dispatch)、[dispatch API](https://docs.github.com/en/rest/actions/workflows#create-a-workflow-dispatch-event)。实施后须先核对远端目标 ref 的文件和输入；如目标 ref 的新增输入不能被服务接受，停止并报告，不改默认分支或偷偷触发其他发布入口。

## 4. 精确矩阵

### 4.1 三个平台 job

每个 job 独立执行，`fail-fast: false`，上限 45 分钟；验证步骤上限 38 分钟，为后处理留出预算，工具准备耗时仍计入 job 总时限：

```text
native-linux-x64   → ubuntu-24.04
native-win-x64     → windows-2025
native-osx-x64     → macos-15-intel
```

每个 job 的 CLI 内容保持一致，由 Node 编排处理平台路径 / 环境：

```bash
rustup toolchain install 1.96.0 --profile minimal --component rustfmt --component clippy
node Scripts/build-native-images.mjs --rid=<当前RID> --debug
node Scripts/build-native-images.mjs --rid=<当前RID>
dotnet restore Radish.slnx --force-evaluate --warnaserror
dotnet package list --project Radish.slnx --vulnerable --include-transitive --format json --no-restore
dotnet build Radish.slnx -c <Debug|Release> --no-restore --warnaserror
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -c <Debug|Release> --no-build --no-restore --filter <下列过滤器> --logger <独立TRX> --results-directory <本job证据目录>
```

过滤器沿用 Linux 批次：

```text
(FullyQualifiedName~SqliteNativeContractTests|FullyQualifiedName~AuthOpenIddictMigrationTest|FullyQualifiedName~DbMigrateBootstrapTests|FullyQualifiedName~ImageProcessorTest|FullyQualifiedName~AttachmentServiceUploadContractTest)&Database!=PostgreSQL
```

四宿主各以两个配置、普通 / 显式 RID 输出分别执行 publish，RID 带 `--self-contained false`；使用现有 Node gate 与原 deps / runtimeconfig 执行冷探针。先普通图并存档，再 RID restore，下一配置前恢复普通图，防止图覆盖。模块识别修正新增 22 项行为用例后，预期每配置 82 个目标用例；发现数量以实际为准，零发现、目标用例跳过或不完整 TRX 不通过。旧候选每配置 60 项的历史结果保持原口径。

| 项目 | 每个平台 | 三平台合计 |
| --- | ---: | ---: |
| 严格 solution build | 两配置 | 6 组 |
| 定向测试 | 两配置，预期各 82 次 | 预期 492 次，实际留痕 |
| 四宿主 publish | 2 配置 × 2 布局 × 4 宿主 = 16 | 48 份 |
| 按产物编译探针 | 每份 publish 单独编译，共 16 | 48 次 |
| 独立冷进程 | 每布局 / 配置 12 个，共 48 | 144 个 |
| API 图片 self-test | 两配置 × 两布局 = 4 | 12 次 |

使用原 contract 对应的 Windows / Intel macOS / Linux native SHA256；特别核对 Windows provider 的实际托管 TFM，不套用 Linux provider DLL hash。每个输出检查后可清理二进制，保留日志 / 清单，控制 runner 磁盘峰值。

publish / 探针的展开命令如下，参数由第 1 节 RID 和固定矩阵生成，不直接复制带占位符的命令执行。`host` 只取 `Radish.Api / Radish.Auth / Radish.Gateway / Radish.DbMigrate`，`configuration` 只取 Debug / Release；所有目录在本 job 的 runner.temp 下，新建且拒绝覆盖：

```bash
dotnet publish "$host/$host.csproj" -c "$configuration" --no-restore --warnaserror -o "$genericOutput"
dotnet restore "$host/$host.csproj" -r "$rid" --force-evaluate --warnaserror
dotnet publish "$host/$host.csproj" -c "$configuration" -r "$rid" --self-contained false --no-restore --warnaserror -o "$ridOutput"
node Scripts/check-nuget-notices.mjs --assets
node Scripts/check-sqlite-native.mjs --assets="$assets" --framework=net10.0 --rid="$reportRid" --output="$output" --deps="$host.deps.json" --report="$report"
node Scripts/run-sqlite-runtime-probe.mjs --compile-from="$output" --host="$host" --scratch="$caseScratch"
node Scripts/run-sqlite-runtime-probe.mjs --output="$output" --host="$host" --scratch="$caseScratch"
dotnet "$apiOutput/Radish.Api.dll" --check-native-images
```

`reportRid` 普通图为空，RID 图为对应 RID；assets 必须与当时的 publish 图对应。每个 output 用对应 host 在全新 caseScratch 编译并执行，清单绑定宿主能力、SDK、源码 / 引用 / 其他根目录 DLL、原 deps / runtimeconfig 和探针 hash；编译引用只取原 deps 的托管 runtime 资产。必需程序集缺失直接失败，不因文件是否存在缩减 mode，也不跨产物复制复用；既有探针文件 / 缺失 JSON / 残留数据均报错。Node 编排直接用 argv 启动子进程，Windows 不依赖 Bash 或把上述 `$变量` 原样传给 PowerShell。

### 4.2 原生 AMD64 Docker job

拆成四个 `ubuntu-24.04` job，service 为 api / auth / gateway / dbmigrate；每个上限 45 分钟，避免一个 14 GB 标准 runner 同时保留四套 SDK build image / 包缓存。开始记录实际剩余磁盘；不足 8 GiB 则停止，不删除 runner 无关预装工具腾空间。

Docker 验证步骤同样限时 38 分钟、job 45 分钟。每个 job 从源码中的既有 Dockerfile 生成只固定 FROM 的副本，引用[已冻结 amd64 输入](../records/sqlite-linux-platform-inputs-2026-10-31.json)，使用原配置 `Release / --load / 不push`。分别核对 build stage 原始图 / 包 / 文件与 final payload，分别按本镜像宿主编译探针，四 final 共四次；运行只读、断网、限额探针；API 四 mode，Auth 三、Gateway 二、DbMigrate 三，共 12 mode；API 另做 native image self-test。

原生 amd64 scanner 对本 job 导出的 archive 静态扫描；记录 scanner、DB metadata / 内容 hash，使用当前 HIGH / CRITICAL 策略和空例外清单。不同 job 的 DB 快照分别留痕，不声称四份共享同一物理缓存。本地八镜像结果与本批四个原生 AMD64 结果分开记录。

Docker CLI 采用 runner 本地引擎，不使用本机 `orbstack` context。`taskId` 由 run ID / attempt / service 组成；基础引用从冻结 JSON 的 `platforms.amd64.images` 读取，scanner 为第 2 节完整引用。构建与探针命令如下，build stage 使用同一命令增加 `--target build` 和独立 tag：

```bash
docker buildx build --platform linux/amd64 --load --progress plain \
  --build-arg BUILD_CONFIGURATION=Release \
  --label "io.radish.sqlite-hosted=$taskId" --label "org.opencontainers.image.revision=$candidate" \
  -t "$taskId:final" -f "$frozenDockerfile" "$sourceRoot"
docker run --rm --name "$taskId-probe-$mode" --label "io.radish.sqlite-hosted=$taskId" \
  --platform linux/amd64 --user "$runnerUid:$runnerGid" --network none --read-only --memory 1g --cpus 2 --pids-limit 256 \
  --cap-drop ALL --security-opt no-new-privileges --tmpfs /tmp:rw,nosuid,nodev,size=256m \
  --mount "type=bind,src=$probeDll,dst=/app/SqliteNativeRuntimeProbe.dll,readonly" \
  --mount "type=bind,src=$caseRoot,dst=/probe-data" --entrypoint dotnet "$taskId:final" exec \
  --runtimeconfig "/app/$host.runtimeconfig.json" --depsfile "/app/$host.deps.json" \
  /app/SqliteNativeRuntimeProbe.dll "$mode" /probe-data
docker save --output "$archive" "$taskId:final"
```

runner uid / gid 作为所有写入任务挂载目录的容器身份；记录 final Config.User、实际 --user 和目录所有权。每个 case 新建 0700 目录，在同一 final、身份、挂载与限制下执行容器写入 / 回读 / 宿主文件所有者核对 / 删除前置检查；独立 0500 目录验证拒绝写入，不换 root 或放宽权限重试。API 图片自检共用相同身份与隔离参数，scanner 的 cache / scan 目录同样验证所有权。

scanner 的 DB 准备与断网 archive 扫描沿用[Linux 方案第 5.4 节](./sqlite-linux-platform-validation-plan.md#54-最终镜像扫描)，将 context 改为 runner 本地引擎、平台改为 linux/amd64；仍不挂载 Docker socket、不加漏洞例外。扫描完成即删除本 job 的大 archive，再上传经过过滤的证据；不上传镜像归档。

## 5. 端口、数据、证据与清理

- 不启动业务宿主、PG / Redis / Valkey，不发布 Docker 端口；本机 15432 不参与。使用 SQLite 内存库、runner 临时目录和临时 Hangfire SQLite storage / 专用队列。
- 当前任务禁用 MSBuild server / node reuse / shared compilation。命令启动前持久化参数与日志位置，启动后立即记录 PID，结束后记录退出码 / signal / 超时 / 取消 / completed。编排器处理 SIGINT / SIGTERM，Unix 终止自有进程组，Windows 对当前自有 PID 等待 taskkill /T /F 结果。清理重入若发现未完成或终止失败的命令，只报告未确认，不凭持久化旧 PID 杀进程。
- 自有容器 / 镜像按 ID、标签和 owner marker 清理；清理报告先写未完成，实际删除目录后才写 completed=true / workingTreeRemoved=true。失败保留错误与原验证结果，不能把清理失败写为整体通过。不按全局进程名或模糊前缀删除。
- 探针成功或失败都在 finally 提取 result.json / hangfire-result.json / job-states.json / process.log / summary / probe-inputs.json / probe-manifest.json，编译 SDK / restore / build 日志同样保留；拒绝符号链接，不复制 DLL、数据库或完整编译目录。每份 native publish 额外保存非空 notices 文件清单、size 和 hash。
- 每个 job 仅上传日志、完整 TRX、JSON、native / deps / notices 哈希清单，artifact 明确保留 14 天。上传前验证没有数据库、密码 / 业务连接、证书私钥、publish 可执行产物、NuGet cache 或完整编译输出。
- 不把 CI artifact 当作 Release asset / 正式源码发行入口。原始 artifact 到期前将审阅结果和所需证据纳入明确本地归档；长期摘要进入 Docs/records。
- 上传条件是最终白名单校验产出的 publish_safe=true，与 cleanup 步骤是否成功分离：安全的失败证据允许上传，清理失败仍让 job 失败。不安全证据拒绝上传，仍可读取 Actions 日志。
- 任务自己的目录 / 容器 / 镜像先自行清理，不 prune 他人缓存，不主动修改网络代理或系统信任。硬终止或 runner 丢失可能来不及完成 finally；剩余资源依赖托管 VM 生命周期清除，只记录实际取得的清理证据，不宣称独立观察到 VM 销毁。

## 6. 远端操作的后续确认清单

四个指定 SHA 的 push 和各一次 dispatch 已按各自授权完成：`915f6359` 见首次记录，`daf5a4d4` 见[第二次记录](../records/sqlite-hosted-native-second-run-2026-10-31.md)，`44c13c50` 见[夹具候选运行](../records/sqlite-hosted-native-fixture-run-2026-11-04.md)，`ab37d19e` 见[模块修正候选运行](../records/sqlite-hosted-native-module-run-2026-11-04.md)。以下只保留后续候选模板，不授权重复执行；新的完整 SHA、范围和远端执行仍需单独确认：

```bash
git push origin "$candidate:refs/heads/dev"
gh workflow run native-images.yml --repo laugh0608/Radish --ref dev \
  -f validation_scope=sqlite-platforms -f expected_head_sha="$candidate"
gh run list --repo laugh0608/Radish --workflow native-images.yml --branch dev --event workflow_dispatch
gh run watch <本批run-id> --repo laugh0608/Radish --exit-status
gh run download <本批run-id> --repo laugh0608/Radish --dir <本批本地证据目录>
```

push 前读取最新远端 dev 并检查快进关系，禁止 force / rebase；执行前冻结完整 head SHA，push 后重新核对远端 dev、工作流 blob 和输入，run 的 headSha 必须相同。七个 job 在安装工具之前校验 expected_head_sha 与 GITHUB_SHA，并按 github.sha checkout；编排器创建任务目录前再次检查批准值，执行前再核对实际 checkout。输入缺失或不匹配立即失败，不自动换 SHA 或重试。只跟踪 / 取消本批明确 run ID，不取消其他任务；无 tag、master 合并、镜像推送、Release、部署或上游通信授权。原生平台通过也不替代 PG 回归、Hangfire 完整对应源码材料与合并前候选门禁。

## 7. 本地接线与后续运行约束

- 编排器入口固定为 `node Scripts/ci/validate-sqlite-platform.mjs --mode=native --rid=<linux-x64|win-x64|osx-x64>`，或 `--mode=docker --rid=linux-x64 --service=<api|auth|gateway|dbmigrate>`；仅接受真实 `workflow_dispatch` 环境及与 run SHA 相同的 checkout，不在本机伪造 Actions 环境执行平台矩阵。
- 三个 native job 与四个 Docker job 各有独立目录、owner marker、容器 ID / image ID；`--mode=cleanup` 用于工作流 always 步骤重入核对与精确清理。artifact 上传依赖该步骤明确输出 publish_safe=true；数据库 / 二进制 / 私钥 / 连接密码或符号链接进入证据会拒绝上传。
- 普通图精确比较批准的 219 项，RID 图只额外允许本 SDK 对应的指定 x64 runtime / apphost pack；所有实际输入继续留痕。generic / RID 分批完成，输出及其原始 assets、实际冷加载结果与 notices hash 分别保存。
- 宿主执行验证 `RUNNER_ARCH`、OS / Node / dotnet / Rust 架构以及 ELF / PE / Mach-O；macOS 拒绝 Rosetta。Docker 还要求 runner 本机 Unix socket、原生 amd64 daemon 与既有 docker driver，不安装新 builder 或自动连接远端引擎。
- 三个 native job 在正式构建前执行实际 MSBuild argv 夹具；Windows 24 组、Unix 12 组，包含驱动器根 / UNC 的 Windows 组不访问网络共享。七 job 执行新增宿主 / SDK 契约，Docker job 另执行容器身份契约；这些前置检查单独计数，不混入目标测试 / 冷进程数。
- 初次 / 第二次托管运行及其本地接线结果保持各日期记录的原口径；后续夹具和模块修正经独立批准推进，最终本次三原生平台及四 final 全矩阵通过。Windows 双配置 / publish / 冷进程已取得真实证据，模块分类与实际加载身份以[最新记录](../records/sqlite-hosted-native-module-run-2026-11-04.md)为准；不把旧失败改写为通过，也不把本批成功扩大为其他专题或发布验收。
