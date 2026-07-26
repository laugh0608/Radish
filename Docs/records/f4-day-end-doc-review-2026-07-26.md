# F4 2026-07-26 日终提交回顾与文档审阅

> 日期：2026-07-26（Asia/Shanghai）
>
> 范围：按提交日期统计的 8 个当日提交；本次日终文档提交自身不计入回顾范围。

## 今日结论

- 今天完成一次 `dev -> master` 阶段集成及配套 CI 门禁修复，随后在 `dev` 连续完成 F4-L-C / D 和 F4-M-A / B / C / D。
- F4-L Wiki 附件隐私与生命周期、F4-M 论坛内容版本完整性与作者恢复均形成服务端权威契约、正式 Web 和成组运行态闭环，两个专题已经关闭。
- F4-M-D 验收发现并修复版本摘要时间双真相与正式 `/me` 缺少退出入口；没有用展示层时区补偿或历史 WebOS 入口掩盖问题。
- F4-N-A 现状审计已确认旧内容赞赏方案存在 Main / Log 跨库事务、`PostAnswer` 定位 / 治理、自定义理由治理三类过期假设。首批收束方向已经获批，完整权威文档修订安排到明日。
- 日终没有遗留未提交业务代码，服务、浏览器会话和临时 PostgreSQL 容器均已停止，临时验收数据已经按专题记录清理。

## 今日全部提交

| 提交 | 主题 | 日终结论 |
| --- | --- | --- |
| `ea46109a` | `fix(ci): 修复发布集成门禁` | 迁移检查改读当前 `SqlSugarScope` 真实配置，SQLite 连接串解析兼容常见格式；Console 与加密依赖完成安全升级。 |
| `f5bf7332` | `feat: 集成七月下旬业务治理与协作成果 (#65)` | Wiki 协作、宠物公开名片、内容治理 / 申诉、用户屏蔽、Wiki 附件和部署迁移编排形成稳定主线集成。 |
| `0d299ed4` | `feat(docs): 接入受保护附件资源加载` | `@radish/http` 认证 Blob、共享 Markdown 受保护资源、object URL 生命周期和 Author / Public / Console 消费链落地。 |
| `169dfb7e` | `fix(wiki): 收口附件权限成组验收` | 六身份、完整文档状态和 PC / mobile Gateway 矩阵通过，修复 Console 固定列遮挡与 PostgreSQL 测试命名配置。 |
| `5c3a0788` | `docs(forum): 定稿内容版本恢复设计` | F4-M 固定完整 Revision、CAS、旧历史兼容、安全恢复和 A-D 边界。 |
| `c3ab5685` | `feat(forum): 建立内容版本恢复权威契约` | Main migration、Post / Comment Revision、完整快照、CAS、恢复 API、稳定错误和 `@radish/http` 契约落地。 |
| `98b8b114` | `feat(forum): 接入内容版本恢复正式页面` | PC / mobile Pencil、正式 Web 版本时间线、对比、恢复、冲突保留、旧历史受权兼容和双语页面落地。 |
| `a10189e8` | `fix(forum): 收口内容版本成组验收` | 多身份 Gateway、双标签 CAS、UTC 摘要时间、正式退出入口、PostgreSQL 17、清理和严格验证收口。 |

## 代码与文档交叉审阅

### 发布集成与 CI

- `DbMigrateInspection` 已以运行中 `ISqlSugarClient.CurrentConnectionConfig` 为检查真相，不再回读可能与宿主不一致的全局默认配置。
- SQLite 文件路径解析已覆盖 `DataSource=...`、`Data Source=...;...`、裸路径和 `:memory:`，并有精确回归测试。
- Console lock 结果为 React / React DOM `19.2.7`、React Router `8.3.0`；OpenIddict SQLite / PostgreSQL migration assembly 的 `System.Security.Cryptography.Xml` 为 `10.0.10`。
- 依赖版本与旧风险分析记录属于当时快照，不追溯改写；当前事实继续以 package 声明、lockfile、依赖审计和 CI 结果为准。

### F4-L Wiki 附件

- `loadAttachmentAssetBlob` 通过 `@radish/http` 统一认证客户端读取二进制；`@radish/ui` 只接收宿主加载函数，不依赖认证或 Wiki API。
- Author、Authenticated / Restricted、Revision 与 Console 使用带 scope 的 object URL；切换账号、文档、Revision、失败或卸载时取消请求并回收 URL。Public + Published 仍使用稳定受控 URL。
- Wiki 专题、文件上传设计、回归索引和 C / D 验收记录与代码一致。日终只更新专题顶部的后续阶段，不改写 F4-L-D 当时“下一步进入 F4-M-A”的历史结论。

### F4-M 论坛内容版本

