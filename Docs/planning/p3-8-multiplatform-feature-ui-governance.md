# P3-8 多端功能补全与 UI 设计治理

> 状态：`P3-8-C1` Console 治理工作台结构基座已完成，下一顺位为 `P3-8-C2`
>
> 启动日期：2026-05-21（Asia/Shanghai）
>
> 本页承载单人开发期的多端功能补全、客户端适配和 UI 设计治理口径。快速入口仍以 [当前进行中](/planning/current) 为准。

## 背景判断

`P3-6` 与 `P3-7` 已完成公开增长观察、WebOS / PC 工作台复访小闭环和高信号候选筛查。它们证明当前没有新的 `P0/P1` 阻断项，但不等于项目已经进入稳定运营期。

当前项目仍处于开发阶段，没有稳定用户反馈和专职测试。公开页面、移动端视图公开页、Flutter 移动客户端、WebOS / PC / Tauri 客户端、Console 等仍存在大量未适配、未开发完或体验割裂的能力。后续不能继续把“等待真实使用观察”作为默认主线，而应恢复主动盘点、主动开发、主动验收的节奏。

同时，各个页面 UI、各客户端 UI、`radish.client` 和 `radish.console` 的页面风格仍有明显历史分叉：局部 CSS、硬编码颜色、组件复用不足、交互反馈不一致、端点视觉层级不统一。需要单独设立 UI 设计治理专题，先形成设计稿和设计源文件，再按设计稿推进实现。

`P3-7-C` 已完成工作台与后端 Service 首批热区治理，继续硬拆低风险候选的收益下降。`P3-8-A` 已完成多端功能缺口与 UI 设计入口审计，首批实现任务 `P3-8-B1 Flutter 公开榜单只读入口` 已完成。`P3-8-B2` 已建立 Console 治理工作台设计端点，`P3-8-C1` 已完成内容治理与经验治理首批结构基座。后续继续按设计稿和页面类型推进，不直接大面积重写 Console。

## 阶段目标

1. **多端功能缺口可见化**
   - 建立覆盖公开页、移动 Web 公开视图、Flutter、WebOS / PC / Tauri、Console 的功能缺口矩阵。
   - 区分未开发、未适配、只读缺口、登录态缺口、跨端回流缺口、壳层能力缺口和治理效率缺口。
   - 按用户主路径、产品价值、实现风险、验证成本和长期路线排序。
2. **UI 设计治理专题化**
   - 为公开页、客户端和 Console 建立统一设计入口。
   - 使用 Pencil 先画设计稿，再根据设计稿开发。
   - 设计源文件进入项目，并与视觉规范、主题 token、共享组件和实现代码同步维护。
3. **开发节奏回到主动推进**
   - 正式稳定运营前，由 AI 协作者主动按矩阵批量验收、成组修复和一次性交付结论。
   - 维护观察只作为并行线，不再阻塞未完成功能和 UI 治理继续推进。

## 首批范围

`P3-8-A 多端功能缺口与 UI 设计入口审计` 已完成，第一批实现任务已从审计矩阵中选出并推进。

首批审计结论见：[P3-8-A 多端功能缺口与 UI 设计入口审计](/planning/p3-8-a-feature-ui-audit)。

产出物：

- 多端功能缺口矩阵：
  - 公开页面
  - 移动端 Web 公开视图
  - Flutter 移动客户端
  - WebOS / PC 工作台
  - Tauri PC 桌面壳
  - Console 管理后台
- UI 端点分组：
  - 哪些端点需要独立设计稿
  - 哪些页面应归入同一个设计稿
  - 哪些已有视觉规范或共享组件可以复用
  - 哪些历史页面需要优先收敛
  - 哪些页面可参考 [UI 设计灵感参考](/frontend/ui-design-inspiration) 中的布局、密度、设置页、仪表盘或社区列表模式
- 设计源文件治理建议：
  - `.pen` 文件目录
  - 文件命名
  - 端点与设计稿映射
  - 设计稿更新和实现同步规则
- 首批开发批次建议：
  - 只选择 `1-2` 个一天级可验证任务
  - 明确完成条件、验证入口和不做范围

已完成：

- `P3-8-B1 Flutter 公开榜单只读入口`
  - 新增 Flutter 原生 `榜单` tab，保留 `发现 / 论坛 / 文档 / 榜单 / 我的` 顺序。
  - 复用现有 `/api/v1/Leaderboard/GetLeaderboard` 公开经验榜接口。
  - 覆盖加载、空态、错误态、刷新态、当前用户标记和发现页跳转返回。
  - 未加入我的排名、商品榜、购买、订单、背包、登录增强或管理员调整。
