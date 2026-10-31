# PostgreSQL 回归与托管 CI 本地接线记录

> 2026-10-31（Asia/Shanghai）。所有者已批准 PG17 / PG16 回归、四组 Hangfire PostgreSQL worker 与资源清理，并批准托管 runner 的本地 CI 改动 / 验证；明确不 push、不触发远端运行、不发布、不部署。结构化结果见[证据摘要](./postgres-regression-2026-10-31.json)。

## 1. 输入与边界

- PG 源码冻结在 `174dd1bab4bf3e9ea572d063a654462cd0ac7fb3`，通过 git archive 建立隔离副本；工作区本轮 CI / 文档改动未混入该业务源码快照。
- 本机 macOS ARM64，SDK `10.0.300`、runtime `10.0.8`、Node `24.16.0`；复用经 verify 的 osx-arm64 两配置 native。19 个项目的新 restore 图仍为批准的 219 个 package/version，NuGet 审计无漏洞；Debug / Release 严格构建均零警告、零错误。
- PG17 `17.11-1.pgdg13+2`、PG16 `16.15-1.pgdg13+2` 使用[方案](../features/postgres-regression-execution-plan.md)冻结的缓存 ARM64 digest，`--pull=never`；串行绑定 `127.0.0.1:15432 → 5432`，各实例使用新任务卷。不读取 Local 配置、既有数据库或生产连接，不启动业务宿主 / 前端。

## 2. 实际测试结果

| 数据库 / 配置 | 范围 | 总计 | 通过 | 预期环境跳过 | PG 方法通过 |
| --- | --- | ---: | ---: | ---: | ---: |
| PG17 Debug | 后端全量 | 4,239 | 4,137 | 102 | 43 |
| PG17 Release | 后端全量 | 4,239 | 4,137 | 102 | 43 |
| PG16 Debug | PostgreSQL 专项 | 43 | 43 | 0 | 43 |
| PG16 Release | PostgreSQL 专项 | 43 | 43 | 0 | 43 |

四份 TRX 均完整 Completed，失败均为 0；合计 8,360 次测试执行通过，其中 PG 方法执行 172 次。跳过数按逐项 UnitTestResult 统计，不能使用此适配器中为 0 的 `Counters.notExecuted` 推定没有跳过。两份全量中的跳过均来自 UploadQuota 的 Redis / Valkey 集成类，原因为未显式启用该隔离集成；没有 PG 跳过，未启动 Redis / Valkey。

实际 API 两配置选中 NoDrive `SqlSugar.dll` SHA256 `38a361e486a809ee2242861ce5ecb94016dca99bcaa9541d8f54cda8b06e9d0b`；未误选 SystemCommon 包中的同名 DLL。Npgsql `10.0.0`、Hangfire.Core `1.8.14` 和 PostgreSql `1.20.12` 的输出与原包逐文件一致，详见证据摘要。

## 3. 四组 Hangfire PostgreSQL worker

PG17 / PG16 × Debug / Release 四组全部通过，每组均：

- 通过当前正式包 storage 真实调用 API 注册，检查 16 项 ID / cron / 时区 / 方法 / 参数，然后移除全部注册项。
- 仅在专用队列运行一个成功任务和一个先失败、重试一次后成功的任务；两个状态均为 Succeeded，重试任务实际尝试两次。没有执行业务周期任务。
- 停止 worker、释放 storage / connection pool，删除本组限定 schema，并通过数据库查询确认该 schema 数量为 0。

合计核对 64 项注册、8 个任务最终成功，四个 schema 均已删除。两种数据库在卷删除前都只剩 `postgres / radish_pg / template0 / template1`；DbMigrate 的五个随机测试库没有残留。临时探针仅补齐 using 后编译，不替换任何正式 DLL，也不作为 Hangfire 源码重建证据。

## 4. 托管 runner 本地接线

已在现有 `native-images.yml` 增加可选 `validation_scope`，默认 `native-images` 的三类旧 job 内容保持；`sqlite-platforms` 只在 workflow_dispatch 下运行三个原生 x64 job 与四个 AMD64 Docker job。新增 `Scripts/ci/validate-sqlite-platform.mjs` 统一执行独立源码副本、版本 / 架构、原图、测试、publish、冷探针、扫描、所有权清理与证据过滤；既有 SQLite checker / contract、正式 Dockerfile、required checks 和发布流程未改。

本地验证通过：

- Node 语法及 28 项契约测试，其中新编排器 9 项，原 SQLite 门禁 8 项、既有漏洞策略 11 项。
- YAML 解析、三个旧 job 内容逐项对比、3 + 4 矩阵、contents:read 权限及既有 Repo Quality 契约。
- 四份前批真实 TRX（各 60 项）的新解析器复核，以及四份正式 Dockerfile 的冻结 FROM 转换；没有重新运行前批 Linux 矩阵。

新增门禁对中文 DisplayName 使用 TRX 的 TestDefinitions / className 判定覆盖，避免把显示名当作类名。未安装 actionlint；上述检查不替代 GitHub 的工作流解析或真实三平台运行。远端 `dev` 仍未取得这批代码，本轮没有 push / dispatch，不能据此关闭原生 AMD64、Windows x64 或 Intel macOS 验收。后续执行按[托管方案](../features/sqlite-hosted-native-validation-plan.md)另行确认最终提交。

## 5. 失败尝试、归档与清理

1. 沙盒内首轮全量在临时证书夹具的 Keychain 调用处失败，按已核实 PID 停止本任务测试进程组。没有接受缺失 TRX 或退出码 0 作为通过，也未修改断言、系统信任或证书设置。
2. 相同冻结产物经最小提权重跑，PG17 Debug 完整通过；临时汇总器误要求跳过原因含环境变量名而停止。核对其 102 项确为已说明的 Redis / Valkey 跳过后，仅修正汇总器，保存并复用该完整 TRX，继续其余测试。两次中断均先清理实例，再创建新卷，因此本任务共启动并删除 4 个数据库实例，只有一个端口串行占用。
3. 最终核对本任务容器 / 卷标签清单为空，15432 无监听，没有本任务测试 / 迁移 / 编译进程残留；临时代理排除仍为空，未修改代理。共享 PG 镜像及其他项目资源保留。
4. 证据保存到工作区 Git 忽略目录 `artifacts/validation/radish-pg-regression-20261031-b6d8-evidence`，含完整 TRX、日志、命令、探针源码、哈希和清理记录；索引覆盖 161 个文件、21,765,787 字节，索引 SHA256 见 JSON。没有复制数据库、包缓存、publish 二进制或私钥。
5. 归档后删除本任务隔离源码、包缓存、bin / obj、合成数据与临时控制器，原临时任务目录已不存在。长期可追踪结论与关键哈希进入本记录 / JSON，本地归档不作为 Release 或正式对应源码发行入口。

本批关闭上述 PG 回归与托管 CI 本地接线；Hangfire 完整对应源码 / 留存补证、剩余原生平台远端运行及合并前候选门禁继续独立推进。
