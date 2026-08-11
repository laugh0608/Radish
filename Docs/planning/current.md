# 当前进行中

> 本页是新会话快速入口，只维护当前阶段、最近结论、当前执行顺位和必要停止线。历史批次与命令级证据统一查看[已完成摘要](/planning/archive)、[记录索引](/records/)和[开发日志](/changelog/)。

## 当前状态

- **阶段**：`Phase 4：长期维护与功能完成`
- **当前子阶段**：`F4 既有功能持续完成`
- **工程第一顺位**：`F4-R R3-P05 Public Docs 与 Legal 实施方案`
- **产品下一顺位**：`先审计并拆分超限 PublicDocsApp.tsx 的职责所有权，冻结受保护资源、正文锚点、保留 slug、来源返回与公开 head 契约；方案确认后再实施`
- **复核日期**：`2026-08-11`
- **正式主线**：Web 优先；PC / mobile 浏览器共同验收。Flutter 是次级移动原生产品线，WebOS `/desktop` 仅历史兼容，Tauri 暂时弃用并等待未来重新评估。
- **最近正式发布**：`v26.7.1.1204-release`（2026-07-12）。

## 最近结论

- `2026-08-11` 已完成并关闭 [R3-P04 Public Forum 浏览族正式实现与 Gateway 验收](/records/f4-r-r3-p04-public-forum-browse-implementation-2026-08-11)：列表、分类、搜索、标签与 Question / Poll / Lottery 统一为 PC 连续结果主轴—从属阅读侧栏，Mobile 固定结果先于辅助信息并以横向筛选带避免页面溢出；browse 专属样式从详情 / Compose 所有权中隔离，原超限样式由 `1646` 行降至 `1185` 行。URL、来源返回、SEO、局部权威状态、类型字段 / 排序与 Compose 登录回流保持不变；运行态补正标签重复公开徽标、内部编码 slug 和不存在标签的重复标题。Client `539 / 539`、type-check、Lint、production build 与静态继承契约通过；Gateway 覆盖 PC `1440 × 900`、Mobile `390 × 844`、七类入口、双语 / 双主题、历史前进后退、自定义日期与不存在标签，无横向溢出，新开稳定标签页 `0 warning / 0 error`，服务已停止。下一步进入 `R3-P05 Public Docs 与 Legal` 实施方案。
- `2026-08-11` 已完成 [R3 路由继承实施分批审计](/records/f4-r-r3-route-inheritance-batch-audit-2026-08-11)：现有 R3 继承方向总体成立，已关闭 R1 / R2 页面不重复迁移；补齐 Console Dashboard 与 Channel Discoverability 的继承来源，并确认 Settings / Profile、公开 Docs 详情与 Legal 尚未由前述 R2 实现关闭。后续拆为六个批次，首批固定为 `R3-P04 Public Forum 浏览族`；Forum browse 样式需先与已关闭的详情 / Compose 所有权隔离，保持 URL、SEO、局部权威状态与 Mobile 主任务优先，不新增 API、权限、业务字段或 Pencil 画板。
- `2026-08-11` 已完成并关闭 [R2-A02 Author 列表、修订与 Forum Compose 正式实现及 Gateway 验收](/records/f4-r-r2-a02-author-list-revisions-forum-compose-implementation-2026-08-11)：Docs / Forum 共用双快照差异，默认 `vN-1 → vN`、可切选中版本到当前版本，PC 并排、Mobile unified；Docs Mine 收敛为权威筛选—连续列表—从属上下文；正式 `/forum/compose` 只挂载一个 Composer，PC `54dvh`、Mobile `72dvh` 并可原位全屏。Gateway 以种子管理员覆盖 PC `1440 × 900`、Mobile `390 × 844`、双语 / 代表主题、无横向溢出和干净标签 `0 warning / 0 error`；运行态修正 Mobile 选中文档行断点与 Composer 图标按钮可访问名称。Client `536 / 536`、type-check、Lint 与 production build 通过；既有三处 `DateTime.Now` baseline 和全仓卫生债务继续留在独立维护线。
- `2026-08-10` 已按人工反馈完成并确认 [R2-A02 Author 列表、修订与 Forum 发布差异第二轮局部代表设计](/records/f4-r-r2-a02-author-list-revisions-forum-compose-representative-design-2026-08-10)：Mine 保持权威筛选—连续列表—从属上下文；Revisions 改为 PC `vN-1 → vN` 并排差异与 Mobile unified diff，并以同一模式覆盖帖子 / 文档；Forum Compose 改为 PC / Mobile 半屏停靠与全屏写作双形态，展开 / 收起必须保留同一草稿、上传与提交状态。唯一活动设计源现有四张 PC、四张 Mobile 和一张必要状态板；九板均结束 placeholder，原生根边界审计 `outOfRoot = 0`，逐板截图无裁切、塌陷或横向溢出，设计已通过 Pen 原生保存落盘。未启动服务 / 浏览器或修改正式页面代码；下一开发日直接进入双快照差异与 Composer 承载状态门禁。
- `2026-08-10` 已完成 [R2-A02 Author 列表、修订与 Forum 发布差异能力门禁](/records/f4-r-r2-a02-author-list-revisions-forum-compose-capability-gate-implementation-2026-08-10)：`AuthorGetList` 落地数据库权威范围 / 草稿阶段筛选、稳定分页、结构化无效参数与列表请求代际；Revision history / detail 独立 unavailable / stale 和请求代际；正式 Forum Compose 使用页面内共享核心，WebOS 保留薄 Bottom Sheet 外壳；版本化本地草稿按账号隔离并与 Workbench 共源；发布器系统文案完成双语且失败只反馈一次。后端全量 `1224 passed / 39 skipped`，HTTP `37 / 37`、Client `533 / 533`、type-check、Lint 与 production build 通过；未启动服务 / 浏览器，未修改 Pencil、数据库、migration、权限或业务范围。下一步进入 R2 局部代表设计。
- `2026-08-10` 已完成 [R2-A02 Author 列表、修订与 Forum 发布差异 readiness](/records/f4-r-r2-a02-author-list-revisions-forum-compose-readiness-audit-2026-08-10)：分级继续保持 `R2`，Docs Mine / Revisions 与 Forum Compose 继承 `R1-A01 / R1-P02`，不新增数据库、权限、业务实体或移动壳层。Pencil 前需先关闭五组门禁：AuthorGetList 权威筛选 / 稳定分页 / 角色本地化，Revision history/detail 独立状态与请求代际，共享 Composer 核心 / WebOS Sheet / 正式页面承载分层，Forum 本地草稿按账号隔离并与 Workbench 共源，以及发布器双语 / 单一结构化反馈。该审计未修改运行时代码、Pencil、API 或数据库，未启动服务 / 浏览器；门禁方案等待确认。
- `2026-08-10` 已完成并关闭 [R2-W02 Private 仪表 / 任务侧栏正式实现与 Gateway 验收](/records/f4-r-r2-w02-private-dashboard-task-rail-implementation-2026-08-10)：六个正式入口统一使用“主任务宽列—紧凑摘要—窄辅助轨”，Mobile 固定主任务先于默认收起的辅助信息；共享 `WebTaskRailDisclosure` 提供 `720px` 折叠语义，Circle / Pet / Me 已删除辅助轨前置。Client `526 / 526`、type-check、Lint、production build 与静态契约通过；Gateway PC `1440 × 980`、Mobile `390 × 844` 无横向溢出，实际折叠交互和 Me / Workbench / Private Shop 顺序通过，浏览器 `0 error`，服务已停止。种子库没有 Pet、订单、权益或背包业务数据，因此本批只复核对应空态，有数据结构继续由自动化与构建守卫；未制造业务数据或修改后端边界。下一步进入 `R2-A02` readiness，不提前推进 R3。
- `2026-08-10` 已完成并确认 [R2-W02 Private 仪表 / 任务侧栏局部代表设计](/records/f4-r-r2-w02-private-dashboard-task-rail-representative-design-2026-08-10)：唯一活动 `.pen` 新增 PC 六页局部代表板、Mobile `390px` 六页顺序板和必要关键状态板，固定“主任务—紧凑摘要—辅助轨”及 Mobile“主任务—摘要—折叠辅助信息”顺序；覆盖权威首次读取、unavailable / stale、dirty、Pet 原幂等键重试、Private Shop 结构化诊断和 Me 局部来源失败。三板均已结束 placeholder，节点边界和截图复核无裁切、塌陷、无效图标或横向溢出；设计已通过 Pen 原生保存落盘，下一步进入正式页面实现。
- `2026-08-10` 已完成 [R2-W02 Private 仪表 / 任务侧栏前端能力门禁](/records/f4-r-r2-w02-private-dashboard-task-rail-capability-gate-implementation-2026-08-10)：Notifications / Circle / Pet / Me 显式区分权威未读取、unavailable 与 stale；通知偏好和 Pet 资料具备 dirty 离开保护；Pet 模糊照料结果保留原幂等键并以权威快照裁决；Private Shop 保留本地化结构化诊断；`MeApp.tsx` 从 `1816` 行降至 `1465` 行。Notification 词元按真实业务域从超限边缘的 community 资源拆出。Client `525 / 525`、type-check、Lint、production build 与 diff 检查通过；未修改 Pencil、API、数据库、权限或业务范围，未启动服务 / 浏览器。
- `2026-08-10` 已完成并关闭 [R2-P03 Public 只读详情变体正式实现与 Gateway 验收](/records/f4-r-r2-p03-public-read-only-detail-variants-implementation-2026-08-10)：商品详情接入综合评分、五星分布、稳定分页、Completed 资格、本人评价 CRUD、dirty 离开确认、CAS `409` 草稿保留、unavailable / stale 和 `ProductReview` 举报；公开主页接入昵称 / `用户名#公开ID`、真实签名空态、当前等级 / 等级名、加入时间和独立权威统计。DbMigrate 补齐商品评价迁移后，Gateway 以匿名与种子管理员覆盖 PC `1440 × 900`、Mobile `390 × 844`、零评价、临时 Completed 资格、创建 / 编辑 / 删除、登录回跳、dirty、真实 CAS 冲突和举报弹窗；新页签稳定态 `0 warning / 0 error`，临时订单与评价均已精确清理。Client `518 / 518`、type-check、Lint 与 production build 通过；未修改 Pencil，下一步进入 `R2-W02` 设计前审计。
- `2026-08-09` 已完成 [R2-P03 商品评价与公开等级能力门禁](/records/f4-r-r2-p03-product-review-public-level-capability-gate-implementation-2026-08-09)：公开资料只增加当前等级 / 等级名；商品评价固定 Completed 订单资格、每用户每商品一条、`1..5` 星、可选 `500` 字评论、唯一约束、数据库聚合和单调 CAS，并以 `ProductReview` 复用既有治理 / 申诉权限与事务。后端 `1220 passed / 39 skipped`，HTTP `36 / 36`、Client `514 / 514`、Console `83 / 83` 及静态构建通过；未启动服务或浏览器。
- `2026-08-09` 已按七轮反馈完成并确认 [R2-P03 Public 只读详情变体局部代表设计](/records/f4-r-r2-p03-public-read-only-detail-variants-representative-design-2026-08-09)：三张 PC 统一为 `24px` 外边距、`12px` 核心分栏和 `14–20px` 主要卡片内距，长内容、商品详情、公开主页保持独立；PC / Mobile 购买与发消息主按钮统一暖白前景。商品 Mobile 固定顶部大图—商品信息—购买动作—评价摘要 / 预览—详情；公开主页按昵称、`用户名#公开ID`、签名、等级 / 等级名和加入时间分区，PC 右侧统计 / 操作保持既有结构。商品价格统一使用 `carrot` 萝卜图标。连同评价零态、资格、dirty、`409`、unavailable / stale 和治理 / 申诉状态，共八个顶层画板均无 placeholder、裁切、塌陷或横向溢出。
- `2026-08-09` 已确认 [R2-P03 商品评价与公开等级范围](/records/f4-r-r2-p03-product-review-public-level-scope-audit-2026-08-09)：不扩入公开经验详情、用户背景上传、评价媒体 / 回复 / 有用 / 追评 / 标签 / 推荐排序 / 实时刷新，不新增 Console 权限、审批流或独立评价中心。
- `2026-08-09` 已完成 [R2-P03 Public 只读详情变体能力门禁实现](/records/f4-r-r2-p03-public-read-only-detail-variants-capability-gate-implementation-2026-08-09)：正式商品详情以当前字符串 LongId 复用既有 `Product` 举报弹窗，匿名沿用统一提示且不新增 URL intent；公开主页由主资料独立裁决 `404 / unavailable`，统计以规范化公开 identifier 局部加载 / 重试，未取得权威结果时不伪造零值或粉丝数。公开统计、帖子和评论 consumer 继续保留结构化错误；Client `512 / 512` 测试、Lint、type-check 与 production build 通过。本批未修改 Pencil、启动服务或浏览器，下一步等待 Pencil 授权。
- `2026-08-09` 已完成 [R2-P03 Public 只读详情变体设计前代码事实与能力覆盖审计](/records/f4-r-r2-p03-public-read-only-detail-variants-readiness-audit-2026-08-09)：分级继续保持 R2，Docs 详情、商品详情、公开主页与 Legal 继承 R1-P02 / R1-F01，不复制四个完整路由。现有 API、权限、URL、LongId、购买幂等、关系版本、治理事务和存储边界足够；Pencil 前只需关闭两项窄前端门禁：正式商品详情复用既有 `Product` 举报弹窗，以及公开主页以主资料权威区分 `404 / unavailable`、把统计失败局部化并停止伪装未读取粉丝数。当前未修改 Pencil、运行时代码或 API，未启动服务 / 浏览器，也未推进 R3。
- `2026-08-09` 已完成并关闭 [R2-C03 Console 设置与权限矩阵正式实现及 Gateway 验收](/records/f4-r-r2-c03-console-settings-permissions-implementation-2026-08-09)：PC 落地只读 / 内建角色保护、Low 轻量确认、Medium 显式确认，以及 dirty、CAS 版本、结构化 `409` 草稿保留；Mobile 落地独立连续角色目录、以权威快照渲染的只读权限详情与 Low 设置 `BottomSheet`，权限 key 同时展示含义，Medium 在列表和提交 handler 双重保持 PC-only。Console `83 / 83` 测试、Lint、普通 / strict type-check、production build 与权限扫描通过；Gateway PC `1440 × 900`、Mobile `390 × 844`、真实角色 / 设置读取、unavailable、空结果、五项胶囊导航和无横向溢出通过，稳定态干净页签无 `warning / error`。运行态修正系统设置初始 `config` 未装载时的空值崩溃；未保存角色授权或系统设置，临时 OIDC 增量已精确清理，服务已停止。API、权限、URL、LongId、CAS、结构化错误、事务和存储边界保持不变。

