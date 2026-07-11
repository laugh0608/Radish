# P3-12-D65 UI 专题候选前验证执行

> 日期：2026-07-05（Asia/Shanghai）
> 范围：`P3-12-D` UI 专题候选前启动无关验证、公开页静态契约回归修复
> 前置：`P3-12-D64` 已整理 D61-D63 重新实现后的证据、剩余限制与 D65 验证清单

## 背景

`P3-12-D64` 已确认 Public Web、Private / Author 与 Console 当前发布前范围完成首批实现和必要复核。D65 按候选前验证清单刷新启动无关验证，不直接进入 `P3-12-E`，也不把未执行的 Gateway PC / mobile 页面联调写成通过。

本轮用户未在当轮确认前后端已启动，因此只执行不依赖运行中宿主的验证：CI 契约、baseline、identity、host baseline、构建和仓库静态检查。真实 `check:host-runtime` 与 Gateway 页面 smoke 顺延到后续运行态补验批次。

## 验证暴露问题

首次执行 `npm run validate:ci -- --report` 时，`Baseline Quick` 命中 `Frontend/radish.client/tests/publicSeoStatic.test.ts` 的公开页契约回归：

- 公开个人页样式缺少 `summaryBackLink` 来源返回样式锚点。
- 公开商城详情购买 / 返回链接的静态断言仍读取旧组件文件，未跟随前次 `PublicShopViews.tsx` 拆分。
- 公开个人页重新出现 `@/api/userFollow`、关注写操作和内部 `voUserId` 依赖，违反公开个人页只读边界。
- 公开发现页账号动作边界文案与静态契约不一致。

## 修复范围

- `PublicProfileApp` 移除关注写入 API、登录态用户依赖、关注状态加载、关注按钮、关注错误提示和内部用户 ID 依赖。
- 公开个人页内容查询继续使用公开路由标识，保持 PublicId 优先与 LongId 兼容读取边界。
- `PublicProfileApp.module.css` 补回 `.summaryBackLink`，并移除不再使用的关注反馈样式。
- `publicSeoStatic.test.ts` 更新商城详情断言读取位置，继续检查真实 `href` 存在，而不是绑定旧文件结构。
- `i18n.ts` 收敛公开发现页账号动作边界文案，避免把订单、背包等私域动作误表达为公开入口能力。

## 保持不变

- 不新增 API、权限键、数据库结构、路由语义或保存 / 提交载荷。
- 不改变公开个人页 PublicId / LongId 兼容读取规则、公开来源返回、tab / 分页路由或公开壳层导航拦截。
- 不改变公开商城购买回流、订单 / 背包正式 Web 路由或商品详情组件业务逻辑。
- 不把关注写操作放回公开个人页；关注关系仍以登录态私域 / 圈子入口承接。

## 验证结果

- `node --test --test-isolation=none ./tests/publicSeoStatic.test.ts`：通过，23 项通过。
- `npm run validate:ci -- --report`：首次失败后修复；重跑通过，`Overall: passed`，`NextStage: ready-for-pr-batch-record`。
- `npm run validate:baseline`：通过；client node 测试 295 项通过，后端测试 551 项通过。
- `npm run validate:identity`：通过；身份语义扫描、LongId 安全扫描和 15 项身份定向测试通过。
- `npm run build --workspace=radish.client`：通过。
- `npm run validate:baseline:host -- --report`：通过；baseline、DbMigrate `doctor` 和 `verify` 只读自检均通过，报告建议宿主启动后继续 `check:host-runtime`。
- `npm run lint:changed`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

## 未执行项

- 未执行 `npm run check:host-runtime -- --details --report`。
- 未执行 Gateway public / private / console PC、mobile 成组真实页面 smoke。

原因：按项目协作规则，真实 smoke / 浏览器联调和宿主运行态检查必须在执行前由用户当轮明确说明前后端已启动。本轮只处理启动无关验证。

## 后续

- 下一顺位进入 `P3-12-D66 UI 专题候选前运行态验证补验`。
- D66 需要在用户当轮确认前后端已启动后，执行 `check:host-runtime -- --details --report` 与 Gateway public / private / console PC、mobile 成组页面复核。
- D66 通过后再判断是否进入 `P3-12-E`；当前不创建 tag，不进入 M15 测试或生产部署流程。