- `P3-8-B2 Console 治理工作台设计端点`
  - 建立 `Docs/frontend/console-governance-workbench-design.md` 端点说明，明确内容治理、经验治理、工作台信息架构、实现拆分顺序和验证入口。
  - 新增 `Docs/frontend/design-sources/console-governance-workbench.pen` 设计源文件入口，并约定 `.pen` 只能通过 Pencil 工具维护。
  - 后续扩展为 `P01-P08` 编号画板，覆盖壳层基座、内容审核、经验台账、治理调度总览、表格 CRUD、设置策略和移动端治理视图。
- `P3-8-C1 Console 治理工作台结构基座`
  - `ModerationPage` 完成 helper、列定义、手动治理区拆分，并接入治理工作台布局承载。
  - `ExperienceAdminPage` 完成 helper、列定义、用户查询摘要、观察摘要、复核区、经验流水区、治理动作表单、页面头部和等级配置拆分，并接入治理工作台布局承载。
  - 页面拆分保持 API、权限、表单字段、表格列、经验规则、冻结 / 解冻语义和数据契约行为等价。
- `P3-8-C2 Console 设计稿到实现的对齐试点`
  - 首个试点选择低风险 `Settings` 设置页，对齐 `P06` 设置 / 权限 / 配置型页面方向。
  - `Settings` 已迁入分组导航、居中设置列和右侧影响范围摘要，保留个人时区偏好、重置默认和密码修改行为。
  - `adminFeature.css` 新增 `admin-settings-*` 与 `admin-setting-section` 可复用布局类，供后续设置 / 策略类页面继续复用。
  - 第二个试点选择 `UserList` 用户管理页，对齐 `P05` 普通表格 CRUD 页面方向。
  - `UserList` 已迁入顶部指标、筛选工具条、表格主体和右侧摘要栏，保留用户查询、分页、状态 / 角色筛选和详情跳转行为。
  - `AdminLayout`、`Breadcrumb`、`index.css` 与 `adminFeature.css` 已继续沉淀 Case Desk 壳层、表格型布局和 `--console-*` token 基座。
  - 第三个试点选择 `Dashboard` 仪表盘，对齐 `P04` 治理 / 运营调度总览页方向。
  - `Dashboard` 已迁入总览指标、快捷操作、最近订单和右侧调度入口，保留统计数据、最近订单加载、权限判断和跳转行为。
  - `adminFeature.css` 新增 `admin-overview-*` 与 `admin-dispatch-*` 可复用布局类，供后续总览 / 调度类页面继续复用。
  - 后续列表迁移首个落点选择 `TagList` 标签管理页，继续复用 `P05` 表格型布局基座。
  - `TagList` 已迁入顶部指标、筛选工具条、表格主体和右侧摘要栏，保留标签 CRUD、启停、恢复、排序、软删除显示和分页行为。
  - 后续列表迁移第二个落点选择 `CategoryList` 分类管理页，继续复用 `P05` 表格型布局基座。
  - `CategoryList` 已迁入顶部指标、筛选工具条、表格主体和右侧摘要栏，保留分类 CRUD、启停、恢复、排序、层级显示、软删除显示和分页行为。
  - 后续列表迁移第三个落点选择 `SystemConfigList` 系统配置页，继续复用 `P05` 表格型布局基座。
  - `SystemConfigList` 已迁入配置指标、筛选工具条、品牌图标配置面板、表格主体和右侧摘要栏，保留配置 CRUD、站点图标上传 / 恢复默认、分类筛选和关键词筛选行为。
  - 后续列表迁移第四个落点选择 `RoleList` 角色管理页，继续复用 `P05` 表格型布局基座。
  - `RoleList` 已迁入角色指标、列表状态工具条、表格主体和右侧权限摘要栏，保留角色 CRUD、启停、权限配置跳转和分页行为。

下一顺位：

- 继续 `P3-8-C2` 后续页面类型试点
  - 设计依据见：[Console 治理工作台设计端点](/frontend/console-governance-workbench-design) 与 [Console 样式与 Token 使用说明](/frontend/console-style-guide)。
  - 优先继续复核 `Applications / Stickers` 等历史列表页是否可按 `P05` 小步迁移，或回看 `P02 / P03` 治理工作台实际使用后的布局细节。
  - 不一次性改完整个 Console，不把所有页面硬套成同一模板。

## UI 设计稿治理

Pencil 设计稿是 UI 专题的设计源文件。后续涉及页面级或客户端级 UI 优化时，原则上先更新对应 `.pen` 设计稿，再进入实现。