- `2026-08-09` 已完成并确认 [R2-C03 Console 设置与权限矩阵局部代表设计](/records/f4-r-r2-c03-console-settings-permissions-representative-design-2026-08-09)：唯一活动 `.pen` 包含权限矩阵与内建角色 PC、系统设置与确认 PC、角色结构只读 Mobile、权限矩阵只读 Mobile、Low 设置编辑 Mobile 和必要关键状态六个独立顶层设计板；Mobile 页面复用 `AdminLayout`、Console 五项入口与共享 `Mobile Tab Bar`，不使用外部说明板或多页面合并容器，并固定为“角色目录选择—当前角色权限详情—设置列表选择 / Low 编辑底板”三条直接任务流。设计覆盖只读 Operator、`System / Admin` 内建保护、dirty / 离开保护、并发 `409`、unavailable / stale、真实空态和 Medium 显式确认；Settings / Profile 仅表现已有自服务能力，不进入 `console.*`。Pencil 静态复核已通过，正式代码随后按确认设计落地。
- `2026-08-09` 已完成 [R2-C03 Console 设置与权限矩阵代码能力门禁](/records/f4-r-r2-c03-console-settings-permissions-capability-gate-implementation-2026-08-09)：内建角色保护、角色命令与唯一性、权限矩阵单调聚合版本 / 原子 CAS、系统设置配置—审计共同提交、结构化 `400 / 409`、显式 Medium 确认和 Settings / Profile 真实性已闭合；后端 `1206` 项与 Console `78` 项测试通过。该门禁没有修改 Pencil、启动服务或浏览器，后续设计继续保持现有 API、权限、URL、LongId、事务和存储边界。
- `2026-08-09` 已完成 [R2-C03 Console 设置与权限矩阵设计前代码事实与能力覆盖审计](/records/f4-r-r2-c03-console-settings-permissions-readiness-audit-2026-08-09)：分级继续保持 R2，沿用 `AdminLayout`、Console 五项真实入口与 R1-C01 / C02，不建立新壳层；审计识别出只读权限矩阵路由不可达、内建 `System / Admin` 无权威保护、授权版本 / 事务不可靠、系统设置冲突被包装为 500、配置与审计未共同提交，以及 Settings / Profile 真实能力口径偏旧等设计前代码门禁，上述门禁已由同日能力批关闭。
- `2026-08-09` 已完成并关闭 [R1-C02 Console 案件治理 / 审计成组实现与 Gateway 验收](/records/f4-r-r1-c02-console-moderation-implementation-2026-08-09)：PC 落地案件队列—受权证据—决定边界三段治理桌；Mobile 落地连续三列案件队列、按需筛选、五项胶囊导航，以及保留 `60px` Console 品牌栏、隐藏底部导航的单任务详情。Gateway PC `1440 × 900`、Mobile `390 × 844`、管理员真实读取、筛选空结果、申诉空队列与详情 `404` / unavailable 通过且无横向溢出；运行态同步修正 Mobile 列表通用工具栏、未选详情占位与刷新图标尺寸。View-only、Reviewer 无 Action、`409`、stale 和申诉 / Chat 边界继续由定向测试守卫；未触发治理写操作或新增临时业务数据。
- `2026-08-09` 已完成并确认 [R1-C02 Console 案件治理 / 审计正式代表设计](/records/f4-r-r1-c02-console-moderation-representative-design-2026-08-09)：PC `1440 × 900` 固定为案件队列—受权证据—决定边界三段治理桌，Mobile `390 × 844` 固定为带 Console 五项真实入口的连续案件队列，以及保留顶部品牌栏、隐藏五项底部导航的单案件全屏任务；关键状态覆盖 View-only、Reviewer 无 Action、`409`、stale / unavailable、筛选空结果和申诉 / Chat 边界。四个顶层画板均无 placeholder、裁切或溢出，后续正式代码已按该设计落地。
- `2026-08-09` 已完成 [R1-C02 Console 案件治理 / 审计前端能力门禁实现](/records/f4-r-r1-c02-console-moderation-capability-gate-implementation-2026-08-09)：补齐 PostAnswer 筛选 / 双语 / Revision 前置校验，以 URL 驱动案件显式选择、筛选、分页和 Case / Appeal mobile 全屏任务；队列 stale、详情 unavailable / stale、View-only 权限表面和 handler 写入冻结均已闭合。Console `73 / 73` 测试、Lint、strict type-check、production build、repo hygiene 与 `git diff --check` 通过；未修改 Pencil、API、数据库、权限、LongId、幂等或事务边界，未启动服务或浏览器。
- `2026-08-09` 已完成 [R1-C02 Console 案件治理 / 审计设计前代码事实与能力覆盖门禁](/records/f4-r-r1-c02-console-moderation-readiness-audit-2026-08-09)：服务端 Case / Appeal、六类目标、权限、版本、幂等、结构化 `404 / 409` 和 Main / Chat 事务边界足够，不需要新增 API、数据库或权限；Console 前端仍需先闭合 PostAnswer 筛选 / 双语 / Revision 前置校验、案件 URL 与 mobile 全屏任务、队列 / 详情 stale / unavailable，以及 View-only 写入表面。该窄能力批确认并关闭前不修改 Pencil、不进入代表设计，也不推进 R2-C03。
- `2026-08-08` 已完成[日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-08)：按 Asia/Shanghai 日期复核 `11` 个提交和 `131` 个变更文件，确认今日依次关闭 R1-P02、R1-A01、R1-W01 与 R1-C01；代码—文档反查已修正 Public Forum 互动能力、F4-R 总专题、Chat / Author 当前说明、Console Orders 指南、八月日志和记录索引。明日只进入 R1-C02 设计前门禁，不提前修改 Pencil 或代码。
- `2026-08-08` 已完成并关闭 [R1-C01 Console 订单表格—明细成组实现与运行态验收](/records/f4-r-r1-c01-console-orders-implementation-2026-08-08)：PC 落地约 `48px` 单行薄表格与显式选择后的 inspector；Mobile 落地连续三列订单行、按需筛选层、五项胶囊导航和隐藏全局导航的全屏详情任务。只读 Operator、重试确认 / `409`、备注冲突、详情不可用 / 列表 stale、筛选空结果、LongId URL 与受权资源回跳均通过。Console `66 / 66` 测试、Lint、strict type-check、production build、repo hygiene 与 `git diff --check` 通过；Gateway PC `1440 × 900` / Mobile `390 × 844` CSS viewport 无横向溢出，稳定态浏览器控制台 `0 error / 0 warning`。六库已按验收前 SHA-256 原样还原并通过完整性检查，临时数据清零、端口停止；未修改 Pencil 或推进 R1-C02。
- `2026-08-08` 已完成并确认 R1-C01 整组正式代表设计：PC `1440 × 900` 使用约 `48.5px` 单行薄表格和按需详情 inspector；Mobile `390 × 844` 使用连续三列订单行、Client 风格五项胶囊导航和隐藏全局导航的全屏详情任务，中列承载商品类型 / 单价与支付证据；只读 Operator、重试确认 / `409`、详情 `404` / 列表 stale、筛选空结果以局部状态板表达。全部画板布局检查零告警，下一步进入正式 Console 代码实现。
- `2026-08-08` 已完成 [R1-C01 Console 订单表格—明细设计前代码事实与能力覆盖门禁](/records/f4-r-r1-c01-console-orders-readiness-audit-2026-08-08)：正式 `/console/orders` 已承接列表、独立详情、筛选 / 分页、备注、履约重试和受权资源回跳，现有 API、权限、结构化错误、LongId 与事务边界足以进入代表设计；当前结构债集中在卡片与大缝隙、重复摘要 / 动作、主表被常驻右栏挤窄，以及 mobile 仍依赖横向表格。正式方向建议采用连续表格主轴 + 按需详情 inspector，并校正 `Failed` 筛选、可重试统计、LongId 输入和通用发放文案；确认前未修改 `.pen` 或代码。
- `2026-08-08` 已完成并关闭 [R1-W01 Private 消息工作区成组实现与运行态验收](/records/f4-r-r1-w01-messages-web-implementation-2026-08-08)：唯一活动 `.pen` 已形成 PC `1440`、Mobile `390` 与必要关键状态代表画板；正式 `/messages` 落地连续会话列表 / 消息主轴 / 按需成员上下文、搜索与在线成员互斥、紧凑 Pin、Mobile 单任务流与共享 Bottom Sheet，并统一 `720px` 结构断点和账户切换 reset。Gateway 普通 User + Accepted 互关 Direct 覆盖文本、引用、图片、Reaction、Pin、已读边界、搜索、成员互斥、焦点恢复与无横向溢出；运行态修正 Mobile 按钮可访问名称和 Chat 私密附件绑定消息后的 ACL 查询翻译错误。后端 `1194` 项、Client `509` 项测试、Lint、三项 type-check 与 production build 通过；临时 Main / Chat / Message / Log / OpenIddict 数据和上传文件均已清零，服务端口已停止。
- `2026-08-08` 已完成并关闭 [R1-A01 Author 编辑代表页成组实现与运行态验收](/records/f4-r-r1-a01-author-editor-implementation-2026-08-08)：正式 `/docs/edit/:id` 已按确认设计落地标题 / Markdown 正文主轴、紧凑版本与动作上下文、PC `320px` context rail 和 Mobile Bottom Sheet；Gateway 下普通 Owner `TestUser` + Published 正式 v1 + `Editing` 共享草稿 v2 + Accepted Editor 覆盖 PC `1440 × 1024` 与 Mobile `390 × 844`，无横向溢出。运行态发现并修正移动端标题被动作区挤压的问题，任务头由 `261px` 收敛到 `170px`；Client `504` 项测试、Lint 与 production build 通过，临时文档、通知、审计和登录会话增量均已清零。
- `2026-08-08` 已完成 [R1-W01 消息工作区能力门禁修复](/records/f4-r-r1-w01-messages-capability-gate-implementation-2026-08-08)：ChatMessage 举报按 tenant / channel / reporter `CanView` 在快照与写入前失败关闭，LongId 全链保持字符串；失败重试复用原键并允许 Pending Direct 模糊结果同键回放；History / MessageWindow 统一稳定 404，Client 在权威失权时清理服务端缓存并隐藏旧正文。后端全量 `1193` 项、Client 全量 `501` 项测试与 production build 通过；撤回权限仍冻结待裁决。
- `2026-08-08` 已完成 [R1-W01 消息工作区设计前代码事实与能力覆盖门禁](/records/f4-r-r1-w01-messages-readiness-audit-2026-08-08)：正式 `/messages` 与 WebOS 已共用同一 ChatApp / API / Store / Hub，Direct、搜索、Reaction、Pin、回执与实时主体能力均已承接；审计发现 ChatMessage 举报 ACL / LongId、失败重试幂等和 History / MessageWindow 错误契约三组代码阻断，撤回能力证据与频道角色权限另需裁决。该临时 readiness 不改变 `R1-A01` 第一顺位。
- `2026-08-08` 已完成 [R1-A01 Author 能力覆盖门禁修复](/records/f4-r-r1-a01-author-capability-gate-implementation-2026-08-08)：新增关系授权的 Author Revision history / detail，支持最新终态证据与载荷清理标记，统一 Author 写响应 evidence，并将 Apply 唯一绑定服务端 `Draft.BaseDocumentVersion`；Pending Invitee 的 Draft / Revision 附件、正式 Slug / Published 公开链接和终态保留时间锚同时按最窄权限闭合。后端全量 `1179` 项与 Client 全量 `492` 项测试、production build 通过；PostgreSQL 条件用例因本机未配置继续显式跳过。
- `2026-08-08` 已完成 [R1-A01 Author 设计前代码事实与能力覆盖门禁](/records/f4-r-r1-a01-author-readiness-audit-2026-08-08)：正式 Web 已承接创建、共享草稿、保存、提交 / 撤回、协作者和冲突恢复主体流程，WebOS 没有应迁回普通 Author 的独占能力；代表身份固定为“登录普通 Owner + Custom 已发布文档 + `Editing` 活跃共享草稿 + Accepted Editor”。审计发现的四项代码缺口已由同日修复批闭合。
- `2026-08-08` 已完成并关闭 [R1-P02 帖子详情成组实现与运行态验收](/records/f4-r-r1-p02-public-detail-implementation-2026-08-08)：正式详情按已确认 PC / Mobile 代表设计落地，接入帖子 / 回帖点赞、reaction、赞赏和两级回帖；`PublicForumDetail.tsx` 从 `2292` 行收敛到 `1500` 行。Gateway 下普通读者 `TestUser` + 普通帖子覆盖 PC `1440 × 1000`、mobile `390 × 844`、`zh-CN / en-US`、`default / guofeng`、点赞 / reaction 可逆写入、两级回帖目标、来源 / 分享与无横向溢出；本地赞赏开关按既有默认关闭边界保持不变。
- `2026-08-06` [正式 Web 能力覆盖复核](/records/f4-r-formal-web-capability-coverage-audit-2026-08-06)的四项推进决策已确认：正式帖子详情承接点赞、reaction 与回帖的回帖；作者删除、投票和抽奖按作者 / 类型状态保留；基础资料、头像和显示时区进入正式 Private Web；转账、资产安全和统计脱离 F4-R，后续单独裁决迁移或退役。R1-P02 的 PC / Mobile 正式代表设计随后均已确认并进入成组代码实现。
- `2026-08-06` R1-P02 多轮评审暴露出跨页面族风险：现有代表页审计能够判断布局和视觉复用，但没有证明正式 Web 已承接项目现有能力。R1-P02 Pencil 暂停继续修补，改为先建立正式 Web、WebOS 历史实现、既有专题与 API / Service 的能力覆盖矩阵；不把“当前 Public 未接入”自动固化为长期产品边界。
- `2026-08-06` 已确认长期视觉演进原则：F4-R 当前批次负责建立并落地新的 Radish 家族 UI 基线；本批完成后的视觉重构默认根据最新 family-ui、项目差异附录、已确认代表页和正式代码进行优化与更新，不从零重新设计。只有全新产品形态、现有信息架构无法承载目标任务或功能边界发生结构性变化并获裁决时，才重新建立页面范式。
- `2026-08-05` 已完成 [R1-P02 Public 详情与互动代码事实与设计边界审计](/records/f4-r-r1-p02-public-detail-interaction-audit-2026-08-05)：现有 API、权限与写入边界保持不变；代表设计固定为普通帖子登录读者的 PC 1440 / Mobile 390 完整画板，加问答、身份回流和既有浮层关键状态。当前结构债集中在参与入口重复、解释型侧栏、卡片套卡片、mobile 辅助区长尾，以及 `PublicForumDetail.tsx` 达 `2292` 行；Pencil 方向确认前不进入代码。
- `2026-08-05` [R1-P01 公开发现成组实现与运行态验收](/records/f4-r-r1-p01-public-discover-implementation-2026-08-05)已关闭：migration 与 host runtime 通过；Gateway 匿名 / 种子管理员登录回流、PC `1440 × 1000`、mobile `390 × 844`、`zh / en`、`default / guofeng`、真实链接和 Console 治理只读路径通过，页面无横向溢出。
- `2026-08-05` 运行态修正统一 JSON `long` 字符串契约与前端 `number` 假设不一致的根因；公开计数现使用字符串 wire contract、`BigInt` locale 格式化和独立复数判别量，不再回显 i18n 资源键。
- `2026-08-05` 已完成并确认 [R1-P01 mobile 与统一公开读模型设计](/records/f4-r-r1-p01-mobile-public-read-model-design-2026-08-05)：`390px` 代表页将搜索和真实内容前置，把参与动作放入精选讨论，并把社区脉搏、贡献者上下文与知识主题分别嵌入首屏、中段和流后；首轮反馈后，混合流已从异色列表拼接重构为焦点事件、连续编号轨道和嵌入式贡献者节点。画板无裁切、溢出或失效图标，`2x` 视觉自检通过，用户已确认。
- `2026-08-05` 已完成 [R1-P01 公开发现成组实现](/records/f4-r-r1-p01-public-discover-implementation-2026-08-05)：Channel 默认 Hidden、migration、版本化 opt-in、append-only 审计、Console `view / manage` 权限和精确租户治理形成闭环；既有 Public 频道不会自动进入匿名流。
- `2026-08-05` “社区正在发生”已落地为统一只读投影：公开 Wiki 首次发布、当前跨帖神评、帖子、问答与显式开放的频道摘要由服务端按 snapshot cutoff 和稳定 keyset 游标合并，响应 `no-store` 且整流失败关闭；`@radish/http` 与 `/discover` 只消费该投影，不再拼接旧列表接口。
- `2026-08-05` `/discover` 已按获批设计实现首条焦点事件、连续编号轨道、嵌入式贡献者、社区脉搏和 PC 非对称洞察区；Client 全量 `489`、Console 全量 `61`、后端专题 `57` 项测试及前后端 production build 通过。PostgreSQL 条件用例仍因本机未配置而显式跳过，不把 SQLite migration 与 Gateway 运行态表述为 PostgreSQL 实跑。
- `2026-08-04` 已完成 [R1-P01 公开社区发现结构研究](/records/f4-r-r1-p01-public-discover-structure-study-2026-08-04)，并确认 `R1-P01 / 社区发现 / PC 1440` 为首个正式 Public 内容流视觉基准：采用现代自然紧凑表面、非对称主次、连续扫描行、Geist 无衬线、`10–16px` 克制圆角、极轻阴影和内嵌数据反馈；灰玉 `#5d6c57`、墨蓝 `#435c74` 与国风暖白 `#f4efe6 / #fbf7f0` 形成 Radish 家族色。
- `2026-08-04` 已按最终裁决清理活动设计源：旧“社区脉搏”PC / mobile、全部参考试验与失败稿均已删除；当前活动源保留已确认的 `1440 × 900` PC、`390px` mobile、`8` 个必要组件母版和主题变量，失败稿继续只由 Git 追溯。
- `2026-08-03` 已创建唯一活动设计源 `radish-web-family-ui-v1.pen`：文件名以主版本管理，普通迭代继续更新 `v1` 并由 Git 留存；原四个大型 `.pen` 不删除、不改名，统一转为只读留档。
- `2026-08-03` `R1-P01` 两轮业务页面评审均未通过。第二轮虽分开了分类、标签和结构化状态，也增加了分类、讨论、社区动态与相关内容区，但绝大多数视觉篇幅仍由帖子占据，全宽分类浏览笨重且偏离参考图的异构工作台节奏；该稿只由 Git 留档，不是实现基线。
- `2026-08-03` 已把 `guofeng` 品牌目标从旧胭脂改为低饱和灰玉 `#5d6c57`，悬停 `#6e736d`，常规操作继续使用墨蓝 `#435c74`；R1-P01 页面使用现有灰玉 / 墨蓝语义 token，全局品牌 token 因影响全部页面族留待共享主题批成组治理。
- `2026-08-03` 已完成 `R1-F01`：统一四主题变量矩阵、Brand / Workbench Action 前景语义、按钮 / 输入 / 状态 chip、加载 / 空态 / 错误 / 权限状态槽，以及 PC / 390px mobile 壳层契约。
- `2026-08-03` 已将 F4-R 上游基线升级为 RadishX `family-ui v26.7.3`：通用副本使用灰玉参考默认并新增 `text-on-brand`；基线补充批当时显式保留 `guofeng` 胭脂品牌，后续 R1-P01 灰玉裁决不改写该历史事实。
- `2026-08-03` 已复核新版全部规范正文、token 和参考索引；27 张参考图仅迁入 family-ui `reference-ui/`，Git blob 全部 `R100` 相同，既有页面族映射继续有效，并强化“观察、拆解、提炼、转译”与禁止照抄边界。
- `2026-07-30` 已将 Pencil 协作调整为代表页驱动：功能、文案、权限和状态机服从专题与代码；R1 / R2 维护必要代表设计，R3 继承实现并通过真实页面截图复核，不再维持逐路由设计镜像。
- `2026-07-30` 已完成 C-1A 代码事实审计：裁决 `7` 个 R1、`4` 个 R2 与 R3 继承表；Console 普通表格 / 明细和案件治理 / 审计因布局、动作与 mobile 模型不同，拆为两个完整代表类型。
- `2026-07-30` 已完成并关闭 [F4-S 公开排行榜参与资格、隐私边界与可信度治理](/features/leaderboard)：公开类型固定为经验、发帖、评论、人气、热门商品；余额、累计消费与购买数量退出匿名排名；列表、总数和个人排名统一资格与稳定排序，读取不再补写公开身份。D 批通过 Gateway PC / mobile、匿名 / TestUser、旧路由和失败契约矩阵，并修正未知整数类型被框架枚举绑定提前截断的问题。
- `2026-07-30` 已完成镜像漏洞门禁分层：`CRITICAL` 与存在修复版本的 `HIGH` 默认阻断，无修复 `HIGH` 留痕但不阻断；原始报告、策略裁决和限期精确例外均可追溯。
- 多端顺位调整为 Web 优先、Flutter 次级；Tauri 暂时弃用，保留历史验证资产但不进入开发、UI、CI、发布或验收门禁，未来只在桌面原生价值、目标用户和维护预算明确时重新评估。
- PR `#65` 已集成七月下旬 Wiki 作者协作、宠物公开名片、内容治理与申诉、用户屏蔽、Wiki 附件及生产迁移编排等成组成果；CI 门禁同时修复真实数据库配置探测、SQLite 连接串解析和依赖安全问题。
- [F4-L Wiki 附件隐私与生命周期权威闭环](/features/wiki-attachment-privacy-lifecycle-design)已完成 A-D 批并关闭：Main 权威引用、Wiki 私有默认、动态 ACL、受保护资源、六身份 Gateway 矩阵及 SQLite / PostgreSQL 验证形成闭环。
- [F4-M 论坛内容版本完整性与作者恢复](/features/forum-content-version-recovery-design)已完成 A-D 批并关闭：Post / Comment Revision、CAS、完整快照、旧历史受权兼容、安全恢复、正式 Web 与多身份 PC / mobile 矩阵均通过。
- F4-M-D 验收修正了版本摘要时间双真相和正式 `/me` 缺少退出入口的共同根因；临时数据残留为 `0`，六库完整性与严格 migration verify 通过。
- [F4-N-D 成组验收](/records/f4-n-d-forum-content-reward-stage-acceptance-2026-07-27)已通过并关闭专题：Post / Comment 登录回流、资产守恒、Outbox、Log 双分录、通知定位及 PC / mobile 代表矩阵形成闭环。
- D 批修正了 `reward` 登录返回意图缺失和 Reliable Task camelCase payload 反序列化契约根因；测试数据、运行设置、浏览器、服务和备份均已清理，六库严格 migration verify 通过。
- [F4-O 论坛问答回答生命周期与治理闭环](/features/forum-answer-lifecycle-governance-design)已完成 A-D 批并关闭：PublicId、服务端分页、独立 Revision、附件、CAS、采纳事件、治理申诉、可靠通知、正式 Web 与 strict migration 形成闭环。
- [F4-O-D 成组验收](/records/f4-o-d-forum-answer-lifecycle-stage-acceptance-2026-07-28)已通过：匿名、问题作者兼管理员、回答作者的 PC / mobile 代表矩阵覆盖创建、编辑、恢复、采纳与撤销；其余权限、失败和治理边界由自动化回归覆盖。
- D 批修正 migration 前置校验加载未来字段、正式 Web 新旧回答区重复渲染两项共同根因；临时帖子、回答、Revision、事件、通知、Outbox、浏览历史和经验副作用均已清理，六库完整性与严格 verify 通过。
- 当前机器未配置 PostgreSQL 集成测试环境，相关条件用例保持显式跳过，不把 SQLite 结果表述为 PostgreSQL 实跑。
- [F4-P 论坛帖子收藏与个人内容回访](/features/forum-post-bookmark-personal-library-design)已完成 A-D 批并关闭：Main 私有关系、显式幂等状态、Post 锁序、稳定分页、不可用目标、正式 Web 与代表运行态矩阵形成闭环。
- [F4-P-D 成组验收](/records/f4-p-d-forum-post-bookmark-stage-acceptance-2026-07-29)覆盖匿名、普通收藏者、作者和第三方读者，以及 PC / mobile、`zh / en`、代表主题、同状态重试、多标签并发和无跨域副作用边界。
- D 批修正个人内容来源被压缩到 `/me` 的统一导航契约根因；公开详情现携带完整 `MeRoute`，可精确返回原收藏标签与页码。临时数据和运行状态已清理，六库完整性与 strict migration verify 通过。
- [F4-Q 论坛标签公开发现、可见性与 SEO 闭环](/features/forum-tag-public-discovery-seo-design)已完成 A-D 批并关闭：公开帖子判定、数据库侧公开计数 / 热门 / 相关聚合、标签首包 head、tags sitemap、正式 Web 相关主题和运行态矩阵形成闭环。
- [F4-Q-D 成组验收](/records/f4-q-d-forum-tag-public-discovery-stage-acceptance-2026-07-29)覆盖匿名、普通登录用户和 Console 管理员，以及 PC / mobile、`zh / en`、`default / guofeng` 代表路径；热门进入、相关切换、禁用 / 删除 / 恢复、`GET / HEAD`、canonical、JSON-LD 和 sitemap 均已通过。
- D 批按共同根因修正 tags sitemap 分片路由、首包 / runtime JSON-LD 单一脚本、英文数量复数、不可用标签 `noindex`、Console 软删除列表和恢复预检契约；临时标签、PostTag、审计与访问计数已清理，六库完整性及 strict migration verify 通过。

