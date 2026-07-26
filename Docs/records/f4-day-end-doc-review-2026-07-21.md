# F4 2026-07-21 日终提交回顾与文档审阅

> 日期：2026-07-21（Asia/Shanghai）
>
> 范围：今日 4 个功能与设计提交；本次日终文档提交自身不计入回顾范围。

## 今日结论

- F4-H 电子宠物公开名片按 A-D 完成并关闭：公开资料聚合只返回字段白名单，正式 `/u/:id` 在 PC / mobile 展示可空只读名片，主人 / 访客 / 匿名与显隐、失效、双语和四主题矩阵通过。
- 新增 `./scripts/check-docs.sh` 与 `npm run check:docs`，全量检查 `Docs/` 的 UTF-8、BOM、`U+FFFD`、常见乱码、换行和末尾换行；首次扫描发现的两份历史日志问题已修复。
- F4-I-A 完成圈子关系与内容治理候选复核，选择内容治理案件、证据与动作一致性作为唯一当前专题，并固定数据、权限、接口、页面、迁移和 A-D 停止线。
- F4-I-B 建立 Case / Evidence / Event / UserModerationState 权威契约、五类目标处置、Chat 可靠任务、可靠通知、新用户侧 / Console API 和 View / Review / Action 分权。
- 旧 `Review / ApplyUserAction` 已复用同一用户当前状态写入边界，只作为 F4-I-C 消费者迁移前的兼容入口；下一批不会继续扩展旧举报单模型。

## 今日全部提交

| 提交 | 主题 | 日终结论 |
| --- | --- | --- |
| `374e5baf` | `feat(pet): 完成公开名片服务端契约` | 落地公开宠物 VO、Pet Service 权威读取、公开资料聚合、client 类型和文档编码门禁。 |
| `7043afe1` | `feat(pet): 完成公开名片 Web 与成组验收` | 更新 `P09 / P14`、正式公开名片和静态测试，并完成 Gateway 多身份 PC / mobile 验收与数据清理。 |
| `1de88bbb` | `docs(planning): 确立内容治理案件专题` | 完成候选审计并固定 F4-I 案件、证据、决定、动作、状态、权限、迁移和验收设计。 |
| `28f7ab14` | `feat(moderation): 建立内容治理案件权威契约` | 落地 migration、Repository、Service、可靠任务、通知、API、权限、前端 HTTP 契约与自动化回归。 |

## 代码与文档交叉审阅

### 已确认一致

- `radish-pet-roadmap.md`、`radish-pet-system.md`、Public Web 设计和 API 索引已准确记录 `VoPet` 可空聚合、公开字段白名单、即时隐藏、不进入 JSON-LD / sitemap 和不建立第二读取入口。
- `validation-baseline.md` 已登记 `check:docs` 的扫描范围和阻断项；脚本、npm 入口与文档说明一致。
- F4-I 专题设计与 B 批记录已覆盖 migration、doctor / verify、案件聚合、追加式证据、Case / State 版本、操作幂等、跨 Chat 可靠动作、通知和旧入口过渡边界。
- `current.md` 与 `development-plan.md` 均已把唯一工程顺位指向 F4-I-C，不重启主动生产证据采集，也不并行展开圈子关系或其他候选专题。

### 本次修正的文档漂移

- `api-index.md` 原先没有 F4-I-B 新增的本人举报、案件、证据、决定和纠正动作接口；本次补齐用户侧 / Console 路径、权限、LongId 和迁移期兼容边界。
- Console 权限覆盖矩阵原先只列 `console.moderation.review` 和旧举报接口；本次补 `console.moderation.action`、Case API 及 ReviewCase 的组合授权规则。
- `console-system.md` 与 `console-modules.md` 原先仍把旧举报单、手工动作日志写成唯一当前契约；本次明确服务端新真相源已完成、正式页面仍待 F4-I-C 迁移。
- Console 设计端点原先仍写“等待 F4-I-B”；本次校准为 B 已完成，并明确 `P02 / P07` 先行、页面按案件职责拆分和旧写入口退役顺序。
- 7 月开发日志缺少 2026-07-21 摘要；本次补 F4-H 关闭、文档编码门禁和 F4-I-A-B 完成事实。

## 今日验证回顾

- F4-H-B：后端全量 `966` 项通过、`25` 项环境用例跳过，client type-check 通过。
- F4-H-C：client `453` 项、公开宠物定向 `19` 项、type-check、lint、production build 与 Baseline Quick 通过。
- F4-H-D：Gateway 覆盖主人 / 访客 / 匿名、显隐与失效、`zh / en × PC / mobile` 和四主题代表矩阵；临时数据与凭据清理、六库完整性和严格 verify 通过。
- F4-I-B：后端全量 `973` 项通过、`26` 项环境用例跳过，PostgreSQL 17 migration 专项、解决方案构建、前端类型 / 构建、权限映射、仓库卫生与文档编码检查通过。
- 本次日终文档批次只执行文档检查、仓库卫生和暂存差异检查，不启动服务、不运行 Gateway smoke。

## 明日事项

1. 只推进 `F4-I-C 内容治理案件工作台与本人举报结果页`，设计和页面使用 F4-I-B 已落地的服务端契约，不建立平行模型。
2. 先更新 Console `P02 / P07` 和私域 `/me/reports` 的 PC / mobile Pencil，再修改正式页面；不绕过设计源直接扩展页面结构。
3. Console `/moderation` 迁移到 Case API，按案件队列、详情、证据、决定和动作拆分当前页面职责；覆盖 View / Review / Action 权限态和保留草稿的并发冲突恢复。
4. `/me/reports` 只展示本人举报收件、目标摘要和精简结果；目标失效时保留提交摘要，不扩展撤回、催办、附件或管理员对话。
5. 新消费者清零旧调用后删除 `Review / ApplyUserAction` 写入口；旧读入口只在仍有真实消费者时保留，并补接口退役防回归测试。
6. 完成前端测试、类型检查、lint、两端生产构建、后端定向回归和文档 / 仓库卫生检查；多角色 Gateway 成组验收留到 F4-I-D。

## 当前状态

- F4-H 已关闭，F4-I 已完成 A-B；当前唯一工程顺位是 F4-I-C。
- 今日服务与验收环境均不作为明日启动状态依据；如 F4-I-D 需要真实 smoke，必须在当轮重新说明启动命令、端口和运行影响并获得授权。
- 当前工作区在本次日终文档提交后应保持干净。
