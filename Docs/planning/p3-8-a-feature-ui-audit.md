# P3-8-A 多端功能缺口与 UI 设计入口审计

> 状态：首批审计结论；`P3-8-B1` 已完成
>
> 日期：2026-05-23（Asia/Shanghai）
>
> 本页承载 `P3-8-A` 的多端功能缺口矩阵、UI 端点分组和首批开发任务建议。当前主线入口仍以 [当前进行中](/planning/current) 为准。

## 审计依据

- [P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)
- [前端多壳层策略](/frontend/shell-strategy)
- [UI 设计灵感参考](/frontend/ui-design-inspiration)
- `Frontend/radish.client/src/public/PublicEntry.tsx`
- `Frontend/radish.client/src/desktop/AppRegistry.tsx`
- `Clients/radish.flutter/README.md`
- `Clients/radish-tauri/README.md`
- `Frontend/radish.console/src/router/routes.ts`

## 总体判断

`P3-7-C` 已把首批明显热区降到可维护区间，继续把主线压在低风险拆分上收益下降。`P3-8-A` 应转向多端真实使用缺口和 UI 端点治理，但第一批仍不直接大面积实现。

当前缺口分成两类：

- **功能缺口**：用户能看到入口或提示，但某端还没有对应只读 / 登录态能力。
- **设计治理缺口**：页面可用，但端点风格、信息密度、组件复用和视觉 token 不一致，适合先做 Pencil 设计稿。

首批推进应优先选择功能缺口，设计治理先定端点和设计源文件，不让 Pencil 前置阻塞小范围功能开发。

## 多端功能缺口矩阵

| 端点 | 已有能力 | 主要缺口 | 类型 | 优先级 | 设计稿要求 |
| --- | --- | --- | --- | --- | --- |
| 公开 Web | `discover / forum / docs / u/:id / leaderboard / shop` 直达、来源返回、公开 head、动态 sitemap、详情 head snapshot | 公开页功能基本完整，后续主要是移动视图细节、分享预览真实反馈和运行日志暴露问题 | 维护 / 小修 | P2 | 页面级改版前需要 |
| 移动 Web 公开视图 | 复用公开 Web 壳层，移动浏览器可直达公开路径 | 未形成独立移动视图验收矩阵；公开页面是否在窄屏保持一致信息密度仍需按端点抽查 | 验收缺口 | P2 | 端点设计时需要 |
| Flutter 移动客户端 | Android MVP 覆盖 `discover / forum / docs / profile`、登录、复访、通知回流和轻回应；`P3-8-B1` 已补齐公开经验榜只读入口 | 购买、订单、账号专属操作仍留到后续；榜单首批仅覆盖公开经验榜第一页 | 功能缺口 | P1 已处理首批 | 不强制，可按现有原生列表样式实现 |
| WebOS / PC 工作台 | Dock、窗口系统、forum、chat、wiki、profile、notification、leaderboard、experience、shop、radish-pit 等应用 | `radish-pit` 仍有未接线通知 hook 和统计图表历史占位痕迹；部分应用视觉 token 与共享组件使用不均 | 功能 + 设计治理 | P2 | 页面级整理前需要 |
| Tauri 桌面壳 | 复用 WebOS，已完成 loopback 登录、默认 `/desktop`、候选包身份和 NSIS 首轮验证 | 签名、自动更新、托盘、菜单、公开分发、普通用户安装 / 卸载补验仍后置 | 分发 / 壳层能力 | P3 | 不需要，除非改 WebOS UI |
| Console 管理后台 | 已覆盖 Dashboard、用户、角色、商品、订单、内容治理、经验治理、萝卜币、贴纸、系统配置等 | `ModerationPage`、`ExperienceAdminPage` 等治理大页超过建议维护边界；局部仍混用 AntD 原生、硬编码颜色和页面私有样式 | 设计治理 + 热区 | P1 | 需要 Console 治理端点设计稿 |

## UI 端点分组

