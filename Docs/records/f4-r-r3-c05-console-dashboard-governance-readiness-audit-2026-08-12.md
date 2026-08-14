# F4-R R3-C05 Console 仪表与治理派生设计前审计

> 日期：2026-08-12（Asia/Shanghai）
>
> 状态：代码事实、继承成立性、能力缺口与风险拆批已完成；下一顺位为 `R3-C05-A Dashboard 权威调度面`，方案等待确认
>
> 范围：Console Dashboard、Documents、Experience、Channel Discoverability；不修改 Pencil、运行时代码、数据库或权限键

## 1. 结论

`R3-C05` 不需要新建或修改 Pencil 代表画板。四页的既有设计继承均成立：

- Dashboard 继承 `R1-F01 + R2-W02` 的主任务优先、紧凑摘要与辅助信息规则；
- Documents、Experience 与 Channel Discoverability 继承 `R1-C01 + R1-C02` 的队列、证据、动作与事件规则；
- Mobile 继续采用“主任务 → 证据 → 动作 → 留痕”的单任务顺序，不把 PC 表格和三栏机械压缩到手机宽度。

审计同时确认四页不能共用一个万能治理状态机：Dashboard 是只读调度面；Channel 是单字段公开资格治理；Documents 同时承载内容版本、审核和生命周期治理；Experience 是带资产效果的用户台账治理。正式实施按风险由低到高拆为四批：

1. `R3-C05-A Dashboard 权威调度面`
2. `R3-C05-B Channel Discoverability 公开资格治理`
3. `R3-C05-C Documents 文档状态与审核治理`
4. `R3-C05-D Experience 经验台账治理`

## 2. 页面事实与继承裁决

| 页面 | 当前主任务 | 权威读取 | 当前写入 / 事件 | 继承 | 风险 |
| --- | --- | --- | --- | --- | --- |
| Dashboard | 按当前权限进入高频 Console 任务，并回看全局指标与最近订单 | `GetDashboardStats` 的租户统计、订单权威分页 | 无写入；只有路由跳转 | `R1-F01 + R2-W02` | 中低 |
| Channel Discoverability | 筛选频道并显式开启 / 关闭匿名摘要资格，回看变更历史 | 服务端分页、资格问题、独立 `DiscoverVisibilityVersion` | 期望版本写入、必填理由、append-only event | `R1-C01 + R1-C02` | 中 |
| Documents | 处理待审草稿、发布与访问策略、回收站、版本回滚和导入导出 | 文档分页、详情、Revision、待审 Draft / 正文双快照 | 审核 Apply 已有双版本 CAS 与 Review Event；其他治理动作缺少单调状态版本和完整事件 | `R1-C01 + R1-C02` | 高 |
| Experience | 定位单一用户，复核统计 / 流水后执行调账、冻结、复核或等级配置重算 | 用户经验、每日统计、分页流水、最近治理动作、等级配置 | 实体内部乐观锁、经验流水与部分治理动作；调用方版本、调账幂等与等级配置原子性不足 | `R1-C01 + R1-C02` | 最高 |

四页既有路由守卫和按钮级权限方向正确，不新增权限键：

- Dashboard：`console.dashboard.view`，最近订单和跳转入口继续按对应模块权限裁剪；
- Channel：`console.channel-discoverability.view / manage`；
- Documents：`view / review / publish / archive / delete / restore / permissions / rollback / import / export` 保持独立；
- Experience：`view / adjust / freeze / recalculate` 保持独立，人工复核继续归 `freeze` 治理权限。

## 3. 共同能力门禁

以下是四页共享的交付要求，但只复用无业务状态的读状态与响应式表面，不建立跨领域 reducer、万能事件或万能动作组件：

1. 每个独立来源使用自己的请求代际；旧响应不能覆盖新目标、新筛选或新分页。
2. `loading / ready / unavailable / stale` 必须可辨识；首次失败不能伪装为零值或空列表，刷新失败保留旧快照并冻结依赖该快照的写入。
3. 有筛选、分页或目标 ID 的工作面使用 URL 作为可回访的已应用查询；输入草稿不在每次键入时改写权威查询。
4. 统计卡只显示可准确命名的范围：服务端总数写“总数”，当前页派生数写“当前页”，最近 `N` 条写“最近 N 条”，不得把局部数组长度伪装成全局指标。
5. PC 保留连续表格、证据和动作上下文；Mobile 使用卡片、分段详情或 Bottom Sheet，并保持页面级无横向溢出。
6. 所有高风险写入同时具备 handler 权限复核、权威目标、busy / dirty 停止线、结构化本地化反馈和冲突后的草稿保留。

