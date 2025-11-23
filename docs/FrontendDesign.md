# 前端设计文档

> 本文聚焦 radish.client 及未来移动端/原生体验的统一设计与实现方案，面向前端与全栈开发者。结合桌面化 UI 目标、小众论坛/圈子/商城场景以及未来 React Native 应用，将交互规范、技术栈、目录结构、状态管理与迭代计划集中说明。

## 1. 设计目标

1. **沉浸式体验**：Web 端采用桌面化（Desktop Shell）交互，让 PC 用户获得接近操作系统的体验；移动端保持轻量但动效一致。
2. **统一视觉与交互语言**：颜色、字体、间距、阴影、动效、图标尺寸由 Design Token 统一管理，PC/移动 Web/RN 共享。
3. **跨端复用**：业务逻辑、数据模型、API Hook 尽量共享，后续 React Native/Expo 应用只需重写 UI 层。
4. **渐进式迭代**：先保证 Web 端 MVP 可用，再逐步增强移动适配与原生 App，避免一次性重构。

## 2. 视图层规划

### 2.1 桌面模式（PC Web）

- `AppDesktopShell` 负责渲染顶部状态栏、左侧桌面图标、底部 Dock 与窗口管理器。
- 窗口（`DesktopWindow`）具备最小化/关闭、拖拽、缩放、Dock 绑定图标、层级管理、快捷键（⌘/Ctrl + W/M）。
- Dock 使用“磁贴+指示点”展示运行状态，双击桌面或 Dock 打开窗口，长按弹出快捷菜单。
- 登录后需在状态栏显示用户信息/IP、消息提示、全局搜索入口。
- 参考实现 `public/webos.html`，React 组件需复刻其中的动效与布局。

### 2.2 响应式与移动 Web

- 桌面 Shell 在宽度 < 1024px 时自动切换到“移动模式”：顶部保留状态栏，Dock 转为底部 Tab，桌面图标改为列表/网格。
- 使用 CSS Container Queries + Tailwind/Uno 原子类实现断点控制；移动模式允许隐藏复杂背景并简化阴影/模糊。
- 手势：移动端窗口以全屏卡片呈现，采用滑入/滑出动画；侧滑返回、下拉关闭等交互通过 `framer-motion` 或 `react-spring` 实现。

### 2.3 React Native / Expo 规划

- 目标：提供原生级页面切换与交互流畅度，重点优化圈子浏览、帖子详情、商城下单。
- 方案：使用 Expo Router + React Native Reanimated 3 + Gesture Handler，拆分为 `app/(tabs)`、`app/(modals)` 等路由段。
- 组件命名与 Web 保持一致（如 `AppShell`, `DesktopWindow` 在 RN 中映射为 `AppMobileShell`, `AppCardPanel`），内部实现使用 RN 组件。
- 共享包：在仓库根目录创建 `packages/ui`, `packages/hooks`, `packages/theme`，通过 pnpm workspace 或 turbo repo 管理，Web 与 RN 均可复用。

## 3. 技术栈与结构

| 层级 | 选型/说明 |
| --- | --- |
| 构建 | React 19 + Vite（Rolldown），ESLint 9，TypeScript 5.5+ |
| 状态/数据 | TanStack Query + Zustand（瞬时 UI 状态）+ React Context（主题/会话），未来 RN 复用 |
| 路由 | TanStack Router（Web），Expo Router（RN），保持路由表结构一致 |
| UI | TailwindCSS/UnoCSS + 自研组件；动效使用 Framer Motion（Web）/Reanimated（RN） |
| API | `@/shared/api/client.ts`（封装 axios/fetch），自动附带 Token、TraceId、设备信息；React Query 统一缓存策略 |
| 表单 | React Hook Form + Zod |
| 国际化 | react-i18next；词条集中在 `src/i18n`，RN 复用 JSON 资源 |
| 打包 | `npm run dev/build/lint/test`，RN 使用 `expo start/build`，CI 通过 Turbo/Pnpm workspace 协调 |

