# Flutter Native P5-C1 Docs Reader 实现记录

> 状态：`P5-C1` 已完成；后续 [P5-C2 Commerce Browse / Transaction](/records/f4-flutter-native-p5c2-commerce-browse-transaction-implementation-2026-08-24)与 [P5-C3 Commerce Private](/records/f4-flutter-native-p5c3-commerce-private-implementation-2026-08-24)也已完成，当前进入 P5-D1 Wallet / Experience readiness
>
> 日期：2026-08-23（Asia/Shanghai）
>
> 前置记录：[P5-C1 实施就绪与方案冻结](/records/f4-flutter-native-p5c1-docs-reader-readiness-2026-08-23)

## 1. 结论

P5-C1 已按冻结边界完成。Docs tab 的公开目录继续只消费既有 `Wiki/GetList` 页码读模型，正文继续只消费 `Wiki/GetBySlug/{slug}`；没有新增或修改后端 API。目录已显式拥有 query target、请求代际、权威页快照和结构化 issue；同查询刷新保留旧页并可进入 stale，新关键词或页码不会误显上一个 target 的结果，迟到与 disposed owner 响应均被隔离。

页面内正文、Discover / Profile / Browse History 直达和文档内链现在共用 `DocsDetailController` 与 `DocsReaderSurface` 契约，同时每个 Navigator route 仍持有独立 controller 和真实返回栈。同 slug 刷新会保留旧正文并表达 refreshing / stale / recover；切换 slug 会立即清除上一篇正文，响应标题与正文始终是权威快照，`initialTitle` 只在首次读取前提供上下文。

compact / medium / expanded 分别形成目录与正文互斥的单任务流、目录—正文同屏和 expanded `280px` 目录 + `24px` 间距 + `904px` 正文阅读轴。搜索、页码、滚动恢复、公开链接复制、数字旧 slug 隐藏、设备最近文档、来源返回与既有 docs 内链规则均保持。

## 2. Owner 与规模

| Owner | 实施后职责 | 行数 |
| --- | --- | ---: |
| `docs_page.dart` | Docs tab 生命周期、query / selection、滚动、handoff 与 Back 编排 | `390` |
| `docs_feed_controller.dart` | query target、目录快照、刷新、请求代际与结构化 issue | `207` |
| `docs_detail_controller.dart` | 公共 reader target、正文快照、刷新 stale 与请求代际 | `176` |
| `docs_catalog_surface.dart` | 搜索、列表、页码、目录状态和 pane / flow 密度 | `354` |
| `docs_reader_surface.dart` | 标题、来源、公开链接、正文、刷新状态和 docs 内链 | `327` |
| `docs_reader_route.dart` | 直达 / 嵌套 route 的薄 Navigator 外壳 | `119` |
| `docs_page_shared_widgets.dart` | Docs 页面族局部 heading、状态与元数据组件 | `223` |
| `docs_issue.dart` | 目录 / reader 共用结构化 issue | `42` |

原 `1294` 行页面已按真实职责拆分。原 `944` 行测试保留为 `24` 行 library 入口，并拆为 behavior、controller 与 adaptive 三组 owner；本批改动 Dart 文件最大 `951` 行，全部低于 `1500` 行硬上限。

## 3. 权威状态与导航契约

- `DocsFeedQuery` 以标准化 keyword、pageIndex 与 pageSize 表达目录 target；首次 / 新 target 使用 loading，同 target refresh 保留旧页，失败通过结构化 issue 区分 unavailable、invalid response 与 request。
- `DocsDetailController` 统一维护 `idle / loading / ready / unavailable / stale`；新 slug 清旧正文，同 slug refresh 才允许保留旧正文。
- feed / reader 的 target 变化、close、dispose 与新 generation 都会使旧响应失效；LongId / slug 保持字符串，不做数值转换。
- 页面内与 route 使用同一 reader surface；route 仍为独立 owner，因此 linked-doc push、逐级 Back、来源返回和 Shell recent 记录保持原契约。
- 成功响应的 slug、title、content 与时间字段是正文权威；handoff `initialTitle` 不覆盖成功快照。

## 4. 三档结构

| 窗口 | 实施结果 |
| --- | --- |
| compact `390px` | 目录与正文互斥；正文返回后恢复搜索、页码和滚动上下文，Android Back 优先退出当前正文任务 |
| medium `800px` | 受控目录—正文同屏；目录保持 `200–280px`，无选择时不自动选择第一篇 |
| expanded `1440px` | `280px` 目录 + `24px` 间距 + `904px` 正文阅读轴，落在既有 content frame 内，不增加第三 rail |

直达 handoff 不为布局补取目录；各断点都复用同一正文 surface。四主题消费同一布局结构与既有 Theme / Shared token，没有建立页面专属主题或断点体系。

## 5. 验证

- Docs 定向：`35 / 35`（改造前 `16 / 16`，既有行为用例未减少）。
- Shell 静态 Smoke：`51 / 51`；覆盖 Docs tab、Discover / Profile handoff、linked-doc、recent store 与 Android Back。
- Flutter 全量：`293 / 293`。
- `flutter analyze`：零问题。
- `dart format`、文件行数与 `git diff --check` 通过；文档与仓库卫生门禁在本记录同步后执行。

本批未新增或修改后端 API、DTO、权限、数据库、migration、依赖、lockfile、Pen 或平台工程，未启动服务，未执行真实 Gateway、浏览器、Android RC 或其他设备 Smoke。

## 6. 下一顺位

P5-C1 关闭。后续 [P5-C2 Commerce Browse / Transaction](/records/f4-flutter-native-p5c2-commerce-browse-transaction-implementation-2026-08-24)与 [P5-C3 Commerce Private](/records/f4-flutter-native-p5c3-commerce-private-implementation-2026-08-24)均已按冻结边界完成；P5-C3 关闭时 Shop `50 / 50`、Shell Smoke `51 / 51`、全量 `318 / 318`。当前下一顺位只进入 P5-D1 Wallet / Experience readiness；派生实现、平台工程、服务启动或真实运行态 Smoke 仍需独立授权。
