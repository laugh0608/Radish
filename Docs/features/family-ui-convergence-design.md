# F4-R 家族 UI 统一接入与产品视觉重构

> 日期：2026-07-30；2026-08-03 更新（Asia/Shanghai）
>
> 状态：A / B、v26.7.3 破坏性基线补充批、C-0 与 C-1A 代码事实审计已完成；C-1B 共享基座与代表画板待推进
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

本补充批只建立可追溯基线和兼容层，不提前改变页面视觉；品牌 CTA、常规主操作、激活态与相应前景色由 C-1B 代表设计确认后按页面族治理。

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
| Public 社区 | `22`、`23`，辅以 `08` | 发帖器层级、内容流扫描、标签 / 反应 / toast 双通道表达、可选辅助侧栏 | 保留内容优先顶部导航，不照搬应用左侧栏或深色主按钮 |
| Private / Author / 消息 | `04`、`10`、`16`、`21`、`26`、`27` | 列表—详情关系、三栏工作区、行式表单、任务分步、会话与附件层级 | PC 可多栏，mobile 必须收敛为单任务流；纸纹只可弱化在边缘 |
| Console 治理工作台 | `01`—`03`、`06`、`07`、`09`、`11`—`18`、`24`、`25` | KPI—主表—明细节奏、筛选 / 搜索 / 行操作、审计时间线、进度、向导、日历、告警与设置 | 按页面类型选模式，不把表格、设置、详情都改成 Dashboard |
| 暗色代表面 | `20`，辅以 `05`、`25` 的主题入口 | 墨底下的表面、边框、前景和状态层次 | 不等于纯黑底或荧光监控大屏；仍使用 family-ui 暗色语义 |
| Web mobile 与 Flutter | 不直接取自 27 张桌面图 | 共享任务层级、状态语义和视觉气质 | 以 family-ui `07-layout-platforms.md` 和 Radish 多端契约独立重排，不缩放桌面稿 |

### 9.2 设计源

现有设计源继续按职责维护，但不再承担路由、功能、文案和状态的完整镜像：

- `web-ui-foundation.pen`：共享 token、壳层、按钮、状态槽和基础组件。
- `public-web-unified-experience.pen`：Public 页面族。
- `private-web-workflows.pen`：Private / Author 页面族。
- `console-governance-workbench.pen`：Console 页面类型。

既有大型 `.pen` 保留为历史设计资产，不在本专题一次性重写。[C-1A 代码事实审计](/frontend/f4-r-representative-page-audit)已按自动升级条件和六维评分裁决一个基础矩阵与六类完整页面类型：

1. 共享组件、状态、主题和壳层矩阵；
2. Public 内容流；
3. Public 详情与互动；
4. Author 编辑 / 发布；
5. Private 消息或列表—详情工作台；
6. Console 表格 / 明细；
7. Console 案件治理 / 审计。

命中 R1 的类型完成 PC / mobile 完整代表画板；R2 只画关键区块、状态或响应式差异；R3 页面写明继承来源后直接实现并用真实页面截图复核。Token 等价接入不要求先重画页面，但不得宣称视觉重构完成。

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
- `2026-08-03` 完成 v26.7.3 破坏性基线补充批：同步上游副本、新增 `text-on-brand`、显式保留 Radish 四主题品牌前景与 `guofeng` 胭脂身份。

退出条件：不改变业务行为，Client、Console、`@radish/ui` 构建与定向测试通过。

### F4-R-C：Pencil 与共享组件视觉收敛

- C-0 已完成：完整审计参考索引与 27 张参考图，建立页面族吸收 / 排除映射；v26.7.3 仅迁址且图片 blob 不变，映射继续有效，设计时仍须按新版“观察、拆解、提炼、转译”边界复核相关参考。
- C-1A 已完成当前代码与页面类型审计，确认 `7` 个 R1、`4` 个 R2 及 R3 继承表。
- C-1B 按共享基座、Public、Author、Private、Console 顺序复核和更新代表设计。
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
2. 四主题、Console 和共享 UI 使用一致的 L1 语义，并区分 `text-on-brand` 与 `text-on-accent`。
3. 新共享样式不再新增无归属硬编码颜色。
4. Public、Private / Author、Console 的代表类型完成必要 Pencil 与代码收敛，派生页继承关系和真实截图复核可追溯。
5. Flutter 建立可维护的语义映射，不要求 Web 像素复制。
6. WebOS 不回归，Tauri 没有被重新引入开发或门禁。
7. PC / mobile、双语、四主题、可访问性和 reduced-motion 验收通过。
