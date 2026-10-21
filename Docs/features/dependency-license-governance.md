# 第三方依赖许可治理与替代方案

> 状态：Rust 图片替代已通过 PR #75 合并与回灌；2026-10-21 开始后续依赖评估，映射替代已获批并完成 Mapperly 4.3.1 切换，ORM / Redis 保持只评估。
>
> 目标：减少 Radish 构建、分发和持续维护对厂商 key、资格申请及复杂授权条件的依赖。图片替代已交付；后续精确依赖安装及服务启动按各批授权执行，其他依赖仍处于审计与方案阶段。

## 1. 当前结论与阅读入口

- 首轮统计覆盖正式 .NET、Web、Flutter、Rust 的 **947 个依赖条目**；精确口径、例外、未解析项与证据见[盘点记录](../records/dependency-license-audit-2026-10-20.md)和[逐包清单 CSV](../records/dependency-license-inventory-2026-10-20.csv)。条目不等于独立软件产品或许可证数量。
- 首轮优先替代对象是 **Six Labors 四包链**与 **AutoMapper**；两者均已按批准方案移除。前者曾在升级候选中出现构建 key 门禁；后者存在 RPL / 商业许可路线选择。不能把“免费申请”或“缺 key 只打日志”作为长期依赖决策的全部依据。
- Hangfire、数据库传递驱动、Microsoft 专用工具 / 原生库、CC-BY / MPL 依赖分别处理，不统一推定为收费或需 key。
- Redis 服务镜像另有版本相关许可风险；它不属于 `StackExchange.Redis` 客户端包，也不包含在上述 947 条中。
- **图片处理已按[完整替代实施方案](./rust-image-processing-implementation-plan.md)完成交付、验收与 PR 集成**。精确输入见[R0 依赖清单](./rust-image-processing-dependency-proposal.md)，迁移前事实与备选比较见[Rust 图片处理替代评估](./rust-image-processing-replacement-assessment.md)。

本专题承载治理目标、候选方案与实施边界；精确版本和本批统计保留在 records；即时顺位只在[当前进行中](../planning/current.md)维护。

**盘点不等于全部替换**：Rust 图片替代已完成，AutoMapper 已按[独立迁移方案](./post-image-dependency-assessment.md)替换为 Mapperly 4.3.1；没有决定整体替换 ORM 或 Redis，也没有安排文件存储重构、历史附件批量重写或 Flutter 内嵌 Rust。PR 已通过并合并，现按已确认顺序继续其他依赖评估；继续 .NET，不启动后端语言迁移。

## 2. 许可判定口径

这里的“需要 license”指需要额外资格、key、商业协议、互惠开源选择或特别分发处理；普通 MIT / Apache / BSD 同样有许可证与保留声明义务，不能称为“没有 license”。

审计分开回答五个问题：

1. **条款是什么**：以当前精确版本的包内 LICENSE / EULA 为优先依据，辅以官方版本来源；不能用仓库首页或最新版本条款覆盖旧包。
2. **是否适用**：分别评估项目所有者使用、官方 SaaS、容器 / 安装包分发和接收者自部署，不能把自己的资格传递给全部使用者。
3. **是否有技术门禁**：区分编译、Release、启动、功能执行和日志提示；没有技术拦截不等于取得授权。
4. **在哪个边界出现**：直接 / 传递，生产 / 测试 / 构建，应用 / 原生二进制 / 字体 / 服务镜像。
5. **替换的真实成本**：行为等价性、数据兼容、平台构建、供应链、安全更新及长期维护成本。

Radish 根 `LICENSE` 为 Source-Available，并非因此自动满足所有第三方库的互惠开源条款。保留该项目许可定位；本专题不改变 Radish 的发行授权，也不对所有可能的部署主体做统一许可结论。

## 3. 首轮重点清单

