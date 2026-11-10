# SQLite 夹具修正候选的单次托管验收

> 2026-11-04（Asia/Shanghai）。所有者批准将 `44c13c5091f51621d475107fbb5834ceedef12f4` push 到 `dev`，单次执行完整 `sqlite-platforms` 七 job，沿用冻结输入，归档证据、清理资源并同步文档。失败先分析记录，不自动重试、不发布或部署。

## 1. 执行身份与范围

- [Run 41817734292](https://github.com/laugh0608/Radish/actions/runs/41817734292)，`workflow_dispatch`，attempt 1；批准候选与 `head_sha` 完全一致。
- push 前远端 `dev` 为 `97e9c4a696d671d768ab77f0048cc9496ac73cc8`，已核对快进关系；push 后读取 GitHub ref 确认候选。仅本地 `origin/dev` 更新受沙盒锁文件权限限制，经最小提权修正跟踪引用，没有再次 push。
- 工作流 blob 为 `ecf481eb2b171985f0721efaff220beff0ac3a3b`；远端文件与本地逐字一致，SHA256 为 `6e19c3b304217f18891d266c0c2cb22926bc027a926008bd5755b831e9caf1cf`。
- 派发前查询该 SHA 的手动运行数为 0，先保存派发意图，再单次执行；输入为 `validation_scope=sqlite-platforms`、`expected_head_sha=44c13c5091f51621d475107fbb5834ceedef12f4`。派发命令、运行 API、各 job 的 SHA 门禁与 candidate 文件交叉核对；没有独立留存 GitHub 原始 event payload。
- Node `24.22.2`、.NET SDK `10.0.403`、Rust `1.96.0`、Trivy `0.70.0` 与冻结镜像 / action SHA 沿用[托管方案](../features/sqlite-hosted-native-validation-plan.md)。三个原生平台及四个 AMD64 final 执行；Razor、原 Native platform / Docker / ASan 共四类 job 跳过。

## 2. 运行结果

运行于 `2026-11-04 19:18:53 +08:00` 完成，结果为 **6 success / 1 failure / 4 skipped**。七份 artifact 已全部核验；Windows 首个冷探针失败使整批保持未通过，没有重试旧 run 或个别 job。

| 目标 | 实际结果 | 验证边界 |
| --- | --- | --- |
| Linux x64 | 通过；两配置各 60 项、16 份 publish / 探针编译、48 个冷进程、4 次图片自检 | 12 组 MSBuild argv、完整 TRX、实际模块身份与清理通过 |
| Intel macOS | 通过；两配置各 60 项、16 份 publish / 探针编译、48 个冷进程、4 次图片自检 | 12 组 argv、C / C++ x64 链接、完整 TRX 与清理通过 |
| Windows x64 | Release 60 项通过；1 份 API 普通 publish 及探针编译完成，首个冷 mode 失败 | 24 组 argv、Rust 双配置与 Release 严格构建通过；Debug 应用及后续矩阵未执行 |
| AMD64 API final | 通过；4 个冷 mode、图片自检、漏洞策略 | 16 项 recurring registration、成功 / 重试行为及独立编译通过 |
| AMD64 Auth final | 通过；3 个冷 mode、漏洞策略 | 本宿主独立探针编译、实际加载与清理通过 |
| AMD64 Gateway final | 通过；2 个冷 mode、漏洞策略 | 本宿主独立探针编译、实际加载与清理通过 |
| AMD64 DbMigrate final | 通过；3 个冷 mode、漏洞策略 | 本宿主独立探针编译、实际加载与清理通过 |

实际为 **300 项测试全部通过，0 failed / 0 skipped**，不是计划的 360 次，缺少 Windows Debug。三平台共完成 33 份 publish、33 次探针编译，96 个原生冷进程通过、1 个失败；四 final 另有 4 次编译、12 个冷 mode 通过。图片自检共 9 次，MSBuild argv 共 48 组。Windows 失败冷进程不计作成功加载证据。

四个 final 均以 `1001:1001` 执行受限探针，12 组可写 / 回读 / 所有者 / 删除检查与 4 组拒绝写入检查通过；四份 Trivy 策略均为 HIGH / CRITICAL 零发现，无例外放行。scanner、各自 DB 内容 hash 与原始报告保留，不声称四个 job 共用同一 DB 物理快照，也不改变正式镜像 USER 策略。

完整逐 job 计数、平台身份、TRX、失败命令及清理摘要见[结果 JSON](./sqlite-hosted-native-fixture-run-2026-11-04.json)。

## 3. Windows：迁移测试通过，首个冷探针出现新失败

完整 Release TRX 为 **60 passed / 0 failed / 0 skipped**，三个 `AuthOpenIddictMigrationTest` SQLite 用例均实际执行且通过。此前临时数据库删除失败在本轮 Release 未重现；Windows Debug 尚未执行，不能推定双配置或平台验收已关闭。

Windows 本次为 `windows-2025` 标签，实际 `ImageOS=win25-vs2026`、`ImageVersion=20261030.294.1`；与 Hangfire Razor 的 `windows-2022` 身份问题是独立范围。24 组 MSBuild argv、MSVC x64 链接样本、Rust 双配置与应用 Release 严格构建通过；随后完成第一份 `Release-generic-Radish.Api` publish、SQLite 输出门禁及对应探针编译。

首个 `microsoft` 冷进程失败，原始错误为：

```text
System.InvalidOperationException: Expected one actually loaded SQLite native module; found 2
```

失败命令为 `commands/021-probe-Release-generic-Radish.Api.log`，冷进程的错误结果与 `process.log` 均保留；API summary 为未完成。后续 cold mode、其余 publish、API 图片 self-test 和 Debug 应用构建 / 测试均未进入。本轮 Windows 只取得一份 publish、一次探针编译和一次失败冷 mode，不把计划的 16 / 48 计作执行结果。

只读源码核对发现，`Scripts/sqlite/RuntimeProbe.cs` 的 `LoadedSqliteModules` 在非 macOS 平台使用 `module.ModuleName.Contains("e_sqlite3", StringComparison.Ordinal)`，随后要求命中数为 1。发布物中的托管 `SQLitePCLRaw.provider.e_sqlite3.dll` 名称也包含该子串；因此存在将托管 provider 一并计数的可能。**这是代码推断，不是已确认根因**：失败前没有保存实际两个命中的 ModuleName / FileName，无法判断它们是原生库、托管 DLL、重复枚举还是其他模块，也不能据此宣称存在两个不同 SQLite 原生库。

接续建议维护在[成组方案第 9 节](../features/sqlite-hosted-validation-repair-plan.md#windows-module-probe-followup)。本批只分析记录，没有修改探针或生产代码；不得通过直接放行数量 2、忽略候选、静默去重或换库来收口。

Windows 清理报告为 `completed=true`、`workingTreeRemoved=true`、`errors=[]`；全部 21 条受控命令有 completed 终态，只有上述探针命令退出码为 1，没有 timeout、cancelled 或 killError。失败 artifact 的服务端 size / digest 与内部 69 个 payload 文件及 manifest 已校验。

## 4. 归档与边界

本机归档目录为 `artifacts/sqlite-hosted-native/44c13c50/`，保留授权与派发记录、远端 ref、工作流快照、冻结输入、运行元数据、原 ZIP、未修改的解包证据、解码 job 日志及独立分析。下载通过 GitHub 插件文件引用完成，临时签名 URL 不写入仓库或归档。

七份 ZIP 的服务端 size / SHA256 digest、内部清单的完整文件集合 / size / hash 均通过，共 **1,449 个 payload 文件**，另有七份 artifact manifest。七份 candidate 均匹配批准 SHA / run ID / attempt，七份 cleanup 均为 completed / workingTreeRemoved=true、errors=[]。699 条受控命令全部有 completed 终态，无 timeout、cancelled 或 killError；唯一非零命令是已记录的 Windows 冷探针。四个 Docker job 共删除 8 个任务镜像标签，没有 prune 共享基础镜像或 BuildKit cache。

总归档 manifest 覆盖 **1,489 个文件（不含自身）**，SHA256 为 `cd7b1e76433fdad799be4d2f7ab17119ee6df85058f76a5c8753713ef97f04c4`，已逐文件回读核验。派发后查询批准 SHA 仅有本次一个手动 run，attempt 为 1；原 ZIP、解包证据和七份解码日志保留，分析与汇总文件独立保存。

没有安装本机依赖 / 工具、启动本机服务 / 容器、改动生产或重新派发。远端清理只核对本任务资源，不 prune 共享缓存；托管 VM 最终物理销毁没有独立观察，不将任务清理报告等同于该事实。
