# Radish UI 差异附录

> 遵循：RadishX `docs/design/family-ui/` `v26.7.3`（2026-07-31）
>
> 状态：Web 页面族、[T01 主题语义](/records/f4-r-t01-web-theme-semantic-baseline-implementation-2026-08-15)、[T02 reduced-motion](/records/f4-r-t02-reduced-motion-static-exit-gate-implementation-2026-08-15)与 [T03 四主题运行态](/records/f4-r-t03-web-four-theme-grouped-runtime-acceptance-2026-08-15)已关闭；下一顺位为 Flutter 语义映射 readiness 审计。

## 1. 真相源与优先级

Radish 采用“家族通用规范 + 项目差异附录”的分层方式：

1. 色彩母板、`--rd-*` L1 语义、状态色语义、字体与图标基线、通用组件形态和避免方向，遵循 RadishX `family-ui v26.7.3`；上游默认取值是参考实现，不替 Radish 决定品牌配色。
2. Radish 的产品范围、路由、主题权益、数据与接口契约、壳层分工和验证边界，继续以本仓库 `Docs/` 为唯一项目真相源。
3. family-ui token 复制进入本仓库构建，不在运行时依赖 `/Users/.../RadishX` 等跨仓库绝对路径。
4. 上游规范与本附录发生冲突时，先在本附录登记项目差异；需要改变家族通用语义时，再回到 RadishX 提交版本变更。
5. `Docs/frontend/visual-theme-spec.md`、`visual-color-reference.md` 和各页面族设计说明在迁移期间继续有效；重复的通用条款逐步收缩，Radish 特有契约保留。

## 2. 产品路线

当前多端顺位固定为：

| 端 | 当前定位 | UI 投入 |
| --- | --- | --- |
| Web | 正式产品优先主线，覆盖 PC / mobile 浏览器、Public、Private、Author 与 Console | family-ui 首要接入与验收对象 |
| Flutter | 次级移动原生产品线，复用同一语义、任务归属和视觉气质；不要求与 Web 像素级一致 | Web 基线稳定后按高价值移动路径推进 |
| WebOS `/desktop` | 历史兼容入口 | 只保持阻断级兼容，不进入视觉重构范围 |
| Tauri | 暂时弃用，保留历史验证资产，未来等待重新评估 | 不开发、不发布、不进入 UI、CI 或验收门禁 |

“Flutter 次级产品线”不等于机械追平 Web。新能力先在 Web 建立完整产品边界；具备明确移动价值时，Flutter再按原生导航、触控、生命周期和系统能力独立设计。

## 3. Profile 分配

| 产品表面 | family-ui Profile | Radish 差异 |
| --- | --- | --- |
| Public 内容流、论坛、Docs、商城、公开主页 | Brand 气质 + 内容阅读密度 | 不是营销站；真实内容保持首屏主体，纹样与衬线只作弱强调 |
| Public 欢迎、空态、About、轻量引导 | Brand | 可使用较完整的纸感、Mascot、轻纹样和较大留白 |
| Private、Author、`/workbench` | Workbench | 保持任务密度、来源返回、状态槽和移动单列工作流 |
| Console | Workbench | 高密度表格、治理队列、证据和权限语义优先 |
| Flutter | Workbench 的移动原生变体 | 共享 L1 语义和状态规则；组件形态由 Flutter 原生端自治 |
| WebOS `/desktop` | 兼容资产 | 不要求重新接入 family-ui，只阻止新视觉体系继续分叉 |

Radish 全部产品表面的标题只表达真实页面、对象或任务名称。标题上方禁止出现装饰性眉题，包括英文大写栏目名、中文分类词、路由 / 阶段 / 编号标签和主标题的同义复述；标题下方禁止出现仅用于解释标题、同义复述或营造氛围的描述性副标题。允许一行不可被标题替代的真实任务或内容范围说明，但必须直接帮助用户判断“这里有什么 / 下一步做什么”。标题旁只保留真实状态、数量、时间、来源、权限和可执行动作。

## 4. 主题与属性契约

Radish 已发布的四主题是产品与商城权益契约，继续保留：

