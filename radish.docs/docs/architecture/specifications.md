# 开发规范

- 养成一个好习惯，先写接口（IService），再写实现（Service）
- BaseRepository、BaseService、IBaseRepository、IBaseService 都已定义，提供完整的 CRUD 方法
- **重要**：简单 CRUD 操作直接使用 `IBaseService<TEntity, TVo>`，只为复杂业务逻辑创建专门的 Service
  - ✅ 推荐：`IBaseService<Category, CategoryVo>` 用于简单的分类查询
  - ❌ 避免：为每个实体都创建 `ICategoryService`，只是包装 BaseService 方法
  - ✅ 推荐：`IPostService : IBaseService<Post, PostVo>` 包含复杂的 `PublishPostAsync` 逻辑

## 项目版本号规范

Radish 项目采用**语义化版本号（Semantic Versioning）**规范，确保版本号能够清晰传达变更的性质和影响范围。

### 版本号格式

标准版本号格式为：`vMAJOR.MINOR.PATCH`

- **MAJOR（主版本号）**：重大功能变更或不兼容的 API 修改
  - 包含架构重构、核心功能调整、破坏性变更
  - 可能导致现有客户端或集成系统需要适配
  - 示例：v1.0.0 → v2.0.0

- **MINOR（次版本号）**：向后兼容的功能新增
  - 添加新的接口、新的功能模块
  - 增强现有功能但不破坏兼容性
  - 示例：v1.0.0 → v1.1.0

- **PATCH（修订版本号）**：向后兼容的问题修复
  - Bug 修复、性能优化、文档更新
  - 不改变 API 接口和功能行为
  - 示例：v1.1.0 → v1.1.1

### 版本号示例

**典型发布流程：**
```
v0.1.0 - 项目初始版本（开发阶段）
v0.2.0 - 添加用户认证功能
v0.3.0 - 添加多租户支持
v1.0.0 - 首个正式发布版本（生产就绪）
v1.1.0 - 添加 SignalR 实时通信功能
v1.1.1 - 修复登录超时 Bug
v1.2.0 - 添加 Rust 原生扩展支持
v2.0.0 - 架构重构，迁移至 .NET 10
```

### 特殊版本格式

**热更新/阶段性版本：**

对于当日发布的热更新、紧急修复或阶段性 PR 合并，可使用扩展格式：`vMAJOR.MINOR.PATCH.YYMMDD`

- **YYMMDD**：两位年份 + 两位月份 + 两位日期
- 示例：
  - `v0.1.0.251126` - 2025年11月26日的开发阶段热更新
  - `v1.2.3.251201` - v1.2.3 版本在2025年12月1日的紧急修复
  - `v2.0.0.260115` - v2.0.0 版本在2026年1月15日的阶段性合并

**使用场景：**
- 当日多次部署需要区分构建版本
- 开发/测试环境的频繁更新
- 分支合并前的阶段性验证
- 热修复快速迭代

**注意事项：**
- 日期后缀不计入语义化版本的比较规则
- 正式发布时应去掉日期后缀，只保留三段式版本号
- 生产环境部署优先使用标准三段式版本号

### 版本号使用规范

**Git 标签：**
```bash
# 创建版本标签
git tag -a v1.2.0 -m "Release version 1.2.0: Add SignalR support"
git push origin v1.2.0

# 创建热更新标签
git tag -a v1.2.0.251126 -m "Hotfix: Fix critical login bug"
git push origin v1.2.0.251126
```

**发布说明：**
- 每个版本发布必须在 GitHub Release 中创建对应条目
- Release Notes 应包含：
  - **新增功能**（Features）
  - **Bug 修复**（Bug Fixes）
  - **性能优化**（Performance）
  - **破坏性变更**（Breaking Changes）
  - **依赖更新**（Dependencies）

**配置文件：**
- 前端版本号：`radish.client/package.json` 中的 `version` 字段
- 后端版本号：`Radish.Api/Radish.Api.csproj` 中的 `<Version>` 标签
- 保持前后端版本号同步更新

**版本兼容性：**
- v0.x.x：开发阶段，API 可能频繁变更，不保证兼容性
- v1.x.x：首个稳定版本，MINOR 和 PATCH 更新保持向后兼容
- v2.x.x+：重大版本更新，需提供迁移指南和兼容性说明

### 版本发布流程

1. **版本规划**：在开发开始前确定目标版本号
2. **代码冻结**：完成所有计划功能后冻结新增代码
3. **测试验证**：完整的功能测试、性能测试、安全测试
4. **版本号更新**：统一更新前后端版本号
5. **Git 标签**：创建版本标签并推送到远程仓库
6. **Release Notes**：编写详细的发布说明
7. **部署发布**：按照 [deployment/guide.md](../deployment/guide.md) 进行部署

### 与 API 版本控制的关系

- **项目版本号**（本节规范）：指整个 Radish 项目的发布版本，用于标识软件的迭代状态
- **API 版本控制**（见 "API 版本控制规范" 章节）：指 HTTP API 的接口版本，用于管理 API 的向后兼容性

两者独立管理：
- 项目版本 v1.2.3 可能同时包含 API v1 和 API v2
- API 版本更新不一定触发项目主版本号变更
- 项目重大版本更新（如 v2.0.0）通常伴随 API 版本升级

## 项目结构约定

- docs/：项目文档，实际文件夹，映射解决方案中的 docs 目录，包含开发规范、设计文档等
- others/：其他资源文件，虚拟文件夹，只是解决方案中的文件夹，其中所有文件均为项目根目录下的，包括 Dockerfile、GitHub 配置、start.ps1 脚本 等
- radish.client：主要 - 前端 React 应用代码（WebOS 桌面环境），TypeScript 编写；采用混合架构支持三种应用集成方式：内置应用(type: 'window')、嵌入应用(type: 'iframe')、外部应用(type: 'external')。详见 [frontend/design.md](../frontend/design.md)
- radish.console：主要 - 管理控制台前端应用，独立的 SPA；通过 OIDC 认证，有独立的路由系统；不嵌入 radish.client，在新标签页独立运行
- radish.ui：主要 - UI 组件库，通过 npm workspaces 供 radish.client 和 radish.console 共享基础组件、Hooks 和工具函数
- Radish.Gateway：主要 - 服务门户与网关项目，ASP.NET Core 编写；已实现统一服务入口、YARP 路由转发、健康检查聚合等核心功能。详细说明见 [Gateway 服务网关](../guide/gateway.md)。
- Radish.Api：主要 - 后端服务代码，ASP.NET Core 编写；专注于提供 REST API 接口，不包含页面展示功能
- Radish.Common：后端服务使用的普通工具类，例如基础日志、基础配置等；**仅能引用外部 NuGet 包，不允许依赖任何内部业务层**。若某工具/扩展需要访问 `Radish.Model`、Service 或 Repository 中的类型（如 DTO、实体、仓储服务等），应放置在 `Radish.Extension` 中，以免 Common 层被反向依赖导致环状引用。
- Radish.Core：后端核心业务逻辑与算法类，保留模块，为后续流程模拟与算法实现做准备
- Radish.Extension：后端扩展功能模块类，例如 Swagger/Scalar、HealthCheck 等
- Radish.Infrastructure：数据库与多租户基础设施层，集中维护 SqlSugar 扩展、租户过滤、表/库路由与连接解析逻辑，供 Extension、Repository 等项目复用
- Radish.IRepository：后端数据访问接口类，定义数据访问层接口
- Radish.IService：后端服务接口类，定义业务逻辑层接口
- Radish.Model：后端数据模型类，仅存放数据库实体；实体（Entity）只能在 Repository 层内被直接操作，Service 层及以上均需转换为视图模型或 DTO
- Radish.Model.LogModels：用于承载日志库实体，`BaseLog` 统一 DateTime/Level/Message 等字段，`AuditSqlLog` 通过 `[Tenant(configId: "log")] + [SplitTable(SplitType.Month)]` 按月写入日志；对应的 `AuditSqlLogVo` 仍位于 `ViewModels`，对外访问必须走 Vo/DTO
- Radish.Repository：后端数据访问实现类，具体实现数据访问接口
- Radish.Service：后端服务实现类，具体实现业务逻辑接口
- Radish.Shared：前后端共享的模型和工具类，例如 DTO、枚举等
- Radish.Api.Tests：xUnit 测试工程，目前包含 UserController 示例测试，约束接口返回示例数据
- Rust 原生扩展：当前统一实现位于 `Radish.Core/radish-lib`（统一 Rust 扩展库，构建后拷贝到 `Radish.Api/bin/<Configuration>/net10.0/`）；如后续需要从 Core 抽离，再迁到根目录 `native/rust/{library}` 并作为 Solution Folder 挂载。

