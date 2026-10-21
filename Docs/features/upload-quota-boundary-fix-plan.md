# 上传配额过期与整数边界修复方案

> 状态：**已获项目所有者批准，待实施**（2026-10-21）。[隔离验证结果](../records/nodrive-valkey-validation-2026-10-21.md)已随 `5f76913a` 提交并推送 `dev`。本方案只处理其中的两类上传限流问题；NoDrive、Valkey 正式替换及发布继续暂停。

## 1. 已确认事实与目标

| 问题 | 当前事实 | 修复目标 |
| --- | --- | --- |
| 过期预留仍结算 | Redis `CompleteScript` 解析了有效期但未检查；内存实现先移出目标再清理过期项。Redis / Valkey 均实测：目标过期、同 Hash 另有有效预留时仍增加 100 字节 | 两种实现统一以 `expiresAt <= now` 为过期，目标仅释放，不增加已用容量 |
| 超大整数统计丢失 | `2^53-1`、`2^53+1` 经 Lua `tostring(fileSize)` 写为科学计数法；后续仅接受数字的解析将其清除。相邻加法、INCRBY 入参和 Lua 返回值仍有精度风险 | 容量从输入、存储、比较、累加、结算到统计返回均保持精确，支持既有正数 `Int64` 服务输入 |

来源为 `Radish.Service/UploadRateLimitService.cs`、`Radish.IService/IUploadRateLimitService.cs` 和上述实测记录。内存过期问题目前为源码确认，批准后先增加失败回归；不把静态判断写成已执行测试。

普通上传的 `AcquireUploadAsync(file.Length)` 位于 AttachmentService 单文件大小检查之前；分片上传先由 `ValidateCreateRequest` 检查配置、总大小和分片数，再申请预留。因此服务自身必须保证整数正确，不能以文件大小上游校验代替；本轮也未证明超大 HTTP 实体可穿过传输层。

## 2. 已批准的业务与技术边界

1. **过期即释放**：缺失、已过期、已结算目标的 Complete 都保持幂等空操作；已发生的分钟尝试不回滚。仍有效的目标结算到预留时的业务日，保持现有跨日口径。
2. **保留容量范围**：`fileSize` 为 `1..long.MaxValue`；日容量为 `0..long.MaxValue`，0 保持拒绝新增正数预留的含义。并发 / 分钟配置非负，0 保持阻止对应新增请求；启用限流时负配置在服务构造时明确报配置错误，不截断、不转换为无限配额。禁用模式保留现有参数校验与不记账行为。
3. **有限的精确运算**：仅在上传限流内部实现至多 19 位非负十进制整数的校验、比较、加法和必要减法，不引入通用大数库或新包。新预留按剩余容量比较，避免先计算可能溢出的 `occupied + fileSize`。
4. **兼容正常存量**：保留 key、Hash / String 类型、四段 `size|rateKey|dailyKey|expiresAt` 格式、InstanceName、用户 hash tag 和现有 TTL 策略；不增加数据库表或迁移脚本。
5. **异常数据显式失败**：存在但不是合法整数的容量、负数、越界、错误 key 类型或不可解析的预留，不再按 0 或“自然过期”静默处理。获取配额、统计与结算报可识别内部错误，沿用现有异常 / 日志链；Fail 按 uploadId 释放、Reset 按现有用户范围清理仍可用。不存在的计数 key 仍为 0。

两项行为变化需明确接受：过期后完成不再补记日容量；异常缓存不再静默放行。普通上传已持久化后的配额异常仍按现有调用方策略记录并返回上传成功，分片仍使用已有重放窗口；本方案不承诺持久化账本或 exactly-once。

### 2.1 整数方案取舍

推荐保留 `Int64` 精确语义，而非降低服务上限。仅改格式化只能解决科学计数法外观，无法修复超过 Lua 数值精度后的比较、累加和返回；仅限制在 `2^53-1` 也不能修复该值已复现的科学计数法写入问题。

