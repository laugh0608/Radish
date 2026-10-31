# Hangfire 隔离重建工具

本工具对应 [实施方案](../../Docs/features/hangfire-rebuild-delivery-plan.md)，只服务已批准的 macOS ARM64 本地重建批次，不接入默认 build / CI。正式应用依赖、SDK 策略和分发文件不变。

`inputs.json` 固定三个源码 commit / tree、SDK `10.0.403` 官方 archive / SHA512、42 项私有构建输入、原始四 DLL hash 及 PG 镜像。获取、安装、数据库运行需要遵守当前任务授权，脚本不会代替授权。

```bash
hfRun="${TMPDIR%/}/radish-hangfire-source-20261031-c9e4"
node Scripts/hangfire/rebuild.mjs --phase=acquire --task-root="$hfRun"
node Scripts/hangfire/rebuild.mjs --phase=build --task-root="$hfRun"
node Scripts/hangfire/rebuild.mjs --phase=verify --task-root="$hfRun"
node Scripts/hangfire/rebuild.mjs --phase=archive --task-root="$hfRun"
node Scripts/hangfire/rebuild.mjs --phase=cleanup --task-root="$hfRun"
```

按序显式执行；新任务根必须不存在且名称匹配契约。仅 acquire 接受 `--resume=true`；失败后保留日志，不能重跑其他阶段来覆盖诊断结果。后续阶段从 owner marker 读取状态，未知参数、包版本、公共接口 / 资源、源码映射或产物重放差异均失败。默认获取使用仓库内准备证据；也可在 acquire 指定 `--input-bundle=<本地归档>`，核对三个 bundle 及完整 feed 后取用封存输入。SDK 仍从官方 URL 取得。

- `acquire.mjs`：获取 / 校验 Git bundles、私有 SDK 和包文件；日志记录完整命令、PID、退出码、超时和输入 hash。
- `build.mjs`、`patches/`：五个选择目标严格 restore / locked restore / build；保留审计和分析器。SDK 内置 SourceLink 使用与 PathMap 一致的虚拟根，修改源码嵌入 PDB。
- `Metadata/`：无 PackageReference 的元数据读取器；不加载执行被检查程序集。读取公共契约、资源、方法 IL、PDB / SourceLink 和 NuGet contentHash；相邻 PDB 须匹配 PE 身份及 checksum。
- `verify.mjs`、`RuntimeProbe.cs`：隔离正式应用基线，执行三类 DLL × 双配置 × 三 storage 的 18 组矩阵。仅 PG 临时监听 `127.0.0.1:15432`，Dashboard 在内存处理；不启动业务宿主。
- `replay.mjs`：新源码目录和新包缓存重放五个目标；要求 DLL 字节、接口、资源和方法 IL 一致，逐文件核对 PDB checksum / 映射 / 修改源码嵌入。
- `archive.mjs`、`cleanup.mjs`：先保留本地审阅材料并校验索引，再删除本任务 SDK、源码、缓存和临时数据；仅按 ID 与标签删除本任务容器 / 卷，不删除共享镜像。

无需安装依赖的定向测试：

```bash
node --test Scripts/hangfire/toolchain.test.mjs
```

本机重建成功不等于与原包字节一致；Windows Razor 再生成、原 PostgreSql DLL 的源码绑定、正式源码获取入口及完整发行结论仍需独立处理。归档内的 DLL / PDB 只供本地审阅，不成为正式产品依赖。

Windows Razor 使用独立的 [Windows 工具](./windows/README.md)和[分段方案](../../Docs/features/hangfire-windows-razor-validation-plan.md)，旧 macOS 入口不负责 Windows 执行。