## 分层依赖约定

- 前端项目：
  - radish.client：仅依赖 npm 包和 @radish/ui 组件库
  - radish.console：仅依赖 npm 包和 @radish/ui 组件库，完全独立于 radish.client
  - radish.ui：仅依赖外部 npm 包，不依赖任何业务项目
  - **重要**：radish.client 和 radish.console 是两个完全独立的 SPA，各自有独立的路由、认证流程和部署方式；它们通过 @radish/ui 共享基础组件，但业务逻辑和状态管理完全隔离
- Gateway 项目（Radish.Gateway）：
  - Phase 0 阶段：依赖 `Radish.Common`（配置工具）和 `Radish.Extension`（日志扩展），提供 Razor Pages 页面展示和静态文件服务
  - P1+ 阶段：额外引入 `Ocelot` 或 `YARP` 实现路由转发，可能需要引用 `Radish.Service` 实现聚合接口和统一认证
  - 职责：服务门户展示、健康检查聚合、API 路由转发（P1+）、统一认证（P2+）、请求聚合（P3+）
- 后端项目按层次结构依赖：
  - Radish.Api 引用 radish.client（用于 SPA 代理）与 Radish.Service，并通过 Program.cs 注入 `IUserService/IUserRepository` 等接口实现；同时依赖 Radish.Common 以注册 `AppSettings` 扩展，避免在其他层重复创建配置源。
  - Radish.Service 依赖 Radish.IService（接口契约）与 Radish.Repository（数据访问实现），负责聚合业务逻辑；Service 层对外仅暴露 DTO/Vo，必须在返回前将仓储层实体映射为视图模型（推荐 AutoMapper）。
    - **简单 CRUD 场景**：Controller 直接注入 `IBaseService<TEntity, TVo>`，无需创建专门的 Service 实现
    - **复杂业务逻辑场景**：创建继承自 `BaseService<TEntity, TVo>` 的自定义 Service，添加特定业务方法
    - BaseService 提供的通用方法：QueryAsync, QueryByIdAsync, QueryPageAsync, AddAsync, UpdateAsync, DeleteAsync 等
  - Radish.Repository 依赖 Radish.IRepository、Radish.Model 以及 Radish.Infrastructure 中的 SqlSugar/租户扩展，只能向 Service 层返回实体或实体集合，禁止直接引用任何 Vo/DTO；接口层 Radish.IRepository 与 Radish.IService 统一依赖 Radish.Model，以便共享实体与视图模型定义。
- Radish.Extension 仅由宿主（Radish.Api、Radish.Gateway）引用，用于集中管理 Autofac/AutoMapper/配置扩展；该项目可以引用 Service/Repository 以及 Infrastructure 以注册实现，但 Service/Repository 项目禁止反向依赖。凡是需要宿主信息的模块（如 Controller 程序集、配置源等）必须通过构造函数参数由宿主传入，例如 `new AutofacPropertyModuleReg(typeof(Program).Assembly)`，避免因为直接引用 `Program` 造成循环依赖。
  - Radish.Core 暂时保留，无直接依赖关系
- `UserController -> IUserService -> IUserRepository` 构成的示例链路是官方范例，任何新功能应当沿用"Controller 调用 Service，再由 Service 访问 Repository"的模式
  - **简单 CRUD 示例**：`CategoryController` 直接注入 `IBaseService<Category, CategoryVo>`，使用 `QueryAsync(c => c.IsEnabled)` 等方法
  - **复杂逻辑示例**：`PostController` 注入 `IPostService`，调用自定义的 `PublishPostAsync()` 方法处理多表事务
  - 若只需简单 CRUD，无需创建专门的 `ICategoryService` 等接口

## 数据库与 SqlSugar 配置

**重要说明 - API 和 Auth 项目共享业务数据库**：
- **Radish.Api** 和 **Radish.Auth** 项目必须使用**相同的业务数据库配置**
- 两个项目的 `appsettings.json` 中的 `Databases` 配置必须保持一致（`Radish.db` 和 `RadishLog.db`）
- 这是因为 Auth 项目需要访问用户、角色、权限、租户等业务数据来验证身份和权限
- **OpenIddict 数据库**（`RadishAuth.OpenIddict.db`）是独立的，仅由 Auth 项目使用，存储 OIDC 认证相关数据
- **所有数据库文件统一存放在解决方案根目录的 `DataBases/` 文件夹**

- `Program.cs` 需要在 `builder.Build()` 前调用 `builder.Services.AddSqlSugarSetup()`。该扩展定义于 `Radish.Extension.SqlSugarExtension`，内部依赖 `Radish.Infrastructure.Tenant.RepositorySetting`、`TenantUtil` 等组件，使用 `SqlSugarScope` 单例注入并绑定所有连接配置。
- `appsettings.json` 约定结构如下（**API 和 Auth 项目必须保持一致**）：

```json
{
  "MainDb": "Main",
  "Databases": [
    {
      "ConnId": "Main",
      "DbType": 2,
      "Enabled": true,
      "ConnectionString": "Radish.db"  // API 和 Auth 共享
    },
    {
      "ConnId": "Log",
      "DbType": 2,
      "Enabled": true,
      "ConnectionString": "RadishLog.db",  // API 和 Auth 共享
      "HitRate": 50
    }
  ]
}
```

  - `MainDb` 指定默认主库的 `ConnId`；当配置多库/主从时，`BaseDbConfig.MutiConnectionString` 会把该连接放在集合首位。
  - `Databases` 中至少包含 `ConnId=Main` 与 `ConnId=Log` 两条记录，后者名称固定（`SqlSugarConst.LogConfigId`），缺失时启动会抛出异常；其余库可自定义 `DbType` 与从库 `Slaves` 列表。
