# SQLite 模块识别修正候选的单次托管验收

> 2026-11-04（Asia/Shanghai）。所有者确认继续上一轮已说明的范围：将 `ab37d19eaf83189ec5c556d14608c1c132dce022` push 到 `dev`，单次执行完整七 job `sqlite-platforms`，沿用冻结工具版本，核验模块清单、归档证据、清理资源并同步文档；失败只分析留痕，不自动重试、发布或部署。

## 1. 执行身份

- [Run 41825865229](https://github.com/laugh0608/Radish/actions/runs/41825865229)，`workflow_dispatch`，attempt 1；`head_sha` 与批准候选完全一致。
- push 前远端 `dev` 为 `44c13c5091f51621d475107fbb5834ceedef12f4`，快进关系与干净工作区已核对；push 后 API 确认完整候选，本地跟踪引用也一致，没有第二次 push。
- 工作流 blob 为 `ecf481eb2b171985f0721efaff220beff0ac3a3b`，远端与本地文件一致，SHA256 为 `6e19c3b304217f18891d266c0c2cb22926bc027a926008bd5755b831e9caf1cf`。
- 派发前该候选的手动运行数为 0，先记录派发意图，再执行一次；输入为 `validation_scope=sqlite-platforms`、`expected_head_sha=ab37d19eaf83189ec5c556d14608c1c132dce022`。派发命令、API、job SHA 门禁和 candidate 文件用于交叉核对，不声称持有独立的原始 event payload。
- 三个原生平台与四个 AMD64 final 执行；原 Native platform / Docker / ASan 与 Razor 共四类 job 跳过。Node `24.22.2`、.NET SDK `10.0.403`、Rust `1.96.0`、Trivy `0.70.0`、action SHA 和镜像输入沿用[托管方案](../features/sqlite-hosted-native-validation-plan.md)，没有扩大平台或安装范围。

## 2. 结果与模块身份

本次于 `2026-11-04 20:24:57 +08:00` 完成，**7 success / 4 skipped**。七个 SQLite job 全部通过，原计划范围执行完整，未重试。结果与实际平台身份见[结果 JSON](./sqlite-hosted-native-module-run-2026-11-04.json)。

| 目标 | 实际通过内容 |
| --- | --- |
| Linux x64 | 双配置各 82 项、16 份 publish / 探针编译、48 个冷进程 / 模块清单、4 次图片自检、12 组 argv |
| Windows x64 | 双配置各 82 项、16 份 publish / 探针编译、48 个冷进程 / 模块清单、4 次图片自检、24 组 argv |
| Intel macOS | 双配置各 82 项、16 份 publish / 探针编译、48 个冷进程 / 模块清单、4 次图片自检、12 组 argv |
| AMD64 API final | 独立探针编译、4 个冷 mode / 模块清单、图片自检及漏洞策略 |
| AMD64 Auth final | 独立探针编译、3 个冷 mode / 模块清单及漏洞策略 |
| AMD64 Gateway final | 独立探针编译、2 个冷 mode / 模块清单及漏洞策略 |
| AMD64 DbMigrate final | 独立探针编译、3 个冷 mode / 模块清单及漏洞策略 |

合计 **492 passed / 0 failed / 0 skipped**，48 份 publish、52 次探针编译、144 个原生冷进程加 12 个 final 冷 mode、156 份模块清单、13 次图片自检与 48 组 argv 均通过。每份编译清单的公共 / EF / Hangfire 源码 hash 均与批准 Git commit 核对，SDK 为 `10.0.403`；实际 native hash 按本 job RID 对应的冻结文件核对，版本与 source ID 保留原门禁。

四个 final 的 12 组可写 / 回读 / 文件所有者 / 删除检查和 4 组拒绝写入检查通过；四份 Trivy 策略均为 HIGH / CRITICAL 零发现，无例外放行。各 scanner 与 DB 内容 hash 独立留痕，不推定四个 job 使用同一物理 DB，也不改变正式镜像 USER 策略。

## 3. Windows 实际模块身份

Windows 的完整 TRX 已核对：Debug / Release 各 **82 passed / 0 failed / 0 skipped**，三个迁移用例与新增识别行为用例均执行。16 份 publish、16 次按产物探针编译、48 个独立冷进程和 4 次 API 图片自检均通过，任务清理完整。

48 份实际模块清单均为一个 `managedProvider` 与一个 `native`，没有将两个候选放行成两份原生库。托管项为 `SQLitePCLRaw.provider.e_sqlite3.dll`，记录的程序集身份与 CLR 加载项一致，SHA256 为 `64f5e6797a789e6f212cf619cc42af3888e64df3d2ac2b35b81304e5d0945b9d`；原生项为 `e_sqlite3.DLL`，SHA256 为 `b7385d722c83fb52142a00477a726723745916d22a555711ee89834c1111fb2e`，实际版本 / source ID 匹配原 `3.53.3` contract。

普通输出从该宿主输出的 `runtimes/win-x64/native/` 加载，RID 输出从该输出根目录加载；各自路径、hash 与编译输入绑定均核对。Windows 名称按已批准的 OrdinalIgnoreCase 语义比较；归档核验脚本的分隔符 / 大小写处理已按该语义修正，仅影响本地读取核验，没有修改远端代码、原始 ZIP 或门禁。

当前清单的两个名称都会匹配旧子串条件，而按 PE / CLR 与原生格式区分后原生计数为 1。这建立的是**本候选**的实际事实；旧 run 没有留存两个命中项，不能回填其路径或据此宣称已独立观察到旧 run 的原因。

## 4. 归档与清理

本机归档目录为 `artifacts/sqlite-hosted-native/ab37d19e/`，保留批准语境、派发意图与响应、远端 ref、工作流、冻结输入、服务端元数据、原 ZIP、未修改解包证据、解码 job 日志及独立核验结果。下载使用 GitHub 插件文件引用，临时签名 URL 不写入项目或归档。

七份 ZIP 的服务端 size / SHA256 digest、内部 manifest 的完整文件集合 / size / hash 均通过，共 **1,910 个 payload 文件**，另有七份 artifact manifest。七份 candidate 均匹配完整 SHA / run ID / attempt；七份 cleanup 均为 completed / workingTreeRemoved=true、errors=[]。760 条受控命令全部有 completed 终态，无 timeout、cancelled 或 killError；四个 Docker job 删除 8 个任务镜像标签，没有 prune 共享基础镜像或 BuildKit cache。

本地总 manifest 覆盖 **1,951 个文件（不含自身）**，SHA256 为 `05e0ee24c1f8a02698454e466a1ccf050eb6728a862b284001d2ac75f2599d29`，已逐文件回读核验。该 SHA 仅有本次一个手动 run，attempt 为 1；原 ZIP / 解包证据与七份解码 job 日志保留，分析及汇总独立保存。

本机没有安装依赖 / 工具或启动项目服务 / 容器，不触达生产、不执行 Razor inventory / verify。任务资源只按 owner / ID 精确清理，不 prune 共享缓存；托管 VM 最终物理销毁没有独立观察，不将任务清理报告等同于该事实。

`npm run check:repo-quality:changed` 与 `git diff --check` 通过；原始证据、总 manifest 和结果 JSON 已回读核对一致。仅保留记录索引的既有篇幅提醒，本批不扩展文档拆分。

## 5. 本批收口与后续边界

本候选的三原生 x64 平台 / 四 AMD64 final 验收，以及临时 SQLite 夹具、模块识别修正的本次托管缺口已关闭。旧失败证据保留；本轮新清单不回填旧 run 的路径，也不宣称全部依赖许可、Hangfire 完整对应源码、合并前候选或其他产品线已经验收。

依既定顺位接续 Hangfire 来源 / 正式源码入口及剩余发行材料。只读复核 [actions/runner-images#15201](https://github.com/actions/runner-images/issues/15201) 仍为五条现有评论，[维护者最新回复](https://github.com/actions/runner-images/issues/15201#issuecomment-6580306089)仍待部署后跟进，未提供完成确认或修复版本；本批未发送消息或执行新的 inventory / verify。新工具安装、运行、服务迁移、集成、发布与部署继续各自确认，不沿用本次一次性授权。
