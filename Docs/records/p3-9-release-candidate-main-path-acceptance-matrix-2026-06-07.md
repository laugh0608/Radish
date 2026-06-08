# P3-9 发布候选主路径验收矩阵（2026-06-07）

> 本记录承接 `P3-9-A 真实使用路径验收入口`。
>
> 本批目标是把 `P3-9` 的首批真实使用路径整理成可执行验收矩阵，并标出已有自动化守护、需要人工复核的入口和缺口登记格式。本记录不要求 AI 协作者启动本地服务。

## 结论

`P3-9` 不再继续把 `P3-8-D` 的购买 / 订单 / 背包、权限授权或 ID 守护作为无限期默认主线。当前应按真实用户路径验收已经打通的能力，并只对发布候选路径中暴露的阻断、状态恢复、身份契约、公开链接、跨端回流和治理效率缺口做成组修复。

首批验收路径分为三类：

1. 访客公开访问：低门槛发现、阅读、分享和登录 / 工作台承接。
2. 登录移动用户：Flutter 中浏览、参与、购买、查看资产和资料维护。
3. Console 管理员：围绕用户、订单、商品、胡萝卜流水和权限授权做排障治理。

## 验收维度

| 维度 | 通过标准 |
| --- | --- |
| 入口可达 | 直接访问路径进入预期壳层，不误进 WebOS 或错误回调页 |
| 状态恢复 | 登录回流、刷新、浏览器返回、Android Back 或详情返回后保留原上下文 |
| 公开链接 | 可见 URL、复制链接、canonical、OpenGraph、JSON-LD 保持同一公开语义 |
| 身份契约 | 外部 LongId 不进入 JavaScript / Dart 数值精度域，PublicId 试点路径优先生效 |
| 失败态 | 网络失败、权限不足、数据不存在或提交失败时给出明确反馈并保留可恢复上下文 |
| 治理效率 | 管理员能从列表、详情、流水和用户维度定位同一笔业务，不丢筛选和返回来源 |
| 范围控制 | 不扩展完整移动商城、通知中心、资产中心、创作者中心、PublicId 全量迁移或完整 E2E 平台 |

## 路径矩阵

| 路径 | 入口 | 用户动作 | 预期状态 | 可接受失败态 | 已有自动化守护 | 人工复核 |
| --- | --- | --- | --- | --- | --- | --- |
| 访客发现公开内容 | `/`、`/discover` | 进入发现页，打开 forum / docs / shop / leaderboard / `/u/:id` | 普通浏览器根路径进入 `/discover`；公开页直接进入纯 Web 壳层 | 样本数据不足时显示明确空态或不存在提示 | `entryRoute.test.ts`、`realUsagePathContracts.test.ts`、公开路由系列测试 | Gateway `https://localhost:5000/` 与移动视口 |
| 访客阅读与分享 | `/forum/post/:id`、`/docs/:slug`、`/shop/product/:id`、`/u/:id` | 打开详情、复制链接、查看预览元信息 | 分享 URL、canonical、OpenGraph、JSON-LD 对齐；forum 优先使用 `Post.PublicId` | 无公开标识时可保留旧路径兼容，但分享预览不应把内部 ID 作为主口径 | `publicHead.test.ts`、`publicStructuredData.test.ts`、`realUsagePathContracts.test.ts` | Gateway 公开页 head smoke |
| 访客进入工作台参与 | 公开帖子或公开商品详情 | 点击参与讨论、轻回应或购买入口 | 进入 `/desktop?app=forum...` 或 `/desktop?app=shop...`；登录后保留原上下文 | 未登录时进入登录门禁，不把用户落回 `/discover` | `authReturnPath.test.ts`、`desktopEntryNavigation.test.ts`、`realUsagePathContracts.test.ts` | Gateway + Auth 登录回流 |
| 登录移动用户浏览 | Flutter 发现 / 论坛 / 文档 / 商城 / 我的 | 打开榜单、公开主页、帖子、文档、商品和最近访问 | Android Back / 页面返回回到来源；刷新态不丢当前上下文 | 数据不存在时显示明确错误，可返回上级 | Flutter 现有页面 / shell 单测 | Flutter 真机或模拟器 |
| 登录移动用户参与 | Flutter forum detail | 发布纯文本帖子、评论或回复 | 成功后刷新局部状态并进入对应详情；失败保留输入 | 接口失败、分类缺失或权限失败时不误显示成功 | `shop_product_detail_page_test.dart` 同类页面测试、论坛相关 Flutter 测试 | Flutter 登录态账号 |
| 登录移动用户购买与资产查看 | Flutter shop detail、订单、背包、胡萝卜流水 | 购买 1 件商品，进入订单详情，查看背包发放和扣款流水 | 订单、背包、流水围绕同一订单字符串 ID 串联 | 资格失败、支付口令失败、无背包发放或流水空结果时保留上下文 | Flutter shop / coin 定向测试、`validate:identity` LongId 守护 | Flutter + 有余额测试账号 |
| 登录移动用户个人任务 | Flutter 我的 | 查看通知、最近访问、胡萝卜资产、经验记录和编辑资料 | 返回“我的”后状态稳定；资料保存后跨端展示一致 | 保存失败保留表单输入；通知已读失败不阻断详情打开 | Flutter 通知 / profile / identity 相关测试 | Flutter 登录态账号 |
| Console 用户排障 | 用户详情 | 查看最近订单、胡萝卜流水和资料展示 | 用户、订单、流水上下文可互相定位；用户 ID 保持字符串 | 无订单或无流水时显示空态，不创建错误余额 | Console helper 测试、`validate:identity` | Console 管理员账号 |
| Console 订单 / 商品 / 流水排障 | 商品列表 / 订单列表 / 胡萝卜流水 | 从商品进订单，从订单进流水，再返回来源 | `returnTo` 合法同源相对路径；`businessId / orderId / productId` 保持字符串 | 非法 returnTo 被拒绝；非法 LongId 不进入筛选状态 | `orderListUrlState`、`productListUrlState`、Console URL helper 测试 | Console 管理员账号 |
| Console 权限授权 | 角色授权页 | 勾选资源树、查看接口预览并保存 | 加载中、保存中、无权限时不能修改；保存 payload 稳定排序 | 并发冲突或保存失败时保留页面上下文 | `rolePermissionHelpers.test.ts`、权限脚本检查 | Console 管理员账号 |