- `Snowflake` 段存放雪花 ID 的 `WorkId` 与 `DataCenterId`，本地/测试/生产必须配置为互不相同的整数（0~30），避免多节点冲突；`appsettings.json` 只保存兜底值，实际部署请在 `appsettings.{Environment}.json`（如 Development/Production）中覆盖，以便按服务器或容器分配固定编号。
- Program 在调用 `builder.Services.AddSqlSugarSetup()` 后会立即读取 `Snowflake` 段并设置 `SnowFlakeSingle.WorkId/DatacenterId`，因此只要在环境配置中写好差异值即可，无需在扩展或仓储层重复配置；若服务器时间曾被回拨，务必同步调整新的 WorkId。
- `BaseRepository` 通过 `IUnitOfWorkManage` 获取 `SqlSugarScope` 单例，内部根据泛型实体的 `[Tenant(configId)]` 特性切换连接；未标注特性的实体沿用 `MainDb`，标注了 `configId="Log"` 等值（大小写不敏感）的实体会自动访问对应库。需要写入日志库或其他独立库的模型务必显式添加 `TenantAttribute`，以免错写入主库；若实体启用了 `[MultiTenant(TenantTypeEnum.DataBases)]`，`BaseRepository` 会通过 `TenantUtil.GetConnectionConfig()` 在运行期动态添加租户库配置。
- 使用 SQLite 时 `ConnectionString` 只需传数据库文件名，运行期会自动拼接 `Environment.CurrentDirectory`；对于 MySQL/SQLServer 等外部数据库，可通过 `dbCountPsw1_*.txt` 本地文件或环境变量隐藏真实连接串，`BaseDbConfig.SpecialDbString` 会优先读取文件值。
- SQL 日志统一通过 `SqlSugarAop.OnLogExecuting` 写入 Serilog，`LogContextTool` 会在上下文中打上 `LogSource=AopSql` 标签；SqlSugar 的缓存实现委托给 `Radish.Common.CacheTool.SqlSugarCache`，保持与 Redis/内存缓存一致的策略。Serilog 输出由 `SerilogSetup + LogConfigExtension` 统一配置，普通日志与 SQL 日志会分别落入 `Log/Log.txt` 与 `Log/AopSql/AopSql.txt`，并启用 `WriteTo.Async()` 防止阻塞请求线程。

### 多租户隔离模式示例

- **字段隔离（单表）**：`BusinessTable : ITenantEntity` 依赖 QueryFilter 自动按 `TenantId` 过滤；调用 `GET /api/Tenant/BusinessTable` 可查看仅当前租户可见的数据，`.http` 示例位于 `Radish.Api/Radish.Api.http`。
- **表隔离**：`MultiBusinessTable` 标注 `[MultiTenant(TenantTypeEnum.Tables)]`，`TenantUtil.SetTenantTable()` 会在运行期改写表名为 `表名_{TenantId}`，示例接口 `GET /api/Tenant/MultiBusinessTable`。
- **分库隔离**：`SubLibBusinessTable` 标注 `[MultiTenant(TenantTypeEnum.DataBases)]`，`TenantUtil.GetConnectionConfig()` 根据租户配置动态注入连接，示例接口 `GET /api/Tenant/SubLibBusinessTable`（参考 `.http` 文件）。租户主数据需在数据库中预配 `TenantConfigId/DbConnectionStr` 等字段，且 `App.HttpContextUser` 必须包含合法 `TenantId`。

## 跨语言扩展（Rust 原生库）

- 目的：为 CPU 密集或高并发任务提供 Rust 实现，并通过 `RustTestController`（v2）验证性能差异；当前统一扩展库为 `Radish.Core/radish-lib`，并以 `[DllImport("radish_lib")]` 进行加载。
- 构建流程：
  1. 安装 Rust 工具链（rustup + stable 均可）。
  2. 在仓库根目录执行：`cd Radish.Core/radish-lib && cargo build --release`（或使用 `build.sh` / `build.ps1`）。
  3. 构建产物位于 `target/release/`：`radish_lib.dll`（Windows）、`libradish_lib.so`（Linux）、`libradish_lib.dylib`（macOS）。需要拷贝到 `Radish.Api/bin/<Configuration>/net10.0/` 或发布目录以便运行期自动加载（脚本已自动复制到 Debug 输出目录）。
  4. `RustTestController` 提供 `/api/v2/RustTest/TestSum1~4` 端点，演示累加、类斐波那契、埃拉托斯特尼筛与并行质数计数；可使用 `?iterations=1_000_000` 等参数在本地验证返回结果与耗时。
- 目录规划：如未来需要把原生模块从 Core 层抽离，可迁至 `native/rust/<library>` 下维护 Cargo 工程，并附 README 说明导出函数签名/调用约定；当前以 `Radish.Core/radish-lib` 作为统一扩展库。
- 提交规范：Rust `target/` 目录与生成的 `.dll/.so/.dylib` 依旧忽略；若需要在 CI 中编译 Rust，请在构建脚本中加入 `cargo build --release` 与共享库复制步骤。

## 枚举与魔术数字规范

- 用户、角色、部门、租户等业务对象的“状态”、“类型”、“级别”等字段，禁止在代码中直接使用 `-1`、`0`、`1` 之类的魔术数字。
- 必须为这类字段定义**语义明确的枚举或常量**，统一放在 `Radish.Shared.CustomEnum`（推荐，用于跨模块/跨前后端共享）或 `Radish.Model` 中，例如：`UserStatusCodeEnum.Normal/Unknown`、`UserSexEnum.Male/Female`、`DepartmentStatusCodeEnum.Normal`、`AuthorityScopeKindEnum.Self/All`。
- 实体类中应使用枚举类型或整型字段 + 枚举映射的方式，Controller 和 Service 逻辑一律基于枚举名/常量判断，避免出现 `if (status == 1)` 这类难以理解的比较。
- 枚举命名需体现业务语义，避免 `Status0/Status1` 这类无含义命名；建议按业务维度划分命名空间，例如统一集中在 `Radish.Shared.CustomEnum` 下管理用户状态、性别、部门状态、权限范围、HTTP 状态码等。
- 数据库存储可以使用 `int` 或 `smallint`，但必须在代码层用枚举封装，并在 AutoMapper 或转换逻辑中保持枚举与整型之间的映射一致。
- 新增或修改状态码时，优先扩展枚举，而不是在各处散落新增数字常量；涉及前端时也应在前后端共享的枚举/常量文件中保持同步。

## 新增实体与字段的标准流程

> 目标：保证“以实体为真源（Code First）”，在不直接手改数据库的前提下，让开发环境与生产环境的表结构、安全地跟随代码演进。

