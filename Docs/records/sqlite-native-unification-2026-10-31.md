# SQLite 原生依赖统一实施与本机验证

> 2026-10-31（Asia/Shanghai）。项目所有者先批准[统一方案](../features/sqlite-native-unification-plan.md)、要求提交工作区，再批准依赖 / 门禁 / 材料与本机验证；方案及只读证据已提交为 `5c676296`。随后单独批准 Auth / Gateway / DbMigrate 的 Node 构建阶段接线。本记录关闭本机开发批次，不关闭其他平台、最终镜像或发行审核。

机器可读结果见[本批证据](./sqlite-native-unification-evidence-2026-10-31.json)，调查前状态见[只读快照](./sqlite-native-chain-inspection-2026-10-31.json)。未启动 API / Auth / Gateway 项目服务、容器或 VM，未连接 PostgreSQL / Redis / Valkey，也未访问生产、推送、创建 PR / tag、发布或部署。

## 1. 实现与精确输入

- Common 改用 `Microsoft.Data.Sqlite.Core [10.0.0]`、`SQLitePCLRaw.config.e_sqlite3 [3.0.3]` 与唯一 native `SQLitePCLRaw.lib.e_sqlite3 [3.53.3]`，正常向消费者传递。
- Auth、Auth.Persistence、Auth.Migrations.Sqlite 三处 EF 引用改为 `.Sqlite.Core [10.0.0]`。初版方案表漏列 Auth 本身的同版本元包引用，首次 restore 发现残留 bundle 后补齐同类替换；未升级 EF、改变 migration 模型或扩大 provider 支持。
- 删除全局逐项目 native 覆盖与 Extension 的 bundle 引用。19 个项目图中 SourceGear、bundle_e_sqlite3 与两个 Microsoft SQLite 元包均已退出；17 个消费图发生预期变化，最终共 **219 个不同 package/version 条目**，只统计 .NET 本轮图，不覆盖全生态。
- 托管 core / provider / config 统一为 3.0.3；Hangfire Storage.SQLite 0.4.2、sqlite-net-pcl 1.8.116、bundle_green 2.0.4、dynamic_cdecl 2.0.4 保持。green 的重名 batteries 候选仍存在，实际复制 / 加载均匹配 config 3.0.3，不宣称所有旧托管包已移除。
- 既有 `Microsoft.Data.Sqlite.dll` 10.0.0、SqlSugar、Npgsql 和应用 provider 边界不变；未改宿主初始化顺序、业务 schema、API、根 LICENSE、Compose、Rust ABI 或 Web / Flutter 依赖。

普通 restore 与本机 `-r osx-arm64` restore 均通过；逐项目比较后，RID 还原没有改变普通图的 package/version 集合。未安装新工具链。NuGet 当前直接 / 传递依赖审计无漏洞条目；最后一次审计的默认用户缓存写入受沙盒阻止，改用本批 `NUGET_HTTP_CACHE_PATH` 后成功，不以失败结果放行。

## 2. 限定门禁与分发接线

`Scripts/check-sqlite-native.mjs` / `Scripts/sqlite-native-contract.json` 与 MSBuild 接线执行两类检查：

1. SDK 冲突消解前读取原始 assets，拒绝第二 native 来源、退出包返回、精确版本漂移、未知路径或源文件 hash 不符；不依赖 SDK 最后选中的文件。
2. 四宿主 / Tests build、四宿主 publish 后对实际输出和 deps 校验。通用输出检查本次根目录及 runtimes 子树，嵌套的 RID / publish 输出由各自检查；RID 输出校验根目录 native。managed batteries / core / provider 与 Microsoft 驱动同时核对。

没有加入 ExcludeAssets、复制优先级、SDK 文件修改或 NoWarn。首次构建真实拦截旧 `iossimulator-x86` 静态文件残留，标准 `dotnet clean` 后重建成功；未选择性删 DLL 或改写 NuGet 缓存。MSBuild 实际负例分别注入第二来源图与损坏的临时 native 副本，均按预期失败。

Node / notices 自测共 **25 项通过**。三个新增 Docker 接线仅复用 `node:24-bookworm-slim` 并复制 Node 到 build 阶段，最终运行阶段不增加 Node；本轮没有执行 Docker 构建或容器验证。

新增六个精确组件的许可 / attribution；Apache 管理层同时保留 NOTICE。九份原始材料的 hash 及规范化关系已校验，复用既有生成器和 MSBuild 材料门禁：

| 宿主 | runtime notices 组件 | 实际文件（含索引 / manifest） |
| --- | ---: | ---: |
| API | 17 | 57 |
| Auth | 12 | 31 |
| Gateway | 11 | 29 |
| DbMigrate | 12 | 31 |

仓库 catalog 共 19 组件（17 runtime / 2 build-only）。所有普通 / RID、Debug / Release、normal / no-build 发布目录的材料路径、数量及 hash 均匹配。历史 226 条清单与 13 组件初批材料口径保留，不倒改旧证据。

## 3. 构建、测试和实际产物

SDK 为 `10.0.300`。普通解决方案、显式 osx-arm64 的 Tests 项目及其四宿主引用均完成 Debug / Release `--no-restore --warnaserror` 构建，均为 **0 warning / 0 error**。SDK 不支持 solution 级 RID build，首次 NETSDK1134 后改用项目级入口；未更改 SDK 或依赖。

