# F4-R R1-C02 Console 案件治理 / 审计设计前代码事实与能力覆盖门禁

> 日期：2026-08-09（Asia/Shanghai）
>
> 状态：静态审计已完成；现有服务端能力门禁通过，Console 前端能力门禁待确认和闭合；门禁解除前不进入 Pencil 或正式代表设计
>
> 范围：正式 Console `/console/moderation` 的案件与申诉工作区、HTTP / Service / Repository、权限、URL 状态、PC / mobile 结构，以及 `R1-C02` 对治理与审计页面的代表边界；本批未修改 `.pen`、运行时代码、API、数据库或权限

## 1. 结论

- `R1-C02` 继续保持 R1，正式锚点为 `/console/moderation`。它同时承载案件队列、受权证据、决定、目标处置、用户动作、申诉复核、纠正执行和事件留痕，不能降为普通 Console 表格 / 明细的 R3 继承页。
- 现有 Controller、Service、Repository 和 `@radish/http` 已覆盖 Case / Appeal 读取、证据采集、决定、纠正、结构化 `404 / 409`、LongId 字符串、操作键幂等、状态版本与 Main / Chat 事务边界；不需要为代表设计新增 API、数据库表、权限或动作类型。
- 正式 Console 已具备案件与申诉两个同路由工作区，但案件仍自动选择首项、选中状态不进入 URL；队列或详情加载失败时保留旧数据且没有 stale / unavailable 状态，mobile 也无法进入隐藏全局导航的全屏任务。这些会让代表设计建立在不稳定导航和错误状态之上。
- F4-O 已把 `PostAnswer` 扩为第六类治理目标，服务端已完整支持限制、恢复、Revision CAS 和采纳关系清理；Console 的目标类型筛选、双语文案和本地 Revision 前置校验仍停留在原五类，是当前明确的能力覆盖漂移。
- 只有 `console.moderation.view` 的 Operator 虽不能提交决定，当前仍看到可编辑的决定控件，只在提交按钮处禁用；这与权限真实性不一致，必须在正式设计前先关闭写入表面。
- 因此本次 readiness 结论是：**服务端能力足够，R1 身份和结构方向可冻结；先用一个窄前端能力批闭合第六目标、URL、stale / unavailable 和只读表面，再进入 PC 1440 / Mobile 390 正式代表设计。**

## 2. 正式代表身份与权限切片

默认代表身份建议固定为：

```text
登录受权 Console Reviewer
+ console.access
+ console.moderation.view / console.moderation.review
+ 可见包含 PostAnswer 与 ChatMessage 的案件队列
+ 当前显式选中一条 Reviewing 案件
+ 已有举报快照、当前证据、目标 Revision 和内部备注草稿
+ 不具有 console.moderation.action / appeal
```

该身份能同时验证“决定”和“动作”分离：Reviewer 可以登记决定与目标处置，但没有 Action 权限时不能附带用户动作或执行结案后纠正。

必要权限切片：

| 身份 | 读取 | 写入 | 页面必须表达的停止线 |
| --- | --- | --- | --- |
| View-only Operator | 案件队列、详情、证据、事件、用户当前状态；申诉队列只读摘要 | 无 | 不呈现可编辑决定 / 纠正表单，不以“可填写但提交禁用”伪装只读 |
| Reviewer | View + 采集证据、登记决定和目标处置 | 不含用户动作、纠正或申诉复核 | 决定区明确显示“仅决定”，用户动作入口不存在 |
| Action Operator | View + 用户动作和已获准纠正 | 不能绕过 Review 或 Appeal 决定 | 只在服务端允许的状态和来源下显示执行入口 |
| Appeal Reviewer | View + 完整申诉陈述、证据与复核 | 没有 Action 时不能执行纠正 | 复核结论与纠正执行保持两个事实 |

受保护的 System / Admin、本人动作、跨租户目标和未举报私聊继续由服务端失败关闭；页面不能把 Console 身份解释为无限数据访问权。

