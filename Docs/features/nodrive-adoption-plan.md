# NoDrive 正式收缩实施方案

> 2026-10-26：项目所有者已确认继续采用“部分驱动收缩 + 分发材料收口”的方向。本文完成正式范围与命令准备；精确依赖安装、下述配置行为和测试容器启动待本任务确认，尚未修改产品代码或依赖。
>
> 当前评估入口为[ORM / Redis 评估](./orm-redis-dependency-assessment.md)，历史实验见[隔离验证记录](../records/nodrive-valkey-validation-2026-10-21.md)。本文承接正式采用，不重新授权历史实验，也不包含 Valkey 切换。

## 1. 目标与完成边界

以同版本 `SqlSugarCoreNoDrive 5.1.4.207` 替换两处 `SqlSugarCore 5.1.4.207`，显式提供 PostgreSQL / SQLite 驱动，减少未使用的厂商驱动分发。保留 SqlSugar 的查询、事务、映射及 schema 管理，不迁移 ORM 或数据库。

- 支持范围为 PostgreSQL / SQLite；对实际启用或使用的其他 provider 在连接与驱动加载前明确失败。
- 预期退出 Oracle / Oscar / Dm / Kdbndp / MySqlConnector 及对应辅助链；以当前候选 assets 与四宿主 publish 实测为准。
- `SystemCommon` 仍引入 SqlClient / SNI。本轮接受它们继续存在，单独补齐材料，不宣称产物只含两个数据库驱动。
- 不改变业务表、数据、API 字段、根 LICENSE、调度器或密码算法；不实施 Common 数据库能力分层搬迁。
- Valkey、Compose、生产数据、发布与部署保持独立顺位；本方案不启动 Web / API / Auth / Gateway 开发服务。

## 2. 精确依赖与项目影响

| 项目 | 动作 | 精确引用 |
| --- | --- | --- |
| `Radish.Common` | 删除通用包，加入 NoDrive | `SqlSugarCoreNoDrive [5.1.4.207]` |
| `Radish.Infrastructure` | 删除通用包，加入同一 NoDrive | `SqlSugarCoreNoDrive [5.1.4.207]` |
| `Radish.Common` | 固定 NoDrive 的关键传递输入 | `SystemCommon [8.5.0]` |
| `Radish.Common` | 显式提供正式驱动 | `Npgsql [10.0.0]`、`Microsoft.Data.Sqlite [10.0.0]` |

方括号为 NuGet 精确版本范围。沿用隔离实验已经验证的输入，不顺带升级版本；`SQLitePCLRaw.lib.e_sqlite3 3.50.3` 等现有约束保持。Gateway 的驱动会由旧传递版本统一到 10.0.0，该变化须单独核对，不能只用引用 Auth 的聚合测试宿主证明。

下载范围仅为当前解决方案及以上包的声明传递链，使用仓库 `NuGet.Config` 指定的 `.nuget/packages`；不修改用户全局缓存、SDK、npm、Rust 或 Flutter 依赖。首次正式 restore 后保存全部 assets 和审计报告，后续 build / test / publish 使用 `--no-restore`；输入改变须重新生成并审查依赖图。本批不引入永久 lockfile 策略。

下列情况停止受影响步骤并重新裁决：直接版本被抬高或降级、声明链外新增包、同名 DLL 选择变化、构建警告、High / Critical 漏洞或需要追加 override。不得删除 DLL、使用 `ExcludeAssets`、关闭警告或降低审计门槛。

## 3. 数据库支持边界

只在真实配置消费点增加同一 provider 支持判定，不增加 provider 插件架构。

| 入口 | 拟定行为 | 验证重点 |
| --- | --- | --- |
| `BaseDbConfig.MutiInitConn` | 对全部 `Enabled=true` 的业务连接先校验 provider，再执行路径处理；只允许 `2=Sqlite`、`4=PostgreSql` | Main / Log / Message / Chat 均覆盖；不能先处理 SQLite 文件路径再发现后续连接不支持 |
| `TenantUtil.GetConnectionConfig` | 分库租户只允许 SqlSugar 的 SQLite / PostgreSQL；创建 ConnectionConfig 前拒绝其他 provider | null 保持明确失败；非法值与已有但不支持的类型均失败；不探测真实租户库 |
| `RuntimeDatabaseConfigResolver` | 显式配置的 OpenIddict / Hangfire provider 在连接串处理前限定到两种支持类型 | 数字、名称、未知整数、缺省及现有 legacy 推断分别测试；不改变合法 legacy 连接行为 |
| `DbMigrate` | 沿用相同初始化校验，不让 doctor / verify / apply 依赖缺库异常来表达不支持 | 用配置与入口测试验证，禁止对现有数据库执行 apply |

