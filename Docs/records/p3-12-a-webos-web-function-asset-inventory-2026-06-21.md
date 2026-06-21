# P3-12-A WebOS 与 Web 功能资产盘点记录

> 日期：2026-06-21（Asia/Shanghai）
>
> 状态：已完成只读盘点，准备进入 `P3-12-B1`
>
> 结论：`P3-12-B1` 优先推进 **账户资产与商城交易 Web 化**，随后按矩阵推进个人中心、论坛作者态 / 治理态、文档作者态和 WebOS 残留清扫。

## 盘点范围

本轮只做代码与文档只读盘点，不启动服务、不做真实页面 smoke，也不创建 PR。

已读取和核对：

- `Docs/planning/current.md`
- `Docs/planning/p3-12-web-completion-webos-retirement.md`
- `Frontend/radish.client/src/bootstrap/entryRoute.ts`
- `Frontend/radish.client/src/desktop/AppRegistry.tsx`
- `Frontend/radish.client/src/apps/*`
- `Frontend/radish.client/src/public/*`
- `Frontend/radish.client/src/me`
- `Frontend/radish.client/src/messages`
- `Frontend/radish.client/src/notifications`
- `Frontend/radish.client/src/pet`
- `Frontend/radish.client/src/circle`

未覆盖：

- 未启动 Gateway / API / Auth / Vite。
- 未执行 PC / mobile 真实页面复核。
- 未核对生产域名运行数据。

## 当前入口事实

- 浏览器根路径 `/` 已默认进入 `/discover`；`/desktop` 仍作为 WebOS 历史入口保留。
- 当前纯 Web 入口包含 `/discover`、`/forum`、`/shop`、`/leaderboard`、`/docs`、`/u/:id`、`/notifications`、`/messages`、`/me`、`/pet`、`/circle`。
- WebOS 注册应用包含 `welcome`、`showcase`、`console`、`scalar`、`document`、`forum`、`chat`、`profile`、`radish-pit`、`notification`、`leaderboard`、`experience-detail`、`shop`。
- 已发现明确 WebOS 直跳残留：
  - `/me` 的完整钱包入口仍指向 `/desktop?app=radish-pit`。
  - 公开商城商品详情购买动作仍回到桌面商城窗口。

## 分类口径

- `正式版必需`：PC / mobile Web 正式版主路径必须覆盖。
- `发布前建议`：不阻断正式版，但会影响完整体验或运营效率。
- `WebOS 保留`：只作为 `/desktop` 历史入口维护，不迁移到 Web 正式壳层。
- `后置评审`：需要独立产品、架构或风控专题，不进入 P3-12 首批。

## 功能资产矩阵

| 能力域 | WebOS 现状 | 纯 Web 现状 | 分类 | P3-12 判断 |
| --- | --- | --- | --- | --- |
| 默认入口与公开壳层 | `/desktop` 提供桌面工作台、Dock、窗口系统和应用注册表。 | `/` 已进入 `/discover`，公开壳层覆盖发现、论坛、文档、商城、榜单和公开个人页。 | 正式版必需 / WebOS 保留 | 默认产品路径已转向 Web；后续只清理与正式 Web 路径冲突的旧链接，不迁移 Dock / 窗口外壳。 |
| WebOS 形态能力 | 桌面背景、窗口几何、Dock、应用窗口管理、`welcome` / `showcase`。 | 无对应纯 Web 产品需求。 | WebOS 保留 | 保留为历史入口和开发展示，不进入正式版迁移。 |
| 公开发现与公开阅读 | WebOS 论坛、文档、商城、榜单均可作为窗口应用打开。 | `/discover`、`/forum`、`/docs`、`/shop`、`/leaderboard`、`/u/:id` 已形成公开阅读路径。 | 正式版必需（已基本具备） | 继续作为发布候选验收重点；不作为 P3-12-B1 代码主攻点。 |
| 论坛阅读、评论与轻回应 | `forum` 窗口承载详情、评论树、轻回应、实时事件和作者操作。 | 公开帖子详情已支持评论、轻回应、登录回流、typing 和来源返回。 | 正式版必需（已部分迁移） | 阅读和参与态已进入 Web；发帖、编辑、历史和作者治理仍需独立迁移。 |
| 论坛发帖、编辑、回答与历史 | `ForumApp` 内有发帖、编辑、删除、回答、评论历史、帖子历史等能力。 | 公开列表偏阅读态；未形成完整 Web 作者工作流入口。 | 正式版必需 | 排在资产 / 商城之后推进，避免正式版仍要求用户回 WebOS 发帖或维护内容。 |
| 文档阅读 | `document` 窗口承载 Wiki 树、阅读和编辑。 | `/docs` 支持公开目录、搜索、详情、Markdown 渲染和分享。 | 正式版必需（已基本具备） | 公开阅读路径可继续维护；作者态另列。 |
| 文档作者态与管理态 | `WikiApp` 支持创建、编辑、发布、撤下、归档、恢复、导入、导出和版本回滚。 | 公开 `/docs` 只覆盖阅读 / 搜索，不承载管理写入。 | 发布前建议 | 不作为用户主路径第一批，但正式版前应决定是否迁移到 Console 或 Web 管理入口。 |
| 个人公开主页 | `profile` 窗口可看用户帖子、评论和社交信息。 | `/u/:id` 已支持公开资料、帖子和评论阅读。 | 正式版必需（已部分迁移） | 公开态可继续维护；登录用户自己的完整中心仍有缺口。 |
| 我的状态与完整个人中心 | `profile` 窗口覆盖我的帖子、评论、快回复、浏览历史、附件、关注面板。 | `/me` 是状态摘要页，包含资料、成长、资产摘要、最近访问和宠物摘要。 | 正式版必需 | 需要把“我的内容 / 历史 / 附件 / 关注”拆成 Web 正式入口，避免 `/me` 长期只作为看板。 |
| 账户资产 | `radish-pit` 覆盖账户总览、转移、记录、安全、统计，包含支付口令和安全日志组件。 | `/me` 只展示余额、冻结余额、最近资产流水；完整钱包入口仍跳 `/desktop?app=radish-pit`。 | 正式版必需 | `P3-12-B1` 第一候选。先完成余额、流水和正式资产入口，再按写入风险拆转移、安全设置和统计。 |
| 商城交易 | `ShopApp` 覆盖首页、商品列表、详情、购买、订单列表、订单详情、背包 / 权益。 | `/shop` 公开浏览和详情是只读购买导流，购买仍回桌面商城窗口；订单和库存无正式 Web 路径。 | 正式版必需 | `P3-12-B1` 与资产中心同批规划：购买、订单、库存和资产余额必须脱离 WebOS。 |
| 通知中心 | `notification` 窗口承载通知中心。 | `/notifications` 已复用通知中心组件，包含登录回流、Hub 启停和 Web 目标跳转。 | 正式版必需（已迁移） | 后续以验收和未读 / 批量管理细节补漏为主，不作为第一批迁移。 |
| 消息复访 | `chat` 窗口承载完整聊天。 | `/messages` 已复用 `ChatApp`，支持会话 / 消息定位、登录回流和公开个人页跳转。 | 正式版必需（已部分迁移） | 消息复访已可用；完整聊天平台、搜索、Reaction、设备通知等继续后置评审。 |
| 电子宠物与个人圈子 | WebOS 无主要来源应用。 | `/pet`、`/circle` 已是 Web-first 页面。 | 正式版必需（已具备） | 当前只保留发布候选前回归，不回拉到 WebOS 迁移线。 |
| 经验详情 | `experience-detail` 窗口承载等级和经验流水详情。 | `/me` 有成长摘要，`/leaderboard/experience` 有排行榜；缺少完整个人经验流水页。 | 发布前建议 | 可在个人中心补全时顺带设计，但不阻断 B1。 |
| Console 与 Scalar | `console`、`scalar` 作为桌面快捷入口。 | Console 已有 `/console/` 正式路径； Scalar 可通过 Gateway 访问。 | 正式版必需 / WebOS 保留 | Console 正式路径继续维护；WebOS 快捷方式保留，不作为迁移对象。 |
| Flutter 与 Tauri | Flutter / Tauri 不属于 WebOS 应用迁移来源。 | Flutter 暂后移；Tauri 后置评估。 | 后置评审 | P3-12 优先保证 PC / mobile Web 正式版主路径，再决定移动原生承接。 |

