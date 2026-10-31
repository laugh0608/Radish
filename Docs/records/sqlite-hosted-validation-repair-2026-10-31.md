# SQLite 托管四类失败的本地成组修正

> 2026-10-31（Asia/Shanghai）。所有者批准按[成组方案](../features/sqlite-hosted-validation-repair-plan.md)实施本地修正与验证，不加临时兜底；暂不 push、远端验收、安装工具或启动服务 / 容器。本批接续[首次托管记录](./sqlite-hosted-native-run-2026-10-31.md)，基线提交为 `a074b68c`。

## 1. 实现结果

| 边界 | 本批改动 | 保持的约束 |
| --- | --- | --- |
| Windows argv | MSBuild 三个 target 在实际执行时转换 Windows 路径为正斜杠，保留完整引号、独立空 RID 和 deps 参数；加入真实 Exec → Node 捕获夹具 | SDK 冲突消解前检查、五个 build owner 和四宿主 publish 门禁不变；Node 不猜测丢失参数 |
| macOS SDK / 编译器 | 任务内固定 Developer Directory，显式 `xcrun --sdk macosx` 选取 SDK / clang / clang++；SDKROOT、目标专用 CC / CXX / linker 和 deployment target 一致；保留 clang++ 调用名，分别链接 C 系统库与 C++ 标准库样本 | 不切换全局 Xcode、不更换工具链、不执行样本；SDK 缺失 / 路径不匹配 / 错误 Mach-O 架构直接失败 |
| 探针编译依赖 | 公共 / EF / Hangfire 按职责分源；compile 也必填 host；每份 publish 和每个 final 单独编译，清单绑定宿主、输出、SDK、源码 / DLL、原 deps / runtimeconfig 与探针 hash | API 4、Auth 3、Gateway 2、DbMigrate 3 个 mode 固定；必需依赖缺失或跨产物复用直接失败，无新增业务包、替代 deps 或默认 API 兼容 |
| 容器身份 | 写入任务目录的 final 以 runner 数字 uid:gid 执行；每个 case 0700 目录核对所有者，容器内写入 / 回读 / 宿主文件所有者核对 / 删除；独立 0500 目录验证拒绝写入；API 自检和 scanner 同步明确身份 | 断网、只读根、限额、cap-drop ALL、no-new-privileges、私有 tmp 和只读探针挂载保持；不改正式 Dockerfile USER，不放宽权限重试 |

编译引用从原宿主 deps 的托管 runtime / runtimeTargets 资产选择，防止 Windows RID 根目录的原生 `.dll` 被误作托管引用；未用作引用的根目录 DLL 仍记录 hash 并绑定。四宿主真实编译通过后，生成工程 assets 的 package libraries 均为 0。

`job-states.json`、编译输入及绑定清单加入精确诊断白名单。C# 探针失败写入 `result.json`，仅清理已知 SQLite / marker 文件，保留 `process.log`；Node 运行摘要在 finally 留存部分结果。原 Microsoft / SqlSugar 断言、EF 回滚、API recurring registration 和 Hangfire 成功 / 重试职责保持。

两类 SQLite job 接入新增契约；三个 native job 执行实际 MSBuild 参数夹具。工作流 YAML 对照确认，除两处步骤名称外，所有 job 定义、action SHA、输入、矩阵和时限与基线相同；Razor 和原 Native Images job 未改动。每平台 16 份产物独立编译，三平台共 48 次，四 final 再 4 次；冷进程数量仍为三平台 144 个及四 final 12 个 mode，前置夹具另行计数。

## 2. 本地验证

本机使用已有 Node `24.16.0`、.NET SDK `10.0.300`、ARM64 Xcode 工具链；未安装托管方案固定的 Node `24.22.2` / SDK `10.0.403`。本地结果不等于这些固定输入或目标 x64 平台通过。

