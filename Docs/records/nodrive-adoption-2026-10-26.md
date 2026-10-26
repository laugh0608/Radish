# NoDrive 正式收缩与配置边界实施记录（2026-10-26）

> 后续证据修正：同日[Hangfire 来源补证](./hangfire-source-inputs-2026-10-26.md)发现测试 native SQLite 为 3.50.3，而实际 publish 为 3.50.4；本记录中的 3.50.3 引用 / 测试结论不能替代发布原生文件身份核对。

> 方案先以 `571054e8` 提交，项目所有者随后批准精确依赖、配置行为和 PostgreSQL 测试资源。实现基于该提交，在 `dev` 直接推进；新增 SpaProxy 固定版本另获确认。范围与回退见[实施方案](../features/nodrive-adoption-plan.md)。当前代码、严格构建、PG17 双配置全量、PG16 双配置专项与四宿主 publish 已通过，测试资源已清理。结构化计数、输入哈希与清理结果见[证据摘要](./nodrive-adoption-evidence-2026-10-26.json)。

## 1. 实际变更

- Common / Infrastructure 两处由 `SqlSugarCore 5.1.4.207` 切为精确范围 `[5.1.4.207]` 的 NoDrive。
- Common 显式引用 Npgsql / Microsoft.Data.Sqlite `[10.0.0]`、SystemCommon `[8.5.0]`；现有 SQLite 原生版本约束保持。
- `DatabaseProviderPolicy` 统一限定 SQLite / PostgreSQL。业务配置在任何 SQLite 路径处理前校验全部启用连接，分库租户在生成连接配置前校验，OpenIddict / Hangfire 在连接串处理前校验。
- 保留所有历史枚举值与数据库结构；禁用历史业务条目继续忽略，合法 legacy 连接推断保持。移除非正式 provider 的旧明文连接文件读取分支，不实现新的主从行为。
- 错误只标识配置位置、provider 与支持范围，不包含连接串或租户数据库信息；非法文本值也不直接回显。
- 补齐配置、租户、DbMigrate 注册入口与真实驱动契约回归；更新数据库三份指南和框架说明。

首次 restore 发现 API 的 `Microsoft.AspNetCore.SpaProxy` 为 `10.*-*`，尝试解析到 `10.0.14`；停止后经项目所有者明确批准固定为原先解析的 `[10.0.13]`，没有升级 SpaProxy。其余解析变化均属于批准的 NoDrive / 显式驱动链。

## 2. 依赖图与产物

本轮正式 restore 的 19 个项目没有 assets 诊断；package ID + version 并集从 237 变为 226。新[NuGet 清单](./nodrive-nuget-inventory-2026-10-26.csv)记录逐包 metadata、作者、来源、许可文件哈希及四宿主分发范围；原 947 条全生态盘点保持不变，不能将本次仅 NuGet 的数量与其直接比较。

| 变化 | 内容 |
| --- | --- |
| 新增包版本条目 | Microsoft.Data.Sqlite `10.0.0`、SqlSugarCoreNoDrive `5.1.4.207`、SystemCommon `8.5.0` |
| 退出通用包 / 厂商链 | SqlSugarCore、MySqlConnector、Oracle、Oscar、Dm、Kdbndp、PerformanceCounter、DirectoryServices.Protocols |
| 退出重复旧驱动版本 | Npgsql `5.0.18`，Microsoft.Data.Sqlite / Core `9.0.0`，SQLitePCLRaw bundle / core / provider `2.1.10` |
| 明确保留 | SqlClient `5.2.2`、SNI runtime `5.2.0`，不作为 SQL Server 产品支持 |

四宿主均从全新目录 publish，并逐项核对 `.deps.json` 与全部文件。共同的 SqlSugar DLL SHA256 为 `38a361e486a809ee2242861ce5ecb94016dca99bcaa9541d8f54cda8b06e9d0b`，没有误选 SystemCommon 内的同名程序集；PG / SQLite 驱动均为 `10.0.0`。

| 宿主 | deps 中 package 条目 | 文件数 | 总文件字节 |
| --- | ---: | ---: | ---: |
| API | 83 | 378 | 140,204,082 |
| Auth | 112 | 570 | 145,807,661 |
| Gateway | 53 | 91 | 24,428,665 |
| DbMigrate | 74 | 196 | 115,290,901 |

这些是本机 framework-dependent publish，不是镜像层体积或跨平台运行结论。四份产物仍包含 Windows SNI runtime；未删除文件、篡改包缓存或设置 ExcludeAssets 来隐藏依赖。

NuGet 联网漏洞检查覆盖 19 个项目，返回零漏洞条目、零报告问题；不代替 Linux 镜像漏洞 / 操作系统包审计。

## 3. 验证矩阵

环境为 macOS arm64，SDK `10.0.300`；PG 服务使用本机 OrbStack Linux arm64、独立测试卷与合成凭据，串行绑定 `127.0.0.1:15432`。精确镜像沿用方案锁定的 digest，服务实际返回 `17.11 (Debian 17.11-1.pgdg13+2)` 与 `16.15 (Debian 16.15-1.pgdg13+2)`。