## 4. `R3-C05-A Dashboard` 门禁

### 4.1 当前缺口

- 统计初值为四个 `0`；首次读取失败后页面仍展示真实零值外观，无法区分“没有数据”和“没有取得数据”。
- 最近订单失败保留初始空数组，也没有独立 unavailable / stale 状态；统计和订单都没有请求代际。
- “优先处理队列”的六张卡只有静态领域文案，没有待处理数量或状态，当前实质是高频路由，不能继续命名为权威队列。
- 页头入口、六张调度卡、命令组和全部功能面板多次重复同一路由；“新建商品”只进入 `/products`，不会打开创建任务，属于错误命令语义。
- Mobile 只把 PC 区块改为单列，最近订单仍是 `760px` 横向表格，没有面向窄屏的订单摘要任务。
- `statisticsApi` 仍使用普通 `Error`，没有统一结构化本地化错误。

### 4.2 正式边界

1. 统计与最近订单拆为独立权威资源状态，支持统一刷新，但任何一方失败都不覆盖另一方。
2. 静态卡改称“高频任务路径”或等价准确名称；不伪造待处理数，不为本批建立跨模块聚合队列 API。
3. 删除页头、命令组中的重复 / 无效入口；保留一组高频任务路径和一组完整权限路由，真正的动作必须有可执行深链才可称为命令。
4. PC 最近订单继续使用紧凑表格；Mobile 改为订单号、用户 / 商品、金额、状态和查看动作组成的连续摘要卡。
5. 本批不接入既有但未使用的趋势 / 排行接口，不把 Dashboard 扩成运营 BI 或推荐面板。

### 4.3 首批验证

- Console 定向契约覆盖统计 / 订单独立 ready、首次 unavailable、刷新 stale、过期响应与权限裁剪；
- 覆盖高频入口和全部功能不丢路由，且不存在伪“新建商品”命令；
- 覆盖 PC 表格与 Mobile 订单卡的同快照字段和详情深链；
- 执行 Console 全量测试、type-check、Lint、production build、权限门禁、`git diff --check` 与 changed hygiene。

## 5. `R3-C05-B Channel Discoverability` 门禁

### 5.1 已成立能力

- 服务端列表具备租户范围、真实分页和公开资格问题；
- 写入携带 `expectedVersion + reason`，仓储执行原子条件更新；
- 重复目标状态保持幂等，成功变更写入 append-only event；
- Repository、Service、migration 与授权边界已有定向测试。

### 5.2 当前缺口与正式边界

1. 关键词 / 可见性 / 生命周期 / 删除筛选和分页进入 URL；筛选草稿与已应用查询分离。
2. 列表和历史分别增加请求代际与 `unavailable / stale`；CAS 冲突保留理由草稿，刷新目标后再确认，不把服务端原始消息直接显示给用户。
3. `summary / eligible / inactive` 当前由本页数组计算，必须明确标注为“当前页”；`total` 继续使用服务端总数。
4. “历史”当前固定读取最近 `20` 条却展示为完整历史；改为服务端分页事件时间线，不能只改文案掩盖历史截断。
5. 成功写入先消费响应中的权威频道快照，再刷新列表；刷新失败应显示 stale，不能出现“成功提示 + 旧状态无说明”。
6. 理由 Modal 具备 dirty / busy 关闭停止线；PC 保留表格和历史时间线，Mobile 使用频道卡、单一显隐确认和事件卡。
7. 不修改匿名 Public Discover 输出规则、频道消息授权、`DiscoverVisibility` 枚举、数据库字段或权限键。

## 6. `R3-C05-C Documents` 门禁

### 6.1 已成立能力

