# NoDrive 与 Valkey 隔离验证方案

> 状态：**已获批执行并完成清理**。方案先以 `77549e95` 推送 `origin/dev`，再执行固定输入实验。NoDrive 通过；Valkey 对照未见候选独有差异，但两端共同的两类业务边界失败，完整验收未通过。详见[验证记录](../records/nodrive-valkey-validation-2026-10-21.md)；后续问题修复与回归见独立[修复记录](../records/upload-quota-boundary-fix-2026-10-21.md)，不覆盖本次实验结果。下文保留获批范围。
>
> 本文定义实验范围、版本、验收与停止线；完整准备、启动、验证和清理命令见[命令附录](./nodrive-valkey-isolation-validation-commands.md)。批准实验不等于批准正式依赖替换、部署配置修改或发布。

## 1. 已批准的范围

本次仅在固定提交导出的临时源码中改依赖和增加验证夹具，使用独立容器、独立卷和合成数据。主工作区仅接收最终实验记录，不接收候选依赖、测试性业务修复或 Compose 变更。

| 实验 | 执行动作 | 不在本次范围 |
| --- | --- | --- |
| NoDrive | 三组源码副本、精确 NuGet 引用与还原、严格构建、SQLite / PostgreSQL 测试、四宿主本地 publish 与图 / DLL 审计 | 正式切换 ORM、关闭其他数据库配置、修改 schema / API、连接既有数据库 |
| Valkey | 当前客户端与真实四段 Lua 对照、缓存 / 防刷契约、密码 / ACL、重连、AOF 与同版本 RDB 恢复 | 正式换服务、生产访问、真实数据迁移、Redis 7.4 文件导入 Valkey、Cluster / Sentinel / backplane、TLS 建设 |
| 留痕 / 清理 | 保存版本、依赖差异、夹具 patch、TRX 与结论，删除实验容器 / 卷和临时源码 / 包目录 | Docker 全局 prune、清用户包缓存、清其他任务资源、tag / Release / 部署 |

预计需要下载隔离 NuGet 包和四个 ARM64 服务镜像；耗时与磁盘取决于缓存，不预先承诺完成时间。实验按阶段串行执行：PostgreSQL 容器上限 2 CPU / 1 GiB；Redis / Valkey 每个上限 1 CPU / 512 MiB，最多同时三个（包含 RDB 恢复实例）。磁盘开始前检查可用空间，建议至少预留 15 GiB；不足则停止，不自行清理用户文件。

## 2. 隔离方式与运行影响

- **源码**：用 `git archive` 导出固定提交 `47bdbfd5b225f5f3d809c2283686d7a80f6d154b` 到 `/private/tmp/radish-deplab.XXXXXX/src/{baseline,drivers,nodrive}`。不复制本地配置、数据库、日志或 `.env`；不创建分支 / worktree。
- **构建**：使用各副本内标准 `bin / obj`，避免源码契约测试从外部 artifacts 路径找不到仓库根。NuGet 包统一放本次目录的 `packages/`，不写原工作区或用户全局缓存。SDK 沿用本机 `10.0.300`，不安装 SDK。
- **图片 native**：复制当前已验证的 `artifacts/native/osx-arm64/{Debug,Release}` 到三个副本，分别执行现有 `--verify`。输入 / 哈希不匹配即停止；本次不自动安装 Rust 或重建原生产物。
- **数据**：PostgreSQL 16 / 17 各自新建卷；测试为每个用例创建独立 schema 并清理。SQLite 使用既有内存 / 临时文件夹具，不读取原 `DataBases/`。Redis / Valkey 使用新卷，key 前缀固定为本次随机 run id，所有值均合成。
- **服务**：只启动附录列出的数据库容器，不运行 `dotnet run`、`npm run dev`、项目组合启动或 browser Smoke。既有测试的临时 loopback 监听 / 证书夹具仍按原测试自清理，不形成常驻产品宿主。
- **Docker**：只使用当前本机 Docker daemon。预检确认 context 为本机；Docker 不可用、连接远程 daemon、端口或命名冲突均停止，不自动启动 Docker Desktop、改变 context 或停掉其他服务。