## 3. 能力覆盖矩阵

| 能力 | 当前代码事实 | 门禁结论 |
| --- | --- | --- |
| 路由归属 | Console 正式路由为 `/moderation`，Gateway 入口为 `/console/moderation`；RouteGuard 使用 `console.moderation.view` | 已承接，不新增平行路由或独立治理 App |
| 案件队列 | 支持 `status / targetType / keyword / pageIndex / pageSize`，返回稳定分页与脱敏摘要 | 已承接；首批 UI 不扩原因、证据可用性或动作结果筛选 |
| 案件详情 | 返回 Case、Reports、Evidence、Events、用户当前状态和动作结果 | 已承接；读取错误需要前端权威失效表达 |
| 决定与目标处置 | `ReviewCase` 使用 Case version、目标 Revision、用户状态 version 和 `operationKey` | 已承接；决定成功不冒充动作成功 |
| 纠正动作 | 结案后可按 Action 权限执行 Mute / Ban / Unmute / Unban 纠正 | 已承接；不能替代 Appeal relief |
| 申诉工作区 | 同路由 `view=appeals&appeal=...` 支持脱敏队列、完整受权详情、复核与 relief | 已承接；mobile 继续只读，不在本批新增移动高风险写入 |
| 第六目标 | HTTP、Service、Repository 已支持 `PostAnswer` 的快照、Restrict / Restore、Revision CAS 与采纳清理 | 服务端已承接；Console 筛选 / i18n / 前置校验待修 |
| LongId | HTTP 契约中的内容、用户与内部 LongId 均保持 `string`，Case / Appeal 使用公开标识 | 已承接；URL 与输入继续禁止 `Number(...)` |
| 冲突与幂等 | Case / Appeal / State 版本冲突与同键异参返回结构化 `409`；当前前端会刷新详情并保留草稿 | 已承接；设计必须保留草稿冲突态和重新提交边界 |
| 事务与跨库 | Main 决定、目标动作、用户状态、事件和 Outbox 在明确事务内提交；Chat 使用可靠任务 | 已承接；不引入实时刷新或前端推测状态 |
| WebOS / Flutter | WebOS 只有受权 Console 入口，没有独立治理实现；Flutter 没有 Console 治理产品线 | 无迁移缺口，不扩多端范围 |

## 4. API、权限与写入边界

现有 Console 契约保持不变：

- View：`GetCaseQueue / GetCase / GetCaseEvents / GetAppealQueue`；
- Review：`CaptureEvidence / ReviewCase`；`ReviewCase` 附带用户动作时还需 Action；
- Action：`ApplyCorrectiveAction / ExecuteAppealRelief`；
- Appeal：`GetAppeal / GetAppealEvents / StartAppealReview / CaptureAppealEvidence / ReviewAppeal`。

写入继续遵守：

- Case、Appeal 与 User State 分别提交各自的 expected version；
- 目标限制提交服务端证据 Revision，不能用前端缓存推测；
- `operationKey` 同键同参回放、同键异参冲突；
- Main 同库写入原子提交，Chat 通过 Outbox 呈现 `ActionPending / ActionFailed / Succeeded`；
- `404` 表示对象已不可用，`409` 表示权威版本或操作键冲突；客户端不得按展示文案判断；
- View、Review、Action、Appeal 权限相互独立，页面动作与 handler 都必须继承服务端权限边界。

本批不得新增自动审核、风险评分、敏感词、批量处置、举报撤回、二次申诉、多级仲裁、附件、管理员对话、实时刷新、完整私聊浏览或全量审计平台。

## 5. Pencil 前必须闭合的窄前端能力门禁

### 5.1 第六目标 `PostAnswer`

当前 Console 案件筛选只列出 `Post / Comment / PostQuickReply / ChatMessage / Product`，`zh / en` 也缺少 `moderation.targetType.PostAnswer`；服务端返回回答案件时，列表和详情会回显资源键且无法按类型筛选。

