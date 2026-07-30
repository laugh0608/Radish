# F4-S 公开排行榜治理代码侧验收记录

> 日期：2026-07-30（Asia/Shanghai）
>
> 结论：A-C 已完成，D 批代码侧回归通过；未启动服务，Gateway PC / mobile 运行态复核尚未执行。

## 验收范围

本批把既有 `/leaderboard` 从八类匿名榜单收口为可信的公开只读发现能力：

- 公开类型只保留 `Experience / PostCount / CommentCount / Popularity / HotProduct`；
- `Balance / TotalSpent / PurchaseCount` 保留旧枚举值兼容，但 API 在查询前以 `Leaderboard.TypeUnavailable` 拒绝；
- 用户榜统一要求账号启用、未删除，经验冻结、帖子 / 评论公开状态与商品有效状态由仓储权威查询应用；
- 列表、分页总数与个人排名复用同一资格和指标投影，排序固定为指标降序、稳定实体 ID 升序；
- `HotProduct` 的个人排名以 `Leaderboard.UserRankUnavailable` 明确拒绝；
- 匿名读取不再补写 PublicId / PublicIndex，数据库缺失时只返回数字 ID 兼容入口；
- 正式 Web 只登记五类公开路由，服务端元数据还需通过类型与分类白名单；旧资产 / 消费 slug 回落经验榜；
- 服务端元数据请求失败时才使用五类本地标签，成功返回的较小集合不会被客户端 fallback 扩张。

Flutter 继续只维护经验榜 MVP，WebOS 只消费服务端类型且不新增功能，Tauri 不进入本批。F4-R 视觉重构与 Pencil 代表页复核均未在本批提前实施。

## 自动化与静态验证

- `dotnet test Radish.Api.Tests --no-restore --filter FullyQualifiedName~Leaderboard`：`25` 通过，`1` 个 PostgreSQL 条件用例显式跳过，`0` 失败；
- `npm test --workspace=radish.client`：`488 / 488` 通过；
- `npm run type-check --workspace=radish.client`：通过；
- `npm run lint:changed`：通过；
- `npm run build --workspace=radish.client`：通过；
- `npm run check:repo-hygiene:changed`：通过；
- `npm run check:docs`：通过；
- `git diff --check`：通过。

后端用例覆盖五类公开元数据、敏感类型拒绝、用户资格、冻结状态、内容资格、商品资格、同值稳定顺序、总数、个人排名、异常传播以及读取不补写公开身份。前端用例覆盖五类路由、三个旧敏感 slug、未知类型、重复类型和类型 / 分类不匹配过滤。

## 未执行项与边界

- 当前机器没有配置 `RADISH_TEST_POSTGRES_CONNECTION_STRING`，条件用例保持显式跳过；SQLite 聚合翻译结果不表述为 PostgreSQL 实跑。
- 本任务没有获得项目启动授权，因此未执行 Gateway 页面访问、PC / mobile 浏览器复核或 `check:host-runtime`。
- 本批没有数据库结构和 migration 变化，没有读取或修改 `.pen`，也没有恢复 Tauri。
- F4-S 的功能、隐私、文案与类型边界已经固定；后续 F4-R 只处理代表页视觉与真实页面继承，不重新扩张敏感榜单。