- 待审队列、正式正文 / Draft 双快照、审核时间线以及 `RequestChanges / Reject / Apply` 已落地；
- Review Apply 携带 `ExpectedDraftVersion + ExpectedDocumentVersion` 并在事务内执行；
- 文档治理与 Author 创作、公开阅读保持独立，Apply 与 Publish 不合并；
- LongId 已按字符串进入 Console API 契约。

### 6.2 当前根因

- 列表、详情、Revision 与 Review 证据缺少完整请求代际和独立 read state；快速切换时旧结果可覆盖新目标。
- 待审队列固定请求 `pageIndex=1&pageSize=100` 且 UI 禁用分页，超过 `100` 条的任务会静默消失。
- 页面用第一页 `documents.length` 派生多张指标；治理 rail 自动选择受限 / 未发布 / 第一篇文档，操作者未明确选择对象时也会出现写按钮。
- Publish、Unpublish、Archive、访问策略、删除 / 恢复只按 ID 写入；Rollback 只按 Revision ID 写入。当前 `WikiDocument.Version` 是内容版本，状态 / ACL 写入既没有调用方期望版本，也没有独立单调治理版本。
- Review Event 只覆盖审核；发布、下架、归档、ACL、删除 / 恢复和回滚没有统一 append-only 治理时间线，`ModifyBy` 只能表示最后一次修改。
- 访问策略可在保存中关闭，理由 / 草稿和统一写入 busy 边界不足；`1425` 行主页面已接近前端硬上限。
- Mobile 仍主要依赖 `900 / 1500px` 表格横向滚动和宽 Modal，没有把待审队列、正文证据、动作与留痕重排为单任务顺序。

### 6.3 正式治理方案

1. 为 `WikiDocument` 增加独立单调 `GovernanceVersion`，与内容 `Version` 分工；发布等需要确认正文的动作同时携带 `ExpectedDocumentVersion + ExpectedGovernanceVersion`，纯治理动作至少携带 `ExpectedGovernanceVersion`。
2. 新增 append-only `WikiDocumentGovernanceEvent`（或语义等价的专属实体），记录动作、前后状态 / 访问摘要、内容版本、治理版本、操作者、理由和时间；条件更新与事件写入同一事务。
3. Review Event 与 Governance Event 保持不同实体和语义，不把审核决定、正文 Revision 和生命周期事件压成一个万能日志。
4. 列表与待审队列各自使用 URL 分页；列表、详情、Revision 列表 / 详情、Review 队列 / 证据和治理事件分别管理请求代际及 stale。
5. 治理动作只绑定操作者显式选中的文档；页面局部派生指标准确标注“当前页”，不得自动把第一篇文档当作权威目标。
6. PC 维持“队列 / 文档列表—证据—动作与事件”；Mobile 固定“选择任务—正文 / 草稿或当前文档—动作—时间线”，宽表只作为 PC 承载。
7. 按真实所有权拆出列表 / 读取编排、Review 工作台、文档治理动作与历史 owner，使主页面回到编排职责；不为控制行数拆只转发参数的空组件。

该批包含实体、migration、Repository / Service / HTTP 契约和 Console 改造，必须在进入代码前单独确认；不得借此修改 Docs Author 创作边界、公开 ACL、附件令牌或审核状态机。

## 7. `R3-C05-D Experience` 门禁

### 7.1 当前根因

- 查询用户与两个写入表单分别维护可编辑 `userId`；按钮状态来自已加载用户，但实际写入可指向另一用户，证据目标和写入目标能够分离。
- `UserExperience` 实体已有 `Version` 和服务端条件更新，但 `UserExperienceVo` 不返回版本，调账 / 冻结 / 解冻请求也不携带操作者看到的版本；内部重试不等于调用方 CAS。
- 加减经验是非幂等资产效果；网络结果不明时再次提交会重新读取新版本并再次加减，当前没有独立幂等键。人工复核也是 append-only 写入，同样可能重复留痕。
- 用户、每日统计、流水与治理动作没有独立 read state / 请求代际；失败会清空数组，旧数据、无数据和未取得数据不可辨识。
- 流水有服务端分页，但目标与筛选不在 URL；治理动作固定最近 `20` 条却按完整留痕展示。
- 等级配置重算是全局写入，当前前端单击即执行；服务端逐条更新且没有事务，任一中途失败可留下部分新、部分旧的等级配置。
- Mobile 仍保留 `960 / 1120 / 1280px` 多张表格，调账与冻结表单在长页面末端，证据和动作目标难以持续核对。

