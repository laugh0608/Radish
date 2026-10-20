# 2026-10-20 第三方依赖许可盘点

> 本页记录本次审计事实，不是所有版本 / 部署方式的许可放行证明。长期方案见[依赖许可治理专题](../features/dependency-license-governance.md)，图片细节见[Rust 替代评估](../features/rust-image-processing-replacement-assessment.md)。

## 1. 范围与状态

- 项目所有者要求统计项目内涉及 key、商业授权或特别许可条件的依赖，建立专门替代专题，并核对已有 Rust 图片实现。
- 本轮只读取 manifests、已有解析图、缓存中的精确包 LICENSE / EULA、源码和官方来源，并编写文档；没有 restore、安装 / 升级包、构建 native、启动服务、访问生产、配置 Secret、申请许可或创建 PR。
- HEAD 为 `9b17484d`；工作区保留[合并前检查](./master-pr-regression-2026-10-20.md)已授权但未提交的 NuGet 安全更新和图片 API 适配。以下 NuGet 数字基于该工作区已还原的候选图，不把它说成 HEAD / master 已采用的新版本。
- [逐包 CSV](./dependency-license-inventory-2026-10-20.csv)保留生态、名称、版本、直接 / 传递口径、许可归类、来源与文件 SHA256，不包含许可证 key、缓存绝对路径或本机业务配置。

## 2. 统计口径

| 范围 | 条目数 | 来源与限制 |
| --- | ---: | --- |
| .NET / NuGet | 241 | 19 个项目现有 `obj/project.assets.json` 的 package 并集，按 ID + version 去重；52 个有直接 / 继承的 restore 引用，189 个为传递依赖 |
| Web / npm | 481 | 根 `package-lock.json` 的外部 package 路径条目；含开发、可选平台包和嵌套路径，不包含 workspaces link 本身；不是 481 个生产运行包 |
| Rust | 154 | `Lib/radish.lib/Cargo.lock` 的 registry 包，排除自有 crate；6 个直接、148 个 lock 传递项，包含目标 / 可选 feature 的上集，不代表全部进入每个动态库 |
| Dart / Flutter | 71 | `pubspec.lock` 与现有 `package_config` 对照；6 个 direct main、2 个 direct dev、63 个 transitive，含 SDK 包 |
| 合计 | **947** | 四种不同统计口径的依赖条目；不能当成跨生态去重的软件产品数量 |

补充范围：

- 根、各项目 csproj 和 `Directory.Build.props` 直接声明也已核对；csproj 中有 51 个不同 ID / version 声明组合，restore 还包括共享 props 等输入，因此不以 51 代替 52。
- SDK / 包内 LICENSE 文件、字体和服务镜像单独复核；它们不是上述包清单之外“默认没许可”。
- Flutter 缓存中两个 SDK 子包未单独携带 LICENSE，本次向 SDK 根 LICENSE 追溯，归入 BSD-3-Clause；没有把文件缺席直接归为专有软件。
- Tauri 已弃用；只读检查其自有 manifest，直接项为 serde、serde_json、tauri、三个 tauri plugin、url 及 tauri-build，未发现新增厂商 key 型依赖线索。历史 `Clients/radish-tauri/Cargo.lock` 的全部传递二进制未纳入本轮 947，也不恢复为正式门禁。
- 未生成目标平台的 publish / Docker SBOM；NuGet 的 `runtimes`、Rust codec、Flutter Gradle / CocoaPods、操作系统与基础镜像的全部底层组件不是本次逐包 metadata 盘点的完整闭包。尤其当前无 iOS / macOS `Podfile.lock` 可审，平台精确发布候选仍需补原生解析图和 notices。

## 3. NuGet：需特别说明的 20 项

| 归类 | 数量 | 精确包 |
| --- | ---: | --- |
| 条件许可与 key 构建边界 | 4 | SixLabors.ImageSharp `4.1.3`、Drawing `3.1.3`、Fonts `3.2.0`、PolygonClipper `1.0.2` |
| 互惠 / 商业双许可 | 1 | AutoMapper `16.1.1` |
| LGPL / 双许可分发复核 | 4 | Hangfire.AspNetCore / Core / NetCore `1.8.14`，Hangfire.PostgreSql `1.20.12` |
| 额外署名义务 | 3 | Isopoh.Cryptography.Argon2 / Blake2b / SecureArray `2.0.0`，包内为 CC-BY-4.0 |
| 厂商专门条款 | 3 | Oracle.ManagedDataAccess.Core `23.8.0`、Microsoft.Data.SqlClient.SNI.runtime `5.2.0`、Microsoft.VisualStudio.Azure.Containers.Tools.Targets `1.23.0` |
| metadata URL / 内容范围复核 | 3 | SqlSugarCore `5.1.4.207`、SqlSugarCore.Dm `8.8.1`、SqlSugarCore.Kdbndp `9.3.7.905` |
| 缺少可直接采用的包内许可信息 | 2 | Oscar.Data.SqlClient `4.0.4`、SQLitePCLRaw.lib.e_sqlite3 `3.50.3` |

