# SQLite Linux 双架构执行准备与验证方案

> 2026-10-31（Asia/Shanghai）。**只读核对完成，执行尚未授权；MCR 输入冻结尚未就绪。** 本文承接[SQLite 统一方案](./sqlite-native-unification-plan.md)，不替代其依赖与平台边界。机器可读盘点见[准备证据](../records/sqlite-linux-platform-readiness-2026-10-31.json)。
>
> 本批先验证 Linux ARM64 与 AMD64 的四宿主输出和最终镜像。ARM64 使用本机原生 Linux 引擎；本机 AMD64 属于仿真验证，不能据此关闭原生 AMD64 验收。Windows x64、Intel macOS 与 PostgreSQL 联调另排。

## 1. 当前结论与停止线

- 源码基线：`dev / 848fd007`；开始核对时工作区干净。本轮只增加方案、盘点证据和入口链接，没有安装、restore、build、test、publish、拉取镜像或创建 / 启动容器。
- 本机工具足以组织 Linux 构建：Docker / Buildx 已有且连接本地 OrbStack。Buildx 声明支持两种架构，实际运行能力仍待批准后测试。
- 没有 Linux Rust 原生产物，必须分别在目标架构重建；缓存 Rust 镜像是 **1.96.1**，不能替代批准的 **1.96.0**。已取得后者两架构的官方 manifest / config 元数据。
- 本机没有漏洞扫描器。建议临时使用与现有 CI 相同版本的 Trivy **0.70.0** 容器，扫描本地导出的镜像 archive，不挂载 Docker socket，不在宿主安装扫描器。
- **MCR 阻断**：`mcr.microsoft.com/dotnet/sdk:10.0`、`aspnet:10.0` 的 registry 查询超时，既无可复用的已核实本地缓存，也未取得 manifest digest / 实际版本 / OS。不能把宿主 SDK 10.0.300 / runtime 10.0.8 填成容器版本。
- **放行条件**：先补齐 MCR 两架构 digest、版本、OS 和完整命令输入表，再提交所有者确认后执行。本文中的 MCR 空项不是授权通配符；本轮不建议直接放行容器执行。

## 2. 工具链、缓存与平台资源

| 项目 | 已核对事实 | 执行含义 |
| --- | --- | --- |
| 宿主 | macOS 27.0.1 / ARM64；SDK 10.0.300，runtime 10.0.8；Node 24.16.0；Rust / Cargo 1.96.0 | 已有本机 SQLite 证据，不代表 Linux 已通过 |
| Docker | CLI / Engine 29.8.2；OrbStack 2.2.5；context `orbstack`，本地 Unix socket | 不切换 context、不连接远端 daemon |
| Buildx | 0.37.2；现有 `orbstack` builder，docker driver，BuildKit 0.33.1 | 不 bootstrap、不创建 builder、不安装 binfmt / QEMU |
| Linux 引擎 | aarch64，10 CPU，16,818,991,104 bytes 内存 | ARM64 原生；AMD64 仿真须明确标注 |
| 磁盘 | 复核时可用约 352 GiB，数值随共享任务变化 | 执行前重查；建议剩余不足 80 GiB 时暂停 |
| ELF 工具 | 宿主有 `file` / `objdump`，无 `readelf` / `llvm-readelf` | 完整动态依赖检查放在已批准的 Rust / SDK 构建环境；缺工具则报告，不临时 apt 安装 |
| 扫描器 | 宿主无 Trivy / Grype / Syft；未缓存 Trivy 镜像 | 下载精确 scanner 镜像和漏洞库也纳入后续授权 |
| 其他平台 | 未确认本次可用的 Windows x64 / Intel macOS 原生 runner；无 PowerShell CLI | 不启动 VM、不安装工具；现有 workflow 声明不等于本次资源已验证 |

盘点过程中 Docker 状态发生外部变化：初读无容器，后续发现其他项目的一个运行容器、一个已退出容器，以及既有 volumes / network。它们都不是本任务资源；不读取其数据、不停止或清理。执行前重新保存资源基线，串行推进，避免与其他任务抢占资源。

### 2.1 本地缓存不能直接充当冻结输入

