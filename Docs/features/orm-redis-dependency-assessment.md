# ORM 与 Redis 依赖评估

> 2026-10-21，基于 `dev` 的 Mapperly 迁移提交 `b8debb26`；该提交已推送 `origin/dev`。本页是 ORM / Redis 后续评估的当前入口，[前轮方案](./post-image-dependency-assessment.md)保留初始盘点。
>
> 首轮只读盘点的精确哈希与版本见[证据清单](../records/orm-redis-assessment-evidence-2026-10-21.json)。随后按批准方案完成隔离实验并清理：NoDrive 通过既定矩阵，Valkey 未发现候选独有差异，但两端共同暴露两类上传限流边界问题，完整业务验收未通过；详见[验证记录](../records/nodrive-valkey-validation-2026-10-21.md)。正式依赖与部署配置未变，未连接生产或发布。

## 1. 当前建议

| 对象 | 建议 | 当前证据边界 |
| --- | --- | --- |
| SqlSugar | 同版本 NoDrive 已通过隔离验证，可据此裁决正式部分收缩；暂不整体更换 ORM | SQLite、PG16 / 17、严格构建与四宿主产物通过；仍带 SQL Server / SNI，不能达到“只剩 PG / SQLite” |
| 完整 ORM 替换 | 仅当必须彻底退出额外驱动链，或 NoDrive 无法通过验证时，另立 EF Core 迁移专题 | 现有 OpenIddict 已用 EF Core；业务查询、事务、实体特性及迁移仍与 SqlSugar 深度耦合，成本明显高于映射替代 |
| Redis 服务 | 保留 Valkey 9.1.2 候选；先裁决上传限流边界与迁移方案 | 普通业务、AUTH / ACL、AOF、同版本 RDB、重连通过；过期结算与超大整数在两端同样失败，无性能 / 生产迁移结论 |
| 实际迁移 | 先确认源版本与数据分类，再定数据方案 | 本次联网解析 `redis:7` 的 amd64 config 为 `7.4.11`，不能推定用户正在运行的版本，更不能直接换镜像挂旧卷 |

实验授权与验证结果不等于正式依赖、配置、数据库支持范围或部署方案获批。后续分别裁决 NoDrive 正式收缩、限流问题修复与 Valkey 迁移，不将两者绑成一次替换。

## 2. ORM：精确包与现有分发边界

### 2.1 现状不只涉及业务宿主

直接引用 `SqlSugarCore 5.1.4.207` 的项目为 `Radish.Common`、`Radish.Infrastructure`。本次复核 Mapperly 验证产生的 API / Auth / Gateway / DbMigrate 四份 Release publish，均有 Oracle、Oscar、Kdbndp、MySqlConnector、Microsoft.Data.SqlClient 的 DLL，以及 SNI 的 Windows runtime 文件；四份 `.deps.json` 也包含 Dm 包。

Gateway 虽不承载业务数据库，仍通过 `Common` 依赖链携带这些资产。因此只查 API 或只删 Infrastructure 引用不足以改变分发结果；将 Common 中数据库基础能力移出属于另外的分层调整，本轮不实施。

当前实际解析的驱动版本也不完全统一：

| 宿主 | Npgsql | Microsoft.Data.Sqlite.Core |
| --- | --- | --- |
| API / Auth / DbMigrate | `10.0.0` | `10.0.0` |
| Gateway | `5.0.18` | `9.0.0` |

这是现有依赖图事实，并非本轮引入的版本差异。NoDrive 不再声明 PG / SQLite 驱动；如果只靠 Auth 的 EF 引用间接带入，独立 Service / Repository 测试或其他宿主可能丢驱动。后续方案必须明确驱动所在项目、精确版本和每个入口的最终图，不能简单照抄 SqlSugar `.nuspec` 中的最低版本。

### 2.2 NoDrive 的已证实收益与剩余问题

首轮只读评估从 NuGet 官方 flat-container 下载 `SqlSugarCoreNoDrive 5.1.4.207` 与 `SystemCommon 8.5.0` 到临时目录，只读取 ZIP 内容；未安装到项目或修改 NuGet 缓存。

