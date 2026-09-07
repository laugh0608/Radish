# Flutter Native P5-D1 Wallet / Experience 实现记录

> 状态：`P5-D1` 已完成；下一顺位进入 `P5-D2 Leaderboard readiness`
>
> 日期：2026-08-24（Asia/Shanghai）
>
> 前置记录：[P5-D1 实施就绪与方案冻结](/records/f4-flutter-native-p5d1-wallet-experience-readiness-2026-08-24)

## 1. 结论

P5-D1 已按冻结边界完成。Wallet 余额 / Coin 流水与 Experience 等级概要 / 经验流水已拆为四个独立只读状态 owner，继续复用既有四个 API endpoint，没有新增后端 API、DTO、权限、数据库、migration 或移动端 BFF。

概要与流水不再由一次整批成败裁决。任一侧首次失败只呈现局部 unavailable，同账号刷新失败保留最后权威快照并进入 stale；已知空流水也是权威快照。两类流水的 refresh 以第一页替换，append 保留旧内容、独立呈现 issue 并按稳定 ID 去重。

compact / medium / expanded 已形成连续单列、概要双列指标 + 完整流水，以及 `280–300 + 24 + <=904` 的概要 rail / 流水主轴。精确断点、四主题、单边失败和超长私域数据均有静态回归。

## 2. Owner 与规模

| Owner | 实施后职责 | 行数 |
| --- | --- | ---: |
| `wallet_page.dart` | route、account / query 编排、刷新与来源返回 | `215` |
| `wallet_balance_controller.dart` | 余额权威快照、credential generation 与 stale | `183` |
| `wallet_transaction_controller.dart` | Coin query、页码、去重和 initial / refresh / append issue | `400` |
| `wallet_surface.dart` | 余额状态、连续 Coin 流水与三档结构 | `560` |
| `experience_page.dart` | route、account 编排、刷新与返回 | `186` |
| `experience_summary_controller.dart` | 等级概要权威快照、credential generation 与 stale | `184` |
| `experience_transaction_controller.dart` | 经验流水页码、去重和局部 issue | `297` |
| `experience_surface.dart` | 等级状态、连续经验流水与三档结构 | `526` |

原 `646 / 562` 行 Wallet / Experience 页面已按 route 编排、远端状态与呈现职责拆分。本批运行时 Dart owner 最大 `560` 行；新增 controller 与 responsive 测试分别为 `650 / 388` 行，全部低于 `1500` 行硬上限。

## 3. 权威状态与私域边界

- Wallet 余额、Wallet 流水、Experience 概要与 Experience 流水分别提交 loading / ready / empty / unavailable / stale，不建立跨域万能分页基类或全局 store。
- `accountId` 是账号 identity，`accessToken` 只作请求凭据。Shell Profile 与订单详情已继续传递已知 account target；无 session owner 的兼容测试入口才回退到标准化 token。
- account、query、repository、credential generation 或 dispose 变化会拒绝迟到响应。换账号先清空旧私域快照；同账号 token 续期重新读取但不改变 identity。
- Wallet query target 标准化 `transactionType / status / businessType / businessId`；非规范正整数 LongId 在 repository 请求前拒绝。query 改变只重置流水，不清除账号级余额。
- 两类流水 refresh 以新第一页权威替换；append 的旧列表、页码与 dataCount 在失败时保持可读，成功时按稳定 ID / 交易号 fallback 去重。

## 4. 导航、主题与结构

- “我的”继续打开 Wallet / Experience 并使用真实 Navigator 返回；订单详情继续以 `CONSUME + Order + orderId` 打开扣款流水，标题、筛选上下文和返回来源保持。
- 两页统一消费 `RadishContentFrame`、`RadishWindowClass`、Theme token、`RadishSectionSurface`、`RadishStateSlot` 与 `RadishStateChip`，不建立第二套断点、主题或 Navigator。
- compact 保持概要先于连续流水；medium 概要内部使用受控双列指标，流水占完整主轴；expanded 形成 `280–300px` 概要 rail、`24px` 间距与不超过 `904px` 的流水主轴。
- 长交易号、参与者、业务 ID、备注、等级名、冻结原因与大数值已改为可换行呈现；服务端 `themeColor` 没有越过语义 token 边界。

## 5. 验证

- P5-D1 repository / model：`8 / 8`，覆盖四个 endpoint、Bearer token、页码与 Wallet 可选筛选 query。
- P5-D1 controller：`20 / 20`，覆盖 initial unavailable / recover、empty / stale、refresh 替换、append issue / 去重、account / query / credential generation / dispose 迟到响应。
- P5-D1 responsive：`14 / 14`，精确覆盖 `599 / 600 / 1024 / 1280`、四主题、四种概要 / 流水单边失败和 compact 长内容。
- Commerce / Wallet route：`20 / 20`；Shell 静态 Smoke：`51 / 51`；上述成组定向合计 `113 / 113`。
- Flutter 全量：`356 / 356`；`flutter analyze`：零问题。
- `dart format`、文件行数、`npm run check:docs`、`npm run check:repo-hygiene:changed` 与 `git diff --check` 通过。

本批未新增或修改后端 API、DTO、权限、数据库、migration、依赖、lockfile、Pen 或平台工程，未启动服务，未执行真实 Gateway、浏览器、Android RC 或其他设备 Smoke。

## 6. 下一顺位

P5-D1 关闭。下一顺位进入 `P5-D2 Leaderboard readiness`：先反查现有只读排行契约、紧凑排名、expanded 公开主页上下文、业务 accent / 主题可读性和当前测试基线，再冻结独立实施边界。P5-D2 实现、P5-D3、P5-E、平台工程、服务启动与真实运行态 Smoke 不随本记录自动授权。