| 用途 | 地址 | 生命周期 |
| --- | --- | --- |
| PostgreSQL 17.11 / 16.15 | `127.0.0.1:15432` | 顺序启动，前一个移除后再启动下一个 |
| Redis 7.4.11 对照 | `127.0.0.1:16379` | Valkey 实验期间 |
| Valkey 9.1.2 候选 | `127.0.0.1:16380` | Valkey 实验期间，允许 stop / start / restart |
| Valkey 9.1.2 RDB 恢复实例 | `127.0.0.1:16381` | 恢复阶段临时启动 |

以上端口冲突时不自动改端口。数据库不暴露到 `0.0.0.0`，不用项目网络或生产卷；实验密码明确为一次性测试凭据，不读取用户密码。

## 3. NoDrive 的依赖与对照设计

### 3.1 三组输入

| 组 | 与固定提交的差异 | 作用 |
| --- | --- | --- |
| A：baseline | 不改依赖 | 确认当前代码在隔离环境与两条 PostgreSQL 版本线上成立 |
| B：drivers | 仅在 Common 显式引用 Npgsql 10.0.0、Microsoft.Data.Sqlite 10.0.0 | 分离 Gateway / 独立项目驱动版本统一带来的影响 |
| C：nodrive | B 的驱动引用 + Common / Infrastructure 两处换 NoDrive，Common 固定 SystemCommon 8.5.0 | 对照 B，判断 ORM 分包带来的程序集选择和传递依赖变化 |

此控制组是实验变量隔离，不决定正式版本统一策略。最终报告分别比较 A→B 与 B→C，不能把所有差异归于 NoDrive。

### 3.2 精确新增 / 改变引用

| 包 | 版本 | 放置位置与原因 |
| --- | --- | --- |
| SqlSugarCoreNoDrive | `5.1.4.207` | C 的 Common / Infrastructure，替代两处 SqlSugarCore 5.1.4.207 |
| SystemCommon | `8.5.0` | C 的 Common 显式固定；防止将不同版本的包行为混入实验 |
| Npgsql | `10.0.0` | B / C 的 Common，使各独立消费者获得明确驱动 |
| Microsoft.Data.Sqlite | `10.0.0` | B / C 的 Common，包含 Core 与 SQLite bundle 入口；不只依赖 Auth 带入 |

其他直接引用保持固定提交原值：StackExchange.Redis `2.9.32`、Microsoft.Extensions.Caching.StackExchangeRedis `10.0.0`、Newtonsoft.Json `13.0.4`、EF Core / Npgsql EF provider `10.0.0`、Mapperly `4.3.1`、xUnit v3 `3.2.0`、Microsoft.NET.Test.Sdk `18.0.0`。不安装 npm、Rust、Flutter 或额外测试框架。

SystemCommon 声明的传递下限为 SqlClient 5.2.2、System.Data.Common 4.3.0、Emit.Lightweight 4.3.0、Regex 4.3.1、JWT 7.6.3；实际统一版本由完整图决定，不能将这些下限当作最终版本。Microsoft.Data.Sqlite 10.0.0 的包元数据声明 Core 10.0.0、SQLitePCLRaw bundle / core 至少 2.1.11；仍保留仓库对 SQLite 原生包的现有约束，不添加或降低 override。

三个副本首次 restore 各生成 lock file，后续使用 locked mode；批准覆盖当前固定提交已有依赖及上述候选的声明传递链。还原后先审查新出现 / 升降级条目；超出声明链、出现降级、漏洞 High / Critical、版本冲突或需额外包补洞时停止，不能借还原静默扩大依赖范围。

### 3.3 验证顺序

