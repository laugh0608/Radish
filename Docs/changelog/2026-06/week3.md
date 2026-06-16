# 2026 年 6 月第 3 周开发日志

## 2026-06-15

- `P3-10-B8` 电子宠物 Phase B 首批代码已继续补体验：`/pet` 新增状态洞察、状态等级、照顾动作可用 / 冷却展示、成功反馈和属性变化提示，帮助用户明确当前该做什么与动作执行结果。
- Gateway 体验补漏联调已覆盖 `https://localhost:5000/pet` 的 PC `1920x1080 @ DPR 2` 与移动 `390x844`；移动 DPR 受 Browser 工具限制实际为 `1`，不写作高 DPR 完整通过。
- `/pet` 真实操作已验证一次 `清洁`：页面出现“清洁完成”、成长和属性变化反馈，清洁动作进入冷却，最近照顾流水刷新。
- 宠物后端契约测试完成补强：覆盖重复领取、只读查询不写入、照顾写流水、幂等重放、每日上限、冷却、状态上下限、动作状态和未领取日志空态。
- Phase B 数值规则完成收口判断：当前继续以 `PetService.CareRules` 作为照顾规则单点，不提前引入 Console 配置 UI、经济配置表、商城物品或社区任务奖励；Phase C 前再统一评审配置来源、经济和治理口径。
- 本批记录已补 [P3-10-B8 电子宠物 Phase B 契约与体验补漏记录](/records/p3-10-b8-pet-phase-b-contract-record-2026-06-15)，专题页同步当前结论。
- `P3-10-B8` 发布候选前批次级回归和合并前自动化总验证已完成：后端定向 / 完整测试、`radish.client` 测试 / 类型检查 / 构建、身份契约、迁移 SQL、`validate:baseline`、`validate:identity`、`validate:baseline:host` 和 `git diff --check` 均通过；后续不继续启动经济消耗、商城物品、社区任务奖励、经验反向加成、Console 配置 UI、首页组件或公开个人主页默认展示。
- 本轮不合并 B8，直接进入 `P3-10-B9 用户身份语义与公开索引`。首批已落地 `User.PublicIndex`、公开句柄 `DisplayName#PublicIndex`、注册登录名 / 邮箱规范化、登录名或邮箱登录、公开资料 / 榜单 / 关系链 / 艾特搜索 / Console 用户展示和 PostgreSQL / DbMigrate 迁移入口。
- B9 自动化验证已通过：后端完整测试 `443` 个、B9 定向测试、`radish.client` / `radish.console` 构建、`validate:identity`、`validate:baseline:quick`、`dotnet build Radish.slnx -c Debug`、`git diff --check` 与 repo hygiene 均通过。
- B9 Gateway 运行态页面 smoke 当前未闭合：提权后 `check:host-runtime` 仍显示 API/Auth `5100 / 5200` 未监听，`5000` 由 macOS `ControlCe` 占用并超时；待宿主恢复后补 PC / 移动页面验收。
- 收工前补 [2026-06-15 收工回顾与明日事项](/records/daily-handoff-2026-06-15)：明日先补 B9 运行态验收；若无阻断，再进入 `P3-10-B10 系统设置治理` 的方案与首批低风险能力评审。

## 2026-06-16

- 已完成圈子关系链公开句柄展示对齐和公开标识查询公开资料内容补点，B9 用户身份语义进入维护线；后续只在发布候选、跨端承接或真实缺口暴露时回拉。
- Console 反馈上下文完成修复，避免动态调用 `message` 等 AntD 反馈 API 时脱离 `App` 上下文。
- `P3-10-B10` 系统设置治理首批完成：系统设置从自由 key-value 收敛为代码级设置定义、默认值、覆盖值、风险等级、生效方式和低风险恢复默认入口；Console 只展示已注册设置，旧未注册 JSON 记录不作为运营设置展示。
- `P3-10-B10` 第二批完成：新增系统设置专用变更审计、修改原因 / 确认参数基础和 Console 历史查看入口；`Site.Branding.FaviconUrl` 仍作为首个低风险可编辑示例。
- `P3-10-B10` 第三批完成：新增 `ISystemSettingProvider` 统一读取入口，注册 `Content.PostTitle.MinLength`、`Content.PostBody.MinLength`、`Comment.Body.MinLength`，并将帖子 / 评论发布与编辑路径接入动态最小长度校验。
- Medium 设置编辑已开放受控写入：后端要求修改原因、确认风险等级和确认设置键，High / Critical 仍拒绝编辑；Console 编辑弹窗已补风险提示。
- 今日验证：`dotnet test Radish.Api.Tests` 通过 `456` 个测试，`npm run build --workspace=radish.console` 通过，`git diff --check` 通过。
- B10 Gateway 页面联调未闭合：当前本机 `https://localhost:5000` 不可达，`5000` 被 macOS `ControlCe` 占用，`3000 / 3100 / 5100 / 5200` 未发现 Radish 监听进程；待运行态恢复后补 PC / 移动页面验收。
