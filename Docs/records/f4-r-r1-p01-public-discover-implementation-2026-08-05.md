# F4-R R1-P01 公开发现成组实现记录

> 日期：2026-08-05（Asia/Shanghai）
>
> 状态：代码侧实现、分层验证与 Gateway PC / mobile 阶段性运行态验收通过；`R1-P01` 已关闭
>
> 范围：Channel 匿名摘要治理、统一公开读模型、Console 管理、`@radish/http`、`/discover` PC / mobile 与对应文档测试

## 1. 交付结论

本批没有把已确认设计降级为单点样式修补，而是先补齐内容来源的权威边界，再让页面只消费一个统一投影：

1. `Channel` 新增 `DiscoverVisibility=Hidden / Summary` 与独立版本，默认 `Hidden`；migration 不按 `ChannelType.Public` 自动开放既有频道。
2. Console 新增频道公开摘要治理页与 `view / manage` 权限；写入使用精确租户、期望版本、原因和 append-only 事件，重复目标状态保持幂等。
3. `PublicDiscover` 从 Chat / Main 当前权威数据读取频道摘要、公开 Wiki 首次发布、当前跨帖神评、帖子与问答，不读取消息正文、私域关系流或历史评论快照。
4. `GET /api/v1/PublicDiscover/GetFeed` 使用 snapshot cutoff、稳定 keyset 游标、`Cache-Control: no-store` 和整流失败关闭；Controller 只注入 `IPublicDiscoverService`。
5. `@radish/http` 提供统一 Vo / 枚举与客户端，`PublicDiscoverApp` 不再并行拼装多个旧列表接口。
6. 页面把原异色模块列表收敛为“首条焦点事件 + 连续编号轨道 + 第三条后嵌入贡献者节点”；PC 保留非对称洞察区，mobile 前置搜索和紧凑入口。

## 2. 治理与隐私边界

- 只有 `Type=Public`、启用、未删除、公共标识合法且显式为 `Summary` 的频道进入匿名投影。
- 关闭摘要后仍可在治理列表和历史中复核，避免失去撤回对象；非 Public 频道不能新开启 Summary。
- 匿名频道 item 只包含名称、描述、频道 ID、最后活动时间与近 `24` 小时有效消息计数，目标仍要求登录。
- `MemberActivity` 要求 Wiki 与 owner 当前均具公开资格；`HighlightedComment` 联动当前 Highlight、Comment、Post 与 User。
- Service 统一清理 HTML、Markdown 与控制字符并按 Unicode 文本元素截断；日志不记录正文、私人身份或完整游标 payload。
- 完整 feed 任一来源失败时返回 `503 / PublicDiscover.SourceUnavailable`，不返回会破坏游标的一半成功页。

## 3. 页面与路由

- 首项无论来源类型都承担焦点事件，避免严格时间序中“最新项不是频道”时失去已确认构图。
- 其余 item 共享编号、时间、标题、摘要、actor 和指标语法，不再重复图标盒、独立底色与逐行箭头。
- 频道、Docs、Forum 帖子 / 评论和贡献者均保留真实 `href`；普通点击交给公共壳层接管，新标签与复制链接仍使用公开真实路径。
- 社区脉搏直接消费同一 snapshot 的 `VoPulse`；Docs、问答、榜单与商城只作为紧凑上下文入口。
- 双语数量使用数值 `count` 决定英文单复数，并用 `formattedCount` 输出当前 locale 数字；中文保持同构资源键。
- 页面只使用现有语义 token。本批不机械修改会影响全部页面族的全局 `guofeng` 品牌 token。

## 4. 代码侧验证

| 验证 | 结果 |
| --- | --- |
| `dotnet build Radish.slnx -c Debug --no-restore` | 通过，`0` warning / `0` error |
| Channel / PublicDiscover / migration / authorization 定向测试 | `57` 通过，`6` 个 PostgreSQL 条件用例因本机未配置而显式跳过 |
| `@radish/http` type-check | 通过 |
| Client type-check / ESLint / production build | 通过；build 仅保留既有 chunk-size warning |
| Client 全量测试 | `489` 通过，`0` 失败 |
| Console type-check / ESLint / production build | 通过 |
| Console 全量测试 | `61` 通过，`0` 失败 |
| Console permission 静态门禁 | 通过 |

PostgreSQL migration 用例没有伪装为已实跑；SQLite migration、仓储资格、精确租户、原子事件、幂等、版本冲突、稳定游标、纯文本映射与整流失败边界均有定向覆盖。

## 5. 运行态验收

当前任务取得启动授权后，通过 `./start.sh` 启动 API、Gateway、Auth、Client 与 Console，并完成以下阶段性验收：

| 验证 | 结果 |
| --- | --- |
| `Radish.DbMigrate apply` 与 migration doctor | `chat.20260805_018_chat_channel_discoverability` 已应用；既有公共频道保持默认 `Hidden` |
| `npm run check:host-runtime -- --details` | Gateway、API、Auth 均返回 `200` |
| Gateway `/discover` 匿名 / 种子管理员登录回流 | 通过；登录后返回 `/discover` 并显示当前账号，feed 仍保持同一公开边界 |
| Public feed 响应与失败关闭 | migration 前真实返回 `503` 且页面进入错误态；migration 后返回 `200` 与 `Cache-Control: no-store` |
| PC / mobile 代表视图 | `1440 × 1000` 与 `390 × 844` 通过；正文和 Console 均无页面级横向溢出 |
| locale / theme | `zh / en`、`default / guofeng` 通过 |
| 公开交互 | Forum / Docs / profile 真实链接、来源返回与复制链接反馈通过 |
| Console 登录与治理只读路径 | OIDC 登录、`view / manage` 资格、2 个默认 Hidden 频道、显式查询、历史抽屉、PC / mobile 导航通过 |
| Console 写入前置门禁 | 未填写原因时确认按钮禁用；本次浏览器复核未提交频道状态变更，版本冲突、幂等、事件与实际写入继续由后端定向测试覆盖 |

运行态发现后端 `long` 按统一 JSON 规则输出为字符串，而前端公开契约误写为 `number`，导致 i18n 复数选择收到字符串并回显资源键。现已把计数契约改为字符串，在显示层使用 `BigInt` 做 locale 格式化并仅传数值复数判别量；修复后 `@radish/http`、Client 定向与全量测试、type-check、lint 和 production build 均通过。

当前机器仍未配置 PostgreSQL 集成测试环境，相关条件用例保持显式跳过；浏览器能力未提供 DPR 覆盖，PC 实际上限为 `1440 × 1000`，与本代表页 `PC 1440` 基准一致。上述限制不阻断 `R1-P01` 关闭，下一顺位进入 `R1-P02 / Public 详情与互动` 的代码事实与设计边界审计。
