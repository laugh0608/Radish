# test-latest 部署反馈修复记录（2026-09-19）

## 范围与根因

本批由项目所有者授权，在 `dev` 工作区处理部署目录、帖子读取、Hangfire 入口与 Console 密度四项反馈。没有发布镜像、改写线上数据库或迁移服务器目录。

- 部署 Compose 将 PostgreSQL、Redis、证书放在 `DeployData`，附件和日志另挂仓库根目录；默认改为 `Deploy/data`、`Deploy/logs`、`Deploy/backups`，并持久化 API/Auth 独立 Data Protection 密钥。首次空环境支持直接启动 Compose，已有数据升级保留生产发布编排。
- 线上帖子列表返回 HTTP 500。用户日志确认最近互动人查询使用带引号的 `Comment / PostId`，而 PostgreSQL 实际物理名为小写，触发 `42P01`。手写 SQL 现在按实体映射和数据库小写配置生成标识符；同步修复帖子、评论点赞中的同类问题，无需修改现有表或数据。
- Hangfire iframe 无 Bearer 请求头，线上入口返回空正文 401，后台任务实际仍在执行。增加仅限看板路径的短期 Cookie 兑换接口，保留服务器权限复核；普通 API 继续仅接受 Bearer，移除本地回环匿名旁路。原始 Token 不进入 URL。
- Console 桌面侧栏收紧为 224px、顶栏为 56px，压缩公共页头、指标和面板留白；资源列表优先展示表格，上下文说明折叠，筛选动作可换行。

## 验证

- .NET 定向测试 **22 通过、0 跳过**：`ForumInteractionRepositoryTest`、`HangfireDashboardSessionTest`、`LikeRelationConsistencyTest`、`ConsoleAuthorizationServiceTest`、`ClaimsPrincipalNormalizerTests`。
- 数据库测试同时覆盖 SQLite 和获批的隔离 `postgres:17.10-alpine`。PostgreSQL 使用生产一致的小写表名配置，验证无评论列表、互动人排除作者 / 隐藏 / 删除 / 其他租户、去重排序、每帖上限及两类点赞。部署默认 PostgreSQL 16，本批未运行 16 镜像测试。
- 会话测试覆盖 Cookie 安全属性、5 分钟上限、原 Token 到期上限、缺失 / 非法 / 越界到期 Claim、过期失效、权限撤销后拒绝、本地匿名拒绝、普通 API 不接受看板 Cookie。
- Console 显式 app TypeScript 检查、生产构建和 changed-only Lint 通过；Console 现有 **138 项**测试通过。构建保留已有的大 chunk 提示。
- 生产部署脚本 **8 项**测试通过；真实 `docker compose --env-file Deploy/.env.example -f Deploy/docker-compose.yaml config --quiet` 通过，未启动部署服务。
- 统一身份 Claim、LongId、Console 权限、文档、changed-only 仓库卫生及 `git diff --check` 通过。

## UI 证据边界

使用现有 React / Ant Design / ConsolePage 组件与当前 CSS 生成静态代表页，Playwright 检查 `1440×900`、`1024×900`、`390×900`：页面及筛选区未发生横向溢出，两个桌面视口首屏分别可完整看到 6 / 4 行示例双行数据，移动视口转为卡片。截图位于本地忽略目录 `output/playwright/console-density-{width}.png`。

这属于静态代表布局复核，不是已登录真实应用 Smoke；未执行前后端完整联调或线上发布后复测。看板与帖子修复的运行证据来自定向后端测试，不能替代部署后验证。

## 提交前补充：Console 登录与权限资源

- 项目所有者追加要求消除旧 Console 登录页面闪现。`/console/login` 现在只显示简短过渡并自动发起 OIDC，移除双栏登录卡片及旧样式；旧 `auto=1` 入口继续兼容。失败时提供手动重试与返回社区。
- 保留业务路由回跳，授权跳转使用 replace；StrictMode 复用同一次 PKCE 创建，卸载后忽略异步结果。
- 提交前权限扫描发现 Hangfire 会话接口缺少 `ApiModule` 与 `ConsoleResourceApiSeed` 登记，现已补齐。API 权限种子从超长 Identity 文件按职责移到 `InitialDataSeeder.ApiPermissions.cs`，逐段比较确认既有种子逻辑保持原样；检查脚本和对应说明同步更新。
- 补充验证：Console **138 项**测试、生产构建、显式 app 类型检查和 changed-only Lint 通过；身份种子 / Hangfire 会话 / Console 授权 **18 项**后端测试通过、0 跳过；权限、身份、时间语义、部署 **8 项**测试与文档检查通过。
- Playwright 使用真实 Login 组件、React StrictMode 与本地拦截的授权依赖，验证首次无旧卡片、只创建一次授权、失败不自动重试、手动重试、新旧登录入口、history replace、业务回跳和卸载后不跳转。`1440×900` 过渡态、`390×844` 失败态无横向溢出。
- 该浏览器验证是隔离组件交互测试，不是生产 OIDC 端到端 Smoke；未连接真实认证服务。截图位于忽略目录 `output/playwright/console-login-transition-1440.png` 与 `console-login-recovery-390.png`，测试浏览器已关闭。

## 迁移与清理

- 老部署更新 Compose 前应按[部署指南](/deployment/guide)停机复制旧目录，并保留原 `.env` 内的凭据、项目名和库名。不要直接更换挂载后把空目录作为原数据启动。
- 冷备份需停止全部容器后完整打包 `Deploy`，保留隐藏文件、属主和权限；使用相同 PostgreSQL 主版本和固定应用镜像版本恢复。自定义外置存储和外部 TLS 代理不包含在默认整目录保证内。
- 隔离容器 `radish-forum-regression-20260919` 及其匿名卷已删除，Playwright 专用浏览器已关闭；未安装或升级依赖，未遗留项目服务。

长期契约见[部署指南](/deployment/guide)、[生产迁移编排](/guide/production-database-migration-deployment)、[定时任务指南](/guide/hangfire-scheduled-jobs)、[Console 样式规范](/frontend/console-style-guide)。
