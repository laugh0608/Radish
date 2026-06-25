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

## 2026-06-24

- `P3-12-B6-4` PublicIndex 保留号治理完成：新增显式保留号与靓号规则系统设置，普通注册、批量新增和 Bootstrap 公开索引分配跳过配置命中号。
- `P3-12-B6-5` 身份旧字段与种子迁移收口完成：后端 `LoginName` / `UserRealName`、个人资料真实姓名输入、登录名系统设置和 DbMigrate 旧身份回填逻辑退场；开发种子账号固定邮箱与 `PublicIndex=1/2/3`。
- `P3-12-B6-6` 验证契约收紧完成：token、structured data 和 return path 身份语义守护补强，B6 代码侧与启动前验证收口；Gateway PC / mobile 页面 smoke 仍待用户明确前后端启动后补验。
- Web 联调阻断修复完成：公开 head snapshot 缓存 / middleware / 配置与商城任务空集合处理补强，并补相关测试。
- `P3-12-D2` 公开 Web 统一体验设计源首批完成：创建 `public-web-unified-experience.pen`，写入公开 Web `rx-*` 变量、`P01` 公开壳层基座和 `P02` 发现内容流；当前仍处于 Pencil 设计源阶段，不进入视觉代码。
- 相关设计 / 说明书已同步：当前规划、P3-12 主线、前端设计说明、设计源索引、D2 设计源记录、记录索引和本开发日志均已对齐今日提交。
- 今日提交回顾见 [2026-06-24 收工回顾与明日事项](/records/daily-handoff-2026-06-24)。明日第一顺位是继续补 `public-web-unified-experience.pen`：先做 `P03` 公开详情阅读，再做 `P04` 公开集合页和 `P05` mobile 单列基线。

## 2026-06-25

- `P3-12-D2` 公开 Web 统一体验设计源完成密度收口：`public-web-unified-experience.pen` 已补齐公开 `P01-P05`，并完成桌面与移动信息密度调整，新增公开 Web 统一体验设计说明。
- `P3-12-D3` 私域与作者态 Web 工作流设计源完成：`private-web-workflows.pen` 已写入私域首页、资产 / 订单 / 背包、作者工作台、编辑器 / 版本回看和移动私域单列 `P01-P05`，并完成一轮视觉规范审阅后的设计优化。
- `P3-12-D4` Web UI 共享基座设计源完成：新增 `web-ui-foundation.pen`，写入 public / private header 合法变体、按钮 / pill、卡片 / rail、状态槽、移动 shell / tab 和跨设计源同步规则。
- Pencil 工作流约束已明确：写入必须以当前活动窗口为准，切换 `.pen` 前必须手动保存；MCP `filePath` 可用于读取 / 检查 / 截图，但不作为非活动窗口写入保证。
- 相关设计 / 说明书已同步：设计源目录、公开 Web 说明、私域 / 作者态说明、共享基座说明、D2 / D3 / D4 记录、当前规划、记录索引和本开发日志均已对齐。
- 今日提交回顾见 [2026-06-25 收工回顾与明日事项](/records/daily-handoff-2026-06-25)。明日第一顺位是按用户意见继续优化 `web-ui-foundation.pen`，确认 Pencil 活动文件并手动保存后再做布局检查和截图目检。