| 主题 ID | 类型 | family-ui 映射 |
| --- | --- | --- |
| `default` | 内建 light | 使用 `--rd-*` 语义的中性青灰项目取值 |
| `guofeng` | 内建 light | 使用低饱和灰玉品牌；纸色、墨色和状态语义沿用 family-ui 亮色基线，常规操作继续使用墨蓝 action |
| `theme-dark-night` | 权益 dark | 使用 `--rd-*` 暗色语义并保留暗夜主题身份 |
| `theme-sakura` | 权益 light | 使用 `--rd-*` 语义的樱花项目取值 |

约束：

- `data-theme`、主题 ID、服务端 `BenefitValue`、激活 / 停用、失效回退和本地持久化契约不变。
- `data-rd-theme="dark"` 只能由当前有效主题的 `colorScheme` 派生，不能成为第二套用户主题状态。
- `data-rd-profile` 表达当前壳层 Profile，不参与主题权益判断。
- 页面和共享组件只消费 `--rd-*` 或明确登记的 L2 / 壳层别名，不根据主题 ID 分叉业务 CSS。
- 品牌实底前景使用 `text-on-brand`，操作实底前景使用 `text-on-action` 语义；代码迁移期间后者映射现有 `--rd-text-on-accent`。品牌识别与 Workbench 主操作不得继续作为同一语义处理。
- 新增主题必须提供完整 `--rd-*` 语义映射、Ant Design 配置、Flutter 语义映射计划和 PC / mobile 可读性验证。

## 5. Token 实现

目标分层：

```text
family-ui L0 palette / L1 --rd-* semantic tokens
                 ↓
Radish 四主题取值与 Profile 覆盖
                 ↓
过渡 L2：--theme-* / --console-*
                 ↓
壳层与领域：--rx-* / WebOS / 页面布局 token
```

实现位置：

- family-ui CSS / JSON 副本：`Frontend/radish.ui/src/theme/family-ui-tokens.css` 与 `family-ui-tokens.json`
- Client 四主题与 L2：`Frontend/radish.client/src/theme/theme-tokens.css`
- 主题注册表与运行时：`Frontend/radish.client/src/theme/theme.ts`
- 共享 Ant Design 语义：`Frontend/radish.ui/src/theme/antd-theme.ts`
- Console Workbench L2：`Frontend/radish.console/src/index.css`
- Flutter 映射：后续 Web 基线稳定后，在 Flutter 专题内确定 `ThemeExtension` 位置

family-ui CSS / JSON 副本保持与固定上游版本逐字一致；Radish 的四主题取值与品牌前景继续在 Client 项目主题层显式覆盖，避免依赖上游参考默认值形成隐式产品契约。

`R1-P01` 颜色校准把 `guofeng` 品牌从旧胭脂调整为低饱和灰玉：品牌 `#5d6c57`、悬停 `#6e736d`、设计柔底 `#e2e6de`、实底前景 `#fffdf8`；常规操作仍使用墨蓝 `#435c74`。该配色先随 `R1-P01 / 社区发现 / PC 1440` 通过设计审核，并已在 [F4-R-T01](/records/f4-r-t01-web-theme-semantic-baseline-implementation-2026-08-15)进入 Client 四主题 CSS、Ant Design 宿主、共享公开 brand、主题预览与 WebOS 边缘消费；[T03](/records/f4-r-t03-web-four-theme-grouped-runtime-acceptance-2026-08-15)进一步确认四主题代表路径没有页面级品牌语义误用。

迁移期间允许组件继续消费 `--theme-*`、`--console-*` 和 `--rx-*`，但这些变量必须能追溯到 `--rd-*` 或在本附录登记为项目领域 token。不得继续新增没有语义归属的硬编码颜色。

## 6. 领域自治

