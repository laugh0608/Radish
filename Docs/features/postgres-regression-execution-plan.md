# PostgreSQL 接库回归执行方案

> 2026-10-31（Asia/Shanghai），**本轮已批准、执行通过并清理完成**，见[执行记录](../records/postgres-regression-2026-10-31.md)。准备阶段只读证据见[准备清单](../records/postgres-native-readiness-2026-10-31.json)。本方案保留本轮 PostgreSQL 支持验证范围，不沿用为下一轮服务授权。

## 1. 范围与已经核实的条件

- 冻结源码：`174dd1ba`；开始核对时 `dev` 工作区干净。当前应用包图为 219 个不同 package/version，NoDrive 5.1.4.207、SystemCommon 8.5.0、Npgsql 10.0.0 及 SQLite 统一版本不变。
- 本机 SDK 10.0.300 / runtime 10.0.8、Node 24.16.0、Rust 1.96.0 已有；工作区 osx-arm64 Debug / Release native 的 `--verify` 均通过，只读核对未重建。
- Docker context `orbstack` 指向本机 Unix socket，Linux ARM64、10 CPU、约 15.66 GiB 内存。两份精确 PG 镜像均已缓存；当前无容器，15432 未见监听，临时 MCR 代理排除已为空。
- 准备阶段只读取工具、镜像、源码、既有输出和 GitHub metadata，未执行服务 / 构建；随后所有者明确批准本方案及托管 runner 的本地 CI 接线，仍禁止 push、远端触发、发布和部署。
- 原生平台已按所有者偏好改走[GitHub 托管 runner 专项方案](./sqlite-hosted-native-validation-plan.md)准备，不阻塞这批本机 PG 回归。

## 2. 精确服务输入

| 服务 | 完整平台 manifest 引用 | 镜像配置版本 | 平台 |
| --- | --- | --- | --- |
| PG17 | `postgres@sha256:a63c1ea3f6a3a5a1621f97d2aba3b30faecfab4224c1b0000cc85ced57bb1939` | `17.11-1.pgdg13+2` | linux/arm64 |
| PG16 | `postgres@sha256:a902560563d3a5661228585f53474c5065e7f273b21ffd77e555a8b5682c5690` | `16.15-1.pgdg13+2` | linux/arm64 |

这是既有已验证版本，不宣称最新补丁。本批 `--pull=never`，不下载或升级数据库镜像。缺少缓存、digest / 架构不符时停止，不回退浮动 tag。

| 项目 | 本次拟定值 |
| --- | --- |
| Task ID / 标签值 | `radish-pg-regression-20261031-b6d8` |
| 标签键 | `io.radish.pg-regression` |
| 宿主连接 | `127.0.0.1:15432` |
| 容器端口 | `5432/tcp` |
| 账号 / 初始测试库 | `radish_pg` / `radish_pg` |
| 密码 | `radish-pg-test-only-20261031`，仅新实例的公开合成测试凭据 |
| 资源 | 2 CPU、1 GiB memory、pids 256、shm 256 MiB |
| 数据卷挂载 | 新建、带本任务标签的卷 → `/var/lib/postgresql/data` |
| 顺序 | PG17 全部完成并清理容器 / 卷后，再启动 PG16；同端口串行复用 |

不使用既有数据卷、项目 DataBases / Deploy 路径或外部数据库连接。端口、名称或目录冲突时停止，不切换端口、不停止其他项目。

## 3. 覆盖矩阵与实际缺口

源码静态盘点有 **43 个 PostgreSQL trait 方法，33 个文件**：Auth 3 个、Repositories 40 个，逐项名称与文件 hash 见准备清单。这是源码方法数；执行阶段重新发现当前程序集用例，不用历史数字代替实际 TRX。

