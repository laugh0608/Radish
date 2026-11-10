# NoDrive 与 Valkey 隔离验证记录（2026-10-21）

> 项目所有者批准临时副本依赖安装、指定容器启动、测试与清理，明确不做正式替换或发布。[方案](../features/nodrive-valkey-isolation-validation-plan.md)及[命令](../features/nodrive-valkey-isolation-validation-commands.md)先以 `77549e95` 提交并推送 `origin/dev`，随后执行；实验源码固定为 `47bdbfd5`。结构化结果、版本和哈希见[证据摘要](./nodrive-valkey-validation-evidence-2026-10-21.json)。

## 1. 结论

- **NoDrive：通过本次隔离验证。** 三组严格构建、SQLite / PostgreSQL 17 全量、PostgreSQL 16 专项与四宿主产物审计均通过。候选能减少部分驱动分发，仍保留 SqlClient / SNI；尚未修改正式依赖、其他数据库配置边界或许可交付材料。
- **Valkey：已测场景未发现候选独有差异，但完整业务验收未通过。** 22 个业务对照用例中 16 通过、6 失败；失败在 Redis 与 Valkey 上一致，归为现有上传限流的两类边界问题。独立的 AOF、同版本 RDB、AUTH / ACL 和同一客户端重连验证通过。失败断言保留，未修复或隐藏业务问题。
- **清理完成。** 本轮容器、命名卷、临时源码、publish、私有包 / HTTP 缓存和控制目录均已删除，四个端口释放；仅保留证据与归属清单，Docker 镜像缓存按方案保留。主工作区只新增 / 更新文档，无正式运行代码、依赖清单或部署配置变更。

## 2. 固定输入与隔离

| 项目 | 实际输入 |
| --- | --- |
| 源码 | `47bdbfd5b225f5f3d809c2283686d7a80f6d154b` 的三份 `git archive` 导出 |
| .NET 客户端运行环境 | macOS arm64；SDK `10.0.300`，runtime `10.0.8` |
| 数据库服务环境 | 本机 OrbStack，Linux arm64；未连接远程 daemon |
| PostgreSQL | `17.11-1.pgdg13+2` 与 `16.15-1.pgdg13+2`，依次绑定 `127.0.0.1:15432` |
| Redis / Valkey | Redis `7.4.11`：`16379`；Valkey `9.1.2`：`16380`；同版本 RDB 恢复：`16381`，均仅回环绑定 |
| Redis 客户端 | 现有 `StackExchange.Redis 2.9.32`、`Microsoft.Extensions.Caching.StackExchangeRedis 10.0.0` |
| 缓存数据 | database 15、随机 run id 前缀、合成凭据和数据；不使用现有卷、生产连接或 FLUSHALL |
| 原生产物 | 复制原工作区 `osx-arm64` 的 Debug / Release native 产物，三份源码均通过现有 `--verify`，未重新安装工具链 |

镜像均按方案的固定 index digest 拉取，核对 `RepoDigests`、Linux arm64 与服务实际版本；精确值在证据摘要中。Valkey 的 `INFO server` 同时返回兼容字段 `redis_version:7.2.4` 和实际字段 `valkey_version:9.1.2`，没有将兼容字段误记为运行版本。

临时根目录为 `/private/tmp/radish-deplab.0rQ0BY`。使用其独立包、HTTP 缓存和 CLI home，不改用户共享 NuGet 包缓存。退出清理校验 `.owner` 与 `io.radish.deplab` 标签；未执行全局 prune。

## 3. NoDrive 对照结果

三组仅作以下依赖差异，其余生产代码相同：

| 组 | 依赖差异 | Debug / Release | SQLite + PG17 全量 | PG16 专项 |
| --- | --- | --- | --- | --- |
| A：baseline | 原提交依赖 | 均 0 warning / error | 4034 / 4034，零跳过 | 43 / 43，零跳过 |
| B：drivers | Common 显式固定 Npgsql / Microsoft.Data.Sqlite `10.0.0` | 均 0 warning / error | 4034 / 4034，零跳过 | 43 / 43，零跳过 |
| C：nodrive | B + Common / Infrastructure 换 `SqlSugarCoreNoDrive 5.1.4.207`，Common 固定 `SystemCommon 8.5.0` | 均 0 warning / error | 4034 / 4034，零跳过 | 43 / 43，零跳过 |

