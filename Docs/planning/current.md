# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、当前判断、执行顺位、发布门禁与维护线**。
>
> 历史批次、命令级验证和页面级实现记录统一查看 [已完成摘要](/planning/archive)、[记录索引](/records/) 与 [开发日志](/changelog/)。

## 当前状态

- **阶段**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前子阶段**：`P3-12-E8 Pre-RC 产品与发布工程硬化`
- **工程第一顺位**：`推送 dev 并发起 dev -> master 集成 PR`
- **产品下一顺位**：`P3-12-F 发布 Go 门禁与小规模受控试用`
- **复核日期**：`2026-07-11`
- **当前判断**：
  - 纯 Web 已成为 PC / mobile 浏览器默认产品形态，`/desktop` 仅保留 WebOS 历史兼容入口；Flutter 保持移动原生维护线，PC/Tauri 后置。
  - `P3-12-A-D` 已完成正式 Web 主路径迁移、WebOS 收束和 Public / Private / Author / Console 页面族首批实现。
  - `P3-12-E1-E7` 已完成首批产品成熟度硬化；E8 首日已回拉主导航、用户语言、页面滚动、聊天工作区和公开文档可见口径。
  - 2026-07-10 全仓审计确认发布工程成熟度落后于产品与功能进度；Q0 属于进入发布候选前必须完成的安全阻断。
  - 2026-07-11 Q0-A 已完成：npm / NuGet High / Critical 清零，NuGet 审计恢复，`Dependency Security` 已纳入 Repo Quality workflow，并由用户确认配置到远程 `master` ruleset。
  - 2026-07-11 Q0-B 已完整关闭：生产性能基准、Weather、敏感配置、事务演示、测试租户写入与手动未读推送入口已删除，正常租户查询与正式通知能力保留，精确防回归契约已建立。
  - 2026-07-11 Q0-C 已完成：API JWT 启用 `radish-api` audience，完整 Claims 与成功鉴权高频日志已删除，Auth transport security 受 Development 显式配置和 Gateway 单跳 Forwarded Proto 契约约束。
  - 2026-07-11 Q0-D 已完成：Markdown 阅读态、富文本导入 / 建链 / 导出共用链接协议白名单，危险或未登记 scheme 统一退化为不可点击文本。
  - 2026-07-11 Q0 成组运行态补验已完成：Scalar 只暴露实际 v1 文档，Gateway 授权码、受保护 API、UserInfo、refresh、audience 拒绝、宿主健康与真实 Markdown / Hub 旅程均通过。
  - 2026-07-11 E8-B 已完成：关系复访、公开 Docs 服务端契约、Console 移动低风险边界、Pencil `P02` 与 Discover 内容优先结构均已收口；七项有限矩阵已通过 PC / mobile CSS 视口成组运行态验收，旅程中发现的公开帖子举报入口和举报队列帖子导航查询阻断已修复并补定向测试。当前无接受后置项和已知 `P0/P1` 阻断。
  - `master...dev` 已完成远程刷新与集成审阅：主线独有 8 个对象均为历史 PR merge ancestry，树内容无额外差异且合并预演无冲突信号；批次级 CI、host baseline、运行态健康和依赖安全均通过。阶段性集成提交完成后只需推送 `dev` 并创建 PR；若 GitHub 实际要求分支最新，再同步 `master`。
  - 集成安全复核发现并清除了附件测试脚本中的完整过期 JWT、默认账号密码、client secret 与 password grant；跨平台脚本统一改用进程级 `RADISH_ACCESS_TOKEN`，完整 JWT / Bearer 字面量 / 私钥头扫描已接入 `Baseline Quick / Repo Quality`。
  - 当前仍是 Pre-RC 硬化期，不把技术 smoke 或页面可达误写成真实使用增长；真正的增长验证从 P3-12-F 受控试用开始。
  - `P3-12-F` 不再被定义为“所有候选工作全部完成后的奖励阶段”：Q0 与有限产品矩阵负责进入 F，Q1 / Q2 / Q3 的发布必要子集在 F 内完成并作为 Release Go 门禁，Q4 转为持续维护。

## V1 产品与发布范围

Radish V1 的产品定位固定为：

> 面向小规模兴趣或创作社区的可独立部署社区产品：用帖子、评论和问答沉淀内容，用聊天、关注和通知形成复访；Docs 承接知识沉淀，宠物、经验、资产与商城作为可选激励层。

范围分层：

- **核心主轴**：发现内容流、论坛、评论 / 回答、登录态聊天、关注 / 圈子、通知复访与信任治理。
- **支撑能力**：Docs 知识沉淀、Workbench 低频能力地图、公开主页与 Console 社区治理。
- **辅助激励**：经验、宠物、资产、背包和商城；不抢占首页与当前发布主线。
- **本次正式 Web 发布矩阵**：`Radish.Gateway`、`Radish.Api`、`Radish.Auth`、`Radish.DbMigrate`、`radish.client`、`radish.console`。
- **维护 / 实验矩阵**：Flutter 为维护与后续受控移动增强；WebOS 为历史兼容；Tauri 与 `Lib/radish.lib` 不属于本次正式 Web 发布阻断面。

## 当前执行入口

- [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)
- [P3-12-E 正式产品成熟度与质量硬化](/planning/p3-12-product-maturity-quality-hardening)
- [P3-12-E8-Q 正式发布工程成熟度与安全收口](/planning/p3-12-e8-release-engineering-maturity-security-closure)
- [P3-12-E8-Q0 安全与暴露面阻断实施方案](/records/p3-12-e8-q0-security-exposure-implementation-plan-2026-07-10)
- [P3-12-E8-B 有限产品矩阵首轮审计记录](/records/p3-12-e8-b-limited-product-matrix-audit-2026-07-11)
- [P3-12-E8 dev -> master 集成审阅记录](/records/p3-12-e8-pre-master-integration-review-2026-07-11)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [前端多壳层策略](/frontend/shell-strategy)
- [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)
- [验证基线说明](/guide/validation-baseline)