其余 221 项在本次包内文件 / 元数据层面归入常规声明范围；这不自动覆盖其所有 vendor native assets 和部署方式。几个容易误判的结果：

- **Six Labors**：四个精确包携带 Split License 1.0，列有开源 / Source Available、传递引用及组织 / 收入等条件分支。Radish 根许可为 Source-Available，因此不能断言本项目必然需要购买商业许可；本次候选 Drawing 的严格构建确实出现 key 拦截，适用条款与技术门禁要分开处理。未通过修改 target 或关闭 warning 绕过。
- **AutoMapper**：精确版本的 LICENSE 为 RPL-1.5 / 厂商协议选择；缺 key 仅日志执行方式不代表项目获得任意使用授权。旧“开源项目通常使用免费版”文字已校准，未改运行时。
- **Hangfire**：允许在适用 LGPL 条件下使用；不因可选商业许可就认定必须订阅。PostgreSql 插件有独立 LGPL 许可。`Hangfire.Storage.SQLite 0.4.2` 则为 MIT；未发现 Hangfire.Pro / Ace。
- **Oracle**：包内明确提供免费分发 / 托管 / 使用条款及条件，不能写成“Oracle 驱动必收费”。它经 SqlSugar 进入图，不是项目启用了 Oracle 的证据。
- **SqlSugar**：现有包 metadata 的许可 URL 指向 Apache-2.0，与前端人工展示的 SqlSugar MIT 标签存在需要核对的粒度差异。不能把上游 ORM 的一个标签复制给所有传递厂商驱动；本轮只登记，不修改产品展示。
- **SQLite**：`SourceGear.sqlite3 3.50.4.5` 包内声明 SQLite Public Domain；`sqlite-net-pcl 1.8.116` 为 MIT；`SQLitePCLRaw.lib.e_sqlite3` 缺元数据许可不等于证明其收费，需要补 native binary 的来源和声明。
- **SNI**：SQL Server native SNI 的包内是 Microsoft 专门条款，包含二进制分发条件；不能只引用 MIT 的托管客户端许可。

## 4. 其余生态

### 4.1 npm

- 468 项归入常规声明范围；12 项为 `lightningcss 1.31.1` 及可选平台包的 MPL-2.0；1 项为 `caniuse-lite 1.0.30001769` 的 CC-BY-4.0。
- 上述 13 项在 lock 中均标记开发依赖；不自动等于用户浏览器下载了这些库的可执行代码，也不免除分发构建工具或数据时的义务。
- 本轮 lock 元数据未发现需要厂商 key / 订阅激活的 npm 包，也没有缺 license 字段的外部条目；该结论不是逐文件 vendored 代码 / 字体的全面许可证明。

### 4.2 Rust

- 核心三包：`image 0.25.10 = MIT OR Apache-2.0`，`imageproc 0.25.1 = MIT`，`ab_glyph 0.2.32 = Apache-2.0`。
- 154 项中 148 项归入常规声明范围；`r-efi 5.3.0 / 6.0.0` 两项为 `MIT OR Apache-2.0 OR LGPL-2.1-or-later`，是选择关系，不自动要求选择 LGPL。
- `libfuzzer-sys 0.4.13` 上游 README 区分 wrapper 的 MIT / Apache 与随附 libfuzzer 的 NCSA；作为一项 embedded notices 复核，不把 wrapper 标签覆盖整个包，也不推定 fuzz runtime 进入产品。
- 本地缺缓存的 `arbitrary 1.4.2`、`crunchy 0.2.4`、`raw-cpuid 11.6.0` 已由其精确 docs.rs 内容补证据。
- 下列 **3 项仍缺精确版本许可正文 / 元数据证据**：`interpolate_name 0.2.4`、`js-sys 0.3.104`、`wasi 0.11.1+wasi-snapshot-preview1`。本机 registry 未缓存，官方页面部分访问失败；不使用其他版本或非权威聚合站替代精确结论。CSV 保持 UNKNOWN。
- **Cargo.lock 当前被忽略且未提交**，此清单是本机快照，不能作为后续 clean build 的锁定承诺；依赖 / feature 冻结是原生替代实施前条件。

### 4.3 Flutter / 字体 / 服务

