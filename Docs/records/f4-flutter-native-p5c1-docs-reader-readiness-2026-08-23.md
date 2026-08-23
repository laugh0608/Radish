# Flutter Native P5-C1 Docs Reader 实施就绪与方案冻结

> 状态：`P5-C1 readiness` 已完成；等待实施确认
>
> 日期：2026-08-23（Asia/Shanghai）
>
> 前置记录：[P5-A 页面族拆批就绪审计](/records/f4-flutter-native-p5a-page-family-readiness-2026-08-23)、[P5-B2 Identity / Revisit 实现](/records/f4-flutter-native-p5b2-identity-revisit-implementation-2026-08-23)

## 1. 本批结论

P5-C1 可以完全复用既有 `Wiki/GetList` 与 `Wiki/GetBySlug/{slug}` 实施，不需要新增后端 API、DTO、权限、数据库或移动端 BFF。现有 Docs 已具备公开列表、关键词搜索、页码、列表刷新旧快照、内联详情、直达 handoff、正文内链、公开链接复制、设备最近文档和 Android Back；主要缺口是列表与详情仍使用早期 MVP 页面、内联与 handoff 各自复制详情 route shell、详情刷新遮住旧正文、错误只有字符串，以及没有页面级三档阅读结构。

本批固定为同一 Docs Reader 契约下的两类任务：Docs tab 内的目录—正文任务，以及 Discover / Profile / Browse History / 文档内链直达的正文任务。两类入口共用同一个 reader controller、正文 surface、状态与链接规则；每个 Navigator route 持有独立 controller 实例，以保留嵌套文档的真实返回栈，不能把所有 route 强行绑定为一个全局实例。

本次只完成代码事实审计、定向基线与方案冻结，没有修改 Dart、API、依赖、lockfile、Pen 或平台工程，没有启动服务或执行真实 Gateway / 设备 Smoke。

## 2. 当前事实与基线

### 2.1 Owner 与规模

| Owner | 当前职责 | 当前规模 |
| --- | --- | ---: |
| `docs_page.dart` | 页面生命周期、列表 / 详情切换、搜索、滚动恢复、handoff route、全部页面 widget | `1294` 行 |
| `docs_feed_controller.dart` | 页码、搜索、列表刷新与单一请求代际 | `224` 行 |
| `docs_detail_controller.dart` | slug、详情读取、刷新与单一请求代际 | `143` 行 |
| `docs_repository.dart` | `Wiki/GetList` 与 `Wiki/GetBySlug` | `62` 行 |
| `docs_models.dart` | 列表 / 详情 VO、handoff source / target 与 LongId 字符串读取 | `257` 行 |
| `docs_page_test.dart` | 页面、状态、导航与 repository stub | `944` 行 |

`docs_page.dart` 尚未超过 `1500` 行，但已接近硬上限；P5-C1 必须先按真实职责拆分，不能在原文件继续叠加三档布局。测试也在新增状态与断点回归前按职责拆分。

### 2.2 当前状态与导航缺口

- feed controller 已有 `_requestVersion`，同一查询刷新会保留旧页；但首次错误与刷新错误仅保存字符串，无法保留 `kind / code / statusCode`，也未显式阻止 disposed owner 的迟到响应。
- 搜索词或页码变化属于新目录 target；当前状态虽进入 loading，但旧 `page` 仍留在 state。新实现必须明确不把旧查询快照渲染为新查询结果。
- detail controller 已有 slug 请求代际；同一 slug 刷新时 state 内虽仍保存旧 detail，UI 却只渲染 loading / error，因此旧正文会被遮住，失败后也无法表达 stale。
- 切换 slug 与刷新同一 slug 的语义尚未区分：切换目标不得继续显示上一篇正文，同一目标刷新才允许保留权威旧正文。
- Docs tab 使用页面内 detail controller；handoff 使用 `_DocsDetailRoutePage` 再创建 controller。两者共用 `_DocsDetailContent`，但标题、能力卡、动作、loading / error 和 linked-doc 导航 shell 重复。
- compact 内联详情已通过 `PopScope` 与 Shell callback 优先返回搜索 / 列表并恢复滚动；handoff route 与嵌套文档使用 Navigator 栈返回来源。这两套返回语义必须保留。
- `DocsDetailHandoffTarget.initialTitle` 只应作为请求前标题提示；slug 与 `GetBySlug` 响应才是正文权威来源，不能用旧标题覆盖响应。

### 2.3 测试基线

