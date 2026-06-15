# P3-10-B8 电子宠物发布候选前批次级回归记录（2026-06-15）

> 本记录承接 [Radish 电子宠物开发计划](/features/radish-pet-roadmap) 与 [P3-10-B8 电子宠物 Phase B 契约与体验补漏记录](/records/p3-10-b8-pet-phase-b-contract-record-2026-06-15)。
>
> 执行时间：2026-06-15 20:52 CST（Asia/Shanghai）

## 批次结论

`P3-10-B8 Phase B` 发布候选前代码 / 测试层回归已完成，未发现需要阻断的服务端数值规则、前端契约或迁移 SQL 缺口。本批发现并补上 `/pet` 登录回流的前端契约测试缺口：`/pet` 现在被显式纳入 `normalizeAuthReturnPath`、`buildPetReturnPath` 和私域入口识别测试，避免后续误删登录恢复能力。

本轮未执行 Gateway PC / 移动真实页面 smoke，原因是 `https://localhost:5000/pet` 当前 5 秒探测超时，未确认本机前后端宿主处于可访问状态；按协作规则未自行启动 `dotnet run` 或 `npm run dev`。

## 覆盖矩阵

| 验证项 | 本轮覆盖 | 结论 |
| --- | --- | --- |
| `/pet` 未登录回流 | `authReturnPath.test.ts` 已补 `/pet` 合法回流、拒绝 query / hash、`buildPetReturnPath` 断言；`entryRoute.test.ts` 已补 `/pet` 私域入口识别 | 通过 |
| 领取与重复领取 | `PetServiceTest` 覆盖未领取创建默认宠物、重复领取返回已有宠物 | 通过 |
| 刷新稳定与只读查询 | `PetServiceTest` 覆盖 `GetMyPetAsync` 不写入主档或流水 | 通过 |
| `feed / clean / play / rest` 四类照顾动作 | `PetServiceTest` 覆盖照顾写入状态和流水；前端 presentation 测试覆盖动作状态展示派生 | 通过 |
| 每日次数 / 冷却 | `PetServiceTest` 覆盖每日上限与冷却；`petPresentation.test.ts` 覆盖次数耗尽、冷却和可用状态展示 | 通过 |
| 最近流水 | `PetServiceTest` 覆盖照顾写流水、未领取日志空态；前端 presentation 测试覆盖流水变化值展示 | 通过 |
| `/me` 宠物摘要入口 | `radish.client` 测试和构建覆盖 `/me` 私域入口仍可识别；本轮未重新做真实页面点击 smoke | 通过自动化，页面待宿主可用时补验 |
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

探测命令：

```bash
curl -k -I --max-time 5 https://localhost:5000/pet
```

结果：5 秒超时，未进入页面 smoke。本轮不写作 Gateway PC / 移动通过。若宿主稍后已由开发者启动，补验入口仍是：

- PC：`https://localhost:5000/pet`，`1920x1080`
- 移动：`https://localhost:5000/pet`，`390x844`
- 种子账号：`test / test123456`

补验重点仍为未登录回流、领取、刷新稳定、四类照顾动作、每日次数 / 冷却、最近流水和 `/me` 宠物摘要入口。

## 后续边界

- 经济消耗、商城物品、社区任务奖励、经验反向加成、Console 配置 UI、首页组件和公开个人主页默认展示继续后置。
- 如果后续 Gateway 页面补验暴露真实问题，按契约测试、服务端数值规则、前端反馈展示和 Gateway 页面路径成组修复。
