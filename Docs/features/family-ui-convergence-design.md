# F4-R 家族 UI 统一接入与产品视觉重构

> 日期：2026-07-30；2026-08-08 更新（Asia/Shanghai）
>
> 状态：A / B、v26.7.3 破坏性基线补充批、C-0 与 C-1A 已完成；C-1B 的六个 R1 与 `R2-C03` 已关闭，当前进入 `R2-P03` 窄前端能力门禁
>
> 上游规范：RadishX `docs/design/family-ui/` `v26.7.3`
>
> 项目差异：[Radish UI 差异附录](/frontend/ui-addendum)

## 摘要

F4-R 将 Radish 现有 `--theme-*`、`--rx-*`、`--console-*`、共享 Ant Design 配置和页面族视觉基座收敛到 family-ui `--rd-*` 语义体系。

本专题不是一次性换皮，也不重做信息架构。实施顺序固定为：

1. 先冻结项目差异、Profile、四主题与多端边界；
2. 再做等价 Token 接入和共享组件语义收敛；
3. 完整阅读参考索引、逐张查看参考图，并形成 Radish 页面族映射；
4. 通过 Pencil 确认共享基座和主要页面，由相似页面继承后直接实现；
5. 最后成组实现和验收 Public、Private / Author、Console 与 Flutter 代表路径。

Web 是正式优先主线，Flutter 是次级移动原生产品线；WebOS `/desktop` 只兼容，Tauri 暂时弃用并等待未来重新评估。

## 1. 背景与问题

Radish 已有成熟的功能、主题和页面族基础，但视觉契约存在四类分叉：

- `radish.client` 主要消费 `--theme-*`，四主题由 `data-theme` 驱动；
- 正式 Web 共享壳层又建立了 `--rx-*`；
- Console 使用独立 `--console-*`，部分状态仍来自 Ant Design 默认值；
- `@radish/ui` 默认主题仍保留早期橙色与通用红黄绿蓝，宿主覆盖不完整。

family-ui 与 Radish 现有纸币色板高度同源，适合成为通用语义层。`v26.7.3` 起上游只提供通用语义、参考 token 和视觉参考，不再维护具体项目配色、接入状态或迁移计划；Radish 的主题身份、页面事实和升级节奏必须由当前仓库代码与 `Docs/` 自主维护。

## 2. 产品决策

### 2.1 多端顺位

1. Web 覆盖 PC / mobile 浏览器，是功能、视觉和验收的第一顺位。
2. Flutter 作为次级移动原生产品线继续发展；新能力先确认 Web 产品边界，再判断原生价值和 Flutter 承接范围。
3. WebOS `/desktop` 只保留历史兼容和旧深链，不承接本轮视觉重构。
4. Tauri 暂时弃用：
   - 保留仓库中的历史验证资产；
   - 不继续功能、UI、签名、更新、分发和专属兼容开发；
   - 不进入日常 CI、发布或 family-ui 验收矩阵；
   - 未来只有明确桌面原生价值、目标用户和维护预算同时成立时，才重新评估，而不是自动解冻。

### 2.2 产品形态

- Public 继续是内容优先社区，不建立营销式首页。
- Private / Author 继续使用正式 Web 工作流，不回退到 WebOS 窗口心智。
- Console 继续是治理工作台，不把所有页面统一成卡片 Dashboard。
- Flutter 共享任务归属、命名、状态语义和视觉气质，但采用移动原生布局与组件。

## 3. 目标

1. 建立家族 L1 `--rd-*` 与 Radish 产品主题之间的稳定映射。
2. 保留四主题权益契约，同时消除 CSS 与 Ant Design 状态色分叉。
3. 让 Public、Private / Author、Console 和 Flutter 使用清晰的 Profile。
4. 优先治理共享组件和壳层，减少页面级重复 CSS。
5. 让视觉重构可分批验证、可审阅、可回退，不阻断功能维护线。
6. 将现有 Radish UI 文档逐步收缩为产品差异和实现契约，避免与 family-ui 重复维护。

## 4. 不做