后续能力批应：

- 在筛选和双语资源中加入 `PostAnswer`；
- 将 PostAnswer 纳入需要当前内容 Revision 的 Restrict 前置检查；
- 补筛选、显示和缺失 Revision 失败关闭测试；
- 保持 HTTP 枚举与 LongId 字符串契约不变。

### 5.2 案件 URL 状态与 mobile 任务

现有 URL helper 只承接 `view / appeal / keyword / returnTo`；案件选择、status、targetType 和分页仍是组件内状态，刷新、复制链接、浏览器前进 / 后退或 mobile 列表—详情切换都不能恢复当前任务。

后续能力批应在现有 `/moderation` query 上建立唯一真相源，至少承接：

- 当前案件公开标识；
- 当前案件筛选和分页；
- 既有 `view=appeals&appeal=... / keyword / returnTo` 兼容；
- 无效、越权或已删除选择的稳定收口；
- mobile 详情打开时由 `AdminLayout` 隐藏全局 header、breadcrumb 和五项底部导航，关闭后恢复原队列 URL。

具体 query key 在代码批确认，但不得新建详情路由、数值化 LongId 或破坏既有申诉深链。

### 5.3 权威失效、stale 与只读表面

当前 Queue / Detail catch 只显示 toast。队列刷新失败会保留旧队列，详情 `404 / 403 / 网络失败` 会保留旧详情及动作表面，用户无法判断数据是否仍权威。

后续能力批应：

- 队列刷新失败时可保留 last-good 内容，但显式标记 stale；
- 详情 `404 / 403` 时清除权威详情或进入 unavailable，不保留可提交动作；
- 其他详情加载失败可保留只读 last-good 快照，但必须标记 stale 并冻结写入；
- `409` 与读取失败分开：`409` 继续刷新权威详情、保留草稿并轮换新 operation key；
- View-only Operator 只看到证据和结果，不渲染可编辑决定 / 纠正控件。

该能力批预期只修改 Console 页面、URL helper、`AdminLayout`、locale 与定向测试；审计未发现需要同步修改后端、迁移、权限或 `@radish/http` 的理由。实施前仍需用户确认。

## 6. 当前结构债

- PC 将队列、证据、举报、决定、纠正和事件依次堆成多张大卡，主任务缺少稳定的队列—对象—动作层级。
- 列表自动选择首项，使“查看队列”和“进入具体案件”没有显式任务边界；对 mobile 尤其不成立。
- 证据和事件都使用长纵向 timeline，决定 / 动作与权威结果分散在滚动长尾，容易把审计阅读和高风险写入混在一起。
- Case 与 Appeal 虽为同路由兄弟工作区，但切换、筛选和所选对象没有统一导航模型。
- Mobile 只是 PC 内容纵向收缩，没有把队列、筛选、证据和动作改造成一次处理一个对象的任务流。

这些属于 R1-C02 的结构问题；不应在旧大卡片上做局部装饰，也不应把治理页硬套为 R1-C01 的普通订单 inspector。

## 7. 正式代表设计输入

### 7.1 PC 1440

建议采用高密度治理桌：

1. 页面头只保留案件 / 申诉视图、权限事实和当前负载，不重复解释流程。
2. 左侧或主轴使用连续薄队列，优先显示公开编号、目标类型、状态、举报数量、目标 / 动作结果和最近变化。
3. 显式选择后打开对象工作区；证据、决定、动作结果和事件按处理顺序分区，不把两条长 timeline 同时全部展开。
4. Review 与 Action 分离，权限不足时删除动作表面；高风险提交保留明确确认、版本与冲突反馈。
5. Case 与 Appeal 共用壳层和对象选择语法，但不把两者合并为同一状态机。

### 7.2 Mobile 390

建议采用单任务转换：