改造前 `flutter test test/docs_page_test.dart` 为 `16 / 16`。已有覆盖包括列表、列表刷新旧快照与失败恢复、搜索、页码上下文、详情打开 / 返回、滚动恢复、窄屏长 slug、inline / route 文档内链、相对 / 绝对链接、handoff recent、数字旧 slug 隐藏、列表失败与详情失败。

当前没有直接覆盖：详情旧正文刷新 pending / stale / recover、跨 slug 迟到响应、disposed owner、结构化 issue、搜索 / 页码 target 隔离、`390 / 800 / 1440` 页面级结构、expanded `280 / 904` 目录—正文、四主题同构、长正文 / 空正文，以及 inline 与 route 是否真正消费同一 reader surface。

## 3. 既有 API 与 target mapping

P5-C1 只消费以下既有契约：

| 既有能力 | P5-C1 用途 | 裁决 |
| --- | --- | --- |
| `GET Wiki/GetList?pageIndex&pageSize&keyword` | 公开目录、搜索与页码 | 保持页码读模型；不改为 cursor，不新增聚合接口 |
| `GET Wiki/GetBySlug/{slug}` | 公开正文权威快照 | 所有 inline / handoff / linked-doc 入口共用 |
| `DocsDetailHandoffTarget` | slug、来源、初始标题 | slug 必填且标准化；来源与初始标题只作本地上下文 |
| `DocsDetailHandoffSource` | Shell、Discover、Docs list / link、Browse History、Profile recent | 枚举与 label 保持；不新增伪来源 |
| Shell recent Docs store | 设备最近文档 | 继续由 Shell owner 记录与去重，不迁入 Docs controller |
| `ReadOnlyMarkdownView` | 公开 Markdown 与 docs 内链 | 保持 `/docs/:slug`、完整 URL、`docs/:slug`、`./:slug` 和普通相对 slug 规则 |
| `PublicLinkCopyPanel` | Gateway Base URL + `/docs/:slug` | 16 位以上纯数字旧 slug 继续只兼容打开，不展示或复制 |

这里的“目录”固定指公开文档列表、搜索与页码，不是从 Markdown 标题派生的页内大纲。P5-C1 不新增 heading parser、锚点定位、附件打开或外部浏览器跳转。

## 4. 权威状态模型

### 4.1 目录 owner

`DocsFeedController` 继续作为公开目录唯一远端 owner，并显式维护 query target：标准化 keyword、pageIndex、pageSize、request generation、页面快照和结构化 issue。

- 首次读取或新 query target 进入 loading；失败进入 unavailable / error，不能渲染上一个关键词或页码的旧列表。
- 同一 query 刷新保留旧页并标记 refreshing；失败保留旧页并标记 stale，成功用权威页替换并清除 issue。
- query target 变化、repository 替换或 dispose 会使旧响应失效；成功响应回写服务端实际 `page`。
- issue 至少保留 `kind / message / code / statusCode`；`FormatException` 明确归为 invalid response，不把所有错误压成字符串。
- 本批保持经典页码，不增加 cursor 拼接或跨页去重语义；列表项 LongId 继续只作字符串。

### 4.2 Reader owner

现有 `DocsDetailController` 收敛为公共 Docs Reader owner，持有标准化 target、详情权威快照、request generation、refreshing 与结构化 issue。状态统一表达 `idle / loading / ready / unavailable / stale`。

- 打开新 slug 时立即建立新 target 并清空上一篇 detail；首次失败只显示当前目标 unavailable 与重试。
- 同一 slug 刷新保留旧 detail，正文持续可读并显示局部 refreshing；失败保留旧正文并标记 stale，成功替换为新权威详情。
- slug 切换、close、route pop、repository 替换或 dispose 都递增 generation；迟到响应不能回写当前 reader。
- 响应 detail 的 slug / title / content 是正文权威；target `initialTitle` 仅在首次读取前辅助标题，不参与成功快照合并。
- 每个 route 使用独立 reader controller 实例，但 inline、handoff 和 linked-doc route 必须构造同一 controller 类型并渲染同一 reader surface；这就是“共用 owner”，不是跨 Navigator route 共享可变实例。

## 5. Owner 拆分

进入实现前按以下真实职责拆分：

| Owner | 冻结职责 |
| --- | --- |
| `docs_page.dart` | Docs tab 生命周期、目录 query、选中 target、滚动恢复、Shell handoff 与 Back 编排 |
| `docs_feed_controller.dart` | query target、目录快照、页码、刷新、请求代际与结构化 issue |
| `docs_detail_controller.dart` | 公共 reader target、详情快照、刷新 stale、请求代际与结构化 issue |
| `docs_catalog_surface.dart` | 搜索、列表、页码、目录状态与 compact / pane 两种密度 |
| `docs_reader_surface.dart` | 标题、来源、公开链接、正文、刷新状态和 docs 内链 |
| `docs_reader_route.dart` | 直达 / 嵌套 route 的薄 Navigator 外壳与来源返回 |
| `docs_page_shared_widgets.dart` | Docs 页面族局部 heading、issue notice 与元数据组件 |

