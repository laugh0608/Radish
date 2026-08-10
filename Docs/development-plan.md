# 开发路线图（总览）

> 本页是路线图入口，只保留 **产品定位、当前阶段、阶段衔接、下一顺位、维护线与明确后置项**。
>
> 今日执行看 [当前进行中](/planning/current)；实现事实与验证证据查看 [记录索引](/records/)、[开发日志](/changelog/) 和对应专题。

## 当前状态

- **当前里程碑**：`Phase 4：长期维护与功能完成`
- **当前子阶段**：`F4 既有功能持续完成`
- **工程第一顺位**：`F4-R R2-W02 Private 仪表 / 任务侧栏；五组前端能力门禁已关闭`
- **产品下一顺位**：`保持 Pencil 不占用；设计资源可用并获授权后进入 R2 局部代表设计`
- **最近正式发布**：`v26.7.1.1204-release`（2026-07-12）
- **复核日期**：`2026-08-10`
- **当前结论**：
  - 第二开发阶段完成公开 Web、Flutter Android MVP、Tauri + WebOS 验证与多端裁决；2026-07-30 起当前投入顺位调整为 Web 优先、Flutter 次级，Tauri 暂时弃用并等待未来重新评估。
  - 第三阶段 P3-1 至 P3-11 已完成公开增长基础、PublicId 试点、复访链路、Web-first 信息架构、身份语义、写操作可靠性和发布候选路径验收。
  - P3-12-A-D 已完成正式 Web 主路径迁移、WebOS 收束和 Public / Private / Author / Console 页面族首批实现。
  - P3-12-E1-E7 已完成首批产品成熟度硬化；E8 首日完成正式导航、用户语言、页面滚动、聊天工作区和公开文档口径回拉。
  - Q0-A 至 Q0-D、Q0 成组运行态补验和 E8-B 七项有限产品矩阵已于 2026-07-11 完成，项目已进入 P3-12-F。
  - Q1、Q2、Q3 与候选运行态验收均已完成；1201 至 1203 暴露的镜像、PostgreSQL migration、OpenIddict provider / model 与候选 DateTime 参数阻断均已按不可变 tag 逐次前滚收口。
  - PR `#63` 已合并，`master / dev / origin` 已统一到 `53539556`；`v26.7.1.1204-release` 五镜像和固定 tag 生产部署成功，首个管理员已创建，服务运行正常。
  - `P3-12-F` 正式发布执行已经关闭；生产数据库迁移发布现已固定为不可变 release tag、停止写入、六库备份、显式 `apply`、独立 `verify`、应用发布和外部健康检查，迁移开始后的失败不会自动执行破坏性恢复。
  - 发布后镜像漏洞门禁分层已经完成：`CRITICAL` 与存在修复版本的 `HIGH` 默认阻断，无修复 `HIGH` 形成 Job Summary 与报告工件但不阻断；精确例外按镜像、包、CVE 和到期日自动治理。
  - F1 商城、F2 主题、F3 i18n 与一对一私聊批次 A-D 均已完成；私聊已通过 SQLite / PostgreSQL 定向回归及双账号 `zh / en × PC / mobile` 运行态矩阵并关闭。
  - F4-B-A-D 已完成稳定定义、结构化目标、偏好、分组 / 摘要 revision、正式 Web 通知工作区和三普通账号 `zh / en × PC / mobile` 成组验收；SQLite / PostgreSQL、离线 / 多标签、cursor、目标失效与临时数据清理均通过，通知中心深化专题已关闭。
  - F4-C-A-D 已完成 Chat 权威检索、跨库 migration、正式 Web / WebOS 搜索工作区和双账号 `zh / en × PC / mobile` 成组验收；共同根因修复、临时数据清理、六库完整性与迁移 verify 均通过，专题关闭。
  - F4-D-A-D 已完成 Chat 专属 Reaction 设计、服务端权威契约、PC / mobile Pencil、正式 Web 与三普通账号成组验收：`CanReact`、目标状态 operation ledger、消息 revision、完整 Hub 快照、撤回一致性、真实离线重连和首批不通知边界均通过；临时数据清理、六库完整性与 migration verify 完成，专题关闭。
  - F4-E-A-D 已完成并关闭：独立 `ChatMessagePin`、频道 `PinRevision`、20 条上限、目标状态、完整 Hub 快照、权限矩阵、撤回一致性、SQLite / PostgreSQL migration、正式 Web / WebOS 和三普通账号成组验收全部通过。
  - F4-F-A-D 已完成并关闭：唯一持久已读游标、原子单调推进、隐私裁剪聚合、发送者受限读取、活跃阅读面、正式 Web / WebOS 和三普通账号成组验收全部通过；验收发现的频道最后消息投影共同根因已修复。
  - F4-G-A-D 已完成并关闭：普通作者所有权、协作者邀请、独立工作草稿、审核应用、草稿 / 正文双版本 CAS、正式 Author / Console 页面和 Gateway 成组验收形成闭环；未审核与驳回正文公开隔离、临时数据清理、六库完整性与严格 migration verify 均通过。
  - F4-H-A-D 已完成并关闭：电子宠物公开名片的权威设计、独立公开 VO、服务端用户 / 租户 / 公开 / 软删除查询、`UserPublicProfileVo.VoPet` 聚合、PC / mobile Pencil、正式 `/u/:id` 只读名片，以及主人 / 访客 / 匿名、显隐、失效、双语、响应式与四主题代表矩阵均已通过；临时数据清理、六库完整性和严格 verify 完成。
  - F4-I-A 已完成圈子关系与治理候选复核，并裁决内容治理案件、证据与动作一致性为唯一当前专题；权威设计固定案件聚合、追加式证据、决定 / 动作分离、用户治理唯一当前状态、五类目标适配、迁移兼容、权限、A-D 批次与停止线。
  - F4-I-B 已完成服务端权威契约：Main ledger migration、历史映射、专属 Repository、案件 / 证据 / 当前状态事务、五类目标处置、Chat 跨库可靠任务、治理通知、新 API、独立动作权限和旧写入口过渡兼容均已落地。
  - F4-I-C 已完成 Pencil 与正式页面：Console `P02 / P07`、`/moderation` Case 工作台、私域 `/me/reports`、权限分离、冲突草稿保留、双语和目标失效摘要均已收口，旧四个治理 HTTP 入口已退役。
  - F4-I-D 已完成并关闭专题：五种角色、五类目标、并发与幂等、目标变化、用户治理状态、Chat 跨库失败 / 重试、双语、PC / mobile 和四主题代表矩阵均已通过；临时数据清理、六库完整性与严格 migration verify 完成。下一顺位进入 F4-J-A 候选复核与权威设计。
  - F4-J-A 已完成候选审计与权威设计：治理申诉相较圈子全局屏蔽、公开聊天和论坛作者回滚，更直接补齐现有高权限治理的对称复核与恢复路径；专题固定独立 Appeal / Event / TargetAction、一次申诉、部分采纳、五类恢复、用户状态来源保护、独立权限、迁移、页面和 A-D 批次。下一顺位进入 F4-J-B。
  - F4-J-B 已完成服务端权威契约：Main / Chat migration、独立申诉聚合、一次申诉与部分采纳、五类来源保护恢复、Mute / Ban 纠正、Chat 可靠任务、权限、通知、HTTP 与 `@radish/http` 契约已落地。下一顺位进入 F4-J-C。
  - F4-J-C 已完成 Pencil 与正式页面：正式 `/me/appeals`、提交 / 撤回、超期决定回看、通知深链与纠正摘要，以及既有 `/moderation` 内的申诉队列、受权详情、复核和 PC 纠正执行均已落地；View 队列服务端脱敏、mobile 只读、冲突草稿和双语响应式边界同步收口。下一顺位进入 F4-J-D。
  - F4-J-D 已完成并关闭：六角色、五类目标、主要申诉状态、五类来源保护恢复、用户状态纠正、Chat 跨库失败重试与双语 PC / mobile 正式路径均已通过；共同根因修复、临时数据清理、六库完整性与严格 migration verify 完成。下一顺位进入 F4-K-A。
  - F4-K 已完成 A-D 批并关闭：Main `UserBlock` 唯一真相、双向交互隔离、关注事务、Direct 历史只读、通知抑制、旧字段迁移、正式 Web 和三账号 Gateway 矩阵均已通过；验收共同根因修复、临时数据清理、六库完整性与严格 verify 完成。
  - F4-L-A-D 已完成并关闭：Main `WikiAttachmentReference`、Wiki 私有默认、动态 ACL、草稿 / Apply 事务同步、令牌先鉴权后消费、正式 Web 受保护资源、六身份 Gateway 矩阵、清理和 SQLite / PostgreSQL 验证形成闭环；通用 Document 保持独立边界。下一顺位进入 F4-M-A 单专题候选审计。
  - F4-M、F4-N、F4-O 与 F4-P 均已完成 A-D 批并关闭。F4-P 建立帖子私有收藏权威关系、显式幂等状态、个人稳定分页、不可用目标移除和正式 Web 回访闭环；D 批修正完整 `MeRoute` 来源返回并完成代表运行态矩阵、清理和严格数据库复核。
  - F4-Q 已完成 A-D 批并关闭：统一公开帖子判定，落地数据库侧标签公开计数 / 热门 / 相关聚合、标签首包 head、tags sitemap、正式 Web 相关主题和代表运行态矩阵；D 批修正 sitemap 分片、单一 JSON-LD、不可用标签 `noindex` 与 Console 软删除恢复契约，清理和严格数据库复核完成。
  - F4-R 已完成 A / B、C-0、C-1A、`R1-F01`、六个 R1、`R2-C03` 与 `R2-P03` 的代表设计、正式实现和 Gateway 运行态闭环；`R2-W02` 的 [readiness](/records/f4-r-r2-w02-private-dashboard-task-rail-readiness-audit-2026-08-10)、[五组前端能力门禁](/records/f4-r-r2-w02-private-dashboard-task-rail-capability-gate-implementation-2026-08-10)与[局部代表设计](/records/f4-r-r2-w02-private-dashboard-task-rail-representative-design-2026-08-10)均已确认，当前进入六个正式页面的视觉实现，不提前推进 R3。F4-S 公开排行榜治理已穿插完成并关闭。
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

