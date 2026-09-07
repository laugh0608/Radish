# Flutter Native P5-D1 Wallet / Experience 实施就绪与方案冻结

> 状态：`P5-D1 readiness` 已完成；后续已按冻结边界实施完成
>
> 日期：2026-08-24（Asia/Shanghai）
>
> 前置记录：[P5-A 页面族拆批就绪审计](/records/f4-flutter-native-p5a-page-family-readiness-2026-08-23)、[P5-C3 Commerce Private 实现](/records/f4-flutter-native-p5c3-commerce-private-implementation-2026-08-24)

## 1. 本批结论

P5-D1 可以完全复用既有 Coin / Experience 私域读取契约和当前原生 route 完成，不需要新增后端 API、DTO、权限、数据库、migration 或移动端 BFF。Flutter 已具备当前账号余额、Coin 流水、等级概要、经验流水，以及“我的”进入两页、订单详情按 `CONSUME + Order + orderId` 打开扣款流水并返回原来源的真实 Navigator 链路。

主要缺口不在接口，而在页面状态所有权。`wallet_page.dart` 与 `experience_page.dart` 仍由 widget 字段同时拥有概要和流水，并把两项请求作为一次整批成功 / 失败提交：余额成功但流水失败、流水成功但余额失败、等级成功但经验流水失败等情况都会遮掉已成功资源；append 没有稳定去重和独立 issue，账号 identity 也仍隐含在 token 中。两页继续使用 `PhaseScopeCard` 与固定单列 Card 堆叠，没有形成 P4 Shared / Adaptive 基座下的页面级三档结构。

本批固定为只读 Wallet / Experience 页面族：当前账号的资产概要与 Coin 流水、等级概要与经验流水。P5-D1 不新增转账、打赏、退款、调账、经验调整、冻结 / 解冻、管理员复核或其他资产 / 经验写入，也不扩统计图表、交易详情、流水业务对象跳转、等级配置、排行榜或公开经验页。

本次只完成代码 / API / 导航事实审计、定向测试基线与方案冻结，没有修改 Dart、后端 API、依赖、lockfile、Pen 或平台工程，没有启动服务或执行真实 Gateway / 设备 Smoke。

## 2. 当前事实与基线

### 2.1 Owner 与规模

| Owner | 当前职责 | 当前规模 |
| --- | --- | ---: |
| `wallet_page.dart` | Wallet route、余额与流水整批读取、分页、订单筛选上下文和全部 widget | `646` 行 |
| `wallet_repository.dart` | `GetBalance` 与带可选筛选的 `GetTransactions` | `114` 行 |
| `wallet_models.dart` | 余额、Coin 流水与页模型 | `176` 行 |
| `experience_page.dart` | Experience route、等级与流水整批读取、分页和全部 widget | `562` 行 |
| `experience_repository.dart` | `GetMyExperience` 与 `GetTransactions` | `89` 行 |
| `experience_models.dart` | 等级概要、经验流水与页模型 | `225` 行 |
| `wallet_repository_test.dart` | Wallet model 解析 | `86` 行、`2` 个用例 |
| `experience_repository_test.dart` | Experience model 解析 | `89` 行、`2` 个用例 |

两个运行时页面都低于 `650` 行，但 route 生命周期、远端状态、分页与整页呈现仍集中在同一 owner。现有 repository 测试实际只覆盖 model 解析，没有覆盖 HTTP endpoint、token 与 Wallet 筛选 query；页面级状态只由 Commerce 组合回归中的一个 Wallet 空态 / stale 用例和 Shell 的一条私域路线覆盖，Experience 没有 standalone 状态或断点测试。

### 2.2 当前行为与缺口

- Wallet initial / refresh 同时启动余额和流水请求，但只有两项都成功才一次提交；任一路失败都会丢弃另一路本轮成功结果。Experience 采用同样模式。
- 同页 refresh 在已有整批快照时会保留旧内容并显示一条整页 issue，但无法表达“余额 stale、流水已更新”或“等级可用、流水首次 unavailable”。成功空流水同样只能依附于概要成功。
- load more 不重新读取概要，这是正确边界；但 append 直接 `addAll`，没有按稳定 ID 去重，也没有独立 append issue。流水刷新、首次失败与 append 失败共用字符串错误。
- 页面 request ID 能拒绝同一 widget 的旧响应，但没有显式 account target、credential generation、query target 或 dispose owner 契约。Shell 已持有 session `userId`，却只向两页传 `accessToken`；订单详情打开 Wallet 时也没有继续传递已知账号 identity。
- Wallet 的 query target 包含 `transactionType / status / businessType / businessId`。当前订单详情只传规范 LongId，但页面本身不规范筛选；filter 改变会把账号级余额也作为同一整批 target 清空重读。
- Experience 现有产品 route 只读取当前账号完整流水。服务端虽支持 expType / 日期过滤，但 Flutter repository 和页面没有该产品能力；P5-D1 不借 UI 重构扩筛选。
- Coin / Experience 流水 ID、账号 ID 与业务 ID 都应保持字符串，不进入 Dart 数值转换。`UserExperience.themeColor` 不能直接建立页面级任意颜色；P5-D1 继续消费 Theme 语义 token。
- compact / medium / expanded 当前呈现同为固定 `20px` padding 单列 Card。长交易号、参与者、业务 ID、备注、等级名和冻结原因主要依靠截断，没有页面级连续记录与宽屏信息分工。