- 不改变 API、权限、数据模型、路由和信息架构。
- 不改变主题商品 ID、权益激活、停用、过期、撤销和回退。
- 不一次性重命名所有 CSS 变量或全仓替换颜色。
- 不把 Public 页面改成宽幅营销站。
- 不把 Console、Private 或 Flutter 做成 WebOS。
- 不借 UI 重构解冻 WebOS 或 Tauri。
- 不要求 Flutter 与 Web 像素级一致，也不默认追平 Web 全部能力。
- 不在 Token 等价迁移批次顺手重画页面。

## 5. 规范优先级

```text
RadishX family-ui：家族通用视觉语义与组件基线
                 ↓
Radish UI 差异附录：产品 Profile、主题、多端与已知偏离
                 ↓
Radish 页面族 / 功能专题：路由、流程、状态、权限与布局
                 ↓
代码与 .pen：具体实现和设计源
```

运行时和产品契约冲突时以 Radish `Docs/` 为准；通用视觉语义冲突时应先判断是否属于项目差异，不能在组件中静默分叉。

## 6. Token 架构

### 6.1 分层

| 层级 | 内容 | 典型前缀 |
| --- | --- | --- |
| L0 家族色板 | 纸、墨、胭脂、玉、赭、雅紫 | `--rd-palette-*` |
| L1 家族语义 | 背景、文本、边框、品牌、操作、状态、字体、圆角、间距、动效 | `--rd-*` |
| L2 Radish 过渡别名 | 现有产品主题、Console 与共享 UI 兼容 | `--theme-*`、`--console-*` |
| L3 壳层 / 领域 | 内容宽度、Header、移动底栏、WebOS 窗口和页面专属布局 | `--rx-*` 及明确领域 token |

### 6.2 迁移方向

第一批先把 family-ui token 复制到 Radish，并建立可验证的兼容关系。迁移期间组件可以继续使用 L2；新共享组件优先使用 L1。

长期目标是：

```css
--theme-bg-app: var(--rd-bg-app);
--console-success: var(--rd-state-success);
--rx-bg-surface: var(--rd-bg-surface);
```

四主题通过根节点覆盖 `--rd-*` 取值，不复制页面 CSS。`--rx-*` 中的内容宽度、移动安全区、Header 高度等真实壳层变量继续保留。

### 6.3 状态与操作语义

- Brand 负责产品识别和少量焦点，不再同时承担危险操作。
- Action 使用墨蓝系，适合 Workbench 主操作、链接和 focus。
- Success、Warning、Danger、Info、Neutral 使用 family-ui 状态语义。
- 所有状态必须至少通过图标 / 文字 / 颜色中的两个通道表达。
- Ant Design 的 `colorSuccess / colorWarning / colorError / colorInfo` 必须由同一主题定义提供，不再依赖共享默认的荧光状态色。

### 6.4 v26.7.3 破坏性基线复核

`2026-08-03` 已完成上游 `v26.7.3` 破坏性更新复核：

- family-ui CSS / JSON 副本保持与上游逐字一致，通用参考默认品牌由胭脂改为灰玉，并新增亮暗两套 `text-on-brand`；
- 灰玉默认值不构成 Radish 产品决策，Client 项目主题层显式保留 `guofeng` 胭脂品牌，并为四主题固定品牌实底前景；
- 27 张参考图仅从原素材目录迁入 family-ui `reference-ui/`，Git blob 全部 `R100` 相同，既有页面族吸收 / 排除映射继续有效；
- 新版进一步明确品牌色、操作色和状态色分工。代码事实显示现有 Client 仍有 `92` 个样式文件消费 `--theme-brand-primary`，尚无页面消费 `--theme-action-primary`；该事实进入 `R1-F01` 语义裁决，不在基线补充批机械全仓替换。

本补充批只建立可追溯基线和兼容层，没有机械改变页面视觉。后续 `R1-F01` 已确认 Brand CTA、Workbench Action、激活态及各自实底前景的共享语义；具体页面仍由对应 R1 / R2 组合与代码复核治理。

`R1-P01` 随后形成新的 Radish 项目配色裁决：`guofeng` 品牌目标由旧胭脂调整为低饱和灰玉 `#5d6c57`，悬停为 `#6e736d`，品牌柔底为 `#e2e6de`，常规操作仍使用墨蓝 `#435c74`。这是基于 `/discover` 代表页与长期视觉方向作出的项目决策，不是对上游默认值的隐式继承。`R1-P01` 页面实现已通过现有 `accent-jade / accent-ink / action` 语义落地局部构图；Client 全局品牌 token 会影响全部页面族，不随单页实现机械替换，进入共享主题批时再成组复核。