补充约束：

- 保留 `DataBaseType` 的历史成员及数值，不重排、不将旧值转换成 SQLite；租户实体中的 SqlSugar 枚举同样不迁移。
- `Enabled=false` 的历史业务条目不加载、不自动删除；启用时明确报错。
- 当前 `Slaves` 模型未接入 `SqlSugarSetup` 的读写分离消费，本轮不增加主从路由、递归连接或自动切换行为。
- 移除 `BaseDbConfig` 中不再可达的 SQL Server / MySQL / Oracle 明文文件读取分支；不扩大到一般连接串规范化或日志重构。
- 新错误仅携带配置路径、provider 和支持类型，不回显连接串、密码或租户数据库地址。
- 对生产中是否存在非正式 provider 不作推定；若所有者确认存在，先独立处理迁移范围，本批不会自动转换。

同步更新 `Docs/guide/database-overview.md`、`configuration.md`、`database-connection.md` 的支持说明。枚举表应区分历史值和正式支持，避免“枚举可解析”被理解为产品支持。

## 4. 实施与验证

直接在 `dev` 推进，不创建主题分支或 worktree。依赖、支持判定、必要回归与文档组成一个完整批次。

1. 保存变更前引用与 assets；执行第 6 节包命令，核对实际图和安全报告。
2. 实现第 3 节消费点校验，增加配置与租户回归。测试覆盖不支持 provider 在文件 / 数据库副作用前失败、禁用条目、合法配置及错误脱敏。
3. Debug / Release 严格构建，验证实际加载的 SqlSugar DLL。沿用实验预期 SHA256 `38a361e486a809ee2242861ce5ecb94016dca99bcaa9541d8f54cda8b06e9d0b`；错误程序集立即失败。
4. PostgreSQL 17 上运行两种配置的后端全量测试；SQLite 由现有临时 / 内存夹具覆盖。PostgreSQL 16 上运行两种配置的 `Database=PostgreSQL` 专项；新增真实驱动探针纳入该 trait 和既有独占 collection。
5. API / Auth / Gateway / DbMigrate 四宿主生成全新 Release publish 目录，审查 `.deps.json`、SqlSugar DLL、PG / SQLite 驱动和所有 runtime 文件；源树、assets、产物均不得混留通用包。
6. 保存新增 / 退出依赖、测试用例名与通过 / 失败 / 跳过数量、产物清单和清理结果；刷新当前依赖清单，保留 2026-10-20 的 947 条原始快照。
7. 执行 `npm run check:docs`、按变更范围的卫生检查及 `git diff --check`。完整批次准备合并时另跑最新候选门禁；不把此次定向结果作为 PR 或发布自动放行。

全量中的 Redis / Valkey 上传集成测试因未启用其独立服务而预期跳过，必须逐类记录；PostgreSQL 用例不得跳过，不能沿用 10 月 21 日的固定数量。此轮不启动 Redis / Valkey，也不把旧 262 项记录当成新候选验证。

## 5. 分发材料工作包

[本次只读预检](../records/dependency-distribution-preflight-2026-10-26.md)已经核对 19 份本地 assets、12 个重点包的 metadata / 许可文件及字体接线；它不是重新 restore 或候选发布结果。