| 检查 | 结果 |
| --- | --- |
| Debug / Release 严格构建 | 各 0 warning / error |
| 新增非 PG 定向回归 | 57 / 57 通过，无跳过 |
| PG17 + SQLite 全量 Debug | 4,135 通过、102 预期跳过、0 失败 |
| PG17 + SQLite 全量 Release | 4,135 通过、102 预期跳过、0 失败 |
| PG16 专项 Debug / Release | 各 43 / 43 通过，无跳过 |
| 四宿主 Release publish | 通过，图 / 文件 / SqlSugar 哈希已核对 |
| native Debug / Release 输入校验 | 通过，未重建 native |
| 文档 / 变更文本卫生 / diff 检查 | 通过；configuration.md 保留既有软篇幅提醒，未超过硬上限 |
| 证据一致性 | 候选源码、逐包材料与归档哈希、测试计数和 PG 用例对应关系均通过复核 |

新测试合计 58 项：55 项配置与租户边界，3 项驱动契约（实际 SqlSugar DLL、SQLite 参数化 Int64、PostgreSQL 参数化 Int64）。DbMigrate 入口测试使用纯内存配置，确认非法 provider 在注册连接前失败。

102 项跳过均为上传配额 Redis / Valkey 集成测试；本轮未授权启动这两种服务，显式关闭该测试开关，不把它们算作通过。43 项 PostgreSQL 用例已在 PG16 / PG17 的 Debug / Release 四轮逐名对照，全部通过，包含本轮新增驱动探针；没有用旧计数代替当前执行证据。

过程修正与权限边界：

1. 首次还原因默认 NuGet HTTP 缓存目录的沙盒写权限失败；使用本批临时 HTTP 缓存并按原范围提权重试成功，包仍位于仓库缓存。
2. 新增 DbMigrate 测试初次缺少 `InternalApp` 配置初始化，定向结果为 56 通过 / 1 失败；补齐夹具并恢复静态状态后重新构建，57 项通过，未修改产品行为来适应错误夹具。
3. PG17 全量首次在沙盒中因 macOS 临时钥匙串失败，主动中止测试并清理该轮资源，保留原失败日志；随后相同候选与测试范围提权重跑通过。没有调整系统证书信任或放宽断言。

## 4. 分发材料与未关闭项

已整理 13 个重点组件和三份字体许可的审查材料包，逐项来源与哈希见[材料清单](./nodrive-distribution-materials-2026-10-26.json)：

- Hangfire 三个核心包、PostgreSQL / SQLite 插件分别保存各自 nuspec 和许可 / NOTICES；包内商业许可文本仅作来源证据，不表示已取得商业授权。
- Argon2 / Blake2b / SecureArray、SNI runtime 与 VS Container Tools 均保留原始文件和逐包 metadata；没有替换密码算法、调度实现或删除 IDE 工具。
- Mapperly 的 LICENSE 从 nuspec 指向的[精确 commit](https://github.com/riok/mapperly/blob/036698914bd48be888b25322938b565db72ff7ff/LICENSE)补齐，保存内容的 Git blob 与上游返回值相同。
- NoDrive / SystemCommon 已保存 nuspec 的作者 / copyright 和其指向的[Apache-2.0 正文](https://www.apache.org/licenses/LICENSE-2.0.txt)。包未提供源码 commit，精确源码对应关系及上游 NOTICE 仍需补证；不把上层声明覆盖到全部传递包。
- API publish 的 native 字体许可沿既有接线交付；Flutter 两份 OFL 资源声明未改。本轮没有构建 Flutter 安装包。

**材料包尚未接入四宿主的通用 publish。** 此次四份新产物证明重点 NuGet 包正文未由公共构建接线统一带入，不能将缓存或审查包称为发行材料已完整。下一批需要确认原文保存位置、各宿主 / 构建工具的分发映射、MSBuild 接线及缺失材料失败策略；按已批准方案，该接线另列可审查范围。

## 5. 证据与后续边界

本机目录：`/private/tmp/radish-nodrive-adoption.unNhWX`。其中 evidence 保存前后 assets、审计、构建 / 测试日志、TRX、命令、产物文件清单和失败重试记录；publish 保存四宿主候选；`dependency-materials-2026-10-26.zip` 保存原文审查包。临时目录不是永久远程归档，仓库保留摘要、哈希与逐包清单。

PG17 / PG16 容器与卷均已按归属标签清理，15432 无监听，两库结束时仅剩默认 public schema；镜像按方案保留在共享缓存。不删除用户包缓存或其他任务资源。

NoDrive 的本地实现与技术验证不等于整个依赖专题或发布门禁关闭。当前未修改 Compose、切换 Valkey、访问生产、启动产品宿主、执行浏览器 Smoke、创建 PR / tag 或发布；准备集成时仍须对最终候选执行批次门禁，随后按既定顺位恢复日志 L2 与 P8-C。