## 7. 主题运行时

`radish.client` 继续拥有主题注册表、持久化和权益同步，`@radish/ui` 只接收宿主配置。

根节点属性职责：

| 属性 | 职责 |
| --- | --- |
| `data-theme` | Radish 产品主题 ID，保持现有四主题契约 |
| `data-rd-theme` | family-ui 明暗语义，由有效主题派生 |
| `data-rd-profile` | 当前壳层视觉 Profile，不表示用户主题 |
| `color-scheme` | 浏览器原生控件明暗模式 |

Console 不跟随用户商城主题，默认使用 Workbench 亮色；未来如果增加 Console 暗色，也必须独立立项并复用同一 L1 暗色语义。

## 8. Profile 与布局

### 8.1 Public

- 内容流、搜索、详情与评论首先保证扫描和阅读。
- Brand 体现在浅纸色、克制标题、局部 Mascot、空态和边缘纹样。
- 不新增与真实内容争夺首屏的 Hero、能力 band 或宣传按钮组。
- PC / mobile 都必须是真实重排，不做桌面页面缩放。

### 8.2 Private / Author

- 使用 Workbench 密度，主体任务优先。
- 继续复用 `WebShellHeader`、`WebStateSlot` 和来源返回契约。
- 创作页避免多层卡片；工具、正文、状态和提交动作分区明确。

### 8.3 Console

- 使用 Workbench Profile。
- 保留表格、设置、详情、治理工作台各自结构。
- 优先复用 `ConsolePage` 语义组件和现有表格布局。
- 先统一状态、控件、浮层和壳层，不逐页复制新皮肤。

### 8.4 Flutter

- 共享配色语义、状态语义、字体层级和图标方向。
- 保留原生导航、触控目标、安全区、Android Back 和系统生命周期。
- Web 页面族完成并稳定后，再为 Flutter 建立 Dart 语义映射；不直接解析 CSS。

## 9. 参考素材与设计源

### 9.1 参考素材门禁

`2026-07-30` 已完整阅读 RadishX `docs/design/family-ui/references.md`，并逐张查看其索引的 27 张外部 UI 参考图。后续页面级视觉工作必须延续这一门禁：

1. 先读参考索引中的“主要学习点 / 明确不学”和 family-ui 对应章节；
2. 再声明当前页面吸收的布局、密度、组件或状态原则，以及拒绝复制的表面特征；
3. 按[代表页协作流程](/frontend/pencil-representative-page-workflow)裁决 R1 / R2 / R3，只有 R1 / R2 更新对应 `.pen`；
4. 只接入 token、只读规范文字或只看部分截图，均不能视为完成视觉迁移。

参考图仅用于内部观察和原则提炼，不是设计或产品素材。不得将截图嵌入 `.pen`、产品、`public/` 或对外交付物，不复制其品牌、图标、文案、配色和页面；Radish 只吸收结构、密度、组件形态与状态表达。

页面族映射如下：

| Radish 范围 | 主要参考 | 吸收重点 | 明确边界 |
| --- | --- | --- | --- |
| 共享基础与壳层 | `01`、`02`、`05`、`08`、`10`、`11`、`12`、`16`、`19`、`25` | 发丝边框、紧凑控件、状态 chip、工具条、设置分区、详情字段行、时间线、stepper、菜单与通知层级 | 不把所有内容卡片化，不引入高饱和蓝紫操作面 |
| Public 社区 | `13`、`16`、`18`、`27`，辅以 `22`、`23` | 非对称主次、异构模块尺度、连续工作面、紧凑工具区，以及人员 / 任务 / 状态 / 内容的并置节奏；内容卡片继续吸收标签、反应和反馈表达 | 不照搬后台左侧栏、外部业务字段或品牌；帖子不能继续占据绝大多数可见面积，也不使用全宽分类浏览大块 |
| Private / Author / 消息 | `04`、`10`、`16`、`21`、`26`、`27` | 列表—详情关系、三栏工作区、行式表单、任务分步、会话与附件层级 | PC 可多栏，mobile 必须收敛为单任务流；纸纹只可弱化在边缘 |
| Console 治理工作台 | `01`—`03`、`06`、`07`、`09`、`11`—`18`、`24`、`25` | KPI—主表—明细节奏、筛选 / 搜索 / 行操作、审计时间线、进度、向导、日历、告警与设置 | 按页面类型选模式，不把表格、设置、详情都改成 Dashboard |
| 暗色代表面 | `20`，辅以 `05`、`25` 的主题入口 | 墨底下的表面、边框、前景和状态层次 | 不等于纯黑底或荧光监控大屏；仍使用 family-ui 暗色语义 |
| Web mobile 与 Flutter | 不直接取自 27 张桌面图 | 共享任务层级、状态语义和视觉气质 | 以 family-ui `07-layout-platforms.md` 和 Radish 多端契约独立重排，不缩放桌面稿 |