`docs_page_test.dart` 按 feed / reader states、adaptive、navigation / handoff 与 support / fixtures 拆分。现有 16 个用例不得减少，所有改动 Dart owner 必须低于 `1500` 行。

## 6. 三档阅读结构

页面继续消费既有 `RadishWindowClass`、`RadishContentFrame`、`RadishSectionSurface`、`RadishStateSlot`、四主题 token 与 reduced-motion，不建立第二套断点或视觉状态原语。

| 窗口 | 冻结结构 |
| --- | --- |
| compact `390px` | 单任务：目录与正文互斥。目录为搜索—结果—页码连续流；打开正文后由同一 reader surface 占满任务区，顶部保留返回目录。Android Back 优先回到原搜索 / 页码与滚动位置。 |
| medium `800px` | 目录—正文同屏。左侧目录宽度按可用空间约三分之一并限制在 `200–280px`，右侧正文使用剩余主轴；无选择时显示真实“选择文档”引导，不自动选中第一篇。 |
| expanded `1440px` | `280px` 目录 + `24px` 间距 + `904px` 正文阅读轴，整体落在既有 `1280px` content frame 内；搜索、页码留在目录，正文不增加第三条洞察 rail。 |

直达 handoff route 不为凑布局重复请求目录：compact、medium 与 expanded 都使用同一正文 surface；medium / expanded 将正文限制在 `904px` 阅读轴，并把来源返回与只读边界作为紧凑上下文。Docs tab 的目录—正文和 handoff 的正文任务共享组件与状态语义，但保持不同导航栈。

medium / expanded 选中新文档只更新右侧 reader，目录搜索、页码和滚动上下文继续保留；Android Back / Shell Back 清除当前选择后仍留在 Docs tab。文档内链在 Docs tab reader 中替换当前选中目标，返回仍回目录；从 handoff route 打开的内链继续 push 新 route，Back 逐级返回上一文档和最初来源。

## 7. 关键行为与状态门禁

1. 保留现有 `16 / 16` Docs 用例，并新增 feed / reader controller 定向状态测试。
2. 覆盖目录 initial unavailable、同 query refresh pending / stale / recover、搜索 / 页码新 target 不展示旧页，以及迟到 / disposed 响应隔离。
3. 覆盖 reader initial loading / unavailable / retry、同 slug 旧正文 refresh pending / stale / recover、新 slug 清空旧正文、跨 slug 迟到响应与 disposed 隔离。
4. 覆盖 `390 / 800 / 1440`、expanded `280 / 904` 主轴、长标题 / slug / Markdown / code、空正文、四主题同构和无横向溢出。
5. 证明 inline 与 handoff route 使用同一 reader surface / controller contract，同时保留 route 独立实例与嵌套 Back 栈。
6. 保留搜索、页码、列表刷新、滚动恢复、公开链接复制、数字旧 slug 隐藏、所有既有 docs 内链、recent 记录和来源返回。
7. 运行 Docs 定向、Shell 静态 Smoke、`flutter analyze`、全量 `flutter test`、文件行数、`npm run check:docs`、仓库卫生与 `git diff --check`。

## 8. 停止线

- 不新增或修改后端 API、DTO、权限、数据库、migration、依赖或 lockfile。
- 不新增编辑、发布、回收站、版本治理、作者协作、收藏、附件治理、页内大纲、外部浏览器跳转或离线下载。
- 不改 Shell 主导航、recent store、Discover、Forum、Profile、Browse History、Commerce 或其他页面族；只保持现有 Docs handoff consumer 契约。
- 不把直达 handoff 改成隐式目录请求，不把 `initialTitle` 或设备 recent 当作正文权威快照。
- 不读取或修改 Pen；P5-C1 直接继承 P3 Reader 方向与 P4 Theme / Shared / Shell。
- 不启动服务，不执行真实 Gateway、浏览器、Android RC 或其他设备 Smoke；运行态验收继续独立授权。

P5-C1 readiness 已关闭，下一步等待项目所有者确认按上述冻结方案进入 Dart 实施。Commerce、派生只读面、平台工程与运行态 Smoke 不随本方案自动授权。