| 验证 | 结果 |
| --- | --- |
| Node 契约与原门禁 / 漏洞策略回归 | 50 项通过，0 失败 / 跳过 |
| 实际 MSBuild → Exec → Node | macOS 12 组通过；sources / build / publish、普通 / RID、末尾分隔符、空格 / 中文 / 括号 |
| 四宿主原 SQLite 输出门禁 | 全部通过；均为各宿主现有 Release 输出，没有覆盖 bin / obj |
| 四宿主无包探针严格编译 | 全部通过，0 警告 / 错误；引用数量 API 91、Auth 115、Gateway 54、DbMigrate 81；未运行探针 |
| 实际 C / C++ 样本 | 同一 SDK 编译 / 链接通过，Mach-O ARM64；未执行，不计作 Intel 验收 |
| 静态与文档 | 10 个 Node 文件语法、YAML 及旧 job 对照、文档链接 / 文本卫生、敏感字面量、diff 检查通过 |

关键入口：

```bash
node --test Scripts/ci/validate-sqlite-platform.test.mjs \
  Scripts/sqlite/probe-contract.test.mjs Scripts/ci/sqlite-macos-toolchain.test.mjs \
  Scripts/ci/sqlite-container-identity.test.mjs Scripts/check-sqlite-native.test.mjs \
  Scripts/evaluate-image-vulnerabilities.test.mjs
node Scripts/ci/check-sqlite-msbuild-argv.mjs --scratch=<全新目录> --evidence=<证据目录>
node Scripts/run-sqlite-runtime-probe.mjs --compile-from=<宿主现有输出> --host=<宿主名> --scratch=<全新case目录>
npm run check:repo-quality:changed
node Scripts/check-sensitive-literals.mjs
git diff --check
```

探针编译内部执行 `dotnet restore` 与 `dotnet build -c Release --no-restore --warnaserror`，工程无 PackageReference，禁用构建服务器 / node reuse / shared compilation。无需补建宿主输出，没有 solution build / publish、冷探针、Hangfire worker、图片 self-test 或 Docker 运行。

容器本地回归只验证命令参数、目录所有权、失败留痕和拒绝路径编排；模拟回调不视为容器读写证据。SDK 负向契约验证缺失工具 / SDK 和错误架构；探针负向契约验证缺失依赖、不同输出、不同宿主、deps / runtimeconfig / 引用 / 源码 / 探针漂移、未知 mode 以及原生 DLL 不进入编译引用。

## 3. 证据与清理

证据保存在 `artifacts/sqlite-hosted-validation-repair/2026-10-31-local/`，包含最终 Node 结果、12 组 argv / MSBuild 日志、四宿主编译输入 / 清单 / SDK / restore / build / 输出门禁日志、macOS 工具身份与链接日志、源码 hash、YAML 对照和清理报告。摘要与归档 manifest hash 见[结果 JSON](./sqlite-hosted-validation-repair-2026-10-31.json)。证据按白名单校验，不保留编译二进制、数据库或整个 scratch。

全部夹具、编译工程和样本位于本批唯一临时根目录；归档校验后精确移除。只读进程检查没有发现 dotnet / MSBuild / VBCSCompiler / clang 残留；未停止其他任务进程。SDK / Xcode / 系统代理与生产配置未改变。

仓库卫生检查仅提示既有 `validation-baseline.md` 与 records 索引篇幅超过建议上限；本批不做无关文档拆分。入口 `AGENTS.md / CLAUDE.md`、依赖版本 / lockfile、SQLite contract、正式 Dockerfile 与业务代码未修改。

## 4. 剩余缺口与下一步

- 新候选的 Windows 实际 cmd → Node 路径（24 组，含盘符根 / UNC 字符串）、Intel macOS C / C++ 链接、三平台严格构建 / 测试 / publish / 冷进程和图片自检尚未执行。
- 四个真实 AMD64 final 的 uid / gid、读写正反向夹具、12 个冷 mode、API 图片自检和漏洞扫描尚未执行。
- Linux 的历史成功仅为对照。共享 targets / 探针已变化，新候选须完整七项回归，不仅重跑历史失败 job；固定工具、证据 / 清理与时限继续按[托管方案](../features/sqlite-hosted-native-validation-plan.md)执行。
- 本批仅形成本地候选并保存提交。push 和新 SHA 的单次 `sqlite-platforms` dispatch 待所有者另行确认；不重跑旧 run、不自动重试，也不执行 Razor verify、发布或部署。