- 当前已进入发布后常态开发与 F4；一对一私聊、F4-B 至 F4-Q 与 F4-S 均已关闭，F4-R 已完成 A / B、v26.7.3 基线补充批、C-0、C-1A、`R1-F01`、六个 R1、`R2-C03` 与 [R2-P03](/records/f4-r-r2-p03-public-read-only-detail-variants-implementation-2026-08-10) 的设计、实现和运行态闭环；`R2-W02` readiness、能力门禁与代表设计已完成，当前进入正式视觉实现。
- 发布后只保持一个主要功能专题在进行；`P0/P1` 用户问题可以中断，P2/P3 按同类问题成组维护。
- 长期维护线处理反馈、安全、依赖、迁移和部署；F1 商城、F2 主题、F3 i18n、一对一私聊、F4-B 至 F4-Q 与 F4-S 已关闭，功能完成线继续推进 F4-R C-1B 的 R1 / R2 代表设计与继承实现。
- Q4、公开 head 和 WebOS 继续按触达范围或真实问题维护；Flutter 作为次级移动原生产品线按明确移动价值推进，不与 Web 优先主线并行复制功能。
- 主动生产使用数据采集只在计划内功能全部完成、没有其他明确任务、产品进入最终完成体复核且用户确认后重启；不得因旧记录、观察周期或新会话频繁重新排期。