### 7.2 正式治理方案

1. `UserExperienceVo` 暴露 `VoVersion`；所有用户写入只绑定当前已加载权威用户，不再提供第二套可编辑目标 ID。
2. 调账原因改为必填，并携带 `ExpectedVersion + IdempotencyKey`；建立独立经验管理员调账幂等域，资产更新、`ExpTransaction` 与幂等结果同事务提交。
3. 冻结 / 解冻携带 `ExpectedVersion` 和明确原因；版本冲突保留表单草稿并要求刷新。人工复核携带证据所对应的用户版本，并为 append-only 提交提供幂等保护。
4. 用户目标、统计窗口、流水筛选与分页进入 URL；用户、每日统计、流水、治理事件和等级配置使用独立请求代际与 read state。
5. 治理动作改为服务端分页时间线；“最近动作”指标按真实分页总数或明确的最近 N 条口径显示。
6. 等级配置重算先展示当前公式 / 影响摘要并显式确认；服务端整批事务更新、失败整批回滚，并留下可回看的配置重算审计记录。
7. PC 保留台账工作台；Mobile 固定“用户目标—异常证据—流水—复核 / 冻结 / 调账—留痕”，表格改为同快照卡片或按需展开，等级配置作为低频从属区。

本批不扩入自动处罚、反作弊模型、经验发放规则、排行榜、公开经验详情、人工修改用户等级或新的 Console 权限。

## 8. 测试事实与分层验证

当前已有的测试资产不等于四页能力已经闭环：

- Dashboard 后端统计聚合有 Service 测试，Console 只覆盖订单深链合同，缺少资源状态与 Mobile 契约；
- Channel 的 Repository / Service / migration / authorization 覆盖较完整，Console 主要只有 i18n 资源测试；
- Documents 有展示 helper、i18n、Review 双版本服务端覆盖，但普通治理动作没有 CAS / 事件测试；
- Experience 有核心发放、调账、冻结和复核 Service 测试，尚未覆盖调用方版本、调账幂等、目标绑定和等级重算事务失败。

开发中按每批风险执行：

| 批次 | 后端 | Console | 其他 |
| --- | --- | --- | --- |
| C05-A | Statistics 定向回归 | 状态 / 路由 / PC-Mobile 合同、全量测试、类型、Lint、build | 权限、diff、changed hygiene |
| C05-B | Channel Repository / Service / authorization 与历史分页 | URL、竞态、CAS 草稿、Mobile 卡 / 事件 | 不跑 Public Discover 运行态；保持其静态回归 |
| C05-C | Wiki 状态 CAS、事件事务、Review 回归、SQLite / PostgreSQL migration | 列表 / Review / Revision / 事件状态与 PC-Mobile 契约 | 文档和 migration 门禁 |
| C05-D | Experience CAS、幂等、流水、治理事件、等级重算事务 | 权威目标、URL、独立状态、Mobile 工作流 | OperationIdempotency 与权限门禁 |

四批代码和静态门禁成组关闭后，再在取得当轮启动授权的前提下执行 Gateway PC / Mobile、双语、代表主题和角色权限运行态复核；本次审计不启动服务或浏览器。

## 9. 停止线与下一步

- 不修改 Pencil；如果正式实现证明上述继承无法表达新的主任务或 Mobile 顺序，立即停止并先申请 Pencil 占用权。
- 不建立 Dashboard 跨模块推荐 / BI 系统，不新增万能治理事件、万能状态机或跨领域页面抽象。
- 不修改 Public Discover、Docs Author、经验发放规则、排行榜、WebOS、Tauri 或 Flutter。
- 不在 C05-A / B 提前实施 Documents / Experience 的 migration、幂等或治理事件。
- 不把运行态 smoke 当作每个小提交的默认步骤；四批准备成组验收时再申请启动授权。

下一步是 `R3-C05-A Dashboard 权威调度面`。先关闭只读权威状态、伪队列 / 伪命令和 Mobile 最近订单，再进入带写入的 Channel 批。