`2026-08-03` 的 `R1-P01` 评审证明原 Public 映射过度依赖 `22 / 23` 的帖子流语法，导致首页即使增加辅助卡片，视觉重心仍几乎全部落在帖子上。`2026-08-04` 已按 `13 / 16 / 18 / 27` 完成参考转译、公开能力审计、低保真比较和多轮视觉重校准。最终 `R1-P01 / 社区发现 / PC 1440` 不复刻参考产品的信息架构，只吸收现代无衬线排版、克制圆角阴影、小面积状态色和自然的数据反馈，并围绕 Radish 顶部公共导航、结构化问题、混合社区信息与社区洞察重组；灰玉、墨蓝与国风暖白构成家族色，“社区正在发生”并置公共频道、成员公开动态、神评、帖子和问答。`2026-08-05` 已完成并确认唯一 `Mobile 390`：搜索与内容前置，脉搏、贡献者和知识主题分别嵌入首屏、中段与流后；首轮反馈后，混合流由异色列表拼接重构为墨蓝焦点事件、连续编号轨道和嵌入式贡献者人物节点，统一空间、分隔与动作语法。统一公开读模型同时固定频道摘要 opt-in、公开 Wiki 首次发布、当前神评资格、稳定时间序游标和首批 `no-store`；代码侧现已完成 migration、Console 治理、跨源 API、`@radish/http` 和 `/discover` 连续流实现。两张画板节点检查、用户评审及 Gateway PC / mobile 运行态复核均通过，运行态发现的 Int64 wire contract 偏差已在统一 HTTP 契约层修正；详见[公开社区发现应用结构](/features/discover-public-app)。`22 / 23` 继续只负责帖子内部表达。

`2026-08-05` 的 [R1-P02 代码事实与设计边界审计](/records/f4-r-r1-p02-public-detail-interaction-audit-2026-08-05)确认了公开详情当时的接入事实和结构债；后续评审证明当前页面未传入共享组件回调不能自动成为长期产品边界。`2026-08-06` 的[正式 Web 能力覆盖复核](/records/f4-r-formal-web-capability-coverage-audit-2026-08-06)确认四项推进决定，随后已完成 PC / mobile 正式代表设计、成组实现和 Gateway 运行态验收，详见[实现记录](/records/f4-r-r1-p02-public-detail-implementation-2026-08-08)。

`2026-08-08` 的 [R1-A01 设计前代码事实与能力覆盖门禁](/records/f4-r-r1-a01-author-readiness-audit-2026-08-08)确认正式 Web 已承接主体作者流程，代表身份固定为普通 Owner 的可编辑共享草稿；审计发现的普通 Author Revision 读取、终态审核证据、写响应证据和 Apply 基准版本 CAS 已按[能力门禁修复记录](/records/f4-r-r1-a01-author-capability-gate-implementation-2026-08-08)闭合。随后完成 PC / mobile 正式代表设计、标题 / Markdown 正文主轴、统一 context rail / Bottom Sheet、页面实现与 Gateway 运行态验收，详见[成组实现记录](/records/f4-r-r1-a01-author-editor-implementation-2026-08-08)。

