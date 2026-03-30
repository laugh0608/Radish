# 2026-03 第五周 (03-30)

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

### 本轮验证与已知约束

- ✅ 根目录临时 `.log` 文件已清理，避免工作区视觉噪音继续干扰排障。
- ✅ 登录链路治理代码与文档口径当前已完成一轮同步收口，可继续进入人工回归。
- ✅ `dotnet build Radish.slnx -c Debug` 已在宿主环境构建通过，当前为 `0 error`。
- ⚠️ 构建仍带有仓库内既有 warning，当前这轮收口未顺带清理存量告警。