启动 `P3-8-A` 的 UI 审计和 Pencil 设计稿拆分前，应先阅读 [UI 设计灵感参考](/frontend/ui-design-inspiration)，从 AFFINE、CodexApp、Cloudflare、GitHub、Discourse 与 1Panel 的截图中提炼布局、信息密度、控件气质和配色节奏，再结合 Radish 自身内容重新设计，不直接照搬外部产品。

候选端点包括但不限于：

- 公开页面设计稿
- 移动端 Web 公开视图设计稿
- WebOS 工作台设计稿
- Flutter 移动客户端设计稿
- Tauri / PC 桌面壳设计稿
- Console 管理后台设计稿

上述拆分只是候选，不在本页提前固定。`P3-8-A` 需要结合实际页面、客户端边界、复用关系和维护成本决定最终端点粒度。例如公开页与移动端 Web 公开视图是否拆开、Tauri 壳与 WebOS 是否共用一个设计稿、Console 是否按后台模块拆分，都需要审计后再定。

治理规则：

- `.pen` 文件必须进入项目仓库或项目约定的设计目录，不能只存在个人本地。
- `.pen` 文件名应表达端点和职责，避免 `new-design.pen`、`ui-v2.pen` 这类无意义名称。
- 每个 `.pen` 文件应能追溯到对应实现范围、视觉 token、共享组件和主要页面。
- 设计稿变更影响实现时，应同步更新对应文档或开发批次说明。
- `.pen` 文件为 Pencil 工具管理的设计源文件；后续创建、读取和修改 `.pen` 时应通过 Pencil 工具完成，不直接用普通文本工具读取或改写。

## 开发边界

本阶段做：

- 主动盘点并补齐未完成的多端功能。
- 优先处理公开访问、移动浏览、移动客户端、桌面工作台、Tauri 壳层和 Console 主路径缺口。
- 建立 UI 设计稿与实现同步机制。
- 在新增或改动页面中逐步收敛到 `@radish/ui`、主题 token 和统一交互反馈。

本阶段不做：

- 不把等待真实用户反馈当作继续开发的前置条件。
- 不一次性重构所有 UI。
- 不把所有端点一次性改成同一套布局。
- 不绕过设计稿直接大面积改视觉。
- 不让 Pencil 前置阻塞纯功能缺口、后端治理、行为等价拆分、小范围表单 / 状态 / 文案修正。
- 不把 Tauri 当作原生 UI 重写路线。
- 不启动完整 Playwright / E2E 平台作为继续开发的前置。
- 不启动完整可观测性平台或大而全运营平台。

## 排序原则

优先级从高到低：

1. 阻断用户主路径或跨端回流的缺口。
2. 明显影响公开访问、移动阅读、客户端使用和治理效率的未完成功能。
3. 会导致各端 UI 持续分叉的设计和组件问题。
4. 能用一天级批次验证并提交的高收益小闭环。
5. 低收益微体验、分发材料、平台化工程和一次性历史数据补齐。

## 验证口径

文档与审计批次默认验证：

```bash
npm run check:repo-hygiene:changed
git diff --check
```

进入实现批次后，再按触达范围选择：

- 前端页面或 UI 改动：`npm run type-check --workspace=radish.client`、`npm run type-check --workspace=radish.console`、必要的 node tests 或浏览器联调
- 共享 UI 改动：`npm run type-check --workspace=@radish/ui`，并覆盖 client / console 受影响入口
- Flutter 改动：运行对应 Flutter 单测或最小 smoke
- 后端 / Console 数据契约改动：`dotnet test Radish.Api.Tests` 或定向 filter
- 跨端链路改动：按 [验证基线说明](/guide/validation-baseline) 选择 quick / baseline / host 入口

`P3-8-B1` 已执行：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

`P3-8-B2` 文档 / 设计端点批次默认执行：

```bash
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-C1` 已执行：

```bash
npm run type-check --workspace=radish.console
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-C2` 设置页试点已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-C2` 表格 CRUD 试点已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
git diff --check
```

`P3-8-C2` 调度总览页试点已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
git diff --check
```

`P3-8-C2` 标签列表迁移已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
git diff --check
```

`P3-8-C2` 分类列表迁移已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
git diff --check
```

`P3-8-C2` 系统配置列表迁移已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
git diff --check
```

`P3-8-C2` 角色列表迁移已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
git diff --check
```

## 与维护线关系

`P3-6` 公开增长观察和 `P3-7` WebOS / PC 工作台复访链路继续作为维护线存在。若公开访问、head / sitemap、分享预览、购买 / 订单 / 背包、运行日志或权限授权暴露高信号问题，可以从维护线回拉小闭环。

维护线不能再反向阻塞 P3-8 的主动开发。只有当维护线暴露 `P0/P1` 阻断项时，才临时抢占当前批次。
