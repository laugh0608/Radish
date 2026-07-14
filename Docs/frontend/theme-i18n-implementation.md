# 前端主题与 i18n 实施说明

> 本文承载主题运行时与 i18n 的当前实施契约。视觉方向与 token 定义分别以[视觉主题规范](/frontend/visual-theme-spec)和[视觉色彩参考](/frontend/visual-color-reference)为准；阶段优先级以[当前规划](/planning/current)为准。

## 1. 当前结论

截至 `2026-07-14`，F2 已完成正式 Web、共享 UI 与商城 Theme 权益的统一运行时，并通过 PC / mobile 页面族及登录权益旅程验收。后续主题工作进入常态维护，不再继续以零散页面颜色补丁扩张主题体系。

当前固定结论：

- 纯 Web 是正式产品主线，主题入口必须覆盖 Public、Private、Author 三类正式 Web 页面；
- `radish.client` 是主题状态与产品主题注册表的所有者，`@radish/ui` 只消费宿主传入的主题配置；
- `default`、`guofeng` 是无需登录的内建主题，`theme-dark-night`、`theme-sakura` 是商城 Theme 权益资源；
- 根节点 `data-theme`、Ant Design 配置与共享组件必须由同一个主题状态驱动；
- 内建主题偏好保存在当前设备，权益主题的激活选择以服务端 `UserActiveBenefit` 为权威；
- 本轮不把 i18n 完成度治理混入主题代码，只保持现有语言能力不回退。

## 2. F2 范围与停止线

### 2.1 本阶段完成范围

1. 建立稳定主题注册表，统一资源 ID、名称、明暗模式、访问类型和 Ant Design 语义配置。
2. 在 React 挂载前恢复最近有效主题，避免首屏先显示错误主题。
3. 统一 `radish.client` 与 `@radish/ui` 的主题提供器，消除根页面与共享组件配色分叉。
4. 在正式 Web 共享 Header 提供 PC / mobile 均可访问的主题入口。
5. 登录后读取用户 Theme 权益，激活、切换、停用后立即同步主题；跨设备复访以服务端选择校正。
6. 开放服务端 Theme 商品能力，但只允许已注册的正式资源值进入上架和激活链路。
7. 成组复核 Public、Private、Author 页面族，以及 WebOS 历史入口和 Console 的兼容性。

### 2.2 本阶段不做

- 不提供用户自定义主题编辑器、主题上传或第三方主题市场；
- 不新增 `system` 主题 ID，也不把系统深浅色偏好冒充为产品主题；
- 不重构正式 Web 全局导航信息架构；
- 不推进 Flutter 或 Tauri 主题发布能力，二者仅保持现有资产可构建；
- 不借主题治理批量重写页面结构或处理与主题无关的 i18n 文案；
- 不要求 Console 跟随用户产品主题，Console 继续使用自己的稳定管理端主题。

## 3. 主题资源契约

| 资源 ID | 名称 | 模式 | 访问类型 | 说明 |
| --- | --- | --- | --- | --- |
| `default` | 清朗 | light | builtin | 中性、清晰的默认 Web 主题 |
| `guofeng` | 国风 | light | builtin | 淡雅新中式主题，保持阅读优先 |
| `theme-dark-night` | 暗夜 | dark | entitlement | 商城 Theme 权益，提供完整深色语义 token |
| `theme-sakura` | 樱花 | light | entitlement | 商城 Theme 权益，提供克制的樱花配色 |

资源 ID 是前后端稳定契约：

- 商品 `BenefitValue`、用户权益 `BenefitValue` 与客户端注册表必须使用完全相同的值；
- 服务端创建、更新、上架和激活 Theme 时必须拒绝未注册资源；
- 客户端遇到未知或已撤销资源时不得继续套用残留样式，应回退到本地内建偏好；
- 新主题进入正式商品前，必须同时具备注册表元数据、完整语义 token、共享 UI 配置、PC / mobile 页面验收和后端允许列表。

## 4. 状态与持久化

主题状态分为两层：

```text
设备内建偏好 builtinTheme
  default | guofeng

当前有效主题 effectiveTheme
  builtinTheme | active entitlement theme
```

### 4.1 首屏

- React 挂载前同步读取最近有效主题并设置根节点 `data-theme` 与 `color-scheme`；
- 本地值必须先经过注册表校验，非法值直接使用 `guofeng`；
- 首屏缓存只解决显示连续性，不替代服务端权益校验。
- 操作系统深浅色偏好不覆盖用户选中的产品主题；浏览器原生控件只跟随当前主题的 `color-scheme`，不会因此自动授予暗夜权益。

### 4.2 匿名用户

- 只能选择 `default` 或 `guofeng`；
- 切换后同时更新 `builtinTheme` 与 `effectiveTheme`；
- 权益主题在入口中可展示产品归属，但不能直接激活。

### 4.3 登录用户

- 认证建立后读取可用 Theme 权益与当前激活选择；
- 存在有效且已注册的激活权益时，以该主题覆盖设备内建偏好；
- 选择另一项已拥有主题时调用权益激活接口，同类唯一选择由服务端保证；
- 选择内建主题时，若当前存在 Theme 激活权益，必须先停用该权益，再应用内建偏好；
- 激活、停用、过期或撤销后，客户端应立即校正有效主题；
- 权益接口暂时失败时保留首屏缓存并明确记录日志，不用无意义默认值掩盖故障。

### 4.4 多标签页与登出

