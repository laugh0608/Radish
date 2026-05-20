# Console 样式与 Token 使用说明

> 入口页：[前端设计文档](/frontend/design)  
> 最后更新：2026-05-20

本文说明 `radish.console` 后续新增或改动页面时的局部样式口径。当前只做小范围收敛，不启动 Console 整站视觉重构。

## 1. 当前边界

- Console 仍是独立后台入口，不嵌入 WebOS 窗口。
- `@radish/ui` 已提供按钮、表格、表单、弹窗、确认框、骨架屏、图标、Toast 等基础能力；新增页面不应再创建重复的本地基础控件。
- Console 局部样式先以 `index.css` 中的 `--console-*` CSS 变量承接主题 token，再由 `AdminLayout.css` 与 `adminFeature.css` 消费。
- 本说明只约束新增 / 明显改动页面，不要求把所有历史页面一次性改写。

## 2. Token 使用

新增 Console 样式优先使用以下局部变量：

| 变量类型 | 示例 | 用途 |
|----------|------|------|
| 背景 | `--console-bg-app`、`--console-bg-surface`、`--console-bg-muted` | 页面底色、卡片、弱背景 |
| 文本 | `--console-text-primary`、`--console-text-secondary`、`--console-text-on-dark` | 标题、正文、次要说明、深色区域文字 |
| 边框与阴影 | `--console-border-subtle`、`--console-shadow-soft`、`--console-shadow-card` | 面板边界、顶部栏、功能卡片 |
| 品牌与状态 | `--console-brand-primary`、`--console-info`、`--console-warning-*` | 主色、信息提示、警示说明 |
| 圆角 | `--console-radius-panel`、`--console-radius-control` | 页面面板、按钮 / 输入等控件容器 |

确需新增颜色时，先判断是否能映射到既有 `--theme-*` 语义 token；只有 Ant 状态色或 Console 独有治理提示确实无法复用时，才在 `index.css` 中集中新增 `--console-*` 变量。

## 3. 页面样式分层

- `index.css`：只放 Console 根级 token、全局 box model、`body` 与 `#root` 基础样式。
- `AdminLayout.css`：只放后台壳层、侧边栏、顶部栏、内容区等布局样式。
- `adminFeature.css`：承接通用功能页结构，例如功能页容器、卡片、标题区、banner、指标网格、表单栅格。
- 具体页面 CSS：只放该页面不可复用的布局或业务状态样式，避免复制 `adminFeature.css` 已有结构。

## 4. 开发规则

- 新增页面优先从 `@radish/ui` 导入已有组件和 Ant alias。
- 新增硬编码颜色前，先查 `--console-*` 和 `--theme-*` 是否已有对应语义。
- 不在 JSX 里扩散 inline 色值、阴影和圆角；需要复用时放入 CSS 变量或 `adminFeature.css`。
- 不为了统一而改动无关历史页面；只有新增页面、可见缺陷修复或明确反馈触达的页面才顺带收敛。
- Console 样式治理不改变公开内容壳层、WebOS 桌面、Tauri 壳或 `radish.client` 主题切换规则。
