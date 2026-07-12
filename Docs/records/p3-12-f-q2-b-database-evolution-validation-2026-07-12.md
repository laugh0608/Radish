# P3-12-F Q2-B 数据库演进验证记录

> 状态：`Release Go 必要子集已通过`
>
> 日期：`2026-07-12`（Asia/Shanghai）
>
> 范围：schema ledger、OpenIddict 显式迁移、经验自然日迁移、并发 apply、备份 / 前滚 / 恢复；未写入当前业务数据库，未执行部署或创建 tag。

## 结论

- Main / Log / Message / Chat 已由 `20260712_000_baseline` 接管；baseline 后的结构缺失不再由 Code First 或旧补丁静默修复。
- `20260712_001_experience_natural_dates` 在 SQLite / PostgreSQL 均可前滚、重入和严格验证，异常历史值会在写入前被拒绝。
- SQLite 使用 non-deferred 写事务串行化 baseline 与 migration；PostgreSQL 使用 transaction-scoped advisory lock。双写者实测只登记一条 baseline / 业务 migration。
- OpenIddict SQLite / PostgreSQL migration assembly、空库迁移、旧 `EnsureCreated` schema adoption、pending 启动门禁均通过。
- PostgreSQL custom-format `pg_dump → 前滚 → 删除 → pg_restore → 再前滚` 演练通过；SQLite 文件副本恢复与再次 apply 已转为自动化测试。

## 验证矩阵

### 代码侧

- `npm run check:dependency-security`：npm / NuGet High、Critical 均为 `0`。
- `dotnet build Radish.slnx -c Debug --no-restore --disable-build-servers`：`0` Warning / `0` Error。
- `dotnet test Radish.Api.Tests --no-restore --disable-build-servers`：`618` 通过、`7` 个 PostgreSQL 环境用例按约定跳过。
- staged repo hygiene 与 `git diff --cached --check`：通过。

### SQLite

- 完整旧值 `2026-07-11 16:00:00` 转换为业务自然日 `2026-07-12`。
- 三列声明类型转为 `date`，原索引保留，重复 apply 不重复 ledger。
- 非业务日起点样本被拒绝，列类型保持旧值且不登记成功 migration。
- 两个并发 baseline 接管 / apply 通过 immediate write transaction 串行化。
- 旧库文件副本恢复后可再次前滚到 2 条 ledger 与 `date` 结构。

### PostgreSQL 17

- 旧 `timestamp without time zone` 按 UTC instant 解释后转业务时区；旧 `timestamptz` 直接转业务时区；治理自然日按 UTC 日期转换。
- date 新库契约、旧 timestamp 前滚、timestamptz 数据库侧审计和双 apply advisory lock 均在隔离容器实跑通过。
- `ExpTransaction.CreatedDate` 与 `UserExpDailyStats.StatDate` 均从跨日 UTC 起点准确得到 `2026-07-12`，索引保留。

## PostgreSQL 备份与恢复演练

1. 在临时 `postgres:17-alpine` 容器创建 `q2b_rehearsal` 旧基线：
   - `ExpTransaction.CreatedDate`：`timestamp without time zone`；
   - `UserExpDailyStats.StatDate`：`timestamp with time zone`；
   - `UserExperienceGovernanceAction.StatDate`：`timestamp without time zone`；
   - ledger 仅含 `20260712_000_baseline`。
2. 使用 `pg_dump --format=custom --schema=q2b_rehearsal --no-owner --no-privileges` 生成升级前备份。
3. 首次前滚后确认：三列均为 `date`，值为 `2026-07-12`，两个业务索引存在，ledger 为 baseline + natural dates。
4. 删除演练 schema，使用 `pg_restore --exit-on-error` 恢复；确认三列回到旧 timestamp 类型且 ledger 仅含 baseline。
5. 对恢复基线再次前滚；确认 3 个 `date`、2 个正确自然日值和 2 条 ledger。
6. 临时 schema、dump 与容器随 `--rm` 容器清理；仓库和当前业务数据库未留下运行态数据。

## 交付边界

- 数据库迁移默认只前滚；应用回退必须配合升级前备份恢复。
- `init` 不再允许覆盖已登记 baseline 的 Main；已有 ledger 的缺表 / 缺列必须新增 migration。
- 下一工程顺位进入 Q2-C 版本单一真值，不在本批创建 tag、镜像或发布记录。
