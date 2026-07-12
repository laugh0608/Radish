# P3-12-F Q2-A 时间语义与历史数据迁移方案

> 状态：`Release Go 高风险时间语义已收口；物理 date 改列已由 Q2-B 有序迁移完成`
>
> 日期：`2026-07-12`（Asia/Shanghai）
>
> 范围：只覆盖正式发布矩阵中的 UTC 时刻、业务自然日、时区边界、历史数据识别与高风险迁移；不包含 Q2-B schema ledger / OpenIddict 升级、Q2-C 版本治理、全仓机械替换或页面视觉调整。

## 摘要

当前 API 已将输出时间序列化为 UTC ISO 8601，PostgreSQL 参数与 BaseRepository 实体写入也存在 UTC 规范化，但业务代码仍大量直接读取进程本地时间。正式源码中约有 `384` 处 `DateTime.Now`、`20` 处 `DateTime.Today`、`117` 处 `DateTime.UtcNow`，只有文件访问 token 首批注入了 `TimeProvider`，且仍使用 `GetLocalNow()`。

问题不是简单替换方法名：绝对时刻、业务自然日、用户生日和展示时间共用 `DateTime`，Hangfire 按配置业务时区调度而任务内部仍依赖操作系统时区；SQLite 还可能同时存在经 BaseRepository 规范化的 UTC 值与绕过该入口写入的本地时间。无条件平移历史数据会把正确记录改错，因此必须先建立语义分类、可测试时钟和显式迁移决策。

## 实施进度（2026-07-12）

### 已完成

- 契约基础：新增 `BusinessCalendar`，API 与 DbMigrate 共用 `appsettings.Shared.json` 的 `Time.DefaultTimeZoneId`；覆盖上海业务午夜、纽约 DST 23 / 25 小时自然日和跳过午夜的时区边界。
- 绝对时刻高风险链路：文件访问 token、操作幂等、支付口令、投票 / 抽奖、订单 / 权益、文件清理均改为注入 `TimeProvider` 获取 UTC，不再依赖进程本地时间。
- 业务自然日高风险链路：上传日限额、每日萝卜币奖励、商城日统计、评论高亮目标日与回收站日期目录统一使用 `BusinessCalendar`。
- 经验 / 登录自然日：`ExperienceService` 全部 partial 统一注入 UTC 时钟；每日登录 / 连续登录上限、经验流水日期、统计窗口和管理员调整按系统业务日计算，API 的统计日、峰值日、规则命中日与治理命中日改用 `DateOnly / yyyy-MM-dd`。
- API 输入：Console 的绝对截止时间改发带 offset 的 ISO 8601；全局 converter 拒绝无 offset 的日期时间，同时暂时保留 `yyyy-MM-dd` 自然日兼容入口。
- 历史审计：DbMigrate `doctor / verify` 分页只读检查 `ExpTransaction.CreatedDate`、`UserExpDailyStats.StatDate`、`CommentHighlight.StatDate` 与治理 `StatDate`，同时输出实际列类型和 `datetime` 兼容态 / `date` 目标态；异常只报告并阻断严格验证，不自动平移。
- 增量门禁：`validate:baseline:quick` 已接入时间语义扫描；baseline 已由 `117` 个文件、`329` 处下降到 `111` 个文件、`306` 处，禁止文件级计数继续增长。

### 本批验证

- `dotnet test Radish.Api.Tests --no-restore --maxcpucount:1 --nodeReuse:false`：`609` 通过，`3` 个需外部 PostgreSQL 的集成测试按默认环境条件跳过。
- 隔离 PostgreSQL 17：新增时间语义集成测试 `1/1` 通过；确认两处经验自然日列当前生成 `timestamp without time zone`，`CommentHighlight.StatDate` 生成 `date`，审计无异常，测试 schema 与临时容器均已清理。
- `npm run validate:baseline:quick`：通过，包含四个前端 workspace 类型检查、`319` 个 Client 测试及时间语义 / 权限 / 身份 / 敏感信息门禁。
- `Radish.DbMigrate verify`：当前 SQLite 中两处经验自然日列为 `datetime` 且各 `5` 行，`CommentHighlight.StatDate` 为 `date`，治理日期为 `datetime`；时间语义异常均为 `0`。
- `npm run check:repo-hygiene:changed` 与 `git diff --check`：通过；三份既有 guide 文档仍有篇幅提醒，本批未展开无关拆分。