1. **设计与建模**
   - 在 `Radish.Model` 中定义/修改实体类型（继承 `RootEntityTKey<TKey>`），补充字段、注释与 `[SugarColumn]` 等特性。
   - 若涉及状态码、类型枚举等，优先在 `Radish.Shared.CustomEnum` 下新增或扩展枚举（如 `UserStatusCodeEnum`、`UserSexEnum`、`DepartmentStatusCodeEnum`、`AuthorityScopeKindEnum`、`HttpStatusCodeEnum`）。
   - 确认前后端对外暴露的是 ViewModel/DTO，而不是直接暴露实体。

2. **代码层变更（实体/枚举）**
   - 修改实体字段时同步更新：
     - 对应的 ViewModel/Vo（位于 `Radish.Model/ViewModels`）。
     - AutoMapper 配置（位于 `Radish.Extension/AutoMapperExtension`）。
     - 相关 Service/Controller 中对该字段的使用（避免遗漏）。
   - 新增状态/类型字段时，禁止直接写 `-1/0/1`，必须通过 `Radish.Shared.CustomEnum` 中的枚举来表达语义。

3. **本地开发环境同步（InitTables / DbMigrate）**
   - 确保本地 `Radish.Api/appsettings.Local.json` 中数据库连接指向开发环境库（SQLite 或 PostgreSQL）。
   - 在仓库根目录执行：

     ```bash
     dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- init
     ```

     - 动作说明：
       - 根据当前配置创建数据库（如不存在）。
       - 扫描 `Radish.Model` 中标记了 `[SugarTable]` 的实体类型，执行 `CodeFirst.InitTables`：
         - 新表会被创建；
         - 新增字段会自动补列；
         - 不会主动删除旧字段。
   - 使用数据库客户端确认表结构是否符合预期（字段名、类型、默认值、索引等）。

4. **生成迁移 SQL（供测试/生产环境使用）**
   - 在一套“迁移基线库”（例如测试环境数据库）上，同样执行 `DbMigrate init` 让结构跟随最新实体。
   - 使用数据库自带工具或对比工具，生成**从旧版本到新版本**的结构差异 SQL：
     - 只包含必要的 `CREATE TABLE` / `ALTER TABLE` / `CREATE INDEX` 等 DDL；
     - 拆分为按版本管理的文件，例如：`deploy/sql/2025XXXX_add_user_profile_fields.sql`。
   - 将迁移 SQL 文件提交到仓库，作为版本的一部分，方便后续审查与回滚。

5. **上线前执行迁移 SQL**
   - 部署流程中，在启动新版本 API/Gateway 之前：
     - 由 DBA 或 CI/CD 流水线在目标数据库上按顺序执行本次版本对应的迁移 SQL；
     - 执行完成后，再发布/切换应用实例。
   - 生产环境**禁止**在应用启动时自动调用 `InitTables`，所有结构变更必须通过迁移脚本显式执行。

6. **数据初始化与回填（配合 DbMigrate seed）**
   - 若新增字段需要默认业务数据（例如为所有历史用户回填某个状态），建议：
     - 在迁移 SQL 中加入安全的 `UPDATE` 语句，或
     - 在 `Radish.DbMigrate` 中实现 `seed` 子命令，集中处理默认管理员、角色、租户、基础参数等数据初始化。
   - 数据初始化脚本同样应纳入版本管理，并在上线流程中显式执行。

---

## BaseService 与 BaseRepository 使用指南

### 设计理念

Radish 项目采用泛型基类模式来避免为每个实体重复编写相同的 CRUD 代码：

- **BaseRepository`<TEntity>`**：提供完整的数据库操作方法（基于 SqlSugar）
- **BaseService`<TEntity, TVo>`**：提供完整的业务层方法（自动进行实体到 ViewModel 的映射）

**核心原则**：
- ✅ **优先使用 BaseService/BaseRepository** - 减少重复代码，保持架构简洁
- ✅ **只为复杂业务逻辑创建自定义 Service** - 例如涉及多表事务、复杂验证的场景
- ❌ **避免创建只包装 Base 方法的 Service** - 这会增加不必要的抽象层

### BaseService 提供的完整功能


#### 增（Create）
```csharp
Task<long> AddAsync(TEntity entity)                     // 插入单条,返回雪花ID
Task<int> AddRangeAsync(List<TEntity> entities)         // 批量插入
Task<List<long>> AddSplitAsync(TEntity entity)          // 分表插入
```

#### 删（Delete）
```csharp
Task<bool> DeleteByIdAsync(long id)                     // 根据ID删除
Task<bool> DeleteAsync(TEntity entity)                  // 根据实体删除
Task<int> DeleteAsync(Expression<Func<TEntity, bool>>) // 根据条件删除
Task<int> DeleteByIdsAsync(List<long> ids)             // 批量删除
```

#### 改（Update）
```csharp
Task<bool> UpdateAsync(TEntity entity)                  // 更新整个实体
Task<int> UpdateRangeAsync(List<TEntity> entities)     // 批量更新
Task<bool> UpdateColumnsAsync(TEntity entity, columns)  // 更新指定列
Task<int> UpdateColumnsAsync(updateExp, whereExp)      // 根据条件更新指定列
```

#### 查（Query）
```csharp
Task<TVo?> QueryByIdAsync(long id)                     // 根据ID查询
Task<TVo?> QueryFirstAsync(Expression<...>)            // 查询第一条
Task<TVo?> QuerySingleAsync(Expression<...>)           // 查询单条（多条抛异常）
Task<List<TVo>> QueryAsync(Expression<...>)            // 条件查询列表
Task<List<TVo>> QueryWithCacheAsync(Expression<...>)   // 带缓存的查询
Task<(List<TVo>, int)> QueryPageAsync(...)            // 分页查询（支持排序）
Task<int> QueryCountAsync(Expression<...>)             // 查询数量
Task<bool> QueryExistsAsync(Expression<...>)           // 判断是否存在
Task<List<TResult>> QueryMuchAsync<...>(...)          // 三表联查
Task<List<TEntity>> QuerySplitAsync(...)              // 分表查询
```


### 使用场景


#### 场景 1：简单 CRUD（直接使用 BaseService）

✅ **推荐做法**：

```csharp
// Controller
public class CategoryController : ControllerBase
{
    private readonly IBaseService<Category, CategoryVo> _categoryService;

    public CategoryController(IBaseService<Category, CategoryVo> categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<MessageModel> GetTopCategories()
    {
        var categories = await _categoryService.QueryAsync(
            c => c.ParentId == null && c.IsEnabled && !c.IsDeleted);
        return new MessageModel { IsSuccess = true, ResponseData = categories };
    }

    [HttpGet("{id:long}")]
    public async Task<MessageModel> GetById(long id)
    {
        var category = await _categoryService.QueryByIdAsync(id);
        if (category == null)
            return new MessageModel { IsSuccess = false, MessageInfo = "分类不存在" };
        return new MessageModel { IsSuccess = true, ResponseData = category };
    }
}
```

#### 场景 2：复杂业务逻辑（创建自定义 Service）

❌ **不推荐**：创建只包装 BaseService 方法的 Service

```csharp
// 不要这样做！
public interface ICategoryService : IBaseService<Category, CategoryVo>
{
    Task<List<CategoryVo>> GetTopCategoriesAsync(); // 只是 QueryAsync 的包装
}
```

