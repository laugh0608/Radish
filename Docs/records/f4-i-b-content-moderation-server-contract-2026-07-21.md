# F4-I-B 内容治理服务端权威契约完成记录

> 日期：2026-07-21（Asia/Shanghai）
>
> 范围：Main migration、案件 / 证据 / 事件 / 用户状态、五类目标处置、可靠任务与通知、API / 权限 / `@radish/http`；不包含 Pencil、正式页面迁移和 Gateway 运行态验收

## 一、交付结论

F4-I-B 已把逐条举报和可变动作记录收束到可追溯的服务端治理链路：同一租户、同一目标聚合到唯一开放案件，举报快照与复核证据追加保存，案件决定和用户当前状态分别使用版本保护，动作通过业务键幂等回放，内容处置结果和事件留痕在同一事务中提交。

旧 `Review / ApplyUserAction` 在 F4-I-C 消费者迁移前保持接口兼容，但已改为调用同一 `UserModerationState` 原子写入边界；运行权限不再出现“旧入口写动作流水、新入口读不到当前状态”的过渡分叉。

## 二、数据与迁移

- 新增 `ContentModerationCase / ContentModerationEvidence / ContentModerationCaseEvent / UserModerationState`，扩展 `ContentReport` 和 `UserModerationAction` 的案件、公开标识、业务键与状态版本字段。
- `20260721_008_content_moderation_case` 同时覆盖 SQLite 与 PostgreSQL：历史 Pending 按目标聚合，历史终态保持独立案件，举报快照和决定转为追加式证据 / 事件，当前有效 Mute / Ban 校准到唯一状态行。
- 数据库唯一约束覆盖开放目标、举报者同案、证据 / 事件序号、用户策略状态、案件决定业务键和动作业务键；doctor / verify 检查无法解释的历史事实和唯一性漂移。

## 三、事务与动作边界

- 专属 `IContentModerationCaseRepository` 承担举报聚合、证据追加、Case CAS、决定、纠正动作和用户状态版本写入，Service 不直接访问 Db。
- Post / Comment / PostQuickReply / Product 在 Main 事务内按目标版本执行限制；版本变化时整体回滚案件、状态、动作和事件。
- ChatMessage 通过 Main `ReliableOutboxMessage` 请求 Chat 源库精确撤回，消费成功后才把案件置为 Resolved；重放按 `operationKey` 返回原结果，同键异参冲突。
- Ban 生效时以独立 Unmute 动作终止当前 Mute；自然到期由读取时实时派生，不修改历史动作流水。
- 举报结案与用户治理状态变化在业务事务内写入 `NotificationRequested`，使用稳定业务键避免可靠任务重放产生重复通知。

## 四、接口与权限

- 用户侧：举报响应改为公开收件对象，并提供本人举报列表和详情。
- Console：新增案件队列、详情、事件、证据采集、案件决定和结案后纠正动作接口。
- `moderation.view / moderation.review / moderation.action` 分离；附带用户动作的案件决定必须同时拥有 Action 权限，既有 Review 授权在 seed 迁移时显式继承 Action，避免升级后误丢权限。
- `@radish/http` 增加稳定 DTO / VO 契约，LongId 继续使用字符串；F4-I-B 没有修改 Console 页面或 client 路由。

## 五、验证结果

- 后端全量 `973` 项通过、`26` 项环境用例按配置跳过；内容治理与通知定向回归覆盖案件聚合、重复举报、Case / State 版本、决定和动作幂等、目标版本回滚、Chat 待处理 / 完成、旧写入口权威状态兼容及可靠通知。
- SQLite migration 历史映射与重入通过。
- 隔离 `postgres:17-alpine` 容器内 PostgreSQL migration 首次执行、重入和 verify 通过；测试 schema 与临时容器均已删除。
- `@radish/http` type-check、client / Console production build、Console 权限映射、解决方案构建、仓库卫生、文档编码 / 乱码与 `git diff --check` 纳入本批提交前验证。
- 本批未启动 API、Auth、Gateway、client 或 Console，不执行浏览器 smoke；这些按阶段节奏留给 F4-I-D 成组验收。

## 六、下一顺位

F4-I-C 先更新既有 Console `P02 / P07` 和 `/me/reports` 的 PC / mobile Pencil，再迁移正式 `/moderation` 与本人举报页面，覆盖权限态、证据 / 决定 / 动作分区、冲突恢复、目标失效和双语。新消费者完成迁移后删除旧写 API，避免长期维护两套入口。
