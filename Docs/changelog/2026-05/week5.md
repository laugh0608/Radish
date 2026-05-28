# 2026 年 5 月第 5 周开发日志

## 2026-05-25

- 多端路线进一步收敛为 `纯 Web + Flutter` 主线：普通浏览器根路径 `/` 与默认入口转向纯 Web，`/desktop` 仅保留为 WebOS 历史入口，后续新功能默认不进入 WebOS；PC/Tauri 放到最后再评估，若重启也只做纯 Web 增强体验。
- 相关规划与说明已同步到 [开发路线图](/development-plan)、[当前进行中](/planning/current)、[前端设计文档](/frontend/design)、[前端多壳层策略](/frontend/shell-strategy)、多端路线评估方案、阶段规划、验证基线和回归索引。
- `P3-8-D` 已建立移动 Web 公开视图验收矩阵，覆盖 `/discover / forum / docs / u/:id / leaderboard / shop` 的窄屏信息密度、来源返回、分享入口、公开只读边界、公开链接和跨端回流。
- 完成 `390 x 844` Vite 直连移动视口实测，未发现新的 `P0/P1`；公开页顶部工作台入口从 `/` + `WebOS` 收敛为 `/desktop` + “工作台”，避免根路径转纯 Web 后入口语义漂移。
- 普通浏览器根路径 `/` 已切向 `/discover` 公开分发页，Vite 直连与 Gateway 入口均已复核；Tauri 当前仍保留 `/desktop`。
- 历史 `?demo` 认证测试页已移除，旧 `App.tsx / App.css` 删除，`/oidc/callback` 拆为独立正式回调入口；`/?demo` 不再绕过纯 Web 默认入口。
- 公开个人页 `/u/:id` 已补显式复制链接入口；forum / docs / shop / profile 详情类公开分享状态收敛到统一 hook，底层剪贴板复制改为先同步 textarea fallback、再回退 `navigator.clipboard`。
- 今日验证覆盖 `radish.client` 类型检查、定向 node tests、生产构建、changed-only 文本卫生、`git diff --check` 与浏览器实测；`radish.client` 构建仍保留既有 `app-shop` chunk size warning。
- 收工前已补 [2026-05-25 收工回顾与明日事项](/records/daily-handoff-2026-05-25)：明日优先补 `430 x 932 / Gateway` 移动公开视图复核；若无阻断，再评估纯 Web 登录后购买 / 订单 / 背包轻链路，只选择一个一天级闭环。

## 2026-05-26

- `P3-8-D` 继续按纯 Web + Flutter 主线推进，完成公开详情来源返回持久化、公开商城到 `/desktop` 的购买入口修正、商品 / 订单 / 背包深链承接，以及未登录商品详情登录后恢复原商品上下文继续购买。
- Flutter 侧完成三个高价值小闭环：经验榜条目打开原生公开主页并返回榜单；最新 forum 通知打开帖子详情后返回原打开位置；forum detail 轻回应区发起登录后回到当前轻回应区继续发布。
- 规划入口、P3-8-D 专题和 Flutter README 已同步今日能力边界；本批没有新增后端 API、数据库结构、权限模型、视觉设计稿或共享 UI token，因此前端视觉规范、部署说明和 Pencil 设计源文件无需跟随更新。
- 今日验证覆盖 `radish.client` 定向测试、类型检查、生产构建，Flutter 定向测试、`flutter analyze`、全量 `flutter test` 与 `git diff --check`；`radish.client` 构建仍保留既有 `app-shop` chunk size warning。
- 收工前已补 [2026-05-26 收工回顾与明日事项](/records/daily-handoff-2026-05-26)：明日优先做今日链路的批量验收复核，再根据结果只选择一个纯 Web 或 Flutter 一天级小闭环。

## 2026-05-27