## 最近进展（2026-08-08—11，六个 R1 与四个 R2）

1. 按已确认的 PC / Mobile 正式代表设计重构帖子详情：PC 三栏服务社区返回、正文主轴和线程索引，移动端折叠为顶部紧凑入口与正文后行内索引。
2. 接入现有帖子 / 回帖点赞、表情回应、赞赏和回帖的回帖；两级评论、神评 / 沙发、收藏、举报、问答与修订边界保持不变。
3. 将超限详情容器拆分为控制器、正式视图、回答分页、实时评论、定位高亮和公开 Head 领域模块，主容器收敛至 `1500` 行。
4. 新增 R1-P02 能力与响应式静态契约测试，并同步既有 SEO、上传导航保护和双语资源门禁。
5. Client 全量 `491` 项测试、ESLint `0` warning 与 production build 通过；Gateway PC / mobile smoke 随后完成，R1-P02 已关闭。
6. 按 C-1B 顺位完成 R1-A01 专题、正式 Web、HTTP / Service、Console 与 WebOS 历史实现的紧凑能力覆盖门禁，未修改 `.pen` 或运行时代码。
7. 固定普通 Owner 的可编辑共享草稿代表身份，以及 PC 编辑主轴、mobile 单任务流、关键状态区和 `R2-A02` 差异边界。
8. 识别四项不能由视觉层规避的 Author 能力缺口；方案获批后先闭合设计前能力门禁，不提前进入页面视觉实现。
9. 按获批方案闭合 Author Revision、终态与写响应 evidence、Apply 基准版本 CAS，并补关系授权、附件、保留期、正式 Slug 和事务回滚测试。
10. R1-A01 能力门禁已解除；未修改 `.pen` 或页面视觉，未启动服务。
11. Pencil 被占用期间临时完成 R1-W01 紧凑 readiness；确认正式 `/messages` 与 WebOS 没有能力分叉，并冻结代表身份、关键状态、PC → mobile 输入和 R2 / R3 边界。
12. 按后续授权闭合 R1-W01 的举报 ACL / LongId、失败重试幂等和 History / MessageWindow 404，并补 Client 缓存 fail-closed；能力门禁已解除，撤回权限保持冻结。
13. 完成并确认 R1-A01 PC / Mobile 正式代表设计，将标题与 Markdown 正文固定为编辑主轴，并把目录、状态、属性与协作者收口到统一 context rail / Bottom Sheet。
14. 正式 Author 页面按代表设计落地；运行态复核修正 Mobile 标题与动作区争抢宽度，`390px` 任务头由 `261px` 收敛为 `170px`。
15. Gateway 以普通 Owner、Published 正式 v1、`Editing` 草稿 v2 和 Accepted Editor 完成 PC / mobile smoke；临时 Main / Message / Log / OpenIddict 数据均已精确清零，服务端口已停止。
16. 在唯一活动 `.pen` 中完成并确认 R1-W01 PC `1440`、Mobile `390` 与必要关键状态代表设计；按 family-ui 消息参考收紧连续分栏，消除大卡片缝隙，不恢复 WebOS 窗口或 Dock。
17. 正式 `/messages` 落地会话列表、消息主轴和按需在线成员栏；搜索与成员互斥，Pin 与关系确认在 Mobile 使用共享 Bottom Sheet，并统一 `720px` 紧凑断点和账户切换 reset。
18. 成组实现完成时 Client 全量 `509` 项测试、ESLint、Client / UI / HTTP type-check 与 production build 通过，为后续运行态验收建立静态基线。
19. 获得当前任务授权后完成 Gateway PC `1440 × 900` 与 Mobile `390 × 844 @ DPR 3` smoke；连续分栏、详情 / 列表切换、搜索 / 成员互斥、Pin、已读边界、焦点恢复、受保护图片和无横向溢出均通过，干净移动会话控制台 `0 error / 0 warning`。
20. 运行态修正 Mobile 会话头按钮可访问名称，以及 Chat 附件绑定消息后 nullable 谓词被 SQLSugar 错译导致的合法私密缩略图 `404`；后端全量 `1194` 项通过，临时账号、Direct、消息、Reaction、Pin、通知、审计、OIDC 与上传文件均已精确清零，五库完整性通过且端口停止。
21. 完成 R1-C01 `/console/orders` 的专题、API / Service、权限、URL、LongId、跨资源回跳和 PC / mobile 结构复核；服务端能力门禁通过，未修改 `.pen` 或运行时代码。
22. 形成待确认的“连续表格主轴 + 按需详情 inspector”PC 方向，明确消除任务流卡、常驻摘要和大卡片缝隙，并冻结未落地日期范围、排序、批量、退款、导出与手工状态修改能力。
23. 在唯一活动 `.pen` 中完成并确认 R1-C01 PC `1440 × 900` 正式代表画板；按 family-ui `ui-ref-06` 将订单表收敛为单行薄表格，保留精确状态、受权资源回跳、备注和服务端复核的履约重试边界。
24. 完成并确认 R1-C01 Mobile `390 × 844` 连续订单列表、全屏详情任务和必要关键状态；订单行补齐商品类型 / 单价与支付证据中列，底部导航复用 Client 胶囊视觉并保持 Console 五项真实入口。
25. 按确认设计完成正式 `/console/orders` 代码：PC 薄表格 / 按需 inspector、Mobile 三列订单行 / `BottomSheet` 筛选 / 全屏详情，以及只读、冲突、不可用、stale 和空结果状态均已落地。
26. 同批校正全部 `Failed` 与可重试统计、LongId 字符串输入和通用“重试发放”文案；Console `66 / 66` 测试、Lint、strict type-check、production build 与变更卫生检查通过，未启动服务或浏览器。
27. 获得当前任务授权后使用 Gateway 完成 PC `1440 × 900` 与 Mobile `390 × 844` CSS viewport smoke；列表、inspector、三列移动行、按需筛选、全屏详情、Console 五项真实入口、分页、LongId URL 与资源回跳均通过且无横向溢出。
28. 管理员与临时只读 Operator 覆盖重试确认 / `409`、备注成功 / 冲突、详情 `404`、列表 stale、筛选空结果和写入口隐藏；稳定态浏览器控制台 `0 error / 0 warning`。服务已停止，六库原样还原且完整性通过，临时数据清零。
29. 完成 R1-C02 `/console/moderation` 静态 readiness：服务端能力门禁通过；识别并冻结 PostAnswer、案件 URL / mobile task、stale / unavailable 和 View-only 权限表面四组前端门禁，未修改 `.pen`、运行时代码或 API。
30. 按确认方案关闭 R1-C02 窄前端能力门禁：六类目标、URL / mobile task、权威读取状态与权限停止线落地；Console `73 / 73` 测试和静态构建通过，仍未修改 `.pen` 或启动服务。
31. 在唯一活动 `.pen` 中完成并确认 R1-C02 PC `1440 × 900`、Mobile `390 × 844` 与必要关键状态正式代表设计；四个顶层画板无 placeholder、裁切或溢出。
32. 按确认设计完成正式 `/console/moderation` 代码：PC 三段治理桌、Mobile 连续三列队列 / `BottomSheet` 筛选 / 保留品牌栏的全屏任务，以及必要权限与失败状态表面均已落地；Console `75 / 75` 测试和静态构建通过。
33. 获授权完成 Gateway PC `1440 × 900` 与 Mobile `390 × 844` CSS viewport 验收；三段治理桌、三列队列、按需筛选、五项胶囊导航、品牌栏全屏任务、空结果和详情 unavailable 通过且无横向溢出，并修正 Mobile 列表工具栏、未选详情占位与刷新图标尺寸。
34. 按 R2-C03 readiness 成组关闭角色内建保护、命令与唯一性、权限矩阵聚合版本 / 事务 CAS、系统设置配置—审计共同提交、结构化 400 / 409、显式 Medium 确认和 Settings / Profile 真实性门禁；后端 `1206` 项与 Console `78` 项测试通过，尚未修改 Pencil 或启动服务。
35. 获当前任务明确授权后，在唯一活动 `.pen` 中完成 R2-C03 两个 PC 关键区块、三个独立 Mobile `390 × 844` 页面和必要关键状态局部代表设计；六个顶层设计板通过截图与节点边界复核，未启动服务或浏览器，等待设计确认。
36. 用户确认 R2-C03 设计后完成正式 Console 实现；角色 / 权限 Mobile 只读路径、权限 key 含义、内建保护、Low 设置 `BottomSheet`、Medium PC-only、dirty / CAS / `409` 与真实空态均落地，Console `83 / 83` 测试和全部静态门禁通过，未启动服务或浏览器。
37. 获授权后完成 Gateway PC `1440 × 900` 与 Mobile `390 × 844` 运行态验收；修正系统设置初始空值崩溃，角色 / 权限 / 设置真实读取、Low / Medium、unavailable、空结果、五项导航和无横向溢出通过，干净页签无 `warning / error`。未产生权限或配置写入，临时 OIDC 增量已精确清理，服务停止。
38. 选择 `R2-P03` 为下一顺位并完成 readiness：正式 Docs、Shop、Profile、Legal 与 WebOS / API 能力反查通过；识别正式商品举报和公开主页权威加载两项窄前端门禁，尚未修改 Pencil、运行时代码或 API。
39. 按确认方案关闭 R2-P03 窄前端能力门禁：商品详情接入共享 `Product` 举报；公开主页拆分主资料、统计和内容权威状态，停止伪造统计值并保留结构化错误。Client `512 / 512` 测试与静态构建通过，尚未修改 Pencil 或启动服务。
40. 获当前任务明确授权后，在唯一活动 `.pen` 中完成 R2-P03 两个 PC 代表区、三个独立 Mobile `390 × 844` 页面和必要关键状态局部代表设计；复用既有 Public Header 与 Client 胶囊导航，六板通过截图和节点边界复核，未启动服务或浏览器，等待设计确认。
41. 按第一轮反馈把 PC Header 对齐正式 Client、将商品 / 公开主页拆为独立 PC 页面并收紧间距；商品 Mobile 改为顶部大图电商结构，公开主页 Mobile 增加主题背景、`昵称#公开ID` 与等级提示。七板静态复核通过；商品评价和公开等级经反查确认需要新增窄能力门禁，方案等待确认。
42. 用户确认商品评价与公开等级窄范围后，完成 Main 评价实体 / 迁移、Completed 资格、唯一关系、五星聚合、CAS 生命周期、公开等级安全投影和 `ProductReview` 治理 / 申诉；共享 HTTP、Client 与 Console 契约及分层测试全部通过。
43. 在唯一活动 `.pen` 中为 PC / Mobile 商品页补综合评分、五星分布和已购评价预览，并新增零评价、资格、dirty、`409`、unavailable / stale 与治理 / 申诉关键状态板；八板静态复核通过，等待整组设计确认。
44. 按第二轮反馈把三张 PC 主区统一收紧为 `24px` 外边距、`12px` 分栏和更紧凑卡片内距，修复 PC / Mobile 购买与发消息深底深字，并将公开主页身份拆成昵称、`用户名#公开ID` 与个性签名三层；五个受影响画板静态复核零告警。
45. 按第三轮反馈将 PC / Mobile 商品价格的 `¥` 替换为品牌萝卜币图标，并将 PC 公开主页头部重排为左侧身份资料、右侧统计 / 关系动作两区；三个受影响画板完成截图、节点边界与法币符号扫描复核。
46. 第四轮反馈否决第三轮的 `sprout` 和生硬左右分区；最终改用 `carrot` 萝卜轮廓，PC / Mobile 身份区分别拉开昵称、账号、签名和等级基线，PC 恢复统计与关系动作的横向分布。四个受影响画板静态复核无裁切或溢出。
47. 第五轮反馈保留 Mobile，只重做 PC 公开主页头部：封面 / 信息底由 `120 / 176px` 调整为 `136 / 140px`，主身份、签名 / 等级、统计 / 操作拆成三个横向区块，整卡收敛到 `276px`；PC 截图和节点边界复核通过。
48. 第六轮反馈继续简化 PC 用户头部：删除签名资料条与三套对齐线，改为左侧完整身份列、右侧统一右边界的统计 / 操作列；昵称与账号合并到标题行，Mobile 保持不变，PC 截图和节点边界复核通过。
49. 第七轮反馈按指定信息归属利用 PC 中间留白：左侧保留昵称 / 账号与签名，中间展示当前等级、等级名徽章式视觉及加入时间，右侧统计 / 操作完全不变；徽章式视觉继续只消费现有公开等级名，不新增徽章能力。
50. 按确认稿完成正式商品详情：综合评分、五星分布、稳定分页、Completed 资格、本人评价 CRUD、dirty、CAS `409`、unavailable / stale 与评价举报已接入；Mobile DOM 顺序固定为大图—信息—购买—评价—详情，LongId 保持字符串。
51. 完成正式公开主页身份区：昵称 / `用户名#公开ID`、真实签名空态、当前等级 / 等级名、加入时间和独立权威统计分别加载；未取得统计结果时不伪造零值，未新增背景上传、徽章实体或公开经验详情。
52. Client 全量 `518 / 518`、type-check、Lint 与 production build 通过；本批没有修改 Pencil。
53. 获当前任务授权后执行 DbMigrate `apply`，补齐本地商品评价迁移与种子数据；Gateway、API、Auth 与 Client 健康检查通过。
54. 使用匿名与种子管理员完成 Gateway PC `1440 × 900`、Mobile `390 × 844` 成组验收：零评价、Completed 资格、评价创建 / 编辑 / 删除、dirty 拦截、真实 CAS 冲突、`ProductReview` 举报弹窗、公开主页等级 / 统计、双语 / 主题和无横向溢出均通过。
55. 运行态修正评价登录回跳丢失意图，以及资格 / 冲突错误泄露服务端原始文案；受控 `intent=review` 可在登录后滚动并打开符合资格的评价编辑器。临时订单与评价精确清零，新页签稳定态 `0 warning / 0 error`，`R2-P03` 关闭。
56. 完成 R2-W02 readiness：六个 Private / Workbench 入口的正式 Web、WebOS、API / Store 与响应式事实反查通过；专题保持 R2，但 Pencil 前需先关闭五组窄前端门禁，方案等待确认。本批未修改运行时代码、Pencil、API、数据库或权限，未启动服务 / 浏览器。
57. 按确认方案关闭 R2-W02 五组前端门禁：权威状态、dirty 离开保护、Pet 同键重试、Private Shop 结构化错误和 Me 容器拆分全部落地；Client `525 / 525`、type-check、Lint 与 production build 通过，Pencil 继续不占用。
58. 完成并确认 R2-W02 PC / Mobile / 必要关键状态局部代表设计，固定主任务优先和移动辅助信息折叠契约。
59. 六个正式入口接入共享任务轨折叠、紧凑摘要和主任务顺序；Client `526 / 526`、type-check、Lint、production build 与静态契约通过。
60. 获授权完成 Gateway PC `1440 × 980` 与 Mobile `390 × 844` 成组验收；无横向溢出、折叠交互、关键几何顺序与 `0 error` 通过，服务已停止，R2-W02 关闭。
61. 完成 R2-A02 readiness：三个正式 Author 入口、WebOS 历史能力、Wiki / Forum 契约与响应式事实反查通过；专题保持 R2，但 Pencil 前需先关闭五组能力门禁，方案等待确认。本批未修改运行时代码、Pencil、API、数据库或权限，未启动服务 / 浏览器。
62. 按确认方案关闭 R2-A02 五组能力门禁：Author 权威列表、Revision 独立状态与竞态、共享 Forum Composer、账号草稿隔离 / Workbench 共源和双语 / 单一反馈均落地；后端、HTTP、Client、类型、Lint 与生产构建通过，下一步进入局部代表设计。
63. 在唯一活动 `.pen` 中完成并按反馈修订 R2-A02 四个 PC、四个 Mobile 与必要关键状态局部代表设计；九板 placeholder、逐板截图和原生根边界审计通过，并已获得人工确认。
64. 按确认设计完成 R2-A02 正式前端实现：Docs / Forum 共享相邻 / 当前双快照差异，Mine 主轴与 Mobile 顺序收敛，正式 Composer 以同一实例承载 PC / Mobile 半屏和全屏；Client 全量 `536 / 536`、类型、Lint、构建与变更卫生通过。
65. 获授权后使用种子管理员完成 Gateway PC `1440 × 900` 与 Mobile `390 × 844` 成组验收；修正选中文档行 Mobile 断点和 Composer 图标按钮可访问名称，复验无横向溢出，干净标签页 `0 warning / 0 error`，R2-A02 关闭。

