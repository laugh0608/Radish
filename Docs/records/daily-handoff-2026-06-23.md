# 2026-06-23 收工回顾与明日事项

## 今日提交回顾

`git log --since="2026-06-23 00:00 +0800"` 在本记录提交前回顾到今日 10 个提交；本记录自身作为收工文档提交追加。

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `260f812f docs(records): 补充 0622 收工回顾` | 收工文档 | 补 2026-06-22 收工回顾、6 月第 4 周日志、记录索引和数据库发布 SQL 口径。 |
| `50f633f2 docs(guide): 同步文档入口与治理说明` | 文档治理 | 同步文档系统入口、职责边界、验收口径、文档作者 / Console 治理回归索引和 Wiki 文档 API 归属。 |
| `3e44c50c fix(scripts): 清理组合启动残留进程` | 启动脚本修复 | `start.sh` 组合启动增加后台服务进程组 / 子进程树记录，`Ctrl+C` 先优雅停止，超时后强制清理，解决组合启动后端残留占端口问题。 |
| `cafa512e docs(identity): 补充展示名与公开索引规则` | 身份设计 | 补注册页 `DisplayName` 慎重设置提示、改名限制，以及 `PublicIndex` 靓号保留 / Console 设置候选。 |
| `a4052d74 fix(scripts): 延后启动脚本依赖检查` | 启动脚本修复 | 修复直接执行 `./start.sh` 不出现选择界面的问题，将依赖检查延后到实际启动动作。 |
| `c823314b feat(client): 增加 Web 功能总入口` | P3-12-B5 | 新增 `/workbench` 功能地图，公共壳层工作台入口改指正式 Web，总入口设计与 Gateway PC / mobile smoke 已记录。 |
| `fb5ea9db docs(identity): 整理 B6 实施盘点` | P3-12-B6 / 设计 | 完成 B6 代码触点盘点、分批顺序、确认点和当前规划下一顺位整理。 |
| `04aeb2b0 feat(identity): 完成 B6-1 注册登录收敛` | P3-12-B6-1 | Auth 固定邮箱 + 密码登录，注册 / Bootstrap 必填 `DisplayName`，OIDC / CurrentUser 普通显示身份退出登录名。 |
| `b32fd178 feat(identity): 完成 B6-2 公开展示收敛` | P3-12-B6-2 | 论坛、聊天、榜单、圈子、公开个人页、转账搜索、资产流水和 Console 用户治理统一收敛到 `DisplayName / DisplayHandle` 口径。 |
| `f5231ce5 feat(identity): 完成 B6-3 展示名变更治理` | P3-12-B6-3 | 新增展示名变更审计记录，个人资料改名走服务端治理，接入冷却、滚动窗口和窗口内最大次数系统设置。 |

## 文档同步复核

- 已同步规划入口：[当前进行中](/planning/current) 已更新到 `2026-06-23`，记录 B5、B6-1、B6-2、B6-3 完成状态，并把明日事项写为 `P3-12-B6-4 PublicIndex 保留号治理`。
- 已同步 P3-12 主线说明：[P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement) 已记录 `/workbench` 首批落地、B6-1 / B6-2 / B6-3 完成和 B6-4 下一顺位。
- 已同步 B5 / B6 专题：[P3-12-B5 Web 功能总入口设计](/records/p3-12-b5-web-workbench-entry-design-2026-06-22) 已记录代码实现与 Gateway smoke；[P3-12-B6 身份语义二次收口设计](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22) 已记录 B6-1、B6-2、B6-3 实施事实、验证口径和后续分批。
- 已同步身份与认证说明：[用户身份语义与公开索引](/architecture/user-identity-semantics)、[标识体系与社区联邦长期路线](/architecture/id-and-federation-roadmap) 和 [认证服务统一指南](/guide/authentication-service) 已对齐邮箱登录、`DisplayName` 公开身份、`DisplayHandle`、`PublicIndex` 和历史 `LoginName` 退场口径。
- 已同步系统设置说明：[系统设置治理专题](/guide/system-settings-governance) 与 [运行时配置边界与系统设置](/guide/runtime-configuration-boundaries) 已把展示名改名冷却、滚动窗口和窗口内最大次数列为当前已注册设置；`PublicIndex` 靓号保留列表 / 规则仍保留为下一批候选。
- 启动脚本修复没有改变 `start.sh` 作为交互式本地启动入口的文档级职责；[架构框架](/architecture/framework) 当前说明仍有效，本轮只在收工记录留痕具体修复原因和影响。
- 本次收工补同步开发日志：[2026 年 6 月第 4 周开发日志](/changelog/2026-06/week4)、[2026 年 6 月开发日志](/changelog/2026-06)、[2026 年开发日志](/changelog/2026) 和 [记录与验收索引](/records/)。
- 已复核无需跟随更新范围：今天没有进入 UI 视觉实现、Pencil 设计稿、Flutter 新主线、PR、发布、完整 E2E、推荐算法、联邦社交、完整 PWA 或无关 WebOS 扩面清扫。