### 3.1 目录结构（Web）

```
radish.client/src
├─ app/               # 入口、providers、路由
├─ features/          # 领域（posts, comments, points, shop）
├─ widgets/           # Dock、StatusBar、WindowManager 等桌面部件
├─ entities/          # 数据实体 + hooks
├─ shared/
│   ├─ api/           # HTTP 客户端、DTO 映射
│   ├─ config/        # 设计 Token、主题
│   ├─ lib/           # 工具库、加密
│   └─ ui/            # 基础组件（Button, Card, Overlay）
└─ mobile/            # 移动专用增强（可选）
```

未来 RN 目录与上述结构保持一致，放在 `apps/mobile`。

## 4. 设计系统

1. **Token**：在 `shared/config/tokens.ts` 定义颜色、字体、间距、阴影、模糊、动效时长，导出 `desktopTheme`、`mobileTheme`, `darkTheme`。
2. **标准化组件库**：
   - 目标是沉淀一套 Button/Input/Select/Checkbox/Radio/Switch/Transfer/Form 等基础组件，统一语义、尺寸、状态与动效。实现方式可为完全自研，或基于 Ant Design/Arco/Next UI 等第三方库进行“白标化”二次封装，但最外层 API、设计 Token、动效必须保持 Radish 规范。
   - 若采用第三方库，需要拆分 `@/shared/ui/primitives`（直接映射 antd 组件并覆写主题）与 `@/shared/ui/controls`（业务中使用的封装层）。封装层负责：① 统一命名与变体（`<Button variant="ghost" intent="danger" size="lg">`）；② 注入 Design Token；③ 屏蔽第三方库的命名/类前缀；④ 导出 `Form`、`Field`、`FormItem` 等组合件。
   - 组件库需配套 Storybook/VitePress 文档（`npm run storybook` 或 `docs/ui`），列出属性、交互、辅助线，并在 PR 中附录对应故事链接。所有组件要提供暗黑模式、无障碍状态（Focus ring、ARIA 标签）、移动端触控半径。
   - 表单体系默认依赖 React Hook Form + 自定义 `Form`, `FormField`, `FormItem`, `FormControl`, `FormMessage` 组合，支持动态 schema、联动校验、表单布局（水平/垂直/紧凑）。穿梭框、列表选择、树选择等复合组件要提前评估数据量并提供虚拟滚动。
   - 发布节奏：先落地 Button/Input/Select/IconButton/FormItem，随后扩展 Table、Modal、Drawer、Notification。组件需写入 `radish.client/src/shared/ui`，未来 RN 端在 `packages/ui/native` 保持相同 API。
3. **组件层级**：
   - 基础组件（Button/Input/Card/Modal）→ shared/ui。
   - 布局组件：`AppShell`, `Dock`, `DesktopGrid`, `Window`, `MobileTabBar`。
   - 业务组件：`PostList`, `CommentThread`, `ShopCarousel`。
4. **动效规范**：
   - 窗口打开：scale+opacity 210ms，最小化：向 Dock 图标缩放 180ms。
   - Tab 切换：左右滑动 250ms+Bezier。
   - RN 端采用 Reanimated 的 shared transitions，实现与 Web 近似的动效。
5. **图标体系**：统一使用 `RadishIcon`（基于 Iconify 或自建 SVG 集），尺寸 16/20/24px，Dock 图标 48px。
6. **文案与国际化**：字符串全部走 i18n，默认 zh-Hans，预留 en-US；移动端文案保持简洁避免超出。

## 5. 交互与导航

- **PC**：桌面窗口模式 + Dock + 全局搜索命令面板（⌘K）；支持多窗口并存、窗口置顶、窗口记忆位置。
- **移动 Web**：底部 Tab（圈子/探索/消息/商城/我），Tab 内再嵌 Stack；详情页支持向下拉关闭。
- **RN**：沿用移动 Web 的信息架构，但引入原生导航转场、侧滑返回、沉浸状态栏、震动反馈。
- **深链**：所有页面提供 `/app/:module/:id` 形式的深链接，对应 RN 中的动态路由，方便推广与通知跳转。