| 包 | `.NETStandard2.1` 声明依赖 | 包内程序集 |
| --- | --- | --- |
| `SqlSugarCoreNoDrive 5.1.4.207` | Newtonsoft.Json `>=13.0.2`、SystemCommon `>=8.5.0` | `SqlSugar.dll`，3,019,776 bytes |
| `SystemCommon 8.5.0` | Microsoft.Data.SqlClient `>=5.2.2`、System.Data.Common `>=4.3.0`、System.Reflection.Emit.Lightweight `>=4.3.0`、System.Text.RegularExpressions `>=4.3.1`、System.IdentityModel.Tokens.Jwt `>=7.6.3` | 另一个 `SqlSugar.dll`，2,874,368 bytes |

NoDrive 主 DLL 的 SHA256 为 `38a361e486a809ee2242861ce5ecb94016dca99bcaa9541d8f54cda8b06e9d0b`，与现用 `SqlSugarCore 5.1.4.207` DLL 逐字节一致。[上游安装说明](https://www.donet5.com/home/Doc?typeId=1226)也将两者描述为相同代码、不同驱动打包方式。这支持优先验证同版本 NoDrive，避免同时升级 ORM 行为。

分包需要区分以下四项；隔离结果只关闭相应技术检查：

1. **仍带 SQL Server 链**：SystemCommon 的精确包仍依赖 SqlClient；现有 SqlClient 5.2.2 图进一步依赖 SNI 5.2.0。因此 NoDrive 只能作为部分收缩候选。
2. **同名程序集选择**：两个候选包均包含 `lib/netstandard2.1/SqlSugar.dll`，内容不同。后续隔离实验已通过 restore / build / 运行时探针 / 十二份 publish 证明固定输入选择了预期 5.1.4.207；包版本变化时仍需复核。
3. **依赖图增加项**：SystemCommon 新增 JWT 等依赖边，须检查整个图的版本统一、漏洞、构建警告与产物，不能只计算删除了几种驱动。
4. **许可材料**：两包元数据指向 Apache-2.0，但 ZIP 中没有独立 LICENSE 正文。后续分发需补足对应上游许可与 copyright 材料；各传递包继续独立判定，不能由上层包许可覆盖。

隔离实验的 resolved graph 与四宿主 publish 已确认退出由通用包引入的 Oracle / Oscar / Dm / Kdbndp / MySqlConnector 及辅助链；正式工作区仍使用原包，不能将临时产物当作正式替换完成。禁止删 DLL、修改缓存或用 `ExcludeAssets` 隐藏命中。

### 2.3 与完整 ORM 迁移的比较

源码文本扫描（包含注释，排除 bin / obj）在 Model 命中 115 个文件、Repository 41 个、Service 43 个、DbMigrate 50 个、测试 110 个。它表示审查面，不等于需要修改的文件数量或工时估算。

| 路线 | 实际收益 | 主要代价 / 选择条件 |
| --- | --- | --- |
| 保留当前包并补许可材料 | 不改变数据库行为 | 继续分发未使用驱动，Oscar 等证据缺口仍待关闭 |
| 同版本 NoDrive + 明确驱动引用 | 可以保留 ORM 实现，缩小部分厂商链 | SQL Server / SNI 不会自然消失；固定输入的程序集选择与双数据库验证已通过，正式配置和许可边界仍待裁决 |
| 自维护精简 fork | 能自行控制 provider 分发 | 必须承担上游同步、构建来源、安全修复与测试；当前不建议作为默认路线 |
| EF Core | 可按正式 PG / SQLite provider 边界组织；已有 OpenIddict 经验可复用 | 需要重写 SqlSugar 查询、实体映射、仓储及事务接线，不能把现有 Auth 迁移经验当作业务库已兼容 |
| Dapper / 手写 SQL | SQL 与驱动选择直接可见 | Dapper 提供 SQL 执行 / 结果映射；分页、软删除、租户过滤、更新保护、schema 管理仍须项目承担，不宜仅为许可治理扩大自维护面 |

EF 的[全局过滤器](https://learn.microsoft.com/en-us/ef/core/querying/filters)可用于软删除 / 租户隔离，[Dapper 上游](https://github.com/DapperLib/Dapper)提供直接 SQL 操作能力；表中的迁移成本是结合 Radish 现状作出的判断，不是上游性能承诺。

完整替换至少要逐项保持：Main / Log / Message / Chat 切库与租户连接、公共租户可见性、软删除、Snowflake / PublicId、分页 / 分表、原地更新与乐观并发、金额 / UTC / nullable 映射、嵌套事务 / savepoint、SQLite WAL / busy 行为、PostgreSQL 大小写标识符、SQL 日志 AOP、既有 schema ledger。OpenIddict 的 EF migrations 与 Hangfire 自有 schema 保持各自边界，不能顺带重建业务表。

### 2.4 包级实验与正式采用的裁决标准

已批准实验固定 Common / Infrastructure 两处候选引用、驱动版本及隔离目录；前三项技术证据已完成，第四项仍属于正式采用前的范围裁决：

- 主 SqlSugar DLL 的最终哈希与预期一致；两套包不混留；所有入口的 PG / SQLite 驱动完整。
- Debug / Release 严格构建、身份 / 租户 / 事务 / schema 定向测试及全量后端通过；本轮 Mapperly 验证中跳过的 42 个 PostgreSQL 用例不能继续作为双数据库证据缺口。
- API / Auth / Gateway / DbMigrate 的新依赖图与 publish 清单给出真实退出和剩余条目，联网安全审计无新增不可接受项。
- 明确非正式数据库配置的行为。`BaseDbConfig` 与数据库枚举当前仍允许其他类型；关闭配置路径需要单独确认，不能以删除驱动制造不明确的运行失败。

若目标坚持“完全退出 SQL Server / SNI”，现有 NoDrive 候选不满足完成标准；应转入上游更细分包调查或完整 ORM 方案裁决，而不是继续叠加隐藏依赖的手段。

## 3. Redis：用途、数据与候选

### 3.1 当前部署与客户端

`Deploy/docker-compose.yaml` 声明 `redis:7`、AOF、密码和 `/data` 持久卷；API / Auth / DbMigrate 引用其 healthcheck。连接使用 `StackExchange.Redis 2.9.32` 与 `Microsoft.Extensions.Caching.StackExchangeRedis 10.0.0`，`CacheSetup` 共享 multiplexer，`InstanceName` 参与 key 前缀。

公开 registry 在本次读取时将 `redis:7` 解析为 manifest index `sha256:44960b3100c48159a66d28c18b0588c193492e25f7551d60e51dd464fc3eca5b`；其 linux/amd64 config 的 `REDIS_VERSION=7.4.11`。这证明浮动 tag 不能保证仍停在 Redis OSS 7.2；不证明已经部署的容器或旧卷来自这个版本。

[Redis 官方许可表](https://redis.io/legal/licenses/)区分 7.2 及以前的 BSD-3-Clause、7.4 的 RSALv2 / SSPLv1，以及 8+ 的新增 AGPLv3 选择。本轮不作适用于所有部署主体的许可裁定，客户端许可也不能替代服务条款。

### 3.2 key 与丢失影响

以下为源码消费路径分类，不是生产 key 扫描；所有逻辑 key 仍需考虑配置前缀和 database index。

| 数据族 / 入口 | TTL 与权威来源 | 迁移时的处理判断 |
| --- | --- | --- |
| `upload:{userId}:reservations` | Hash；默认预留 1 小时，可由调用方指定；Lua 维护有效期 | 在途容量与结算状态，丢失会影响并发上限和日配额；须排空上传或完整保留 |
| `upload:{userId}:rate:{UTC分钟}` | String；120 秒 | 分钟尝试次数，清空会放宽防刷窗口 |
| `upload:{userId}:daily:{业务日期}` | String；完成后设置到下一业务日的 TTL，跨日仍按预留日期结算 | 已用日容量，不能在同一业务日无说明清零；需保留或经批准安排窗口重置 |
| `post_quick_reply:cooldown:*` / `dedup:*` | 默认 30 / 300 秒，来自 SystemConfig，可调整 | 当前真实防刷消费路径；保留剩余 TTL，或停止写入并等待实际配置的最长窗口 |
| `notification:dedup:*` | 实现默认 300 秒 | 仅发现接口、实现与未使用注入参数，未找到业务调用；作为历史 / 潜在 key 单列，不宣称当前通知链依赖它 |
| `god_comments:post:*` / `sofas:parent:*` | 1 小时，数据库可重建 | 展示缓存，可失效后重建，需评估冷启动查询压力 |
| `sticker:groups:*` | 30 分钟，数据库可重建 | 可重建；保留租户前缀及写后失效语义 |
| `experience:level-configs:enabled` / `ExperienceCalculator:AllLevels` | 默认 60 分钟，可配置 | 配置缓存可重建；后者直接使用 IDistributedCache |
| PublicHead / Sitemap | 20 / 30 分钟，key 含来源站点区分 | 可重建；还存在进程内状态，不属于 Redis 卷迁移 |
| SqlSugar 查询缓存 | Category / Tenant 查询默认 10 秒，另有分库租户查询 `.WithCache()` | 数据库可重建，但须保持租户路由与失效正确；不复制旧查询缓存作为业务权威 |
| `keys` 缓存索引 | ICaching 维护的 JSON key 列表，无显式 TTL | 不是完整 keyspace 清单：直接 IDistributedCache 和上传 Lua 不都登记；迁移盘点不可只用它 |

`CacheConst` 中存在验证码、在线用户、权限等名称，但未发现对应业务写入；不能据常量推断 Redis 承载会话权威。当前 OIDC token 在数据库，DataProtection 使用文件路径；未发现 Redis session / DataProtection 持久化接线。`RedisBasketRepository` 保留 String / List / key 方法，但未找到业务消费者；API SignalR 当前也未接 Redis backplane。

### 3.3 业务验证与现有问题

上传实现包含 Acquire / Complete / Fail / Statistics 四段 Lua，使用 Hash、String、TTL、INCR / INCRBY 与 DEL。key 具备用户 hash tag，但当前部署仍是单实例；本轮不扩展成 Cluster。普通 ICaching 经 IDistributedCache 存储，上传 Lua 直接操作原生类型，验收必须覆盖两条路径。

现有 `UploadRateLimitServiceTest` 的 8 个用例使用 `RedisOptions.Enable=false`，可以证明内存分支的业务预期，不能证明 Lua 在 Redis / Valkey 中执行正确。后续隔离实验已以真实客户端与生产 Lua 补充两端对照，覆盖并发预留、容量上限、幂等、过期清理、跨日、InstanceName 和重连；新增夹具保存在实验 evidence，不作为正式测试代码提交。

静态阅读还发现 CompleteScript 接收 `now`、解析 `expiresAt`，但未据此拒绝已过期条目；内存分支也先移出待完成条目，再清理其余过期项。隔离实验已复现“目标已过期、其他预留仍存活时，目标仍结算”的问题，且补充发现 `2^53±1` 大整数被转成科学计数法后遭解析清除；两端表现一致。缓存 key 索引采用非原子读改写，轻回应检查和写入也不是一段 Lua。以上是现有实现的关注点，未在本轮修复；切换服务本身不会自动解决这些并发 / 语义问题。

### 3.4 Valkey 候选与数据路径

[官方下载页](https://valkey.io/download/)当前列出 `9.1.2`、`8.1.10`、`7.2.14`；[支持政策](https://valkey.io/topics/releases/)列出 9.1 维护至 2029-05-19、安全支持至 2031-05-19，8.1 分别至 2028-03-31 / 2030-03-31。结合本轮时间与维护期，建议优先实验 9.1.2，无需仅因名称接近 Redis 7 而新增一次中间升级。

| 评估项 | 候选 9.1.2 | 比较项 8.1.10 |
| --- | --- | --- |
| 镜像 | `valkey/valkey:9.1.2` | `valkey/valkey:8.1.10` |
| index digest | `sha256:418652cfb58ef879d4978c33553735d7147016032d5aefaa14c828e611eb9dfd` | `sha256:640c5e62cea04b6d6f2084232651d0cc70362d31f4f805e7be94dbed6855e8f2` |
| registry 中 linux/amd64、linux/arm64 | 均存在 | 均存在 |
| 顶层 COPYING | [BSD-3-Clause](https://raw.githubusercontent.com/valkey-io/valkey/9.1.2/COPYING) | [BSD-3-Clause](https://raw.githubusercontent.com/valkey-io/valkey/8.1.10/COPYING) |

manifest / config 核对不包含镜像层漏洞扫描、操作系统包许可或功能验收。以上 digest 未写入 Compose；其中 9.1.2 已获批完成 Linux arm64 隔离实验，8.1.10 未运行。实际版本与运行范围见验证记录，正式分发仍需独立安全与许可材料。

[官方迁移文档](https://valkey.io/topics/migration/)支持 Redis OSS 7.2 及以前的兼容迁移，明确 Redis CE 7.4+ 数据文件不能按该路径直接使用。因此需按源版本分流：

- **源为 OSS ≤7.2**：以旧数据的不可变副本演练快照 / 复制；验证类型、数值、TTL、AOF 重启恢复。源卷保持独立，不允许两个服务同时写同一目录。
- **源为 7.4+**：不直接复用 AOF / RDB。选择经过验证的逻辑数据迁移，或在批准的停写窗口按 key 分类重建；`DUMP / RESTORE` 涉及序列化格式，不能假定绕过版本限制。
- **源版本未知**：不选择数据路径、不把清空缓存作为默认方案。后续只读核对需明确实例、版本、digest、持久化格式及实际 key / TTL 范围，不读取或记录业务值。

即便暂停上传等在途操作，当前业务日已用配额仍不会自动消失；“等待一分钟后清空”不足以保留限制。切换后如已产生新写入，回退必须处理增量，不能直接指回旧卷造成配额或去重状态倒退。

## 4. 隔离结果与后续停止线

本评估已随 `47bdbfd5` 推送；[隔离方案](./nodrive-valkey-isolation-validation-plan.md)与[命令](./nodrive-valkey-isolation-validation-commands.md)获批后随 `77549e95` 推送并执行。完整[验证记录](../records/nodrive-valkey-validation-2026-10-21.md)区分 NoDrive 通过、Valkey 对照一致但两类业务边界失败，以及已通过的独立恢复场景。以下仍是后续变更的范围约束：

1. ORM：精确候选包和显式驱动、需改的项目、剩余许可链、临时目录；SQLite 与 PostgreSQL 验证均要覆盖，数据库服务启动另列范围。
2. Valkey：精确版本 / digest、独立测试容器、仅回环绑定端口（可提议 `16379 / 16380`，使用前检查占用）、独立测试卷与合成数据、停止和删除容器 / 卷的清理命令；不复用项目或生产卷。
3. 协议与业务：保持现有客户端和配置接口，验证 Lua / IDistributedCache、命令错误、TTL、AOF / RDB 恢复、密码 / ACL、TLS 使用边界与重连。当前 Compose 未配置 Redis TLS，不把明文联通称为 TLS 验收。
4. 运维：若后续切换，更新 server / CLI 命令、healthcheck、卷权限与路径、备份恢复和运维文档；保持服务名 / 连接端点是否不变需方案明示，不在实验中默认修改部署。
5. 结论：明确“可安装 / 可运行 / 可迁移数据 / 可发布”的不同证据。仍需合并前回归与独立发布授权，包审计或一次 GET / SET 成功均不足以完成替换。

本轮只在临时副本改变依赖和增加夹具，实验资源已清理；正式运行代码、依赖清单与部署配置未改，未形成生产迁移或发布结论。验证结果已随 `5f76913a` 推送；项目所有者指定先整理[两类上传配额问题的修复方案](./upload-quota-boundary-fix-plan.md)，现已获准实施和隔离回归。NoDrive 收缩和 Valkey 迁移继续暂停。