## P3-12-B 首批建议

### `P3-12-B1` 账户资产与商城交易 Web 化

推荐第一批原因：

- 已存在直接阻断正式版口径的 `/desktop` 回跳：`/me` 完整钱包、公开商城购买。
- 商城购买、订单、库存、权益发放和资产流水是正式版用户真实交易路径，不能长期依赖 WebOS 窗口。
- 前置治理已完成支付 / 转账幂等、背包 / 权益发放可靠性和 Flutter 单商品购买幂等承接，具备迁移基础。

建议范围：

- 设计正式 Web 资产入口，承接余额、冻结余额、近期流水、完整流水列表和资产状态。
- 设计正式 Web 商城交易入口，承接购买、订单列表、订单详情、库存 / 权益和购买后的资产 / 库存回流。
- 将 `/me` 完整钱包入口和公开商城购买入口从 `/desktop` 回跳切到正式 Web 路径。
- 转移、支付口令、安全日志和统计按写入风险拆分，先在设计 / 说明文档中确认边界，再决定是否并入同一代码批次。
- 不新增经济玩法、不扩展商品体系、不改变既有幂等与发放真值。

### 后续建议顺序

1. `P3-12-B2`：完整个人中心 Web 化，覆盖我的内容、浏览历史、附件、关注面板和经验详情补齐。
2. `P3-12-B3`：论坛作者态 Web 化，覆盖发帖、编辑、回答、历史查看和作者反馈。
3. `P3-12-B4`：文档作者态归属裁决，决定迁移到 Console、Web 管理页，或继续 WebOS 历史维护。
4. `P3-12-C1`：清理 B1 直接相关的 WebOS 残留入口与文案，不做无替代路径的大删。

## UI 与 Pencil 约束

- `P3-12-B1` 若新增页面级资产 / 商城交易视图，进入实现前应先明确功能说明、路由边界和响应式信息架构。
- 页面级 UI 设计、美化或跨页面视觉重塑必须进入 `P3-12-D`：先用 Pencil 做设计稿，再更新设计 / 说明文档，最后实现。
- 明确 bug、低风险文案、单个链接回跳替换或不改变页面结构的小修，不需要借道 UI 美化专题。

## 验证口径

本轮验证：

- `git status --short --branch`：`dev` 分支，领先 `origin/dev` 8 个提交；未发现本轮开始前工作区改动。
- 完成代码与文档只读盘点。
- 未运行构建、测试或真实页面 smoke。

进入 `P3-12-B1` 后建议验证：

- 代码批次内执行 `radish.client` 定向测试 / type-check / build。
- 涉及 HTTP 客户端或共享组件时补 `@radish/http` / `@radish/ui` 对应 type-check。
- 涉及后端交易、库存、资产流水或安全口令时执行后端定向测试与必要完整测试。
- 准备阶段验收或合并前，再在用户确认前后端已启动后执行 Gateway PC / mobile 真实页面复核。
