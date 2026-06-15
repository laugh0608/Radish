# P3-10-B8 电子宠物发布候选前批次级回归记录（2026-06-15）

> 本记录承接 [Radish 电子宠物开发计划](/features/radish-pet-roadmap) 与 [P3-10-B8 电子宠物 Phase B 契约与体验补漏记录](/records/p3-10-b8-pet-phase-b-contract-record-2026-06-15)。
>
> 执行时间：2026-06-15 20:52 CST（Asia/Shanghai）
>
> Gateway 补验时间：2026-06-15 21:25 CST（Asia/Shanghai）

## 批次结论

`P3-10-B8 Phase B` 发布候选前批次级回归已完成，未发现需要阻断的服务端数值规则、前端契约、迁移 SQL 或 Gateway 页面路径缺口。本批发现并补上 `/pet` 登录回流的前端契约测试缺口：`/pet` 现在被显式纳入 `normalizeAuthReturnPath`、`buildPetReturnPath` 和私域入口识别测试，避免后续误删登录恢复能力。

开发者启动前后端后，已通过 Gateway `https://localhost:5000` 补验 PC / 移动真实页面。`/pet` 未登录访问会进入 `Radish 统一登录`，种子账号 `test / test123456` 登录后可回流到 `/pet`；已登录页面覆盖刷新、四类照顾动作、每日次数 / 冷却、最近流水和 `/me` 宠物摘要入口。

## 覆盖矩阵

| 验证项 | 本轮覆盖 | 结论 |
| --- | --- | --- |
| `/pet` 未登录回流 | `authReturnPath.test.ts` 已补 `/pet` 合法回流、拒绝 query / hash、`buildPetReturnPath` 断言；`entryRoute.test.ts` 已补 `/pet` 私域入口识别；Gateway 经官方登出端点清理会话后访问 `/pet`，进入 `Radish 统一登录`，登录后回到 `/pet` | 通过 |
| 领取与重复领取 | `PetServiceTest` 覆盖未领取创建默认宠物、重复领取返回已有宠物 | 通过 |
| 刷新稳定与只读查询 | `PetServiceTest` 覆盖 `GetMyPetAsync` 不写入主档或流水；Gateway 刷新后状态与流水稳定，更新时间正常刷新 | 通过 |
| `feed / clean / play / rest` 四类照顾动作 | `PetServiceTest` 覆盖照顾写入状态和流水；前端 presentation 测试覆盖动作状态展示派生；Gateway 已依次执行喂食、清洁、互动、休息 | 通过 |
| 每日次数 / 冷却 | `PetServiceTest` 覆盖每日上限与冷却；`petPresentation.test.ts` 覆盖次数耗尽、冷却和可用状态展示；Gateway 展示喂食 / 互动冷却、清洁 / 休息今日用完 | 通过 |
| 最近流水 | `PetServiceTest` 覆盖照顾写流水、未领取日志空态；前端 presentation 测试覆盖流水变化值展示；Gateway 最近照顾按动作追加并置顶 | 通过 |
| `/me` 宠物摘要入口 | `radish.client` 测试和构建覆盖 `/me` 私域入口仍可识别；Gateway `/me` 展示 `联调萝卜`、心情和阶段，`电子宠物` 链接可回到 `/pet` | 通过 |
| 迁移入口 | `Deploy/sql/20260615_add_pet_tables.sql` 与实体字段核对，并在 SQLite 临时库重复执行两次，`PetProfile` / `PetStatLog` 表和索引均可落下 | 通过 |

## 自动化执行

执行目录：仓库根目录 `/Users/luobo/Code/Radish`