[Lua 5.1 手册](https://www.lua.org/manual/5.1/manual.html#2.2)说明默认 number 为双精度浮点数；[Redis Lua API](https://redis.io/docs/latest/develop/programmability/lua-api/#data-type-conversion)区分 number / integer reply 与 string / bulk reply。[INCRBY](https://redis.io/docs/latest/commands/incrby/)本身支持有符号 64 位整数，因此保持命令入参为精确字符串即可使用其整数能力，无需先转 Lua number。

## 3. 拟实施修改

### 3.1 过期判断与结算顺序

- 内存分支：在用户锁内取得同一个 `now`，清理过期预留后再获取目标；过期目标不进入累加。对有效目标先校验并计算日计数，再保存结算与剩余状态；保留缓存 IO 失败的既有一致性限制，不声称内存多次缓存写入具备事务回滚。
- Redis 分支：C# 预读只用于提供显式日 key；脚本必须重新读取目标并在写入前验证格式、日期 key 和有效期，不能以预读时有效作为结算依据。`expiresAt <= now` 时删除该目标、必要时清理空 Hash，返回未结算；不修改日计数和其他预留。
- 保留 TimeProvider 提供的 UTC 秒和 BusinessCalendar，不切换为 Redis 服务器时钟。`now` 表示本次操作采样时间；内存采样在锁内，Redis 时间随调用参数传入，不新增网络延迟补偿承诺。
- 有效目标结算前校验日计数类型、格式及相加上界，避免当前“先 HDEL，再因 INCRBY 错误失去预留”。Lua 隔离执行不等于异常时回滚：可预判的数据 / 溢出错误必须在所有写入之前返回；网络、OOM 等基础设施故障不以此宣称已解决。

### 3.2 容量全链路精确处理

- C# 将容量参数以 invariant 十进制字符串传给脚本；Lua 的 fileSize、maxDailySize、pending、completed、occupied 均作为经过验证的十进制字符串处理，禁止容量的 `tonumber`、`tostring(number)` 往返。
- Acquire 先读取并验证必要状态，在内存中分类有效 / 过期条目、精确累加当前业务日的预留并比较配额；校验结束后才执行过期清理与获准预留写入。重复 uploadId 不新增尝试，保持现有幂等语义。
- Complete 将已验证的原始 size 字符串交给 INCRBY；不使用它返回给 Lua 的 number 作为容量真值。脚本返回的容量字段为 bulk string，由 C# 严格解析回 `long`。状态码、并发数和分钟数保留受界限约束的整数。
- Statistics 精确返回已用、预留及占用；内存的累加、结算和 `UploadStatistics.OccupiedUploadSizeToday` 同步使用溢出检查。正常“配额不足”仍为现有 DailyUploadSize 拒绝 / 429；不把正常超配额包装为内部错误。
- 合法历史状态的占用可因下调配置而大于当前限额：统计照实返回，新增预留拒绝，原有效预留仍可完成。若状态自身总量超出 Int64 表达范围，则明确失败，不能饱和为最大值或绕回负数。
- 容量纯算法按真实复用放入服务内部脚本片段 / 专用文件；不扩展 Common 业务职责，不新建通用框架。测试通过真实生产脚本验证，不能复制一套算法充当通过证据。

### 3.3 存量异常与影响面

健康四段记录继续读写，无需全量清空或改 key 版本。旧科学计数法值已经丢失原始精度，不能推测还原为正确字节；本方案保留数据并报告错误，待自然 key TTL、按既有 Fail 释放或经单独确认的运维处理，不批量扫描 / 删除生产数据。正常过期条目仍按原规则清理。

同一 uploadId 继续按现有调用方生成的唯一标识使用，不支持重用 ID 后识别任意迟到完成；不新增 reservation generation token。混跑旧实现仍可能写入异常数据或按旧语义处理，未来发布需独立确认升级顺序；本轮无发布动作。

| 文件 / 区域 | 计划修改 |
| --- | --- |
| `Radish.Service/UploadRateLimitService.cs` 及必要的内部 Lua 文件 | 两分支有效期、精确整数、状态验证与写入顺序 |
| `Radish.IService/IUploadRateLimitService.cs` | 注释与 OccupiedUploadSizeToday 溢出检查；不改方法签名和响应字段类型 |
| `Radish.Common/OptionTool/UploadRateLimitOptions.cs` | 仅同步合法范围注释；局部校验在服务构造处，不改全局配置绑定机制 |
| `Radish.Api.Tests/Services/UploadRateLimitServiceTest.cs`、新增 Redis 集成测试与必要夹具 | 永久保留缺陷回归、两连接并发和边界测试 |
| `Docs/features/file-upload-design.md`、验证基线与本方案 | 批准后将已实现契约、显式集成测试入口和实测结果同步到真相源 |

AttachmentController、ChunkedUploadService 预计只增加相关回归，不修改现有上传成功 / 配额诊断、失败释放和重放策略。若实施发现必须更改这些业务接口或引入 ledger / outbox，应停止扩张并重新确认。

## 4. 验证矩阵与通过条件

新增永久测试先在旧实现上复现两类失败，再修复并运行同一断言。内存、Redis 7.4.11、Valkey 9.1.2 三种后端覆盖下表；两个服务使用相同生产客户端与 Lua，候选只是对照实例。

| 类别 | 必须覆盖 |
| --- | --- |
| 有效期 | 到期前 1 秒、恰好到期、到期后；目标过期但另有有效目标；唯一目标 / key 已自然消失；过期 Complete 重放；目标在 Redis 预读与执行间变为过期的确定性夹具 |
| 结算 | 有效 Complete 一次计数，重复 Complete / Fail 幂等；并发 Complete / Fail 与 Statistics；跨日归属保持；分钟尝试不回滚 |
| 整数 | 1、32 位上界附近、4,000,000,001、8,000,000,002、`2^53-1 / 2^53 / 2^53+1`、`long.MaxValue-1 / long.MaxValue`；精确配额、差 1 字节拒绝；多个预留之和跨越 `2^53` |
| 上界 | 已用 + 预留恰好 long.MaxValue；再申请 1 字节正常拒绝；旧状态聚合 / 结算溢出明确失败且无预留丢失；配置下调后的已有预留仍可正确结算 |
| 存储与返回 | 原始 Hash 数值只含十进制数字；日 key 精确字节；脚本返回再解析不舍入；连续 Statistics 不改变有效预留；旧健康格式兼容 |
| 异常状态 | 科学计数法、负数、非数字、越界与错误 Redis 类型；预留 / 日计数校验失败不得先删目标或消耗分钟次数；缺失与损坏计数区别；合法过期项与坏项混存 |
| 配置 / 并发 | 0 限额、负配置、禁用、非法 fileSize；两个独立 multiplexer、20 个并发 Acquire，不超过并发 / 日容量上限；内存锁路径同样覆盖 |
| 周边回归 | 原有 8 项限流用例、AttachmentController、ChunkedUploadService、AttachmentService 上传契约与上传日志测试；正常错误码、失败释放、终态重放行为保持 |

用例不创建超大文件，大整数直接传入真实限流服务。FakeTimeProvider 检查业务边界，真实短 TTL 单独检查服务器过期；不以睡眠碰运气制造边界。必要时用现有 mock 拦截 Redis 预读后继续委托真实数据库，明确其只用于时序控制。

通过条件：新矩阵全部通过；原 6 个失败在两端按原业务预期变为通过，不能删除、改成期望异常或降低断言；各端 filter 发现数非零且指定集成测试零跳过。记录精确名称、输入、TRX 与异常状态前后快照。普通无连接测试运行可显式跳过集成项；显式启用集成模式而缺连接 / 连不上必须失败。

Debug / Release 执行 `dotnet build Radish.slnx -c <configuration> --no-restore --warnaserror`；定向行为测试使用 `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter '<实际类名或 trait>'`，保存 TRX。测试入口编写后在执行记录中给出完整 filter 与发现数；缺现有 assets / native 产物先报告，不自动安装或升级依赖。完成 `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check`。此次不涉及 ORM，不重复 PostgreSQL 容器 / 三组全量 / 十二份 publish。

## 5. 已批准的本轮运行与清理范围

**项目所有者已明确批准本次修复及以下两台临时测试容器、回归和清理，并要求先提交推送方案再改代码。** 本次授权不扩大到依赖替换或发布。

| 项目 | 限定范围 |
| --- | --- |
| Redis | `redis@sha256:44960b3100c48159a66d28c18b0588c193492e25f7551d60e51dd464fc3eca5b`，7.4.11，`127.0.0.1:16379:6379` |
| Valkey | `valkey/valkey@sha256:418652cfb58ef879d4978c33553735d7147016032d5aefaa14c828e611eb9dfd`，9.1.2，`127.0.0.1:16380:6379` |
| 资源 | 每个 `--platform linux/arm64 --cpus=1 --memory=512m --pids-limit=128`；最大两台，使用本机已有 Docker daemon 和镜像，`--pull=never` |
| 数据 | database 15；本轮随机名称、owner 标签、独立卷、合成密码、唯一 key 前缀；不读取项目连接串或用户已有卷 |
| 命令范围 | `docker image inspect`、`docker volume create`、两次 `docker run -d`、限本轮容器的 exec 健康 / 元数据读取，以及 stop / rm / volume rm 清理；不安装包、不启动 Api / Auth / Gateway / Web |

启动参数沿用[隔离命令附录](./nodrive-valkey-isolation-validation-commands.md)第 4 节的两台 server、密码、AOF 和 healthcheck 配置，名称 / owner / 凭据改为本轮新值；只创建 Redis / Valkey 两个卷，**不执行原附录的 pull、第三个 RDB 卷、依赖安装、PG、RDB 恢复或 restart 阶段**。镜像缺失、端口占用、daemon 非本机或不可用即停止并报告，不自动启动 Docker Desktop、不换端口 / 版本或拉取镜像。

获批后先编写并检查本轮专用启动 / 清理脚本，注册 EXIT trap 后再创建资源。正常与异常退出均核对归属再停止并删除两台容器、两个卷及本轮控制临时文件；保留测试源码、文档和证据，不清理共享 NuGet / Docker 镜像缓存，不使用 FLUSHALL / 全局 prune。结束用标签列表与两个端口检查确认清理；清理失败保留资源标识并明确报告。

本轮聚焦业务缺陷，AOF / RDB / 重连历史结果保留原证据日期，不冒称修复后重验。TLS、Cluster、Sentinel、生产数据处置、发布 / 回退、永久账本和 NoDrive / Valkey 正式采用均另行确认。