| 对象 | 当前精确版本 / 范围 | 识别到的边界 | 建议处理 |
| --- | --- | --- | --- |
| `SixLabors.ImageSharp`、`SixLabors.ImageSharp.Drawing` | 迁移前候选 `4.1.3 / 3.1.3`；本次工作区已移除 | 包内 Split License；Drawing 候选实际阻断无 key 的严格构建 / Release | 第一批：完整替代图片链；许可申请只作为明确选择后的备选 |
| `SixLabors.Fonts`、`SixLabors.PolygonClipper` | 传递 `3.2.0 / 1.0.2` | 同一 Six Labors 许可链 | 与图片链一起退出，不只删两个直接引用 |
| `AutoMapper` | 迁移前 `16.1.1`，现已移除 | 旧包为 RPL-1.5 / 商业双许可 | 第二批已切换 Mapperly 4.3.1（Apache-2.0），见[实施记录](../records/mapperly-migration-2026-10-21.md) |
| Hangfire 三个核心集成包 | `AspNetCore / Core / NetCore 1.8.14` | LGPL-3.0-or-later 或对应商业许可 | 核对分发义务及商用扩展缺席事实，再决定保留或替换 |
| `Hangfire.PostgreSql` | `1.20.12` | 包内 LGPLv3；第三方插件不能套用 Hangfire 核心商业许可 | 与核心调度方案一起评估 |
| `Oracle.ManagedDataAccess.Core` | 传递 `23.8.0` | Oracle Free Distribution, Hosting, and Use 条款 | 优先评估未使用 provider 的依赖收缩；不是发现 Oracle 名称即要求付费 |
| `Oscar.Data.SqlClient` | 传递 `4.0.4` | 包元数据缺 license / URL，包内未见对应许可正文 | 补证据或通过受支持方案退出；不能借用 ORM 的许可替它放行 |
| `SqlSugarCore.Dm / Kdbndp` | 传递 `8.8.1 / 9.3.7.905` | 元数据只有 Apache URL，未据此证明所含厂商二进制全部同许可 | 核对精确包内容与分发权；先收缩 provider 方案，不默认更换整个 ORM |
| `Microsoft.Data.SqlClient.SNI.runtime` | 传递 `5.2.0` | 包内 Microsoft 软件许可；Windows 原生库分发条件 | 与未使用 SQL Server provider 一并核对，区别于 MIT 客户端源码 |
| Visual Studio Container Tools Targets | `1.23.0` | Microsoft 工具 EULA，开发 / 构建用途 | 评估是否仍需 VS 容器调试接线；删除前验证 IDE / 构建影响 |
| `Isopoh.Cryptography.Argon2 / Blake2b / SecureArray` | `2.0.0` 三包 | 包内 CC-BY-4.0，署名和许可保留 | 优先补分发清单；不为去掉署名义务直接改变密码算法 |
| `lightningcss` 及平台包 | npm `1.31.1`，12 个开发依赖条目 | MPL-2.0，文件级许可义务 | 不属于 key 阻断；核对构建工具分发方式，无默认替换要求 |
| `caniuse-lite` | npm `1.0.30001769`，开发依赖 | CC-BY-4.0 数据许可 | 保留声明与来源，不按商业库替换 |
| Redis 服务 | Compose 声明 `redis:7`，精确 patch / digest 未冻结 | 7.2 及以前 BSD，7.4 系列 RSALv2 / SSPLv1 | 独立评估固定受支持版本或 Valkey；不能由 MIT 的客户端许可替服务背书 |

以上不是付费包列表。首轮 NuGet 有 20 个需说明 / 复核条目，其余元数据与包内许可属于常规声明范围；其中 5 包属于两组优先替代链，不能把 20 条全部叫作“必须买 license”。完整例外和统计边界见盘点记录。

## 4. 图片链替代：优先利用已有 Rust

项目已有 `Lib/radish.lib`、`IImageProcessor` 和 P/Invoke 接线。以下为迁移前静态核对事实；当前工作区已改为 Rust 单实现并移除 Six Labors，实际交付结果见[实施记录](../records/rust-image-replacement-2026-10-20.md)：

- Rust FFI 真正提供文字水印与 SHA256；当前应用上传 SHA256 主路径本身使用 .NET BCL，并非全部经 Rust。
- `RustImageProcessor` 的缩略图、缩放、压缩、EXIF 移除、多尺寸、信息与有效性检查仍调用 `CSharpImageProcessor`；图片水印也是正常 C# 路由。
- 文字水印成功后仍调用 C# 获取输出尺寸；失败继续回退 C#。打开 `UseRustExtension` 不能移除 Six Labors。
- 现有 Rust 文字水印实现存在参数不等价、字体覆盖和格式识别问题；当前 Dockerfile 不负责构建 Rust 原生库，不能把本机存在一个 `.dylib` 当作 Linux 发布能力。

首轮评估比较过以下路线；当前 Rust 设计见本节后的实施方案入口：

