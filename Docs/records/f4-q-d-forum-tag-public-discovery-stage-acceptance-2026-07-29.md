# F4-Q-D 论坛标签公开发现成组验收记录

> 日期：2026-07-29（Asia/Shanghai）
>
> 结论：通过；F4-Q A-D 批形成完整闭环，专题关闭。

## 验收范围

本批通过 Gateway 对标签公开发现、相关主题、首包 head、动态 sitemap 与 Console 标签生命周期执行 PC / mobile 运行态验收，并结合服务端、仓储和前端自动化回归覆盖公开可见性、软删除与不可索引边界。

代表身份与路径：

- 匿名读者从 `/discover` 热门标签进入公开标签页，继续切换真实相关标签并进入帖子详情；
- TestUser 作为普通登录用户访问标签公开页，确认登录态不改变公开读取、canonical 和结构化数据契约；
- Admin 通过 Console 创建 Alpha / Beta 两个临时标签，覆盖启停、软删除、显示已删除、恢复和重新启用；
- 两个临时标签关联既有公开帖子后，相关标签 API、相关主题 UI、公开数量、热门标签和 tags sitemap 使用同一公开聚合；
- 标签不可用时保留只读状态页，但 runtime head 收敛为 `noindex, nofollow`，不再保留 canonical、OpenGraph URL 或 JSON-LD。

页面矩阵：

- `1920 × 1080` PC 与 `390 × 844` mobile CSS viewport；
- `zh / en`；
- `default / guofeng` 代表主题；
- mobile `clientWidth = scrollWidth = 390`，无水平溢出；
- 相关标签输出真实 `/forum/tag/:canonicalSlug` 链接，普通点击复用 SPA，辅助点击与复制链接保留浏览器语义。

应用内浏览器当前不提供设备像素比设置，本批没有把 mobile 结果表述为特定 DPR 实跑。四主题完整静态契约、键盘焦点、loading / empty / error / retry 和 reduced-motion 继续由 Client 自动化回归覆盖。当前机器未配置 PostgreSQL 集成测试环境，相关条件用例保持显式跳过；本批不把 SQLite 结果表述为 PostgreSQL 实跑。

## 首包、API 与 sitemap

- 可用标签的公开 head API 与 Gateway `GET / HEAD /forum/tag/:slug` 均返回成功，canonical 指向无排序、页码或来源参数的标签 slug；
- Gateway 首包和 Client runtime 共享 `radish-public-jsonld` 脚本 ID，最终只保留一份 `CollectionPage` JSON-LD；
- `/sitemaps/tags-1.xml` 返回 `200`，只包含具备公开帖子的可用标签；
- 禁用 Beta 后，相关标签聚合与 tags sitemap 均排除该标签；
- 清理后冷启动复核中，两个临时标签的公开 head 返回 `404`，热门标签与 tags sitemap 均无临时 slug，真实 `random-thoughts` 标签保持 canonical、单份 `CollectionPage` JSON-LD 与 `HEAD 200`。

## 验收中修正

### sitemap 与结构化数据契约

- `PublicSitemapController` 的分片文件名正则遗漏 `tags`，导致 sitemap index 已列出 tags 分片但 `/sitemaps/tags-1.xml` 返回 `404`；现已统一识别 `posts / docs / products / tags`。
- Gateway 首包与 Client runtime 使用不同 JSON-LD script ID，页面激活后产生两份结构化数据；现已收口到同一公开脚本 ID。
- 英文公开帖子数量原本只有单一文案，`1` 显示为 `1 public posts`；现已补齐 i18next `_one / _other` 资源。

### 不可用标签 head

禁用标签进入通用状态页后，runtime 曾继续保留可索引 canonical、OpenGraph URL 和 `CollectionPage` JSON-LD。根因是通用公开 head 描述符没有表达“页面存在但不可索引”的状态。

本批为 `PublicHeadDescriptor` 增加显式 `indexable` 语义：不可用标签注册 `indexable: false`，统一 head lifecycle 设置 `noindex, nofollow`，并移除 canonical、OpenGraph URL 与结构化数据。重新进入可用标签时，旧 robots 元数据也会由同一生命周期清理。

### Console 软删除与恢复契约

Console 勾选“显示已删除”后仍看不到软删除标签，恢复动作也会在预检阶段报告不存在。根因是基础仓储查询始终携带全局软删除过滤器，管理服务虽接收 `includeDeleted`，却没有显式切换查询边界；恢复预检同样使用默认 `QueryByIdAsync`。

本批在 `IBaseRepository / BaseRepository` 增加显式 `includeDeleted` 查询重载，默认调用继续保持过滤；Tag 管理分页只在受权参数为真时包含软删除实体，恢复预检按 ID 显式包含软删除。定向服务测试锁定列表与恢复两条契约。

## 自动化与静态验证

- `dotnet test Radish.Api.Tests --no-restore`：`1101 / 1101` 通过，`36` 项 PostgreSQL 条件用例按环境跳过。
- `npm run test --workspace=radish.client`：`482 / 482` 通过。
- 标签 head 与 UI 定向测试：`27 / 27` 通过。
- `npm run type-check --workspace=radish.client`：通过。
- `npm run lint --workspace=radish.client`：通过。
- `npm run build --workspace=radish.client`：通过；保留既有大 chunk 提示，没有新增构建错误。
- `./start.sh` 选择 `10` 的最终冷启动构建：`0 warning / 0 error`。
- `npm run check:host-runtime -- --details`：Gateway、API、Auth 均返回 `200`。
- `Radish.DbMigrate doctor / verify`：通过，Main / Log / Message / Chat ledger 全部已应用，OpenIddict pending 为 `0`。

## 清理与关闭

验收数据已按精确 ID 清理并复核：

- 临时 Tag `2082460907593531392 / 2082461011117342720`：`0`；
- 临时 PostTag `2082462000000000000 / 2082462000000000001`：`0`；
- 临时 slug 对应 UserBrowseHistory 与审计记录：`0`；
- Admin / TestUser 默认头像 `DownloadCount` 已回退本批只读页面访问增量，恢复为 `1125 / 330`；
- Main / Log 执行清理后压缩，数据库文件中不再保留临时名称或 slug 标记；
- `Radish.db / Radish.Log.db / Radish.Message.db / Radish.Chat.db / Radish.Hangfire.db / Radish.OpenIddict.db` 的 `PRAGMA integrity_check` 均为 `ok`。

测试身份已退出，语言与主题恢复为 `zh / default`，viewport 已重置为 `1920 × 1080`，应用内浏览器标签页已关闭；Gateway、API、Auth、Frontend 与 Console 均已停止。清理后的冷启动缓存复核完成，服务再次停止。

F4-Q 至此关闭。下一步等待批准进入 F4-R-A 单专题候选只读审计；候选裁决和设计边界确认前不直接编码，也不重启主动生产证据采集。
