# 前端构建拆包策略

> 本文从 [前端设计文档](/frontend/design) 拆出，承载专题细节；设计入口只保留当前结论、边界和导航。

## 12. 构建拆包策略（manualChunks + 动态导入）

为降低首屏负载并提升 WebOS 子应用的按需加载体验，`radish.client` 采用如下策略：

#### 12.1 动态导入（按应用懒加载）

- 在 `src/main.tsx` 中使用 `React.lazy + Suspense`，按入口场景懒加载 `App` / `Shell`。
- 在 `src/desktop/AppRegistry.tsx` 中将窗口应用改为懒加载注册，打开窗口时再下载对应子应用代码。
- 目标：避免一次性加载论坛、商城、个人中心、萝卜坑等所有应用资源。

#### 12.2 手动分包（manualChunks）

- 在 `vite.config.ts` 中启用 `build.rollupOptions.output.manualChunks`。
- 将常见基础依赖与大体积依赖拆分为稳定 vendor chunk（如 React、i18n、window、markdown 等）。
- 对 WebOS 业务子应用按目录进行应用级 chunk 切分（如 `app-forum`、`app-shop`、`app-radish-pit`）。

#### 12.3 结果与后续

- 构建结果已从“单一超大入口包”转为“入口小包 + 子应用懒加载包”结构。
- `ExperienceDetailApp` 已采用图表二级懒加载（`LineChart/PieChart`），将大图表依赖从应用主包中分离。
- 论坛应用新增二级懒加载：`PublishPostModal` / `EditPostModal` / `PostDetailContentView` 在触发时再加载；发帖弹窗内 `MarkdownEditor` 与预览 `MarkdownRenderer` 也改为按需加载。
- 论坛详情视图继续细分为 `forum-detail-view`（壳层）、`forum-detail-post`（正文）、`forum-detail-comments`（评论），并将 Markdown 生态依赖统一归入分包规则。
- `ProfileApp` 新增 Tab 内容按需加载与头像裁切弹窗懒加载，避免在个人页首屏静态打入附件/裁切相关代码。
- `@radish/ui/Icon` 改为加载 `mdi` 子集（`mdi-subset.json`）并按需异步注册，避免引入整份图标数据。
- `@radish/ui` 在 `package.json` 增加组件子路径导出（如 `icon`、`toast`、`modal`、`input`、`select`、`bar-chart`、`area-chart` 等），client 侧优先使用子路径导入，降低 barrel export 连带打包风险。
- 最新构建（2026-02-08）中，`app-profile` 已从约 `792.80 kB` 降至约 `59.12 kB`，`app-forum` 约 `42.30 kB`。
- 发帖弹窗主 chunk 进一步收敛：`forum-publish-modal` 从约 `341.12 kB` 降至约 `10.36 kB`，编辑器与渲染器拆至独立异步 chunk（`MarkdownEditor` / `MarkdownRenderer`）。
- 萝卜坑已完成页级拆分：`app-radish-pit` 约 `25.32 kB`，并拆出 `pit-transfer` / `pit-history` / `pit-security` / `pit-statistics` 独立 chunk。
- Showcase 已从桶导入迁移到子路径导入，`app-showcase` 从约 `751.21 kB` 降至约 `410.06 kB`。
- `forum-detail-view` 已从约 `349.00 kB` 拆分为：`forum-detail-view` 约 `5.49 kB`、`forum-detail-post` 约 `3.91 kB`、`forum-detail-comments` 约 `19.85 kB`。
- 商城工作台已完成页级拆分：`ShopApp` 按首页、商品列表 / 详情、订单列表 / 详情、背包和购买弹窗懒加载，并通过 `shop-products`、`shop-orders`、`shop-inventory`、`shop-purchase` 等 chunk 分组承载，`app-shop` 已从超过 500k 的历史 warning 收敛到约 `27.39 kB`。
- 论坛评论工具链已进一步拆为 `forum-comments-tree`、`forum-comments-editor`、`forum-comments-reactions`、`forum-comments-stickers` 等分组；评论编辑器仍会携带 Markdown 预览、上传、艾特和贴纸工具，属于已知重交互 chunk。
- `radish.client` 当前将 `chunkSizeWarningLimit` 设为 `800`，用于匹配论坛评论工具链的已知体积边界；若后续出现新的超限 warning，应先判断是否来自新增重依赖或分包规则失效，不能只提高阈值掩盖问题。

##
