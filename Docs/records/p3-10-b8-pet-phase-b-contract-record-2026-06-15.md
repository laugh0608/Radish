# P3-10-B8 电子宠物 Phase B 契约与体验补漏记录（2026-06-15）

> 本记录承接 `P3-10-B8 Radish 电子宠物规划`。
>
> 本批围绕 `/pet` 登录态私域主链路，收口首批体验补漏、后端契约测试、数值规则位置和 Gateway 真实页面复核。

## 批次结论

`P3-10-B8 Phase B` 的私域主链路当前具备继续进入发布候选前批次级回归的条件。首批代码已完成领取、命名、四类照顾动作、状态变化流水、`/me` 摘要入口和本地迁移差异 SQL；本批追加状态洞察、冷却展示、照顾反馈和后端契约测试，未引入经济消耗、商城物品、社区任务奖励、经验反向加成、Console 配置 UI、首页组件或公开个人主页默认展示。

## 变更摘要

| 项目 | 结果 |
| --- | --- |
| `/pet` 体验补漏 | 已补状态洞察、状态等级、动作可用 / 冷却展示、照顾成功反馈和属性变化提示 |
| 后端契约测试 | 已补重复领取、只读无写入、照顾写日志、幂等重放、每日上限、冷却、状态上下限、动作状态和未领取日志空态 |
| 数值规则位置 | Phase B 继续以 `PetService.CareRules` 作为照顾规则单点；暂不引入 Console 配置、经济配置表或商城物品配置 |
| 迁移入口 | 测试 / 生产上线前仍以 `Deploy/sql/20260615_add_pet_tables.sql` 作为版本化差异 SQL 审核入口 |

## 数值规则收口

当前照顾规则只在后端 `Radish.Service/PetService.cs` 的 `CareRules` 单点维护：

| 动作 | 每日次数 | 冷却 | 状态变化 | 成长 |
| --- | --- | --- | --- | --- |
| `feed` | 3 | 30 分钟 | 饱食度 `+24`，精力 `-4` | `+5` |
| `clean` | 3 | 30 分钟 | 清洁度 `+25`，精力 `-4` | `+4` |
| `play` | 3 | 45 分钟 | 饱食度 `-6`，清洁度 `-5`，精力 `-14` | `+6` |
| `rest` | 2 | 60 分钟 | 饱食度 `-2`，精力 `+28` | `+3` |

阶段判断、心情判断和状态上下限仍由服务端计算。前端只展示服务端返回的动作状态，并做状态洞察 / 冷却剩余时间等展示派生。

本批不把规则提前迁移为数据库配置，原因是 Phase B 尚不接入经济、物品、任务奖励或 Console 配置 UI。进入 Phase C 前再统一评审配置来源、后台查询和治理口径，避免现在引入半成品配置表。

## 自动化执行

执行目录：仓库根目录 `/Users/luobo/Code/Radish`

```bash
dotnet test Radish.Api.Tests --filter FullyQualifiedName~PetServiceTest
dotnet test Radish.Api.Tests
npm run validate:identity
npm run test --workspace=radish.client
npm run type-check --workspace=radish.client
npm run build --workspace=radish.client
npm exec --workspace=radish.client -- eslint src/pet/PetApp.tsx src/pet/petPresentation.ts tests/petPresentation.test.ts
git diff --check
npm run check:repo-hygiene:changed
npm run check:repo-hygiene:staged
```

结果：

- `PetServiceTest` 定向测试通过，`10` 个测试通过。
- `Radish.Api.Tests` 完整测试通过，`439` 个测试通过。
- `validate:identity` 通过，身份语义扫描、LongId 字符串安全扫描和身份语义定向测试均未发现回归。
- `radish.client` 测试、类型检查、构建和本批前端文件 ESLint 均通过。
- `git diff --check`、changed 文本卫生检查和 staged 文本卫生检查通过。

## Gateway 联调

执行入口：`https://localhost:5000/pet`

账号状态：沿用本地浏览器登录态，页面宠物为 `联调萝卜`。

覆盖结果：

- PC `1920x1080 @ DPR 2`：页面正常渲染宠物状态、状态洞察、三项状态等级、照顾动作、冷却 / 可用标签和最近照顾；未发现横向溢出或局部文本溢出。
- 移动 `390x844`：页面正常渲染关键内容、状态等级、照顾动作和最近流水；未发现横向溢出或局部文本溢出。受当前 Browser 工具限制，本次移动 DPR 实际为 `1`，不写作高 DPR 完整通过。
- 动作验证：执行一次 `清洁` 后，页面出现 `清洁完成` 反馈，展示成长和属性变化，`清洁` 动作转入冷却，最近照顾流水刷新。

## 观察项

- 本地浏览器曾出现旧 refresh token `invalid_grant` 日志，但页面随后正常加载并完成 `/pet` 接口交互；当前不归类为电子宠物代码回归。
- `npm run lint --workspace=radish.client` 仍会被既有 forum/public 文件 lint 问题阻断，本批只对修改范围执行了定向 ESLint。

## 后续边界

- 发布候选前再补一次批次级回归，覆盖 `/pet` 未登录回流、领取、刷新稳定、四类照顾动作、每日次数 / 冷却、`/me` 摘要和迁移 SQL。
- 经济消耗、商城物品、社区任务奖励、经验反向加成、Console 配置 UI、首页组件和公开个人主页默认展示继续后置，不作为 Phase B 当前开发默认项。