同日 `R1-W01` 完成 readiness、ChatMessage 举报 ACL / LongId / 重试幂等 / 历史错误能力门禁、PC / mobile 正式代表设计、连续消息工作区实现与 Gateway 运行态验收，详见[成组实现记录](/records/f4-r-r1-w01-messages-web-implementation-2026-08-08)。随后 `R1-C01 / R1-C02` 与 `R2-C03` 依次完成 readiness、必要能力门禁、代表设计、正式实现和 Gateway PC / mobile 验收。`R2-P03 Public 只读详情变体` 已完成正式商品举报 / 公开主页权威加载、商品评价 / 公开等级能力门禁和不复制四个完整路由的局部代表设计并获确认；下一步进入正式商品详情与公开主页实现。

### 9.2 设计源

当前唯一活动设计源为 `Docs/frontend/design-sources/radish-web-family-ui-v1.pen`。文件保留已确认的 `R1-P01 / R1-P02 / R1-A01 / R1-W01 / R1-C01 / R1-C02`、`R2-C03` 与 `R2-P03` 必要代表画板、组件母版、关键状态和主题变量；失败研究只由 Git 历史留存。后续 R1 / R2 继续在该文件中新增或替换必要代表设计。`v1` 在普通设计迭代中持续演进并由 Git 留存，只有不兼容的结构性升级才新建 `v2`。

`web-ui-foundation.pen`、`public-web-unified-experience.pen`、`private-web-workflows.pen` 与 `console-governance-workbench.pen` 保留为只读历史资产，不删除、不改名、不继续同步。[C-1A 代码事实审计](/frontend/f4-r-representative-page-audit)已按自动升级条件和六维评分裁决一个基础矩阵与六类完整页面类型：

1. 共享组件、状态、主题和壳层矩阵；
2. Public 内容流；
3. Public 详情与互动；
4. Author 编辑 / 发布；
5. Private 消息或列表—详情工作台；
6. Console 表格 / 明细；
7. Console 案件治理 / 审计。

命中 R1 的业务类型必须在同一活动设计源中提供可按真实尺寸独立审核的 PC / mobile 顶层业务画板；关键状态与辅助说明可以拆成独立画板，复合说明板中的嵌入缩略图不能替代业务页面交付。R2 只画关键区块、状态或响应式差异；R3 页面写明继承来源后直接实现并用真实页面截图复核。Token 等价接入不要求先重画页面，但不得宣称视觉重构完成。

## 10. 批次

### F4-R-A：设计与治理基线

- 更新多端路线：Web 优先、Flutter 次级、Tauri 暂时弃用。
- 建立本专题与 UI 差异附录。
- 固定 Profile、四主题、Token 分层和停止线。
- 盘点上游接入表与当前代码差异。

退出条件：入口文档一致，后续代码不再依赖口头裁决。

### F4-R-B：Token 与共享主题基座

- 复制 family-ui token。
- 接入 Client 四主题和 L2。
- 统一 `@radish/ui` / Ant Design 状态语义。
- 接入 Console Workbench L2。
- 补主题定义与映射测试。
- `2026-08-03` 完成 v26.7.3 破坏性基线补充批：同步上游副本、新增 `text-on-brand`，并在当时显式保留 Radish 四主题品牌前景与 `guofeng` 胭脂身份；后续灰玉改色属于 C-1B 设计裁决，不改写本批历史事实。

退出条件：不改变业务行为，Client、Console、`@radish/ui` 构建与定向测试通过。

### F4-R-C：Pencil 与共享组件视觉收敛

- C-0 已完成：完整审计参考索引与 27 张参考图，建立页面族吸收 / 排除映射；v26.7.3 仅迁址且图片 blob 不变，映射继续有效，设计时仍须按新版“观察、拆解、提炼、转译”边界复核相关参考。
- C-1A 已完成当前代码与页面类型审计，确认 `7` 个 R1、`4` 个 R2 及 R3 继承表。
- C-1B 已建立单一版本化活动设计源与共享组件 / 主题基座；六个 R1 与 `R2-C03` 的 readiness、必要能力修复、PC / mobile 代表设计、页面实现及运行态复核均已关闭。`R2-P03` 的 readiness、两批能力门禁与局部代表设计已完成并确认，下一步进入正式页面实现；全局共享主题色继续单独成组治理。
- 更新共享基座和命中的 R1 / R2 代表画板，不为 R3 派生页面创建重复画板。
- 收敛按钮、输入、状态 chip、表格、卡片、Modal、空态、Header 和移动底栏。
- 先实现共享组件和 R1 代表页，完成真实截图复核后再进入派生页面。

