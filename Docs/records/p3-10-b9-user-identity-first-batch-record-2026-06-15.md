# P3-10-B9 用户身份语义首批记录（2026-06-15）

> 本记录承接 [用户身份语义与公开索引](/architecture/user-identity-semantics) 与 [P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture)。
>
> 执行时间：2026-06-15 22:13 CST（Asia/Shanghai）

## 批次结论

`P3-10-B9 用户身份语义与公开索引` 首批代码已完成，核心目标是把私有登录凭证、公开展示句柄、公开搜索和 Console 排障语义拆开。本批不合并 B8，不继续电子宠物 Phase C，也不启动数据库主键迁移、邮箱通知系统、ActivityPub / WebFinger 或 Console 高风险账号字段治理。

本批新增 `User.PublicIndex`，公开展示句柄统一派生为 `DisplayName#PublicIndex`；注册登录名和邮箱规范化为小写，登录支持登录名或邮箱；公开资料、榜单、关系链、艾特搜索、聊天 / 论坛提及和 Console 用户排障展示开始使用公开展示名 / 公开句柄。

自动化验证已通过。Gateway 真实页面 smoke 当前未闭合：沙盒内与提权后 `check:host-runtime` 结果一致，API/Auth 端口未监听，`5000` 由 macOS `ControlCe` 占用并超时。待开发者恢复 Gateway / API / Auth 后，再补 PC `1920x1080` 与移动 `390x844` 页面抽查。

## 覆盖矩阵

| 验证项 | 本轮覆盖 | 结论 |
| --- | --- | --- |
| 模型与 DTO | `User.PublicIndex`、`UserVo`、公开资料、关系链、榜单、艾特搜索 DTO 补齐公开索引与公开句柄 | 通过 |
| 注册 / 登录 | 注册登录名和邮箱统一小写，邮箱必填，登录名规则收紧；登录服务支持登录名或邮箱 | 通过 |
| 公开资料与榜单 | 公开个人页、公开结构化数据、公开榜单和工作台榜单展示公开展示名 / 公开句柄 | 通过 |
| 关系链与提及 | 关注 / 粉丝、聊天提及、论坛提及、转账用户搜索使用公开句柄，不把登录名 / 邮箱作为普通用户搜索字段 | 通过 |
| Console 排障 | Console 用户列表 / 详情展示公开句柄，同时保留登录名 / 邮箱作为管理员排障信息 | 通过 |
| 迁移入口 | `DbMigrate` 自动补列、回填、种子保留索引纠偏和唯一索引；`Deploy/sql/20260615_add_user_public_index.sql` 作为 PostgreSQL 部署差异 SQL 审核入口 | 通过 |
| Gateway 页面 | 当前宿主未处于可联调状态，未执行页面 smoke | 待补 |

## 自动化执行

执行目录：仓库根目录 `/Users/luobo/Code/Radish`

```bash
dotnet test Radish.Api.Tests --filter "FullyQualifiedName~UserIdentitySemanticsServiceTest|FullyQualifiedName~UserFollowServiceTest|FullyQualifiedName~TenantIsolationRegressionTests|FullyQualifiedName~AccountControllerTest"
dotnet test Radish.Api.Tests
npm run build --workspace=radish.client
npm run build --workspace=radish.console
npm run validate:identity
npm run validate:baseline:quick
npm run check:host-runtime -- --details --report --report-file .tmp/p3-10-b9-user-identity-host-runtime.md
npm run check:host-runtime -- --details --report --report-file .tmp/p3-10-b9-user-identity-host-runtime-elevated.md
lsof -nP -iTCP:5000 -sTCP:LISTEN
lsof -nP -iTCP:5100 -sTCP:LISTEN
lsof -nP -iTCP:5200 -sTCP:LISTEN
dotnet build Radish.slnx -c Debug
git diff --check
npm run check:repo-hygiene:changed
```

结果：

- 后端身份语义定向测试通过，`12` 个测试通过。
- 后端完整测试通过，`443` 个测试通过。
- `radish.client` 生产构建通过。
- `radish.console` 生产构建通过。
- `validate:identity` 通过，身份语义扫描、LongId 字符串安全扫描和身份语义后端定向测试均未发现回归；内部定向测试 `14` 个通过。
- `validate:baseline:quick` 通过，覆盖前端类型检查、`radish.client` `252` 个测试、Console 权限链路扫描、Repo Quality contract 自校验、身份语义影响面判定自校验和身份语义防回归扫描。
- 上述后端验证仍输出既有 XML 注释 warning，未出现本批新增编译错误。
- `check:host-runtime` 沙盒内与提权后均未通过：Gateway `/health` / `/healthz` 超时，API / Auth `5100 / 5200` 端口未监听。
- `lsof` 显示 `5000` 当前由 macOS `ControlCe` 监听，`5100` 与 `5200` 无监听进程。
- `dotnet build Radish.slnx -c Debug` 通过，`0` 个 warning，`0` 个错误。
- `git diff --check` 通过。
- `check:repo-hygiene:changed` 通过，检查 `50` 个变更文件，未发现文本卫生问题。

## 迁移说明

- 测试 / 生产 PostgreSQL 部署使用 `Deploy/sql/20260615_add_user_public_index.sql` 作为版本化差异 SQL 审核入口。
- 本地 SQLite 开发库不直接重放该 PostgreSQL SQL；继续通过 `Radish.DbMigrate init/apply` 补齐 `User.PublicIndex`、旧用户回填、种子保留索引纠偏和 `idx_user_public_index` 唯一索引。

## 后续边界

- 宿主恢复后补 Gateway PC / 移动 smoke，重点覆盖注册文案、登录名或邮箱登录、公开个人页、公开榜单、关系链用户项、艾特搜索和 Console 用户列表 / 详情。
- 不把 `DisplayName#PublicIndex` 替代 `PublicId` 路由。
- 不在公开页面展示登录名或邮箱。
- 不启动数据库主键迁移、邮箱通知系统、ActivityPub / WebFinger、Console 高风险账号字段治理或 B8 Phase C。
