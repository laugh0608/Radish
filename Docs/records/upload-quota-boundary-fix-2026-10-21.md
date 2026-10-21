# 上传配额过期与整数边界修复记录（2026-10-21）

> 项目所有者批准[修复方案](../features/upload-quota-boundary-fix-plan.md)、两台临时测试容器、回归及清理；方案先以 `46d22765` 提交并推送 `dev`，再修改代码。NoDrive、Valkey 正式替换及发布均未执行。结构化计数、源码与原始证据哈希见[证据摘要](./upload-quota-boundary-fix-evidence-2026-10-21.json)。

## 1. 结果与行为变化

- 内存 / Redis Complete 都拒绝结算 `expiresAt <= now` 的预留，仅释放目标；同一 Hash 中另有有效预留也不会让过期目标被计入日容量。分钟尝试次数保持不回滚。
- 容量全链路支持精确 Int64：生产 Lua 以十进制字符串比较、加减、存储与返回容量，INCRBY 直接接收原始字符串；内存算术与占用统计检查溢出，新增容量按剩余值判断，避免先相加溢出。
- 缺失计数仍为 0；已存在的非法整数、科学计数法、负值、越界或错误 Redis key 类型明确报错。可预判的格式 / 类型 / 溢出错误不会先消费分钟次数或删除结算目标；有效旧四段格式继续兼容。
- 启用限流时负配置明确失败，0 保持阻止新增预留的语义。下调日限额后，已有占用照实返回、拒绝新增、有效预留仍可完成；无配置键、公共方法签名、依赖或部署文件变更。

业务逻辑集中在 `UploadRateLimitService` 与内部 `UploadQuotaScripts`。正常配额拒绝仍走既有 429；普通上传持久化成功后的配额失败、分片终态重放及日志策略保持原有行为，新增测试覆盖这些调用关系。

## 2. 修复前后证据

先新增相同的过期 / 大整数断言，在旧生产实现上运行；首个测试夹具构建的 Dispose 调用错误已修正后重新严格构建，未关闭分析器。

| 阶段 | 通过 / 总数 | 失败 / 跳过 | 含义 |
| --- | --- | --- | --- |
| 修改业务代码前，首组边界回归 | 22 / 39 | 17 / 0 | 内存 3 失败、Redis 7 失败、Valkey 7 失败，确认过期与整数问题 |
| 最终 Debug 定向回归 | 262 / 262 | 0 / 0 | 当前源码、真实两端连接与内存用例全部执行 |
| 最终 Release 定向回归 | 262 / 262 | 0 / 0 | 相同 filter、相同用例名，Release 实际产物执行 |
| 显式启用但缺 Redis 连接的夹具门禁 | 0 / 1 | 1 / 0（预期失败） | 返回码 1，明确指出缺少连接，没有跳过或退回内存；不混入业务通过数 |

逐名核对修复前全部 17 个失败，在最终两份 TRX 中都为 Passed；其中保留[隔离实验](./nodrive-valkey-validation-2026-10-21.md)原六个失败对应的过期目标与 `2^53±1` 业务预期，没有改成接受异常或降低期望。

每种构建配置下的 262 个用例如下：

| 测试组 | 数量 |
| --- | ---: |
| 内存配额边界 | 43 |
| Redis 配额边界与原生存储 | 43 + 8 |
| Valkey 配额边界与原生存储 | 43 + 8 |
| 原限流测试（含新增占用溢出断言） | 9 |
| AttachmentController | 22 |
| ChunkedUploadService | 20 |
| AttachmentServiceUploadContract | 34 |
| AttachmentUploadLogging | 32 |

