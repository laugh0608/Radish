# 2026-03 第五周 (03-30)

## 2026-03-31 (周二)

### 论坛首屏慢链路治理

- **论坛首页请求链路已收敛**：`radish.client` 当前已去掉论坛首页数据加载中的前端串行阻塞，分类、标签与帖子列表不再被无谓串行等待放大成整页白屏。
- **帖子列表已补专用查询路径**：帖子列表当前改走专用查询与投影路径，分页阶段不再把长文本正文一并拖入 SQLite 查询，首屏列表读取压力已明显收敛。
- **论坛热点索引已补自愈**：`DbMigrate` 当前已补论坛热点相关索引的自愈入口，旧库执行 `apply` 后可自动补齐，避免历史 SQLite 库继续拖慢首屏分类 / 标签 / 列表读取。
- **日志序列化异常已同步收敛**：围绕 `serializer.Serialize(writer, CreateSummary(typedValue));` 的日志序列化异常当前已补收敛处理，避免排障期继续被同类异常刷屏。

### 日志系统行为修复

- **文件日志已恢复按宿主项目分目录输出**：`LogContextTool` 当前会优先通过 `ContentRootPath` 与 `AppContext.BaseDirectory` 解析更具体的项目名，避免开发态再次把 `Radish.Api`、`Radish.Auth`、`Radish.Gateway` 统一误判成 `Radish` 并写入同一个日志目录。
- **日志库口径已明确仍为共享 `Log` 库**：当前数据库日志设计仍为共享 `ConnId=Log`，配合 `InformationLog / WarningLog / ErrorLog / AuditSqlLog / AuditLog` 按类型分表，不是按宿主项目拆分日志库。
- **`SkipTables` 对插入场景已真正生效**：`SqlSugarSetup.ExtractTableName(...)` 当前已补齐 `INSERT INTO` 的表名提取，`WikiDocument` / `WikiDocumentRevision` 不会再因为插入语句拿不到表名而刷出整段 SQL。
- **SQL AOP 已避免模板占位冲突**：SQL 日志当前改为结构化参数写入，参数值中即使包含 `{userId}` 这类花括号文本，也不会再触发 Serilog SelfLog 的 `Required properties not provided` 异常。

### 协作与提交流程治理

- **多入口协作文件约束已对齐**：`AGENTS.md`、`CLAUDE.md`、`GEMINI.md` 当前已统一补入“同类协作文件保持基本复制与长期同步”的约束，避免多入口协作规范后续分叉。
- **PR 模板已落地**：仓库当前已补 `.github/PULL_REQUEST_TEMPLATE.md`，并按现阶段主线、验证基线与 `DbMigrate` 影响检查收口。
- **分支与 PR 治理 ADR 已补齐**：新增 `Docs/adr/0001-branch-and-pr-governance.md`，统一沉淀 `master / dev` 角色、默认 PR 流向、规则来源与长期执行边界。

## 2026-03-30 (周一)

### 认证登录链路稳定性治理

- **`Account/Login` 已收口为登录名精确查询**：登录入口不再走“列表查询后再取第一条”的路径，当前统一改为 `GetEnabledUserByLoginNameAsync(...)`，并明确只命中 `IsEnable && !IsDeleted` 的可登录用户。
- **登录页已补防重复提交**：用户点击登录后，提交按钮会立即禁用并切换为“登录中...”，避免网络慢或数据库等待时重复点击放大并发登录请求。
- **用户登录复合索引已落地并可自愈**：`User` 当前新增 `idx_user_login_active(TenantId, LoginName, IsDeleted, IsEnable)`，`DbMigrate init/seed/apply` 会对旧库自动补齐，避免历史 SQLite 库继续缺少登录热点索引。
- **SQLite 连接初始化与慢链路观测已补齐**：`SqlSugarSetup` 当前会在连接已打开时补 `busy_timeout=60000`、`synchronous=NORMAL`，并对每个数据库文件首次切到 `journal_mode=WAL`；`SqlSugarAop` 也已补慢连接、慢查询、慢命令与数据库异常日志。
- **编译冲突已顺手收口**：`SqlSugarSetup.cs` 中的 `DbType` 当前已显式限定为 `SqlSugar.DbType`，不再与 `System.Data.DbType` 产生二义性编译错误。

### 文档口径同步

- **认证文档已补排障说明**：`guide/authentication.md` 当前已明确 `/Account/Login` 属于 OIDC 之前的业务登录入口，并补充后台闲置恢复、重复提交与旧 SQLite 库排障顺序。
- **日志文档已补当前实现细节**：`guide/logging.md` 当前已补 SQLite 连接初始化、慢连接 / 慢查询 / 慢命令阈值与登录链路排障建议。
- **当前规划页已同步收口**：`planning/current.md` 当前已把登录链路稳定性治理纳入“当前输出”，后续重点改为手工回归后台闲置恢复、重复登录与慢日志收敛情况。

### 固定文档启动同步守卫

- **启动同步前已补 Wiki 表就绪检查**：`Radish.Api` 当前会在执行固定文档启动同步前先确认 `WikiDocument / WikiDocumentRevision` 是否存在；若主库仍未完成 `DbMigrate`，则改为输出“已跳过，请先执行 DbMigrate apply”的信息日志，不再把一次性缺表时序问题刷成错误。
- **已补纯单元测试覆盖跳过条件**：当前已为“关闭固定文档展示”“缺少 Wiki 表”“Wiki 表已就绪”三类分支补回归测试，避免后续又把启动守卫改丢。

### 本轮验证与已知约束

- ✅ 根目录临时 `.log` 文件已清理，避免工作区视觉噪音继续干扰排障。
- ✅ 登录链路治理代码与文档口径当前已完成一轮同步收口，可继续进入人工回归。
- ✅ `dotnet build Radish.slnx -c Debug` 已在宿主环境构建通过，当前为 `0 error`。
- ⚠️ 构建仍带有仓库内既有 warning，当前这轮收口未顺带清理存量告警。