- NoDrive / SystemCommon：安装后记录精确 nuspec、程序集选择和上游版本对应的许可 / copyright 材料，不能以 Apache URL 覆盖其所有传递包。
- Hangfire：分别整理三个核心集成包和 PostgreSQL 插件的原文、NOTICES、来源与实际分发方式；明确开源路径与商业路径，复制备选商业条款不代表取得授权。不因材料整理更换调度器。
- Argon2 三包：保留逐包作者、来源与原始许可，单独核对声明在最终宿主产物中的可获得性；不变更密码实现。
- Microsoft：将 VS Container Tools 的构建用途和 SNI 的原生 runtime 分开记录；核对最终产物，不把 NuGet 缓存内存在正文当成已经随产品交付。
- 字体：DejaVu / Noto Sans 的原生产物接线与 Flutter 的两份 OFL 资源声明已存在；只复核新候选实际携带与哈希一致性，不重复替换字体或重新生成图片库。
- 新的通用 notices 生成 / publish 接线若需要修改 MSBuild 或 Docker，先给出独立可审查范围；当前正式方案仅包含 NoDrive 采用与材料整理，不默认引入全生态许可 CI 系统。

## 6. 待确认命令与运行资源

以下命令清单为本轮拟执行范围，尚未运行。首次执行前再次核对 `dev`、工作区差异、SDK `10.0.300`、本机 ARM64 Docker、磁盘与端口；预检不沿用到未来任务。

### 6.1 包声明与还原

在仓库根执行；两处删除与五处添加全部成功后才 restore。任一失败停止并报告部分完成状态，不留下可被误认成完成的候选。

```bash
dotnet remove Radish.Common/Radish.Common.csproj package SqlSugarCore
dotnet remove Radish.Infrastructure/Radish.Infrastructure.csproj package SqlSugarCore
dotnet add Radish.Common/Radish.Common.csproj package SqlSugarCoreNoDrive --version '[5.1.4.207]' --no-restore
dotnet add Radish.Infrastructure/Radish.Infrastructure.csproj package SqlSugarCoreNoDrive --version '[5.1.4.207]' --no-restore
dotnet add Radish.Common/Radish.Common.csproj package SystemCommon --version '[8.5.0]' --no-restore
dotnet add Radish.Common/Radish.Common.csproj package Npgsql --version '[10.0.0]' --no-restore
dotnet add Radish.Common/Radish.Common.csproj package Microsoft.Data.Sqlite --version '[10.0.0]' --no-restore
dotnet restore Radish.slnx --force --disable-parallel --warnaserror
dotnet package list --project Radish.slnx --vulnerable --include-transitive --format json --no-restore
```

### 6.2 测试服务

沿用已验证的精确镜像内容，不使用浮动 tag，不推定其为最新补丁：

| 测试线 | 镜像 | 本次预期版本 |
| --- | --- | --- |
| PG17 | `postgres@sha256:a63c1ea3f6a3a5a1621f97d2aba3b30faecfab4224c1b0000cc85ced57bb1939` | `17.11-1.pgdg13+2` |
| PG16 | `postgres@sha256:a902560563d3a5661228585f53474c5065e7f273b21ffd77e555a8b5682c5690` | `16.15-1.pgdg13+2` |

可执行 `docker pull --platform linux/arm64 <上表精确镜像>` 补充本机缓存；两实例串行使用 `127.0.0.1:15432`，每次上限 2 CPU / 1 GiB，独立新建卷。测试后停止并删除本批容器和卷；镜像留在共享缓存，不执行 prune。端口或名称冲突时停止，不改用其他端口、不停止其他容器。

在专用 Bash 会话中创建运行目录并设置变量，所有凭据仅用于新测试实例：

```bash
RADISH_NODRIVE_RUN=$(mktemp -d /private/tmp/radish-nodrive-adoption.XXXXXX)
RADISH_NODRIVE_OWNER=$(basename "$RADISH_NODRIVE_RUN")
RADISH_NODRIVE_PASSWORD=radish-nodrive-test-only-20261026
mkdir -p "$RADISH_NODRIVE_RUN/evidence" "$RADISH_NODRIVE_RUN/publish"
```

每次令 `RADISH_NODRIVE_PG` 为 `17` 或 `16`，`RADISH_NODRIVE_IMAGE` 为对应精确镜像；检查名称不存在后执行：

