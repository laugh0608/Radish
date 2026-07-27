# F4 2026-07-27 日终提交回顾与文档审阅

> 日期：2026-07-27（Asia/Shanghai）
>
> 范围：按提交日期统计的 8 个当日提交；本次日终文档提交自身不计入回顾范围。

## 今日结论

- 今天先完成候选质量门禁前移和 PostgreSQL 旧结构 migration 修复，再连续完成 F4-N 内容赞赏 A-D 与 F4-O 回答生命周期 A/B。
- Candidate Quality 已进入 `PR -> master` required check，不再等到 Docker 镜像发布才首次暴露 PostgreSQL migration 与完整候选回归问题。
- F4-N 已关闭：Post / Comment 固定 `1 胡萝卜` 赞赏具备 Main 原子资产事务、Log 幂等投影、Reliable Outbox、正式 Web、Pencil 与 Gateway 成组验收。
- F4-O-B 已把 `PostAnswer` 建成具备 PublicId、独立 Revision、附件引用、CAS 生命周期、采纳事件、治理申诉和可靠通知的服务端权威对象。
- 日终代码审阅发现两个需明确延续的契约问题：回答创建仍兼容 `PostId`，migration strict verify 尚未覆盖采纳一致性、可见回答数、附件归属和索引检查。它们已排入明天 B -> C 复核，不以“B 已完成”措辞掩盖。
- 日终没有遗留未提交业务代码，也没有再次启动服务或执行浏览器 smoke。

## 今日全部提交

| 提交 | 主题 | 日终结论 |
| --- | --- | --- |
| `dd173431` | `fix(ci): 前移候选质量门禁` | Candidate Quality 接入 `master` 保护与 PR 工作流；测试配置隔离和数据库连接说明同步收口。 |
| `dab4a913` | `fix(db): 修复 PostgreSQL 旧结构迁移` | Notification Inbox 与 Wiki Attachment migration 兼容 PostgreSQL 旧列 / 旧约束结构。 |
| `7346e163` | `fix(ci): 前移候选质量门禁 (#66)` | PR `#66` merge commit；合入前两项 CI / migration 修复，不额外引入文件差异。 |
| `787dc927` | `docs(forum): 定稿内容赞赏权威设计` | 固定 Post / Comment 首批边界、Main / Log 资产审计关系、预设理由、可靠通知和 A-D 批次。 |
| `5b4c5ba6` | `feat(forum): 实现内容赞赏服务端闭环` | 专属 Repository / Service、Main 资产事务、Log 投影、migration、API、幂等和通知落地。 |
| `c18acf8c` | `feat(forum): 完成内容赞赏正式 Web 闭环` | Pencil、PC / mobile 正式 Web、双语主题、登录回流与 Gateway 成组验收完成，F4-N 关闭。 |
| `f5c17ac0` | `docs(forum): 确立问答回答生命周期专题` | 从多个候选中选定 F4-O，固定回答权威模型、事务、治理、通知、页面和 A-D 停止线。 |
| `cf585685` | `feat(forum): 建立问答回答生命周期权威` | 完成 F4-O-B 模型、Repository / Service、migration、治理申诉、通知、API、`@radish/http` 和专项回归。 |

## 按代码反查文档

### 已保持一致

- `Docs/guide/validation-baseline.md` 已准确表达 Candidate Quality 是 `PR -> master` required check、保留手动与镜像发布复用且不再定时运行。
- F4-N 专题、萝卜币系统 / 路线图、Pencil 入口和 D 批验收记录已经与实现及运行态事实一致。
- `Docs/planning/current.md`、开发路线图和发布后功能完成线已经把工程顺位切换到 F4-O-C。

### 本次修正

- F4-O 权威文档把 A 批“现有代码事实”改为明确历史基线，避免 B 完成后继续被读成当前缺口。
- F4-O API 表改为实际 `/api/v1/Question/{Action}` 路径；删除未实现的 `GetSummary` 草案，说明分页响应直接携带解决态与采纳摘要。
- F4-O migration 章节区分“B 批当前 Verify”与“专题关闭前 strict verify 目标”，保留未完成检查的真实状态。
- F4-N 文档说明 F4-O-B 只完成回答级服务端前置，正式 Web 与成组验收完成前不自动扩展内容赞赏目标。
- 早期问答 MVP 文档进一步声明其模型和 API 只用于历史追溯，现行权威统一转向 F4-O。

## 明天事项

1. 获得批准后，先做 F4-O B -> C 契约复核：
   - 决定回答创建从兼容 `PostId` 收敛到 `postIdentifier` 的方式；
   - 固定 migration strict verify 对采纳一致性、`AnswerCount`、附件归属和索引的检查边界。
2. 更新 PC / mobile 权威 Pencil，覆盖回答分页、作者动作、采纳替换 / 撤销、历史恢复、举报、失效和通知定位。
3. 在正式 `/forum/post/:postPublicId` 接入 `@radish/http` 回答契约，移除页面一次性加载全部回答和旧写入入口依赖。
4. 完成四主题、双语、键盘 / 焦点、窄屏、长正文、附件、CAS 冲突与不可用态的代码侧验证。
5. F4-O-D 之前不启动服务、不做 Gateway smoke；进入 D 批时重新说明命令、端口、运行影响和清理方式并取得授权。

## 验证与留痕边界

- F4-N 的运行态证据、数据清理和严格 migration verify 已单独记录在 [F4-N-D 成组验收](/records/f4-n-d-forum-content-reward-stage-acceptance-2026-07-27)。
- F4-O-B 本地验证：全仓 .NET Debug 构建 `0 warning / 0 error`；后端测试 `1105` 项中 `1072` 通过、`33` 项 PostgreSQL 环境测试按条件跳过；`validate:baseline:quick`、`@radish/http` 测试和仓库卫生检查通过。
- PostgreSQL 条件测试不伪报为本地已执行；由具备数据库服务的候选门禁补齐。
- 本次日终只修改文档，不执行运行态验证，不新增依赖。

## 明日启动口径

新会话先读 [当前进行中](/planning/current)，再读 [F4-O 权威设计](/features/forum-answer-lifecycle-governance-design) 的第十、十二和十三节。先汇报 B -> C 契约复核结论与 Pencil 修改范围，获得批准后再进入正式页面实现。