每组全量包括原有 4031 个用例与新增 3 个 DriverProbe：实际加载的 SqlSugar 路径 / 哈希、SQLite provider 参数化查询、PostgreSQL provider 参数化查询。PG16 的 43 个用例包括原有 42 个与 1 个 PG 探针；已逐名核对原 Mapperly 验证中的 42 个跳过项在本次六轮中全部通过。三个探针在三组使用完全相同的源码。

首次 restore 生成 lock file，后续 locked mode 还原。三组各 19 份 assets 图均无诊断，NuGet 漏洞报告均无命中；不同版本计为不同包标识时，A / B / C 分别为 237 / 232 / 226。该报告不等于镜像漏洞扫描或发布放行。

**A → B**：统一独立消费者的驱动引用；Gateway 从 Npgsql `5.0.18` / Sqlite.Core `9.0.0` 变为 `10.0.0`。测试宿主原本已由 Auth 的依赖统一到 10.0.0，因此不能把测试宿主的加载结果当作单独启动 Gateway 的证据。

**B → C**：退出 SqlSugarCore 通用包及 MySqlConnector、Oracle、Oscar、Dm、Kdbndp、PerformanceCounter、DirectoryServices.Protocols 包链，新增 NoDrive / SystemCommon。四个宿主均仍有 `Microsoft.Data.SqlClient 5.2.2` / `Microsoft.Data.SqlClient.SNI.runtime 5.2.0`。

| Release publish | B 文件数 | C 文件数 | 减少的文件字节量 |
| --- | --- | --- | --- |
| API | 389 | 378 | 9,009,132 bytes |
| Auth | 581 | 570 | 9,009,818 bytes |
| Gateway | 102 | 91 | 9,005,260 bytes |
| DbMigrate | 207 | 196 | 9,009,128 bytes |

各宿主实际退出同一组 11 个文件，包括五类无关驱动及其辅助 runtime 文件；每份产物约减少 8.59 MiB。字节量包含 `.deps.json` 等文件差异，不当作镜像层大小或性能收益。

三组最终 Debug / Release 构建、运行时探针与十二份 publish 均选中 SHA256 `38a361e486a809ee2242861ce5ecb94016dca99bcaa9541d8f54cda8b06e9d0b` 的 `SqlSugar.dll`，与现用 5.1.4.207 相同；没有误选 SystemCommon 所带的另一份 DLL。未通过复制覆盖、删文件、NoWarn 或 ExcludeAssets 制造结果。

本轮证明这些固定输入在已测 SQLite / PG 边界可用；正式采用仍需确认非正式数据库配置的支持范围和分发许可材料。若目标要求完全退出 SQL Server / SNI，本候选仍不满足。

## 4. Redis / Valkey 对照与既有问题

临时夹具直接构造真实 `UploadRateLimitService`、`Caching`、RedisCache 与 PostQuickReplyService；轻回应仅模拟仓储 / 系统设置读取，缓存操作为真实调用。未复制或改写生产 Lua。每个普通场景在两端执行；并发场景使用两个独立 multiplexer、20 个同时请求。

| 场景 | 结果 |
| --- | --- |
| JSON / UTF-8、null / miss、删除、TTL、InstanceName 隔离、IDistributedCache Hash 布局及索引不完整性 | 两端通过 |
| Acquire 并发上限、分钟尝试次数、预留占日容量、Complete / Fail 幂等、Statistics、Reset | 两端通过 |
| FakeTimeProvider 过期清理、真实短 TTL、跨业务日结算、4,000,000,001 字节及 8,000,000,002 字节容量上界 | 两端通过 |
| 轻回应 cooldown / dedup：30 / 300 秒与 7 / 19 秒配置、拒绝错误码 | 两端通过 |
| 正确 / 错误密码、受限 ACL 用户的业务 Lua、跨前缀与管理命令拒绝 | 两端通过 |
| 目标预留已过期，但其他预留使同一 Hash 仍存活 | 两端失败：预期不结算，实际计入 100 字节 |
| Int64 大整数：`2^53-1`、`2^53+1` | 两端均失败：Acquire 接受后，预留统计变为 0 |

对后两类单列失败断言，并额外重跑 6 个诊断用例保存原始 reservation；诊断仍为 6 失败，不并入首次 22 个用例的通过数。没有放宽预期或改生产方法。

