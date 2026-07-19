# 开发路线图（总览）

> 本页是路线图入口，只保留 **产品定位、当前阶段、阶段衔接、下一顺位、维护线与明确后置项**。
>
> 今日执行看 [当前进行中](/planning/current)；实现事实与验证证据查看 [记录索引](/records/)、[开发日志](/changelog/) 和对应专题。

## 当前状态

- **当前里程碑**：`Phase 4：长期维护与功能完成`
- **当前子阶段**：`F4 既有功能持续完成`
- **工程第一顺位**：`F4-E-B 聊天消息置顶：服务端权威契约`
- **产品下一顺位**：`实现多条置顶 migration、原子状态、独立权限、revision 快照、撤回一致性与前端 HTTP 契约`
- **最近正式发布**：`v26.7.1.1204-release`（2026-07-12）
- **复核日期**：`2026-07-19`
- **当前结论**：
  - 第二开发阶段完成公开 Web、Flutter Android MVP、Tauri + WebOS 验证与多端裁决；2026-07-12 起纯 Web 成为唯一正式主线，Flutter 条件维护，Tauri 冻结实验。
  - 第三阶段 P3-1 至 P3-11 已完成公开增长基础、PublicId 试点、复访链路、Web-first 信息架构、身份语义、写操作可靠性和发布候选路径验收。
  - P3-12-A-D 已完成正式 Web 主路径迁移、WebOS 收束和 Public / Private / Author / Console 页面族首批实现。
  - P3-12-E1-E7 已完成首批产品成熟度硬化；E8 首日完成正式导航、用户语言、页面滚动、聊天工作区和公开文档口径回拉。
  - Q0-A 至 Q0-D、Q0 成组运行态补验和 E8-B 七项有限产品矩阵已于 2026-07-11 完成，项目已进入 P3-12-F。
  - Q1、Q2、Q3 与候选运行态验收均已完成；1201 至 1203 暴露的镜像、PostgreSQL migration、OpenIddict provider / model 与候选 DateTime 参数阻断均已按不可变 tag 逐次前滚收口。
  - PR `#63` 已合并，`master / dev / origin` 已统一到 `53539556`；`v26.7.1.1204-release` 五镜像和固定 tag 生产部署成功，首个管理员已创建，服务运行正常。
  - `P3-12-F` 正式发布执行已经关闭。受控试用转为发布后的早期真实使用观察；首次管理员入口门禁不一致已登记为非阻断维护首项，产品顺位进入商城商品效力与权益履约。
  - F1 商城、F2 主题、F3 i18n 与一对一私聊批次 A-D 均已完成；私聊已通过 SQLite / PostgreSQL 定向回归及双账号 `zh / en × PC / mobile` 运行态矩阵并关闭。
  - F4-B-A-D 已完成稳定定义、结构化目标、偏好、分组 / 摘要 revision、正式 Web 通知工作区和三普通账号 `zh / en × PC / mobile` 成组验收；SQLite / PostgreSQL、离线 / 多标签、cursor、目标失效与临时数据清理均通过，通知中心深化专题已关闭。
  - F4-C-A-D 已完成 Chat 权威检索、跨库 migration、正式 Web / WebOS 搜索工作区和双账号 `zh / en × PC / mobile` 成组验收；共同根因修复、临时数据清理、六库完整性与迁移 verify 均通过，专题关闭。
  - F4-D-A-D 已完成 Chat 专属 Reaction 设计、服务端权威契约、PC / mobile Pencil、正式 Web 与三普通账号成组验收：`CanReact`、目标状态 operation ledger、消息 revision、完整 Hub 快照、撤回一致性、真实离线重连和首批不通知边界均通过；临时数据清理、六库完整性与 migration verify 完成，专题关闭。
  - F4-E-A 已完成真实代码与旧 Phase 2 草案审计；权威设计固定独立 `ChatMessagePin`、频道 `PinRevision`、20 条上限、目标状态、完整 Hub 快照、Public / Announcement / 普通 Private / Direct 权限和撤回一致性，下一批进入服务端契约。
  - F4-A 首轮仓库盘点只确认一条发布后生产 UX 证据：首次管理员入口门禁不一致。该 `P2` 已在 `dev` 修复；采集说明与模板保留为最终收尾资产，主动生产证据采集已经冻结，不再作为当前功能选题前置。

## V1 产品定位

Radish V1 固定为：