✅ **推荐做法**：当有复杂业务逻辑时才创建自定义 Service

```csharp
// Service 接口
public interface IPostService : IBaseService<Post, PostVo>
{
    // 复杂业务方法：发布帖子需要更新分类计数、处理标签等
    Task<long> PublishPostAsync(Post post, List<string>? tagNames = null);

    // 复杂查询：需要关联查询分类名称、标签列表
    Task<PostVo?> GetPostDetailAsync(long postId);
}

// Service 实现
public class PostService : BaseService<Post, PostVo>, IPostService
{
    private readonly IBaseRepository<Category> _categoryRepository;
    private readonly IBaseRepository<Tag> _tagRepository;
    private readonly IBaseRepository<PostTag> _postTagRepository;
    private readonly ITagService _tagService;

    public PostService(
        IMapper mapper,
        IBaseRepository<Post> baseRepository,
        IBaseRepository<Category> categoryRepository,
        IBaseRepository<Tag> tagRepository,
        IBaseRepository<PostTag> postTagRepository,
        ITagService tagService)
        : base(mapper, baseRepository)
    {
        _categoryRepository = categoryRepository;
        _tagRepository = tagRepository;
        _postTagRepository = postTagRepository;
        _tagService = tagService;
    }

    public async Task<long> PublishPostAsync(Post post, List<string>? tagNames = null)
    {
        // 1. 插入帖子（复用 BaseService 方法）
        var postId = await AddAsync(post);

        // 2. 更新分类的帖子数量
        if (post.CategoryId > 0)
        {
            var category = await _categoryRepository.QueryByIdAsync(post.CategoryId);
            if (category != null)
            {
                category.PostCount++;
                await _categoryRepository.UpdateAsync(category);
            }
        }

        // 3. 处理标签关联
        if (tagNames != null && tagNames.Any())
        {
            foreach (var tagName in tagNames.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                var tag = await _tagService.GetOrCreateTagAsync(tagName);
                var postTag = new PostTag(postId, tag.Id);
                await _postTagRepository.AddAsync(postTag);

                tag.PostCount++;
                await _tagRepository.UpdateAsync(tag);
            }
        }

        return postId;
    }

    public async Task<PostVo?> GetPostDetailAsync(long postId)
    {
        var post = await QueryByIdAsync(postId); // 复用 BaseService 方法
        if (post == null) return null;

        // 补充关联信息
        var category = await _categoryRepository.QueryByIdAsync(post.CategoryId);
        if (category != null) post.CategoryName = category.Name;

        // ... 其他复杂逻辑
        return post;
    }
}
```


### BaseRepository 直接使用

在自定义 Service 中，如果需要操作其他实体，可以直接注入 `IBaseRepository<T>`：


```csharp
public class PostService : BaseService<Post, PostVo>, IPostService
{
    private readonly IBaseRepository<Category> _categoryRepository; // 直接使用 BaseRepository

    public async Task SomeBusinessLogic()
    {
        // 直接调用 BaseRepository 方法
        var category = await _categoryRepository.QueryByIdAsync(123);
        category.PostCount++;
        await _categoryRepository.UpdateAsync(category);
    }
}
```


### 最佳实践总结

1. **默认使用 BaseService** - Controller 直接注入 `IBaseService<TEntity, TVo>`
2. **谨慎创建自定义 Service** - 只在以下情况创建：
   - 需要操作多个实体（事务协调）
   - 有复杂的业务验证逻辑
   - 需要调用外部服务/API
3. **自定义 Service 继承 BaseService** - 复用基础方法，只添加特殊逻辑
4. **命名规范**：
   - 接口：`IPostService : IBaseService<Post, PostVo>`
   - 实现：`PostService : BaseService<Post, PostVo>, IPostService`

---

## 实体与视图模型规范


- 仓储层（Radish.Repository）只处理 `Radish.Model` 中定义的实体类型，禁止将实体对象直接向外暴露；Service 层获取实体后必须映射为视图模型再返回给 Controller。
- 视图模型命名以 `Vo` 为前缀，但不得只追加单个前缀；需结合业务含义进行缩写或扩写（例如 `VoUsrAudit`、`VoAssetReport`），从命名上进行模糊化以减少被直接猜测的风险。
- AutoMapper Profile 中维护实体与视图模型的对应关系；如需手动映射，也必须在 Service 层完成，确保 Controller 不访问实体。
- DTO/Vo 定义集中在 `Radish.Model` 或 `Radish.Shared` 的对应目录下，提交前请自检实体与视图模型字段是否同步更新。

## AutoMapper 与配置扩展

- `Radish.Extension/AutoMapperSetup` 负责集中注册全部 profile，并通过 `expression.ConstructServicesUsing` 使用 DI 容器解析依赖；新增 profile 时直接在 `AutoMapperConfig.RegisterProfiles` 中挂载。
- `AutoMapperConfig.RegisterCustomProfile` 可以配置全局规则（例如识别属性前缀/后缀、通用转换器），之后的 `RegisterProfiles` 列表挂载业务 profile（`RoleProfile`、`UserProfile`、`AuditSqlLogProfile` 等）；保持两段式注册可以避免 profile 顺序造成的耦合。
- `AuditSqlLogProfile` 已示范如何同时识别源和目标的 `Vo` 前缀：`RecognizeDestinationPrefixes("Vo")` + `RecognizePrefixes("Vo")` 使 `AuditSqlLog` 与 `AuditSqlLogVo` 能够双向自动映射，开发其他 Vo 模型时直接复用该写法即可。
- AutoMapper 授权：
  - 在 `appsettings.{Environment}.json` 中新增 `AutoMapper:LicenseKey`，严禁提交真实 key，可通过用户密钥或 Secret Manager 注入。
  - 运行时通过 `AppSettings.RadishApp(new[] { "AutoMapper", "LicenseKey" }).ObjToString()` 读取，并在 `expression.LicenseKey` 上设置；为空时自动跳过，避免影响本地调试。
- `Radish.Common.AppSettings` 为自定义配置入口，Program.cs 使用 `builder.Services.AddSingleton(new AppSettings(builder.Configuration));` 注册后即可在任何层注入/静态调用。
  - 当需要分段读取配置时，统一调用 `AppSettings.RadishApp(params string[] sections)`，禁止在业务代码中自行 new ConfigurationBuilder，以保证配置来源一致。
  - 强类型配置建议实现 `IConfigurableOptions`（位于 `Radish.Common.Option.Core`），由 `ConfigurableOptions` + `AllOptionRegister` 自动绑定，再通过 `IOptions<T>` 注入。
- `Radish.Common.Core.App` 负责缓存 `Configuration/WebHostEnvironment/RootServices`，Program.cs 必须依次执行 `hostingContext.Configuration.ConfigureApplication()`、`builder.ConfigureApplication()`、`app.ConfigureApplication()` 与 `app.UseApplicationSetup()` 才能完成注入；仅在无法通过构造函数注入时，才在静态上下文调用 `App.GetService<T>`、`App.GetOptions<T>`，并优先保证线程安全与生命周期一致。
- 对应扩展支持 `Get<T>()`、`ObjToString()` 等常用方法，可在新增配置时同步补充注释，方便多人协作。

