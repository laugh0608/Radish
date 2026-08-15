# F4-R-T03 Web 四主题成组运行态验收

> 日期：2026-08-15（Asia/Shanghai）
>
> 状态：T03 已完成，Web 主题退出门禁关闭；下一顺位进入 Flutter 语义映射 readiness 审计
>
> 范围：Gateway 正式入口、四主题、双语、PC / mobile、代表页面、键盘可见焦点、reduced-motion 与真实 Theme 权益状态机

## 1. 结论

T03 已通过成组运行态矩阵，T01 主题语义与 T02 reduced-motion 的宿主级实现均在真实页面生效：

- `default / guofeng / theme-dark-night / theme-sakura` 保持各自身份，实底控件、正文、状态与反馈可读；
- Public、Private、Author、Console 与 WebOS 兼容入口覆盖 PC / mobile 代表面，没有横向溢出或关键内容遮挡；
- Client 中英文、主题切换和刷新持久化成立，Console 保持独立 Workbench Profile，不受 Client 权益主题影响；
- 键盘可见焦点具备轮廓或边框双通道，reduced-motion 下 Modal、移动对话框、Toast、加载与反馈状态保持静态可见；
- 种子 Admin 的真实 Theme 权益完成暗夜激活、樱花切换、默认主题停用与刷新回退，服务端活动指针和操作流水与页面状态一致；
- 没有发现需要修改运行时代码、接口、权限、数据模型、依赖或 Pencil 的缺陷。

至此 F4-R 的 Web 页面族、主题语义、reduced-motion 和四主题运行态退出条件全部满足。Flutter 后续只共享可维护的 Dart 语义与状态规则，不机械解析 Web CSS 或复制页面像素。

## 2. 运行环境与代表矩阵

- 启动入口：`./start.sh` 完整本地开发宿主组合；Gateway `https://localhost:5000`、API `http://localhost:5100`、Auth `http://localhost:5200`、Client `http://localhost:3000`、Console `http://localhost:3100/console/`；
- 健康检查：Gateway、API、Auth 均返回 `200`；
- 身份：匿名覆盖 Public，种子 Admin 覆盖 Client 权益与 Console OIDC；未创建临时角色或权限；
- PC：`1920 × 1080` CSS viewport；Mobile：`390 × 844` CSS viewport。

| 表面 | 主题 / Profile | 语言与视图 | 结果 |
| --- | --- | --- | --- |
| `/discover` | `default` | English / PC | 公共壳层、内容流、主题与语言切换、刷新持久化通过 |
| `/discover` | `guofeng` | 中文 / mobile | 灰玉品牌、墨蓝 action、移动内容流与主题对话框通过 |
| `/messages` | `theme-dark-night` | English / PC | Private 工作台、实底控件、正文和复制反馈通过 |
| `/docs/mine` | `theme-sakura` | 中文 / mobile | Author 列表、移动布局、搜索焦点和实底前景通过 |
| `/console/` | Workbench light | 中文 / PC、English / mobile | 固定 Console Profile、表格 / 底栏、More 对话框与焦点通过 |
| `/desktop` | `guofeng` | 中文 / PC | WebOS 历史入口可进入，Dock、图标、窗口基座无阻断回归 |

WebOS 页面加载后仍保留“正在检查系统初始化”的历史文档标题；实际桌面内容和交互入口已正常呈现，因此记录为非阻断兼容细节，不扩入本批视觉主题范围。

## 3. 可访问性与 reduced-motion

- 主题切换触发器、消息搜索和 Console 刷新动作通过键盘获得 `:focus-visible`；消息搜索同时使用品牌边框与外轮廓表达焦点，不只依赖颜色；
- 在 `prefers-reduced-motion: reduce` 下，Client 与 Console 全页面抽样只出现 `0.01ms` transition / animation 和单次迭代；
- Client mobile 主题对话框、消息复制 Toast 与 Console mobile More 对话框均保持可见、可读、可操作，没有因关闭动效而消失；
- 当前浏览器能力只能控制 CSS viewport，Mobile 实际 DPR 为 `1`，因此本记录只证明 `390 × 844` 响应式布局，不冒充物理高分屏验收。

## 4. 真实 Theme 权益状态机

本轮为种子 Admin 创建两个固定 ID、`AcceptanceTest` 来源的临时 Theme 权益，不创建订单、商品或余额变化：

| 动作 | 服务端权威结果 | 页面结果 |
| --- | --- | --- |
| 激活暗夜 | 暗夜权益激活，活动指针指向暗夜，追加 `Activate` | `/messages` 切换为 `theme-dark-night` |
| 切换樱花 | 暗夜停用、樱花激活，活动指针改指樱花，追加关联前权益的 `Activate` | `/docs/mine` 切换为 `theme-sakura` |
| 选择默认 | 樱花停用、活动指针删除，追加 `Deactivate` | 回到 `default`，刷新后保持默认 |

全过程共产生 `3` 条 `ShopEntitlementOperation`。验收后已按固定 ID 精确删除操作流水、活动指针和两个临时权益，最终三类残留计数均为 `0`。

## 5. 会话、清理与验证

- Client 与 Console 分别完成本地 OIDC 授权；验收结束后已退出 Console，并按本轮授权记录精确清理本地 OpenIddict token / authorization，残留计数为 `0`；
- Client 会话在等待 Console 授权期间超过本地既有 `5` 分钟 access token 与 `25` 分钟 refresh token 生命周期，随后按既有契约自动退出。该行为与 Auth 配置一致，不是跨应用令牌冲突或本批回归；
- `Radish.db`、`Radish.Log.db`、`Radish.Chat.db`、`Radish.Message.db`、`Radish.Hangfire.db`、`Radish.OpenIddict.db` 的 SQLite integrity check 均为 `ok`；
- 服务已停止，`5000 / 5100 / 5200 / 3000 / 3100` 均无监听；
- T01 / T02 的静态回归已覆盖 HTTP `48 / 48`、UI `32 / 32`、Client `557 / 557`、Console `138 / 138` 与 Baseline Quick；T03 在此基础上补齐真实宿主和页面矩阵，不重复制造一次全量代码回归流水。

## 6. 下一顺位

下一步先审计 Flutter 当前 `ThemeData / ThemeExtension` owner、四主题可承接范围和高价值移动路径，再提交精确实现拆批方案。审计不得以“Web 已完成”为由机械追平全部路由，也不得引入 CSS 解析、第二套主题权益状态机或 Tauri / WebOS 新功能。