1. **过期目标仍结算**：实际 `now=1783782002`，目标记录的 `expiresAt=1783782001`；另一个预留继续存活。CompleteScript 读取时间但不检查目标有效期，随后删除目标并增加日计数，实际计入 100 字节。静态源码还显示内存分支也先移出目标再清理其余条目；本轮新增真实服务实验只覆盖 Redis 分支，不声称已补齐内存分支回归。
2. **大整数表示与解析不兼容**：两端原始 Hash 均将测试值写为 `9.007199254741e+15|...`。现有 Lua 只接受十进制数字的解析模式，Statistics 将其视为无效记录并清除，导致预留字节变为 0。此处是超大输入边界，不等于已证明正常 HTTP 上传可绕过上游文件大小限制；是否限定服务输入或改变编码需单独设计。

没有观察到 Valkey 独有的失败；也不能将两端同时违反业务预期记为“兼容验收全部通过”。上述两个边界的验收结论停止，修复需另行批准。

## 5. 持久化与重连

- **AOF 正常重启**：两端用生产服务写入待完成 / 已完成上传状态及真实 IDistributedCache 内容；分别等待 BGREWRITEAOF 完成且状态为 ok，再正常重启。值、String / Hash 类型及剩余 TTL 通过；未验证断电零丢失。
- **Valkey 同版本 RDB**：在 9.1.2 生成并等待 BGSAVE 完成，将 `dump.rdb` 复制到新卷、由独立 9.1.2 容器加载；同样的状态与 TTL 检查通过。没有导入 Redis 7.4 的 RDB / AOF。
- **同一 multiplexer 恢复**：两端各 1 个用例通过。测试在真实 Ping 异常后写出 disconnected 信号，操作者随后启动原容器；同一客户端重连后，上传日计数、分钟次数、缓存值和 TTL 均保留。

持久化夹具分 seed / verify-restart / verify-rdb 三轮，每轮发现并通过 1 个测试；seed 与 verify-restart 均在一个测试内检查两端。恢复另为 Redis / Valkey 各 1 个测试。为避免正常 120 秒 rate key 在恢复期间自然失效，仅对持久化实验的合成 rate key 将 TTL 延长至 30 分钟；生产默认 120 秒已在普通对照中单独验证。

本轮未覆盖 TLS、Cluster、Sentinel、SignalR backplane、Linux .NET 宿主运行、amd64、生产数据迁移、跨版本回退、镜像漏洞 / notices 或负载容量，不将本机结果外推为这些门禁通过。

## 6. 过程修正、留痕与清理

首轮新增 DriverProbe 缺少仓库要求的 PostgreSQL 独占 Collection，约定检查失败后中断；补齐临时夹具、三组重新严格构建，并以最终六份完整 TRX 计数。Redis 夹具初次构建被 xUnit cancellation / Assert.Single 规则阻止，按规则修正后 0 warning / error；没有关闭分析器。上述初次失败不归因于候选，也未混入最终通过结果。

全量测试因现有 macOS 临时钥匙串夹具按最小范围提权；沿用仅测试进程的 `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false`。未修改产品热重载设置，未安装额外 SDK / 测试框架，未启动开发宿主或 browser Smoke。

原始证据保留在 `/private/tmp/radish-deplab.0rQ0BY/evidence/`：命令脚本、最终夹具及 patch、三组 csproj patch、lock / assets / 安全报告、构建与测试日志、13 份 TRX（含失败诊断）、十二份 publish 清单 / deps、镜像与容器元数据、持久化状态和清理结果。摘要记录证据 manifest 与夹具 patch 的 SHA256；原始文件留在本机临时目录，不作为永久可用的远程归档。

清理退出码为 0；归属标签筛选的容器与卷均为空，根目录只剩 `.owner`、`evidence/`，端口 `15432 / 16379 / 16380 / 16381` 无监听。两个 PG 实例结束时均只剩默认 `public` schema。测试辅助源码 / 脚本已保存进 evidence 后移除外部临时副本，未清理其他任务资源或用户缓存。

后续应分别裁决 NoDrive 正式部分收缩、上传限流两类边界修复及 Valkey 数据 / 运维迁移方案；本轮授权不延伸到这些变更或发布。