- 内建偏好与有效主题写入本地存储后，通过 `storage` 事件同步同源标签页；
- 登出或认证失效时清除当前用户的权益状态，回到设备内建偏好；
- 不在本地存储用户权益清单、到期时间等业务数据。

## 5. 运行时分层

```text
theme.ts
  主题注册表、ID 校验、首屏读写、根节点应用
        ↓
themeStore.ts
  builtinTheme、effectiveTheme、权益拥有/激活状态
        ↓
ThemeProvider.tsx
  认证后权益同步、跨标签页同步、@radish/ui 配置
        ↓
ThemeSwitcher.tsx / 商城背包
  用户选择与服务端激活、停用动作
        ↓
正式 Web 页面族 / WebOS / 共享 UI
  仅消费语义 token，不各自维护主题状态
```

`@radish/ui` 不读取 `localStorage`、认证状态或商城接口。它只接收宿主传入的 `ThemeConfig`，因此 Console 可以继续使用自己的默认配置，Client 则使用产品主题注册表生成的配置。

## 6. 正式 Web 入口

主题入口归属共享 `PublicShellHeader -> WebShellHeader`：

- PC 端位于页面动作区，和通知、账号入口保持同一层级；
- mobile 端保留可点击图标，不依赖被隐藏的桌面导航；
- 弹层展示当前主题、内建主题和正式权益主题状态；
- 未登录用户点击权益主题时进入登录流程，已登录但未拥有时进入商城主题分类；
- 切换动作必须有进行中状态和可见失败提示，不能静默失败；
- WebOS 现有 Dock 主题入口保留兼容，但正式产品入口以 Web Header 为准。

## 7. 页面覆盖与 token 纪律

F2 按页面族验收，不按单个组件宣称完成：

1. Public：发现、论坛、公开文档、公开主页、排行榜、公开商城、公开承诺页；
2. Private：我的、圈子、消息、通知、宠物、商城订单与背包、工作台；
3. Author：文档列表、创作、编辑和修订；
4. Compatibility：`/desktop` WebOS 历史入口、共享弹层与 Console 回归。

页面样式约束：

- 页面只能消费 `--theme-*` 或 `--rx-*` 语义 token；
- 白色、黑色等仅在确属反色文字、媒体遮罩或标准图标资源时直接使用；
- 新增状态色先进入主题 token，再进入组件；
- 深色主题必须复核文字、边框、悬浮、禁用、骨架、Markdown、代码块、图表和遮罩，不只替换页面背景；
- 不为每个主题复制整份页面 CSS，差异集中在根主题 token。

## 8. 商城开放与安全边界

- `BenefitType.Theme` 在 F2 完成资源消费面后进入可售、可激活能力矩阵；
- Theme 商品配置要求必须公开列出允许的资源 ID；
- 既有 `暗夜主题`、`樱花主题` seed 保持下架，不因能力开关变化自动上架；
- 管理员确认价格、资源、展示素材和期限后显式上架；
- 已发放的合法 Theme 权益可激活；未知资源权益保持可见、可停用，但不可重新激活；
- 服务端同类唯一选择、过期和撤销仍沿用商城权益专题的统一机制，不在主题层另建状态表。

商城履约与治理细节见[商城商品效力与权益履约专题](/features/shop-product-effect-entitlement-fulfillment)。

## 9. 验证口径

开发中至少执行：

- 主题注册表与状态转换单测；
- Theme 能力、资源校验、激活/停用服务测试；
- `@radish/ui` type-check；
- `radish.client` test、type-check 和 build；
- 后端相关测试与解决方案 build；
- `git diff --check` 和 changed repo hygiene。

专题验收时再执行：

- Gateway 下 PC `1920 × 1080` 与 mobile 单列视图的正式 Web 页面族 smoke；
- 匿名内建主题切换、刷新与多标签同步；
- 登录后 Theme 权益激活、同类切换、停用、刷新和登出回退；
- 暗夜主题下 Markdown、表单、弹层、状态反馈与移动底栏可读性；
- Console 与 `/desktop` 兼容性复核；
- `npm run validate:baseline:host` 等批次级运行态验证。

### 9.1 F2 验收结果

2026-07-14 已按本节口径完成专题验收：

- `default / guofeng / theme-dark-night / theme-sakura` 四套注册主题均由同一状态、根节点和 Ant Design 配置驱动；
- 匿名内建主题切换、刷新持久化和登录后服务端校正通过；
- 受控 Theme 权益完成暗夜激活、樱花同类切换、回到内建主题停用、刷新和跨标签同步；
- PC `1920 × 1080` 与 mobile `390 × 844` 覆盖 Public、Private、Author 代表页面，暗夜 Markdown、列表、弹层和移动底栏保持可读且无横向溢出；
- `/desktop` 历史入口正常，Console 通过构建并确认 Gateway 授权边界可达；
- 主题 seed 继续下架，未拥有主题只进入公开商城“主题”检索，不伪造可售分类或自动上架；
- 临时权益、激活指针和操作流水已精确清理，数据库完整性正常。

详细证据见 [F2 主题系统专题验收记录](/records/f2-theme-system-stage-acceptance-2026-07-14)。

## 10. i18n 当前边界

现有 i18n 已覆盖桌面壳层及商城、论坛、聊天、通知、个人中心、文档等高频链路。F2 只为新增主题入口补必要翻译键，不扩展为全站文案治理。主题专题完成后，再按规划进入独立的 i18n 完成度治理，重点复核残余硬编码文案、长文本布局、错误反馈和语言持久化一致性。
