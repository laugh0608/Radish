# F4-S-D 公开排行榜治理成组验收记录

> 日期：2026-07-30（Asia/Shanghai）
>
> 结论：通过；F4-S A-D 批形成完整闭环，专题关闭。

## 验收范围

本批通过 Gateway 对公开排行榜的类型白名单、参与资格、稳定排名、隐私边界、公开身份跳转和响应式页面执行运行态复核，并结合后端与 Web 自动化覆盖失败契约和静态路由边界。

代表身份与页面：

- 匿名读者访问经验、发帖、评论、人气与热门商品五类公开榜单；
- TestUser 登录后复核经验榜“我的排名”、本人标记和公开主页入口；
- PC 使用 `1920 × 1080`，mobile 使用 `390 × 844` CSS viewport；
- 应用内浏览器当前未设置设备像素比，本批不把 mobile 结果表述为特定 DPR 实跑；
- mobile `clientWidth = scrollWidth = 390`，经验榜数据态和热门商品空态均无页面级水平溢出；
- 页面控制台在最终冷启动复核中为 `0 error / 0 warning`。

## API 与页面结论

- `GetTypes` 仅返回 `Experience / PostCount / CommentCount / Popularity / HotProduct`，数值顺序为 `[1, 6, 7, 8, 5]`；
- 五类公开列表均成功：当前本地数据量依次为 `4 / 1 / 2 / 0 / 0`，空数据榜单展示正式空态；
- `Balance / TotalSpent / PurchaseCount` 在查询前统一返回 `Leaderboard.TypeUnavailable`；
- 未定义整数类型 `999` 在 `GetLeaderboard` 与 `GetMyRank` 均返回 `Leaderboard.TypeUnavailable`；
- `HotProduct` 的个人排名返回 `Leaderboard.UserRankUnavailable`；
- TestUser 的经验榜个人排名为 `#2`，列表本人标记和个人排名一致；
- `/leaderboard/balance`、`/leaderboard/total-spent`、`/leaderboard/purchase-count` 均安全规范化为 `/leaderboard`；
- 用户榜条目进入 `/u/usr_...` 公开主页，浏览器返回后仍回到来源榜单；
- PC 五类 Tab、数据态、空态和辅助说明布局完整，页面宽度与视口一致。

## 验收中修正

运行态首次请求 `type=999` 时，ASP.NET Core 在进入 Controller 前按 `LeaderboardType` 枚举绑定失败，返回通用 `Common.ValidationFailed`。这与专题已固定的“非公开或未知类型统一返回 `Leaderboard.TypeUnavailable`”契约不一致，且使未知类型和已退出公开的敏感类型产生两套失败语义。

本批将 `GetLeaderboard` 与 `GetMyRank` 的查询入口统一为整数接收，再在 Controller 内转换为 `LeaderboardType` 并交给 `LeaderboardPublicPolicy` 判定。已定义公开类型、敏感类型和默认值保持不变；修正只消除框架绑定对业务错误契约的提前截断，不扩张公开类型。

## 自动化与静态验证

- `dotnet test Radish.Api.Tests --no-restore --filter FullyQualifiedName~Leaderboard`：`25` 通过，`1` 个 PostgreSQL 条件用例显式跳过，`0` 失败；
- 代码侧批次既有 Web 回归：`488 / 488` 通过，type-check、changed-only lint 与 build 通过；
- `./start.sh` 选择 `10` 的修正后冷启动构建：`0 warning / 0 error`；
- `npm run check:host-runtime -- --details`：Gateway、API、Auth 均返回 `200`；
- 最终 Gateway 页面：登录态经验榜正常，控制台 `0 error / 0 warning`；
- `npm run check:docs`、`npm run check:repo-hygiene:changed` 与 `git diff --check`：通过。

当前机器没有配置 `RADISH_TEST_POSTGRES_CONNECTION_STRING`，条件用例保持显式跳过；本批不把 SQLite 聚合翻译结果表述为 PostgreSQL 实跑。

## 清理与关闭

- 测试身份已通过正式退出入口退出；
- 浏览器会话已关闭，viewport 最终为 `1920 × 1080`；
- 榜单只读页面访问产生的三个默认头像 `DownloadCount` 增量已按启动前值恢复为 System `35`、Admin `1125`、TestUser `330`；
- 本批没有创建业务测试实体，没有数据库结构或 migration 变化；
- Gateway、API、Auth、Frontend 与 Console 均已停止；
- 未读取或修改任何 `.pen` 文件，Pencil 继续等待用户明确确认空闲；
- Tauri 仍为冻结实验资产，未进入开发或验收范围。

F4-S 至此关闭。下一步仍为 F4-R C-1B，只有用户明确确认 Pencil 空闲后才进入代表设计复核；等待期间可以继续选择不依赖 Pencil 的完整功能或维护专题。
