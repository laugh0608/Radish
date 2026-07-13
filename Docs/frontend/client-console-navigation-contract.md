# Client 与 Console 跨应用导航契约

> 状态：首批契约已实施
> 最后更新：2026-07-13（Asia/Shanghai）
> 适用范围：`radish.client`、`radish.console`、Gateway Web 入口，以及 Flutter / Tauri 的条件式外部承接

## 摘要

Radish 的产品端与治理端保持两个独立 SPA，通过 Gateway 同一公开入口下的真实 URL 联动：

- `radish.client` 承担公开内容、登录态私域、作者态和普通用户任务；
- `radish.console` 承担治理、权限、审计、运营和高风险管理动作；
- 两端共享 Radish.Auth SSO，但分别使用 `radish-client` 与 `radish-console` OIDC client，不传递或复用对方 Token；
- 跨应用只传导航意图、对象标识和受控返回路径，不传运行时对象、表单状态、用户快照或认证凭据；
- 不使用 iframe、微前端容器或 `postMessage` 把 Console 嵌入产品端。

首批实现固定使用 `backTo` 表达从 Console 返回产品端的路径；Console 已有的 `returnTo` 继续只表达 Console 内部页面返回，二者不得混用。

## 一、职责边界

| 入口 | 正式职责 | 导航壳层 | 当前状态 |
| --- | --- | --- | --- |
| `/discover`、`/forum`、`/me`、`/workbench` 等 | 产品浏览、互动、复访和作者任务 | `radish.client` Web shell | 正式主线 |
| `/console/` | 后台治理、权限、审计、运营和排障 | `radish.console` Console shell | 正式治理端 |
| `/desktop` | WebOS 历史多窗口工作台 | `radish.client` WebOS shell | 兼容维护 |
| Flutter | 已落地 Android MVP 的原生任务 | Flutter 原生导航 | 条件维护 |
| Tauri | 系统窗口和桌面增强实验 | 系统壳 + Web UI | 冻结 |

Console 不嵌入 WebOS 窗口，也不把 Console 侧栏、表格密度或高风险动作迁入产品端。产品端可以提供“进入治理工作台”入口，但必须由 Console 自身再次执行认证和权限守卫。

## 二、Web 拓扑与认证

生产和 Gateway 联调入口固定为：

```text
https://<public-origin>/...          -> radish.client
https://<public-origin>/console/... -> radish.console
https://<public-origin>/connect/... -> Radish.Auth
```

本地 Vite 直连仅用于前端调试：

```text
http://localhost:3000              -> radish.client
http://localhost:3100/console/     -> radish.console
```

认证规则：

1. `radish.client` 使用 `radish-client` 完成 Authorization Code + PKCE。
2. `radish.console` 使用 `radish-console` 完成独立 Authorization Code + PKCE。
3. Auth 登录会话提供 SSO；进入 Console 时允许发生一次授权重定向，但不得复制 `radish.client` Token。
4. 两端 Token 存储键必须保持独立；跨应用 URL、`history.state`、`postMessage` 和日志中禁止携带 Token。
5. Console 权限仍由 `canEnterConsole` 与页面级权限守卫决定，产品端只控制入口可见性，不能代替服务端和 Console 授权。

## 三、跨应用 URL 契约

### 3.1 真实 URL

所有跨应用动作必须提供真实 `href`：

- 普通点击可以进入当前标签页；
- Cmd / Ctrl 点击、中键、新开标签和复制链接仍能表达同一业务目标；
- 禁止使用只有 `onClick`、没有 `href` 的按钮承担跨应用导航；
- 禁止使用 hash shell 代替产品公开 canonical URL。

### 3.2 Console 内部 `returnTo`

`returnTo` 只用于 Console 内部页面之间的来源返回，例如：

```text
/console/orders?...&returnTo=/users/2042219067430928385
```

Console Router 可以使用 `navigate(returnTo)` 处理该字段。它不得指向产品端路径，也不得被解释为跨应用返回。

### 3.3 跨应用 `backTo`

`backTo` 用于记录进入 Console 前的产品端来源，例如：

```text
/console/?backTo=/workbench
/console/moderation?section=queue&sourceReportId=2042219067430928385&backTo=/forum/post/pst_...
/console/orders?orderId=2042219067430928386&openDetail=1&backTo=/shop/order/2042219067430928386
/console/products?productId=2042219067430928387&openDetail=1&backTo=/shop/product/2042219067430928387
```

规则：

- 只接受单个站内绝对路径；
- 拒绝空值、外部 URL、协议相对 URL、反斜杠和控制字符；
- 拒绝 `/console`、`/oidc/callback`、认证端点和 API 路径；
- 允许的产品端路径前缀固定为 `/discover`、`/forum`、`/docs`、`/u`、`/leaderboard`、`/shop`、`/messages`、`/notifications`、`/circle`、`/me`、`/pet`、`/workbench` 与兼容入口 `/desktop`；
- 查询参数和 hash 可以保留，但不得包含 Token、authorization code 或其他凭据；
- LongId 必须保持字符串，公开 forum / user 等已有 PublicId 的路径继续优先使用 PublicId。

Console 使用普通 `<a href>` 或 `window.location.assign()` 返回 `backTo`，不能交给 basename 为 `/console/` 的 React Router `navigate()`。

### 3.4 OIDC 往返保持

首次进入 Console 可能经过 `/console/login -> Auth -> /console/callback`。为避免授权往返丢失产品来源：