## 今日验证

- `start.sh` 修复来自用户本地复现反馈：组合启动 `Ctrl+C` 后端残留端口占用已修复；随后直接执行 `./start.sh` 不显示菜单的问题已修复。
- B5 已在用户确认前后端启动后完成 Gateway PC / mobile smoke，详见 [P3-12-B5 Web 功能总入口设计](/records/p3-12-b5-web-workbench-entry-design-2026-06-22)。
- B6-1 验证覆盖 `dotnet build Radish.slnx -c Debug`、身份相关后端定向测试、`dotnet test Radish.Api.Tests`、`radish.client` / `radish.console` 构建和 `git diff --check`。
- B6-2 验证覆盖 `dotnet test Radish.Api.Tests`、`dotnet build Radish.slnx -c Debug`、`radish.client` / `radish.console` 构建和 `git diff --check`。
- B6-3 验证覆盖展示名变更定向测试、`dotnet test Radish.Api.Tests`、`dotnet build Radish.slnx -c Debug` 和 `git diff --check`。
- 本收工文档批次执行 `git diff --check` 作为文档侧格式检查。

运行态说明：

- 今日执行过 B5 Gateway 真实页面 smoke，前提是用户明确说明前后端已启动。
- 后续真实 smoke 不沿用今天的运行状态；新会话需要用户再次明确说明前后端已启动后再执行。

## 明日事项

1. 先读取 [当前进行中](/planning/current)、[P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)、[P3-12-B6 身份语义二次收口设计](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22)、[用户身份语义与公开索引](/architecture/user-identity-semantics)、[系统设置治理专题](/guide/system-settings-governance) 和本记录，确认当前第一顺位是 `P3-12-B6-4 PublicIndex 保留号治理`。
2. 第一顺位：新增 `UserIdentity.PublicIndex.ReservedIndexes` 与 `UserIdentity.PublicIndex.VanityRules` 设置定义，先固定显式保留列表、靓号规则的输入格式、默认值、风险等级、校验规则和 Console 修改确认口径。
3. 改 `UserService` / Bootstrap 公开索引分配器：普通注册、批量新增和首个管理员初始化分配 `PublicIndex` 时必须跳过显式保留号和规则命中号；规则变更只影响后续分配，不自动改写既有用户。
4. 补后端定向测试：覆盖默认分配、跳过显式保留号、跳过规则命中号、重复保留号、配置越界 / 非法 JSON 暴露配置错误、Bootstrap 首个管理员保留号行为。
5. 文档同步范围：B6 专题、用户身份语义、系统设置治理、运行时配置边界和当前规划入口；若引入新的规则 JSON 结构，应在 B6 专题内给出示例和停止线。
6. B6-4 代码验证优先跑后端定向测试、`dotnet test Radish.Api.Tests`、`dotnet build Radish.slnx -c Debug` 和 `git diff --check`；真实 Gateway 页面 smoke 留到 B6 成组功能准备验收或用户明确要求时执行。
7. B6 收口后再进入 `P3-12-D` UI 设计专题；UI 专题仍按 Pencil 设计稿 -> 设计 / 说明文档 -> 代码实现 -> PC / mobile 复核推进。