```bash
RADISH_NODRIVE_CONTAINER="$RADISH_NODRIVE_OWNER-pg$RADISH_NODRIVE_PG"
RADISH_NODRIVE_VOLUME="$RADISH_NODRIVE_CONTAINER-data"
docker volume create --label "io.radish.nodrive=$RADISH_NODRIVE_OWNER" "$RADISH_NODRIVE_VOLUME"
docker run -d --pull=never --platform linux/arm64 --name "$RADISH_NODRIVE_CONTAINER" \
  --label "io.radish.nodrive=$RADISH_NODRIVE_OWNER" \
  --cpus=2 --memory=1g --pids-limit=256 --shm-size=256m \
  -p 127.0.0.1:15432:5432 \
  --mount "type=volume,source=$RADISH_NODRIVE_VOLUME,target=/var/lib/postgresql/data" \
  -e POSTGRES_USER=radish_nodrive -e POSTGRES_DB=radish_nodrive \
  -e "POSTGRES_PASSWORD=$RADISH_NODRIVE_PASSWORD" \
  --health-cmd='pg_isready -U radish_nodrive -d radish_nodrive' \
  --health-interval=2s --health-timeout=3s --health-retries=30 "$RADISH_NODRIVE_IMAGE"
```

最多等 60 秒达到 healthy，再核对 `RepoDigests`、平台和 `SHOW server_version`；不符合则停止。测试连接由此会话显式设置：

```bash
export RADISH_TEST_POSTGRES_CONNECTION_STRING="Host=127.0.0.1;Port=15432;Database=radish_nodrive;Username=radish_nodrive;Password=$RADISH_NODRIVE_PASSWORD;Pooling=false"
export DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
```

只在测试进程禁用配置热重载；不修改产品配置。数据库仅合成数据与测试 schema，不读取既有 DataBases、项目卷、生产连接或业务值。沿用测试自清理的回环监听和临时证书夹具；沙盒阻止时只申请该测试命令提权，不调整系统信任。

### 6.3 构建、测试与产物

```bash
dotnet build Radish.slnx -c Debug --no-restore --warnaserror
dotnet build Radish.slnx -c Release --no-restore --warnaserror
# PG17：分别令 RADISH_NODRIVE_CONFIGURATION=Debug / Release
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -c "$RADISH_NODRIVE_CONFIGURATION" \
  --no-build --no-restore --logger 'trx;LogFileName=full-pg17.trx' \
  --results-directory "$RADISH_NODRIVE_RUN/evidence/$RADISH_NODRIVE_CONFIGURATION/pg17"
# PG16：在 PG17 已清理后，分别执行 Debug / Release
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -c "$RADISH_NODRIVE_CONFIGURATION" \
  --no-build --no-restore --filter 'Database=PostgreSQL' \
  --logger 'trx;LogFileName=postgres16.trx' \
  --results-directory "$RADISH_NODRIVE_RUN/evidence/$RADISH_NODRIVE_CONFIGURATION/pg16"
# RADISH_NODRIVE_HOST 依次为 Radish.Api / Radish.Auth / Radish.Gateway / Radish.DbMigrate
dotnet publish "$RADISH_NODRIVE_HOST/$RADISH_NODRIVE_HOST.csproj" -c Release \
  --no-build --no-restore -o "$RADISH_NODRIVE_RUN/publish/$RADISH_NODRIVE_HOST"
```

本机 native Debug / Release 的 `--verify` 已在准备阶段通过；执行前再次校验。失效即停止，不自动安装 Rust 工具或重建原生产物。

### 6.4 清理与回退

正常或失败退出均执行；实现运行包装时在创建第一个服务前注册退出清理。只处理本次清单中的名称，并分别核对容器 / 卷标签等于 `RADISH_NODRIVE_OWNER`：

```bash
docker logs "$RADISH_NODRIVE_CONTAINER" > "$RADISH_NODRIVE_RUN/evidence/pg$RADISH_NODRIVE_PG.log" 2>&1
docker rm -f "$RADISH_NODRIVE_CONTAINER"
docker volume rm "$RADISH_NODRIVE_VOLUME"
unset RADISH_TEST_POSTGRES_CONNECTION_STRING DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE
```

先结束本批测试进程，再清服务；清理失败保留清单并报告，不误报端口释放。保留 evidence 与候选 publish 供审查，报告其绝对路径；不清理用户缓存或其他任务文件。

代码回退通过反向补丁或独立 revert 同时撤回包引用、支持判定和配套文档，再还原并验证原依赖；不使用 reset / force push，不删除历史提交。本批无 schema / 数据迁移，不需要数据库回滚；已经上线的回退须另行确认当时版本与数据条件。