覆盖到期前 / 恰好到期 / 到期后、其他预留保活、唯一目标与服务器真实 TTL、预读后过期、跨业务日、重复结算 / 释放、两连接并发与 Complete / Fail / Statistics 竞争。数值包含 32 位附近、4 / 8 GB、`2^53-1 / 2^53 / 2^53+1`、`long.MaxValue-1 / long.MaxValue`、精确总量与差 1 字节拒绝。另有固定种子的 32 组 Int64 样本，在每种后端用 BigInteger 独立计算预期；计为每端一个 Fact，不把内部循环冒充额外测试数。

异常缓存检查保留前后状态，证明格式 / 溢出拒绝不删除目标或其他已过期条目、不增加分钟计数。Redis 实测保留 Hash / String 类型、十进制原值、120 秒分钟 TTL、1 小时默认预留与业务日日计数 TTL。预读时序测试只用 mock 控制时间推进，HashGet 和 ScriptEvaluate 均委托真实数据库与生产 Lua。

## 3. 执行输入与命令

- macOS arm64、现有 .NET SDK `10.0.300`；使用现有 assets 和 native 产物，未安装 / 升级包或修改 lockfile。
- 本机 OrbStack，Linux arm64：Redis `7.4.11` 与 Valkey `9.1.2`，固定 digest 与资源参数按方案；每台 1 CPU / 512 MiB，只绑定 `127.0.0.1:16379 / 16380`。
- database 15、每轮 / 每用例独立前缀、独立卷与合成凭据，未读取生产配置或复用项目卷。测试直接调用真实 UploadRateLimitService；Redis 分支操作现有 StackExchange.Redis 客户端与生产 Lua。
- 两端连接与前缀通过方案第 6 节环境变量显式注入。仅测试进程设置 `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false`；没有修改产品配置。

```bash
dotnet build Radish.slnx -c Debug --no-restore --warnaserror
dotnet build Radish.slnx -c Release --no-restore --warnaserror
```

两次均为 0 warning / error。每种配置各执行以下完整 filter，并分别保存 TRX：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -c Debug --no-build --no-restore --filter 'FullyQualifiedName~UploadQuota|FullyQualifiedName~UploadRateLimitServiceTest|FullyQualifiedName~AttachmentControllerTest|FullyQualifiedName~ChunkedUploadServiceTest|FullyQualifiedName~AttachmentServiceUploadContractTest|FullyQualifiedName~AttachmentUploadLoggingTests' --logger 'trx;LogFileName=debug-regression.trx'
```

Release 使用 `-c Release` 与独立 `release-regression.trx`。文档与文本卫生执行 `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check`，均通过。未将这些定向结果称为全量后端、Repo Quality 或发布验收。

## 4. 清理与剩余边界

本轮根目录 `/private/tmp/radish-upload-quota.tj6YWc` 的两个容器、两个命名卷已按 `io.radish.upload-quota` owner 标签核验后删除。清理退出码 0，标签筛选无剩余资源，端口 `16379 / 16380` 无监听；控制目录及外部启动入口已删除，只保留 `.owner` 与 `evidence/`。镜像缓存保留，未执行 FLUSHALL / 全局 prune，未清理其他任务资源。

原始 evidence 保存修复前代码、最终十个 C# 文件、源码 diff、构建日志、各轮 TRX、测试逐名对照、镜像 / 容器元数据与清理结果；manifest SHA256 进入仓库证据摘要。本机临时目录不是永久远程归档，永久回归源码已随 `31973553` 提交到仓库。

- 旧科学计数法记录已损失精度，无法推测还原；现在报错并保留，未访问或批量修正生产数据。
- 本轮没有新增持久化 settlement ledger / outbox、网络故障 exactly-once、内存多次缓存 IO 的事务回滚或上传 ID 重用支持。
- AOF / RDB / 重连仍引用上一轮独立实验结果，本次未重跑；未验证 TLS、Cluster、Sentinel、负载容量、Linux .NET 宿主或 amd64。
- NoDrive、Valkey 的正式采用、部署数据迁移与发布继续等待独立裁决。实现与结果文档已随 `31973553` 提交；后续事项统一见[当前进行中](../planning/current.md)。