## 缺口登记规则

发现问题时按以下格式登记，不把低收益微调混入发布候选阻断：

| 字段 | 说明 |
| --- | --- |
| 路径 | 访客公开访问 / 登录移动用户 / Console 管理员 |
| 严重度 | `P0` 阻断主路径；`P1` 明显破坏状态或身份契约；`P2` 影响体验但可绕过；`P3` 低收益微调 |
| 复现入口 | URL、Flutter 页面或 Console 页面 |
| 期望结果 | 按本矩阵的通过标准描述 |
| 实际结果 | 具体失败、错误提示或状态丢失 |
| 处理方式 | 本批修复 / 后续回拉 / 记录为已知风险 |

## 人工复核前置

AI 协作者不直接启动服务。需要人工复核时，由用户按项目规则启动：

```bash
pwsh ./start.ps1
npm run dev --workspace=radish.client
npm run dev --workspace=radish.console
```

推荐入口：

- Gateway：`https://localhost:5000`
- Console：`https://localhost:5000/console/`
- Flutter：Android 模拟器或真机调试入口

最小样本数据：

- 至少 1 条公开帖子，含评论或轻回应。
- 至少 1 篇公开文档，含正文内链或锚点。
- 至少 1 个公开商品，且测试用户有可购买余额。
- 至少 1 笔已完成购买，能串联订单、背包和胡萝卜流水。
- 至少 1 个管理员账号和 1 个普通用户账号。

## 本批自动化守护

本批新增或继续使用的自动化守护：

- `Frontend/radish.client/tests/realUsagePathContracts.test.ts`
- `Frontend/radish.client/tests/authReturnPath.test.ts`
- `Frontend/radish.client/tests/desktopEntryNavigation.test.ts`
- `Frontend/radish.client/tests/publicHead.test.ts`
- `Frontend/radish.client/tests/publicStructuredData.test.ts`
- `npm run validate:identity`

## 下一步

1. 先补齐访客公开访问路径中可自动化的 public head / share / desktop handoff 守护。
2. 再进入 `P3-9-B 登录移动用户主路径整备`，以 Flutter 登录态连续路径为主。
3. 若 Flutter 人工复核暴露问题，按同类状态恢复、失败态或身份契约成组修复；若未暴露问题，输出收口结论并转向访客公开访问或 Console 管理员路径。