| 缓存 | 实际身份 | 处理 |
| --- | --- | --- |
| `node:24-bookworm-slim` | ARM64，Node **24.21.0**；digest `0e0ff40c…` | 当前远端同 tag 已是 **24.22.2**；不混用缓存与远端 |
| Rust 无 tag 镜像 `a339861a…` | ARM64，Rust **1.96.1** | 排除，不能改 tag 冒充 1.96.0 |
| PostgreSQL 17 / 16 缓存 | ARM64，17.11 / 16.15，完整 digest 在证据中 | 仅盘点，本批不启动、不复用数据卷 |
| .NET SDK / ASP.NET、Trivy | 未发现对应已核实缓存 | MCR 停止线；扫描器另按下表冻结 |

### 2.2 已取得的远端平台 manifest

以下来自 registry manifest 与 image config 的只读读取，没有下载镜像层。使用 **平台 manifest digest**，不把本地 image ID、index 或 attestations 混为一谈。

| 镜像与实际版本 | linux/amd64 | linux/arm64 |
| --- | --- | --- |
| `node:24-bookworm-slim`，24.22.2 | `sha256:a43f7fc3e52e87d12a905f46eea0275def84ca8f2981a5b53fed44d7fb398ee0` | `sha256:e80dee4ed3709cf1f8004e34ecfb90df71b7eac8ba0d7d6222ba238a823d1eea` |
| `rust:1.96.0-bookworm`，1.96.0 | `sha256:c993d32d95cc146bd12c84d66f0b924a6a96f3988325f39c144f2f9893dea120` | `sha256:05f85ef6eb0f2600ac62e9d779642b5b362d5be2ed67b80e162fae6e28b66524` |
| `mcr.microsoft.com/dotnet/sdk:10.0` | **未取得，禁止执行** | **未取得，禁止执行** |
| `mcr.microsoft.com/dotnet/aspnet:10.0` | **未取得，禁止执行** | **未取得，禁止执行** |