| 检查 | PG17 | PG16 | 完成要求 |
| --- | --- | --- | --- |
| 服务器身份 | 一次启动 | 独立一次启动 | digest / ARM64、server_version、current_user、初始库与回环端口正确 |
| 严格构建 | macOS ARM64 Debug / Release 共两组，为两种 PG 共用 | 共用同一批构建 | 0 warning / error，原始资产图保持 219 项 |
| 后端测试 | Debug / Release 全量 | Debug / Release `Database=PostgreSQL` 专项 | PG 不跳过；完整 TRX / 完成摘要与进程退出一致 |
| NoDrive / Npgsql | 全量内的真实驱动、仓储、事务 / UoW、时间语义等 | 相同 PG 专项 | NoDrive 实际 DLL hash、Npgsql 10.0.0 类型和 Int64 参数契约正确 |
| OpenIddict / DbMigrate | 空库、幂等、schema 接管 / 拒绝不完整 schema | 相同 PG 专项 | 显式测试库，迁移和结果一致，不应用到既有库 |
| Hangfire PostgreSQL | Debug / Release 各一次临时探针 | 同左 | 共四次真实 storage / 注册 / 专用任务 / 重试；不是现有 43 方法已经覆盖的内容 |
| 清理 | 删除 PG17 容器 / 卷 | 删除 PG16 容器 / 卷 | 所有权与 ID 核对，测试进程停止，最终本任务资源为零 |

全量仍不启用 Redis / Valkey 上传集成测试；预期环境跳过按当前测试名逐项统计，不能笼统接受任意跳过。此前此类为 102 项，但本批以实际发现 / TRX 为准。测试总数、耗时和跳过数均不预写成通过。

### 3.1 DbMigrate 的数据与权限

`AuthOpenIddictMigrationTest` 会创建 `q2b_openiddict_{main|log|message|chat|auth}_<GUID>` 五个随机库，显式运行两次 `DbMigrate apply` 后核对幂等，并在 finally 中逐个 `DROP DATABASE ... WITH (FORCE)`。因此测试角色需要建库 / 删除自己测试库的权限；仅在本次全新容器中使用 entrypoint 创建的测试管理员。

测试子进程设置 Production 环境标识，但 Main / Log / Message / Chat / OpenIddict 连接均显式覆盖为本次实例的随机库，Redis 与 developer seed 禁用。执行前检查这段代码 / 环境构造仍匹配冻结清单；不直接运行无参数 DbMigrate，不读取或覆盖用户 Local 配置。

### 3.2 Hangfire PostgreSQL 补充探针

PG trait 中没有真实 PostgreSQL worker 探针。准备阶段形成 **135 行、当时未编译 / 未执行** 的临时 C# 审阅稿，路径与 SHA256 记录在准备清单；执行阶段补齐 `System.Diagnostics` using 后在隔离工程严格编译，不改产品源码：

- 沿用 API 实际 `UsePostgreSqlStorage(options => options.UseNpgsqlConnection(...))` 初始化方式；为隔离测试另设 SchemaName。
- 仅接受 `127.0.0.1:15432 / radish_pg / radish_pg`；schema 限定为 `radish_pg_20261031_b6d8_pg{16|17}_{debug|release}`，拒绝其他值。
- 真实调用 `ApiRecurringJobRegistration.Register`，对照已有 SQLite 探针检查 16 项 ID / cron / 时区 / 方法 / 参数；先移除全部注册项，再以单 worker、专用 `postgres-contract-probe` 队列执行无业务依赖的成功 / 失败重试任务。
- 两任务均到 Succeeded，重试尝试为两次，worker 释放后删除本次 schema；记录实际 Hangfire.Core / PostgreSql / Npgsql 程序集路径、版本和 SHA256。
- 使用正式包 Core 1.8.14 与 PostgreSql 1.20.12，不替换为源码重建 Core，不混入 LGPL 修改库实验。
- 临时工程只用已有 SDK 的 FrameworkReference 与当前 API 构建输出的 Reference，无 PackageReference；编译与运行属于本轮批准范围，实际结果另行留痕。

## 4. 已批准命令与隔离方式

以下为本轮已批准的命令形态；实际展开参数、退出码、资源与清理结果单独归档。

### 4.1 源码与构建输入

```bash
pgTask=radish-pg-regression-20261031-b6d8
pgRun="${TMPDIR%/}/$pgTask"
pgSource="$pgRun/source"
pgRevision=174dd1ba
```