## 6. 数据与状态共享

- DTO/模型统一放在 `Radish.Model`，通过 `openapi-typescript` 或手写类型同步到前端。
- React Query 中央缓存 key 以 `[domain, params]` 组织，支持窗体级缓存与跨窗口复用。
- Zustand 用于 Dock/窗口状态、桌面背景、主题、通知等客户端状态；提供持久化（localStorage/IndexedDB）策略。
- 加密：封装 `encryptSensitivePayload(payload)`，登录/敏感操作在 Hook 内自动处理；RN 端使用 `react-native-rsa-native` 实现同样接口。

## 7. 性能策略

- Vite 分包：`vendor`, `app-shell`, `desktop-widgets`, `feature-*`；使用 `React.lazy` + Suspense 分割功能窗口。
- 图片/视频资源通过 CDN + 自适应格式（WebP/AVIF）；启动时按需加载壁纸。
- 列表使用虚拟化（TanStack Virtual / React Virtuoso / FlashList），支持骨架屏。
- 缓存：React Query + IndexedDB（`idb-keyval`）存储最近访问帖子与商城数据；移动端可做离线浏览。
- RN 端预渲染关键页面、启用 Hermes 引擎、关闭不必要的日志。

## 8. 测试与质量

- 单元/组件测试：Vitest + React Testing Library，覆盖核心组件与 Hook。
- 端到端：Playwright（桌面/移动视口），涵盖登录、发帖、商城下单；RN 端可使用 Detox。
- 可访问性：使用 Testing Library `axe` 与手动检查确保键盘导航可用；移动端遵循 WCAG 对比度。
- 性能基线：Lighthouse P95 > 85 分，首屏交互 < 2.5s（桌面），移动端 < 3s。

## 9. 构建与发布

| 任务 | 命令 | 说明 |
| --- | --- | --- |
| 开发 | `npm run dev --prefix radish.client` | Vite Dev Server，代理 API |
| 构建 | `npm run build --prefix radish.client` | 产出 `dist/`，供 Radish.Api 或静态服务器托管 |
| 预览 | `npm run preview --prefix radish.client` | 本地验证生产包 |
| RN 开发（规划） | `cd apps/mobile && npx expo start` | 触发展示 |
| RN 构建（规划） | `npx expo prebuild && expo build:*` | 生成安卓 APK/AAB |

CI 需串行执行 Web lint/test/build，再触发 RN 构建（可选）。生产部署时，Web 静态资源可挂载到 CDN 或由 Gateway/前端容器提供；RN 端通过 EAS Update 或商店发版。

## 10. 迭代路线

1. **阶段 A（M1-M4）**：完善 Web 桌面模式 + 响应式骨架；完成身份、内容、商城基本页面。
2. **阶段 B（M5-M6）**：强化桌面动效、Dock 互动、移动模式体验；抽象共享 Hook/DTO。
3. **阶段 C（M7-M8）**：接入可观测性、性能优化、PWA 可用性；准备 RN 环境（monorepo、共享包）。
4. **阶段 D（M9+）**：启动 React Native App，优先实现圈子/帖子/商城主流程；与 Gateway 引入同步，确保 API 稳定。
5. **阶段 E（持续）**：探索 Flutter/原生模块可能性，仅在需要更强渲染能力时评估。

每个阶段需在 DevelopmentLog 中记录进度、问题、设计迭代，并同步更新本文件。

## 11. 维护与协作

- 所有前端设计/交互变更必须先更新 Figma/设计稿，再同步到本文件与 `DevelopmentFramework.md`。
- 提交 PR 时附带关键页面截图（桌面 + 移动），若涉及 RN，也需要录屏或 GIF。
- 对共享包或 Design Token 的修改需在 PR 描述中列出影响面，并通知 Web/RN 负责人。
- 本文定位为“前端唯一事实来源”，其它文档只保留摘要与引用，避免重复维护。
