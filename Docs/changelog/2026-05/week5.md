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