| 领域 | 自治内容 | 约束 |
| --- | --- | --- |
| 正式 Web 壳层 | 内容宽度、Header、移动底栏、来源返回和状态槽 | 继续使用 `--rx-*`；视觉基础映射到 `--rd-*` |
| Console | 表格密度、治理工作台、证据区、权限与审计表达 | 使用 Workbench Profile；状态语义不得自建另一套红黄绿 |
| Flutter | 原生导航、触控、系统返回、安全区和平台控件 | 共享语义，不照搬 Web DOM / CSS |
| 四主题权益 | 主题身份、购买、激活、回退和跨设备同步 | 不因 family-ui 接入改变业务契约 |
| WebOS | Dock、窗口与历史桌面交互 | 只维护兼容，不作为新 family-ui 组件来源 |

## 7. 已知偏离与处理

| family-ui 条款 | 当前偏离 | 原因 | 处理 |
| --- | --- | --- | --- |
| 通用参考默认与已确认的 `R1-P01` 颜色目标均使用灰玉品牌 | T01 已关闭旧胭脂运行时偏离 | 共享 owner 已具备成组治理条件 | 品牌、悬停、柔底和品牌前景已显式更新；T03 代表路径已确认页面品牌引用、状态色和其他主题身份边界成立 |
| 通用 light / dark | Radish 有四套注册主题 | 已发布产品与权益契约 | 保留主题 ID，统一映射到 `--rd-*` |
| 通用规范列出 Tauri 平台映射 | Tauri 暂时弃用 | 当前多端路线已调整 | 平台示例不构成 Radish 投入要求；继续冻结 Tauri |
| Public 为 Brand | Radish Public 是内容优先社区 | 阅读和互动密度优先 | Brand 用于气质层，不引入营销首页 |
| 项目撤掉整套 UI 专题 | Radish 文档含大量产品与运行时契约 | 不能丢失领域边界 | 先拆通用与项目差异，再逐步收缩 |
| 单一暗色映射 | `theme-dark-night` 有独立暗夜取值 | 权益主题需要稳定身份 | 保持语义一致，视觉收敛单独验收 |

## 8. 维护规则

- family-ui 非破坏性更新先评估，再更新本页遵循版本。
- 破坏性更新必须单独建立迁移窗口，不在业务功能批次里顺手跟进。
- `v26.7.3` 的破坏性窗口已于 `2026-08-03` 完成：新增 `text-on-brand`，当时显式固定 Radish 四主题品牌前景并保留 `guofeng` 胭脂身份；随后 `R1-P01` 颜色校准把国风品牌目标改为灰玉并随 PC 代表页获确认，mobile 与页面实现批收口后，运行时改色已由 [T01 共享主题批](/records/f4-r-t01-web-theme-semantic-baseline-implementation-2026-08-15)关闭。
- Public 内容流新页面默认参考 `R1-P01 / 社区发现 / PC 1440` 的现代自然紧凑语法：继承表面、密度、排版、用色、动作和数据表达，但不复制其信息架构；具体继承卡见[视觉主题规范](/frontend/visual-theme-spec#610-r1-p01-现代自然紧凑基准)。
- 通用语义优先回到 family-ui；Radish 产品差异维护在本页及对应专题。
- 页面级视觉工作必须先完整阅读 family-ui `references.md`、逐张查看其索引的参考图，并在 [F4-R 专题](/features/family-ui-convergence-design)中维护 Radish 页面类型的吸收 / 排除映射；只复制 token 不能视为完成视觉迁移。
- 参考图只用于内部原则提炼，不得进入 `.pen`、产品、`public/` 或对外交付物；移动端不得由桌面参考图等比缩放。
- Pencil 遵循[代表页协作流程](/frontend/pencil-representative-page-workflow)：R1 / R2 先完成必要代表设计，R3 明确继承来源后直接实现，不为每条路由和文案变化复制画板。
- 唯一活动设计源为 `Docs/frontend/design-sources/radish-web-family-ui-v1.pen`；原四个大型 `.pen` 只读留档。普通设计迭代继续更新 `v1`，只有不兼容的结构性升级才新建下一主版本。
- 功能、按钮、文案、权限与状态机以专题文档和当前代码为准；Pencil 只约束共享视觉契约、代表结构、关键状态和响应式变化。
- 页面族验收以代表路径覆盖 PC / mobile、`zh / en`、四主题和 reduced-motion；派生页通过继承检查、代码侧验证和真实截图复核。