退出条件：代表类型、评分和继承关系可追溯；必要 PC / mobile 画板确认；公共组件没有平行变体扩张。

### F4-R-D：页面族实现与成组验收

- Public 页面族。
- Private / Author 页面族。
- Console 页面族。
- R3 页面按代表画板继承说明成组实现；若暴露新结构或响应式模型，再升级为 R1 / R2。
- Web 基线稳定后建立 Flutter 语义映射和高价值代表页。
- WebOS 只做兼容回归；Tauri 不进入矩阵。

退出条件：四主题、双语、PC / mobile、reduced-motion、构建与专题运行态矩阵通过。

### F4-R 完成后的视觉演进

F4-R 负责建立并落地 Radish 新的家族 UI 基线。此后视觉重构默认基于当时最新的 family-ui、Radish 差异附录、已确认代表页和正式代码做优化与更新，保留已经成立的信息架构、功能入口和产品识别，不从零重新设计。

只有出现全新的产品形态、现有结构无法承载核心任务，或功能 / 平台边界发生结构性变化，并完成专题裁决后，才允许建立新的页面范式或不兼容设计源主版本。普通规范升级、配色调整、组件替换、密度优化和响应式修正不得触发全页推倒重来。

## 11. 验证

开发中：

- `npm run type-check --workspace=@radish/ui`
- `npm run type-check --workspace=@radish/http`
- `npm run test --workspace=radish.client`
- `npm run build --workspace=radish.client`
- `npm run build --workspace=radish.console`
- 触达 Flutter 时执行 `flutter analyze` 与定向 `flutter test`
- `git diff --check`
- `npm run check:repo-hygiene:changed`

专题验收：

- Gateway 下 PC `1920 × 1080` 与 mobile `390 × 844`
- Public、Private / Author、Console 代表路径
- `default / guofeng / theme-dark-night / theme-sakura`
- `zh / en`
- keyboard focus、状态双通道、reduced-motion 和横向溢出

真实 smoke 必须在服务启动获得当前任务授权后执行。

## 12. 风险与控制

| 风险 | 控制 |
| --- | --- |
| 一次性修改数千处 Token 引用 | 使用 L2 兼容层，按页面族迁移 |
| family-ui 接入破坏四主题权益 | 保留 `data-theme` 和服务端契约，只增加语义映射 |
| 上游参考默认色被误当成 Radish 品牌决策 | 上游副本原样保留，四主题在项目层显式覆盖品牌与品牌前景 |
| Public 被改成营销页 | 内容首屏和现有信息架构列入停止线 |
| Console 与 Client 再次分叉 | 状态语义和 Ant Design 配置从共享层治理 |
| Flutter 机械复制 Web | 共享语义，布局和组件保持原生自治 |
| WebOS / Tauri 消耗主线 | WebOS 只兼容；Tauri 暂时弃用且不进入门禁 |
| 上游规范与当前代码事实错位 | 上游只提供通用规则，迁移表按当前代码重新审计 |
| Pencil 页面镜像过大且快速过期 | 只维护代表视觉契约；功能与文案服从文档和代码，派生页用真实截图复核 |

## 13. 完成定义

F4-R 只有同时满足以下条件才关闭：

1. Radish 已固定并记录遵循的 family-ui 版本。
2. 四主题、Console 和共享 UI 使用一致的 L1 语义，并区分 `text-on-brand` 与操作实底前景语义；设计源使用 `text-on-action`，代码迁移期间映射现有 `--rd-text-on-accent`。
3. 新共享样式不再新增无归属硬编码颜色。
4. Public、Private / Author、Console 的代表类型完成必要 Pencil 与代码收敛，派生页继承关系和真实截图复核可追溯。
5. Flutter 建立可维护的语义映射，不要求 Web 像素复制。
6. WebOS 不回归，Tauri 没有被重新引入开发或门禁。
7. PC / mobile、双语、四主题、可访问性和 reduced-motion 验收通过。
