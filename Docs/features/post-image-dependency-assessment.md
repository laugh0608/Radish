# 图片替代后的依赖评估与映射迁移方案

> 2026-10-21，基于 `4a8589b6` 的源码与本机已有 resolved graph；外部依据同日核对。本页保留迁移前盘点与方案；项目所有者随后批准安装 `Mapperly 4.3.1` 并完整替代 AutoMapper，实施结果见[映射迁移记录](../records/mapperly-migration-2026-10-21.md)。
>
> 本批仅替代 AutoMapper，采用 Mapperly 生成映射与显式业务转换。SqlSugar 优先调查上游驱动收缩，Redis 独立评估 Valkey；继续 .NET。治理原则见[依赖许可专题](./dependency-license-governance.md)。

## 1. 交接与本轮边界

[PR #75](https://github.com/laugh0608/Radish/pull/75) 已完成检查、合并和 `master → dev` 快进回灌。前会话记录的三分支一致于 `4a8589b6`，本轮本地 `dev / origin/dev / origin/master` 跟踪引用核对一致、初始工作区干净；本轮没有重新 fetch，不将跟踪引用视为新的联网验证。

图片替代的验收事实保留在[实施记录](../records/rust-image-replacement-2026-10-20.md)。未发布，未创建新 tag；下一批不包括后端语言迁移、文件存储重构、历史附件重写、Flutter 内嵌 Rust 或新增图片格式。

初始评估只读核对；获批后实施第 2 节的映射替代与验证，不启动项目宿主或容器，不连接生产。ORM 与 Redis 保持只评估。

## 2. AutoMapper：范围与必须保留的行为

### 2.1 迁移前源码盘点（`4a8589b6`）

扫描范围为 `Radish.*/**/*.cs`，排除 `bin / obj`，去除注释后统计文本调用；不是 Roslyn 调用图，也不把声明等同实际运行覆盖。

- `AutoMapper 16.1.1` 由 Extension / Service 直接引用；API 与 Auth 调用 `AddAutoMapperSetup`。
- `AutoMapperConfig` 实际注册 **18 个 Profile、104 个有向 CreateMap**。源码另有未注册的 `AuditLogProfile` 两个映射，以及只有注释的 `ViewModelProfile`。
- Service 中 `Mapper / _mapper / mapper.Map` 共 **98 处，分布于 30 个文件**；其中 BaseService 有 9 处。API / Auth 源码未找到直接 Map 调用。
- `IMapper` 出现在 79 个文件（含测试）；仅用来估计构造函数与测试迁移影响。
- 未找到 `ProjectTo` 调用；当前 Profile 未使用 `ReverseMap`，反向映射为独立声明。不得因此省略反向字段保护。

| 已注册 Profile | 有向映射数 | 主要验证点 |
| --- | ---: | --- |
| Custom / Role / AuditSqlLog / Oidc | 10 / 2 / 2 / 10 | 双向前缀、租户模型、OIDC 特殊字段 |
| User | 7 | `Uuid ↔ Id`、PublicId、显示名 / handle、密码忽略 |
| Forum | 29 | 租户字段保护、列表 / 树形补全、问答 / 投票 / 抽奖 / 表情 |
| Attachment | 2 | 文件大小文本、两个依赖注入 URL resolver |
| Coin / PaymentPassword | 4 / 1 | 金额、敏感字段及服务补全 |
| Notification / Experience | 2 / 8 | 状态、等级、交易与日统计 |
| Shop | 12 | 原地更新、上下架 / 版本 / 审计保护、库存与权益计算 |
| Chat / UserFollow / Wiki | 4 / 1 / 4 | 消息补全、关注、文档详情与树 |
| ContentModeration / ConsoleAuthorization / Pet | 2 / 2 / 2 | 特殊 ID、状态补全、权限资源和宠物数据 |

迁移前配置与 Profile 可从 `4a8589b6` 查阅；现行入口：[映射注册](../../Radish.Service/Mapping/ServiceMappingRegistration.cs)、[商品映射](../../Radish.Service/Mapping/ShopMapper.cs)、[BaseService](../../Radish.Service/Base/BaseService.cs)。

### 2.2 不能机械替换的语义

1. **字段名字与安全边界**：`Vo` 不是大小写差异。对目标属性建立明确映射 / 忽略清单；特别检查 `UserVo.Uuid`、`LoginPassword`、TenantId、创建 / 修改审计、Version 和上下架字段。不得靠新库的自动匹配扩大写入范围。
2. **原地更新**：`ProductService` 的 `Mapper.Map(dto, product)` 修改已加载对象。新实现必须保留忽略字段的原值，不能用新实体覆盖数据库对象。现有 `ShopMappingTests.ProductWriteMappings_ShouldNeverChangeSaleState` 是必要回归。
3. **业务转换**：显示名 / handle、库存状态、有效期文案、文件大小格式均保持既有结果；时间、文化区、数字精度不能因默认转换改变。
4. **集合与默认值**：验证 null 对象、null / 空集合、列表顺序、nullable、枚举、嵌套集合、对象默认初始化与集合复制语义。不能直接继承新库默认行为。
5. **附件 URL**：两个 `IValueResolver` 调用 `IAttachmentUrlResolver`。继承的 BaseService 查询同样必须经过附件 URL 补全，不能只覆盖上传返回值；不把 scoped 依赖捕获进全局静态字段。
6. **服务补全**：Profile 忽略的头像、标签、树、权限或状态仍由既有服务计算，生成器不得擅自从同名字段填入。
7. **未注册映射**：`AuditLogService` 存在 `AuditLog → AuditLogVo` 调用，而 `AuditLogProfile` 未进入集中注册。先用定向测试确认运行行为；若需修复，作为本批映射缺口明确列出。`CreateAuditLogDto → AuditLog` 的 `DateTime.Now` 规则不因迁移顺手启用。

### 2.3 方案比较与已批准设计

| 方案 | 收益 | 成本 / 判断 |
| --- | --- | --- |
| Mapperly + 显式业务转换 | 编译生成可检查代码，映射缺失可在构建期暴露 | 推荐；需显式配置前缀与忽略字段，验证当前 SDK / 生成器 |
| 全部手写 | 不增加 mapper 依赖，特殊规则直观 | 可行备选，但大量平铺字段需持续人工同步与覆盖检查 |
| 保留 AutoMapper | 现有行为变动最小 | 仍需明确 RPL / 商业许可路径，不满足本次减少许可选择负担的目标 |

候选固定为 `Riok.Mapperly 4.3.1`；[NuGet 精确版本页](https://www.nuget.org/packages/Riok.Mapperly/4.3.1)列出无 NuGet 依赖，[对应 tag 许可](https://raw.githubusercontent.com/riok/mapperly/v4.3.1/LICENSE)为 Apache-2.0。获批后已核对包内 `.nuspec` 的 Apache-2.0 标识、LICENSE 与无 NuGet 依赖声明；生成器和发布产物检查见实施记录。

映射放在 `Radish.Service/Mapping/`，按业务域组织，保持实体到 Vo / DTO 的 Service 层边界：

- 纯映射使用明确类型的生成方法；语义转换使用现有业务函数或少量显式方法，不新增反射注册表或泛用 `Map<T>(object)` 分发器。
- `BaseService<TEntity, TVo>` 通过构造参数接收明确的 `Func<TEntity, TVo>`，由具体 Service 提供本域映射。集合与分页仍在 BaseService 统一处理，保持顺序、空值和总数语义；IService 的业务方法签名不改。
- 附件使用持有 `IAttachmentUrlResolver` 的实例映射器 / 显式补全方法，按 Service 生命周期使用。派生 Service 传入包含补全的映射方法，保证基类查询也正确。
- `UpdateProductDto` 使用明确的“更新已有商品”方法；读映射与写映射分别声明，防止只读映射规则被复用于写入。
- 所有 104 个已注册方向建立迁移对照；未被业务或测试使用的方向先证明可删，再决定删留。未注册 Profile 单独处理，不机械激活。
- 删除两处 AutoMapper 包引用、Profile / 注册链、API / Auth 启动调用、LicenseKey 配置与依赖展示；更新测试构造，不用空 mapper mock 掩盖行为缺失。

Mapperly 的[字段配置](https://mapperly.riok.app/docs/configuration/mapper/)、[已有目标更新](https://mapperly.riok.app/docs/configuration/existing-target/)和[泛型限制](https://mapperly.riok.app/docs/configuration/generic-mapping/)说明了所需能力，但不证明 Radish 已等价。前缀采用显式属性映射；编译诊断逐项处理，不全局屏蔽未映射字段告警。

### 2.4 实施批次、精确依赖动作与验收

按批准方案先完成代表语义验证，再整体切换；最终交付只保留一套映射实现。

1. **代表验证**：User、Shop、Attachment 与 BaseService 列表 / 分页，验证生成代码、字段保护和 DI；调查 AuditLog 注册缺口。
2. **完整迁移**：按上表完成所有业务域、服务构造、测试与配置清理；不把只有部分功能迁移的状态当成最终候选。
3. **验证收口**：Debug / Release 严格构建，现有映射与相关服务测试，附件 URL、租户隔离、商品更新、用户身份定向测试；完整后端及 identity / baseline 按候选门禁执行。需要数据库容器或浏览器 Smoke 时另列命令、端口、数据与清理范围申请授权。
4. **产物审计**：现有与 clean restore 图、宿主 publish 的 `.deps.json` / DLL、测试引用和文档展示中均不得保留 AutoMapper；生成器不意外作为业务运行依赖分发。保留必要许可证材料。

依赖授权清单仅包含：

```sh
dotnet add Radish.Service/Radish.Service.csproj package Riok.Mapperly --version 4.3.1 --no-restore
dotnet remove Radish.Service/Radish.Service.csproj package AutoMapper
dotnet remove Radish.Extension/Radish.Extension.csproj package AutoMapper
dotnet restore Radish.slnx --disable-parallel
```

新增引用设为 `PrivateAssets="all"`，精确固定版本。删除旧引用安排在迁移完成时；本轮不新增 npm / Rust / Flutter 包，不创建或修改数据库，不启动项目服务。若真实 restore 出现未列明依赖或需改变候选版本，先解释差异再确认。

迁移失败时回退本批代码 / 依赖变更；不保留运行时双 mapper fallback，不变更数据库结构或 API 响应来适配生成器。

## 3. ORM：后续精确包调查

初轮建议为保留 SqlSugar 行为、优先调查驱动收缩。Mapperly 提交 `b8debb26` 推送后，已取得 `SqlSugarCoreNoDrive 5.1.4.207` 精确包：主 DLL 与现用通用包相同，但 `SystemCommon 8.5.0` 仍引入 SqlClient / SNI，并携带另一份同名程序集。

完整包证据、四宿主分发边界、EF Core / Dapper 比较和实验标准以[ORM 与 Redis 依赖评估](./orm-redis-dependency-assessment.md)为准。本轮没有安装候选或替换 ORM。

## 4. Redis：后续服务与数据调查

初轮已识别上传四段 Lua 与持久化边界。后续已完成源码 key 分类，确认现有上传测试仅验证内存分支；公开 registry 当前 `redis:7` 的 amd64 config 为 7.4.11，但实际部署源版本未知。

Valkey 9.1.2 / 8.1.10 的精确 manifest、支持期、数据兼容停止线与隔离实验要求以[ORM 与 Redis 依赖评估](./orm-redis-dependency-assessment.md)为准。本轮不改 Compose、不启动服务、不迁移数据。

## 5. 当前建议与停止线

第 2 节已获批准并完成映射代码切换，验证事实见[实施记录](../records/mapperly-migration-2026-10-21.md)。NoDrive / Valkey 已按单独批准的范围完成隔离验证与清理，结论及两类既有业务问题见[记录](../records/nodrive-valkey-validation-2026-10-21.md)；两类问题随后已随 `31973553` 修复并完成定向回归，见[修复记录](../records/upload-quota-boundary-fix-2026-10-21.md)，不改写原实验失败事实。二者均未正式替换。AuditLog 的旧注册缺口经测试确认后已补齐读取映射，未启用未使用的写入配置。

本文没有改变现行架构规范、业务接口或数据库行为。实施需要项目所有者批准；依赖实验与服务启动分别遵守 [AGENTS.md](../../AGENTS.md) 的单独授权要求。