## 今天事项（2026-08-11）

1. R2-A02 已提交并关闭；R3 正式路由、继承缺口与六个实施批次的代码事实审计已经完成。
2. 首批 `R3-P04 Public Forum 浏览族` 的代码与静态回归已完成：browse 样式所有权已隔离，四类浏览组件已统一 PC 连续主轴和 Mobile 主任务优先。
3. 当前只等待服务启动授权；授权后按 Gateway PC `1440 × 900`、Mobile `390 × 844` 覆盖 Forum 七类浏览入口、双语 / 主题、URL 历史、无横向溢出与干净标签页。
4. 继续保持 URL、来源返回、SEO、局部权威状态和业务字段；不新增 API、权限、Pencil 画板或万能 Public 抽象。
5. 三处既有 `DateTime.Now` baseline 与全仓卫生债务继续独立维护；不推进 R3-P05、WebOS 新功能、Tauri 或 Flutter 机械追平。

## 当前执行入口

- [R3-P04 Public Forum 浏览族正式实现与静态回归](/records/f4-r-r3-p04-public-forum-browse-implementation-2026-08-11)
- [R3 路由继承实施分批审计](/records/f4-r-r3-route-inheritance-batch-audit-2026-08-11)
- [R2-A02 Author 列表、修订与 Forum Compose 正式实现及 Gateway 验收](/records/f4-r-r2-a02-author-list-revisions-forum-compose-implementation-2026-08-11)
- [2026-08-10 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-10)
- [开发路线图](/development-plan)
- [R2-A02 Author 列表、修订与 Forum 发布差异局部代表设计](/records/f4-r-r2-a02-author-list-revisions-forum-compose-representative-design-2026-08-10)
- [R2-A02 Author 列表、修订与 Forum 发布差异能力门禁实现](/records/f4-r-r2-a02-author-list-revisions-forum-compose-capability-gate-implementation-2026-08-10)
- [R2-A02 Author 列表、修订与 Forum 发布差异设计前代码事实与能力覆盖审计](/records/f4-r-r2-a02-author-list-revisions-forum-compose-readiness-audit-2026-08-10)
- [R2-W02 Private 仪表 / 任务侧栏正式实现与 Gateway 验收](/records/f4-r-r2-w02-private-dashboard-task-rail-implementation-2026-08-10)
- [R2-W02 Private 仪表 / 任务侧栏前端能力门禁实现](/records/f4-r-r2-w02-private-dashboard-task-rail-capability-gate-implementation-2026-08-10)
- [R2-W02 Private 仪表 / 任务侧栏设计前代码事实与能力覆盖审计](/records/f4-r-r2-w02-private-dashboard-task-rail-readiness-audit-2026-08-10)
- [R2-P03 Public 只读详情变体正式实现与 Gateway 验收](/records/f4-r-r2-p03-public-read-only-detail-variants-implementation-2026-08-10)
- [R2-P03 商品评价与公开等级能力门禁实现](/records/f4-r-r2-p03-product-review-public-level-capability-gate-implementation-2026-08-09)
- [R2-P03 商品评价与公开等级范围审计](/records/f4-r-r2-p03-product-review-public-level-scope-audit-2026-08-09)
- [R2-P03 Public 只读详情变体局部代表设计](/records/f4-r-r2-p03-public-read-only-detail-variants-representative-design-2026-08-09)
- [R2-P03 Public 只读详情变体能力门禁实现](/records/f4-r-r2-p03-public-read-only-detail-variants-capability-gate-implementation-2026-08-09)
- [R2-P03 Public 只读详情变体设计前代码事实与能力覆盖审计](/records/f4-r-r2-p03-public-read-only-detail-variants-readiness-audit-2026-08-09)
- [R2-C03 Console 设置与权限矩阵正式实现](/records/f4-r-r2-c03-console-settings-permissions-implementation-2026-08-09)
- [R2-C03 Console 设置与权限矩阵局部代表设计](/records/f4-r-r2-c03-console-settings-permissions-representative-design-2026-08-09)
- [R2-C03 Console 设置与权限矩阵能力门禁实现](/records/f4-r-r2-c03-console-settings-permissions-capability-gate-implementation-2026-08-09)
- [R2-C03 Console 设置与权限矩阵设计前代码事实与能力覆盖审计](/records/f4-r-r2-c03-console-settings-permissions-readiness-audit-2026-08-09)
- [R1-C02 Console 案件治理 / 审计成组实现](/records/f4-r-r1-c02-console-moderation-implementation-2026-08-09)
- [R1-C02 Console 案件治理 / 审计正式代表设计](/records/f4-r-r1-c02-console-moderation-representative-design-2026-08-09)
- [R1-C02 Console 案件治理 / 审计前端能力门禁实现](/records/f4-r-r1-c02-console-moderation-capability-gate-implementation-2026-08-09)
- [R1-C02 Console 案件治理 / 审计设计前代码事实与能力覆盖门禁](/records/f4-r-r1-c02-console-moderation-readiness-audit-2026-08-09)
- [2026-08-08 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-08)
- [R1-C01 Console 订单表格—明细成组实现](/records/f4-r-r1-c01-console-orders-implementation-2026-08-08)
- [R1-C01 Console 订单表格—明细设计前代码事实与能力覆盖门禁](/records/f4-r-r1-c01-console-orders-readiness-audit-2026-08-08)
- [R1-W01 Private 消息工作区成组实现](/records/f4-r-r1-w01-messages-web-implementation-2026-08-08)
- [R1-A01 Author 编辑代表页成组实现](/records/f4-r-r1-a01-author-editor-implementation-2026-08-08)
- [R1-W01 消息工作区能力门禁修复](/records/f4-r-r1-w01-messages-capability-gate-implementation-2026-08-08)
- [R1-W01 消息工作区设计前代码事实与能力覆盖门禁](/records/f4-r-r1-w01-messages-readiness-audit-2026-08-08)
- [R1-A01 Author 能力覆盖门禁修复](/records/f4-r-r1-a01-author-capability-gate-implementation-2026-08-08)
- [R1-A01 Author 设计前代码事实与能力覆盖门禁](/records/f4-r-r1-a01-author-readiness-audit-2026-08-08)
- [Docs / Wiki 普通作者贡献与协作设计](/features/wiki-author-contribution-collaboration-design)
- [私域与作者态 Web 工作流](/frontend/private-web-workflows-design)
- [文档作者协作与审核使用说明](/guide/docs-author-collaboration)
- [正式 Web 能力覆盖复核](/records/f4-r-formal-web-capability-coverage-audit-2026-08-06)
- [R1-P02 Public 详情与互动代码事实与设计边界审计](/records/f4-r-r1-p02-public-detail-interaction-audit-2026-08-05)
- [R1-P02 帖子详情成组实现](/records/f4-r-r1-p02-public-detail-implementation-2026-08-08)
- [公开 forum 应用结构](/features/forum-public-app)
- [R1-P01 公开发现成组实现](/records/f4-r-r1-p01-public-discover-implementation-2026-08-05)
- [R1-P01 mobile 与统一公开读模型设计](/records/f4-r-r1-p01-mobile-public-read-model-design-2026-08-05)
- [社区发现 Public App](/features/discover-public-app)
- [R1-P01 公开社区发现结构研究](/records/f4-r-r1-p01-public-discover-structure-study-2026-08-04)
- [2026-08-04 日终设计收口与文档审阅](/records/f4-day-end-doc-review-2026-08-04)
- [2026-08-03 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-03)
- [2026-07-30 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-30)
- [F4-R 家族 UI 统一接入与产品视觉重构](/features/family-ui-convergence-design)
- [F4-R C-1 代表页代码事实审计](/frontend/f4-r-representative-page-audit)
- [Radish UI 差异附录](/frontend/ui-addendum)
- [F4-S 公开排行榜参与资格、隐私边界与可信度治理](/features/leaderboard)
- [F4-S-D 公开排行榜治理成组验收](/records/f4-s-d-leaderboard-public-governance-stage-acceptance-2026-07-30)
- [F4-S 公开排行榜治理代码侧验收](/records/f4-s-leaderboard-public-governance-code-acceptance-2026-07-30)
- [发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)
- [F4-N 论坛内容赞赏](/features/forum-content-reward)
- [F4-N-D 论坛内容赞赏成组验收](/records/f4-n-d-forum-content-reward-stage-acceptance-2026-07-27)
- [F4-O 论坛问答回答生命周期与治理闭环](/features/forum-answer-lifecycle-governance-design)
- [F4-O-D 论坛回答生命周期成组验收](/records/f4-o-d-forum-answer-lifecycle-stage-acceptance-2026-07-28)
- [F4-P 论坛帖子收藏与个人内容回访](/features/forum-post-bookmark-personal-library-design)
- [F4-P-D 论坛帖子收藏成组验收](/records/f4-p-d-forum-post-bookmark-stage-acceptance-2026-07-29)
- [F4-Q 论坛标签公开发现、可见性与 SEO 闭环](/features/forum-tag-public-discovery-seo-design)
- [F4-Q-D 论坛标签公开发现成组验收](/records/f4-q-d-forum-tag-public-discovery-stage-acceptance-2026-07-29)
- [2026-07-29 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-29)
- [2026-07-28 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-28)
- [2026-07-27 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-27)
- [F4-M-D 论坛内容版本成组验收](/records/f4-m-d-forum-content-revision-stage-acceptance-2026-07-26)
- [F4-L-D Wiki 附件成组验收](/records/f4-l-d-wiki-attachment-stage-acceptance-2026-07-26)
- [2026-07-26 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-26)
- [验证基线说明](/guide/validation-baseline)
- [镜像漏洞门禁分层](/guide/image-vulnerability-gate)

