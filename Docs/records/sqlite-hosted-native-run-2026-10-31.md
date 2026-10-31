# SQLite 托管三平台与 AMD64 镜像单次验证

> 2026-10-31（Asia/Shanghai）。所有者明确批准完整 SHA 的 push 与一次 SQLite 三平台 / 四个 AMD64 最终镜像验证、证据归档和清理；失败先分析记录，不自动重试，不执行 Razor verify、发布或部署。专题见[托管执行方案](../features/sqlite-hosted-native-validation-plan.md)，接续[执行契约修正](./sqlite-hosted-execution-contracts-2026-10-31.md)。

**结果：Linux x64 通过；Windows、Intel macOS 与四个 AMD64 镜像任务失败，整体 run 为 failure。七项清理 / 上传均成功，未重试。** [结构化结果](./sqlite-hosted-native-run-2026-10-31.json)。

## 1. 执行身份与范围

- 批准 / push / run HEAD：`915f63591bfbfeff0978c8fb2de3b64fd997cce9`；工作流 blob `03f727d16591f9ffe6409bedd87670c87640b8f4`。
- 远端 dev 从 `75a9e33f8a3d1856d1fa0ffaa7f96c091824d456` 快进三个提交；push 后核对远端 ref 与工作流字节一致。默认 master 保持 `4a8589b6a1c5264506c51429cf7fe470e783f9fc`，执行前没有开放的 head=dev PR。
- [Run 41196110296](https://github.com/laugh0608/Radish/actions/runs/41196110296)，attempt `1`；dispatch 时间 `2026-10-31 19:15:37 +08:00`，CLI 成功返回唯一 run URL。
- 输入：`validation_scope=sqlite-platforms`、`expected_head_sha=915f63591bfbfeff0978c8fb2de3b64fd997cce9`。输入证据为本地 dispatch 记录、job 环境与前置检查；没有声称取得独立事件 payload。
- 七个 SQLite 矩阵任务独立执行，原三类验证与 Hangfire Razor 四个 job 均 skipped；没有其他 scope、重跑、取消、tag、Release、镜像推送或部署。

## 2. 逐项结果

| 目标 | job ID | 已取得证据 | 停止位置 |
| --- | --- | --- | --- |
| Linux x64 | `124522492546` | Debug / Release 严格构建，测试各 60 项，16 份 publish，48 个冷进程，4 次图片自检，16 份 notices 清单 | 全部通过 |
| Windows x64 | `124522492600` | VS 2026 x64 编译器身份样本、Rust Debug / Release、219 项包图与 NuGet 审计 | Release solution build 的 SQLite MSBuild 输出检查命令 |
| Intel macOS | `124522492577` | 原生 x64、Node / .NET / Rust、Xcode 与 SDK 路径日志 | C++ 编译器身份样本链接 |
| API AMD64 final | `124522492558` | build / final、SQLite 图与文件、57 份 notices 文件比对、探针编译 | microsoft 首用探针写 result.json |
| Auth AMD64 final | `124522492563` | build / final、SQLite 图与文件、31 份 notices 文件比对 | 探针编译 |
| Gateway AMD64 final | `124522492644` | build / final、SQLite 图与文件、29 份 notices 文件比对 | 探针编译 |
| DbMigrate AMD64 final | `124522492615` | build / final、SQLite 图与文件、31 份 notices 文件比对 | 探针编译 |

四个镜像任务均未完成冷探针、API 图片 self-test 或 Trivy 扫描；镜像成功构建不等于最终镜像验收通过。Windows 没有进入定向测试、publish 或冷探针；macOS 没有进入原生库构建、应用 restore / build。Linux 本批实际完成 16 份 publish / 48 个冷进程；原始两份完整 TRX 与 48 份实际模块结果已在本机独立复核，测试零失败 / 跳过，SQLite native / managed hash、版本与 sourceId 均符合 contract。不将三平台计划的 48 份 publish / 144 个冷进程记为本次已执行。

## 3. 已定位的失败

### Windows：MSBuild 命令的路径与引号边界

真实工具为 Visual Studio Enterprise 2026 `18.11.12315.225`，MSVC `14.51.36231`；x64 编译身份样本和 Rust 双配置均成功。此次不是 VS 2022 查询失败，也不应通过切换到 Razor runner 规避。

`015-build-Release.log` 中四宿主均报告 `SQLite output check requires deps file name`，随后 `sqlite-native.targets(14,5)` 产生 MSB3073。实际命令中 `--output="...\bin\Release\net10.0\"` 紧接 `--deps="Radish.*.deps.json"`；末尾反斜杠落在闭引号前。结合检查器明确报告缺失 deps 选项，定位为 Windows 命令行传参边界，而不是 deps 文件内容或 SQLite 包缺失。应在后续修正中验证真实 Windows argv，对 build / publish 同类目录参数一起处理。

### macOS：记录了 SDK 路径，却未将其传入直接调用的编译器

`016-compiler-identity.log` 为 `ld: library 'System' not found`。命令直接调用 Xcode 16.4 toolchain 内的 clang；`013-macos-sdk.log` 已取得 `.../MacOSX.platform/Developer/SDKs/MacOSX.sdk`，但编排器只记录该路径，样本编译 argv 不含 sysroot，也没有由编排器设置 SDKROOT。

这说明当前身份样本没有建立完整的 Xcode SDK 使用环境；下一批应显式连接所选 SDK 与编译 / 链接环境，并校验目标架构。日志没有证明 SDK 安装损坏，不应据此扩大安装范围。此次失败发生在新身份样本，不能归因于 Rust 或 SQLite 本身。

### 三个非 API 镜像：探针编译引用与实际宿主输出不匹配

`RuntimeProbe.cs` 的原契约是针对已验证的 API 输出编译，然后配合各宿主原 deps / runtimeconfig 执行可用 mode。Docker job 却各自以 Auth / Gateway / DbMigrate final 输出编译整份探针。

Auth / DbMigrate 各出现 9 个编译错误，缺少 Hangfire 与 `Radish.Api.Services`；Gateway 出现 10 个错误，另缺少 EF。错误与源码的直接引用、宿主引用集合一致，属于探针编排与编译边界问题。后续应明确公共 / EF / Hangfire 探针的编译依赖与候选绑定方式，保持宿主原依赖边界；不向宿主补入无关包来迁就测试工具。

### API 镜像：探针数据挂载目录的写权限不匹配

首个 microsoft mode 在写 `/probe-data/result.json` 时抛出 UnauthorizedAccessException / Permission denied。该 mode 尚未形成可校验的结果 JSON，因此不能登记为冷进程验证通过。

证据显示 final image `Config.User` 为空，run 没有指定 `--user`，挂载目录由 runner 创建，同时容器 `--cap-drop ALL`。编排器没有建立容器执行身份与宿主目录所有者之间的写入契约；缺少现场目录 uid / gid / mode 记录，不能补写这些具体数值。后续应显式匹配任务 uid / gid 并增加写入前置验证，保留只读根文件系统、断网和 capability 限制，不能靠 chmod 777 或恢复所有 capabilities 掩盖问题。

## 4. 归档、清理与停止线

本地证据根目录为 `artifacts/sqlite-hosted-native/915f6359`。保存批准输入、push 前引用、工作流快照、dispatch 命令、GitHub 原始 run / jobs / artifacts REST 元数据、原 ZIP、解包证据与日志。原始证据不改写，分析结果单独保存。

artifact 设为保留 14 天，服务端到期为 2026-11-14。逐份先比对 ZIP SHA256 与服务端 digest，再核对 artifact-manifest 中每个文件的 size / hash、路径边界和文件集合，不接受额外文件或符号链接。

本机经既有代理下载存储端 ZIP 时遇到 TLS 超时；仅对这次归档下载使用直接连接后成功，未修改系统代理、证书或全局环境，也未重复派发 CI。GitHub API token 只用于 GitHub API 请求，重定向后的存储请求不携带该 token；临时签名链接不写入项目记录。

- Run 于 `2026-10-31 19:23:15 +08:00` 完成最后一个 job；1 success / 6 failure / 4 skipped。七个 job 的清理、artifact 上传和 post-job 均成功。
- 七个 ZIP 均与服务端 digest 一致，内部 **573 个 payload 文件**的 size / hash、完整集合均通过；另有七份 manifest。完整 job 日志 ZIP SHA256 为 `564736aabb09bb14c5d2c92928e3ce401b4b2015063fec1dfad7f584bd66d42a`。
- 本地总归档 manifest 覆盖 **693 个文件**，SHA256 为 `cba7d2f12ab4586b30dbf38563d9e0cbc80967c598c92d9577f22cdfdca62bd8`。原 ZIP、原始解包证据和日志保留；摘要单独存储，未改写原始结果。
- 七份 cleanup.json 均为 completed=true、workingTreeRemoved=true、errors=[]；309 条 owned command 全部有 completed 终态，无 timeout / killError。四个 Docker job 合计删除八个任务 image tag；最终按任务名称 / tag 查询的日志均为空，受控容器已不存在。
- 共享 base image / BuildKit cache 没有执行 prune；它们和 checkout / 剩余证据依赖托管 runner 生命周期处理。没有独立观察 VM 销毁，不能把这一边界写成已完成物理销毁。
- 本机保留归档文件；下载临时文件已清除，未安装工具、启动业务服务或运行本机镜像。此次只写分析与计划记录，没有修复运行代码或再次触发 CI。

下一步应成组修正 Windows argv、macOS SDK 使用、探针编译依赖及容器写入身份，并补相应平台行为回归；新候选的 push / 远端验收另行确认。本次一次性授权已执行完毕。
