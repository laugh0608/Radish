# 2026 年 7 月第 2 周开发日志

## 2026-07-06 至 2026-07-10 阶段摘要

- 07 月 06 日完成 E4-B 隐私安全边界、E5-A 可恢复错误与诊断复制、E5-B 受控写入验证，并对 E6 进入判断做了纠偏。
- 07 月 07 日完成 E7 Console 后台调度台、Public 内部术语与信息密度、Auth 授权确认页层级收口。
- 07 月 08 日完成 E8 产品形态差距回拉，统一正式 Web 导航、页面滚动、聊天入口、移动导航与对外页面口径。
- 07 月 10 日完成发布工程成熟度与安全收口审计，形成 Q0 安全与暴露面实施方案，为 07 月 11 日成组实施提供边界。

## 2026-07-11

### 今日提交回顾

2026-07-11 在 `dev` 共完成 9 个提交，从 Pre-RC 安全与产品收口正式进入 P3-12-F Release Go 工程：

- `d8b8cdad fix(security): 收口 Q0 依赖与生产暴露面`：恢复 npm / NuGet 依赖安全门禁，清理正式发布矩阵中的性能、Weather、演示和测试暴露面。
- `f88bd6b9 fix(security): 收紧身份验证与传输安全`：API JWT 启用 `radish-api` audience，清理完整 Claims 日志，固定 Auth 明文传输和 Gateway 可信代理边界。
- `c2d094b9 fix(ui): 统一 Markdown 链接协议防护`：阅读态、富文本导入 / 建链 / 导出共用链接协议白名单，危险 scheme 统一退化为不可点击文本。
- `e62cb810 feat(web): 收口 E8-B 产品矩阵与集成门禁`：完成七项有限产品矩阵、公开 Docs 服务端契约、举报链路阻断修复与 dev -> master 集成材料；PR `#58` 后续合并为 `c5906604`。
- `738b3f67 fix(client): 修复公开承诺路由 lint`：消除集成前的公开承诺路由 lint 阻断。
- `33e4690f feat(reliability): 治理事务后可靠任务`：用源库 Outbox + Hangfire 治理奖励、经验、通知等不可丢失的事务后写，补齐幂等、重试、DeadLetter 和受权人工重放。
- `86466308 fix(reliability): 补齐候选级数据库验证`：完成 PostgreSQL 双 Worker、租约恢复、DbMigrate 重入和真实 Hangfire 恢复验证，同时修复 Chat 种子重入与可靠任务权限契约。
- `873c5ea5 feat(api): 统一错误契约与异常治理`：保留 `MessageModel` 兼容边界，建立全局异常处理、稳定错误码、`TraceId / X-Correlation-ID`、真实 HTTP 状态和客户端诊断保留。
- `ef370884 feat(api): 治理文件访问令牌安全边界`：完成原始 token 一次返回、原列 hash、历史 token 迁移、原子消费 / 撤销、权限、可信代理与日志凭据脱敏。

### 今日验证与阶段结论

- Q0 成组运行态、E8-B PC / mobile 有限矩阵和 PR `#58` 集成已完成，当前没有已知 `P0/P1`。
- Q1-A 已通过 PostgreSQL、DbMigrate 重入和真实 Hangfire 恢复验证；Q1-B 已通过后端、HTTP 客户端和 Console 类型检查。
- Q1-C 解决方案构建为 `0 warning / 0 error`，完整后端测试为 `597 passed / 2 skipped / 0 failed`；其中 Q1-C PostgreSQL 双 Worker 用例因未配置环境连接而明确跳过。
- 当前正式子阶段已从 `P3-12-E8 Pre-RC` 切换到 `P3-12-F 正式版发布候选`。

### 明天事项（2026-07-12）

1. **关闭 Q1-C 环境门禁**
   - 备份 Main 数据库并记录迁移前 `FileAccessToken` 行数与旧格式行数。
   - 执行 `dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- apply`，紧接着执行 `verify`，核对历史 token 已原位转为 hash，且无异常格式或重复 hash。
   - 在可用 PostgreSQL 16 环境配置 `RADISH_TEST_POSTGRES_CONNECTION_STRING`，补跑 `FileAccessTokenPostgresIntegrationTest`，确认双 Worker 并发不突破访问次数。
2. **正式进入 Q2-A 时间语义治理**
   - 全仓盘点 `DateTime.Now / Today / UtcNow`、SQL 默认时间、Hangfire 调度、过期 / 冷却 / 签到 / 奖励 / 统计窗口，按“持久化时间 / 跨服务契约 / 过期与冷却 / 业务自然日 / 纯展示”分类。
   - 形成 UTC 持久化、业务时区、用户展示时区和 `TimeProvider` 注入契约，同时明确历史本地时间数据的识别、转换、兼容和前滚停止线。
   - 优先纳入文件 token、Outbox 租约 / 重试、通知、奖励 / 签到、会话过期等高风险链路，定义数据迁移和边界测试矩阵。
   - Q2-A 属于时间契约与历史数据迁移；先提交完整子方案并确认，确认后按高风险链路成组实施，不做全仓机械替换。
3. **保持 Release Go 顺序**
   - Q2-A 后继续 Q2-B PostgreSQL / OpenIddict 生产相似升级、schema ledger 和备份 / 前滚材料，再进入 Q2-C 版本单一真值。
   - 不启动 Q3、页面调整、tag、部署或无关重构；候选运行态 smoke 仍需当轮确认服务已启动。