## 当前目标

### 1. 完成 Q0 安全与暴露面阻断

- 按 `Q0-A → Q0-B → Q0-C → Q0-D` 分四个独立批次推进。
- Q0-A 已完成：恢复 npm / NuGet 依赖审计、清除 High / Critical，并建立可阻断的 CI 安全门禁。
- Q0-B 已完成：删除生产性能基准、Weather、敏感配置、事务演示和测试 Action，并清理专用模型与服务链。
- Q0-C 已完成 audience、敏感 Claims 日志和 Auth transport security 收紧。
- Q0-D 已完成 Markdown 编辑态与阅读态危险链接协议防护；Q0-A 至 Q0-D 的代码、静态验证与成组运行态补验全部通过。

### 2. 用 E8-B 有限矩阵收口产品形态

E8-B 只覆盖以下发布前产品判断：

1. `/discover` 改为内容优先：真实社区内容流为主体，删除重复路线说明；Docs、商城和榜单由“更多”承接，首页只保留少量与社区活动相关的辅助卡片。
2. 内容参与：公开阅读 → 登录恢复 → 评论 / 回答 → 成功反馈连续成立。
3. 关系复访：内容作者 → 公开主页 → 关注 → 圈子 / 通知回流连续成立。
4. 聊天回流：会话、未读 / @我、断线恢复与通知定位满足首发登录态聊天边界。
5. 信任治理：举报 → Console 证据回看 → 处理动作 → 审计记录连续成立。
6. 公开 Docs：服务端保证 `Published + Public`，草稿、受限、删除内容不进入公开列表、搜索与详情。
7. Console：正式定位为桌面优先后台；移动端只保证队列查看、搜索、证据回看和低风险处理，不要求桌面完整能力等价。

矩阵以“通过 / 阻断 / 接受后置”收口；通过后不再追加 E9 式逐页 UI 扫尾。

### 3. 重定义 P3-12-F

进入 F 前必须满足：

- Q0-A 至 Q0-D 全部通过。
- E8-B 有限产品矩阵通过或形成明确的接受后置清单。
- 当前正式 Web 发布矩阵明确，核心路径没有已知 `P0/P1`。
- 刷新 `master...dev` 范围并形成可审阅的集成结论。

F 内完成并作为 Release Go 门禁：

- 不可丢失奖励、资产、权益和通知写的可靠任务治理。
- 未知服务端异常不向客户端返回原始 `ex.Message`；完整领域错误码迁移可分批。
- 文件访问令牌若进入本次发布则完成 hash、原子计数和并发测试；否则退出正式暴露面。
- PostgreSQL / OpenIddict 生产相似升级演练、版本单一真值、候选级验证、Gateway PC / mobile smoke 与回滚材料。
- 时间语义只优先治理鉴权、过期、幂等、资产和后台任务等高风险路径，不做全仓机械替换。

### 4. 控制集成批次

- 合并到 `master`、创建 tag 和生产发布是三个独立决策。
- Q0 与 E8-B 收口后优先恢复 `dev -> master` 集成，不等待 Q1-Q4 全量清理。
- 当前批次继续扩大前必须刷新提交、文件和验证影响面；不再保留“P3-10 可恢复 PR”这一过期状态。

## 下一顺位

1. Q0-A 至 Q0-D 已完成并形成验证记录。
2. E8-B 七项有限产品矩阵已全部通过，运行态发现的举报入口与举报队列查询阻断已修复；不再追加 E9 式逐页扫描。
3. `master...dev` 范围、风险和批次级验证已形成集成审阅结论；阶段性集成提交完成后推送 `dev` 并创建 `dev -> master` PR；不创建 tag，不部署。
4. 进入 `P3-12-F`，完成发布 Go 门禁与小规模受控试用，再根据真实使用证据判断是否结束第三阶段。

## 并行维护线

- 公开 head、动态 sitemap、head snapshot 与生产公开域名配置。
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`。
- WebOS `/desktop` 阻断级兼容维护。
- Flutter analyze / test 与已落地主路径维护，不扩完整能力套件。
- 历史大文件、全量仓库卫生、TypeScript strict 和共享前端边界按 touched-file / 专题治理持续下降，不作为进入 F 的全量前置。

## 当前不做

- 不创建发布 tag，不进入生产部署或 Phase 4 稳定运营。
- 不新增 E9 式全站逐页 UI / 文案扫尾；新缺口必须命中 E8-B 有限矩阵、Q0 或真实阻断。
- 不把 Console 移动端做成桌面完整能力复制。
- 不恢复 WebOS、Tauri 或完整 Flutter 套件为当前主线。
- 不启动推荐算法、ActivityPub / WebFinger、宠物经济扩展、完整移动商城、完整 PWA、完整 E2E 或大而全可观测性平台。
- 不把 Q4 全量大文件、历史颜色、格式和文档债务设为进入 F 的阻断。
- 不绕过 Pencil 直接实施新页面族、全局导航、跨页面视觉体系或重大交互；既有页面的小范围密度、文案、状态和布局修正可代码先行并用截图复核。

## 验证与文档维护

- 开发中按风险执行定向测试、type-check、build、changed-only lint、repo hygiene 与 `git diff --check`。
- 准备合并到 `master` 时执行批次级 baseline、identity、依赖审计和范围复核。
- 发布候选运行态检查仍需用户当轮明确说明前后端已经启动。
- 本页不再记录 P3-1 至 P3-12-D 的命令级流水；历史事实进入 records、changelog 或 archive。
