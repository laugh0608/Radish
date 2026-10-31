# SQLite 托管执行契约复核与本地修正

> 2026-10-31（Asia/Shanghai）。所有者批准先完成本地成组修正与验证；本轮不安装工具、不启动服务、不 push / dispatch。执行范围继续由[托管方案](../features/sqlite-hosted-native-validation-plan.md)约束。**本地检查通过不等于三个原生平台或四个最终镜像已验收。**

## 1. 复核基线

- 本地修正前 HEAD：`1c65e1df414970e9168120a67522ffea7261912c`。
- 只读核对远端 `dev`：`75a9e33f8a3d1856d1fa0ffaa7f96c091824d456`；本地领先两个文档提交，快进关系成立。
- 默认分支 `master`：`4a8589b6a1c5264506c51429cf7fe470e783f9fc`；`native-images.yml` 工作流 ID `390778640`，state=active；没有开放的 head=dev PR。
- 该工作流查询时共有三次运行，均为已有 Razor inventory；最近一次为 `41193259788`。本轮未增加运行、未重试 inventory、未执行 Razor verify。
- 分支身份通过 GitHub connector 获取；连接器不接受工作流查询端点，改用本机 `gh api` 只读核对工作流 / run / 默认分支，未修改远端状态。

## 2. 发现与修正

| 发现 | 本地修正 | 验证要点 |
| --- | --- | --- |
| SQLite 只比较 checkout 与 run SHA，没有独立检查批准的 SHA | 七个矩阵 job 在安装前要求 expected_head_sha=GITHUB_SHA，固定 checkout；编排器在目录创建前及读取实际 HEAD 后双重检查 | 空值 / 漂移拒绝且不创建目录；工作流接线检查 |
| native 探针成功后才复制日志，失败时 scratch 会被 finally 删除；编译日志也未纳入归档 | 成功 / 失败均按固定文件名保留诊断；最终清理再次收集未完成 case；数据库 / DLL 不进入证据 | 注入 probe exit 7，保留原始日志、部分 JSON 和编译日志，验证清理后仍可读取 |
| 命令只在退出后写 commands.json | 启动前写命令，spawn 后写 PID，close 后写终态；取消与超时操作当前进程树 | 本机真实 Node 子进程退出 23、超时、取消、可执行文件缺失 |
| cleanup.json 在目录删除前就写 completed=true | 删除后核对目录，失败保留 errors；未确认终止时拒绝删除，不按旧 PID 重杀 | 注入删除失败、重入成功及未完成 PID；不伪造清理成功 |
| cleanup 失败导致安全失败证据也不能上传 | 最终白名单通过才输出 publish_safe=true；与步骤成功状态分离 | 安全失败证据可发布，含 Pwd 的合成日志拒绝发布；job 失败状态保留 |
| Windows 只验证 VS C++ 组件存在，没有证据说明实际编译器 | 加载选中实例的 x64 环境白名单，固定实际 cl / link 路径；编译不运行的 C++ 样本，检查 PE x64；Unix 同样记录路径、hash 并核对 ELF / Mach-O | 本地验证白名单、目标架构和路径拒绝；实际工具加载留待对应 runner |
| native 各发布输出缺少独立 notices 哈希留痕 | 每份输出保存非空 notices 清单、size / SHA256 | 缺失、空目录、内容变更及符号链接覆盖 |

Windows 不改回 VS 2022：当前锁文件的 `cc 1.9.0 / find-msvc-tools 0.1.15` 保持，已读取现有本机 crate 源码，发现工具支持 VS 18 / 2026。SQLite 的 C++ 构建要求独立于 Razor / Full MSBuild 要求。本轮没有更新 Cargo.lock 或增加组件下载。

编译身份通过目标专用 CC / CXX 与 Rust linker 环境传给当前任务；当前任务禁用 MSBuild server / node reuse / shared compilation，避免构建服务器跨命令存活。VS 环境只采集 PATH、INCLUDE、LIB、LIBPATH、VCToolsInstallDir、VCToolsVersion、WindowsSdkDir、WindowsSDKVersion、VSCMD_ARG_HOST_ARCH、VSCMD_ARG_TGT_ARCH；不导出完整环境。编译样本只用于架构判断，未设计为运行探针。

验证步骤增加 38 分钟上限，job 保持 45 分钟；工具准备也占总时限。正常退出、超时和可处理信号进入 finally；强制终止 / runner 丢失不保证后处理执行。后续记录必须区分自行清理与依赖托管 VM 生命周期的资源，不能仅凭 job 完成声称观察到 VM 销毁。

## 3. 本地验证

```bash
node --check Scripts/ci/validate-sqlite-platform.mjs
node --check Scripts/ci/validate-sqlite-platform.test.mjs
node --test Scripts/ci/validate-sqlite-platform.test.mjs Scripts/check-sqlite-native.test.mjs Scripts/evaluate-image-vulnerabilities.test.mjs
npm run check:repo-quality:changed
git diff --check
node Scripts/check-sensitive-literals.mjs
```

- Node：38 项通过，0 失败 / 跳过；编排器 19 项、SQLite 原门禁 8 项、漏洞策略 11 项。
- Ruby 自带 YAML 解析通过；与修正前 HEAD 比较，`native-sanitizer / native-platforms / native-docker / hangfire-windows-razor` 四个 job 完全一致；本轮只调整 SQLite 两个矩阵 job 及共享输入说明。
- 文档、文本卫生、敏感字面量与 diff 检查按上述命令完成。原测试中的合成私钥头触发字面量门禁，已改为在测试执行时组装同一夹具，保留原拒绝断言；未放宽扫描规则。记录索引原有篇幅提醒不作为验收失败，也不在本轮扩展历史索引重构。
- 测试使用本机临时目录与短命 Node 子进程，测试后删除；没有假造 Actions 环境运行原生矩阵。cleanup 测试仅针对合成 owner / evidence 目录，不调用 Docker。
- 本机不是三个目标 x64 runner；MSVC 环境加载 / taskkill、Linux / Intel macOS 编译器实际选择、完整 restore / build / publish / probe / scan 尚待远端执行。本地夹具不替代这些证据。

## 4. 下一次执行边界

修正完成后以新的完整提交 SHA 作为候选；上面的 HEAD 仅是修正前审阅基线，不能用于派发修正后的代码。push 前复核最新远端引用和快进关系，push 后复核目标 ref / 工作流 blob，再以 `validation_scope=sqlite-platforms` 和 `expected_head_sha=<批准完整SHA>` 单次 dispatch。

工具与依赖范围沿用专题固定值：runner 内 Node 24.22.2、.NET SDK 10.0.403 / runtime 10.0.14、Rust 1.96.0 minimal + rustfmt / clippy；现有 NuGet 219 项图和 Cargo.lock；固定 digest 的 Linux build / runtime / Trivy 镜像。缺少 C++ / SDK 组件则停止，不临时安装更多组件。不运行 npm install / ci。

验收范围仍为三个原生 x64 平台、双配置、48 份发布、144 个 native 冷进程和 12 次图片自检，以及四个原生 AMD64 最终镜像、12 个镜像探针 mode、API 图片自检与既有漏洞策略。未修复 HIGH 仍按既有策略报告，CRITICAL / 有修复 HIGH 默认阻断，不临时增加例外。

端口不暴露；不启动业务服务器、PG / Redis / Valkey。证据 artifact 保留 14 天，下载后校验 ZIP digest 与内部 manifest，归档日志、平台 / 命令 / 清理事实并形成逐平台结论。失败保留证据；不自动重试、不取消其他任务、不发布或部署。Hangfire 上游询证与 Razor verify 独立等待条件满足。