### 移交 Q2-B / 持续治理

- `ExpTransaction.CreatedDate`、`UserExpDailyStats.StatDate` 与治理 `StatDate` 的物理 `date` 改列已由 Q2-B `20260712_001_experience_natural_dates` 承接，具备 SQLite / PostgreSQL 前滚、异常拒绝、幂等和 ledger 留痕；备份 / 恢复演练仍归 Q2-B 收口。
- 隔离 PostgreSQL 已确认新建 schema 类型；带历史数据的生产相似快照升级演练属于 Q2-B，届时复用本专题的对齐审计作为迁移前置与 verify 门禁。
- 模糊 SQLite 历史绝对时刻仍保持“只报告、不猜测”；只有部署者提供 legacy 时区或明确 `assume-utc` 后才能设计可写迁移。

## 一、统一契约

### 1. 绝对时刻

- 持久化、跨服务消息、过期、锁定、冷却、幂等和审计时刻统一使用 UTC。
- 新增或触达的业务服务通过注入的 `TimeProvider` 获取 `GetUtcNow().UtcDateTime`，不得读取进程本地时区。
- `DateTimeOffset` 只用于保留外部 offset 或协议原值；进入领域和持久化边界后规范为 UTC。
- API 输出继续使用带 `Z` / offset 的 ISO 8601；表示绝对时刻的输入必须带 offset，不能由服务器猜测浏览器本地时区。

### 2. 业务自然日

- 每日奖励、每日上限、签到、日统计和按日任务使用系统业务时区 `Time.DefaultTimeZoneId`，当前默认 `Asia/Shanghai`。
- 用户展示时区只影响显示，不改变经济与治理规则；不得通过切换个人时区重复领取每日权益。
- 领域层优先使用 `DateOnly` 表达自然日；数据库优先使用 `date`，不再把本地午夜伪装为 UTC 时刻。
- 在自然日字段尚未完成 schema 迁移前，只允许通过统一业务日边界转换函数读写，禁止新增 `.Date` / `DateTime.Today` 散点。

### 3. 展示时区

- 展示按“用户偏好时区 > 浏览器时区 > 系统默认时区”回退，维持既有前端能力。
- 后端业务判断不读取用户展示时区；只有明确的业务自然日和 Hangfire cron 使用系统业务时区。

## 二、只读审计结论

### 1. 最高风险链路

- 文件访问 token：已注入 `TimeProvider`，但 `GetLocalNow().DateTime` 会产生 `Unspecified`，与 PostgreSQL / JSON 的“Unspecified 按 UTC”规则冲突。
- 幂等记录：24 小时窗口直接使用 `DateTime.Now`，跨服务器时区或切换时区会改变重放判断。
- 支付密码：锁定、剩余分钟和密码年龄直接使用本地时间，属于身份与资产安全边界。
- 上传限流：分钟 / 日 key 基于服务器本地格式化字符串，多实例时区不一致会分裂限流窗口。
- 投票、抽奖、权益、订单和清理：同一领域内混用 `Now / UtcNow`，跨数据库、任务和 API 边界后可能提前或延后。
- Hangfire：cron 已使用配置业务时区，但 CommentHighlight / Shop 等任务内部仍用操作系统 `Today / Now` 选择业务日。

### 2. 自然日与输入契约

- `UserExpDailyStats.StatDate`、`ExpTransaction.CreatedDate` 等自然日仍使用 `DateTime`；本地 SQLite 样本可见业务日被存为前一天 `16:00:00` UTC 形式。
- API 的全局 UTC converter 输出契约清楚，但会接受无 offset 字符串；Console 商品到期时间当前发送 `yyyy-MM-dd HH:mm:ss`，其语义依赖服务器时区。
- 用户生日、统计日期与绝对截止时间需要拆分，不应继续共用一个全局 `DateTime` 输入规则。

### 3. 历史数据

- PostgreSQL 写入路径已有参数 UTC 规范化，仍需在生产相似快照中核对列类型与数据分布。
- SQLite 直接 seed / 专属仓储与 BaseRepository 路径可能具有不同历史语义，裸文本值无法可靠推断原始 `DateTime.Kind`。
- 历史绝对时刻不得按固定 `+8/-8` 全库平移；自然日字段可以在满足“业务时区本地午夜”对齐条件后确定性转换。