1. **包与编译门**：三组独立 restore，保存 assets / lock / NuGet 安全报告；Debug / Release `--warnaserror`。发现警告或冲突先分类，不用 NoWarn / ExcludeAssets 绕过。
2. **程序集门**：C 编译、测试实际加载与四宿主 publish 中的 SqlSugar.dll 都必须等于预期 SHA256 `38a361e486a809ee2242861ce5ecb94016dca99bcaa9541d8f54cda8b06e9d0b`。SystemCommon 同名 DLL 不得被误选；不能以手动复制覆盖解决。
3. **PostgreSQL 17 + SQLite**：A / B / C 串行全量后端测试，42 个 PostgreSQL trait 用例全部执行，不接受未配置连接导致的跳过。保留身份、租户、金额、UTC、软删除、savepoint、迁移 ledger 和日志覆盖。
4. **PostgreSQL 16**：A / B / C 串行执行 `Database=PostgreSQL` 专项。17 对应当前 CI 版本线，16 对应 Compose 版本线；本次精确 patch 是实验输入，不代表既有部署 patch。
5. **入口与产物**：四宿主本地 Release publish，记录每个入口的驱动版本、SqlSugar.dll 哈希、`.deps.json` 与所有 runtime / native 文件。主候选应去掉 Oracle / Oscar / Dm / Kdbndp / MySqlConnector 的通用包链；SqlClient / SNI 预期仍在，必须如实列明。

获批后在三个副本加入同一份 `DependencyLabDriverProbeTests`（待编写，不是现有测试），检查程序集路径 / 哈希、真实 SQLite 与 PostgreSQL provider 连接和代表查询。夹具不修改生产方法，不将测试自己的映射替代真实 Repository 行为。所有新 filter 必须发现非零用例；保存新增用例数与名称，不能把空匹配当成功。

## 4. 镜像精确输入

均为 2026-10-21 只读 registry manifest / config 核对；执行时使用 digest，不能自动换 tag。此次运行限定本机 `linux/arm64`，已有 amd64 manifest 不等于完成 amd64 运行验收。

| 镜像 / 实际版本 | 固定 manifest index digest |
| --- | --- |
| postgres，`PG_VERSION=17.11-1.pgdg13+2` | `sha256:a63c1ea3f6a3a5a1621f97d2aba3b30faecfab4224c1b0000cc85ced57bb1939` |
| postgres，`PG_VERSION=16.15-1.pgdg13+2` | `sha256:a902560563d3a5661228585f53474c5065e7f273b21ffd77e555a8b5682c5690` |
| redis `7.4.11` | `sha256:44960b3100c48159a66d28c18b0588c193492e25f7551d60e51dd464fc3eca5b` |
| valkey/valkey `9.1.2` | `sha256:418652cfb58ef879d4978c33553735d7147016032d5aefaa14c828e611eb9dfd` |

Redis 7.4.11 只作为当前 Compose tag 的协议对照，不假定它是生产版本；不增加 Redis 7.2 或 Valkey 8.1 的运行矩阵。镜像拉取获批后需核对 RepoDigests、架构与服务版本；registry 元数据不足以形成镜像漏洞 / 第三方 notices 的发布结论。

## 5. Valkey 测试夹具与矩阵

使用 A 的生产代码和现有客户端；获批后只在 A 副本新增 `DependencyLabRedisContractTests`、`DependencyLabRedisPersistenceTests` 和 `DependencyLabRedisRecoveryTests`。通过测试构造真实 `UploadRateLimitService`、`Caching` 与现有缓存 provider，调用原生产 Lua，不复制一份改写后的 Lua 冒充等价验证。

夹具只接受附录的 `127.0.0.1` 端口、database 15、非空本次 run id 前缀。普通用例只清理自己的 key；不调用 FLUSHALL，不接受环境中的默认项目连接。Docker stop / restart / 恢复由操作者按附录控制，测试代码不拥有 Docker 全局权限。