- Console 在 Router 处理前读取并校验 `backTo`；
- 合法值写入当前标签页的 `sessionStorage`；
- OIDC 回调完成后从同一标签页恢复；
- 点击“返回社区”后清理；
- 新标签页不继承其他标签页的来源上下文；
- 不使用 `localStorage` 长期保存跨应用来源。

## 四、导航行为

### 4.1 纯 Web

- `/workbench` 与产品页面中的 Console 入口默认同标签打开。
- 浏览器后退保持可用；用户可自行新开标签。
- Console 页头固定提供“返回社区”入口：存在 `backTo` 时返回精确来源，否则返回 `/workbench`。
- Console 对产品对象的“查看现场”使用产品端 canonical URL，不复制一套只读详情。
- 页面存在未保存表单时才执行离开确认；普通列表、详情和只读页面不拦截跨应用返回。

### 4.2 WebOS `/desktop`

- Console 继续作为 external app 使用新标签打开，不嵌入 WebOS 窗口。
- `window.open` 必须使用 `noopener / noreferrer`，禁止外部页面获得 `window.opener`。
- 新标签 URL仍使用 `/console/?backTo=/desktop`，让 Console 可显式返回历史工作台。

### 4.3 Console 回看产品现场

- forum 帖子与评论：`/forum/post/:publicId`，评论定位使用受控查询参数；
- 用户：`/u/:publicId`；
- 商品：`/shop/product/:productId`；
- Docs：`/docs/:slug`；
- 聊天：优先 `/messages?channelId=...&messageId=...`，不再新增 `/desktop?app=chat...`；
- 订单、背包和个人资产使用正式私域 Web 路径。

这些 URL 是产品端真相源。Console 可以在自己的工作台显示必要证据快照，但不能把证据快照当作公开页面替代品。

## 五、UI 过渡与视觉衔接

### 5.1 同一 SPA 内

- 使用各自 Router 导航和既有 loading / error / permission 状态槽；
- 只做轻量淡入、位移或骨架，不对整张表格和大面积布局做持续动画；
- 保留列表筛选、分页、滚动和来源返回状态；
- 尊重 `prefers-reduced-motion`。

### 5.2 跨 SPA

跨应用导航属于真实文档切换，不伪装成同一 SPA：

1. 点击后当前链接进入短暂 pending 状态，文案可变为“正在进入治理工作台…”；
2. 防止同一动作被连续触发，但不阻止用户复制链接或新开标签；
3. Console 首屏先显示稳定壳层、认证或权限校验状态；
4. 两端共享基础品牌色、字体节奏和轻背景，降低视觉跳变；
5. Console 保持治理端密度，不同步 `radish.client` 的 `guofeng` 主题。

首批不把跨文档动画设为正确性的依赖。浏览器不支持增强动画时必须直接完成普通 URL 导航。

## 六、Flutter 与 Tauri

### 6.1 Flutter

- Flutter 打开 Console 时使用系统浏览器，不嵌入 WebView；
- 不传递 Flutter Token，由系统浏览器中的 Auth SSO 完成 Console 授权；
- Console 返回产品内容时继续使用标准 Web URL；
- Flutter 已支持的 Web 路径可以在未来映射为原生路由，不支持的路径保留浏览器 fallback；
- 当前条件维护阶段不新增完整 Console、原生治理页或 iOS 专项联动。

### 6.2 Tauri

- 当前保持冻结，不因本专题解冻；
- 若未来重启，Console 默认交给系统浏览器；
- Tauri deep link 只承接已登记的产品任务，不承接 Console Token 或后台运行时状态。

## 七、首批已实施范围

1. 新增 `backTo` 校验、当前标签页保存和清理工具。
2. `/workbench` Console 入口携带 `backTo=/workbench`。
3. WebOS external Console 入口携带 `backTo=/desktop` 并补 `noopener / noreferrer`。
4. Console `AdminLayout` 增加“返回社区”入口和跨应用 pending 状态。
5. Console 既有 forum / shop / chat 回看链接对齐正式产品路径，优先清理仍指向 `/desktop` 的聊天链接。
6. 保留订单、商品、用户、治理页既有 Console 内部 `returnTo`，不在本批重写内部导航。
7. 补合法 / 非法 `backTo`、OIDC 往返保持、字符串 ID、真实 `href`、外部窗口安全和移动布局测试。

当前静态验收已覆盖 client / Console 的测试、type-check、lint 与生产构建。由于本批未在当前任务中启动前后端，Gateway 下的 PC / mobile 双向跳转与 OIDC 真实浏览器往返留到成组运行态验收，不把历史服务状态当作本批证据。

## 八、停止线

- 不合并 `radish.client` 与 `radish.console` bundle；
- 不新增微前端框架或跨应用事件总线；
- 不使用 iframe 嵌入 Console；
- 不共享或复制 Token；
- 不同步两端完整主题状态；
- 不扩 Flutter 完整后台，不解冻 Tauri；
- 不在本专题重做 Console 页面、WebOS 工作台或全局导航视觉。

## 九、验证口径

- `radish.client` 与 `radish.console` 定向测试、type-check、lint 和生产构建；
- `backTo` 开放重定向与凭据参数拒绝测试；
- Console 无 Token时的 OIDC 跳转前后来源恢复测试；
- Workbench、WebOS external、Console 页头与 Console 回看链接静态契约测试；
- changed / staged repo hygiene 与 `git diff --check`；
- 只有专题准备验收且服务在当前任务中已确认启动时，才执行 Gateway PC / mobile 浏览器 smoke。
