# F4-R R3-C05-D Experience 权威台账治理实现

> 日期：2026-08-13（Asia/Shanghai）
>
> 状态：代码实现与静态门禁完成；按当前阶段约束未启动服务、浏览器或运行态验收
>
> 范围：经验版本 CAS、调账 / 审核幂等、冻结动作事实、等级重算事务审计、URL 查询与 PC / Mobile 治理承载

## 本批结论

R3-C05-D 已关闭经验查看目标与写入目标可分离、`UserExperience.Version` 未返回调用方、调账 / 人工审核缺少调用方幂等、冻结历史缺少版本事实、等级配置重算不是整批事务，以及 Console 用固定读取和清空快照掩盖失败等根因。所有受控写入只绑定已加载的权威用户和版本；既有经验获得规则、等级公式、权限键与公开产品边界不变，没有扩入自动处罚、反作弊、收益排行、公开经验详情或人工修改等级。

## 服务端权威契约

- `UserExperienceVo` 返回 `VoVersion`；调账、冻结、解冻和人工审核都要求理由与 `ExpectedVersion`，调账 / 审核另外要求 `IdempotencyKey`。成功结果返回权威经验快照和动作事实，版本冲突使用结构化 `409`。
- 调账和人工审核使用独立幂等域：同键同载荷重放已完成结果，异载荷、仍在处理或成功结果缺失明确冲突。重放先于版本判断，因此调用方在响应丢失后可使用原请求安全取回结果。
- 专用 Experience repository 在同一领域事务内完成用户版本 CAS、经验台账 / 状态更新、治理动作与幂等结果；PostgreSQL 使用目标级 advisory lock 收敛同一用户并发，SQLite 维持相同原子契约。
- 冻结 / 解冻写入理由及前后版本。过期冻结在权威读取时规范化为自动解冻，并追加 `AutoUnfreeze` 动作，不再留下只有当前状态、没有历史事实的隐式变化。
- `20260813_022_experience_authoritative_governance` 为 SQLite / PostgreSQL 建立动作版本快照、等级重算 append-only 审计与稳定索引；迁移前动作保留为空版本快照，不伪造历史版本。
- 等级保存先生成影响预览与 `PreviewFingerprint`；正式重算在事务内重新校验指纹、整批更新受影响用户并追加公式、变更数、理由、操作者和时间审计，预览过期时拒绝写入。

## Console 权威交互

- 用户目标、统计日期、流水日期 / 分页和动作分页进入 URL。主资料、统计、流水、动作历史与等级配置分别维护请求代际及 `loading / ready / unavailable / stale`，旧请求不能覆盖当前目标或查询。
- 用户写入只绑定已经加载并确认的权威目标；切换用户或首次读取失败不会把旧目标继续当成可写对象。刷新失败保留旧快照但标记 stale，并冻结依赖该快照的写入。
- 调账 / 审核在一次提交生命周期内保持稳定幂等键。CAS 冲突保留表单与幂等键、精确刷新同一目标后要求重新确认；成功先消费权威响应，再刷新从属证据。
- 流水和动作使用服务端真实分页。PC 保持连续台账—证据—动作，Mobile 使用单一用户任务和分页卡片；两种视图共用同一权威快照、权限、dirty / busy 与 stale 停止线。
- 等级配置保存前展示影响摘要、指纹与理由，并提供重算审计；预览、执行和审计读取均不伪造成功或权威空态。

## 静态验证

| 门禁 | 结果 |
| --- | --- |
| `dotnet test Radish.Api.Tests --no-restore` | `1270 passed / 41 skipped`；跳过项均为当前环境未提供 PostgreSQL 等条件集成环境 |
| Experience migration / Repository / Service / Controller 定向回归 | 版本 CAS、幂等重放 / 异载荷冲突、事务回滚、自动解冻、预览指纹与审计通过；PostgreSQL 条件用例因未提供连接串跳过 |
| `npm run test --workspace=radish.console` | `126 / 126` 通过 |
| Console type-check / strict type-check / Lint / production build | 通过，Lint `0 warning`，production build 无 chunk size 警告 |
| 权限与 LongId 门禁 | 通过；仅保留既有 `console.hangfire.replay` 未消费提示 |
| `npm run validate:baseline:quick` | 除本批外既有 `SystemConfigStorageCoordinator.cs` 三处 `DateTime.Now` 时间语义预算外，其余阶段通过 |
| 文档、changed hygiene 与 `git diff --check` | 通过 |

## 停止线与下一步

- 未新增权限键、Pencil 画板、WebOS / Flutter 功能或跨模块万能治理状态机；没有启动 DbMigrate、API、Auth、Gateway、Console dev server 或浏览器。
- PostgreSQL migration / advisory lock 并发行为已有条件集成用例，但当前环境缺少 `RADISH_TEST_POSTGRES_CONNECTION_STRING`，本轮只取得 SQLite 实跑证据；合并前应在具备连接串的门禁环境执行。
- R3-C05 的 Dashboard、Channel、Documents 与 Experience 四批代码及静态门禁至此关闭。下一顺位是成组运行态验收：先按浏览器 smoke 指南说明 migration、服务启动命令、端口、运行影响与清理方式并取得当前任务授权，再通过 Gateway 覆盖 PC / Mobile、双语、权限、URL 回访和受控治理写入。
