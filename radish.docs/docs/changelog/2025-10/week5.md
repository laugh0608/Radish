# 2025年10月 - 第5周（10月28日-10月31日）

> 移动端适配与文档完善

::: warning 历史记录
以下内容为历史记录，保留 ABP + Angular + MongoDB 方案的决策脉络以供参考，但不再作为现行实现依据。
:::

## 2025.10.29

- feat(openiddict): 同时允许 http/https 回调与登出回调（含静默刷新），前端切换协议无需改配置。
- docs(readme): 重写根 README，整合 docs 内容，补充快速开始、HTTPS/CORS/SSO 指南与常用脚本。
- docs: 合并 DevelopmentBoard 至 DevelopmentPlan，统一为"开发计划与看板"，并更新索引与前端文档引用。
- docs(framework): 在 DevelopmentFramework.md 顶部新增"功能期望与范围"（功能/非功能、里程碑、范围外）。
- docs(index): docs/README.md 索引项更名为"项目总体功能与需求（含范围与里程碑）"。
- docs(framework): 增补"非目标与边界（Non-Goals & Boundaries）"详细清单，明确暂不覆盖项与阶段边界。

## 2025.10.28

- feat(host/mobile): 移除 Hero 区小屏"更多"下拉，统一使用下方移动端功能面板。
- feat(host/mobile): 重构移动端功能面板为两列栅格；语言入口改为下拉；"登录"在未登录时独占整行以提高可见性。
- style(host/mobile): 暗色模式适配移动功能卡片（半透明毛玻璃、清晰边框与阴影），并增强下拉与描边按钮在暗色下的对比度；主题切换、我的账户、注销统一为描边样式。
- style(host/mobile): "卡片密度"从整行分段控件调整为"标签 + 三按钮"，最终并入为"一体式分段控件（含标签的不可点击段）"，与其他按钮视觉一致。
- fix(host/mobile): 解决 Razor 编译错误（string 与 LocalizedHtmlString 混用），语言按钮使用 `L["Language"].Value`。
- fix(host/mobile): 分段控件按钮被全局 100% 宽度规则挤压的问题，显式设置 `width:auto` 恢复等分显示。
- feat(host/mobile): 分段控件支持"当前密度"选中高亮，并与本地存储 `radish.density` 联动初始化与更新。
