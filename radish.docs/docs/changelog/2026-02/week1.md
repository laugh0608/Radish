# 2026-02 第一周 (02-03 至 02-09)

## 2026-02-08 (周日)

### Client 拆包与加载优化

- **入口懒加载改造**：`src/main.tsx` 改为 `React.lazy + Suspense`，按场景加载 `App` / `Shell`。
- **应用级懒加载注册**：`desktop/AppRegistry.tsx` 改为窗口应用按需加载，避免桌面首屏加载全部子应用。
- **构建分包策略**：`vite.config.ts` 增加 `manualChunks` 规则，拆分 vendor 与应用 chunk。
- **体验页二级懒加载**：`ExperienceDetailApp` 将 `LineChart/PieChart` 改为按需加载，应用主包显著缩小。
- **图标数据异步加载**：`@radish/ui/Icon` 改为按需加载 MDI 图标集并缓存，避免业务包静态打入整份图标数据。
- **构建基线验证**：`npm run build --workspace=radish.client` 通过，入口包与子应用包已拆分。

### Client 拆包与导入统一（持续优化）

- **UI 子路径导入统一**：`radish.client`（showcase 除外）已从 `@radish/ui` 桶导入迁移为 `@radish/ui/*` 子路径导入，避免 barrel export 连带打包。
- **Forum 二级懒加载细化**：发帖/编辑弹窗与帖子详情内容视图独立延迟加载，首屏论坛包进一步收敛。
- **Profile 二级懒加载**：`ProfileApp` 中 `UserPostList` / `UserCommentList` / `UserAttachmentList` 及 `AvatarUploadModal` 改为按需加载。
- **Iconify 精简**：`@radish/ui/Icon` 由整包 MDI 改为 `mdi` 子集异步加载，显著降低图标相关体积。
- **构建结果**：`npm run build --workspace=radish.client` 通过；`app-profile` 约 `59.13 kB`（此前约 `792.80 kB`），`app-forum` 约 `42.56 kB`，无超 500k chunk 告警。

### Client 萝卜坑拆包深化

- **页级懒加载**：`RadishPitApp` 将 `AccountOverview` / `Transfer` / `TransactionHistory` / `SecuritySettings` / `Statistics` 改为按需加载。
- **统计页二级懒加载**：`Statistics` 将 `IncomeExpenseChart` / `CategoryBreakdown` / `TrendAnalysis` 改为按需加载。
- **分包规则细化**：`vite.config.ts` 新增 `pit-transfer` / `pit-history` / `pit-security` / `pit-statistics` chunk 规则。
- **构建结果**：`app-radish-pit` 从约 `491.91 kB` 降至约 `25.32 kB`，并拆分出多个萝卜坑子 chunk；当前超 500k 仅剩 `app-showcase`。

## 2026-02-07 (周六)

### 论坛分类与标签

- **固定标签后端驱动**：固定标签来源从前端 `env` 切换为后端数据，前端通过 `GET /api/v1/Tag/GetFixedTags` 渲染。
- **后台标签管理闭环**：Console 新增“标签管理”页，支持分页、新增、编辑、启停、排序、软删除与恢复。
- **固定标签种子补齐**：新增五个固定标签：`社区新闻`、`社区活动`、`精华帖`、`碎碎念`、`公告`。
- **旧库兼容修复**：`DbMigrate` 在标签种子前自动同步 `Tag` 表结构，修复 SQLite 旧库 `SortOrder` 缺列导致的 Seed 失败。
- **客户端职责统一**：`radish.client` 与 `radish.console` API 调用统一迁移至 `@radish/http`。

## 2026-02-05 (周四)

### 论坛交互与 UI 组件

- **Icon 渲染兜底**：为 `@radish/ui/Icon` 增加统一样式兜底，修复紧凑按钮中图标不显示的问题
- **MarkdownEditor 扩展**：新增 `theme` 与 `toolbarExtras` 支持，便于统一弹窗配色与工具栏扩展
- **论坛发帖体验**：发帖弹窗配置项收敛至编辑器工具栏，整体布局与交互一致化

## 2026-02-03 (周二)

### 前端认证与续期

- **OIDC 登录 scope 补全**：前端登录请求加入 `offline_access`，确保下发 refresh_token
- **短 Token 续期策略优化**：`tokenService` 记录 `token_expires_at`/`token_refresh_at`，动态提前量刷新以适配 3 分钟 Token
- **回调与刷新一致化**：OIDC 回调与自动刷新统一走 `tokenService` 写入过期与刷新时间
- **个人中心请求统一化**：Profile/Balance 改用 `@radish/http` 的 `apiGet`，401 时可自动刷新并重试