## 并行维护线

- 接收明确的 `P0/P1` 生产故障、用户反馈、安全、依赖、迁移和部署问题；P2/P3 按同类问题成组处理。
- 公开 head、动态 sitemap、生产域名、镜像漏洞门禁和多实例附件基础设施按真实触达范围维护，不与 F4-R 候选审计并行扩张。
- WebOS 只处理阻断级兼容；Flutter 按 Web 优先顺位承接明确高价值移动原生路径，不机械追平 Web。
- `Radish.Repository/SystemConfigStorageCoordinator.cs` 仍有 `3` 处既有 `DateTime.Now` 与时间语义 baseline 预算不一致；该独立治理项不混入 R2-A02，但在 `PR -> master` 前必须关闭或重新形成经过审计的基线结论。
- 主动生产使用数据采集继续冻结到计划内功能完成、没有明确维护任务且用户确认的最终收尾阶段。

## 当前不做

- 不因 F4-N 关闭而扩入 `PostAnswer`、自定义理由、自定义金额、重复赞赏或独立赞赏中心。
- 不把 F4-O 扩成回答投票、复杂排序、悬赏、萝卜币、独立问答 App 或全量 PublicId 迁移。
- F4-Q 已关闭，不回拉标签关注、个性化推荐、标签首页、SSR / SSG 或公开个人页 sitemap；R2-P03 已冻结的商品评价 / 公开等级边界不扩入媒体、回复、评价有用、独立评价中心或公开经验详情，正式实现只接入已确认代表设计，也不提前实施 R3 派生页面视觉改造。
- 不恢复 Tauri，不扩展 WebOS 新功能，不把 Flutter 做成 Web 的机械复制，也不重启主动生产证据采集。
- 不继续修改历史 `.pen` 留档，不为路由、主题、文案或等价状态复制完整画板；任何后续 `.pen` 修改仍需当前任务的明确授权。
- 不为日常单个文档或小提交频繁创建 `dev -> master` PR；完整功能批次形成后再统一集成。

## 验证与文档维护

- 开发中按风险执行定向测试、type-check、build、changed-only lint、repo hygiene 与 `git diff --check`。
- 准备合并到 `master` 时执行批次级 baseline、identity、依赖审计和范围复核。
- 真实 smoke 只在专题或成组功能准备验收时执行；启动服务前必须取得当前任务授权。
- 本页不保存历史流水；完成事实进入 records、changelog 或 archive。