## 缓存策略

- `Radish.Extension.RedisExtension.CacheSetup` 提供统一的 `AddCacheSetup()`，需在 Program 的服务注册阶段调用；该方法会读取 `Redis.Enable`/`Redis.ConnectionString`/`Redis.InstanceName` 并自动在 StackExchange.Redis 与内存缓存之间切换。
- 若 `Redis.Enable=true`，启动时会构建单例 `IConnectionMultiplexer` 并注册 `IRedisBasketRepository`（包含 List/Queue 等常用 API），其他场景使用 `Radish.Common.CacheTool.ICaching` 进行增删查；禁用 Redis 时自动回落到 `IMemoryCache` + `IDistributedMemoryCache`。
- 所有新增缓存键应通过 `ICaching.AddCacheKey*` 记录，便于 `DelByPattern`/`Clear` 等工具批量清理；若需发布订阅或队列语义，请依赖 `IRedisBasketRepository`，不要在业务代码中自行 new 连接。
- 请勿将生产 Redis 连接串直接写入仓库，示例配置仅用于本地调试，线上需要通过环境变量或 Secret Manager 注入。

## AOP 与日志

- `Radish.Extension/ServiceAop` 基于 Castle.DynamicProxy 实现接口拦截，当前主要用于捕捉 `BaseService<,>` 等应用服务的入参、响应与耗时信息，并通过 `AopLogInfo` 统一结构化输出。
- 常规业务日志统一调用 Serilog 静态方法（`Log.Information/Log.Warning/Log.Error` 等），除非框架/第三方库必须注入 `ILogger<T>` 才能工作，否则 Controller、Service 以及扩展类中都不再手动注入 `ILogger`，避免出现多套日志通道。
- `AutofacModuleRegister` 已在泛型服务注册时启用 `.EnableInterfaceInterceptors().InterceptedBy(ServiceAop)`，如果后续服务需要自定义拦截，可在同一位置扩展拦截器数组。
- `Radish.Common/AopLogInfo` 集中维护 AOP 日志字段，调用层仅负责填充必要属性并交给日志基础设施处理，避免在各服务中手写日志模型。

## 项目依赖约定

- 前端依赖管理使用 npm 或 yarn，推荐使用 yarn 以提高安装速度和一致性
- 后端依赖管理使用 NuGet，推荐使用最新稳定版本的包（兼容 .NET 10）
- 所有第三方依赖需经过安全审查，确保无已知漏洞
- 定期更新依赖包，保持项目安全和性能
- 使用依赖锁定文件（如 package-lock.json 或 yarn.lock）确保团队成员使用相同版本的依赖
- 避免使用过时或不再维护的库，优先选择社区活跃且有良好文档支持的库
- 对于大型依赖，考虑使用按需加载或代码拆分以优化性能
- 记录所有依赖变更，确保团队成员了解更新内容和影响

## 示例实现与测试约定

- `Radish.Api/Controllers/UserController`、`Radish.Service/UserService`、`Radish.Repository/UserRepository` 与对应的接口项目组成“用户列表”示例。该示例演示分层调用方式和 DI 注册写法，亦为 Swagger/.http 文件中的演示请求提供数据。
- 示例仓储目前返回内存中的 Alice/Bob 两条静态数据，供开发联调、单元测试与文档说明复用；如需扩展，请保持“接口契约 + 数据映射 + 控制器”结构。
- `Radish.Api.Tests/Controllers/UserControllerTest` 使用 xUnit 校验示例接口返回 `OkObjectResult`，且至少包含两条用户数据；任何对示例链路的改动都必须同步更新该测试。
- `Radish.Api/Radish.Api.http` 已加入“用户列表”调用示例，可在调试时直接复用；如新增演示接口，应当在 .http 文件中同步记录。

## SignalR 实时交互规范

- Hub 位置：所有实时交互入口放在 `Radish.Api/Hubs`（命名为 `*Hub.cs`），继承 `Hub<TClient>` 以启用强类型调用；公共消息 DTO 定义在 `Radish.Model` 中，避免前后端字段不一致。
- DI 与调用：业务服务通过注入 `IHubContext<T>` 或 `IHubContext<T, TClient>` 发送消息，禁止在 Controller 中直接 new Hub；需要跨层推送时在 Service 层聚合，保持仓储层不涉及实时推送。
- 客户端：`radish.client` 使用 `@microsoft/signalr`，连接封装在 `shared/signalr/useSignalrHub.ts`（预留）中，负责断线重连、心跳与 Token 附带；前端只暴露订阅型 API。
- 安全：Hub 仅接受已认证用户，连接时附带 JWT（Query 或 Header），后台在 `OnConnectedAsync` 验证租户/角色；Hub 方法命名遵循 PascalCase，禁止接受动态字符串并在方法内再次鉴权。
- 可扩展性：生产部署多实例时启用 Redis Backplane，并限制客户端分组订阅；压测期间通过 `MaximumReceiveMessageSize` 控制 payload，必要时启用 MessagePack 序列化。
- 调试：添加 `Radish.Api/Hubs/hubs.http` 或在现有 `.http` 文件中记录 `/negotiate` 请求；本地开发启用 `builder.Services.AddSignalR().AddJsonProtocol(...)` 并结合浏览器 Network 面板排查。

## 前后端通信安全要求

- 所有前后端数据交互必须通过 HTTPS 进行，禁止使用明文 HTTP，确保传输链路具备 TLS 加密。
- 登录、密码重置、密保验证等含有敏感字段的请求，客户端需先使用项目约定的 RSA 公钥加密敏感参数，后端使用私钥解密，再进入业务流程。
- 若同时涉及本地缓存或离线存储，需确保不会以明文形式存储账号、密码、令牌等敏感信息。
- 公私钥对统一由后端团队生成和轮换，前端需在构建阶段或运行时安全加载最新公钥。
- 开发联调阶段同样遵循 HTTPS+RSA 约束，以防调试阶段泄漏敏感数据。

## 国际化（i18n）约定

- 支持语言固定为 `zh`（简体中文）与 `en`（英语），默认使用 `zh`，统一使用中性语言代码，所有多语言扩展（如 `zh-TW`、`en-GB`）在此基础上继续演进。
- 前端（radish.client）负责界面文案、交互提示等 UI 文本的多语言管理，统一使用 `react-i18next`：
  - 所有新页面禁止写死中文/英文字符串，必须通过 `t('app.title')`、`t('auth.login')` 等 key 访问文案。
  - i18n key 采用“小写 + 点号分隔”的层级命名：`{domain}[.{subDomain}].{meaning}`，推荐顶级域包括：`app.*`、`auth.*`、`user.*`、`weather.*`、`oidc.*`、`error.*`、`info.*`、`lang.*` 等。
  - 详细示例与最佳实践见 [I18nGuide.md](I18nGuide.md) 的“前端国际化规范”。