- `P3-8-D` 二轮复核继续补齐纯 Web 公开主路径：公开商品榜单进入商品详情并保留榜单来源返回、商城详情返回文案按商品列表 / 榜单来源精确展示、公开详情显式来源返回保留既有 `history.state` 来源链路，避免 `discover -> forum detail -> profile -> 返回 forum detail` 形成来源循环。
- Gateway 公开页资源 URL 已收口：HTTPS Gateway 下本地 HTTP 媒体、favicon、头像和 Markdown 附件归一到当前 Gateway origin；移动 Web 公开阅读链路补齐 Markdown、docs 详情和 forum 详情窄屏防溢出；公开分享链接统一走运行时公开域名配置。
- Flutter 公开个人页来源返回已补齐：发现、论坛作者和榜单进入原生公开主页后，Android Back 可回到原来源；公开主页继续打开帖子 / 评论详情并返回后，仍保留来源 tab。
- 前端构建治理已处理历史 `app-shop` chunk warning：`ShopApp` 按页面和购买弹窗懒加载，商城手动 chunk 细分后 `app-shop` 已低于 500k 警告阈值；仓库不保留 npm update notifier 配置，npm 自身 update notice 仍按本机环境显示。
- `/discover` 论坛卡片公开路径已统一为 PublicId 优先，与 forum 列表 / 搜索 / 标签页 URL 口径一致；P3-8-D 移动 Web 验收矩阵已补 2026-05-27 二轮静态复核记录。
- 规划入口、P3-8-D 专题、Flutter README、前端构建拆包说明、记录索引与本周 / 本月开发日志已同步；今日没有新增后端 API、数据库结构、权限模型、视觉 token、Pencil 设计稿或部署配置，因此相关说明书无需跟随更新。
- 今日验证覆盖 `radish.client` 公开路由、公开 head、商城来源返回、Gateway 资源 URL、商城登录回流等定向测试，`radish.client` / `@radish/ui` 类型检查，`radish.client` 生产构建，Flutter `smoke_test`、全量 `flutter test`、`flutter analyze`，`validate:baseline:quick`、changed / staged 文本卫生与 `git diff --check`。
- 收工前已补 [2026-05-27 收工回顾与明日事项](/records/daily-handoff-2026-05-27)：明日优先转向 Flutter `公开个人页 -> 帖子 / 评论详情 -> Android Back 回到原 profile 来源` 主路径；若能力已完整，只补验收结论，不继续为了纯 Web 矩阵凑低收益小闭环。

## 2026-05-28

- `P3-8-D` 继续沿纯 Web + Flutter 主线推进，先完成纯 Web / 工作台论坛登录回流、Dock 主动登录回流、公开商城工作台入口契约、公开商品榜单到详情、Gateway 公开资源 URL 和移动 Web 公开阅读链路二轮复核；移动 Web 公开视图矩阵阶段收口，后续不再逐页打磨 Web 公开页。
- Flutter 侧连续补齐公开商品只读详情、轻量 forum 通知列表、公开商城列表、论坛详情评论发布 / 回复、评论区登录回流与来源回归；随后补齐 forum/docs/shop 原生公开详情完整公开链接展示与复制入口。
- Flutter docs 阅读链路继续补强：只读 Markdown 阅读器可识别 `/docs/:slug`、完整公开 URL、`docs/:slug`、`./:slug` 与普通相对 slug 文档内链并打开原生 docs detail；页内锚点、附件路径和非 docs 链接继续按文本展示。
- 今日文档同步覆盖 [当前进行中](/planning/current)、[开发路线图](/development-plan)、[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)、[Flutter README](../../../Clients/radish.flutter/README.md)、本周 / 本月开发日志和每日交接记录；本轮没有新增后端 API、数据库结构、权限模型、视觉 token、Pencil 设计稿或部署配置，因此相关说明书无需跟随更新。
- 今日验证覆盖 `radish.client` 定向测试、类型检查、构建和 changed 文本卫生；Flutter 全量 `flutter test`、`flutter analyze`、docs / forum / shop 定向测试；仓库级 `git diff --check` 与 `npm run check:repo-hygiene:changed`。
- 收工前补 [2026-05-28 收工回顾与明日事项](/records/daily-handoff-2026-05-28)：明日优先推进 Flutter 原生公开主页链接复制 / 展示、长文本防溢出、来源返回和匿名 / 已登录边界复核。
