# Windows Razor 本地工具与限定 CI 接线

> 2026-10-31（Asia/Shanghai）。[方案](../features/hangfire-windows-razor-validation-plan.md)获批后，先以 `35d22fe2` 提交准备材料，再实施本地工具 / CI 接线与契约检查。**本地检查通过，Windows 实际库存和 Razor 矩阵未执行。** 结果见[JSON](./hangfire-windows-razor-local-implementation-2026-10-31.json)。

## 实现与边界

新增 `Scripts/hangfire/windows/` 独立入口，不放宽旧 macOS Task 平台边界；正式应用、默认 SDK、219 项图和 ThirdParty 不改。

| 范围 | 实现 |
| --- | --- |
| 精确输入 | 固定 SDK 10.0.403 win-x64 ZIP、11 项包的 archive / contentHash、两仓 commit / tree、23 对模板与 LF / CRLF 派生 hash、24 个包内 DLL；补充 Newtonsoft 13.0.4 的实际 net6.0 DLL hash |
| 库存与启动门禁 | Inventory 只读；Verify 在任何输入获取 / 上游 task 运行前核对四个已审阅工具值。两者都要求完整 run SHA，一律限定 native Windows x64 的独立 RUNNER_TEMP 根 |
| 子进程 | ArgumentList 传参；清空继承环境后只列 OS 必需项和私有 CLI / cache 设置，避免 Actions token 或业务配置进入 MSBuild / binlog；关闭节点复用、共享编译、遥测和开发证书生成 |
| 源侧 | 在私有副本选择 netstandard2.0，移除显式旧分析器 / SourceLink，Core JSON 13.0.4 和 JsonStats 等价适配；保留源文件 BOM / 换行和原锁 / diff，不新增 NoWarn |
| Razor | 删除 23 个旧输出后直接调用 Full MSBuild 原始 Razor import；自有 wrapper / 只读程序集 task 记录实际 CLR、引擎路径 / hash，缺报告或旧输出不能通过 |
| 编译与证明 | 两根目录、四类 Core 的严格 restore / locked / build / merge；原包 API / 资源摘要、实际 PDB / SourceLink / 嵌入源码，以及同组 DLL / C# 字节重放门禁 |
| C# 对照 | SDK Roslyn 解析有效 token / ValueText；仅折叠保持值不变的字符串常量拼接，处理 CodeDom 长字符串换行。字符串内容、数值和标识符漂移仍失败 |
| 片段探针 | 无 PackageReference；真正创建 ErrorAlert 并执行 RazorPage.ToString，核对 Core / Newtonsoft 路径与 hash、HTML / 模板标记；没有 DI 宿主、端口或数据库 |
| 清理 / 留痕 | owner / PID / CreationTime / executable 约束，阻止 junction / symlink 越界，终止本任务进程后删除 work；成功 / 失败均整理受限证据与 hash manifest，拒绝产品 DLL / PDB、SDK、nupkg 与 cache 上传 |

C# CLR4 快照 task 用 RoslynCodeTaskFactory 的 Class / Source 形式，依据 [MSBuild 官方说明](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-roslyncodetaskfactory?view=vs-2022)。本机仅编译其源码，不将 .NET 10 编译通过推定为 Windows CLR4 实际绑定通过。

## CI 范围

`.github/workflows/native-images.yml` 只增加一个 `hangfire-windows-razor` job 和 Razor 专用输入。新 job 仅在手动 `validation_scope=hangfire-razor` 时运行，默认 inventory，`contents: read`，checkout 固定本次 run SHA 且不持久化凭据；依赖 actions 使用冻结 SHA。

原 native-sanitizer、native-platforms、native-docker、sqlite-native-platforms、sqlite-native-docker 五个 job 的 YAML 对象摘要均与准备提交一致。默认 native-images、原 SQLite 选择、workflow_call 行为不变，Razor 不因 push / PR / workflow_call 顺带运行。

失败也执行 cleanup，并仅在证据白名单 / manifest 校验后上传；清理失败仍令 job 失败，安全证据可保留。artifact 保留 7 天，待上传证据最后由 GitHub 销毁 VM 清除。本轮未上传任何 artifact。

## 本地验证结果

```bash
node --test Scripts/hangfire/windows/contracts.test.mjs Scripts/ci/validate-sqlite-platform.test.mjs Scripts/hangfire/toolchain.test.mjs
node Scripts/hangfire/windows/check-local.mjs
npm run check:repo-quality:changed
git diff --check
```

- 25 项 Node 契约通过：新增 Razor 8 项、既有 SQLite 9 项、既有 Hangfire 8 项；包含 YAML 解析、旧五 job 结构对照、手动范围、身份参数、失败上传白名单和进程边界静态检查。
- 使用现有 SDK `10.0.300`，在临时目录 / 空 NuGet feed 下严格构建自有 Tools 和 Debug / Release Probe，零 warning / error；不安装 SDK 或包，未执行项目服务。
- 11 组 C# 契约通过，覆盖 token / 字符串换行 / 常量拼接、非法语法、精确源侧编辑、BOM / CRLF、缺 / 多生成文件、公开契约及 PDB 路径逃逸拒绝。
- 11 个已保存 nupkg 的 archive SHA256 与 SDK NuGet contentHash 匹配；两份本地 bundle 冻结源码的适配器执行通过；23 对既有文件核对与 CRLF 派生 hash 通过。
- 从既有缓存读取**原 Core**，Debug / Release 两个冷进程均实际渲染 ErrorAlert，Core / Newtonsoft 的路径 / hash 和 HTML 契约通过。该结果只验证自有探针及本机原包，不是 Windows 新 Core 的验证结果。
- 四类真实拒绝通过：未执行生成导致缺产物、Core hash 错误、模板 marker 缺失、Newtonsoft hash 错误；被拒绝的渲染不写成功报告。

早期本地编译发现可空实参与两个 JsonSerializer 类型歧义，均在源代码中修正并重跑通过，没有加入诊断豁免。相关失败日志保留，未计入最终通过结果。

## 证据与清理

最终证据为 `artifacts/hangfire-windows-local/radish-windows-razor-local-y173mC`，精确 manifest hash 见结果 JSON；包含命令 / 退出码、11 组自测、包身份、适配 / 23 对关系、原 Core 元数据及两组实际渲染。仅留本地审阅证据，未装入正式发行包。

临时源码、SDK helper 的 bin / obj / CLI home / cache、探针 case 均已删除，包括早期两份手工检查目录；其编译诊断另行保留。本任务 dotnet / 编译器 / 探针进程表核对残留为 0，全局 SDK 仍为 10.0.300，原上批归档与准备证据保留。

## 尚未执行

本机没有 pwsh / Windows Full MSBuild，未执行 PowerShell parser / 运行验证、CLR4 task factory 或实际 Windows 工具库存；PowerShell 边界目前只有代码审查和 Node 静态契约检查。未来 23 对真实再生成、两根目录的 8 次 Core / 2 次 Cronos 构建、PDB 实际映射及 16 组 Windows 冷渲染仍待 runner。

下一步先按指定提交分别确认 push 和 inventory 手动运行，取得实际 image / VS / MSBuild 精确值，再确认 verify 与证据上传。本地 CI 接线不授权这些远端操作；未 push、dispatch、发布、部署，不关闭原插件 DLL 源码绑定、正式源码入口或完整发行审查。