| 验证 | 结果 |
| --- | --- |
| Debug 后端全量 | 4,239 总数，**4,094 passed / 145 skipped / 0 failed** |
| Release 后端全量 | 4,239 总数，**4,094 passed / 145 skipped / 0 failed** |
| 两配置 skip 分类 | PostgreSQL 43；未启用 Redis / Valkey 隔离集成 102；不计为服务验证通过 |
| 新增 SQLite 回归 | 两配置均通过 native version / source ID、Int64 / Unicode / NULL、WAL 读快照、事务回滚、checkpoint、重开与 integrity_check |
| build 文件检查 | 普通 / RID × 两配置 × 四宿主与 Tests = **20 组** |
| publish 文件检查 | 普通 / RID × 两配置 × 四宿主 × normal / no-build = **32 组** |
| 冷进程加载 | 上述 52 份独立输出，共 **160 组首用路径**，全部通过 |
| Hangfire SQLite | **16 组**真实 storage 验证，各核对 16 项周期注册并完成探针成功与失败后重试 |
| 旧 native 数据兼容 | 3.50.3 / 3.50.4 各自创建临时 WAL 夹具 → 3.53.3 读写 / 回滚 / 提交 → 原版本重开及完整性检查，共 6 阶段通过 |

通用 net10.0 图实际选择 44 项 native runtime 文件；包还含未被该 TFM 选择的 net8.0 WASM 静态资产，不将包总文件数当作输出数。osx-arm64 RID 输出实际为 1 份 native。各输出的全部相关文件均留 path / 来源 / SHA256，机器证据按四组相同文件集合去重保存。

本机所有实际加载的 native 均为 **SQLite 3.53.3**，SHA256：

```text
09fe20a6c050912a7944b2f2f2981debe31679f7919f523c87251ae8cf0f2daa
```

source ID 均为 `2026-06-26 20:14:12 d4c0e51e4aeb96955b99185ab9cde75c339e2c29c3f3f12428d364a10d782c62`。同 RID 下普通 / RID、Debug / Release、build / publish、normal / no-build 的动态库身份一致，消除了原来测试 3.50.3 / publish 3.50.4 的差异。

## 4. 运行测量和诊断事实

- 探针临时放在原输出旁，以各宿主自己的 deps / runtimeconfig 运行，不启动宿主。先分别使用 Microsoft、SqlSugar、可用的 EF 或 Hangfire，再读取模块实际路径、hash、raw version / source ID 和托管程序集身份；模块必须来自相应输出，不接受 NuGet 缓存路径充当交付证据。
- macOS 的 `Process.Modules` 未列出 dylib，改用系统 dyld 已加载镜像表读取路径，不额外加载一个候选库来反推宿主。其他平台尚未执行，不扩展为平台 Go。
- 初版手工 csc 参照 .NET 8 依赖与 .NET 10 reference pack 时出现 CS1701；最终使用无 PackageReference 的临时 SDK 工程，沿用正常 SDK 框架引用与默认编译策略，严格构建零警告。未新增 NoWarn 或修改项目警告规则。
- Hangfire 首版探针只在创建作业时指定队列，重试回到 default 而未执行；为测试作业补充专用 Queue 属性后，16 组最终验证均成功。应用队列 / 重试策略未改；全部周期业务注册在启动探针 worker 前移除，不执行真实清理任务。
- 沙盒全量运行中的既有临时自签名证书夹具报告 Keychain 不存在；按验证规则提权，不修改系统信任或证书设置。初期中止 / 诊断运行与一次取消的重复运行不计为通过，**只采用有完整 TRX 和完成摘要的 Debug / Release 结果**。
- 最终两配置全量各约 9 分钟。TRX 确认既有 1000 轮图片压力用例完整通过，Debug 38.4 秒、Release 8.6 秒；整体时长还包含大量串行日志回归，不能把全部耗时归因于图片或 SQLite。中间采样 / 长测试诊断不是失败证据，也没有为缩短时间删减测试。

## 5. 清理、材料与剩余边界

最终冷进程探针退出，临时数据库、WAL / SHM、执行标记和暂存到宿主输出的探针 DLL 均已清理。候选输出、逐文件报告、两份完整 TRX、源材料、诊断日志和探针编译输入在机器证据目录保留，便于复核；记录中的本机路径不作为对外发行入口。

本机审阅材料只服务本批验证，不是完整 Radish 运行包或 Hangfire Corresponding Source 发行包。新的 3.53.3 基线也不追溯替代 10 月 26 日 Core 源码替换试验的旧 native 身份；后续对应源码工作应明确采用哪个候选重新验证。

仍未关闭：其他四个正式 .NET 服务平台的产物 / 运行、Linux 双架构最终镜像、PostgreSQL / Redis / Valkey 服务集成，以及 Hangfire 完整对应源码、NoDrive 询证、SNI 平台分发和全生态材料。版本候选保持 26.8.2，本轮不合并、发布或部署。

最终文档、本地链接、changed-only 文本卫生、敏感字面量、Repo Quality 契约、notices 及 `git diff --check` 均通过；仅保留验证基线与 records 索引的既有建议篇幅提醒，未借本批扩展文档重构。