Scanner 在宿主原生 ARM64 运行，固定 `aquasec/trivy:0.70.0@sha256:06c0a1359e4d745c6d709f5b64421db79fe0b5602c87d6eb5d6ba7d3d79d91ab`，可静态扫描两架构 archive。其版本与现有 CI 固定 action 的 [默认版本](https://github.com/aquasecurity/trivy-action/blob/ed142fd0673e97e23eac54620cfb913e5ce36c25/action.yaml#L96)一致。

MCR 恢复后需记录四个 manifest 及其 config，核对 .NET 10、平台、发行版和 glibc；不改用未经核对的镜像源。若远端输入出现阻断漏洞或需要变更版本 / 发行版，先重新确认方案。

## 3. 原生产物与现有构建接线

### 3.1 SQLite

唯一输入继续为 `SQLitePCLRaw.lib.e_sqlite3 [3.53.3]`；托管层 config / core / provider 3.0.3 与 Microsoft Core 10.0.0 不变。

| 文件 | bytes | 批准 SHA256 |
| --- | ---: | --- |
| `runtimes/linux-x64/native/libe_sqlite3.so` | 1470784 | `b0f5c48026fedcf9f05cf31c6fadf9e1e001ca59cd85aa68b8b6814e52c18a07` |
| `runtimes/linux-arm64/native/libe_sqlite3.so` | 1534296 | `707fff6b18c1f083158e7a543c8d2545d5485f547ffd96db29679a95f52878d5` |

缓存文件已与契约比对：ELF machine 分别为 62 / 183，`file` 分别识别为 x86-64 / AArch64。两者可见 `GLIBC_2.34` 字符串；这只是静态提示，完整动态 version requirements、DT_NEEDED 和实际加载仍待 Linux 验证。不能把 Rust 的 bookworm 构建基线自动套到 SQLite 二进制。

source ID、所有托管文件及其他 native 文件哈希继续以 `Scripts/sqlite-native-contract.json` 为准；最终探针必须回报该 source ID、3.53.3、唯一实际加载路径及对应架构的 SHA256。

### 3.2 Rust 图片库

- `artifacts/native` 当前只有 `osx-arm64/Debug` 和 `Release`；没有 Linux artifact。旧 `artifacts/image-publish` 也不能作为本候选 Linux 证据。
- ABI 为 `65536`；Cargo.lock SHA256 为 `b49f8c1f810f6daa259407345af50d91c8f84b16d3423893405507816ec61b90`。沿用当前字体、licenses、vendored C 与 `--locked`，不更新 Cargo.lock。
- API Dockerfile 在目标架构的 Rust bookworm stage 生成 Release artifact，再由对应 SDK 消费；不复制宿主 Mach-O。
- API Dockerfile 原生 stage 固定生成 Release。**不能只传 `BUILD_CONFIGURATION=Debug` 就宣称 Debug artifact 已覆盖。** Debug 在隔离验证镜像中显式执行 `build-native-images.mjs --debug`。

### 3.3 四宿主镜像

API、Auth、Gateway、DbMigrate 的现有 Dockerfile 均已有 Node 构建阶段接线，最终 runtime 不含 Node。本方案不修改正式 Dockerfile、runtime 入口、数据库配置或 Compose。

为复现输入，批准后在任务目录生成 Dockerfile 副本，唯一差异为将四类 `FROM` 替换为本次平台 digest，并保存副本及 diff。Auth / Gateway 既有 `apt-get install openssl ca-certificates` 仍访问发行版仓库：须记录实际 Debian 包版本和最终 image ID；只锁 FROM 不能保证镜像逐字节重建。本批不引入 apt snapshot 或改写包安装策略。

已有 `.github/workflows/native-images.yml` 只给 API 最终镜像做原生图片探针 / 扫描，不覆盖四宿主 SQLite。本次不触发 workflow、不推送代码或镜像，也不借用固定的 `radish-image-check:*` 标签。

## 4. 端口、数据与资源影响

| 步骤 | 网络 / 端口 | 写入与运行影响 |
| --- | --- | --- |
| registry 只读核对 | 外连 HTTPS 443，无监听 | 任务证据目录保存 JSON |
| 镜像构建 / restore / Cargo | registry、nuget.org、Cargo 与现有 apt 仓库的外连；无 `-p` | Docker 镜像层 / BuildKit cache、隔离源码副本中的 obj / bin / 包缓存 |
| SDK 定向测试 | restore 后 `--network none`；不启动三宿主 | 容器临时目录中的 SQLite / WAL / 测试数据、证据挂载中的 TRX / 报告 |
| 四宿主 final 探针 | `--network none`，不映射端口 | 内存库、任务专属探针目录和临时 Hangfire SQLite；专用队列，退出时清理 |
| Trivy 库准备 | 仅扫描器访问漏洞库 registry；无监听 | 任务专属 Trivy cache 目录 |
| Trivy archive 扫描 | `--network none`，无 Docker socket | 原始 JSON、策略 JSON 与摘要 |

API 的 5100、Auth 的 5200、Gateway 的 5000 本批均不监听 / 发布；DbMigrate 没有 HTTP 端口，**禁止执行其默认 `apply`**。使用 `--entrypoint dotnet` 覆盖四镜像入口，直接运行独立探针，避免 Auth 证书入口脚本、宿主 seed / scheduler 和迁移程序启动。

不挂载源码工作区、`DataBases`、`Deploy`、用户证书库或任何既有数据卷。PG 端口 `127.0.0.1:15432` 只读查询未见监听，但本批不保留、不绑定该端口，也不启动 PG / Redis / Valkey。此前 43 项 PG 环境用例仍待独立批次。

建议串行构建四宿主，完成 ARM64 后再 AMD64。SDK 测试容器限制 4 CPU / 6 GiB / pids 512；final 探针限制 2 CPU / 1 GiB / pids 256，tmpfs `/tmp` 256 MiB。现有 docker-driver BuildKit 不由这些 `docker run` 限额约束，执行时监测引擎压力；不擅自修改 OrbStack 全局资源。

单个冷探针超时 60 秒、Hangfire 120 秒、单组测试 20 分钟、单镜像构建 45 分钟作为诊断停止点；超时须保留日志并确认进程退出，不把退出码或缺失报告视为通过。

## 5. 待批准的精确命令与执行顺序

以下均为**拟执行命令**。`planRoot`、`sourceRoot`、镜像引用和资源 ID 由任务清单一次生成并留痕；MCR 缺项时必须拒绝生成可执行清单。本轮没有运行下列构建 / 容器命令。

### 5.1 冻结源码与资源名称

```bash
taskId=radish-sqlite-linux-20261031-a7c2
planRoot="${TMPDIR%/}/$taskId"
sourceRoot="$planRoot/source"
candidate=848fd007
```

执行前确认目录 / 标签尚不存在，保存 Git 状态和 Docker 资源 ID 清单。用 `git archive --format=tar --output="$planRoot/source.tar" "$candidate"` 解出干净快照至 `sourceRoot`；不包含本机 `.nuget`、未跟踪配置、bin / obj 或业务数据。提交后若影响代码输入则重定基线，不能把旧 SHA 写成新候选。

输入清单要求每个架构都有 `node / rust / sdk / aspnet` 完整引用、metadata SHA、实际版本和 OS；空值立即失败。后续 `nodeRef / rustRef / sdkRef / aspnetRef` 只从该清单读取，不能在失败时退回 tag。只下载清单中的两架构基础镜像和 ARM64 Trivy；不 `docker login`、不修改正式 Dockerfile。

### 5.2 隔离 Linux SDK 验证环境

批准后生成临时验证 Dockerfile：使用同一平台的 Node、Rust 与 SDK digest，复制干净源码；在 Rust stage 生成两配置 artifact，再交给 SDK stage。新增内容只在任务目录，不引入产品构建接线。

```bash
# Rust stage 内；分别在目标平台执行，不交叉编译。
node Scripts/build-native-images.mjs --rid="$rid" --debug
node Scripts/build-native-images.mjs --rid="$rid"
```

架构映射固定：`arm64 → linux/arm64 → linux-arm64`，`amd64 → linux/amd64 → linux-x64`。验证镜像名 `$taskId:validate-$arch`，容器名 `$taskId-sdk-$arch`；标签 `io.radish.sqlite-linux=$taskId`，另记 candidate SHA。构建均为单平台 `--load`，不 `--push`。

SDK 容器先联网执行一次同版本 restore 并保存依赖图 / NuGet audit，然后断网执行 build / test / publish。容器内工作目录固定 `/src`，证据挂载为 `/evidence`；避免迁移测试寻找 `Radish.slnx` 时失去源码根。

```bash
docker --context orbstack buildx build --builder orbstack --platform "linux/$arch" \
  --load --progress plain --label "io.radish.sqlite-linux=$taskId" \
  -t "$taskId:validate-$arch" -f "$planRoot/dockerfiles/$arch/validate.Dockerfile" "$sourceRoot"
docker --context orbstack run -d --name "$taskId-sdk-$arch" \
  --label "io.radish.sqlite-linux=$taskId" --platform "linux/$arch" \
  --memory 6g --cpus 4 --pids-limit 512 --workdir /src \
  --mount "type=bind,src=$planRoot/evidence/$arch,dst=/evidence" \
  --entrypoint /bin/sh "$taskId:validate-$arch" -c 'exec sleep infinity'
# 联网准备命令用 docker exec 在该容器执行；完成后断网。
docker --context orbstack network disconnect bridge "$taskId-sdk-$arch"
# 下一次普通 / RID restore 确需访问缓存外输入时，临时连接同一默认 bridge。
docker --context orbstack network connect bridge "$taskId-sdk-$arch"
```

上述 connect / disconnect 是按阶段使用的命令，不是顺序运行后保持联网；每次测试 / 探针前 inspect 必须确认无网络连接。各阶段通过 `docker exec` 执行以下容器内命令，并单独保存退出码和完整日志；常驻 SDK 容器最后按第 7 节精确删除。

```bash
dotnet restore Radish.slnx --force-evaluate --warnaserror
dotnet package list --project Radish.slnx --vulnerable --include-transitive --format json --no-restore
dotnet build Radish.slnx -c "$configuration" --no-restore --warnaserror
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -c "$configuration" \
  --no-build --no-restore --filter "$testFilter" \
  --logger "trx;LogFileName=$arch-$configuration.trx" --results-directory /evidence/tests
```

还原图与当前批准版本清单比较，普通图以当前 219 个不同 package/version 条目为基线；新增 RID runtime pack 单独记录。若普通依赖漂移、Native / Managed 来源不符或 NuGet 审计出现阻断问题，停止对应架构，不自行升级依赖或添加警告豁免。

`configuration` 依次 Debug / Release；`testFilter` 固定为：

```text
(FullyQualifiedName~SqliteNativeContractTests|FullyQualifiedName~AuthOpenIddictMigrationTest|FullyQualifiedName~DbMigrateBootstrapTests|FullyQualifiedName~ImageProcessorTest|FullyQualifiedName~AttachmentServiceUploadContractTest)&Database!=PostgreSQL
```

只覆盖本批数据库 / ABI / 附件关联回归，不启动外部服务，不冒充再次全量测试。核对过滤后实际发现数量、TRX completed / failed / skipped 和进程状态；过滤为零、目标 SQLite 用例跳过或无完整 TRX 即失败。

四宿主 `host` 依次 `Radish.Api / Radish.Auth / Radish.Gateway / Radish.DbMigrate`，在每个架构、每个配置分别执行普通输出和显式 RID 输出，输出目录全新且分开：

```bash
dotnet publish "$host/$host.csproj" -c "$configuration" --no-restore --warnaserror \
  -o "/evidence/publish/$arch/$configuration/generic/$host"
dotnet restore "$host/$host.csproj" -r "$rid" --force-evaluate --warnaserror
dotnet publish "$host/$host.csproj" -c "$configuration" -r "$rid" \
  --self-contained false --no-restore --warnaserror \
  -o "/evidence/publish/$arch/$configuration/rid/$host"
```

**顺序约束**：同配置所有 generic 发布 / 报告 / 探针完成后才进行 RID restore；跨配置返回 generic 前重新恢复普通图。RID restore 使用同一联网准备阶段或临时恢复网络，再断网验证；记录实际 RID runtime pack，不能让新 restore 的图覆盖已留存证据。

构建 / publish 自动执行当前 SQLite 和 notices 门禁。另显式执行 `node Scripts/check-nuget-notices.mjs --assets`；保存原始 assets、deps、native manifest、各门禁日志和 notices 文件清单。必要报告命令：

```bash
node Scripts/check-sqlite-native.mjs --assets="$assets" --framework=net10.0 \
  --rid="$reportRid" --output="$output" --deps="$host.deps.json" --report="$report"
```

`reportRid` 普通图为空、RID 图为对应 rid。使用现有脚本在 SDK 容器内编译探针，对 32 份独立 publish 运行冷进程验证；每份按其实际文件选择 Microsoft / SqlSugar / EF / Hangfire，保存结果：

```bash
node Scripts/run-sqlite-runtime-probe.mjs \
  --compile-from="/evidence/publish/$arch/Release/generic/Radish.Api" --scratch=/evidence/probe-compiler
# 每个 output 对应全新 scratch，预先放入上一命令生成的 SqliteNativeRuntimeProbe.dll。
node Scripts/run-sqlite-runtime-probe.mjs --output="$output" --host="$host" --scratch="$caseScratch"
dotnet "$apiOutput/Radish.Api.dll" --check-native-images
```

每个架构先准备 Release generic API 以编译探针，再对各配置 / 布局输出执行；不在原工作区输出插入文件。API 的 generic / RID 输出均做上述图片自检，不启动 HTTP 服务。

### 5.3 八份最终镜像与实际加载

在同一干净快照上，用第 3.3 节的四份 Dockerfile 副本分别构建两架构 Release 镜像：

```bash
docker --context orbstack buildx build --builder orbstack \
  --platform "linux/$arch" --load --progress plain \
  --build-arg BUILD_CONFIGURATION=Release \
  --label "io.radish.sqlite-linux=$taskId" --label "org.opencontainers.image.revision=$candidate" \
  -t "$taskId:$service-$arch" -f "$planRoot/dockerfiles/$arch/$service.Dockerfile" "$sourceRoot"
```

`service` 与宿主映射：`api / auth / gateway / dbmigrate`。保存完整日志、最终 image ID、platform、镜像包清单；另外导出对应 `--target build` 层供 assets / NuGet 源文件门禁核对，不把 build 层当 final 镜像。

使用任务专属停止态容器 `docker create` / `docker cp` 提取 final `/app` 清单，再立即删除该容器；不执行其入口。将 final 文件与 build stage `/app/publish` 的 SQLite / notices 哈希逐项比对，检查 final 不存在 Node、旧 SQLite 来源或不同托管 batteries。

用隔离编译的托管探针 DLL 挂入 `/app`，保留原宿主的 deps / runtimeconfig。每种 first-use mode 使用全新进程 / 目录：

```bash
docker --context orbstack run --rm --name "$taskId-$service-$arch-$mode" \
  --label "io.radish.sqlite-linux=$taskId" --platform "linux/$arch" \
  --network none --read-only --memory 1g --cpus 2 --pids-limit 256 \
  --cap-drop ALL --security-opt no-new-privileges \
  --tmpfs /tmp:rw,nosuid,nodev,size=256m \
  --mount "type=bind,src=$probeDll,dst=/app/SqliteNativeRuntimeProbe.dll,readonly" \
  --mount "type=bind,src=$caseRoot,dst=/probe-data" \
  --entrypoint dotnet "$taskId:$service-$arch" exec \
  --runtimeconfig "/app/$host.runtimeconfig.json" --depsfile "/app/$host.deps.json" \
  /app/SqliteNativeRuntimeProbe.dll "$mode" /probe-data
```

核对 `result.json` 中加载模块只在 `/app` 内、恰好一份、目标架构 hash / version / source ID 正确；托管程序集路径 / hash 与契约一致。探针数据目录不得残留 db / WAL / SHM，Hangfire worker 已停止；若探针模块枚举与 Linux 实际行为不符，先保留证据并定位，不放松判定。

API final 另用同样的断网、只读与限额参数运行 `--entrypoint dotnet <image> /app/Radish.Api.dll --check-native-images`，两架构各一次。

### 5.4 最终镜像扫描

按照现有[漏洞裁决策略](../guide/image-vulnerability-gate.md)判断；这是本批平台验证，不修改全项目发布矩阵或例外清单。保留全部 CRITICAL、可修复 HIGH 阻断规则及无修复 HIGH 报告规则。

```bash
scannerRef=aquasec/trivy:0.70.0@sha256:06c0a1359e4d745c6d709f5b64421db79fe0b5602c87d6eb5d6ba7d3d79d91ab
docker --context orbstack image save -o "$archive" "$taskId:$service-$arch"
docker --context orbstack run --rm --platform linux/arm64 \
  --name "$taskId-trivy-db" --label "io.radish.sqlite-linux=$taskId" \
  --mount "type=bind,src=$scanCache,dst=/cache" "$scannerRef" \
  image --cache-dir /cache --download-db-only
docker --context orbstack run --rm --platform linux/arm64 \
  --name "$taskId-trivy-$service-$arch" --label "io.radish.sqlite-linux=$taskId" \
  --network none --mount "type=bind,src=$scanCache,dst=/cache" \
  --mount "type=bind,src=$archive,dst=/input/image.tar,readonly" \
  --mount "type=bind,src=$scanResults,dst=/results" "$scannerRef" \
  image --input /input/image.tar --cache-dir /cache --skip-db-update \
  --skip-java-db-update --offline-scan --scanners vuln --severity HIGH,CRITICAL \
  --ignore-unfixed=false --exit-code 0 --format json --output /results/trivy.json
node Scripts/evaluate-image-vulnerabilities.mjs --report "$scanResults/trivy.json" \
  --image "radish-$service" --exceptions .github/image-vulnerability-exceptions.json \
  --output "$scanResults/evaluation.json" --summary "$scanResults/summary.md"
```

下载漏洞库一次后八份 archive 共用同一快照，记录 scanner version、DB metadata / 内容 hash 和扫描时间；archive 无需运行目标架构程序。若扫描器需要未准备的额外数据库或离线扫描失败，应停止补齐明确输入，不能将空报告当通过；不得引入 ignore 文件或改变例外。

## 6. 验证矩阵与完成标准

| 验证 | ARM64 | AMD64 | 结论要求 |
| --- | --- | --- | --- |
| 资源 / 镜像输入 | 本机原生 Linux，待执行 | 本机仿真，待执行 | FROM digest、实际版本、OS 与执行方式齐全 |
| Rust Debug / Release | 两份重建 | 两份重建 | Rust 1.96.0、ABI 65536、输入 / 字体 / licenses、ELF 架构及 glibc / DT_NEEDED |
| .NET 严格构建与定向测试 | 两配置 | 两配置 | 原始依赖图、零警告 / 错误、完整 TRX；SQLite / EF / DbMigrate / 图片关联用例不跳过 |
| 四宿主 publish | 两配置 × generic / RID，共 16 份 | 同左 | 合计 32 份；源包 / 输出 / deps / notices 一致 |
| publish 冷加载 | 每配置 / 布局 12 个 mode，共 48 | 同左 | 合计 96 个独立进程；API 4、Auth 3、Gateway 2、DbMigrate 3，缺组件 / 多组件需解释 |
| 四宿主 final | Release 四镜像 | Release 四镜像 | 合计八镜像；每架构 12 个冷探针，API 再做图片自检 |
| Hangfire SQLite | publish 和 API final | 同左 | 16 项真实注册、专用测试任务执行 / 失败重试、worker 停止；不执行业务清理任务 |
| 镜像漏洞策略 | 四份 final archive | 四份 final archive | 八份扫描原报告与 evaluation；遵循原策略 |
| 原生 AMD64 目标运行 | 不适用 | **本机仿真不能关闭** | 后续明确原生 runner 及授权；本轮不触发远端 CI |

现有宿主 macOS ARM64 证据继续保留；Windows x64 / Intel macOS 仍待资源与命令确认。PostgreSQL provider / DbMigrate 接库验证、Redis / Valkey、Hangfire 完整对应源码材料、NoDrive 询证和 SNI 分发均不由本批替代。通用 publish 携带 Windows runtime 资产时如实记录，不新增 ExcludeAssets 或更改 SNI 路线。

失败须停在具体矩阵单元，保留成功项和失败证据，不把编译通过、镜像能导出或 SQLite 文件存在扩大为运行通过。后续原生 AMD64 应复用相同源码和输入 manifest，记录输出 image ID 差异；不能只运行历史图片 workflow 后声称四宿主 SQLite 已验证。

## 7. 清理与回退

- 任务目录固定前缀和一次性 marker；保存自身 PID / container ID / image ID / 标签 / 原始资源基线。每个步骤使用 `try/finally` 或 shell trap；超时后按本任务 ID 停止进程 / 容器并等待退出，禁止按 `dotnet` / `docker` 名称批量杀进程。
- 容器清理先用 `docker ps -aq --filter "label=io.radish.sqlite-linux=$taskId"` 列清单，逐个 inspect 确认 ID / label 后才 `docker rm -f <exact-id>`；不用 `compose down`、`system prune`、`volume prune` 或 `builder prune`。
- 默认不创建 named volume / 自定义网络。数据库只在任务临时目录或容器可写层；清理 db / WAL / SHM 前先确保对应 worker / 进程已停止。
- 镜像只删除本任务记录的候选 tag / image ID。共享基础镜像、Trivy 基础镜像与 BuildKit cache 默认保留并报告新增占用；不为恢复空间删除他人缓存，不承诺构建缓存可逐项回退。
- 先将日志、TRX、manifest、探针 JSON、native / notices 哈希、原始漏洞报告与策略结果归档到 `Docs/records/` 对应本批记录或明确的本地证据目录，再删任务目录中的源码副本、镜像 archive、下载缓存和测试数据。原始大文件无需全部提交 Git，但必须记录路径 / SHA / 保留状态。
- 末次复核本任务标签的容器数量为零、测试进程已退出、任务数据已清理；其他项目资源保持其自身管理，本轮前后状态变化只报告，不自动恢复。
- 无生产 / 既有数据库变更，不需要数据恢复。正式源码 / Dockerfile 未改；若后续发现需要产品接线变更，先明确差异再确认，不能把验证方案批准当作产品改造授权。

## 8. 确认范围

当前可审阅的是上述边界、矩阵、端口和清理策略。**MCR digest / 实际版本仍缺失，完整执行命令清单尚不能放行**；先完成这项只读补证，再请所有者确认：精确基础镜像下载、两架构隔离构建 / restore / 定向测试 / publish、八镜像独立探针和 Trivy 数据库下载 / 扫描 / 清理。

批准也不包含业务宿主启动、PG 启动、VM 修改、包版本更新、正式 Dockerfile 修改、远端 CI 触发、push、tag、Release、部署或上游通信。

Registry 查询方式参考 [Docker imagetools inspect](https://docs.docker.com/reference/cli/docker/buildx/imagetools/inspect/)；archive 扫描参考 [Trivy Container Image / Tar Files](https://trivy.dev/docs/latest/guide/target/container_image/#tar-files)。
