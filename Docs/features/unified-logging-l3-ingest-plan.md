# 日志 L1 / L2 剩余门禁与 L3-A 接收入库方案

> 更新于 2026-11-17（Asia/Shanghai）。L3-A、第 8 节自有来源、第 9 节 Controller 与第 10 节已登记切片均有本地实现和回归。OrbStack / Linux arm64 的 SQLite 和 PostgreSQL 16.15 链路已通过。真实运行范围见[SQLite 记录](../records/unified-logging-l1-l3-sqlite-runtime-2026-11-17.md)和[PostgreSQL 记录](../records/unified-logging-l1-l3-postgres-runtime-2026-11-17.md)，剩余门禁见下方第 2 节。
>
> 本页属于[统一日志专题](./unified-logging-governance-design.md)，承接[事件契约](./unified-logging-contract.md)。已完成来源仍以事件契约为准；本页维护剩余门禁及下一批实施边界，不另建主线。

## 1. 推荐顺位与交付结论

本批已实施 **L3-A：受控内网接收与幂等持久化**，交付默认关闭、可独立测试的接收链，并纳入 Log 连接直接相关的防递归修正。第 3 节列明的剩余七个自有来源也已完成本地切片；随后核对其他已登记业务来源的最终出口，再完成 L1 传输 / 磁盘 / 平台门禁及 L3-B 部署、留存和恢复。L4 查询、L5 告警与 L6 正式切换仍按主专题推进。

L2 未整体关闭不阻止用合成安全事件实现和验证 L3-A；但它阻止把全宿主真实日志接入直接认定为安全完成。L1 上界未关闭时，接收端只能是受控候选，不能开启 production Compose 汇聚。上述顺位保持一个日志专题串行推进，不同时开展 Native 或客户端连接实施。

2026-11-05 实施前的基线核对：

- `Scripts/logging/collector-mock.mjs` 将请求累积到内存并追加文件，没有真实事务、认证或幂等存储，不能复用为产品接收器。
- 产品 API / Model / Repository / Service / DbMigrate 中尚无该内网接收与存储实现；Common 已存在生成契约 `Radish.Common.LogTool.RuntimeLogEvent`，新增持久实体需避免同名歧义。
- `SqlSugarSetup` 已排除 Log 库的普通 SQL AOP；但 SQLite PRAGMA 失败仍调用带异常的普通 `Log.Warning`，不能以 AOP 排除推定整条入库链绝不递归。
- `BaseLog` 带旧表的本地时间默认值与分表语义，新运行事件不能直接继承它。
- 浏览器本批、Hangfire 两来源、Common 两处异常诊断及既有框架来源保留已完成结论，不重复实施。

### 2026-11-10 收口接续