> 面向小规模兴趣或创作社区的可独立部署社区产品：用帖子、评论和问答沉淀内容，用聊天、关注和通知形成复访；Docs 承接知识沉淀，宠物、经验、资产与商城作为可选激励层。

产品优先级：

1. **社区核心**：发现、论坛、评论 / 回答、登录态聊天、关注 / 圈子、通知和信任治理。
2. **社区支撑**：Docs、Workbench 低频能力地图、公开主页和 Console。
3. **辅助激励**：经验、宠物、资产、背包和商城。
4. **长期扩展**：推荐、联邦、PWA、开放平台和多端增强；不进入当前正式 Web 候选。

低频模块必须能回到内容、关系、贡献或复访主轴，不能与社区核心并列争夺默认首页和开发顺位。

## 当前开发节奏

- 当前已进入发布后常态开发与 F4；一对一私聊、F4-B 通知中心深化、F4-C 聊天历史搜索和 F4-D 消息 Reaction 均已关闭，当前只推进 F4-E 消息置顶，不并行展开其他候选功能。
- 发布后只保持一个主要功能专题在进行；`P0/P1` 用户问题可以中断，P2/P3 按同类问题成组维护。
- 长期维护线处理反馈、安全、依赖、迁移和部署；F1 商城、F2 主题、F3 i18n、一对一私聊及 F4-B / C / D 已关闭，功能完成线当前进入 F4-E-B 消息置顶服务端权威契约。
- Q4、公开 head、WebOS 和 Flutter 继续按触达范围或真实问题维护，不与正式 Web 功能主线争夺顺位。
- 主动生产使用数据采集只在计划内功能全部完成、没有其他明确任务、产品进入最终完成体复核且用户确认后重启；不得因旧记录、观察周期或新会话频繁重新排期。

## 已确认的多端方向

1. **Web 浏览器**
   - 纯 Web 是 PC / mobile 浏览器默认正式产品。
   - 根路径 `/` 进入内容优先发现页；`/workbench` 承接低频能力与历史兼容入口。
   - 本次正式发布矩阵包含 Gateway、API、Auth、DbMigrate、client 和 Console。
2. **Flutter**
   - Android MVP 与既有高价值路径只做阻断、安全和认证兼容维护，不默认新增页面或追平 Web。
   - 只有受控试用证明系统推送、后台任务、商店分发或原生生命周期具有明确价值后，才重新立项；iOS 不主动扩展。
3. **WebOS `/desktop`**
   - 只作为历史兼容入口，不承接新增功能。
   - 只处理阻断级问题和迁移所需缺口。
4. **Tauri / PC**
   - 冻结为实验资产，不进入日常开发、候选 CI 必需矩阵、签名或分发。
   - 只有托盘、文件系统、协议唤起、自动启动等明确桌面原生需求成立时才单独评估解冻；解冻后也只增强纯 Web，不绑定 WebOS。
5. **Console**
   - Console 是桌面优先的社区治理后台。
   - 移动端只承接队列查看、搜索、证据回看和低风险处理，不要求桌面能力完整复制。

## 阶段路线

### 第一开发阶段：首版发布

- `v26.3.2-release` 已完成首版真实发布。
- 认证、基础社区、商城、治理、部署与回滚形成首版基线。

### 第二开发阶段：社区深化与多端验证

- 已完成公开内容壳层、Flutter Android MVP、Tauri + WebOS 验证和多端路线裁决。
- 阶段结论已归档；WebOS 与 PC/Tauri 不再作为新增功能主线。

### 第三开发阶段：Web-first 与正式产品化

- P3-1 至 P3-11 已完成增长基础、长期契约、真实路径验收和 Web-first 转向。
- P3-12-A-D 已完成正式 Web 能力迁移和页面族首批实现。
- P3-12-E8 已完成有限产品收口与 Q0 安全阻断，阶段事实转入记录和维护。
- P3-12-F 已完成候选期可靠性、数据库演进、版本、候选验证、正式发布和生产部署，阶段事实转入发布记录与维护线。

### Phase 4：长期维护与功能完成

以下进入条件已经满足：

- 正式 Web Release Go 门禁通过并完成可回滚发布。
- 部署后复核通过且没有未处置的 `P0/P1`。

进入后同时维护长期维护线与功能完成线。被动收到的生产故障和可追溯反馈作为后续开发输入；主动采集激活、首次参与、回应后回流等生产使用数据冻结到项目最终收尾，不作为 tag 或功能开发前置。