- 后端（Radish.Api / Radish.Auth）负责错误/提示类系统消息的多语言资源管理：
  - `.resx` 资源文件统一命名为 `Errors.<culture>.resx`，使用中性语言代码后缀：
    - API：`Radish.Api/Resources/Errors.zh.resx`、`Errors.en.resx`
    - Auth：`Radish.Auth/Resources/Errors.zh.resx`、`Errors.en.resx`（可选 `Errors.resx` 作为默认）
    - 通过 `IStringLocalizer<Errors>` 访问。
  - 在各宿主 `Program.cs` 中使用 `RequestLocalizationOptions + UseRequestLocalization`，按 HTTP 头 `Accept-Language` 或 QueryString 选择当前 `CultureInfo`：
    - 支持文化固定为 `zh` 与 `en`，`DefaultRequestCulture = \"zh\"`。
    - API：优先使用 `Accept-Language` 头（插入 `AcceptLanguageHeaderRequestCultureProvider` 至首位）。
    - Auth：优先顺序为 Query String → Cookie → `Accept-Language`，方便登录页切换语言。
- API 响应统一使用 `MessageModel<T>`/`MessageModel` 三件套结构：
  - 对外字段固定为：`StatusCode` + `IsSuccess` + `MessageInfo` + `Code?` + `MessageKey?` + `ResponseData?`。
  - `Code`：业务错误码，面向日志与前端精细化判断，采用大驼峰命名（如 `Auth.InvalidCredentials`、`User.NotFound`、`Weather.LoadFailed`）。
  - `MessageKey`：与 `.resx` 和前端 i18n 映射的 key，沿用 `error.*` / `info.*` 命名规范（如 `error.auth.invalid_credentials`）。
  - `MessageInfo`：按当前文化翻译后的完整提示句子，由后端本地化组件生成，作为前端兜底展示内容。
- 前端发起 HTTP 请求时必须携带 `Accept-Language` 头：
  - 统一通过封装的 `apiFetch()` / `requestJson<T>()` 等 helper 写入 `Accept-Language: i18n.language || 'zh'`，避免在页面中分散设置（当前示例实现见 `radish.client/src/App.tsx`）。
  - 前端解析响应时优先使用 `messageKey` 做 i18n 映射，仅在 key 缺失或解析失败时回退到 `MessageInfo`。
- 所有新接口、新页面默认遵循上述统一模式：Controller 返回 `MessageModel<T>`，通过 `IStringLocalizer<Errors>` 提供多语言文案，前端使用 `requestJson<T>` + `parseApiResponse` 完成解析与提示。

## 前端桌面化 UI 规范

> 详细的交互、设计 Token、跨端策略以 [frontend/design.md](../frontend/design.md) 为准，此处保留关键守则，便于与后端规范并列查看。

- radish.client 以桌面模式为核心交互范式，首页加载后呈现类似 macOS 的桌面界面。
- 顶部为状态栏，需显示当前登录用户名、IP 地址以及预留系统状态信息区域。
- 底部为 Dock 栏，用于承载核心功能快捷入口；点击或双击图标都应维持桌面操作逻辑和动效。
- 左侧屏幕区域展示社区功能图标，用户双击后需弹出大弹窗（模态窗口）显示对应功能内容。
- 弹窗左上角包含最小化与关闭按钮，交互样式参考 macOS；最小化后回到 Dock 或桌面图标，关闭后释放资源。
- 状态栏、Dock、桌面图标以及弹窗需要统一的外观主题和响应式策略，优先适配桌面端分辨率，对移动端访问给出限缩体验或引导。
- 参考实现：`radish.client/public/webos.html`（Nebula OS 示例）展示了开机过渡、可拖拽窗口、多任务任务栏、Start 菜单与预置 Terminal/Editor/Browser 等应用图标；任何新页面都应优先在该场景中定义窗口外观与交互再迁移至 React 组件，保持动画节奏、图标尺寸与控件布局一致。

## radish.client 前端规范

> 技术栈、目录结构、状态管理、测试策略等完整说明见 [frontend/design.md](../frontend/design.md)，以下列出与后端协作密切的约束。

1. 前后端传输的敏感字段计划使用 `encryptByPublicKey()` 加密，后端通过 `DecryptByPrivateKey()` 解密；方法虽未实现，但需提前规划调用点，保证接口兼容性。
2. 组件统一使用函数式写法，结合 `useState`、`useMemo`、`useEffect` 等 Hook 管理状态与生命周期，避免继续编写 Class 组件。
3. 使用 `const` 定义组件与内部函数，遵循 React 不可变范式，常规情况下避免 `function` 声明以减少作用域、提升与 `this` 绑定问题。
4. 禁用 `var`，默认 `const`，仅在确需重新赋值时选用 `let`；在 React 顶层逻辑几乎无需 `let`，如需持久化可变值，优先用 `const + useState` 组合处理。
5. React Compiler 仍属实验功能，主干暂不启用；若官方 GA 后可在独立分支尝试 `babel-plugin-react-compiler`，确认与 `radish.client` 的桌面化 UI、第三方组件兼容且能明显减少手写 memo，再提交 PR 合入主线。

## Radish.Api 接口规范

1. API 方法全部位于 Server 层 Controller 命名空间，统一使用 `[Route("api/[controller]/[action]")]` 作为路由前缀。
2. 需鉴权的 API 必须在 Controller 或 Action 上添加 `[Authorize(Permissions.Name)]`；无需鉴权的显式标注 `[AllowAnonymous]`，避免默认放行。
3. Controller Action 默认遵循 `[Produces("application/json")]` 与 RESTful 设计原则，除非业务场景要求其他内容类型或风格。
4. WeatherForecastController 中的 `GetById` 演示了 `[HttpGet("{id}")]` 路径参数写法，访问 `api/WeatherForecast/GetById/1` 可直接验证模型绑定；后续新增路由参数时沿用该示例的命名、注释和返回格式，便于单测覆盖。
5. 实体类存放在 Model 层 `Models` 命名空间并继承 `RootEntityTKey<TKey>`（含主键 Id）；若有自定义外键实体，同样继承该基类。视图模型位于 `ViewModels` 命名空间，按对外暴露字段设计，无继承硬性要求。
6. 实体与视图模型的映射集中在 Extension 层 `AutoMapper` 命名空间，每组实体定义独立 `CustomProfile`，避免在 Controller 或仓储中手动映射。
7. 新增对外接口遵循以下流程：
   （1）在 Model 层定义实体与视图模型；
   （2）在 Extension 层配置映射关系；
   （3）在 Core 层实现算法或数据处理；
   （4）在 IService 层声明接口；
   （5）在 Service 层实现接口，注入 `IBaseRepository` 或专用仓储并完成映射；
   （6）（可选）若泛型仓储不满足需求，在 IRepository/Repository 层定义并实现专用仓储；
   （7）在 Server 层通过依赖注入调用 IService，完成数据返回或存储。