先确认目录 / 名称 / 标签不存在，保存当前资源 ID，创建 owner marker、evidence、tmp、http-cache、cli-home 等目录；用 `git archive --format=tar --output="$pgRun/source.tar" "$pgRevision"` 解到 pgSource，不复制 Local 配置、业务数据、bin / obj 或 `.git`。

只向隔离副本复制批准的 219 项 `.nuget/packages/<id>/<version>` 缓存及已验证的 osx-arm64 两配置 native；不复制无关旧版本或 Hangfire 工具 feed，不写用户全局包缓存。随后在副本中执行：

```bash
node Scripts/build-native-images.mjs --verify --rid=osx-arm64 --debug
node Scripts/build-native-images.mjs --verify --rid=osx-arm64
dotnet --version
dotnet restore Radish.slnx --force-evaluate --warnaserror
dotnet package list --project Radish.slnx --vulnerable --include-transitive --format json --no-restore
dotnet build Radish.slnx -c Debug --no-restore --warnaserror
dotnet build Radish.slnx -c Release --no-restore --warnaserror
```

固定 SDK 10.0.300 / runtime 10.0.8；若解析到其他 SDK，停止，不安装新版本。还原 / 审计仅使用 NuGet.Config 的 nuget.org，HTTP cache / CLI home / TMPDIR 只在本次子进程指向 pgRun；不改 HOME 或宿主全局配置。保存新 assets 与版本差异，普通应用图不是批准的 219 项或有阻断漏洞即停，不加 NoWarn / ExcludeAssets 或升级包。

当前已有足够 native，若复制后 verify 失败，不自动 Cargo 重建或安装工具。本批测试复用已有产物门禁，四宿主 publish 已在前批验证，本轮不为接库回归重复整套 publish。

### 4.2 串行服务启动

每轮 pgMajor 为 `17 / 16`，pgImage 取第 2 节相应完整 digest，pgContainer / pgVolume 分别为 `$pgTask-pg$pgMajor` / `$pgTask-pg$pgMajor-data`：

```bash
docker --context orbstack volume create --label "io.radish.pg-regression=$pgTask" "$pgVolume"
docker --context orbstack run -d --pull=never --platform linux/arm64 \
  --name "$pgContainer" --label "io.radish.pg-regression=$pgTask" \
  --cpus 2 --memory 1g --pids-limit 256 --shm-size 256m \
  -p 127.0.0.1:15432:5432 \
  --mount "type=volume,source=$pgVolume,target=/var/lib/postgresql/data" \
  -e POSTGRES_USER=radish_pg -e POSTGRES_DB=radish_pg \
  -e POSTGRES_PASSWORD=radish-pg-test-only-20261031 \
  --health-cmd='pg_isready -U radish_pg -d radish_pg' \
  --health-interval=2s --health-timeout=3s --health-retries=30 "$pgImage"
```

最多等 60 秒 healthy，保存 inspect 的 container ID / image ID / label / localhost PortBindings；再执行 `docker exec "$pgContainer" psql -U radish_pg -d radish_pg -Atc 'SELECT version(), current_database(), current_user'` 并读取 `SHOW TimeZone`。任何身份或版本不符即停，不只依赖 pg_isready。

### 4.3 测试与探针

仅对子进程设置：

```text
RADISH_TEST_POSTGRES_CONNECTION_STRING=Host=127.0.0.1;Port=15432;Database=radish_pg;Username=radish_pg;Password=radish-pg-test-only-20261031;Pooling=false
DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
DOTNET_GENERATE_ASPNET_CERTIFICATE=false
```

启动包装器按不区分大小写的键名比较，先清除继承的其他 `RADISH_TEST_*` 覆盖项，以及 `Databases__ / OpenIddict__ / Hangfire__ / Redis__ / ConnectionStrings__` 业务连接覆盖，只注入上述测试环境；不修改父进程环境。不启用 `RADISH_TEST_UPLOAD_QUOTA_INTEGRATION` 或 Redis / Valkey 连接。

先在当前构建输出运行 `dotnet test ... --list-tests --filter 'Database=PostgreSQL' --no-build --no-restore` 留下实际发现清单，再执行（pgConfiguration 为 Debug / Release）：