## 三、实施批次

### Q2-A1 契约基础

1. 保留框架 `TimeProvider` 作为唯一可替换时钟。
2. 新增轻量 `BusinessCalendar`：基于 `TimeProvider` 与 `TimeOptions.DefaultTimeZoneId` 提供当前业务日、业务日起止 UTC 时刻和 UTC → 业务日转换。
3. API 与 DbMigrate 显式注册时钟 / 业务日服务；测试使用仓库内固定时钟，不新增测试依赖。
4. 增加跨午夜、`Asia/Shanghai` 和 `America/New_York` DST 边界测试。

### Q2-A2 Release Go 高风险链路

1. 先迁移文件 token、幂等记录、支付密码、上传限流。
2. 再迁移投票 / 抽奖、订单 / 权益、过期清理和可靠任务时刻。
3. 迁移时保持接口与数据库字段兼容，不在同一批重命名全部字段。
4. 所有比较双方必须来自同一 UTC 时钟；持续时间使用 `TimeSpan` 或明确的秒 / 分钟配置。

### Q2-A3 业务自然日

1. 每日萝卜币、经验、签到、日统计和 Hangfire 日任务改用 `BusinessCalendar`。
2. 幂等业务 key 中的日期使用 `DateOnly` 的稳定 `yyyyMMdd` 表达。
3. API 使用 `DateOnly` / `yyyy-MM-dd`；需要长期保留的自然日字段以数据库 `date` 为目标，物理改列通过 Q2-B schema ledger 执行。
4. 用户展示时区不参与每日限额和奖励归属。

### Q2-A4 历史数据与门禁

1. DbMigrate `doctor / verify` 输出时间语义审计：自然日对齐、绝对时刻列类型、异常范围和迁移状态。
2. 自然日转换必须先备份、可重入，并对非本地午夜值失败，不猜测修复。
3. SQLite 模糊绝对时刻只提供显式策略：`assume-utc` 或部署者提供 legacy 时区；默认只报告、不平移。
4. 增量扫描禁止在新的业务代码中增加 `DateTime.Now / Today / DateTimeOffset.Now`；历史存量按本专题触达下降，不作为一次性全仓阻断。

## 四、验证矩阵

- 固定时钟：UTC 与业务时区不同时，过期 / 锁定 / 幂等窗口仍按同一绝对时刻判断。
- 业务日：UTC 前后跨越业务午夜时，奖励 key、统计日和任务目标日只切换一次。
- DST：无效 / 重复本地时刻不进入持久化，业务日起止由 `TimeZoneInfo` 转换并保持半开区间。
- API：绝对时刻输出带 UTC 标识；无 offset 的绝对时刻输入拒绝；自然日只接受 `yyyy-MM-dd`。
- SQLite / PostgreSQL：高风险条件更新、过期边界、查询窗口和自然日转换结果一致。
- DbMigrate：备份后 apply、重入、严格 verify 和异常数据拒绝均有测试。
- 日常验证：定向测试、`dotnet build`、后端测试、`git diff --check` 与 changed-only repo hygiene；准备合并时再执行 baseline。

## 五、停止线

- 不做全仓 `DateTime.Now / Today / UtcNow` 机械替换。
- 不在 Q2-A 引入 Q2-B schema ledger 或 OpenIddict 显式迁移。
- 不把用户展示时区用作经济规则时区。
- 不对无法判定语义的 SQLite 历史绝对时刻自动平移。
- 不启动 API / Gateway / Auth 或浏览器 smoke；需要运行态复核时仍遵循当轮启动授权。
- 不为时间治理顺带重构页面、领域模型或无关大文件。

## 六、退出条件

- Release Go 高风险链路不再直接读取进程本地时间，且可由固定时钟测试。
- 每日奖励、经验统计和 Hangfire 业务日使用同一配置时区，不受容器 OS 时区影响。
- 新增业务代码无法重新引入本地时间散点。
- 历史数据具有只读识别、显式决策和严格验证口径；需要写入的结构 / 数据补丁进入 Q2-B schema ledger，模糊数据不会被猜测改写。
- PostgreSQL 与 SQLite 定向测试通过，文档口径与实现保持一致。
