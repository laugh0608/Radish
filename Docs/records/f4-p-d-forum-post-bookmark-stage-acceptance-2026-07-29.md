# F4-P-D 论坛帖子收藏成组验收记录

> 日期：2026-07-29（Asia/Shanghai）
>
> 结论：通过；F4-P A-D 批形成完整闭环，专题关闭。

## 验收范围

本批通过 Gateway 对帖子收藏、个人收藏回访和不可用目标执行 PC / mobile 运行态验收，并结合服务端、migration、HTTP 契约和前端自动化回归覆盖完整幂等、权限与隐私边界。

代表身份与路径：

- 匿名读者点击收藏后进入统一登录，登录成功精确回到 canonical 帖子详情，页面不自动提交收藏；
- TestUser 收藏 Admin 帖子，覆盖普通收藏者和第三方读者路径；
- TestUser 收藏自己创建的临时帖子，覆盖帖子作者路径；
- 收藏、重复收藏、取消、重复取消、取消后重收，以及多标签同态提交后均收敛到唯一有效关系和权威 `CollectCount`；
- 11 条受控收藏覆盖稳定 `BookmarkedAt DESC, Id DESC` 分页、URL 页码和末页移除后的页码收敛；
- 帖子禁用后列表只显示通用 Unavailable 占位，不泄露旧标题、作者或原因，并可通过 Bookmark PublicId 移除；
- 收藏写入没有产生 Notification、UserNotification、Reliable Outbox、经验、萝卜币或奖励副作用。

页面矩阵：

- `1920 × 1080` PC 与 `390 × 844 / DPR 3` mobile；
- `zh / en`；
- `default / guofeng` 代表主题；
- mobile `clientWidth = scrollWidth = 390`，无水平溢出；
- 修正后的浏览器控制台没有 warning / error。

四主题完整静态契约、键盘焦点、`aria-pressed`、loading / disabled 和 reduced-motion 继续由 Client 自动化回归覆盖。当前机器未配置 PostgreSQL 集成测试环境，相关条件用例保持显式跳过；本批不把 SQLite 结果表述为 PostgreSQL 实跑。

## 验收中修正

### 个人内容来源返回契约

从 `/me/content?tab=bookmarks&page=2` 打开帖子后，详情返回链接曾被压缩成 `/me`，丢失收藏标签和分页。

根因是 `PublicRouteDescriptor.me` 固定为占位 `{ kind: 'index' }`，`MeApp` 捕获公开详情来源时始终写入该静态描述符，`PublicEntry` 又把全部 `me` 来源硬编码构造成 `/me`。这不是收藏列表局部 href 问题，而是所有个人中心子页进入公开详情时都会丢失来源上下文的统一导航契约缺陷。

本批按根因修正：

- `PublicRouteDescriptor.me` 改为携带完整 `MeRoute`；
- `MeApp` 以当前 route 构造来源描述符，统一覆盖个人内容、浏览历史、附件和其他个人子页；
- `PublicEntry` 通过既有 `buildMePath()` 还原标签、筛选和页码；
- 单元与静态契约测试锁定 `/me/content?tab=bookmarks&page=2` 来源状态及统一构造器使用。

修正后 PC 与 mobile 的两个“返回我的状态”入口都指向 `/me/content?tab=bookmarks&page=2`，点击返回后仍为收藏标签、第 2 页和 `2 / 2` 分页。

## Migration 与运行态

- `20260729_017_forum_post_bookmark` 已应用，Main / Log / Message / Chat ledger 全部已应用，OpenIddict pending 为 `0`。
- 当前环境首次执行 `dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- apply` 时停留在本机 MSBuild 层并以生成失败退出，未进入应用逻辑；随后使用同一 Debug 构建产物执行 `dotnet Radish.DbMigrate/bin/Debug/net10.0/Radish.DbMigrate.dll apply`，migration、幂等 seed 与最终严格 Doctor 成功。该现象不是 migration 逻辑失败。
- `./start.sh` 选择 `10` 两次启动 Gateway、API、Auth、Frontend 与 Console；构建均为 `0 warning / 0 error`。
- `npm run check:host-runtime -- --details`：Gateway、API、Auth 均返回 `200`。
- Gateway 正式页面完成匿名、登录态、PC 与 mobile 浏览器复核；Frontend / Console Vite 入口正常启动。

第一次打开应用读取到此前浏览器会话的失效 refresh token，客户端按既有策略自动退出；重新登录后的收藏主链路和来源返回修正回归没有控制台 warning / error。

## 自动化与静态验证

- 来源返回定向测试：`36 / 36` 通过。
- `npm run test --workspace=radish.client`：`476 / 476` 通过。
- `npm run type-check --workspace=radish.client`：通过。
- `npm run lint --workspace=radish.client`：通过。
- `npm run build --workspace=radish.client`：通过；保留既有大 chunk 提示，没有新增构建错误。
- `git diff --check` 与仓库卫生检查：通过。
- `Radish.DbMigrate verify`：通过。
- `Radish.db / Radish.Log.db / Radish.Message.db / Radish.Chat.db / Radish.Hangfire.db / Radish.OpenIddict.db` 的 `PRAGMA integrity_check` 均为 `ok`。

本批最终代码只修改 Client 内部导航契约与测试，没有后端业务代码变化；后端收藏事务、权限、并发和 migration 矩阵沿用 F4-P-B / C 已通过的完整 baseline，本批用真实 Gateway 状态与数据库投影复核补齐运行态证据。

## 清理与关闭

验收数据已按精确 ID 清理并复核：

- 临时 Post、UserPostBookmark、PostTag、PostContentRevision、RevisionTag、ContentSubmissionRecord、Reliable Outbox 和 UserBrowseHistory：`0`；
- 不可用帖子、分页夹具和两轮来源返回夹具：`0`；
- 发帖产生的 `POST_CREATE / FIRST_POST` 经验流水和当日统计已删除，TestUser 经验恢复为 `15`；
- 技术分类和技术标签 `PostCount` 均恢复为 `1`；
- TestUser 默认头像 `DownloadCount` 已回退本次来源返回修正回归新增量，恢复为第二轮运行态前的 `330`；
- Bookmark 相关通知、经验、萝卜币和 Outbox 副作用记录：`0`。

测试身份已退出，语言与主题恢复，移动设备指标和 viewport 已重置，应用内浏览器标签页已关闭；Gateway、API、Auth、Frontend 与 Console 均已停止。清理后的 strict migration verify 与六库完整性检查通过。

F4-P 至此关闭。下一步回到 F4 功能完成线，等待批准进入 F4-Q-A 候选只读审计；候选裁决和专题边界确认前不直接编码。