| 设计端点 | 覆盖范围 | 参考方向 | 当前建议 |
| --- | --- | --- | --- |
| 公开内容壳层 | discover、forum、docs、profile、leaderboard、shop 的公开 Web 与移动 Web | Discourse 列表、GitHub 个人页、AFFINE 文档 | 先做移动视图验收矩阵，不急于重画整套公开页 |
| Flutter 移动客户端 | discover、forum、docs、profile，后续可能加入 leaderboard | 原生单任务导航、移动低密度阅读 | 小功能可直接实现；整体视觉治理后续单独设计 |
| WebOS 工作台 | Dock、Desktop、窗口容器、forum、chat、wiki、shop、profile、radish-pit | AFFINE 三栏、Discourse chat、GitHub 列表 | 已完成首批热区治理，下一步只处理高价值断点 |
| Console 治理工作台 | Moderation、Experience、Coins、Users、Orders、Products | Cloudflare / Discourse admin、CodexApp 设置 | 建议首个 Pencil 端点，先统一大页结构与信息密度 |
| Tauri 桌面安装包 | Tauri 系统壳 + WebOS 默认入口 | 不单独设计 UI | 继续复用 WebOS，不做原生 UI 设计稿 |

## 设计源文件治理建议

- 建议目录：`Docs/frontend/design-sources/`。
- 建议命名：
  - `public-content-shell.pen`
  - `flutter-mobile-client.pen`
  - `webos-workbench.pen`
  - `console-governance-workbench.pen`
- `.pen` 文件只通过 Pencil 工具创建、读取和修改，不使用普通文本工具编辑。
- 首批不必一次性创建所有 `.pen`；只有进入页面级 UI 改造前才创建对应设计稿。
- 小范围功能补齐、文案、状态、后端治理和行为等价拆分不要求先创建 `.pen`。

## 首批开发任务建议

### 已完成：`P3-8-B1 Flutter 公开榜单只读入口`

理由：

- Flutter `discover` 当前已明确提示“榜单详情后续批次再评估”，这是用户可感知功能缺口。
- Web 公开榜单和后端榜单能力已存在，移动端首批可只做只读阅读，不触碰购买、订单、背包或治理能力。
- 不需要先做 Pencil 设计稿，可沿用 Flutter 现有 `discover / forum / docs / profile` 的原生列表和返回范式。

建议范围：

- 在 Flutter 增加只读榜单入口，优先从 `discover` 打开。
- 首批只覆盖公开经验榜或统一公开榜单列表中的一个稳定类型。
- 支持加载、空态、错误态、刷新和返回来源。
- 不做“我的排名”、购买跳转、商品详情、订单、背包或登录态增强。

建议验证：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

实施结论：

- 已新增 Flutter `features/leaderboard` 数据模型、仓储和只读页面。
- 已将壳层导航调整为 `发现 / 论坛 / 文档 / 榜单 / 我的`，并从发现页提供 `打开榜单` 入口。
- 已复用 `/api/v1/Leaderboard/GetLeaderboard?type=1&pageIndex=1&pageSize=20`，不改变后端 API、权限或经验规则。
- 已覆盖榜单页面单测、发现页回调测试、壳层返回 smoke，并补齐论坛测试 fake `publicId` 以对齐当前公开路径契约。
- 已通过 `flutter test` 与 `flutter analyze`。

### 下一顺位：`P3-8-B2 Console 治理工作台设计端点`

理由：

- `ModerationPage.tsx` 与 `ExperienceAdminPage.tsx` 仍是 Console 侧最明显的治理大页热区。
- 这些页面不是简单拆文件问题，信息密度、筛选、表格、动作区和人工复核路径需要先统一设计端点。

建议范围：

- 创建或更新 `console-governance-workbench.pen`。
- 只覆盖内容治理与经验治理两类工作台的通用布局、筛选区、队列表格、详情 / 动作区和日志回看区。
- 暂不改代码；设计完成后再选择一个页面做一天级实现。

建议验证：

```bash
npm run check:repo-hygiene:changed
git diff --check
```

## 当前不选

- 不把 Tauri 签名、自动更新、托盘、菜单拉回当前主线。
- 不启动完整移动商城、完整通知中心、聊天或创作器。
- 不启动公开 Web 整体 UI 重构。
- 不继续硬拆 `ExperienceService` 经验发放主流程。
- 不把完整 E2E、完整可观测性平台或完整运营后台作为继续开发前置。
