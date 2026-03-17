# 2026-03 第三周 (03-16 至 03-22)

## 2026-03-16 (周一)

### P5 验收资产补齐启动

- **论坛抽奖 HttpTest 已补齐**：新增 `Radish.Api.Forum.Lottery.http`，覆盖“发帖附带抽奖 -> 列表识别 -> 父评论参与 -> 回复不计入 -> 手动开奖 -> 中奖名单回显”。
- **个人中心浏览记录 HttpTest 已补齐**：新增 `Radish.Api.User.Profile.http`，覆盖帖子 / 商品 / Wiki 详情访问后的浏览记录写入、分页读取与重复访问计数验证。
- **HttpTest 索引已更新**：`HttpTest/README` 已补充抽奖与个人中心脚本入口，保持当前专题分组可检索。

### 最小人工验收顺序沉淀

- **抽奖验收顺序已补文档**：`forum-lottery-mvp.md` 已增加最小人工验收顺序，明确 OIDC 取 token、发帖、评论参与、开奖和浏览记录回看之间的执行关系。
- **当前阶段结论保持克制**：本轮先完成“验收资产补齐”，尚未在文档中提前宣称整轮联调已完成；后续仍需在真实本机环境执行并记录结论。

### P5 联调问题收口

- **开奖通知已补齐**：开奖成功后会向中奖用户发送站内通知，不再出现“中奖结果只在帖子里可见、但用户没有收到提醒”的问题。
- **论坛通知跳转已优化**：点击“帖子被评论 / 回复 / @提及 / 点赞”等论坛通知时，若当前已有论坛窗口则直接复用并跳转；无窗口时才新开。
- **浏览记录口径已修复**：帖子详情与商品详情请求补上鉴权后，个人中心浏览记录已可正常写入帖子、商品与 Wiki 三类对象。
- **中奖通知展示已细化**：前端已为 `LotteryWon` 增加独立通知类型映射、图标与高亮配色，不再混入普通系统通知。
- **相关提交**：`749e8c3`、`91532ef`。

### 本轮阶段判断更新

- **P5 当前结论**：论坛抽奖 MVP 与个人中心浏览记录优化已达到“可演示、可联调、可回归、可转维护”的收口标准。
- **下一步策略调整**：不直接沿用旧版 `M13` 口径启动新阶段，而是先完成规划文档、基础说明与后续路线的重审对齐。

### M13 首轮验证入口补齐

- **统一验证入口已补齐**：根目录新增 `npm run validate:baseline`、`validate:baseline:quick`、`validate:baseline:host`，用于串联当前已有的前端类型检查、`radish.client` 最小测试、Console 权限扫描、后端构建 / 测试与宿主只读自检。
- **验证边界已文档化**：新增 `validation-baseline.md`，明确“日常提交前 / 合并前 / 宿主配置相关改动后”的分层验证建议，并保留 `HttpTest` 为专题回归层。
- **前端最小测试兼容性已收口**：`radish.client` 的 `node --test` 已改为 `--test-isolation=none`，避免受限环境下因子进程隔离导致 `spawn EPERM`。
- **验证链已补齐到 full**：`npm run validate:baseline:quick` 与 `npm run validate:baseline` 均已通过；`full` 已覆盖前端 `type-check`、`radish.client` 最小测试、Console 权限扫描、后端构建与 `Radish.Api.Tests` 195 个测试。
- **回归测试已补边界**：`PostLotteryServiceTest` 已补齐 `INotificationService` 依赖与最小通知断言，避免开奖通知接入后测试构造函数失配。
- **相关提交**：`df37475`。

## 2026-03-17 (周二)

### M13 身份语义防回归扫描落地

- **身份扫描脚本已新增**：根目录新增 `npm run check:identity-claims`，用于扫描运行时代码中回退到原始 Claim 读取、`ClaimTypes` 直接依赖、`ClaimsPrincipal/User.IsInRole(...)` 判断与协议 Claim 字符串散点写法。
- **统一验证入口已接入**：`validate:baseline` 与 `validate:baseline:quick` 已纳入身份语义防回归扫描，不再只覆盖类型检查、最小测试与 Console 权限扫描。
- **误报边界已工程化收口**：协议边界、`Program.cs` 配置入口与 `HttpContextTool` 标准化/兼容层已按白名单排除，避免把允许保留的兼容代码误判为新增回归。
- **验证说明已同步**：`validation-baseline.md` 与身份迁移文档已更新，当前 `M13` 的第一条工程化收口项从“计划中”切换为“已落地可执行”。