- 71 个 Dart / SDK 包的本地许可均可追溯至 BSD / MIT / Apache，未发现包级厂商 key；原生插件的系统 / 平台库仍按精确平台候选补闭包。
- Flutter 本地 Noto Sans SC / Noto Serif SC 已有 OFL 文本并在 pubspec 中打包；不是收费字体 key 问题。
- Rust 嵌入 DejaVu Sans，当前 README 链接到 Bitstream Vera / Public Domain 说明，但仍需让原许可 / notices 随原生产物交付。已移除将系统 Arial 改名替换的建议；字体换名不能证明相同字形或可再分发。
- Compose 的 `redis:7` 是版本浮动入口，7.2 与 7.4 的许可不同；目前仅记录源码声明，不访问生产推定真实版本。`StackExchange.Redis` 的 MIT 不能证明 Redis 服务镜像为 BSD。Valkey 是待比较候选，尚未启动或迁移。

## 5. 审计缺口与统计边界

本次明确的 metadata / 精确许可缺口共 **5 项**：2 个 NuGet + 3 个 Cargo；其中 Oscar 应优先补分发权证据，SQLite 应核对 native 来源，三个 Cargo 项在进入正式原生候选前补齐。另有 SqlSugar 3 项 URL / 驱动内容复核，不并入这 5 个 UNKNOWN。

包清单之外保留以下后置验证：

1. 正式 Release / publish / Docker 与 Flutter 平台原生产物的实际闭包及第三方 notices。
2. Rust 允许格式 / feature 与 codec 选择；不是 Cargo.lock 全部内容都进入 binary，也不是未选 feature 的许可证不存在。
3. Redis 精确版本 / digest / 许可及候选替代兼容性。
4. Radish Source-Available 的分发边界与第三方互惠许可路径；未通过改写项目根 LICENSE 消除问题。

本次结论为“已建立可复核的首轮清单与替代专题”，不是“全部第三方依赖许可已通过”。

## 6. 证据与复核方式

- NuGet：汇总现有 `*/obj/project.assets.json`，读取 `.nuget/packages/<id>/<version>/<id>.nuspec` 与其引用的 license 文件；CSV 中的 scopes 为项目名并集，不是线上启用次数。
- npm：按 `package-lock.json.packages` 输出外部路径条目、`license / version / dev / link`；`direct-name` 仅表示名称被 workspace manifest 直接声明，不代表每个嵌套版本都被直接引用。
- Cargo：解析本机 lock 和已缓存的精确 `Cargo.toml`；缺缓存项只用精确发布页 / 源码补证据。`/` 形式历史许可表达式保留原样，不擅自改为 AND。
- Flutter：对照 lock 与 `package_config` 定位缓存中的 LICENSE，读取 SDK 根许可作为明确 fallback；不读取用户凭据。
- 文本 SHA256 固定证据文件；CSV 中 `[cargo-registry] / [pub-cache] / [flutter-sdk]` 为来源占位前缀，不是仓库可直接跳转路径。无本地文件的官方网页条目 SHA256 留空。
- 本轮提取脚本保留于本机 `.tmp/license-inventory-20261020.py`，尚未建设通用 CI 许可门禁；恢复时须重新提取最新候选，不直接沿用本记录数量。

| 输入 | SHA256 |
| --- | --- |
| `package-lock.json` | `2032b589f617c2298a07edcd606e22d07ba82cd5e7585f3415d83bc330a7ce6d` |
| 本机 `Lib/radish.lib/Cargo.lock` | `fb84d20ce57f8a8b08ce3607c2d266f7d1d93ffb955c51d19b9b7bf0666ea571` |
| `Clients/radish.flutter/pubspec.lock` | `8f94fa210807a2da4792a62148b260ab4681bf2967a1142596fbb480374aff2b` |

官方来源见[主专题依据](../features/dependency-license-governance.md#8-外部依据)，Cargo 缺缓存补证据：[arbitrary 1.4.2](https://docs.rs/crate/arbitrary/1.4.2)、[crunchy 0.2.4](https://docs.rs/crate/crunchy/0.2.4/source/Cargo.toml)、[raw-cpuid 11.6.0](https://docs.rs/crate/raw-cpuid/11.6.0/source/Cargo.toml)、[libfuzzer-sys](https://docs.rs/crate/libfuzzer-sys/latest)（核对时页面明确为 0.4.13）。

## 7. 本轮验证

- CSV 一致性复核通过：947 条、四生态数量与正文相符、无重复证据条目、5 个 metadata 缺口、所有 NuGet 证据文件 SHA256 匹配；清单未写入缓存绝对路径或许可 key。
- `npm run check:docs` 与 `npm run check:repo-quality:changed` 通过：根入口同步、变更文件卫生、全量文档卫生和本地链接均通过。
- `git diff --check` 通过；上轮保留的五个代码 / 项目文件未在本轮继续修改。
- 本轮为文档与只读审计，没有重复运行后端测试，不新增 Rust 构建 / 真实运行结论；未提交或推送。