## 下一顺位

1. 一对一私聊与会话管理批次 A-D 已完成并关闭，详情见 [专题设计与验收结果](/features/chat-direct-conversation-design)。
2. [F4-B 通知中心深化与通知治理](/features/notification-center-deepening) 已完成 A-D 批服务端契约、正式 Web 页面和三普通账号成组验收，专题关闭。
3. [F4-C 聊天历史搜索与消息定位](/features/chat-message-search-design) 已完成 A-D 批并关闭，权威 ACL、跨库迁移、快照 cursor、正式 Web / WebOS 与双账号成组验收均通过。
4. [F4-D 聊天消息 Reaction](/features/chat-message-reaction-design) 的 A-D 批已完成，权威 ACL、目标状态幂等、revision 实时快照、正式 Web / WebOS 与三普通账号成组验收均通过，专题关闭。
5. [F4-E 聊天消息置顶](/features/chat-message-pin-design) 的 A 批已完成现状审计与专题设计，固定多条置顶、独立状态表、频道 revision、权限矩阵、撤回一致性和正式 Web 边界；下一批进入 F4-E-B 服务端权威契约。逐条已读和移动系统通知继续后置。

## P3-12-F 门禁分层

### 进入 F

- Q0 全部通过。
- E8-B 有限矩阵通过或形成明确接受后置清单。
- 正式 Web 发布矩阵明确，核心路径没有已知 `P0/P1`。
- 当前集成范围可审阅、可验证。

### F 内 Release Go

- 不可丢失业务写不依赖裸 fire-and-forget。
- 未知异常不返回原始 `ex.Message`。
- 文件访问令牌按本次发布范围选择“完成安全治理”或“退出正式暴露面”。
- PostgreSQL / OpenIddict 升级演练、版本单一真值、候选测试、Gateway smoke 和回滚材料完成。
- 高风险时间语义完成定向治理；全仓时间、strict、大文件和历史卫生债务进入持续治理。

当前上述工程门禁已经通过。被动收到的真实问题继续用于调整维护与功能投入；主动生产使用观察只在最终完成体复核时重启，不阻挡正式 tag 或日常功能推进。

## 并行维护

- 公开 head、动态 sitemap、head snapshot 和生产公开域名配置。
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`。
- WebOS `/desktop` 阻断级兼容。
- Flutter analyze / test 与已落地主路径回归。
- Q4 大文件、共享前端边界、全量卫生和文档归档按触达范围持续下降。

## 明确后置

- 在 `dev -> master` PR、正式发布记录、required checks 或镜像供应链门禁未通过时创建 tag 或生产部署。
- WebOS 新功能、Tauri 分发、完整 Flutter 套件和独立移动 Console。
- 推荐算法、ActivityPub / WebFinger、完整 PublicId / 主键迁移。
- 宠物经济扩展、完整移动商城、完整钱包 / 售后与资产风控平台。
- 完整 PWA、完整 E2E、完整可观测性、Redis 平台化、开放平台和 BFF 深化。
- Q4 全量大文件拆分、历史样式与仓库卫生清零。

## 当前主线入口

- [当前进行中](/planning/current)
- [第三开发阶段](/planning/phase-three-real-usage-contract-governance)
- [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)
- [P3-12-E 正式产品成熟度与质量硬化](/planning/p3-12-product-maturity-quality-hardening)
- [P3-12-E8-Q 正式发布工程成熟度与安全收口](/planning/p3-12-e8-release-engineering-maturity-security-closure)
- [前端多壳层策略](/frontend/shell-strategy)
- [验证基线说明](/guide/validation-baseline)
- [未来规划](/planning/backlog)
- [发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)
- [正式 Web 一对一私聊与会话管理设计](/features/chat-direct-conversation-design)
- [F4-A 首批真实使用证据整理与反馈归因记录（冻结）](/records/f4-a-first-real-usage-evidence-attribution-2026-07-18)
- [F4-B 通知中心深化与通知治理](/features/notification-center-deepening)

## 文档规则

- 本页只维护总体方向和阶段衔接；今日任务以 `current.md` 为准。
- 历史批次和命令级验证进入 records、changelog 或 archive。
- 产品、工程和发布门禁分别维护在 P3-12-E、E8-Q 与验证 / 部署专题中，不在入口文档重复完整清单。