## 已确认的多端方向

1. **Web 浏览器**
   - 纯 Web 是 PC / mobile 浏览器默认正式产品。
   - 根路径 `/` 进入内容优先发现页；`/workbench` 承接低频能力与历史兼容入口。
   - 本次正式发布矩阵包含 Gateway、API、Auth、DbMigrate、client 和 Console。
2. **Flutter**
   - 作为次级移动原生产品线继续承接明确高价值移动路径，功能边界先在 Web 成立。
   - 不机械追平 Web；系统推送、后台任务、商店分发、原生生命周期与 iOS 产品化仍需独立价值判断。
3. **WebOS `/desktop`**
   - 只作为历史兼容入口，不承接新增功能。
   - 只处理阻断级问题和迁移所需缺口。
4. **Tauri / PC**
   - 暂时弃用，保留历史验证资产，不进入日常开发、UI、候选 CI 必需矩阵、签名或分发。
   - 未来只有桌面原生价值、目标用户和维护预算明确时才重新评估；若重新启动，默认增强正式 Web，不绑定 WebOS。
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
5. [F4-E 聊天消息置顶](/features/chat-message-pin-design) 的 A-D 批已完成并关闭，权威 ACL、目标状态幂等、20 条上限、revision 实时快照、正式 Web / WebOS 与三普通账号成组验收均通过。
6. [F4-F 聊天轻量阅读回执](/features/chat-message-read-receipt-design) 已完成 A-D 批并关闭，唯一持久游标、隐私矩阵、正式 Web / WebOS 和三普通账号运行态矩阵均已通过。
7. [F4-G Docs / Wiki 普通作者贡献与协作](/features/wiki-author-contribution-collaboration-design) 已完成 A-D 批并关闭，普通作者、协作者、审核者、双版本 CAS、独立发布与公开隔离均通过。
8. [F4-H 电子宠物公开名片与隐私闭环](/features/radish-pet-roadmap) 已完成 A-D 批并关闭；公开字段白名单、即时显隐、身份 / 租户 / 软删除隔离、PC / mobile Pencil、正式 Web、双语与四主题代表矩阵均已通过，移动系统通知继续后置。
9. [F4-I 内容治理案件、证据与动作一致性](/features/content-moderation-case-evidence-action-design) 已完成 A-D 批并关闭；案件、证据、决定、动作、用户状态、五类目标、多角色正式页面和运行态验收全部通过。
10. [F4-J 内容治理申诉与处置纠正](/features/content-moderation-appeal-relief-design) 已完成 A-D 批并关闭；独立申诉、部分支持、五类恢复、用户状态纠正、正式页面和六角色成组验收均已通过。
11. [F4-K 用户屏蔽与关系交互隔离](/features/user-block-relationship-isolation-design) 已完成 A-D 批并关闭；Main 唯一真相、关系策略、关注事务、Direct 迁移兼容、通知抑制、正式 Web 和成组验收均已通过。
12. [F4-L Wiki 附件隐私与生命周期权威闭环](/features/wiki-attachment-privacy-lifecycle-design) 已完成 A-D 批并关闭；Main 权威引用、私有默认、动态 ACL、事务同步、令牌、正式 Web、六身份运行态、清理和 SQLite / PostgreSQL 验证均通过。
13. [F4-M 论坛内容版本完整性与作者恢复](/features/forum-content-version-recovery-design) 与 [F4-N 论坛内容赞赏](/features/forum-content-reward)均已完成 A-D 批并关闭。
14. [F4-O 论坛问答回答生命周期与治理闭环](/features/forum-answer-lifecycle-governance-design)已完成 A-D 批并关闭；Answer 权威生命周期、治理通知、strict migration、正式 Web、Gateway 代表矩阵与清理均已通过。
15. [F4-P 论坛帖子收藏与个人内容回访](/features/forum-post-bookmark-personal-library-design)已完成 A-D 批并关闭；私有 Bookmark 权威关系、显式幂等状态、个人稳定分页、不可用目标、migration、正式 Web、代表身份 Gateway 矩阵与清理均已通过。
16. [F4-Q 论坛标签公开发现、可见性与 SEO 闭环](/features/forum-tag-public-discovery-seo-design)已完成 A-D 批并关闭；公开判定、标签聚合、Gateway head、tags sitemap、正式 Web、代表身份与 PC / mobile 运行态矩阵均已通过。
17. [F4-R 家族 UI 统一接入与产品视觉重构](/features/family-ui-convergence-design)已经启动；A / B、family-ui `v26.7.3` 基线补充、C-0、[C-1A 代码事实审计](/frontend/f4-r-representative-page-audit)、`R1-F01`、六个 R1、`R2-C03` 与 [R2-P03 Public 只读详情变体](/records/f4-r-r2-p03-public-read-only-detail-variants-implementation-2026-08-10) 均已形成设计—实现—运行态闭环。[R2-W02 readiness](/records/f4-r-r2-w02-private-dashboard-task-rail-readiness-audit-2026-08-10)、[前端能力门禁](/records/f4-r-r2-w02-private-dashboard-task-rail-capability-gate-implementation-2026-08-10)和[局部代表设计](/records/f4-r-r2-w02-private-dashboard-task-rail-representative-design-2026-08-10)已完成，下一步接入 Notifications、Me、Circle、Pet、Private Shop 与 Workbench 正式页面；后续视觉工作继续依据家族 UI 基线优化，不从零重新设计。
18. [F4-S 公开排行榜参与资格、隐私边界与可信度治理](/features/leaderboard)已在等待 Pencil 期间完成 A-D 并关闭：五类公开白名单、共同参与资格、稳定全序、敏感 / 未知类型拒绝、只读公开身份、Web 路由和 Gateway PC / mobile 成组验收均已收口。

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
- Flutter analyze / test、已落地主路径回归和经裁决的次级移动原生能力。
- Q4 大文件、共享前端边界、全量卫生和文档归档按触达范围持续下降。

## 明确后置

- 在 `dev -> master` PR、正式发布记录、required checks 或镜像供应链门禁未通过时创建 tag 或生产部署。
- WebOS 新功能、Tauri 恢复 / 分发、Flutter 机械追平 Web 和独立移动 Console。
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
- [F4-I 内容治理案件、证据与动作一致性](/features/content-moderation-case-evidence-action-design)
- [F4-J 内容治理申诉与处置纠正](/features/content-moderation-appeal-relief-design)
- [F4-M 论坛内容版本完整性与作者恢复](/features/forum-content-version-recovery-design)
- [F4-N 论坛内容赞赏储备方案](/features/forum-content-reward)
- [F4-O 论坛问答回答生命周期与治理闭环](/features/forum-answer-lifecycle-governance-design)
- [F4-P 论坛帖子收藏与个人内容回访](/features/forum-post-bookmark-personal-library-design)

## 文档规则

- 本页只维护总体方向和阶段衔接；今日任务以 `current.md` 为准。
- 历史批次和命令级验证进入 records、changelog 或 archive。
- 产品、工程和发布门禁分别维护在 P3-12-E、E8-Q 与验证 / 部署专题中，不在入口文档重复完整清单。