本页承接总纲[四个收口批次](unified-logging-governance-design.md#closeout-batches)的前两批；后两批沿原 L4/L5/L6，不因本页只写接收入库而删除。L3-A、七个来源和 14 个 Controller 保持本地完成，不恢复为待实现。

第一批按共享生成和消费链核对剩余 Service / Job / Hub。内容及表情已按[第 10.13 节](#l2-content-sticker-output)完成本地收口。[第 10.14 节](#l2-diagnostic-and-output-boundaries)已完成首版诊断字段、Flutter 本地入口及 CLI / 应急边界的本地实现，真实采集与设备按各自批次验收。

第二批将下表 L1 剩余项与 L3 实际 collector、接收器及 SQLite / PostgreSQL 组合验证：先明确目标架构、镜像、隔离存储、端口、故障方式和恢复 / 清理边界；每个尚缺条件对应一次可验结果，不追加多平台矩阵。真实运行另获授权，替身、合成格式或模块通过不能关闭真实链路。传输、磁盘、持久化与留存出口未齐时不提前进入生产切换。

<a id="l1-l3-exit-checklist"></a>

## 2. L1 剩余退出清单

| 门禁 | 已有证据 | 仍需交付 / 通过条件 | 所属批次 |
| --- | --- | --- | --- |
| 传输正式上界 | Docker Message mode 候选上界完成[推导](#l1-l3-transport-budget)及 [OrbStack 实测](../records/unified-logging-l1-l3-transport-runtime-2026-11-17.md)。合法流量全部交付，实际请求在候选预算内，接收端门限和进程内存已取证 | 部署接入时冻结目标 daemon、镜像和传输参数。扩展生产者、平台或并发条件时复核对应上界和内存 | L1 接续，与 L3-B 联验 |
| 坏批隔离与真实确认 | 前置拒收摘要、同批有效事件交付、无原文留存及 400 / 413 的丢弃计数已通过隔离联验。SQLite 和 PostgreSQL 均通过 503 重投恢复 | 留存仍按下列门禁验证 | L3-A 实现接收语义，L1 / L3-B 验证插件链 |
| 磁盘与支路隔离 | [OrbStack 联验](#l1-l3-disk-queue-runtime)实测共享卷 ENOSPC、两支路缺口与恢复。HTTP 队列溢出的 7,200 条缺口与丢弃计数一致 | 文件支路 ENOSPC 的丢弃计数、截断尾恢复及入队前失败的可观测性待收口。权限错误与有界应急通道保留原退出条件 | L1 接续 |
| 容量与恢复 | chunk 轮转、有限队列和 SIGKILL 后持久 chunk 恢复已有本机证据 | 计入单片超出量与索引 / 缓冲开销；验证重启、提交中断、备份恢复与丢失区间；SIGKILL 不等于真实断电持久性保证 | L1 / L3-B |
| 部署平台与原生来源 | Fluent Bit 5.1.2 的 OrbStack / Linux arm64 隔离实验；PG / Redis 合成格式 | 核定实际目标架构、digest、daemon 回环路径与七类服务标签；真实镜像来源关闭敏感 DETAIL / STATEMENT 等；未支持的平台显式保留 | L3-B 接入前 |

依据：[首轮传输](../records/unified-logging-l1-contract-and-transport-2026-09-19.md)、[采集安全与故障边界](../records/unified-logging-l1-guarded-collector-2026-09-19.md)。已有通过项不机械重做；只有配置 / 策略变化或组合故障新增时补回归。目标平台未确定前，不把本机 arm64 digest 当作多平台放行。

<a id="l1-l3-first-runtime-verification"></a>

### 2.1 首轮联验判据与接续

1. 对照第 10 节调用清单、事件契约和各批回归，列出尚会阻断真实联验的已登记来源。随后确认单一隔离环境的架构、镜像 digest、Docker daemon 到 collector 的路径、服务与流标识、独立内网端口、专用凭据、测试存储、故障注入和清理。此清单用于确认本次运行范围。
2. 先以三个边界对账。API 5xx 的响应反馈 ID 与文件及真实 API 接收后 SQLite 的 canonical JSON 一致，旧事件缺省兼容，提交后响应丢失重投只保留一个事件。用受信的服务与流元数据识别 DbMigrate stdout 报告，使其在 JSON parser 前退出事件链。对独立启动 / 入库应急 stderr 验证有界证据和失败通道隔离，入库应急避开同一失败链。
3. PostgreSQL 的提交、重投与 Log 库断连恢复，以及 Docker Message mode 候选传输上界与坏批隔离均已通过。OrbStack 磁盘与队列联验复现了文件丢弃计数缺口、截断尾及入队前失败，按第 2 节推进收口。API 反馈 ID 的独立可空列与索引归 L3，精确受权筛选归 L4。Flutter 目标设备输出沿 Native 验收。

#### OrbStack / Linux arm64 隔离联验入口

从仓库根目录运行 `node Scripts/logging/collector-sqlite-probe.mjs --plan` 可预览镜像、端口和生成的采集配置。首轮复用 Fluent Bit 5.1.2 的 arm64 固定镜像，API / DbMigrate 使用同一工作树的 Debug 构建和本地 ASP.NET Core 10.0.14 容器镜像，故障注入中转使用 Node.js 24.16.0。API 另外需要通过 `node Scripts/build-native-images.mjs --debug --verify --rid=linux-arm64` 校验的 Linux 原生产物。

获当批启动授权后执行 `node Scripts/logging/collector-sqlite-probe.mjs --run`。Fluent Bit 的 Forward 只发布到 daemon 回环动态端口。API 5100、内网入库 15199、故障注入中转 8080、collector metrics 2020 都留在专用 Docker 网络。DbMigrate apply 从空白 SQLite 初始化。API 的 5xx 来自临时分类表更名，入库失败来自临时 SQLite 触发器，两种注入均在用例结束时撤销。中转在接收 API 确认提交后断开 HTTP 响应，用同批次摘要核对 Fluent Bit 重投。

隔离配置在 parser 前仅凭 Docker tag 和标准流元数据分流 DbMigrate stdout 与 API stderr，用容器的有界本地日志保留命令报告和应急证据。结束时按本次标签清理容器、网络、临时 SQLite、日志文件和凭据，保留 `artifacts/logging-sqlite-probe/` 下的断言结果与清理状态。清理失败时报告残留资源的位置和名称。该脚本的运行结果用于本轮 SQLite 联验，后续门禁仍按第 2 节执行。

<a id="l1-l3-postgres-runtime"></a>

#### PostgreSQL 隔离联验入口

`node Scripts/logging/collector-ingest-probe.mjs --plan --database=postgresql` 渲染镜像、端口和采集配置。复用 SQLite 联验的三个运行镜像及 Linux arm64 原生产物，并采用[先前固定的 PostgreSQL 16.15 镜像](postgres-regression-execution-plan.md)的 arm64 变体。镜像缺失时在当批范围内按 digest 拉取，运行脚本始终使用 `--pull never`。

```bash
docker --context orbstack pull --platform linux/arm64 postgres@sha256:a902560563d3a5661228585f53474c5065e7f273b21ffd77e555a8b5682c5690
node Scripts/logging/collector-ingest-probe.mjs --run --database=postgresql
```

PostgreSQL 5432 与原有 API、入库、中转及指标端口只供当批专用 Docker 网络访问。数据库使用该容器的临时内存挂载，由现有初始化脚本创建六个临时库，DbMigrate apply 执行正式迁移及严格校验。核对 `bigint` 列、UTC 列、端口隔离、5xx 反馈 ID 及提交后丢响应的重投去重。

故障注入仅对临时 `radish_log` 禁止新连接并终止现有会话。等待真实 API 返回 503 且采集文件收到测试事件，同时验证业务库查询正常。恢复连接后，对照同一批次摘要核对 Fluent Bit 重投成功、数据库一行及独立 stderr 分流。退出时按任务标签清理容器、网络、临时数据和凭据，保留 `artifacts/logging-postgresql-probe/` 下的断言结果。[首次运行结果](../records/unified-logging-l1-l3-postgres-runtime-2026-11-17.md)

<a id="l1-l3-transport-budget"></a>

### 2.2 候选传输预算与坏批联验

**适用范围**是现有回环 Forward 入口、固定 Fluent Bit 5.1.2、登记的 Docker `fluentd` Message mode 生产者及当前规范化链。预算以一次 append 最多一条普通日志记录、无独立 MessagePack metadata / group 标记为前提。Moby 驱动及其 logger 逐条生成 Message，固定版 Fluent Bit 的 Message 解码和入口队列也逐条追加。OrbStack daemon 已用合成记录核对这一形状。PackedForward、外部 Forward 客户端和新 metadata 需要单独推导预算。[Docker 日志驱动](https://docs.docker.com/engine/logging/drivers/fluentd/) · [Moby 驱动参考](https://github.com/moby/moby/blob/master/daemon/logger/fluentd/fluentd.go) · [Message-mode 解码](https://github.com/fluent/fluent-bit/blob/v5.1.2/plugins/in_forward/fw_prot.c#L1130-L1197) · [入口队列追加](https://github.com/fluent/fluent-bit/blob/v5.1.2/src/flb_input_ingest.c#L370-L397)

1. **Forward 与 chunk。** `Buffer_Max_Size 64K` 将连接接收缓冲限制为 `64,000` 字节。核心先筛选本次追加记录，再把规范化后的 MessagePack 写入 chunk，写后超过 `2,048,000` 字节才锁定，锁定后下一次追加另开 chunk。最后追加前最多已有 `2,048,000` 字节，另加一条最大规范记录（含普通事件封装）`3,260` 字节，合计 `2,051,260` 字节。最小规范消息体按 `396` 字节计，丢弃项为零条，拒收摘要仍为一条。每个 chunk 至多 `floor(2,048,000 / 396) + 1 = 5,172` 条。[Forward 配置](https://docs.fluentbit.io/manual/data-pipeline/inputs/forward) · [大小单位解析](https://github.com/fluent/fluent-bit/blob/v5.1.2/src/flb_utils.c#L527-L587) · [chunk 切分与写后锁定](https://github.com/fluent/fluent-bit/blob/v5.1.2/src/flb_input_chunk.c)
2. **HTTP 编码。** 当前 `json_lines` 无额外时间键，编码普通记录体并每条追加一个 LF。在受控 ASCII 文本、数字、布尔和对象的字段集合内，将 JSON 序列化含 LF 预算取为 MessagePack 的 6 倍。文本按每字节最多两个 JSON 字符计，键值分隔符计入键开销，布尔最多 5 字节，当前有限数值最多 24 字节。单行按全部白名单字段的保守组合得到 `4,212` 字节，整请求按 chunk 得到 `12,307,560` 字节，均低于接收器 `8 KiB / 16 MiB / 32,768 条` 门禁。运行 `node Scripts/logging/check-transport-budget.mjs` 可以按现有事件策略重算。[MessagePack 到 JSONL](https://github.com/fluent/fluent-bit/blob/v5.1.2/src/flb_pack.c) · [HTTP 插件](https://github.com/fluent/fluent-bit/blob/v5.1.2/plugins/out_http/http.c)
3. **内存口径。** 输入的连接缓冲、处理中间对象、常驻 chunk、同时 flush 的序列化缓冲和重试积压均占内存。服务端每个激活请求用 `4 KiB` 读取缓冲、`8 KiB` 行缓冲及最多 `200` 条待提交记录。HTTP 请求体字节数与进程 RSS 分别度量。运行时记录并发连接、输出 worker、up chunk 和缓冲队列配置，并在正常负载及单支路短暂不可用时记录峰值和恢复后水位。

| 联验组 | 判据 |
| --- | --- |
| 合法流量与上界 | 在同一 Docker tag 上分别连续输出最短规范事件、全长标识及最大字段组合、混合事件。记录实际 HTTP 请求字节数、行数、最大行及 SQLite 的事件 ID 对账，确认跨 chunk 轮转仍全部交付且无 413。显式采样目标 daemon 的 Message 类型和每次追加条数。 |
| 来源坏记录与语义拒收 | 将格式无效的合成日志与有效事件混发，验证 collector 输出固定 `pipeline.rejected` 摘要、相邻有效 ID 到达两条支路、原文不进入缓冲持久化与最终输出。直接接收时的语义无效 JSON 另外核对有限拒收桶与有效记录提交。 |
| 坏信封与后续批次 | 在隔离接收端复核 400 / 413 和已提交前缀计数。采集器分支对确认不可重试的坏 chunk 应暴露丢弃，后续正常批次继续到达。用安全摘要记录故障与损失范围。收到 408、429 或 503 时按重试语义对账。 |

#### OrbStack 传输联验的运行范围

在仓库根目录预览配置，获得本批启动授权后执行第二条命令。运行入口使用本机缓存的 Fluent Bit 5.1.2、ASP.NET Core 10.0.14、Node.js 24.16.0 镜像和现有 Linux arm64 Debug 产物。完整 digest 随预览与报告保留。运行脚本使用 `--pull never`，由 DbMigrate apply 创建临时 SQLite。

```bash
node Scripts/logging/collector-ingest-probe.mjs --plan --database=sqlite --suite=transport
node Scripts/logging/collector-ingest-probe.mjs --run --database=sqlite --suite=transport
```

- **端口与负载。** Forward 仅发布到 daemon 回环动态端口，经中转 24224 原样进入 Fluent Bit 24224。HTTP 中转 8080、collector metrics 2020、API 5100、专用接收端 15199 留在本次专用 Docker 网络。两组合法负载分别为 5,200 条精简事件与 1,200 条最大字段组合。另有 3 条混合记录、2 条终止错误用例和 1 条恢复探针，专用接收端再测试字节、条数与单行门限。临时文件轮转单文件预算使用 16M，方便对账整个用例，采集 flush 和队列预算沿用已验证的配置。
- **证据。** 中转只记帧类型对应的条数、大小与合成生产者标识。观察到每行对应一个 Message frame 后，结合固定版入口源码核对逐条追加。HTTP 中转记录原始 JSONL 请求字节、条数、单行最大值与接收状态。进程内存用 Linux `VmRSS` 和 `VmHWM` 报告，Fluent Bit 通过联验中转的共享 PID namespace 读取，API 读取其容器 PID 1。采样点包括就绪、正常负载后、503 重试积压、恢复和接收端门限验证后。`VmHWM` 是截至采样点的进程累计 RSS 峰值。
- **故障与恢复。** 只对临时库目标事件注入一次可撤销的写入阻断。合成混合记录对账文件、SQLite 和同批重投。两组终止 HTTP 错误由中转仅对指定测试事件变更请求，确认 API 返回 400 / 413、collector 计数丢弃、文件分支保留事件及后续正常批次入库。报告保留断言、各阶段内存读数和最大请求尺寸。
- **清理。** 脚本启动时打印本次 `radish-logging-transport-<id>` 和临时目录。退出时按精确 `radish.logging.probe=<本次 id>` 标签删除本次 API、DbMigrate、collector、中转和合成生产者容器及其临时卷，再删除专用网络和打印的单一 `/private/tmp/radish-logging-transport-…` 目录。目录包括 SQLite、日志／缓冲、合成输入和凭据。保留本机镜像缓存与 `artifacts/logging-transport-probe/<本次 id>.json`。清理失败时报告具体资源及目录。

强制终止后，可用本次输出的完整 ID 核对资源，再按资源 ID 和打印的目录精确清理。

```bash
probe_id='radish-logging-transport-实际运行ID'
docker --context orbstack ps -a --filter "label=radish.logging.probe=$probe_id"
docker --context orbstack network ls --filter "label=radish.logging.probe=$probe_id"
```

本入口已通过该隔离环境的全部用例，实测数据见[传输联验记录](../records/unified-logging-l1-l3-transport-runtime-2026-11-17.md)。正式部署配置按目标部署批次冻结，改变上述预算前提时重新核对。

<a id="l1-l3-disk-queue-runtime"></a>

### 2.3 OrbStack 磁盘与队列故障联验

在仓库根目录预览配置，获本批启动授权后执行第二条。复用本机缓存的 Fluent Bit 5.1.2、ASP.NET Core 10.0.14、Node.js 24.16.0 及现有 Linux arm64 Debug 产物。运行使用 `--pull never`，由 DbMigrate apply 初始化临时 SQLite，镜像 digest 随预览和报告保留。

```bash
node Scripts/logging/collector-ingest-probe.mjs --plan --database=sqlite --suite=disk-queue
node Scripts/logging/collector-ingest-probe.mjs --run --database=sqlite --suite=disk-queue
```

**容量与网络。** 一个带任务标签的 24 MiB Docker `local` / tmpfs 命名卷同时挂入 Fluent Bit 和中转容器的 `/probe-disk`。脚本核对挂载身份、容量和设备号后，才允许在 `files/probe-fill.bin` 写入占位数据并捕获真实 `write` 的 `ENOSPC`。`files` 与 `buffer` 同盘可争用，SQLite、配置和凭据位于各自的临时挂载。文件轮转阈值设为 16M，flush 间隔设为 10 秒以便核对落盘后、flush 前的窗口。两支路仍分别使用 4M 队列。Forward 只发布回环动态端口，经中转 24224 到 collector 24224。API 5100、内网入库 15199、中转 8080、指标 2020 留在专用 Docker 网络。该容量探针覆盖 Linux tmpfs 的写入空间争用，块设备性能与持久化另行验证。[Docker local 卷选项](https://docs.docker.com/reference/cli/docker/volume/create/#driver-specific-options--o---opt)

| 场景 | 判据 |
| --- | --- |
| 已写入缓冲后填满共享卷 | 先确认 128 条合成记录存在于缓冲且尚未 flush，再从文件侧占满卷。记录 `ENOSPC`、两边实际占用、HTTP 入库结果及文件缺口。释放占位文件，核对新事件恢复两路交付。 |
| 卷满后追加 | 卷在新记录到来前已为零可用空间。用 Forward 计数和 collector 存储诊断或退出状态定位故障发生阶段，记录两支路缺口。释放空间，若 collector 已退出则启动本次同一容器，再核对新事件和残留缓冲。 |
| HTTP 队列溢出 | 卷有余量时，在临时 SQLite 使特定合成事件返回真实 503。分五波发送共 12,000 条，每波先核对文件支路，再观察 HTTP 队列丢弃。撤销故障，核对存活积压重投、新事件两路交付、缺口及缓冲排空。 |

以合成事件 ID 对账文件和 SQLite，逐阶段列出文件缺口、HTTP 缺口与两边都缺的重叠项，并记录 collector 的 `errors` / `dropped_records` 增量、差额及不完整文件尾。固定版 Fluent Bit 的 plain 文件输出路径在写入与关闭后仍可能返回成功，因此计数与事件对账都保留，实际差额留作故障证据。[固定版文件输出实现](https://github.com/fluent/fluent-bit/blob/v5.1.2/plugins/out_file/file.c#L1365-L1416)

**清理范围。** 启动时打印完整的 `radish-logging-disk-queue-…` 任务 ID 和临时目录。退出时删除该任务标签下的启动、API、collector、中转和合成生产者容器、专用网络及精确命名卷 `<本次 id>-shared-disk`。删除打印的 `/private/tmp` 目录及其中的 SQLite、配置、凭据、指标中转记录和合成负载。保留本机镜像缓存与 `artifacts/logging-disk-queue-probe/<本次 id>.json`。强制终止后用打印的完整 ID 核对资源，再按对象 ID 和精确目录清理。

```bash
probe_id='radish-logging-disk-queue-实际运行ID'
docker --context orbstack ps -a --filter "label=radish.logging.probe=$probe_id"
docker --context orbstack network ls --filter "label=radish.logging.probe=$probe_id"
docker --context orbstack volume ls --filter "label=radish.logging.probe=$probe_id"
```

2026-11-17 复验四项通过。共享卷写满时文件支路实际缺 123 条，文件丢弃计数增量为 0。满卷后的单条事件在创建 chunk 前丢失，两个输出计数均无增量。HTTP 队列溢出实际缺 7,200 条，与丢弃计数一致。释放共享卷和撤销 503 后，两条支路均恢复交付。探针用活跃 chunk 和非空 chunk 文件共同判断排空，另行记录空文件残留。[联验记录](../records/unified-logging-l1-l3-disk-queue-runtime-2026-11-17.md)

<a id="l1-l3-disk-queue-followup"></a>

### 2.4 文件写入与入队故障收口（候选待联验）

#### 固定版根因

- 静态路径的 `plain` 文件输出忽略 `fprintf` 的结果，轮转更新在 `fflush` 失败时只打印诊断，关闭过程也未向 flush 回调返回失败。回调仍返回 `FLB_OK`，引擎因而按整个 chunk 记成功。系统写到半条 JSONL 后遇到空间耗尽，后续追加沿用该文件，首条新事件可能与残尾拼成坏行。路径打开失败是另一条返回 `FLB_ERROR` 的路径。[plain 输出](https://github.com/fluent/fluent-bit/blob/v5.1.2/plugins/out_file/file.c#L882-L893) · [flush 返回](https://github.com/fluent/fluent-bit/blob/v5.1.2/plugins/out_file/file.c#L1408-L1425) · [轮转刷新](https://github.com/fluent/fluent-bit/blob/v5.1.2/plugins/out_file/file_rotate.c#L757-L774) · [引擎计数](https://github.com/fluent/fluent-bit/blob/v5.1.2/src/flb_engine.c#L624-L647)
- 创建 chunk 失败时，`input_chunk_get` 提前返回空值，记录还未进入输出队列。普通 `input_records_total` 在部分后续写失败路径上已先增加，不能用输入和输出累加值相减推算这一阶段的丢失。延迟 worker 路径还会为同一次失败打印后续诊断，错误行数也无法当作记录数。[输入创建与写入顺序](https://github.com/fluent/fluent-bit/blob/v5.1.2/src/flb_input_chunk.c#L3300-L3430) · [延迟写入路径](https://github.com/fluent/fluent-bit/blob/v5.1.2/src/flb_input_ingest.c#L375-L431)

#### 已批准的实施范围

1. 在 Fluent Bit 5.1.2 候选源码上验证可独立审阅的补丁。文件支路沿用静态 JSONL 路径、现有轮转和有限队列。在同一文件的写入锁内记录本批开始偏移，检查编码、追加及刷新结果。写失败且确认已回滚到该偏移时，以整个 chunk 进入文件支路重试。轮转并发、异常关闭或无法确认回滚时，先将文件支路标为故障，暂停往该文件追加并报告结果未知。恢复只处理活动文件末端可确认的不完整记录，修复或确认边界后再追加。整块重试耗尽或被队列淘汰时，由引擎记录文件支路丢弃量。已确认成功指本次文件写入完成，断电持久性沿原验证边界。
2. 在 Forward 的输入存储追加失败点增加计数，首版覆盖 chunk 创建失败和存储写入失败。用输入实例及固定阶段区分，按本次失败追加的记录数累计一次，输出淘汰仍由各支路计数。单条上游事件在分流前失败时只记一次，计数范围限于采集器已收到并可确认的记录。诊断只带阶段、固定原因及数量，走 collector 自身的有界通道，由内网指标独立读取。进程不可读或写入结果不确定时标为未知。
3. 静态核对补丁的来源、构建输入与许可。后续候选镜像单独固定 digest，以 SQLite 和现有联验负载比较原版与候选的行为。实施边界为 collector、离线回归及其联验探针；现有 HTTP 接收、合成事件契约和生产部署沿各自批次推进。

| 验收场景 | 需取得的证据 |
| --- | --- |
| 已有 chunk 后文件卷 ENOSPC 及文件路径权限故障 | HTTP 在故障期仍能入库。对文件支路分别核对已交付、待重试和最终丢弃，终态丢失量与输出计数一致。 |
| 短写、刷新或关闭失败，含轮转前后 | 旧的完整记录保留。可回滚的 chunk 在恢复后只完整写入一次，前后两条探针都完整落盘。无法核对的写入结果明确进入未知，避免继续污染 JSONL。 |
| 写满缓冲卷后追加及后续写入失败 | 分流前失败的合成记录数与新增输入计数一致，两个输出不重复加记。释放空间后两条支路恢复，诊断可在故障期间独立读取。 |
| 共享卷恢复、HTTP 503 和队列溢出回归 | HTTP 仍交付存活积压，终态缺口与其丢弃量一致。磁盘占用、诊断存储与排空可核验。 |

候选实现在 [5.1.2 补丁](../../Deploy/logging/fluent-bit/5.1.2-storage-errors.patch)。`radish_jsonl` 只在候选探针启用。使用无 stdio 写缓冲的 POSIX 追加，检查短写和关闭结果，回滚后核对原文件大小。进入轮转前先验证活动文件尾，尾部扫描上限 8 KiB。关闭或回滚结果不确定时，疑似已写入的批次终止重放，对该 inode 暂停追加并增加未知批次计数。探针或运维在确认故障文件后以替换活动文件恢复；候选验证要求单写者，文件长度和完整行的证明只覆盖当前故障运行区间。

内网 `/api/v2/metrics/prometheus` 提供 `fluentbit_input_storage_append_failed_records_total{stage="chunk_create|chunk_write"}`、`fluentbit_output_radish_file_blocked` 和 `fluentbit_output_radish_file_unknown_batches_total`。未知批次与引擎丢弃计数需要合读，未知批次可能已有一部分成功保留。collector 诊断由 Docker `local` 驱动保留，每个容器设 `max-size=1m`、`max-file=2`，采集故障事件不回流到当前 Forward。

#### 构建与 OrbStack 联验入口

上游 Apache-2.0 源码固定为 `66910c10a4d7eafa810b84229cf2dcf7b0f26f97`，归档 SHA-256 及补丁适用性由准备脚本核验。六种文件故障与恢复检查已通过，macOS arm64 已编译文件插件和 Fluent Bit 核心静态目标。候选镜像编译使用 [Dockerfile](../../Deploy/logging/fluent-bit/Dockerfile) 固定的 Debian arm64 基础镜像和 APT 快照，运行层沿用原 Fluent Bit 5.1.2 固定镜像。候选镜像构建和服务启动各按下面的精确范围取得授权。

```bash
probe_root=$(mktemp -d /private/tmp/radish-fluent-bit-build-XXXXXX)
curl --fail --location --output "$probe_root/upstream.tar.gz" \
  https://codeload.github.com/fluent/fluent-bit/tar.gz/66910c10a4d7eafa810b84229cf2dcf7b0f26f97
node Scripts/logging/collector-candidate.mjs --verify "$probe_root/upstream.tar.gz"
node Scripts/logging/collector-candidate.mjs --stage "$probe_root/upstream.tar.gz" "$probe_root/context"

probe_image="radish/fluent-bit-storage-probe:${probe_root##*/}"
docker --context orbstack buildx build --builder orbstack --platform linux/arm64 --load \
  --tag "$probe_image" "$probe_root/context"
probe_image_id=$(docker --context orbstack image inspect --format '{{.Id}}' "$probe_image")

node Scripts/logging/collector-ingest-probe.mjs --plan --database=sqlite --suite=disk-queue --collector-image="$probe_image_id"
node Scripts/logging/collector-ingest-probe.mjs --run --database=sqlite --suite=disk-queue --collector-image="$probe_image_id"
```

联验按 §2.3 复用 SQLite、24 MiB 共享卷、回环动态 Forward 端口和两条 4M 队列。用 UID 10001 的 collector 测试文件拒写。输入侧先在满卷上创建 chunk，再用同一个 Docker tag 的两波输入测试已有 chunk 的扩容失败。文件轮转阈值设为 4M，以便在五波队列用例中穿过轮转并对账。运行保留原版和候选的独立 JSON 报告，按 §2.3 回收容器、网络、卷和临时数据库。构建目录 `$probe_root` 可按本次精确路径移除，候选镜像及本机构建缓存用于复验。OrbStack 构建和故障联验仍待执行，完成后接续留存与备份恢复。

## 3. L2 剩余来源与退出条件

本节列出的八个入口已完成本地切片：PRAGMA 随 L3-A 交付，其余七个来源按第 8 节批准后完成。结果见[本批记录](../records/unified-logging-l2-owned-operations-2026-11-05.md)。这不是全仓运行覆盖证明，下面的其他退出工作仍保留。

| 来源 / 代码入口 | 本地实施结果 | 仍保留的边界 |
| --- | --- | --- |
| `UserController.GetUserByHttpContext` | 移除正常过程；权限 / 头像失败生成固定安全降级摘要 | 空权限集 / 忽略头像与其他失败外抛语义保持；真实登录读取未验收 |
| `ClientController` | 查询失败与四种写入结果使用固定事件；直接消费者 ApiExceptionHandler 补最终输出重建 | 业务调用 / 响应不变；真实 OpenIddict 存储与密钥轮换未验收 |
| `BootstrapService` | 首管理员奖励失败只记录固定部分完成摘要 | 不推定奖励未写入或注册回滚；真实奖励事务未验收 |
| `NotificationDedupService` | 缓存检查 / 写入 / 删除失败安全输出；正常 Debug 移除 | 当前仅发现构造注入，未检出业务方法调用；直接服务测试不证明生产可达 |
| `ChatMessageSearchService` | 移除成功 Info 与专用计时，保留注入签名 | 查询、游标与结果保持；真实搜索存储未验收 |
| `RateLimitSetup` | 一条注册摘要；逐请求观察仅显式开发诊断；内存管道覆盖 429 / 403 | 黑名单分区返回 GetNoLimiter 的既有行为不修复；未启动真实宿主 |
| `ExperienceExtensionSetup` | 移除重复注册 Info，保留注册与生命周期 | 不扩展经验业务行为 |
| `SqlSugarSetup.ExecuteSqlitePragma` | L3-A 已完成独立安全应急输出，保留 false 返回 | 真实数据库初始化故障仍待运行验收 |

其他必须保留的退出工作：

1. **已登记业务来源最终出口核验**：生成端安全不代表旧 sink 上继承的任意 scope 已受控。第 9 节的 14 个 Controller 已完成精确来源与最终输出收口，见[实施记录](../records/unified-logging-l2-controller-output-2026-11-05.md)。其余 Service / Job / Hub 按现有注册来源建立“生成点 → 消费边界 → 旧 / 候选输出 → 证据”的清单；已有完整证据直接引用，只补缺口，不重新调查任意外部订阅器。
2. **诊断信息完整性**。API 5xx 的反馈 `requestId` 已绑定并可随 canonical JSON 接收。L3 的精确索引与 L4 的受权筛选待对应批次。jobId、tenantId、异常类型及安全栈帧按第 10.14 节后置。
3. **裸输出例外**。DbMigrate 命令报告走 stdout，阶段诊断与有界应急走 stderr。报告标签已转义并限长，无调用的 `ConsoleHelper` 已移除。受信流识别及独立应急证据路线待 L1 / L3 联验。
4. **客户端与原生边界**。浏览器本地公共出口已完成，Flutter 应用框架报告和显式引导失败已收口。root isolate、原生引擎 / 插件及子 isolate 的输出随目标平台验收。
5. **成组运行验收**：真实登录、业务读取 / 写入、后台空轮询、网络 / 存储故障须在后续授权环境按已登记链验证安静性、安全及异常所有权；模块替身与内存管道不替代它们。

不再挂回当前队列：第 57 节已关闭的早期错误结果构造问题、第 69 节已登记 DataProtection 来源、Hangfire 第 73 节、浏览器第 74 节。未注册 OpenIddict 集成、无产品调用的 Socket `.Client`、HttpSys delegation 与外部 EventPipe / Activity / metrics 保持原专题边界。

L2 整体退出要求：上述实际接线来源完成分类，正常生产不生成开发载荷，最终输出仅含受控字段，异常所有权和原业务结果通过回归；命令协议 / 应急例外明确。不能凭日志调用数量下降或候选 sink 的统一裁剪宣称退出。

## 4. L3-A 接收契约

### 4.1 接口与隔离

- `POST /internal/logs/ingest` 默认关闭。启用后由 API 的独立内网端口接收，当前端口只接受该接收路径。普通业务端口拒绝 `/internal`。
- 判断实际连接的本地端口，不信任 Host、转发头或请求路径代替监听隔离；Gateway 不转发 `/internal`。校验在读取请求体及通用审计之前完成。
- 使用专用 collector 凭据，独立于用户 JWT；凭据绑定配置中的 deployment 和允许 service 集合，载荷不得自行扩权。配置缺失、端口冲突或重复绑定应阻止启用；日志不得回显凭据。
- 配置仍归 `RadishLogging`、共享 / 宿主 appsettings 与未提交的本地覆盖；本批不写 production Compose、不公开宿主端口。跨主机 TLS / mTLS 保留后续范围。

### 4.2 有界读取与确认

| 项目 | L3-A 候选约束 |
| --- | --- |
| 输入 | UTF-8 JSONL；严格逐行解析，拒绝重复 JSON 字段、非法编码与不支持的压缩；空信封拒绝，不整包读入内存 |
| 大小 | 单条规范事件沿用 8 KiB；信封暂以 16 MiB / 32768 条为受控测试候选，**不是生产传输保证**；空行也计入字节预算 |
| 资源 | 内部最多 200 条一个事务；入口限制并发、读取时间与数据库命令时间；默认候选并发 2，超额返回 429，不进入无界等待队列 |
| 内容 | schema / 等级 / 诊断 / 来源 / 时间格式 / UUID / 允许字段按共享策略校验；拒绝未授权来源和 Production diagnostic，不重新生成合法事件 ID 或覆盖发生时间 |
| 成功 | 有效记录及本事务拒收摘要持久化后才算该批提交；读取完信封且全部内部批次提交后返回 200 与受控计数 Vo |
| 暂时失败 | 数据库不可用、事务失败返回可重试 503；已提交内部批次保留，整包重放依赖唯一键，不伪造全包回滚 |
| 取消 / 响应丢失 | 取消未提交事务，已提交记录不撤销；提交后断连不生成第二个 eventId，collector 重放去重 |
| 语义无效单条 | 不写原文，持久化有限原因摘要；其他有效记录继续；不得因为丢掉坏记录而不报告拒收数 |
| 认证 / 格式 / 超限 | 认证失败 401/403；语法损坏或不支持编码 400；超限 413。不得假成功；这些状态必须与 L1 前置隔离联验后才准接入真实 collector |

在后段发现语法错误 / 超限时，前面已提交的内部批次可能存在；返回值必须准确表达失败，不能称整包原子。接收器不会因 413 自动保存剩余原文；合法 chunk 的上界与坏批隔离仍由第 2 节关闭，否则停在候选状态。

接口由 Controller 注入 `IRuntimeLogIngestService`；Service 执行校验和批次编排，Repository 执行事务与幂等写入，不向 Controller 暴露实体。响应使用 `MessageModel<RuntimeLogIngestVo>` 和 `Vo` 字段，只含安全计数 / 固定状态，不回传行内容或第三方错误。

### 4.3 数据模型与幂等

- Log 库新增持久实体 `RuntimeLogEventRecord`，物理表显式命名为 `runtime_log_event`，与 Common 的生成事件明确区分；不继承旧 `BaseLog` 或默认业务软删除。
- 保存原事件核心字段，新增 UTC `IngestedAtUtc` 和存储主键；过滤字段独立列，白名单属性存有界 JSON。服务端规范化生成稳定核心内容摘要，排除本次入库时间。
- 唯一键 `(DeploymentId, EventId)` 是并发裁决者，不能仅“先查后插”。同键同内容为重复，同键异内容拒收并保留先写事件，不能静默覆盖；实现分别验证 SQLite 与 PostgreSQL 的冲突语义。
- 原 `OccurredAtUtc / ObservedAtUtc` 保留毫秒精度与 UTC；错误时间进入拒收，不用当前时间冒充；不得把旧 `DateTime.Now` 语义带入新表。
- 索引覆盖发生时间 + EventId、服务 + 时间、等级 + 时间、TraceId / OperationId；不在本批增加查询 API、任意全文检索或物理分区。
- 拒收摘要按受信部署 / 服务、固定原因和 UTC 时间桶有界聚合，不保存原文、任意来源字符串或坏 eventId。事务内更新并设置计数上界；重放可能重复计入无有效 ID 的拒收次数，明确这是拒收尝试数，不宣称唯一坏事件数。物理表为 `runtime_log_rejection_bucket`，保留当前 UTC 日及此前 6 日，每次成功事务最多清理 200 个过期桶；停收时不额外启动后台清理。
- 幂等保证限于事件键仍保留期间；L3-B 必须协调保留期、最大重放时窗与去重记录寿命，过期旧文件不能因键已清理被自动重导并触发新告警。本批无自动历史补录。

### 4.4 migration 与故障通道

- 按[结构变更治理](../guide/database-schema-change-governance.md)新增 Log scope 的有序 migration、checksum、前置条件和 Verify；接入 `init / apply / doctor / verify`，不得改已发布 migration 或让 API 启动建表。
- 覆盖空库、已有 baseline + pending、新结构重入、结构漂移拒绝。旧分级表和审计表保留；本批没有删除 / 回填旧原文或改变权威审计规则。
- 新写入专用 Repository 复用现有 Log 连接选择，不为 collector 配置数据库凭据，也不让 Common 依赖 Model / Service。
- 接收请求排除通用请求体审计、正常请求完成日志与逐批成功日志；保持 Log SQL AOP 排除。数据库失败、校验失败与 SQLite 初始化失败使用独立限频安全诊断，不写回同一数据库 sink。
- 管道保存 accepted / duplicate / rejected / failed 与最近成功提交时间等有限状态；数据库断开时仍可返回失败 / Unknown，不能靠“成功写入日志”证明日志链健康。本批不实现 Console 状态页面或告警计算。
- 回退关闭新监听与接收功能，保留新表与已收数据，旧链仍可运行；不执行 Down / 删除表 / 清空日志。跨旧版本的完整部署与恢复演练归 L3-B / L6。

## 5. 改动面与验证矩阵

实际改动范围：`Radish.Model/LogModels`、`Radish.Model/DtoModels`、`Radish.Model/ViewModels`、`Radish.IRepository` / `Radish.Repository`、`Radish.IService` / `Radish.Service`、API 内网路由与管道接线、DbMigrate 注册 / 结构验证、共享配置与日志策略、定向测试。`SqlSugarSetup` 只调整上述直接相关日志边界。实际文件按既有职责拆分，不建立通用新框架。

| 验证层 | 必须覆盖 | 证据限制 |
| --- | --- | --- |
| 无服务本地检查 | 构建、定向测试、日志契约、文档 / 链接 / 卫生、diff | 不代表端口隔离和真实 collector 已通过 |
| 内存 HTTP / 合成输入 | 公共端口与内网分流、凭据错误、非法来源、重复字段、边界长度、时间格式、生产诊断、隐私标记、取消与资源释放 | 真实 Kestrel 监听需要后续运行验收 |
| 临时 SQLite | 唯一键并发、跨内部批次失败、同键冲突、响应丢失重放、旧基线升级 / 重入 / verify、受控锁冲突 | 不替代 PostgreSQL 或全宿主启动 |
| 失败与递归 | DB 故障、PRAGMA 失败、应急输出失败 / 重入、计数上界；stdout / stderr / 拒收存储均无合成秘密 | 不替代 ENOSPC 与共享磁盘故障 |
| 授权后的 PostgreSQL 16 / 实际监听 | 与 SQLite 一致的事务与 UTC 契约、migration / 索引、连接失败恢复、端口双向隔离 | 未执行前只能报告本地实现完成，不能关闭 L3-A 全部验收 |
| L1 / L3-B 联验 | Fluent Bit → API → Log 库、413 / 429 / 503、提交后断连、恢复、文件一致性与坏批隔离 | 通过后才可申请部署接入，不等于 L6 切换 |

L3-A 本地实施可使用已安装依赖、内存管道和测试创建的临时 SQLite；不启动项目服务或容器。测试临时目录在结束时精确清理。无需为文档规划重跑产品全量测试。

## 6. 批次批准与停止线

项目所有者已批准第 4–5 节的 L3-A 代码、迁移、临时 SQLite / 内存管道验证与文档收口；包括直接相关 PRAGMA 日志防递归，第 3 节其余七个来源后续已按第 8 节单独获批并完成本地实施。L3-A 已提交为 `7002cd75`。服务与容器启动另行确认。

服务 / 容器验证在准备好可审阅脚本后，另列确切命令、镜像 digest、回环端口、目录、数据库与清理清单申请授权；本页不沿用 9 月启动许可，不请求笼统授权未来运行。缺少 PostgreSQL / 监听 / collector 证据时明确保留对应门禁。

出现以下情况时停止扩张并报告：必须新增依赖；现有分层无法承接接收事务；需要改变原业务结果；无法证明合法 chunk 上界却准备启用真实采集；需要改生产配置、迁移真实库或扩大平台范围。方案批准不代表 L1 / L2 / L3 整体完成，也不自动授权 push、PR、发布或部署。

## 7. 当前配置与验证入口

接收开关为 `RadishLogging:Ingest:Enabled`（默认 false），与生成端 `RadishLogging:Enabled` 分开。接收模式继承受信的 `RadishLogging:Mode`；载荷模式必须一致，Development 仅在 Development 宿主允许，不能由事件自行开启诊断。

启用候选时必须显式提供 `Kestrel:Endpoints:<name>:Url` 公共监听、独立 `Ingest:Port`（1024–65535）、`DeploymentId`、允许的 `Services` 与 `Credential`。缺失或公共端口冲突均失败；不能只设置 URLs 再让专用 Listen 覆盖原公共监听。实际绑定与网络可达性仍待运行验收。凭据只通过未提交的本地配置或 `RadishLogging__Ingest__Credential` 注入，不放入共享文件；HTTP 使用 `X-Radish-Collector-Key`，允许 `application/x-ndjson` / `application/json`，不支持压缩。

并发默认 2（允许 1–8）、请求时限默认 30 秒（1–120）、数据库命令时限默认 5 秒（1–30）；字段分别为 `MaxConcurrentRequests`、`RequestTimeoutSeconds`、`DatabaseTimeoutSeconds`。请求时限到达返回 408，调用方取消为 499；真实断连可能无法收到响应。返回计数只包含服务已确认完成的内部事务；数据库提交结果不确定时不能据此推定未写入，必须沿原事件 ID 重放。

`RuntimeLogIngestState` 仅保留本进程已接纳处理的提交计数、处理失败次数与最近确认提交时间，重启清零；它不是持久计数、跨实例汇总或对外查询接口。认证拒绝与尚未进入 Service 的请求不计入这些处理计数。独立 stderr 诊断每分钟最多尝试一次，输出失败保留计数但不保证落盘。

无需服务的专项入口：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~RuntimeLogIngest&Database!=PostgreSQL'
npm run check:logging-contract
```

新增 PostgreSQL 集成测试带 `Database=PostgreSQL` trait，只有另行授权隔离连接后才能执行；本批未执行。现有 Log baseline 升级走 `20261105_023_runtime_log_ingest`；尚无 baseline 的旧库仍须满足当前结构接管规则，不借新迁移自动放宽接管或修补已有漂移。

`20261105_023_runtime_log_ingest` 对拒收计数 `attempt_count` 按数据库验证 64 位存储。PostgreSQL 接受 `bigint` 和 SqlSugar 元数据返回的 `int8`，拒绝 32 位 `integer` / `int4`。SQLite 继续接受 `INTEGER`，迁移 ID 和账本校验和保持原值。

## 8. 已批准切片：剩余七个自有来源

2026-11-05 基于 `7002cd75` 完成只读核对，方案由 `29e8b4f4` 保存。状态：**项目所有者已批准，本地实施与回归已完成**。本批一次处理第 3 节剩余七个来源的生成、最终出口与回归，不逐文件重复申请；不扩展到所有旧业务来源，也不把本批完成等同于 L2 整体退出。

### 8.1 源码事实与处理边界

| 来源 | 实施内容 | 必须保留的语义 / 证据边界 |
| --- | --- | --- |
| 当前用户组装 | 移除四条正常过程 Info；权限 / 头像失败分别生成固定降级事件，只带 failureKind | 查询失败仍返回空权限集 / 忽略头像；用户读取本身的外抛失败不新增本地消费；不记录身份、角色或头像地址 |
| 客户端管理 | 列表 / 详情消费失败改为固定动作与 failureKind；创建 / 更新 / 软删除 / 密钥重置完成只带固定动作 | 写操作 catch 仍构造并外抛既有 BusinessException，由既有异常边界负责，不额外写本地失败事件；保留 manager 调用与响应，不把成功返回等同于整个请求最终完成 |
| 首管理员创建 | 奖励失败改为固定部分完成摘要，只带 failureKind | 管理员已创建的事实不回滚；不声称奖励必然未写入、已经发放或后续自动重试；下层既有重试观察不等于最终失败重复消费 |
| 通知去重 | 三类缓存失败分别使用固定操作枚举；移除正常 Debug 中的业务键 | 当前仅在 PostService / CommentService 构造签名发现注入，未检出生产方法调用；保留服务及接口，不新增调用。直接服务测试证明检查失败返回 false、写入 / 删除失败被消费，不宣称运行链已触达 |
| 聊天搜索 | 移除逐次成功 Info 及仅用于该输出的计时 | 保留搜索校验、可见范围、分页游标、结果与异常；不新增关键词、频道或用户日志 |
| 限流扩展 | 合并为一个固定注册摘要；白名单 / 黑名单观察及拒绝信息只允许显式开发诊断，受 EnableLogging 与宿主诊断门禁共同控制 | Production 不逐请求发告警；禁止 IP、路径、请求头、Lease 或自由方法文本。黑名单分区当前返回 GetNoLimiter，而 403 在 OnRejected 中处理，本批不修复该既有决策问题，也不把“命中黑名单”描述为“已拒绝” |
| 经验扩展 | 移除重复的注册 Info | 服务注册顺序与生命周期保持，不引入新输出来源 |

### 8.2 最终输出与异常所有权

- 正常过程直接移除；需要保留的状态、降级及诊断使用固定事件目录和封闭字段。不为这批增加任意字符串上下文、异常正文 / 栈、用户 / 客户端 ID、密钥、URL 或缓存键。
- 给保留事件建立精确来源身份；复用现有最终 sink 包装接线，在上下文合并后重建旧链代码 / 配置 WriteTo、AuditTo 与候选输出。按来源匹配事件，不用宽前缀或只有 EventCode 的全局拦截，也不依靠生成端安全推定 scope 安全。
- 同来源未知事件保留严重失败的固定 unknown 摘要；正常未知与开发诊断受明确门禁控制。只保留原时间、等级、固定分类、允许字段及强类型 trace / span，不从自由字符串恢复关联键。
- User 两种降级、Client 两种查询失败、Bootstrap 奖励失败和 Dedup 三类缓存失败各由当前消费边界记录；原本外抛的 Client 写入失败继续交给既有异常边界。OpenIddict manager 精确来源治理沿用事件契约第 66 节，只验证衔接，不重新扩大框架范围。
- 保留 WriteTo 消费 sink 异常、AuditTo 可传播异常和候选应急通道的既有区别；输出异常可能影响 catch / 结果构造时，测试和文档如实描述，不借日志治理新增吞错或回滚承诺。

### 8.3 实施与验收

1. 更新上述来源、必要的精确输出适配与共享事件策略；不改 API、数据模型、限流算法、业务事务或依赖版本。保留既有注入接口，避免为了移除一条输出扩大调用方改造。
2. 使用真实 Controller / Service / 限流选项及内存 HTTP 管道，依赖使用现有替身；覆盖业务返回、降级与外抛边界、正常安静性、模式 / 诊断开关及 EnableLogging。不绑定端口或执行项目 Start / Run。
3. 覆盖旧链与候选输出、任意 scope 污染、未知 / 错误事件身份、合成秘密、WriteTo / AuditTo 故障和候选应急输出；断言最终序列化结果而非只看生成参数。并复用 ClientController、BootstrapService、UserControllerProfile、ChatMessageSearchService 等已有回归。
4. 执行定向测试、解决方案构建、日志契约、身份相关定向检查、文档 / 链接 / 文本卫生和 diff 检查，按真实结果更新事件契约与当前计划。已有 Wiki 时间语义 baseline 问题不顺手改动或刷新基线。

本批不安装依赖、不启动服务 / 容器、不连接真实存储、不开启生产汇聚。L1 磁盘 / 上界、L3 PostgreSQL / collector 联验、Flutter 来源及其他已登记业务来源最终出口仍各自保留。本节批准范围内的实现和本地验收已连续完成；新增业务行为修复或实际运行验证仍另列范围。


### 8.4 直接消费者的实际补漏

Client 写入失败接入真实 `ApiExceptionHandler.TryHandleAsync` 的回归发现：该边界原本仅在生成端安全，旧链仍继承任意 scope。为完成本批已批准的最终出口与异常所有权核验，补充其固定 `EventId(1, ApiRequestFailed)`，并在 `ApplicationOperationLogSink` 按精确来源重建既有 `http.failed`。只保留 500–599 的 int 状态和封闭 failureKind，未知身份使用 `http.handler.unknown`；响应写入顺序、处理返回值和日志故障传播均保持。此修正覆盖该共享消费边界，不据此宣称其他旧业务来源已完成最终输出治理。

## 9. 下一批：API Controller 最终输出收口

2026-11-05，准备基线为 `12f739f9`。状态：**已获批准并完成本地实现与定向验证**。承接第 8 节，按一个批次完成下面 14 个 Controller 的已登记输出，不逐文件申请，不重做已完成的 User / Client / ApiExceptionHandler。结果见[实施记录](../records/unified-logging-l2-controller-output-2026-11-05.md)，现行规则见[契约第 77 节](./unified-logging-contract.md#77-api-controller-最终输出边界2026-11-05)。

### 9.1 已确认缺口与有限目录

源码核对发现，以下来源虽已移除异常原文，但大多数 Serilog 调用只有 EventCode，没有固定 SourceContext；当前精确 sink 不接管它们。Attachment 使用 ILogger，但尚无本来源的最终适配。不能按 `http.failed` 等通用 EventCode 全局拦截，否则会把其他服务或 scope 伪造事件当成已治理 Controller。

真实 Reaction 错误结果构造方法的临时内存探针已确认：原 503 响应保持，旧链最终事件仍含外部 scope 的 Authorization，候选链则经公共策略剔除该字段。这是待修缺口复现，不是验收通过；方法与限制见[准备记录](../records/unified-logging-l2-controller-output-readiness-2026-11-05.md)。

| 精确 Controller 来源 | 本批已有事件目录 | 除固定身份外允许的字段 |
| --- | --- | --- |
| `CoinController` | `http.failed`、`coin.balance_query_rejected / transaction_query_rejected / adjustment_rejected` | failureKind；http.failed 另允许 statusCode |
| `PaymentPasswordController` | `http.failed` | failureKind、statusCode |
| `AttachmentController` | `http.failed`、`upload.cleanup.failed` | failureKind；分别另允许 statusCode 或 cleanupOperation |
| `ShopController` | `http.failed`、`order.cancellation_rejected / fulfillment_retry_rejected / remark_rejected`、`benefit.activation_rejected / deactivation_rejected / revocation_rejected`、`product.management_rejected`、`inventory.use_failed / rename_failed` | failureKind；http.failed 另允许 statusCode |
| `ReactionController` | `http.failed` | failureKind、statusCode |
| `PostQuickReplyController` | `http.failed`、`quick_reply.query_rejected / notification_enqueue_failed` | failureKind；http.failed 另允许 statusCode |
| `PollController` | `http.failed`、`poll.request_failed` | 同上 |
| `LotteryController` | `http.failed`、`lottery.request_failed` | 同上 |
| `QuestionController` | `http.failed`、`question.request_failed` | 同上 |
| `PostController` | `post.edit_failed / top_failed` | failureKind |
| `CommentController` | `comment.create_failed / like_failed / edit_failed` | failureKind |
| `TagController` | `tag.request_failed` | failureKind |
| `CategoryController` | `category.request_failed` | failureKind |
| `StickerController` | `sticker.request_failed` | failureKind |

目录中的斜杠表示相同前缀下的独立事件；实现按完整字符串登记。Shop 的私有 helper 虽接收 eventCode 参数，调用点仍是表内有限常量，不开放任意事件码。

### 9.2 实施方式与行为边界

1. Serilog 生成点补本 Controller 的固定 SourceContext；Attachment 的两个 ILogger 入口使用固定命名 EventId。所有入口继续只传固定 failureKind、受控状态或操作枚举，不新增业务标识、消息、异常对象或详情字段。
2. 扩展既有 `ApplicationOperationLogSink` 的精确来源目录及按事件字段规则，复用三条输出接线，不新增一层通用日志框架。原 User / Client / Bootstrap / Dedup / RateLimit / ApiExceptionHandler 的规则保持；Controller 目录与字段若需拆文件，仅按真实职责拆分。
3. `statusCode` 仅保留 int 500–599，`cleanupOperation` 仅为 quota-complete / quota-release，failureKind 复用既有封闭枚举；其他字段不继承。http.failed 固定 http 分类，其余按现有语义固定为 http 或 application；旧 `business` 非法分类归为 application，不扩展共享类别集合。
4. 每个精确来源仅接受本目录事件，未知 / 缺失 / 跨来源身份生成该来源固定 unknown；Warning 以上保留安全摘要，低等级只在既有显式开发诊断门禁下输出。保留原 Timestamp / 等级和强类型 trace/span，剔除异常、任意 scope、自由字符串关联键；邻接类型与其他来源不匹配。
5. 保留原 catch 类型、响应字段、Message / 本地化求值顺序和异常传播；不挪动已由契约第 57 节治理的结果构造顺序，不增加全请求去重、吞错、重试、回滚或持久化。Service 原事件与 Controller 消费事件的不同语义继续保留，不把同一事件码视为同一所有者。
6. 保留 WriteTo 消费输出失败、AuditTo 可传播与候选应急通道语义；日志失败前已发生的业务副作用不撤销。生产候选开关、接口、权限、依赖和数据结构不变。

### 9.3 验证与完成标准

- 新增真实 Controller / 已有消费方法到正式 ConfigureLegacy / RuntimeLoggingConfiguration 的最终输出回归。覆盖旧 / 候选 Development / Production、配置 WriteTo / AuditTo 和代表性代码 console；注入 Authorization、任意嵌套对象、伪造 SourceContext / EventCode / Diagnostic、跨事件安全字段及非法数值，断言最终事件 / JSON 无污染。
- 覆盖每个登记事件、未知 / 错误身份、邻接来源、诊断门禁、强类型关联和输出失败。明确哪些消费方法用反射定向调用，哪些完整 action 已通过现有服务 / repository 替身覆盖；不把前者冒充 HTTP 端到端。
- 复用 CoinMovement、PaymentPassword、AttachmentUpload、ShopFulfillment / Management / Entitlement、Reaction / QuickReply / Poll / Lottery / Question、Post / Comment / Tag / Category / Sticker 等日志与业务回归。继续覆盖结果构造二次失败、专用拒绝安静性、消息只读取一次、原状态与 mutation 计数；Service 层已知最终输出缺口不借本批顺手扩张。
- 对共享 sink 的既有六来源、HTTP 异常消费者及 L3 本地接收追加定向防回归；构建、共享 Node / .NET 日志契约、身份 / 权限、文档 / 链接 / 卫生及 diff 检查按影响面执行。
- 完成后同步事件契约和本批记录，将这 14 个 Controller 从最终出口待核验队列移除；不以候选统一裁剪或历史测试通过代替旧链精确来源结果。

本批只使用已安装依赖、内存管道、已有测试替身及相关测试自管临时文件 / SQLite；不启动项目服务 / 容器、不连接真实存储、不执行实际业务迁移、不访问生产。真实 File / DB sink 故障和 collector 联验仍单独授权。新业务行为问题或需要扩大 Service / 框架来源时先报告，不将其隐藏在日志收口中。

### 9.4 命令输出例外与后续顺位

- 本节原核对确认 DbMigrate 人读报告与运行诊断分流，`ConsoleHelper` 在仓库内没有调用。第 10.14 节已限制报告标签并移除该 helper，见[现行边界](#l2-diagnostic-and-output-boundaries)。
- 后续的 Service / Job / Hub 按第 10 节完成已登记本地切片，真实采集及持久化按[第 2.1 节](#l1-l3-first-runtime-verification)接续。

<a id="l2-final-output-first-slice"></a>

## 10. L2 最终输出核验与首组修复

2026-11-10 在 `dev` 的 `546b0968` 复现。商城订单取消探针经真实 Job 方法和日志配置到达旧链配置 WriteTo / AuditTo 和候选 JSONL。结果见[核验记录](../records/planning-closeout-2026-11-10.md#l2-job-output-audit)。

复现时，`BusinessJobSummary` 在全局 logger 上绑定事件码和计数，缺少固定的 `SourceContext`。原精确来源适配未重建这条输出，候选公共白名单仍会接受合法但不属于该 Job 事件的上下文。确认范围后完成本地修复，输出契约见[事件契约第 78 节](./unified-logging-contract.md#l2-business-job-final-output)。

### 10.1 已登记调用链

以下清单按[事件契约](./unified-logging-contract.md)的精确来源分组。旧链在 `SerilogSetup.ConfigureLegacy` 合并上下文后分流，候选链经 `RuntimeLoggingConfiguration` 和 `RuntimeSerilogSink` 生成 JSONL。已有生成端及直接消费者证据按原范围引用。共享后台 Job、抽奖 Job、文件和记录清理、服务内清理、Outbox、奖励 / 币 / 经验以及商城 / 库存 / 支付口令已完成本地最终输出收口，其余按组补证。

| 分组 | 实际入口与生成点 | 消费边界和证据 |
| --- | --- | --- |
| 首组业务 Job | `ShopJob.CancelTimeoutOrdersAsync / MarkExpiredBenefitsAsync / GenerateDailyStatsAsync`、`CommentHighlightJob.ExecuteAsync`、`RetentionRewardJob.ExecuteAsync` 均由 `ApiRecurringJobRegistration` 登记，共用 `BusinessJobSummary.Write` | 契约 §11、78 和 `BusinessJobLoggingTests`。商城取消通过真实配置验证最终出口，另外四个入口沿原方法回归 |
| 抽奖 Job | `PostLotteryJob.ExecuteAutoDrawAsync` 用自己的 `ILogger<PostLotteryJob>` scope 写摘要，扫描异常交给 Hangfire | 契约 §11、79、`BusinessJobLoggingTests` 和 `LotteryControllerLoggingTests`。[第 10.4 节](#l2-lottery-job-output)已完成本地切片 |
| 文件及记录清理 | `FileCleanupJob` 的软删除、临时文件、回收站、孤立附件入口用内部批次摘要。通知收件箱、Wiki 草稿、聊天回应三个 Job 各在 `ExecuteAsync` 用类型化 logger | 契约 §10、80、`CleanupLoggingTests` 和 `FileCleanupJobTest`。[第 10.5 节](#l2-cleanup-job-output)已完成本地切片 |
| 上传及令牌清理 | `ChunkedUploadService.CleanupExpiredSessionsAsync`、`FileAccessTokenService.CleanupExpiredTokensAsync` 用 `ServiceCleanupSummary`。前台目录 / 配额失败走同 helper 的 `RecordStandaloneFailure` | 契约 §13、21–24、81。[第 10.6 节](#l2-service-cleanup-output)已完成本地切片 |
| Outbox | `ReliableOutboxDispatcherJob.DispatchAsync` 输出分派摘要。`ReliableOutboxExecutionJob.ExecuteAsync` 消费任务，`ReliableOutboxRepository` 的条件更新确实影响行后输出重试 / 死信 | 契约 §9、82 和 `OutboxNativeLoggingTests`。main / chat 的状态写入、权威 `operationId` 和旧 / 候选最终出口已完成本地回归，见[第 10.7 节](#l2-outbox-output)。Hangfire provider / 状态观察器沿用 §73 证据 |
| 奖励、币与经验 | `CoinService` 批量及并发重试、`ExperienceService` 发放 / 幂等 / 初始化和等级配置缓存使用 `RewardRuntimeLog`。调账、转账幂等、奖励存在检查和经验治理各有登记生成点 | 契约 §12、14–15、17–18 及奖励 / 币 / 经验日志回归。2026-11-11 已核验六个生成来源，复现来源误归、scope 污染和生产过滤缺口；已按[第 10.8 节](#l2-reward-output)完成本地修复，输出契约见 §83 |
| 商城、库存与口令 | `OrderService.PurchaseAsync` 扣币及履约分支，及已登记的 `ProductService`、`UserInventoryService`、权益和 `PaymentPasswordService` 生成点 | 契约 §14、16、19–20、25 和对应业务回归。四个生成来源已按[第 10.9 节](#l2-shop-output)完成本地最终输出修复，契约见 §84 |
| 公开查询与报表 | `PublicHeadSnapshotService`、`PublicSitemapService` 的缓存及生成分支，Gateway 快照客户端 / 注入中间件，以及公开发现流、排行榜和统计查询链 | 契约 §26–29、85。前四个来源的 11 个事件已按[第 10.10 节](#l2-public-query-output)完成本地最终输出修复；发现流、排行榜和统计沿用 API 最终异常消费回归 |
| 通知与实时推送 | `UserFollowService` 通知入队，`NotificationService` 与可靠任务的收件箱变更到 `NotificationPushService.PushInboxChangedAsync`，以及 `UserInteractionRealtimeNotifier.NotifyRelationshipChangedAsync`、`CommentRealtimePushService.PushAsync` | 契约 §30–31、33、35、38、86。四个生成来源的五个事件已按[第 10.11 节](#l2-notification-realtime-output)完成本地最终输出修复。轻回应沿已有事务与 Controller 证据 |
| Hub 生命周期 | `ChatHub.OnDisconnectedAsync` 和 `NotificationHub.OnDisconnectedAsync` 在既有清理后用类型化 logger 写摘要。`CommentHub` 进组、退组和输入中路径已有安静性回归 | 契约 §32、34、36、87。两条自有断开摘要已按[第 10.12 节](#l2-hub-lifecycle-output)完成本地最终输出修复 |
| 内容及表情 | `CommentService` 编辑与高亮、`ContentSubmissionService` 冲突恢复、`ForumContentRevisionService` 恢复失败、`ReactionService` 重试、`StickerService` 缓存、批量附件与缩略图降级 | 契约 §37、39–40、42、48–49、88。五个来源、13 个事件按[第 10.13 节](#l2-content-sticker-output)完成本地最终输出，已收口 Controller 做关联回归 |

浏览器、七个已收口的自有来源及其直接消费者、14 个 Controller、已登记的 Hangfire provider / 状态观察器，沿用事件契约 §73–77 的本地结论。Flutter 应用引导、DbMigrate 命令报告和应急输出见[第 10.14 节](#l2-diagnostic-and-output-boundaries)。

### 10.2 首版诊断字段建议

| 字段 | 首版取舍与生成边界 |
| --- | --- |
| 事件身份、等级、服务与安全计数 | 首版必需。Job 事件码、`jobKind`、`outcome`、计数与 `durationMs` 来自已登记的摘要生成点，实际消费的异常经 `RuntimeFailureSummary.Classify` 生成封闭 `failureKind` |
| `traceId / spanId / operationId` | 首版按已有契约有值时保留。trace / span 使用已绑定的 W3C 类型，operation ID 使用已登记的权威生成点，例如 Hangfire 状态观察器与 Outbox 状态写入。首组 Job 摘要不从普通 scope 继承关联值 |
| `requestId` | 首版 API 5xx 用户反馈字段。已在最终输出绑定服务端 `HttpContext.TraceIdentifier`，与 W3C `traceId` 独立。权威来源、接收兼容及查询交付边界见[第 10.14 节](#l2-diagnostic-and-output-boundaries) |
| `jobId` | 后置原始标识。任务失败关联已有 Hangfire / Outbox 各自派生的 `operationId`。若需关联批次摘要与调度记录，先验证排障场景及跨重试含义 |
| `tenantId` | 后置。首版已规划的查询依赖部署、服务与关联字段。租户级排障或权限另行确认，并从服务端解析且校验的租户上下文生成 |
| 安全异常类型及栈帧 | 后置。首版用 `failureKind` 和精确来源定位。若实际排障仍有缺口，另登记固定异常类别与受控帧标识，制定大小预算和允许来源 |

### 10.3 首组修复与本地验收

1. `BusinessJobSummary` 绑定固定生成身份。`BusinessJobLogSink` 在旧链代码 / 配置 WriteTo、配置 AuditTo 和候选链合并上下文后，按精确身份及三个已登记事件重建摘要。字段白名单剔除普通 scope 和无关字段，保留原时间、等级和类型化 trace / span。已登记的摘要固定为普通事件。同名事件继续按各自来源处理。
2. 本地修改限于共享 helper 与输出适配。保留五个入口的计数口径、锁释放、Job 返回、业务写入 / 事务和 Hangfire 重试，以及 WriteTo 消费输出故障、AuditTo 传播故障、候选有限应急的行为。
3. 正式回归覆盖旧 / 候选与 Development / Production、代码 console 与配置 WriteTo / AuditTo、固定身份、同名异来源、scope 污染、空批次、部分失败、计数、输出故障和锁释放。真实任务、介质和 collector 联验在获批的目标环境另行执行。见[实施记录](../records/planning-closeout-2026-11-10.md#l2-job-output-implementation)。

<a id="l2-lottery-job-output"></a>

### 10.4 抽奖 Job 最终输出与本地验收

`PostLotteryJob` 已有独立的类型化来源。修复前，`BusinessJobLogSink` 只处理 `BusinessJobSummary`，抽奖摘要会继承外层 scope。候选 JSONL 会输出合法但不属于抽奖摘要的字段，且 Production 下外层 `Diagnostic=true` 会使失败摘要整条消失。见[本地核验记录](../records/planning-closeout-2026-11-10.md#l2-lottery-job-output-audit)。

1. 复用 `BusinessJobLogSink` 和既有日志接线，为精确来源 `Radish.Service.Jobs.PostLotteryJob` 定义独立的事件和字段白名单。只登记 `job.batch.completed`（Information、`succeeded`）与 `job.batch.failed`（Error、`partial` / `failed`）。输出采用固定模板、`sourceCategory=job`、`jobKind=post-lottery`、本批次的 `processedCount` / `failedCount` / `durationMs`，失败事件另保留封闭 `failureKind`。两条均为普通事件，保留原时间及类型化 trace / span，排除 scope 的 `operationId`、普通上下文和原异常。该来源的未登记事件按安全通用事件处理，同名异来源保持各自归属。
2. 保留现有扫描条件、顺序、批量边界、帖子去重、返回值和单项失败后继续处理。开奖服务的事务、审计、Outbox 和 Hangfire 的扫描异常归属保持原行为。配置 WriteTo 消费 sink 故障，AuditTo 保留异常传播，候选输出故障沿用有限应急。
3. 定向回归覆盖两种环境的配置 WriteTo / AuditTo 与候选 JSONL、旧链代码 console、scope 污染和同名异来源。验证完成、部分失败、全部失败、空批次、扫描异常、批量边界与去重，并复跑抽奖业务回归。见[实施记录](../records/planning-closeout-2026-11-10.md#l2-lottery-job-output-implementation)。真实调度、生产日志介质和 collector 留到对应环境验收。

<a id="l2-cleanup-job-output"></a>

### 10.5 文件及记录清理 Job 最终输出与本地验收

修复前，文件摘要未绑定 `SourceContext`，外层已有业务 Job 来源时会被其他适配器重建为 `runtime.unclassified`，丢失原事件和计数。三个记录清理 Job 的类型化来源稳定，但同样会带出外层字段。生产候选链会因外层 `Diagnostic=true` 丢掉文件失败、记录完成和收件箱容量告警。见[核验记录](../records/planning-closeout-2026-11-10.md#l2-cleanup-job-output-audit)。

1. 文件摘要固定 `Radish.Service.Jobs.FileCleanupJob` 来源。`CleanupJobLogSink` 在旧链 WriteTo / AuditTo 和候选输出中按下表重建登记事件，固定 `SourceCategory=job` 和 `Diagnostic=false`，保留类型化 trace / span 及 Serilog 事件的原等级和时间戳。精确来源的未登记事件用安全通用摘要处理，同名事件继续按各自来源处理。
2. 保留文件移动 / 删除和原计数口径、分片排除、引用保护、空批次安静及已消费失败的返回行为。保留记录仓储调用、结果对象和异常外抛，由 Hangfire 按原策略重试。WriteTo、AuditTo 和候选链各自沿用现有输出故障行为。上传会话、文件访问令牌和 Hangfire 自有日志继续按原分组处理。
3. 定向回归覆盖七个入口、两种环境的配置 WriteTo / AuditTo、代码 console 和候选 JSONL。已验证来源错配、无关字段、生产过滤、收件箱两事件的字段隔离、空批次和异常归属。目录失败警告用适配器输入验证，真实目录权限故障、生产日志介质、Hangfire 调度及 collector 留到对应环境验收。见[实施记录](../records/planning-closeout-2026-11-10.md#l2-cleanup-job-output-implementation)。

| 来源 | 登记事件与字段 |
| --- | --- |
| `FileCleanupJob` | 保留完成、警告、失败。`jobKind` 限于 `file-deleted / file-temp / file-recycle / file-orphan`，保留对应 `outcome`、`durationMs` 和原七项计数 `processedCount / movedCount / missingCount / skippedCount / removedDirectoryCount / failedCount / directoryFailureCount`。失败或含真实目录清理异常的警告保留封闭 `failureKind`，仅缺失文件的警告排除该字段 |
| `NotificationInboxCleanupJob` | 固定 `jobKind=notification-inbox`。完成只保留 `relationCount / groupCount / notificationCount / durationMs`。`job.cleanup.capacity_warning` 只保留聚合 `count`，对应本批容量告警条数 |
| `WikiDraftPayloadCleanupJob` / `ChatMessageReactionOperationCleanupJob` | 各自固定 `jobKind=wiki-drafts / chat-reactions`，完成只保留 `count / durationMs` |

<a id="l2-service-cleanup-output"></a>

### 10.6 上传会话及文件访问令牌清理最终输出与本地验收

修复前，后台两条任务和前台清理失败的 `ServiceCleanupSummary` 缺少固定 `SourceContext`。外层来源为 `FileCleanupJob` 时，服务的完成事件被误按文件摘要处理，丢失更新计数。中断和前台失败可被归为 `runtime.unclassified`，其中中断在生产候选链消失。外层 `Diagnostic=true` 也会使原本正常的完成、中断和失败事件消失。附件 Controller 的同名 `upload.cleanup.failed` 已独立适配。见[核验记录](../records/planning-closeout-2026-11-10.md#l2-service-cleanup-output-audit)。

1. 批次 `Dispose` 和 `RecordStandaloneFailure` 均使用固定 `Radish.Service.Internal.ServiceCleanupSummary` 来源。`ServiceCleanupLogSink` 在旧链代码及配置 WriteTo、配置 AuditTo 和候选 JSONL 的上下文合并后，按下表重建这一精确来源。登记事件固定 `Diagnostic=false`，保留类型化 trace / span 及 Serilog 事件的原等级和时间戳。未登记的组合使用安全通用摘要。
2. 保留原计数口径、空批次安静、跨租户条件更新、分片目录宽限和配额结算去重。中断只报告此前进度，仓储异常由 Hangfire 处理。输出故障沿用既有语义，配置 AuditTo 写入失败仍可覆盖中断路径正在传播的仓储异常。附件 Controller 的同名事件、分片状态回写的两个 `upload.session.*` 事件沿各自生成和适配边界处理。
3. 回归覆盖两环境的旧链配置 WriteTo / AuditTo、代码 console 和候选 JSONL。已验证成功、已消费失败、先有进度后中断、空批次、前台三个操作、附件 Controller 同名事件、上下文覆盖和输出故障。真实权限故障、Hangfire 调度、生产日志介质和 collector 留到对应环境验收。见[实施记录](../records/planning-closeout-2026-11-10.md#l2-service-cleanup-output-implementation)。

| 生成路径 | 登记事件与字段 |
| --- | --- |
| `upload-sessions` 批次 | 完成、中断为 Info，已消费失败为 Error。`SourceCategory=job`，保留该 `jobKind`、对应 `outcome`、`durationMs` 和原六项计数 `processedCount / updatedCount / removedDirectoryCount / settlementCount / skippedCount / failedCount`。只有失败事件保留封闭 `failureKind` |
| `file-tokens` 批次 | 只登记完成和中断，使用同一计数结构和 `SourceCategory=job`，保持真实撤销、已处理及并发跳过的区别。中断为 Info，剔除外层 `failureKind` |
| 前台 `RecordStandaloneFailure` | 只登记 `upload.cleanup.failed` Error，固定 `SourceCategory=application`。仅保留封闭的 `cleanupOperation`（`directory / quota-release / quota-complete`）和 `failureKind` |

<a id="l2-outbox-output"></a>

### 10.7 Outbox 最终输出与本地验收

修复前，分派的类型化来源稳定，但会继承外层关联值、分类和无关字段。状态摘要由 Repository 绑定权威 `operationId`，缺少固定来源时可能被其他适配器重建为 `runtime.unclassified`，连同状态字段一起丢失。生产候选链在外层 `Diagnostic=true` 时会丢掉三类事件。直接调用 Repository 时也能继承非本次失败的 `failureKind`。见[核验记录](../records/planning-closeout-2026-11-10.md#l2-outbox-output-audit)。

1. 保留 `Radish.Api.Services.ReliableOutboxDispatcherJob` 类型化来源，为状态摘要固定 `Radish.Repository.ReliableOutboxRepository` 来源。在旧链代码及配置 WriteTo、配置 AuditTo 和候选 JSONL 合并上下文后，按下表重建已登记事件。固定 `SourceCategory=job` 和 `Diagnostic=false`，保留类型化 trace / span 及 Serilog 事件的原等级与时间戳。未登记的组合使用安全通用摘要。
2. `ReliableOutboxService` 使用现有分类器，经仓储接口把可选的 `failureKind` 显式交给 Repository。Repository 在事件中绑定这一值，包括缺省的空值，最终适配只输出合法分类。保留仓储直接调用路径，并保留 `operationId` 的原算法和库域。状态关联只取 Repository 绑定的规范 GUID。
3. 保留分派入队计数、条件更新影响行后才生成状态事件、零行和重复执行安静、既有审计字段、重试和死信规则。回归覆盖 main / chat SQLite、两种环境的最终出口、来源错配、生产过滤、事件字段、入队和输出故障。WriteTo、AuditTo 和候选沿用现有故障语义。租约归属、实际调度、PostgreSQL 并发和生产采集链路依各自边界处理。

| 精确来源 | 登记事件与字段 |
| --- | --- |
| `ReliableOutboxDispatcherJob` | `outbox.dispatched` Info，仅保留 `count / durationMs`。分派是入队数，和执行成功数分别统计 |
| `ReliableOutboxRepository` | `outbox.retrying` Warning 和 `outbox.dead_letter` Error，仅保留 `databaseScope / attempt / outcome / failureKind` 及由本次状态写入方生成的 `operationId` |

2026-11-11 所有者确认上述范围，已由 `OutboxLogSink` 和显式分类参数完成本地实现。旧链代码 console、两环境的配置 WriteTo / AuditTo 与候选 JSONL，以及 main / chat SQLite 的状态转换、零行更新和输出故障回归通过；结果及未执行边界见[实施记录](../records/unified-logging-l2-outbox-output-2026-11-11.md)。奖励、币与经验的核验和本地实现见第 10.8 节，不重做已完成切片。

<a id="l2-reward-output"></a>

### 10.8 奖励、币与经验最终输出与本地验收

2026-11-11 基于 `38d15bd3`，六个生成来源的实际方法经旧链 WriteTo / AuditTo、代码 console 和候选 JSONL 核验。普通上下文可污染字段；外层来源为 `ServiceCleanupSummary` 时事件被重建为该来源的 unknown；外层 `Diagnostic=true` 可使生产候选链丢掉事件。修复前证据见[核验记录](../records/unified-logging-l2-reward-output-audit-2026-11-11.md)。2026-11-11 所有者批准以下范围，已由固定生成来源和 `RewardLogSink` 完成本地实现；输出契约见[第 83 节](./unified-logging-contract.md#l2-reward-final-output)。

1. 在下表六个生成点绑定各自固定 `SourceContext`，复用现有日志接线，在合并上下文后的最终输出按精确来源、事件码、等级及业务枚举重建。登记事件固定 `SourceCategory=application`、`Diagnostic=false`，仅保留本事件所需字段与类型化 trace / span；Serilog 重建保留原时间和等级，JSONL 继续沿公共时间策略。不继承普通 scope 的 `operationId`、自由文本、无关计数或失败分类。
2. 只给实际消费异常的事件保留封闭 `failureKind`；重试、完成摘要、幂等记录缺失和排行榜缺失用户不继承外层失败。未知 / 缺失 / 跨来源身份、错误等级与未登记组合使用安全 unknown，低等级依原诊断门禁处理；邻接来源不匹配。保持原生成点的结果、计数和异常所有权，不用最终适配重新推断业务成功。
3. 保留币三次及经验六次重试、延迟与抖动、发放 / 幂等 / 调账 / 转账、审计、事务和 Outbox 语义。经验单项已消费失败仍只在单项记录，批次 completed 可表示拒绝或部分结果；资金写入后幂等完成重试成功仅表示调用返回。查询失败返回 true / 0、坏快照空列表、分页和缓存回退均沿原边界，不借日志修改业务契约。
4. 保留 WriteTo 消费输出失败、AuditTo 传播故障、候选有限应急。已观察到 AuditTo 的 sink 故障可使经验单项不再返回 false、缓存读取失败后不再执行数据库回退，以及批次完成后向调用方抛错；这些是本次核验到的既有语义，本批保持该语义。

| 固定来源 | 登记事件与允许字段 |
| --- | --- |
| `Radish.Service.RewardRuntimeLog` | `reward.retrying` Warning：`rewardDomain=coin / experience`、attempt、delayMs；`reward.failed` Error：固定 experience、`rewardOperation=grant / grant-once / initialize`、failureKind；`reward.conflict` Warning：固定 experience / grant-once、failureKind；`reward.cache_fallback` Warning：固定 experience、`cache-read / cache-write / cache-invalidate`、failureKind；`reward.batch_completed` Info / `reward.batch_failed` Error：coin / experience、processedCount / rejectedCount / failedCount、与原结果一致的 outcome，仅失败摘要保留 failureKind |
| `Radish.Service.CoinService` | `coin.transfer_completion_recovered` Warning：仅 failureKind |
| `Radish.Service.OperationIdempotencyService` | `idempotency.completion_missing` Warning：仅 `completionKind=success / failure` |
| `Radish.Service.CoinRewardService` | `reward.existence_check_failed` Error：仅 failureKind |
| `Radish.Service.ExperienceService` | `experience.rank_query_failed` Error 和 `experience.governance_snapshot_invalid` Warning：仅 failureKind；`experience.leaderboard_incomplete` Warning：仅 skippedCount |
| `Radish.Extension.ExperienceExtension.ExperienceCalculator` | `experience.calculator_cache_fallback` Warning：`rewardOperation=cache-read / cache-write / cache-invalidate`、failureKind |

本地回归覆盖六个精确来源、13 个事件码、各合法领域 / 操作组合和非法组合；两环境的配置 WriteTo / AuditTo、代码 console 与候选 JSONL，scope 污染、真实方法返回 / 写入调用、批次计数、重试耗尽及恢复、正常安静、同名异来源、未知事件、类型化关联和输出故障。复跑奖励、币流转、账户治理、奖励入口、经验查询治理及现有 Outbox / Controller 关联回归。事务 / 仓储 / 缓存替身不替代真实数据库、Redis、调度或 collector 验收；不扩入商城、库存、支付口令及其他下一分组。

实现与验证结果见[本批实施记录](../records/unified-logging-l2-reward-output-audit-2026-11-11.md#implementation)。本组本地切片完成不关闭 L2 整体或真实链路门禁。

<a id="l2-shop-output"></a>

### 10.9 商城、库存与支付口令最终输出与本地验收

2026-11-11 基于 `4200e6d9` 核验四个生成来源。商城 / 库存三个 Serilog 来源缺少固定身份，外层来源可使其误归为 unknown；支付口令已有类型化来源。修复前四个来源都存在最终字段污染和生产诊断过滤缺口，见[核验记录](../records/unified-logging-l2-shop-output-audit-2026-11-11.md)。2026-11-11 所有者确认以下范围，已由固定生成身份和 `ShopLogSink` 完成本地实现；输出契约见[第 84 节](./unified-logging-contract.md#l2-shop-final-output)。

1. `OrderService`、`ProductService`、`UserInventoryService` 在现有日志生成点绑定各自固定 SourceContext。`PaymentPasswordService` 保持类型化 logger，将两个局部事件 scope 改为命名 EventId：`1/PaymentLocksCleared`、`2/PaymentSuggestionsFailed`，最终映射下表事件；不让普通 scope 的 EventCode 决定支付口令事件身份。
2. 新增本组 `ShopLogSink`，复用旧链 WriteTo / AuditTo 与候选链已有接线，在上下文合并后按四个精确来源、事件身份、等级及合法 purchaseStage 重建。登记事件固定 `SourceCategory=application`、`Diagnostic=false`，采用固定模板，仅保留下表字段、类型化 trace / span 及 Serilog 原时间 / 等级；JSONL 时间沿公共策略。普通 operationId、字符串关联键、原异常对象、业务载荷和跨事件字段剔除。
3. 未知 / 缺失 / 跨来源身份、错误等级和非法购买阶段使用安全 `runtime.unclassified`；低等级沿既有诊断门禁，邻接来源不匹配。数值仅接收 int：attempt / delayMs 沿现有公共上界 1,000,000 / 60,000 且非负，processedCount 只保留正数；非法数值与非法 failureKind 丢弃。不按计数重新解释业务成功，不给无异常的配置拒绝、重试或清理数量继承 failureKind。
4. 保留库存五次尝试和退避、现有冲突识别、购买支付 / 履约阶段、库存回补与已售数、订单 FailReason / 幂等 / Outbox、历史权益快照、道具条件扣减与权威操作记录。保留改名 ArgumentException 安静、InvalidOperationException 包装 400、其他异常交直接消费者。口令算法、旧版本升级、五次失败锁定 30 分钟、审计与建议回退文字不变。
5. 保留 WriteTo 消费输出失败、AuditTo 传播及候选有限应急。已复现 AuditTo 可中断库存重试、在支付失败回补前抛错、阻止改名 400 包装及建议回退、在清理已经返回仓储数量后抛错；本批不借日志隔离改变这些既有语义。业务事务与响应改造需独立范围。

| 精确来源 | 登记事件、等级及允许字段 |
| --- | --- |
| `Radish.Service.ProductService` | `product.configuration_rejected` Warning，无扩展字段；`product.stock_retrying` Warning，仅 attempt / delayMs |
| `Radish.Service.OrderService` | `order.purchase_failed` Error，仅 `purchaseStage=payment / fulfillment` 与 failureKind；`order.purchase_interrupted` Error，仅 failureKind |
| `Radish.Service.UserInventoryService` | `inventory.rename_rejected` Warning，仅封闭 failureKind |
| `Radish.Service.PaymentPasswordService` | `1/PaymentLocksCleared → payment.locks_cleared` Information，仅 processedCount；`2/PaymentSuggestionsFailed → payment.suggestions_failed` Error，仅 failureKind |

本地回归覆盖四个来源、七个事件码及两种购买阶段；两环境的配置 WriteTo / AuditTo、代码 console 和候选 JSONL；scope / MEL scope 污染、固定身份、未知与错配事件、非法字段、类型化关联、正常安静、库存重试恢复 / 耗尽、支付 / 履约及独立补偿失败、改名包装与 Controller 消费、口令建议和清理结果、输出故障。复跑商城履约 / 管理 / 权益与口令日志、相关服务及 Controller / Outbox 关联回归。

`UserBenefitService` 无保留日志，不增加输出；已完成的 ShopController / PaymentPasswordController 适配只作关联防回归。真实库存 / 资金事务、PostgreSQL 并发、介质和 collector 仍需对应环境证据。接口、表结构、依赖、生产开关与下一组公开查询 / 报表不进入本批。

实现、验证结果与未执行边界见[本批实施记录](../records/unified-logging-l2-shop-output-audit-2026-11-11.md#implementation)。本地切片完成不关闭 L2 整体或真实链路门禁。

<a id="l2-public-query-output"></a>

### 10.10 公开查询与报表最终输出与本地验收

2026-11-12 基于 `edd47101` 核验四个来源、11 个事件。修复前，API 两个来源的六个事件可能被相邻来源适配器改写为 `runtime.unclassified`。四个来源均继承外层字段，`Diagnostic=true` 可使生产候选链丢掉全部登记事件。所有者确认以下范围，已完成本地实施，见[输出契约第 85 节](./unified-logging-contract.md#l2-public-query-final-output)及[本批记录](../records/unified-logging-l2-public-query-output-audit-2026-11-12.md#implementation)。

1. 为两个 API Service 的六个 Serilog 生成点绑定各自类型的 `SourceContext`，继续使用显式事件码。两个 Gateway 类型保留类型化 logger，为五个事件使用固定的命名 `EventId`，以数值和名称共同识别，去掉仅用于标记事件身份的 scope。
2. 在旧链代码 / 配置 WriteTo、AuditTo 和候选 JSONL 的既有适配链共用 `PublicMetadataLogSink`。按精确来源、事件身份和等级重建固定摘要，登记事件固定为非诊断。API 分类在两链统一为 `application`，旧链此前为 `business`。Gateway 使用 `http`。错配身份和等级沿现行策略输出受控 unknown，保留原等级及低级别诊断门禁。
3. 九个失败摘要只接收封闭 `failureKind`，两个不可用事件只接收符合其分支的整数 HTTP 状态。保留原 Serilog 事件时间和类型化 W3C trace / span（含当前 Activity）；自由 scope 中的关联字符串、`operationId`、额外字段、异常和原始载荷均不进入该摘要。

| 精确来源 | 事件与允许字段 |
| --- | --- |
| `PublicHeadSnapshotService` | `public_head.cache_read_failed`、`public_head.cache_write_failed` 均为 Warning，仅 `failureKind` |
| `PublicSitemapService` | `sitemap.cache_read_failed`、`sitemap.cache_write_failed`、`sitemap.section_count_failed` 均为 Warning，`sitemap.generation_failed` 为 Error，均仅 `failureKind` |
| `PublicHeadSnapshotClient` | `public_head.snapshot_unavailable`、`public_head.html_unavailable` 均为 Warning，仅 `statusCode`；`public_head.snapshot_request_failed`、`public_head.html_request_failed` 均为 Warning，仅 `failureKind` |
| `PublicHeadSnapshotMiddleware` | `public_head.injection_failed` Warning，仅 `failureKind` |

验证覆盖真实生成方法、正式出口、两种 scope、诊断开关、错配与非法字段、无 Activity 与类型化关联、代码 console 及输出故障语义。回归缓存和 TTL、sitemap 上次成功 / 空 XML / 部分索引、Gateway 404、JSON 异常原样传播、注入失败后的后续管道，以及 API 与 Gateway 各自的异常归属。公开发现流、排行榜、统计报表及已收口 API 异常处理器只作关联回归。缓存、HTTP 响应、接口、数据模型、依赖和生产开关沿用现有行为与配置，真实介质及采集仍归对应批次。

<a id="l2-notification-realtime-output"></a>

### 10.11 通知与实时推送最终输出与本地验收

2026-11-12 基于 `818ba299` 核验四个类型化来源、五个事件。修复前，已输出事件的身份及等级保持正确，旧链继承外层任意字段，候选 JSONL 继承无关白名单字段和关联值，外层 `Diagnostic=true` 可使生产候选丢掉所有五类事件。所有者确认以下范围，已完成本地实施。见[事件契约第 86 节](./unified-logging-contract.md#l2-notification-realtime-final-output)及[实施记录](../records/unified-logging-l2-notification-realtime-output-audit-2026-11-12.md#implementation)。

1. 沿用四个 `ILogger<T>` 类型来源。五个生成点改用固定的命名 `EventId`，去掉仅标记事件身份和分类的 scope。按精确类型、数值、名称和原等级共同识别，普通 EventCode 不补齐身份。
2. 在上下文合并后接入 `NotificationRealtimeLogSink`，覆盖旧链代码 / 配置 WriteTo、AuditTo 和候选 JSONL。固定摘要、`application` 分类和非诊断标记，旧链此前为 `business`。扩展字段仅允许封闭 `failureKind`。保留原 Serilog 时间及类型化 W3C trace / span，去掉自由 scope 中的关联值、`operationId`、额外属性、异常和原载荷。精确来源的错配身份或等级进入受控 unknown，沿原等级与低级别诊断门禁。

| 来源 | 命名 EventId 与事件 |
| --- | --- |
| `UserFollowService` | `1/FollowNotificationEnqueueFailed` → `user_follow.notification_enqueue_failed` Error |
| `NotificationPushService` | `1/NotificationPushFailed` → `notification.push_failed` Warning |
| `UserInteractionRealtimeNotifier` | `1/ChatRelationshipPushFailed` → `user_interaction.chat_push_failed` Warning，`2/NotificationRelationshipPushFailed` → `user_interaction.notification_push_failed` Warning |
| `CommentRealtimePushService` | `1/CommentPushFailed` → `comment.push_failed` Warning |

验证覆盖两环境的正式出口和代码 console、两种 scope、无 Activity / 类型化 Activity、错误身份或等级、非法字段和 sink 故障。保留关注关系与通知入队的既有提交边界、400 / 404 与上抛所有权、通知持久化及幂等、Outbox 成功和失败分工、双 Hub 和多接收者发送顺序、评论成功结果及后续高亮推送。WriteTo 消费输出故障，候选 writer 走有界应急，AuditTo 保留传播及其中断现有调用的行为。轻回应、已收口 Controller、Outbox 和 SignalR 框架路径仅做关联防回归。Hub 自有生命周期、业务事务调整及真实网络 / 介质 / collector 进入各自批次。

<a id="l2-hub-lifecycle-output"></a>

### 10.12 Hub 自有生命周期最终输出与本地验收

2026-11-12 基于 `e613bbb7` 核验两个类型化来源、两个 Warning 事件。修复前，已输出事件保留正确来源和事件码，正式旧链继承外层连接属性和载荷，候选 JSONL 继承无关白名单字段与关联值，外层 `Diagnostic=true` 可使生产候选丢掉两个事件。所有者确认以下范围，已完成本地实施。见[事件契约第 87 节](./unified-logging-contract.md#l2-hub-lifecycle-final-output)及[实施记录](../records/unified-logging-l2-hub-lifecycle-output-audit-2026-11-12.md#implementation)。

1. 沿用 `ILogger<ChatHub>` 和 `ILogger<NotificationHub>`。各自用 `1/ChatConnectionClosed` 和 `1/NotificationConnectionClosed` 标识 `chat.connection_closed` 和 `notification.connection_closed`，移除仅用于标记身份和分类的 scope。精确来源、数值、名称和 Warning 必须匹配，普通 EventCode 不补齐身份。
2. 在旧链代码 / 配置 WriteTo、AuditTo 和候选 JSONL 的上下文合并后，由 `NotificationRealtimeLogSink` 处理两个 Hub 自有事件。固定模板、`application` 分类和非诊断标记，旧链此前为 `business`。仅保留封闭 `failureKind`、Serilog 原时间 / 等级和类型化 W3C trace / span。剔除普通字符串关联、`operationId`、连接 / 用户 / 租户属性、额外字段、原模板、载荷和异常对象。精确来源的错误身份或等级输出受控 unknown，沿原等级与低级别诊断门禁。
3. 回归清理完成前安静、清理失败原样传播、正常断开安静及通知 Hub 非正用户 ID 跳过退组但仍记录异常的行为。保留 AuditTo 在清理后传播原 sink 故障、WriteTo 消费故障与候选 writer 有界应急的语义。覆盖正式三条路径和代码 console、两种 scope、类型化关联、错配和非法字段。CommentHub 的安静性及 SignalR 框架 `signalr.disconnect_failed` 只做关联回归，真实 WebSocket、日志介质及 collector 另行验收。

<a id="l2-content-sticker-output"></a>

### 10.13 内容及表情最终输出与本地验收

2026-11-12 基于 `c15eb0a0` 核验五个来源、13 个事件。三个 Serilog 服务未固定来源，既能被相邻适配器误归，也能使共享的 `comment.edit_failed` 归到 Controller。五个来源的正式旧链均继承外层载荷，候选 JSONL 可继承不相关字段和关联值，生产候选会因外层诊断标记漏记。所有者确认以下范围。见[核验与实施记录](../records/unified-logging-l2-content-sticker-output-audit-2026-11-12.md)及[事件契约第 88 节](./unified-logging-contract.md#l2-content-sticker-final-output)。

1. 为 `CommentService`、`ContentSubmissionService` 和 `ForumContentRevisionService` 的八处 Serilog 生成点绑定类型来源，沿用显式事件码。回应和表情沿用类型化 logger，六个生成点改为命名 EventId。两类来源均匹配精确类型与原等级，命名身份还须同时匹配数值和名称。
2. 在上下文合并后，以 `ContentStickerLogSink` 重建旧链代码 / 配置 WriteTo、AuditTo 和候选 JSONL。固定模板、`application` 分类和非诊断标记，事件只保留封闭 `failureKind`，保留原 Serilog 时间 / 等级及类型化 W3C trace / span。去除普通 scope 关联、`operationId`、额外字段、异常与原载荷。错配事件走安全 unknown 和既有低级别诊断门禁。

| 来源 | 事件身份与等级 |
| --- | --- |
| `CommentService` | `comment.edit_failed`、`comment.god_recheck_failed`、`comment.sofa_recheck_failed`、`comment.highlight_recheck_failed`、`comment.highlight_fill_failed`，均 Error |
| `ContentSubmissionService` | `content_submission.conflict_resolved` Warning |
| `ForumContentRevisionService` | 两个异常转换分支共用 `post.restore_failed` Error |
| `ReactionService` | `1/ReactionRetrying` → `reaction.retrying` Warning |
| `StickerService` | `1/StickerCacheReadFailed` → `sticker.cache_read_failed`、`2/StickerCacheWriteFailed` → `sticker.cache_write_failed`、`3/StickerCacheInvalidationFailed` → `sticker.cache_invalidation_failed`、`5/StickerThumbnailFailed` → `sticker.thumbnail_failed` 均为 Warning；`4/StickerBatchItemFailed` → `sticker.batch_item_failed` Error |

回归覆盖真实业务方法、正式出口、两种 scope 与 W3C Activity、共享事件码的服务归属、错误身份和等级、非法字段、代码 console 及输出故障。保留评论部分进度与既有事务结果、提交台账的 savepoint 恢复、版本恢复 409、回应单次重试与二次失败归属、表情批量结果、缩略图及缓存降级。WriteTo 消费故障，候选 writer 使用有界应急，AuditTo 保留传播和外层重算 catch 的既有行为。Controller 已收口事件只做关联回归。真实数据库并发、文件介质与 collector 按对应批次验收。

<a id="l2-diagnostic-and-output-boundaries"></a>

### 10.14 首版诊断字段与本地输出边界（本地已实施）

基于 `8d02535a` 核验三组边界，项目所有者已确认实施。[事件契约第 89 节](./unified-logging-contract.md#l2-diagnostic-output-boundaries)记录字段与通道规则，[本批记录](../records/unified-logging-l2-diagnostic-output-boundary-audit-2026-11-12.md#implementation)维护证据。首版字段按实际排障场景取舍。

| 排障场景 | 首版字段与来源 |
| --- | --- |
| 识别故障和采集缺口 | 事件身份与时间、受信部署 / 服务 / 实例 / 版本、来源类别、等级 / Fatal / diagnostic、规范化状态与固定摘要。保留登记事件的封闭 `failureKind`、状态、结果、计数及耗时；只在事件有对应业务含义时输出 |
| 串联服务请求 | 有权威值时保留类型化 W3C `traceId / spanId`，使用事件自带值或当前 W3C Activity。无关联时字段缺省；trace 只用于关联 |
| 由用户反馈定位 API 5xx | 增加可选 `requestId`，首批只由 `ApiExceptionHandler` 已成功写入响应的 `http.failed` 从服务端 `HttpContext.TraceIdentifier` 绑定。长度 1–64，字符限 ASCII 字母、数字及 `:`、`_`、`-`。原响应 ID 及 W3C 关联各自保持；入站 header 和普通 scope 不能补齐 |
| 串联任务重试与最终失败 | 只保留已登记的 Hangfire 状态选举及 Outbox 状态写入各自派生的 `operationId`，两者的处理决定和已落库状态继续按原事件解释 |
| 后置增强 | 原始 `jobId`、`tenantId`、异常类型与安全栈帧。首版按派生任务关联、平台运维范围、事件码与 `failureKind` 排障。新增租户视图、栈帧或原始任务标识须单独给出场景、生成权威与大小预算 |

1. **诊断输出**。在现有 API 最终异常适配中绑定并限定 `requestId`。通用 .NET Serilog fallback 不再从普通属性取字符串 trace / span，`operationId` 只从已登记的权威适配路径进入。覆盖有 / 无 Activity、原响应 ID、恶意 header / scope、缺省与超限。可选字段需让 .NET / Node 共享规范、Lua 与接收白名单兼容缺省样本；接收侧须在启用新输出前支持字段。L3 按独立迁移增加可空列及精确查找索引，L4 加受权的精确筛选。已有入库迁移不改写，生产候选开关沿现状。
2. **Flutter 本地出口**。在 Dart 引导入口接管 `FlutterError.onError` 的应用框架报告，输出固定安全摘要，沿用当前静默规则；保持 ErrorWidget 和业务状态路径。显式 bootstrap 失败使用无原异常内容的固定失败及空原始栈重新抛出，保持启动失败。普通业务故障继续由现有认证、会话及 HTTP 状态消费。通用 root isolate 的处理语义、原生引擎 / 插件和子 isolate 按目标平台另验，首版不新增远端遥测。
3. **CLI 与应急例外**。`help`、`doctor`、`verify` 及 `apply` 内嵌 doctor 的 stdout 是本地命令报告。对环境和连接标识等配置来源文本做单行控制字符转义及有界长度处理，保持报告判定、目标占位和退出码。移除仓库内无调用的 `ConsoleHelper` 裸写入口。DbMigrate 阶段 JSONL 及已登记有界应急事件沿 stderr。L1 / L3 联验用受信来源和流元数据让 CLI 报告在 parser 前退出日志链；独立 bootstrap 应急保留于有界的宿主 / 文件证据，集中接入需单独验证来源和故障通道，入库应急避免回到同一失败链。

本地证据包括 API 反馈 ID 与旧链 / 最终 JSONL 的对照、CLI 两条标准流与退出码、Flutter 合成异常正文 / 栈帧裁剪，以及 Lua 对命令报告、应急事件与可选字段的处理。真实采集负责验证受信流识别、应急留存与新字段实际投递，设备批次负责验证各目标平台输出。