### 2.3 改造前测试基线

执行：

```text
flutter test test/wallet_repository_test.dart test/experience_repository_test.dart test/shop_product_detail_page_test.dart test/smoke_test.dart
```

模型 `4 / 4`、Commerce / Wallet route `20 / 20`、Shell Smoke `51 / 51`，合计 `75 / 75`。现有覆盖保留“我的”—Wallet / Experience—Back、订单—扣款流水—订单详情、筛选空结果刷新失败保留上下文，以及余额 / 等级 / 流水 model 解析。

当前没有直接覆盖：四个资源的 standalone initial / empty / unavailable / stale / recover；任一概要或流水单边失败；Wallet query target、账号切换、token 续期与 disposed 迟到响应；两类 append issue 与稳定去重；HTTP endpoint / query contract；`599 / 600 / 1024 / 1280` 页面结构、四主题同构和长内容无横向溢出。

## 3. 既有 API 与 target mapping

P5-D1 只消费以下既有契约：

| 既有能力 | P5-D1 用途 | 裁决 |
| --- | --- | --- |
| `GET Coin/GetBalance` | 当前账号资产概要 | 独立权威快照；失败不遮掉 Coin 流水 |
| `GET Coin/GetTransactions` | 当前账号 Coin 流水 | 保持经典页码与既有 transaction / status / business 筛选；不扩交易详情、统计或写入 |
| `GET Experience/GetMyExperience` | 当前账号等级、进度、排名与冻结只读状态 | 独立权威快照；不扩等级配置、公开经验或治理动作 |
| `GET Experience/GetTransactions` | 当前账号经验流水 | 保持现有无筛选产品 route 与经典页码；不在本批扩 expType / 日期 UI |
| Shell Profile actions | 登录后打开 Wallet / Experience 并返回“我的” | 继续真实 Navigator 栈；Shell 的 session `userId` 作为 account identity |
| Order Detail Wallet handoff | 按 `CONSUME + Order + orderId` 核对扣款流水 | 保留 title / description / return label 与规范 LongId；返回订单详情 |

`accessToken` 只作为请求凭据，不作为长期账号身份。实施时 `WalletPage` 与 `ExperiencePage` 增加可选 `accountId`，Shell 传 authenticated session `userId`，订单详情继续传递自身已知 account target；只有无 session owner 的 repository / widget 测试入口才回退到标准化 token。账号变化必须立即清空旧私域快照；同账号 token 续期更新凭据并重新读取，但不改变 account identity。

Wallet 流水 query target 由标准化 transactionType、status、businessType 与 businessId 组成。业务 ID 存在时必须先通过正整数 LongId 字符串规范化，异常 target 不发起请求；query 改变只重置流水，不把账号级余额错误归入筛选 target。

## 4. 权威状态与 owner 冻结

### 4.1 Wallet

- `WalletBalanceController` 独立维护 account、credential generation、余额快照、initial / refresh busy 与结构化 issue。首次失败为 unavailable；同账号刷新失败保留旧余额并进入 stale。
- `WalletTransactionController` 独立维护 account + query target、页快照、dataCount / pageCount、initial / refresh / append busy 与三个结构化 issue。refresh 以第一页权威替换，append 保留旧流水并按非空字符串 ID 稳定去重。
- 成功的空流水是权威快照；刷新失败保留空结论并显示 stale，不退回首次 unavailable。余额失败不能遮掉流水，流水失败也不能遮掉余额。
- filter 改变只清空流水 target；account、repository 或 owner dispose 使旧响应失效。credential 变化在同账号内重新读取，不把旧账号内容交给新账号。

### 4.2 Experience

- `ExperienceSummaryController` 独立维护 account、credential generation、等级概要快照、initial / refresh busy 与结构化 issue。
- `ExperienceTransactionController` 独立维护 account、页快照、initial / refresh / append、稳定去重与结构化 issue。
- 等级概要与经验流水分别提交 available / empty / unavailable / stale；任一路失败不遮掉另一路成功，成功空流水刷新失败仍保留空快照。
- account、repository、generation 或 dispose 变化拒绝旧响应；不为两域引入跨页面全局 store。

四个 owner 保持领域命名和独立实现，不建立 Wallet / Experience 万能分页基类或跨域状态机。两类流水虽有相似分页规则，但 query、格式、符号、业务语义和后续演进不同；当前复用收益不足以抵消跨域耦合。

