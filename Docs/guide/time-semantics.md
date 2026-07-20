# 时间语义与业务自然日

> 本页定义 Radish 对“绝对时刻、系统业务日、数据库自然日和用户展示时区”的长期契约。具体数据库迁移规则见 [数据库结构变更协作口径](/guide/database-schema-change-governance)。

## 核心原则

Radish 把时间值分成三类，调用方必须先确定语义再选类型：

| 语义 | 推荐类型 | 存储 | 示例 |
| --- | --- | --- | --- |
| 绝对时刻 | `DateTime`（UTC）或 `DateTimeOffset` | SQLite datetime / PostgreSQL `timestamp with time zone` | 创建、修改、过期、锁定、领取、截止时刻 |
| 系统业务日 | `DateOnly` | PostgreSQL / SQLite `date`；遗留实体映射可暂用午夜 `DateTime` | 每日经验、每日登录、每日上传额度、运营统计 |
| 用户展示时间 | 前端格式化后的本地值 | 不作为数据库真值 | 页面日期时间、相对时间、用户偏好时区 |

- 存储和跨服务比较统一使用 UTC，不以服务器本地时区作为业务依据。
- “今天、昨天、每日额度、统计日”统一按 `Time:DefaultTimeZoneId` 的系统业务时区计算。
- API 的纯日期字段使用 `DateOnly`，JSON 形态固定为 `yyyy-MM-dd`；不要用带时区的时间戳表达纯日期。
- 前端展示按“用户偏好时区 → 浏览器时区 → 系统默认时区”回退，不把展示值写回为新的时间真值。

## 运行时组件

API、Auth 与 DbMigrate 都注册 `TimeProvider.System` 和 `BusinessCalendar`：

- `TimeProvider` 提供可测试的当前 UTC 时刻；Service、Job 和清理逻辑不直接依赖 `DateTime.Now / Today`。
- `BusinessCalendar.GetCurrentDate()` 返回系统业务时区中的当前自然日。
- `BusinessCalendar.GetDate(utcInstant)` 把一个 UTC 时刻归属到业务自然日。
- `BusinessCalendar.GetUtcRange(date)` 返回业务日对应的 UTC 半开区间 `[start, end)`，能够处理 DST 无效和歧义时刻。
- `BusinessCalendar.GetTimeUntilNextDate()` 用于每日缓存额度过期，不假设每天固定等于 24 小时。

共享配置：

```json
{
  "Time": {
    "DefaultTimeZoneId": "Asia/Shanghai",
    "DisplayFormat": "yyyy-MM-dd HH:mm:ss"
  }
}
```

测试需要控制当前时刻时应注入 fake `TimeProvider`，不要修改进程时区或依赖测试执行时间。

## 当前业务应用

### 业务日

- 经验发放、每日统计、治理复核窗口和登录奖励按 `BusinessCalendar` 归属日期。
- 上传每日总量使用业务日作为缓存键，并把 TTL 设置到下一个业务日开始。
- 评论神评 / 沙发统计、商城日统计和文件回收目录按业务日分组。

### UTC 时刻

- 文件访问令牌的创建、过期、撤销、原子消费和清理统一比较 UTC。
- 幂等记录的 24 小时保留窗口、处理中重置与终态时间统一使用 UTC。
- 支付密码锁定、失败窗口、修改间隔和过期解锁统一使用 UTC。
- 投票截止、抽奖开奖、订单创建 / 支付 / 完成 / 取消、权益生效 / 过期、附件和分片清理统一使用 UTC。
- Hangfire job 的 Cron 调度只决定“何时唤醒”；任务内部 cutoff、过期和自然日归属仍必须遵循本页契约。

## 数据库日期列

首批物理 `date` 列为：

- `ExpTransaction.CreatedDate`
- `UserExpDailyStats.StatDate`
- `CommentHighlight.StatDate`
- `UserExperienceGovernanceAction.StatDate`

`20260712_001_experience_natural_dates` 负责把 `ExpTransaction.CreatedDate`、`UserExpDailyStats.StatDate` 与 `UserExperienceGovernanceAction.StatDate` 的已有 SQLite / PostgreSQL timestamp / datetime 列迁移为 `date`；`CommentHighlight.StatDate` 按当前模型创建并纳入同一最终类型审计。迁移规则：

- 已登记 schema ledger 的正式库只允许通过有序 migration 前滚。
- PostgreSQL 同时识别 `timestamp with time zone` 和 `timestamp without time zone` 历史形态。
- 历史值必须确实代表自然日午夜；发现非午夜或无法确认的 legacy 时区时拒绝自动平移，先人工确认语义。
- SQLite 重建表时必须保留索引、数据和 ledger checksum。

DbMigrate 的 `doctor / verify` 会审计上述列类型和异常值；业务代码不得在请求路径临时修表。

## PostgreSQL 参数契约

所有 SqlSugar PostgreSQL 连接通过 `PostgreSqlDateTimeParameterNormalizer` 规范化参数：

- UTC `DateTime` 保持不变。
- Local `DateTime` 转为 UTC。
- Unspecified `DateTime` 按 UTC 解释，兼容 SQLite 读取后丢失 `Kind` 的历史实体边界。
- `DateTimeOffset`、数组和可枚举时间参数按同一规则处理。

该规范化属于持久化契约，对包括 Log 在内的全部 PostgreSQL 连接生效；它不依赖 SQL 日志开关，也不依赖 Npgsql legacy timestamp 全局初始化顺序。禁止通过 legacy timestamp 开关掩盖参数或模型错误。

OpenIddict 使用 EF Core，不走 SqlSugar 参数规范化；其 PostgreSQL 时间列由模型显式固定为 `timestamp with time zone`，详见 [OpenIddict 数据库与迁移](/guide/authentication-openiddict-database)。

## API 与前端

- 纯日期 VO / DTO 使用 `DateOnly / DateOnly?`，前端按 `YYYY-MM-DD` 字符串消费。
- 带时刻字段继续输出 UTC ISO 8601；客户端只在展示层转换时区。
- 日期筛选使用业务日边界，服务端转换成 UTC 半开区间查询，避免 `23:59:59.999` 和 DST 边界错误。
- 不允许前端把纯日期先构造成浏览器本地 `Date` 再回传，从而产生跨时区偏移。

## 验证规则

- `npm run check:time-semantics` 阻止活动源码新增 `DateTime.Now`、`DateTime.Today` 和 `DateTimeOffset.Now`。
- 时间敏感 Service / Job 测试必须注入可控时钟，覆盖 UTC 日界线、业务日界线和必要的 DST 场景。
- PostgreSQL 集成测试必须使用真实 provider，覆盖 `timestamp with time zone` 参数、物理 `date` 列和迁移重入。
- 修改时间模型、自然日列或 provider 映射时，必须同时运行 DbMigrate `doctor / apply / verify` 和对应 schema migration 测试。

## 禁止事项

- 不以服务器本地时间作为持久化或过期比较依据。
- 不用字符串截断时间戳推导业务日。
- 不用 `DateTime` 时间戳表达 API 纯日期字段。
- 不在业务代码中自行添加零点偏移或固定 `24h` 日界线。
- 不通过关闭 Npgsql / EF warning、修改 ledger 或重写已发布 migration 解决时间差异。
