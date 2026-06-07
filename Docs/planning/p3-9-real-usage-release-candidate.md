# P3-9 真实使用主路径产品化与发布候选整备

> 状态：`进行中`
>
> 启动日期：2026-06-07（Asia/Shanghai）
>
> 本页承载 Phase 3 内 `P3-9` 的产品化、发布候选验收和定向治理口径。快速入口仍以 [当前进行中](/planning/current) 为准。

## 背景判断

`P3-8-D` 已围绕纯 Web、移动 Web、Flutter 与 Console 做了多轮主路径缺口补齐，覆盖公开访问、登录回流、购买 / 订单 / 背包、胡萝卜流水、权限授权和 LongId 字符串契约。继续把这些路径作为默认日常主线反复深挖，收益已经下降。

项目还没有进入稳定运营期，也不适合直接切到 Phase 4。当前更需要把已经打通的能力组织成少量真实用户可以连续完成的产品路径，并用发布候选口径做批次级验收、成组修复和收口结论。

## 阶段目标

1. **形成可体验的真实使用路径**
   - 访客可以发现、阅读、分享公开内容，并从公开页进入合适的登录或工作台入口。
   - 登录移动用户可以在 Flutter 中完成常用社区、内容、商城、通知和个人资料任务。
   - 管理员可以在 Console 中围绕用户、商品、订单、胡萝卜流水、权限授权和治理动作定位问题。
2. **把验收从局部缺口转为路径级结论**
   - 每个批次按用户路径列出入口、动作、预期状态、可接受失败态和验证证据。
   - 只修真实阻断、状态恢复、身份契约、公开链接、跨端回流和治理效率缺口。
   - 若批次未暴露问题，应输出收口结论，不继续扩大范围。
3. **为后续 Phase 4 准备发布候选基础**
   - 本阶段不负责完整运维平台、完整 E2E 平台或大规模发布流程。
   - 本阶段应沉淀发布候选前需要人工复核的路径、必要自动化守护和风险清单。

## 首批用户路径

### 访客公开访问

- 从 `/` 进入 `/discover`。
- 浏览 forum / docs / shop / leaderboard / `/u/:id`。
- 打开公开详情，复制分享链接，使用浏览器返回或显式返回回到来源。
- 从公开商品或公开帖子进入 `/desktop` 保留工作台入口，匿名态登录后仍回到原上下文。

验收重点：

- 公开 URL 不泄漏内部 LongId，PublicId 试点路径优先生效。
- canonical、OpenGraph、JSON-LD 与可见公开链接一致。
- 移动窄屏不出现横向撑开。
- 登录回流不把用户带回 `/discover` 或丢失原详情上下文。

### 登录移动用户

- 从 Flutter 发现页进入榜单、公开主页、论坛、文档和商城。
- 发布纯文本帖子、评论或回复，失败时保留输入状态。
- 从商品详情购买 1 件商品，成功后进入订单详情，再查看背包发放和胡萝卜流水。
- 查看通知、最近访问、胡萝卜资产、经验记录和个人资料，并能返回原入口。

验收重点：

- 同一工作流的登录恢复、错误态、刷新态和返回态完整。
- 商品、订单、背包、流水、用户等外部 ID 始终按字符串契约传递。
- 写操作只覆盖成熟 API 支撑的同一工作流，不扩展完整商城、通知中心、资产中心或创作者中心。

### Console 管理员

- 从用户详情查看最近订单和最近胡萝卜流水。
- 从订单详情定位商品、用户、扣款流水和 `BusinessType=Order / BusinessId=OrderId`。
- 从商品详情进入相关订单，再返回商品或原排障上下文。
- 在角色授权页查看资源树、接口预览并保存权限。

验收重点：

- `returnTo` 只接受合法同源相对路径。
- `userId`、`roleId`、`resourceId`、`apiModuleId`、`productId`、`orderId`、`businessId` 全程保持字符串。
- 加载中、保存中和无权限状态不能触发破坏性操作。
- 排障跳转要保留足够上下文，避免管理员迷失在列表筛选和详情弹窗之间。

## 当前门禁

可以放宽：

- Flutter 不再限制为只读；同一工作流内的一组成熟 API 动作可以进入受控写入批次。
- 小范围功能、状态、表单、文案或行为等价修正不被 Pencil 前置阻塞。
- 开发中本地连续提交按风险选择定向测试，不要求每次跑完整 baseline。

必须保留：

- 大页面重设计、端点级视觉治理和跨页面视觉体系变更原则上先更新 Pencil。
- 外部 LongId、PublicId、登录回流和 Console returnTo 契约必须继续强守护。
- PR 合并到 `master` 前必须按 [验证基线说明](/guide/validation-baseline) 扩大验证和留痕。

## 当前不做

- 不直接进入 Phase 4 稳定运营或大规模发布运维。
- 不继续把 P3-8-D 购买 / 订单 / 背包、权限授权或 ID 守护作为无限期默认主线。
- 不恢复移动 Web 公开页逐页打磨或 Console 剩余页面默认迁移。
- 不一次性启动完整移动商城、完整通知中心、完整资产中心、完整创作器或完整浏览历史治理。
- 不启动完整 `PublicId` 全量迁移、数据库主键迁移、ActivityPub / WebFinger 实现。
- 不启动完整 Playwright / E2E 平台或完整可观测性平台作为继续开发前置。
- 不让 WebOS / Tauri 抢占纯 Web + Flutter 主线。

## 首批开发批次

1. `P3-9-A 真实使用路径验收入口`
   - 建立路径矩阵、复核入口和定向守护。
   - 优先使用现有 helper、路由状态和单元测试结构。
   - 不引入完整 E2E 平台。
2. `P3-9-B 登录移动用户主路径整备`
   - 以 Flutter 登录后常用任务为中心做路径验收。
   - 只补主路径阻断、状态恢复和身份契约缺口。
3. `P3-9-C 访客公开访问与分享整备`
   - 按发布候选口径复核公开访问、分享预览、移动窄屏和登录回流。
4. `P3-9-D Console 排障与治理入口整备`
   - 按管理员任务复核用户、订单、商品、流水、权限授权和治理动作。

## 验证口径

文档与验收入口批次默认运行：

```bash
npm run check:repo-hygiene:changed
git diff --check
```

触达前端代码时按 workspace 运行：

```bash
npm run test --workspace=radish.client
npm run type-check --workspace=radish.client
npm run test --workspace=radish.console
npm run type-check --workspace=radish.console
npm run lint:changed
npm run validate:identity
```

触达 Flutter 时按影响范围运行：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

发布候选或 PR 合并到 `master` 前，再按 [验证基线说明](/guide/validation-baseline) 选择 `validate:baseline:quick`、`validate:baseline` 或 `validate:baseline:host`。