## 5. 页面、结构与测试拆分

### 5.1 Owner 拆分

实施顺序固定为先补 owner 测试，再拆运行时：

| Owner | 目标职责 |
| --- | --- |
| `wallet_page.dart` | route 生命周期、account / query 编排、刷新与来源返回 |
| `wallet_balance_controller.dart` | 余额独立快照与请求隔离 |
| `wallet_transaction_controller.dart` | Coin 流水 query、分页、去重与局部 issue |
| `wallet_surface.dart` | 页面头、余额状态、连续流水和三档结构 |
| `experience_page.dart` | route 生命周期、account 编排、刷新与来源返回 |
| `experience_summary_controller.dart` | 等级概要独立快照与请求隔离 |
| `experience_transaction_controller.dart` | 经验流水分页、去重与局部 issue |
| `experience_surface.dart` | 页面头、等级状态、连续流水和三档结构 |

新增 `wallet_experience_controller_test.dart` 与 `wallet_experience_responsive_test.dart` 承载状态、竞态、断点、四主题和长内容；现有 repository model 测试补齐 HTTP endpoint / query contract，Commerce route 与 Shell Smoke 保留导航回归。所有改动 Dart owner 必须低于 `1500` 行，页面与 surface 按真实职责控制在约 `650` 行内。

### 5.2 三档结构

| 窗口 | Wallet / Experience 冻结结构 |
| --- | --- |
| compact `<600px` | 页面头与刷新动作后，概要、流水按单列连续任务顺序呈现；长值和备注允许换行，不把尾部金额挤出视口 |
| medium `600–1023px` | 仍保持概要先于流水的主任务顺序；概要内部使用受控双列指标，流水占完整主轴，避免在 `600px` 强塞窄 rail |
| expanded `>=1024px` | `280–300px` 概要 rail + `24px` 间距 + 不超过 `904px` 的连续流水主轴；`1024px` 使用弹性主轴，`1280px` 达到受控最大阅读宽度 |

两页统一消费 `RadishWindowClass`、`RadishContentFrame`、Theme token、`RadishStateSlot` 与 `RadishSectionSurface`，不建立第二套断点或页面主题。Wallet 订单筛选上下文必须保留在页面头；Experience 排名与冻结状态只读呈现，不提前进入 P5-D2 Leaderboard。服务端 `themeColor`、icon / badge URL 不用于制造未确认的原生视觉或远端媒体。

## 6. 实施门禁

1. 先补四个 controller 的 initial / empty / unavailable / stale / recover、单边失败、跨账号、query、credential generation 与 dispose 迟到响应测试。
2. Wallet / Experience 流水覆盖 refresh 权威替换、append issue、稳定去重、已知空快照刷新失败和 dataCount / pageCount。
3. repository 覆盖四个既有 endpoint、Bearer token、分页与 Wallet 可选筛选 query；不改变公开接口签名或服务端契约。
4. 精确覆盖 `599 / 600 / 1024 / 1280`，并覆盖四主题同构、长交易号 / 参与者 / 业务 ID / 备注 / 等级名 / 冻结原因和大数值无横向溢出。
5. 现有模型 `4 / 4`、Commerce / Wallet route `20 / 20` 与 Shell Smoke `51 / 51` 不得减少；实施后执行 P5-D1 定向、Shell、Flutter 全量与 `flutter analyze`。
6. `dart format`、改动 Dart owner `<1500`、文档、仓库卫生与 `git diff --check` 通过。

## 7. 停止线与下一步

- 不新增或修改后端 API、DTO、权限、数据库、migration、依赖、lockfile 或移动端 BFF。
- 不新增转账、打赏、退款、调账、支付密码、经验调整、冻结 / 解冻、治理复核或管理员动作。
- 不扩 Coin 统计图、独立交易详情、业务对象跳转、Experience expType / 日期筛选、等级配置、公开经验详情或新媒体读取。
- 不提前进入 P5-D2 Leaderboard、P5-D3 Browse History、P5-E 成组门禁、平台工程或分发。
- 不读取或修改 Pen；P5-D1 直接继承 P3 方向和 P4 Theme / Shared / Shell。
- 不启动服务，不执行真实 Gateway、浏览器、Android RC 或其他设备 Smoke；阶段运行态验收继续独立授权。

P5-D1 readiness 已关闭，后续已按本记录建立四个状态 owner 与 controller tests，拆分两页 surface，并完成三档结构与完整静态回归，详见 [P5-D1 实现记录](/records/f4-flutter-native-p5d1-wallet-experience-implementation-2026-08-24)。下一顺位进入 P5-D2 Leaderboard readiness；P5-D2 实施、平台工程、服务启动与真实运行态 Smoke 均不随本记录自动授权。