| 类别 | 必须验证的行为 | 判定 |
| --- | --- | --- |
| 连接与缓存 | AUTH 成功 / 错误密码失败、JSON / UTF-8、null / miss、TTL、删除、InstanceName 隔离、真实 IDistributedCache hash 布局 | Redis / Valkey 输出与类型一致；TTL 按时间容差比较，不能逐毫秒硬等 |
| 四段 Lua | 并发 acquire 上限、分钟尝试次数、预留占日容量、失败释放、重复完成 / 失败幂等、Statistics 与 Reset | 同输入的返回、计数和 key 类型一致；不同客户端并发执行，不能只测串行 |
| 时间 / 边界 | 过期预留清理、跨业务日结算、目标过期但其他预留未过期、大整数与容量上界 | FakeTimeProvider 验证业务日；真实短 TTL 验证服务器过期，不混用两种时钟 |
| 防刷与索引 | 轻回应 cooldown / dedup 的 30 / 300 秒默认及配置覆盖，缓存索引不包含全部 Redis key | 不以清空后功能正常代替状态保留；既有非原子语义作为观察项 |
| ACL | 受限用户只能访问实验前缀，允许业务命令、拒绝管理命令；与现有默认密码模式分别记录 | 不改部署 ACL；拒绝行为必须可见，不能以吞异常判通过 |
| 断线 / 重启 | 同一 multiplexer 断线可见、服务重启后重连、连接失败期间计数不被静默重置 | 有界等待与明确失败，不承诺网络超时下自动重试获得 exactly-once |
| AOF / RDB | 合成 key、原生上传 Hash / String、IDistributedCache 数据及剩余 TTL 的正常重启；独立新卷加载同版本 Valkey RDB | 重启后有正确值与合理剩余 TTL；不包含 Redis 7.4→Valkey 文件迁移 |

每个逻辑场景至少在 Redis / Valkey 各跑一次；并发场景使用两个独立连接与 20 个同时请求，按用例配置核对允许数。可记录固定 1,000 次小数据操作的耗时作诊断，但不将本机 Docker 数字当作生产容量或 SLA。

现有完成逻辑对过期条目的处理已有疑点：依据服务注释中“过期释放”的预期单列测试。如果 Redis 与 Valkey 都偏离预期，标为既有问题并停止对应验收结论；不放宽断言、不在本轮修改业务代码。候选独有差异则判兼容未通过。

TLS 本轮不配置、不测试；理由是现行 Compose 的 Redis 链路没有 TLS，新增证书与客户端 TLS 接线会扩大范围。Cluster、Sentinel、SignalR backplane、生产备份与跨版本回退同样不属于此次兼容实验。

## 6. 完成、停止与清理

完成后分别给 NoDrive 和 Valkey 出结论，不合并为一个笼统“通过”：

- NoDrive：A→B / B→C 差异、完整测试结果与 42 个 PG 用例执行情况、同名 DLL 选择、四宿主包 / 文件退出清单、剩余许可链。
- Valkey：真实 Lua / 缓存 / 防刷矩阵、既有问题与候选差异、AUTH / ACL / 重连 / 同版本恢复结果；列明没有验证的跨架构 / TLS / 真实迁移边界。
- 保存精确版本、命令、测试夹具与 csproj patch、TRX、图 / 哈希和清理结果；结果摘要写入 Docs/records，原始合成数据和详细日志留在实验 evidence 目录。

以下情况立即停止受影响步骤：源提交 / 原生产物校验不符、服务端点不是本机、资源冲突、安装范围变化、严格构建失败、加载错误 SqlSugar.dll、PG 用例跳过、candidate-only 语义差异、需要业务修复或触及既有数据。可以继续不依赖该失败的另一实验，但不得自行扩大安装、启动或修改范围。

退出时按附录校验资源标签，再删除本轮容器和命名卷；删除临时源码、私有包缓存与合成数据副本，保留 `evidence/` 和小型运行清单。镜像层默认留在 Docker 缓存，避免误删其他任务共享内容；不执行 system / volume / image prune。失败也执行相同清理，报告保留失败证据，不留下常驻服务。

本方案的批准只覆盖本文与附录列明的实验、临时夹具和清理。正式替换依赖 / 镜像、修改其他数据库支持范围、修复实验发现的业务问题，以及发布，均需依据实验结果另行确认。