- 实体真实时间字段为 `CreateTime`，不是草案中的 `CreatedAt`；公开 `VoLastEditedAt` 必须读取当前 `ContentRevision` 对应 Revision 的 UTC `CreateTime`，缺失时稳定返回 `Incomplete`。
- 旧 `GetEditHistory` HTTP 入口最终保留为作者 / Admin / System 受权只读兼容，正式版本弹窗按需展示早期记录；新编辑只写完整 Revision，不再双写旧历史。
- 正式 `/me` 已复用统一 `logout`，账号切换、登出、失权和目标切换共同清除内存 Revision 详情。
- 专题设计补齐实际字段、兼容窗口最终裁决、B / C / D 记录链接和运行态完成结论；论坛编辑历史兼容页同步修正正式页面入口与权限口径。

### HTTP 客户端与规划分层

- client 附件上传 XHR 已从 `getApiBaseUrl()` 收敛到 `getApiClientConfig()`，统一取得 base URL、timeout、token、语言与消息翻译器；`Docs/frontend/http-client.md` 的旧待办已经删除。
- `Docs/planning/current.md` 原有 299 行，大量保存 7 月 11 日以来的历史流水，与“快速入口”职责冲突。本次压缩为当前状态、明日事项、执行入口、维护线和停止线；历史事实继续由 records、archive 和 development plan 承接。
- F4-L / F4-M 长期专题继续保存权威契约，验收命令和临时数据事实只保留在对应记录，不回填到入口文档。

### F4-N-A 审计与明日边界

- 首批目标批准收束为 `Post / Comment`；`PostAnswer` 在回答级通知定位和内容治理适配形成闭环前后置。
- 首批理由批准收束为预设码，不开放自定义文本；后者不能在缺少举报、隐藏和证据链时作为半套用户生成内容上线。
- Main 原子事务只承载 `ContentReward`、双方余额、`TIP` 流水、幂等终态与 Reliable Outbox；Log DB `BalanceChangeLog` 是可靠幂等审计投影，不再宣称跨库同事务。
- 内容赞赏使用专属 Repository，不复用需要支付密码的通用转账；现有转账跨库日志假设作为独立维护债，不在 F4-N 顺手扩张。
- 明日只完成权威设计文档修订和实现范围复核；进入模型、API、migration 或页面前再次汇报并等待批准。

## 今日验证回顾

以下结果来自今天各提交与专题验收记录；日终文档批次不重复执行代码测试或运行态 smoke：

- 发布集成：`npm run validate:ci`、依赖安全、Console type-check / build / 59 项测试、解决方案构建、后端 `1052` 项及 GitHub required checks 通过。
- F4-L：隔离 PostgreSQL 17 migration / Repository `13` 项通过；后端全量 `1052` 项通过、`30` 项环境用例按配置跳过；Console `61` 项、type-check、lint、build 与 Baseline Quick 通过。
- F4-M：后端定向 `29` 项及 migration `3` 项通过；后端全量 `1058` 项通过、`31` 项环境用例按配置跳过；client `466` 项、`@radish/http` `23` 项、type-check、lint、build 与 Baseline Quick 通过。
- F4-L / F4-M 成组验收均完成临时数据、文件、凭据、备份、运行进程和浏览器会话清理；六库完整性与严格 migration verify 通过。
- 本次日终文档提交执行文档链接 / 结构检查、文本卫生、staged diff 检查和 `git diff --cached --check`，不启动服务、不执行浏览器 smoke。

## 明日事项

1. 按已批准裁决完整修订[论坛内容赞赏](/features/forum-content-reward)的状态、用户路径、首版对象、数据模型、Main 事务、Log 审计投影、幂等、资格矩阵、通知和停止线。
2. 明确双方余额的确定性锁顺序、业务日上限的 `BusinessCalendar` 半开区间、同目标唯一约束及 SQLite / PostgreSQL 并发验证。
3. 明确 `ContentRewardReceived` 的 `Reaction` 分类、`ForumPost` 定位、逐笔不聚合与屏蔽抑制；评论定位复用现有 `CommentId`，不提前扩展 Answer 定位。
4. 把 A-D 批次固定为权威设计、服务端与 migration、Pencil / 正式 Web、Gateway 成组验收，并写清每批进入条件、验证矩阵和完成标准。
5. 完成 F4-N-A 文档后先汇报设计差异与预计修改范围，等待批准后再进入 F4-N-B。

## 当前不做

- 今晚不修改 F4-N 模型、Service、Repository、Controller、`@radish/http`、Pencil、页面或 migration。
- 不启动服务、不执行真实 smoke、不安装或更新依赖、不创建 PR、tag、发布或部署。
- 不重启主动生产证据采集，不解冻 Flutter / Tauri，不扩展 WebOS 新功能。
