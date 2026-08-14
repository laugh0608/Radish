# F4-R R3-C05-C Documents 权威治理实现

> 日期：2026-08-13（Asia/Shanghai）
>
> 状态：代码实现与静态门禁完成；按当前阶段约束未启动服务、浏览器或运行态验收
>
> 范围：独立治理版本、append-only 治理事件、事务 CAS、真实分页、显式目标与 PC / Mobile 治理承载

## 本批结论

R3-C05-C 已关闭文档普通治理动作只有正文版本、状态变更没有统一事实事件、列表与待审队列固定读取、读取竞态互相覆盖，以及页面默认把首篇文档当作治理目标等根因。内容 `Version`、审核 `ReviewEvent`、独立 `GovernanceVersion` 与 append-only `WikiDocumentGovernanceEvent` 保持分域；既有 Author、公开阅读、附件令牌和审核状态机不变，既有 `console.docs.*` 权限继续复用，没有新增权限键或万能治理抽象。

## 服务端权威契约

- `WikiDocument.GovernanceVersion` 从 `0` 起步；`WikiDocumentGovernanceEvent` 快照动作、状态、可见性、ACL、删除态、正文 / 治理版本、来源 Revision、理由、操作者与时间，并以“租户 + 文档 + 结果治理版本”保持唯一。
- `20260813_021_wiki_document_governance` 同时支持 SQLite / PostgreSQL 幂等 apply / verify；历史文档治理版本回填为 `0`，事件表和稳定索引由显式 migration 建立。
- 发布、下架、归档、删除、恢复、访问策略和回滚提交必填理由与 `ExpectedGovernanceVersion`；Publish / Rollback 同时提交 `ExpectedDocumentVersion`。Repository 在同一事务完成条件更新与事件追加，任一步失败均不留下半次治理事实。
- 普通 `UpdateWikiDocumentDto` 不再承载 ACL；访问策略只能由专用治理 endpoint 修改。所有治理写入返回权威文档与本次事件，冲突使用结构化 `409`，调用方不得以默认值或盲目重试掩盖版本变化。
- 文档、待审、Revision 与治理历史均执行服务端稳定排序和真实分页；治理历史继续归入既有 view 权限。

## Console 权威交互

- 文档列表和待审队列各自把分页 / 筛选写入 URL；列表、待审、选中详情、Revision、Review 证据和治理历史分别维护请求代际、查询快照及 `loading / ready / unavailable / stale`。
- 页面不自动选择第一篇文档。查看证据、执行治理与回滚都绑定操作者明确选择的目标；账号切换会清除旧快照，过期响应不能覆盖当前账号或当前查询。
- CAS 冲突保留理由草稿，精确刷新同一目标后要求重新确认；写入成功先消费返回的权威文档和事件，再后台刷新列表，成功状态不依赖后续读取才能成立。
- PC 使用连续文档表格、证据、动作和事件；Mobile 使用文档卡、单一目标任务、证据与事件卡，不压缩 PC 宽表。dirty / busy、权限和 stale 停止线在 handler 与界面共同守卫。

## 静态验证

| 门禁 | 结果 |
| --- | --- |
| `dotnet test Radish.Api.Tests --no-restore` | `1262 passed / 40 skipped`；跳过项均为当前环境未提供 PostgreSQL 等条件集成环境 |
| Wiki migration / Repository / Service / Controller / Authorization 定向回归 | `52 passed / 1 skipped`；SQLite migration、唯一索引、事务回滚与 CAS 通过，PostgreSQL 条件用例因未提供连接串跳过 |
| `npm run test --workspace=radish.console` | `122 / 122` 通过 |
| Console type-check / Lint / production build | 通过，Lint `0 warning` |
| 权限、文档、changed hygiene 与 `git diff --check` | 通过 |

## 停止线与下一步

- 未修改 Docs Author、公开 ACL 读取、附件 token、Review 状态机、权限键或 Pencil；没有启动 API、Auth、Gateway、Console dev server 或浏览器。
- PostgreSQL migration / 并发行为已有条件集成用例，但当前环境缺少 `RADISH_TEST_POSTGRES_CONNECTION_STRING`，因此本轮只取得 SQLite 实跑证据；此项不阻断开发中的静态收口，合并前应在具备连接串的门禁环境执行。
- R3-C05 的 Dashboard、Channel 与 Documents 三批代码及静态门禁已关闭。下一顺位为 `R3-C05-D Experience 权威台账治理`：先完成事实反查和精确方案确认，再修改经验写入接口、事务或数据模型。