| 路线 | 收益 | 主要成本 / 必须证明的事项 |
| --- | --- | --- |
| 补齐 Rust，移除 Six Labors | 复用既有原生项目、FFI 和 `image / imageproc / ab_glyph` 生态 | 完整 ABI、内存 / 错误 / 资源限制、字体与元数据、所有正式服务目标的原生构建和交付 |
| SkiaSharp | MIT，图像与文字绘制能力集中 | 原生 assets、Linux 运行依赖、格式 / 动图 / EXIF 行为、字体排版不能只靠编译判断 |
| Magick.NET | Apache-2.0，图像格式与元数据候选能力广 | 底层 ImageMagick / delegates 的许可与 CVE、包体积、解码安全策略及平台产物 |
| 继续 Six Labors 并接许可 | API 迁移成本较小 | key / 资格 / 构建接线及外部分发主体的长期管理；不是本专题默认方向 |

项目所有者已确认沿补齐 Rust 的方向推进；[实施方案](./rust-image-processing-implementation-plan.md)明确单一 Rust 图片实现、完整格式 / 水印语义和原生交付门禁。SkiaSharp / Magick.NET 保留为历史候选比较，不与本轮 Rust 方案并行实施。

## 5. AutoMapper 替代方案

迁移前源码已核对 18 个已注册 Profile、104 个有向映射与 Service 中 98 处 Map 调用；附件有两个依赖 DI 的 URL resolver，商品更新需保留现有对象受保护字段。完整语义、候选版本、BaseService 接线及验证边界见[后续依赖评估与映射方案](./post-image-dependency-assessment.md)。迁移结果为 101 个旧方向保持、3 个无调用的空反向映射删除，并新增 AuditLog 读取映射；测试和产物证据见[实施记录](../records/mapperly-migration-2026-10-21.md)。

| 方案 | 适合边界 | 决策点 |
| --- | --- | --- |
| Mapperly（Apache-2.0） | 生成可检查的映射代码，编译期诊断 | 先验证 Vo 前缀、自定义规则、更新对象及生成器在项目分层中的放置 |
| 显式映射方法 | 关键业务映射、存在语义转换的少量类型 | 可读性高、无新增第三方 mapper；大量平铺模型时需控制重复 |
| 保留 AutoMapper 并选择适用许可 | 当前版本行为基本延续 | 必须明确适用路径；“空 key 不影响运行”不构成许可结论 |

已采用“生成映射 + 少量显式语义映射”，不引入仅转发的通用 mapper 抽象。实体 → Vo / DTO 仍由 Service 层负责，不将映射搬到 Controller 或 Repository。已清理 `LicenseKey` 配置、注册、旧 Profile、测试依赖及对外展示，并同步开发规范。

## 6. 其他依赖的替代边界

### 6.1 调度与 Hangfire

先核对当前 LGPL 分发材料和是否修改库，不能从 LGPL 推导出“整个 Radish 必须公开全部源码”或“必须购买 Pro”。本轮未发现 Hangfire.Pro / Ace 包引用。

如所有者决定统一退出该许可链，比较 Quartz.NET 与基于既有 Outbox 的专门 worker；两者都不是原调度器的直接平替。必须覆盖持久化、重试、重复执行、并发 / 多实例、取消与停机、定时 / 时区、管理入口权限，以及历史 Hangfire 作业表 / ID / 队列的迁移和回滚。不得用纯内存 timer 替代需要持久性的业务任务。

### 6.2 ORM 的传递数据库驱动

Radish 正式数据库边界为 PostgreSQL / 本地 SQLite，但通用 `SqlSugarCore` 传递引入 Oracle、神通、达梦、金仓和 SQL Server 相关资产。处理顺位：

1. 检查实际配置支持范围、resolved graph、publish 内容和运行时动态加载。
2. 调查上游受支持的 provider 分包 / 精简方案；已发现同一发布者的 `SqlSugarCoreNoDrive`，但其较新版本通过 `SystemCommon` 仍引入 SQL Server 客户端，尚未证明精确候选满足分发要求，见[详细评估](./post-image-dependency-assessment.md)。
3. 若无法安全收缩，再比较维护受控分发与 ORM 迁移的长期成本；后者属于跨 Repository 的独立架构裁决。

不能只删除输出 DLL、修改本地 NuGet 缓存、隐藏扫描命中或添加 `ExcludeAssets` 来假装分发与运行边界已经解决。也不能因为客户端驱动免费分发，就推导数据库服务器本身免费。

### 6.3 密码、字体与原生工具

- Argon2 三包目前的事项是 CC-BY 声明。若更换实现，必须保留历史编码哈希验证、参数、失败语义和渐进迁移；不能顺带改成另一种哈希或要求用户重置密码。
- Noto 随 Flutter 打包并有 OFL 正文；Rust 嵌入 DejaVu，需把字体原始许可 / notices 纳入原生产物分发。仅在 README 写“Free”不够。
- Microsoft 构建工具 EULA 与原生 SNI 条款分别处理，既不统一归为 MIT，也不自动归为付费 key。