```bash
dotnet test Radish.Api.Tests --filter FullyQualifiedName~PetServiceTest
npm run test --workspace=radish.client
npm run type-check --workspace=radish.client
npm run lint:changed
dotnet test Radish.Api.Tests
npm run build --workspace=radish.client
npm run validate:ci
npm run validate:identity
git diff --check
npm run check:repo-hygiene:changed
sqlite3 /private/tmp/radish-pet-schema-check-20260615.db ".read Deploy/sql/20260615_add_pet_tables.sql"
sqlite3 /private/tmp/radish-pet-schema-check-20260615.db ".schema PetProfile"
sqlite3 /private/tmp/radish-pet-schema-check-20260615.db ".schema PetStatLog"
```

结果：

- `PetServiceTest` 定向测试通过，`10` 个测试通过。
- `radish.client` 测试通过，补测后 `252` 个测试通过。
- `radish.client` 类型检查通过。
- `lint:changed` 命中 `2` 个前端测试文件，通过。
- `Radish.Api.Tests` 完整测试通过，`439` 个测试通过。
- `radish.client` 生产构建通过。
- `validate:ci` 通过，覆盖 `5` 个变更文件；因 `Docs/planning/current.md` 命中默认执行面文档 / 门禁资产，已自动追加 `validate:identity`。
- `validate:identity` 通过，身份语义扫描、LongId 字符串安全扫描和身份语义后端定向测试均未发现回归。
- `git diff --check` 通过。
- `check:repo-hygiene:changed` 通过，已检查 `5` 个变更文件，未发现文本卫生问题。
- 迁移 SQL 在临时 SQLite 库中可重复执行，两个表和五个索引均可创建。

## Gateway 状态

补验入口：

- PC：`https://localhost:5000/pet`，`1920x1080`
- 移动：`https://localhost:5000/pet`，`390x844`
- 种子账号：`test / test123456`

补验结果：

- `/pet` 未登录回流：先访问 `https://localhost:5000/connect/endsession?post_logout_redirect_uri=https%3A%2F%2Flocalhost%3A5000&client_id=radish-client` 清理官方登录会话，再访问 `/pet`，进入 `Radish 统一登录`；登录 `test / test123456` 后回到 `/pet`。
- PC `/pet`：`联调萝卜` 页面渲染正常，刷新后状态与流水稳定，更新时间从 `2026-06-15 21:13:52` 更新到 `2026-06-15 21:16:29`，三项状态保持 `86 / 100 / 72`。
- PC 四类照顾动作：喂食后饱食度 `86 -> 100`、精力 `72 -> 68` 并进入冷却；清洁后精力 `68 -> 64` 且今日用完；互动后饱食度 `100 -> 94`、清洁度 `100 -> 95`、精力 `64 -> 50` 并进入冷却；休息后饱食度 `94 -> 92`、精力 `50 -> 78` 且今日用完。
- 最近流水：`小萝卜吃饱了一些。`、`小萝卜变清爽了。`、`小萝卜玩得很开心。`、`小萝卜恢复了精力。` 均按动作追加并置顶，成长变化分别展示 `+5 / +4 / +6 / +3`。
- `/me` 摘要入口：PC 与移动均展示电子宠物摘要，包含 `联调萝卜`、心情 `开心` 和阶段 `舒展期`；`/me` 的 `电子宠物` 链接可回到 `/pet`。
- 移动 `/pet`：`390x844` 视图无横向溢出，动作冷却 / 今日用完、宠物资料和最近流水可阅读。
- 控制台：本轮 Gateway 页面补验未出现页面级 `error` / `warning`。

工具限制：

- Browser 视口能力只能设置宽高，不能强制 DPR；移动 `390x844` 实测 DPR 为 `1`，不写作 `DPR 3` 通过。
- `https://127.0.0.1:5000` 仅作为隔离登录态的辅助入口尝试，不是本项目文档约定的 Gateway / OIDC 入口；正式结论以 `https://localhost:5000` 为准。

## 后续边界

- 经济消耗、商城物品、社区任务奖励、经验反向加成、Console 配置 UI、首页组件和公开个人主页默认展示继续后置。
- 如果后续发布候选总回归暴露真实问题，按契约测试、服务端数值规则、前端反馈展示和 Gateway 页面路径成组修复。