1. 默认先显示连续案件 / 申诉队列和当前视图；Console 五项真实入口继续使用 Client 胶囊导航视觉。
2. status、targetType 和 keyword 进入按需筛选层，不常驻占据首屏。
3. 选择对象后进入隐藏全局导航的全屏任务；任务头提供稳定返回并恢复队列筛选、分页与滚动上下文。
4. 案件详情按“目标摘要 → 证据 → 决定 / 结果 → 动作 → 留痕”渐进展开，一次只保持一个主要写入区。
5. 案件写入仍按实际 Review / Action 权限承接；申诉 mobile 延续既有只读契约，不顺带开放复核或纠正执行。

## 8. 必要关键状态

正式代表设计至少需要覆盖：

- View-only Operator：证据可读、决定和动作表面不存在；
- Reviewer 无 Action：可以决定，不可附带用户动作；
- `409`：权威详情已刷新，未提交草稿保留，明确要求复核后重试；
- Case / Appeal `404` 或越权：详情不可用，旧内容不可操作，可返回队列；
- 队列 stale：last-good 队列可辨认但不可假装已刷新；
- 筛选空结果：保留重置入口，不自动选中隐藏对象；
- Chat 动作 `Pending / Failed / Succeeded`：决定与执行结果分离，失败可按既有幂等边界重试；
- PostAnswer：显示回答目标、Revision 证据和采纳关系被治理清理 / 恢复的真实结果；
- 申诉队列脱敏、Action-only 纠正与 mobile 只读边界。

不为每个目标、权限、locale、主题和结果复制完整画板；PC / mobile 顶层代表页加局部关键状态板即可。

## 9. R2 / R3 继承边界

- 审计日志、Documents、Experience 和 Appeals 等含事件或复核动作的页面，可继承 R1-C02 的“对象队列—受权证据—决定 / 动作—留痕”语法，但继续服从各自 API、权限和专题状态机。
- 普通 Users、Products、Applications、Categories、Tags、Stickers、Coins 等资源页继续继承 R1-C01，不因存在详情或少量动作改为治理工作台。
- `R2-C03` 设置与权限矩阵同时继承 R1-C01 的高密度资源结构和 R1-C02 的权限事实 / 高风险确认，不复制完整治理页。
- WebOS 只保留正式 Console 入口；Flutter 和 Tauri 不进入本批设计、实现或验收。

## 10. 证据入口

- Console：`Frontend/radish.console/src/pages/Moderation/ModerationPage.tsx`、`ModerationAppealsWorkspace.tsx`、`moderationPageUrlState.ts`
- Console 壳层：`Frontend/radish.console/src/components/AdminLayout/AdminLayout.tsx`
- HTTP：`Frontend/radish.http/src/content-moderation-contract.ts`
- Controller：`Radish.Api/Controllers/ContentModerationController.cs`
- Service / Repository：`Radish.Service/ContentModerationService*.cs`、`Radish.Repository/ContentModerationCaseRepository*.cs`
- 权威说明：[内容治理系统说明](/guide/content-moderation)
- 原案件专题：[内容治理案件、证据与动作一致性](/features/content-moderation-case-evidence-action-design)
- 申诉专题：[内容治理申诉与处置纠正](/features/content-moderation-appeal-relief-design)
- 代表页裁决：[F4-R C-1 代表页代码事实审计](/frontend/f4-r-representative-page-audit)

## 11. 下一步与停止线

1. 由用户确认本记录和第 5 节的窄前端能力批。
2. 获批后只闭合 `PostAnswer`、案件 URL / mobile task、stale / unavailable 和只读表面，并执行 Console 定向测试、Lint、type-check、production build、repo hygiene 与 `git diff --check`。
3. 能力门禁关闭后再进入唯一活动 `.pen` 的 R1-C02 PC `1440`、Mobile `390` 与必要关键状态代表设计。
4. 设计确认后才成组实现正式 Console，并在专题准备验收时另行申请启动服务和 Gateway PC / mobile smoke 授权。

在上述确认前，不修改 Pencil、不修改运行时代码、不启动服务或浏览器，也不提前推进 R2-C03。