8. Server 层对外暴露的 Controller 禁止直接注入 `IBaseRepository` 或任何业务仓储，所有数据访问需经由 Service 层封装。
9. 角色-API 授权依赖 `ApiModule.LinkUrl` 进行正则匹配，因此 URL 必须以 `/` 开头；若 Action 路由包含路径参数（如 `[HttpGet("{id:long}")]`），请在 `LinkUrl` 中写入对应的正则版本（示例：`/api/User/GetById/\d+`），否则 `RadishAuthPolicy` 无法命中该路由。

## API 版本控制规范

### 版本控制策略

- Radish 采用 **URL 路径版本控制**方式，这是业界标准且最直观的版本管理方案（GitHub、Stripe、Twitter 等主流 API 均采用此方案）。
- 版本号格式为 `v{major}.{minor}`，在 URL 路径中以 `v{major}` 形式体现（如 `v1`、`v2`）。
- 所有 API 路由必须包含版本号：`/api/v{version}/{controller}/{action}`。
- 使用 `Asp.Versioning.Mvc` (8.1.0) 和 `Asp.Versioning.Mvc.ApiExplorer` 提供版本控制支持。

### Controller 版本声明

每个 Controller 必须明确声明其所属的 API 版本：

```csharp
[ApiController]
[ApiVersion(1)]  // 声明为 v1 版本
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("功能模块")]
public class MyController : ControllerBase
{
    // Actions...
}
```

**关键特性说明：**
- `[ApiVersion("1.0")]`: 声明 Controller 支持的版本，可同时声明多个版本
- `v{version:apiVersion}`: 路由模板中的版本占位符，运行时自动替换为实际版本号
- 版本号采用 Major.Minor 格式（如 1.0, 2.0）
- URL 中显示为 v1, v2（由 `GroupNameFormat = "'v'VVV"` 配置）

### 版本划分原则

**v1 (稳定版本)：**
- 核心业务接口：用户管理、认证授权等
- 已发布到生产环境的稳定 API
- 必须保持向后兼容，不得破坏性变更
- 示例：LoginController、UserController

**v2 及更高版本（预览/实验版本）：**
- 新功能接口：系统管理、性能测试等
- 实验性功能或重大重构
- 可包含破坏性变更
- 示例：AppSettingController (v2)、RustTestController (v2)

### OpenAPI 文档配置

**自动文档生成：**
- 系统通过 `IApiVersionDescriptionProvider` 自动发现所有 API 版本
- 为每个版本动态生成独立的 OpenAPI 文档（`/openapi/v1.json`, `/openapi/v2.json`）
- Scalar UI 自动展示版本选择下拉菜单，切换版本时文档内容自动过滤

**文档配置位置：**
- `Radish.Extension/OpenApiExtension/ScalarSetup.cs` 负责统一配置
- 每个版本的文档描述、示例、服务器列表均可独立定制
- 支持版本弃用标记（在 `Info.Description` 中添加警告）

**Scalar UI 版本切换：**
```csharp
// 在 ScalarSetup.cs 中自动配置
options
    .AddDocument("v1", "V1", "/openapi/v1.json", isDefault: true)
    .AddDocument("v2", "V2", "/openapi/v2.json");
```

### XML 注释规范

所有 Controller 和 Action 必须提供完整的 XML 注释：


```csharp
/// <summary>
/// 功能简述
/// </summary>
/// <param name="paramName">参数说明</param>
/// <returns>返回值说明</returns>
/// <remarks>
/// 详细说明，包括：
/// - 业务逻辑
/// - 请求示例
/// - 响应示例
/// - 注意事项
/// </remarks>
/// <response code="200">成功响应说明</response>
/// <response code="401">未授权说明</response>
[HttpGet]
[ProducesResponseType(typeof(MessageModel<T>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(MessageModel), StatusCodes.Status401Unauthorized)]
public async Task<MessageModel<T>> MyAction(string paramName)
```


**关键要求：**
- 必须启用 XML 文档生成（在 `.csproj` 中配置 `<GenerateDocumentationFile>true</GenerateDocumentationFile>`）
- 使用 `[ProducesResponseType]` 明确声明所有可能的响应状态码
- 使用 `[Tags]` 对 API 进行分组（如"认证管理"、"用户管理"、"系统管理"）
- 为 v2+ 版本在注释中标注 "(v2)" 等版本标识

### 版本迁移指南

**添加新版本：**
1. 创建新的 Controller 或复制现有 Controller
2. 修改 `[ApiVersion]` 特性为新版本号（如 "2.0"）
3. 更新 XML 注释说明版本差异
4. 在 `ScalarSetup.cs` 中为新版本添加专属描述

**弃用旧版本：**


```csharp
[ApiVersion("1.0", Deprecated = true)]
```


- 添加 `Deprecated = true` 标记
- OpenAPI 文档会自动显示"已弃用"警告
- 保留至少一个完整版本周期再移除

**跨版本支持：**


```csharp
[ApiVersion(1)]
[ApiVersion(2)]
public class MyController : ControllerBase
```


- Controller 可同时支持多个版本
- 使用 `[MapToApiVersion("2.0")]` 标记特定 Action 的版本

### 版本控制配置（Program.cs）


```csharp
builder.Services.AddApiVersioning(options =>
{
    options.ReportApiVersions = true;  // 响应头报告支持的版本
    options.AssumeDefaultVersionWhenUnspecified = true;  // 未指定版本时使用默认版本
    options.DefaultApiVersion = new ApiVersion(1);  // 默认版本 1
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";  // URL 中显示为 v1, v2
    options.SubstituteApiVersionInUrl = true;  // 自动替换路由中的版本占位符
});
```


### URL 格式示例

**v1 接口：**
```
GET /api/v1/Login/GetJwtToken?name=admin&pass=123456
GET /api/v1/User/GetUserList
GET /api/v1/User/GetUserById/123456789
```

**v2 接口：**
```
GET /api/v2/AppSetting/GetRedisConfig
GET /api/v2/RustTest/TestSum1?iterations=1000000
```

### 版本响应头

启用 `ReportApiVersions` 后，响应头会包含版本信息：

```
api-supported-versions: 1.0, 2.0
api-deprecated-versions: (空或已弃用的版本)
```

### 文档访问

- **Scalar UI**: `https://localhost:5000/scalar`（本机直连：`http://localhost:5100/scalar`）
- **OpenAPI JSON**:
  - v1: `http://localhost:5100/openapi/v1.json`（或经 Gateway：`https://localhost:5000/openapi/v1.json`）
  - v2: `http://localhost:5100/openapi/v2.json`（或经 Gateway：`https://localhost:5000/openapi/v2.json`）

### 常见问题

**Q: 是否可以不指定版本？**
A: 不推荐。虽然系统会使用默认版本 (v1)，但显式指定版本更清晰。

**Q: 如何在权限表中配置版本化 URL？**
A: `ApiModule.LinkUrl` 中必须包含版本号的正则，例如：`/api/v1/User/GetUserList` 或 `/api/v\d+/User/GetUserList`。

**Q: 前端如何调用不同版本？**
A: 直接修改 URL 路径中的版本号即可，无需修改请求头或参数。

**Q: 是否支持查询参数或请求头版本控制？**
A: 系统支持但不推荐。URL 路径版本控制是最佳实践，工具支持最好，用户体验最佳。
