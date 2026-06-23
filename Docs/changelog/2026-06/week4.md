# 2026 年 6 月第 4 周开发日志

## 2026-06-22

- `P3-12-B3` 论坛作者态小阶段验收完成：Gateway PC `1920x1080` 与移动 `390x844` CSS 视口覆盖公开论坛列表、发帖登录回流、公开详情 canonical、作者态 `edit/history` return path；随后使用种子账号 `admin` 补验已登录发帖、作者编辑、编辑历史与问答回答提交成功态。
- `P3-12-B4` 文档作者态归属裁决完成：公开 `/docs` 保持阅读、搜索和分享；正式 Web 承接 `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id`；发布、撤回、归档、恢复、权限策略、导入导出和回滚等治理动作归 Console。
- `P3-12-B4-1` 正式 Web 文档作者入口首批代码完成：新增我的文档、新建文档、编辑文档和版本回看入口；内置文档编辑保持只读保护。
- `P3-12-B4-2` Console 文档治理首批代码完成：新增 `/console/documents` 治理入口、治理专用读取 / 权限策略 API、Console 权限键、资源种子和权限覆盖矩阵；正文创建 / 编辑继续归正式 Web 作者入口。
- B4 / D1 阶段运行态 smoke 已完成：Gateway 健康检查通过，PC / mobile 覆盖公开文档、文档作者入口、Console 文档治理、`/messages` 和 `/desktop?app=chat`；同时清理 Console `Space direction` 运行态告警。
- `P3-12-D1` 统一 UI 设计准备已启动：当前只补页面矩阵、设计源拆分、停止线和后续执行顺序；Pencil app 暂未连接，本轮不创建 `.pen`、不修改设计源、不进入视觉代码。
- 用户确认 `/messages` 已是正式 Web 消息 / 聊天入口；实际缺口调整为正式 Web 缺少集中功能总入口。新增 `P3-12-B5 Web 功能总入口设计`，确认下一步实现 `/workbench`，并把公共壳层“工作台”从 `/desktop` 调整到正式 Web 功能地图。
- 新增 `P3-12-B6 身份语义二次收口设计`：确认登录凭证改为邮箱 + 密码，注册必须填写 `DisplayName`，`DisplayHandle = DisplayName#PublicIndex` 作为用户可见唯一身份；`PublicId` 只用于 URL / 分享 / 传参；`LoginName`、`UserRealName` 和 `usr_...` 普通资料页展示退场。
- 数据库结构口径完成收束：项目尚未上线且无正式数据库，B6 后续按破坏性 schema 收口处理；未上线阶段历史发布脚本已清理，正式数据库发布 SQL 只在未来真实上线或存在正式数据库后生成。
- 今日提交回顾见 [2026-06-22 收工回顾与明日事项](/records/daily-handoff-2026-06-22)。明日第一顺位是实现 `P3-12-B5` 的 `/workbench` 功能总入口和公共壳层入口调整；第二顺位是推进 `P3-12-B6` 代码前触点盘点与分批方案。

## 2026-06-23

- `start.sh` 组合启动残留进程问题已修复：组合启动记录后台服务进程组和子进程树，`Ctrl+C` 时先优雅停止，超时后强制清理，避免 `Radish.Auth` / `Radish.Gateway` 残留占用端口。
- `start.sh` 直接执行不显示菜单的问题已修复：依赖检查延后到具体启动动作，交互式菜单恢复为默认入口。
- `P3-12-B5` Web 功能总入口首批完成：新增 `/workbench` 功能地图，公共壳层“工作台”改指正式 Web 功能总览，`/desktop` 降级为桌面版 / WebOS 历史入口功能项；Gateway PC / mobile smoke 已记录到 B5 专题。
- `P3-12-B6` 完成代码前触点盘点和分批方案：明确 B6-1 至 B6-6 的顺序、破坏性 schema 收口口径、注册 / 登录 / Bootstrap / CurrentUser / OIDC / 前端状态 / 公开展示 / Console 用户治理影响面。
- `P3-12-B6-1` 身份基础与注册登录完成：Auth 固定邮箱 + 密码登录，注册 / Bootstrap 必填 `DisplayName`，注册页补展示名公开展示与改名限制提示，OIDC / CurrentUser 普通显示身份不再输出登录名。
- `P3-12-B6-2` 公开展示与前端状态收敛完成：论坛、聊天、榜单、圈子、公开个人页、转账搜索、资产流水和 Console 用户治理统一使用 `DisplayName / DisplayHandle`，过滤 `usr_...` 普通可见风险。
- `P3-12-B6-3` 展示名变更治理完成：新增 `UserDisplayNameChangeRecord`，个人资料改名走 `UserService.ChangeDisplayNameAsync`，接入改名冷却、滚动窗口和窗口内最大次数系统设置。
- 相关设计 / 说明书已同步：当前规划、P3-12 主线、B5 / B6 专题、用户身份语义、长期 ID 路线、认证服务、系统设置治理、运行时配置边界、记录索引和本开发日志均已对齐今日提交。
- 今日提交回顾见 [2026-06-23 收工回顾与明日事项](/records/daily-handoff-2026-06-23)。明日第一顺位是 `P3-12-B6-4 PublicIndex 保留号治理`：新增保留靓号列表 / 规则设置，并让注册与 Bootstrap 分配器跳过保留号。