### 6.4 Redis 服务

先锁定计划候选的精确版本和 digest，再根据实际功能比较保留 Redis 与 Valkey。验证客户端协议之外，还包括 Lua / 原子操作、锁、缓存、SignalR backplane、持久化、ACL / TLS、备份恢复及运维接线。本轮不访问生产以探测其版本，不修改 Compose 或迁移数据。已核对上传限流的四段 Lua、缓存接线及 Redis 7.4+ 数据文件兼容限制，见[后续评估](./post-image-dependency-assessment.md)。

## 7. 分批顺序、验证与完成标准

| 批次 | 目标 | 进入实现前需确认 |
| --- | --- | --- |
| 盘点 | 本文、逐包清单、Rust 源码能力矩阵 | 已获授权；剩余证据缺口保持显式待办 |
| 图片替代 | 去除 Six Labors 四包及 key 构建依赖 | Rust 或备选路线、格式与 ABI、依赖版本、构建平台、配置迁移、回滚 |
| 映射替代 | 去除 AutoMapper 及其许可配置链 | 全部映射语义、生成器 / 显式映射边界、编译与业务回归 |
| 分发收口 | Hangfire、数据库驱动、Microsoft EULA、署名与字体 | 保留 / 收缩 / 替代的逐项裁决与发行材料 |
| 服务候选 | Redis 固定候选或 Valkey 方案 | 数据、运维、协议和回滚方案，独立服务授权 |

图片替代已通过严格构建、平台与成组验收，并随 [PR #75](https://github.com/laugh0608/Radish/pull/75) 合并及回灌；原许可 key 阻断已解除。映射替代已按批准方案实施并单独验证，见[映射迁移记录](../records/mapperly-migration-2026-10-21.md)；既有图片验收不替代新依赖的验证；本轮不发布。

候选完成至少满足：

- 移除对象不再出现在直接 / 传递依赖图、测试依赖、实际发布产物和文档展示中；“生产默认不用”不足以完成移除。
- clean restore、warning-as-error、Release / publish、必要平台构建、行为回归和依赖安全审计通过，普通构建不依赖开发者私有 key 或外部许可申请。
- 图片具备真实 native ABI 与格式 / 像素 / 元数据验证，映射具备转换语义对照，调度 / 密码 / 数据迁移分别验证其权威状态。
- 第三方许可证 / copyright / NOTICE 覆盖库、原生二进制、字体和分发镜像；包体积、启动失败、缺库错误和回滚路径均明确。
- 本地门禁与 GitHub Actions 一致后再恢复 PR；不以压掉 warning、固定有已知漏洞的旧版本或更改审计阈值收口。

新的包安装、依赖替换、运行时 / CI / Docker / 数据边界修改均需按具体方案确认。专题盘点不自动授权全部批次实施。

## 8. 外部依据

所有链接核对日期为 2026-10-20；精确包内文件及 SHA256 另见 CSV。

- [Six Labors 许可与申请说明](https://sixlabors.com/pricing/)；当前四包内 `LICENSE` 为 Six Labors Split License 1.0，不能只以官网概述代替包内条款。
- [AutoMapper 16.1.1 许可正文](https://raw.githubusercontent.com/AutoMapper/AutoMapper/v16.1.1/LICENSE.md)、[许可配置与日志执行方式](https://docs.automapper.io/en/latest/License-configuration.html)。
- [Hangfire 许可](https://www.hangfire.io/licenses.html)；`Hangfire.PostgreSql` 以其独立包内许可为准。
- [Quartz.NET 候选许可](https://github.com/quartznet/quartznet/blob/main/license.txt)、[DejaVu 字体许可与 notices](https://dejavu-fonts.github.io/License.html)。
- [Oracle 23.8.0 发布页](https://www.nuget.org/packages/Oracle.ManagedDataAccess.Core/23.8.0)、[Oscar 4.0.4 元数据页](https://www.nuget.org/packages/Oscar.Data.SqlClient/4.0.4)。
- [Redis 版本与许可表](https://redis.io/legal/licenses/)、[Valkey COPYING](https://raw.githubusercontent.com/valkey-io/valkey/unstable/COPYING)。
- [Mapperly 许可](https://raw.githubusercontent.com/riok/mapperly/main/LICENSE)、[SkiaSharp 许可](https://github.com/mono/SkiaSharp/blob/main/LICENSE.txt)、[Magick.NET 许可](https://github.com/dlemstra/Magick.NET/blob/main/License.txt)。候选未选定精确版本，安装前须再核对版本、全部传递依赖与实际包内条款。