```bash
# PG17，每配置一次全量；以下相对路径均从隔离源码根执行。
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -c "$pgConfiguration" \
  --no-build --no-restore --logger "trx;LogFileName=pg17-$pgConfiguration.trx" \
  --results-directory "$pgRun/evidence/pg17/$pgConfiguration"
# PG16，每配置一次专项。
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -c "$pgConfiguration" \
  --no-build --no-restore --filter 'Database=PostgreSQL' \
  --logger "trx;LogFileName=pg16-$pgConfiguration.trx" \
  --results-directory "$pgRun/evidence/pg16/$pgConfiguration"
```

Hangfire 临时 Probe.csproj 每配置引用相应 `Radish.Api/bin/<configuration>/net10.0/*.dll`，以 SDK 10.0.300 还原 / 严格编译且无包引用；使用该 API 原 deps / runtimeconfig，探针 DLL 只暂存于隔离输出旁：

```bash
dotnet restore "$pgProbeProject" --configfile "$pgSource/NuGet.Config" --warnaserror
dotnet build "$pgProbeProject" -c Release --no-restore --warnaserror
dotnet exec --runtimeconfig "$pgApiOutput/Radish.Api.runtimeconfig.json" \
  --depsfile "$pgApiOutput/Radish.Api.deps.json" "$pgProbeDll" "$pgProbeSchema" "$pgProbeDirectory"
```

全量单组上限 45 分钟、PG 专项 15 分钟、worker 探针 120 秒；超时先保存日志，停止本任务进程组并等退出，再清理。四份完整 TRX 必须 Completed，PG 用例不得跳过；worker 必须输出注册 / 执行 / 重试及程序集 JSON 并清理 schema。不把退出码 0、过滤零条或缺少结果文件记为通过。

## 5. 端口、证书与数据边界

固定业务连接只有本机 `127.0.0.1:15432 → 容器5432`，不监听 5000 / 5100 / 5200，不启动三宿主 / 前端或浏览器 Smoke。VSTest / testhost 可能使用本机临时通信端口，单独记录并随进程结束；不承诺测试基础设施只有一个 socket。

源码中的 MemoryKestrel / HTTP3 fixture 使用内存 transport，`Listen(..., 0)` 是交给内存 listener 的端点值，没有注册 socket / QUIC / UDP；不能仅按方法名认定它们开放真实端口。全量仍有临时自签名证书夹具；此前沙盒遇过 Keychain 不存在，必要时只对这批测试最小提权，不导入信任、不生成宿主开发证书或修改证书库设置。

写入仅限：新 PG volume 的初始库 / 随机库 / schema、隔离源码的包缓存和 bin / obj、TMPDIR 中测试文件 / 证书夹具，以及本批日志 / TRX / JSON。DbMigrate 子进程的 Production 标识不扩大其连接范围。

## 6. 清理、停止线与交付

1. 创建第一个容器 / 卷前就注册 finally / trap；逐条命令记录展开参数、PID、退出码、开始 / 结束时间及证据路径。
2. PG17 完成后先停所有关联测试 / worker，保存数据库日志，核对已记录容器 ID / label，然后删除容器；再 inspect 卷 label 并删除精确 pgVolume。PG16 同样处理；不按名字前缀批量删除其他资源。
3. `docker ps -aq --filter "label=io.radish.pg-regression=$pgTask"` 与带同标签的 volume 清单应为空，15432 无本任务监听；测试进程和子进程均已退出。
4. 保留源码 / native / 依赖 hash、版本、测试发现与完整 TRX、四个 Hangfire 探针 JSON / schema 清理、命令和清理记录；归档后删除隔离源码、包缓存、合成数据和临时凭据文件。共享 PG 镜像保留，不 prune、不改代理。
5. 输入漂移、越界地址、端口占用、测试 / schema / worker 失败、警告或缺少完整证据时，停在受影响步骤；不以跳过、换驱动、吞错误或放宽门禁取得通过。不自动修改产品代码。

本页只覆盖本机 PG 回归及临时 worker；托管 CI 本地修改由其专项方案单独覆盖。push / 远端触发、VM 操作、Hangfire 源码重建与对外发行、Valkey、生产数据、发布部署均不包含在内。
