# 首版 dev 总回归与发布前检查单

> 本页用于把当前首版 `dev` 收官阶段的回归动作收束成一份可执行清单，避免继续从周志、状态矩阵和会话结论里反推“现在到底还要确认什么”。
>
> 关联文档：
>
> - [首版 dev 边界](/planning/dev-first-scope)
> - [首版 dev 功能矩阵状态表](/planning/dev-first-status-matrix)
> - [当前进行中](/planning/current)
> - [验证基线说明](/guide/validation-baseline)
> - [专题回归索引](/guide/regression-index)
> - [回归结论记录模板](/guide/regression-result-template)
> - [变更回归记录模板](/guide/change-regression-record-template)
> - [首版 dev 总回归记录](/guide/dev-first-regression-record)
> - [部署指南](/deployment/guide)

## 1. 适用时机

以下场景优先使用本页，而不是继续临时组织口径：

- 准备判断“首版 `dev` 是否已达到可发内部开发版状态”
- 大部分业务主线已经收口，只剩工程门槛、总回归和记录整理
- 最近几轮 Smoke 已分别通过，但需要把结果串成一次统一结论
- 准备补阶段汇总、发布前检查单或总回归记录

## 2. 当前执行原则

1. **先自动化，后专题**
   - 先确认 `validate:baseline` 与最小 `CI` 门禁稳定，再看专题 UI / `HttpTest` / 宿主链路。

2. **先复用有效记录，再补真实缺口**
   - 当前阶段不是要求把所有功能从头重测一遍。
   - 若某条主线在本轮首版窗口内已有明确通过记录，且之后没有相关改动，可以直接引用已有记录。

3. **先确认主链可用，再决定是否补边角项**
   - 首版总回归只关注“会不会阻塞内部开发版”的事实，不把次级优化重新拉回当前主线。

4. **只记录事实，不写推测**
   - 通过就写“通过”。
   - 未执行就写“未执行”。
   - 阻塞就写复现条件和影响范围，不写“理论上应该没问题”。

## 3. 建议执行顺序

### 第 1 步：先跑自动化门槛

按改动范围选择：

- 日常回归：`npm run validate:baseline:quick`
- 跨层改动或准备合并前：`npm run validate:baseline`
- 宿主 / 配置 / `DbMigrate` / 部署链改动后：`npm run validate:baseline:host`

同时确认：

- `master` Pull Request 上的 `Repo Hygiene`
- `Frontend Lint`
- `Baseline Quick`

截至 `2026-03-26`，最新一次用于合并的 `master` PR 已完成以上三项检查并成功合并，可作为“首次 `CI/CD` 已完成真实闭环”的当前依据。

若以上门槛不稳定，当前不应继续下“可发内部开发版”的结论。

### 第 2 步：先复用本轮已通过的真实 Smoke

截至 `2026-03-26`，当前可直接引用的已通过记录如下：

| 日期 | 范围 | 当前结论 | 记录位置 |
| --- | --- | --- | --- |
| `2026-03-23` | 通知中心 | 已完成，等待总回归确认 | [2026-03 / week4](/changelog/2026-03/week4) |
| `2026-03-23` | 认证 / OIDC / Gateway 基础入口 | 已完成，等待总回归确认 | [2026-03 / week4](/changelog/2026-03/week4) |
| `2026-03-25` | Docker 镜像构建链与最小交付口径 | 已完成，等待总回归确认 | [2026-03 / week4](/changelog/2026-03/week4) |
| `2026-03-26` | WebOS / 论坛基础 / 社区 P0 / Console V1 / 国风视觉基线 / 主题切换 / i18n | 已完成，等待总回归确认 | [2026-03 / week4](/changelog/2026-03/week4) |

若这些范围自对应日期后没有相关改动，可以直接纳入总回归记录，不必重复补一轮同口径 Smoke。

### 第 3 步：只对本轮触达的专题补回归

若本轮在以下主题上仍有改动，按 [专题回归索引](/guide/regression-index) 补对应专题：

- 聊天室 `P1`
- 商城
- 文档应用 `A`
- 投票 / 问答 / 抽奖
- 萝卜坑
- 经验 / 等级 / 排行榜
- 附件、限流、多租户、权限扫描等专项

原则：

- 没改到，就优先引用已有收口记录
- 改到了，就按专题脚本和最小人工验收顺序补一轮事实回归

### 第 4 步：只有在交付链改动时才补部署链复核

以下情况才需要额外补：

- `Deploy/`
- `Radish.Api / Auth / Gateway / Frontend` Dockerfile
- 证书、`RADISH_PUBLIC_URL`、`OpenIddict__Server__Issuer`
- `docker-compose.local.yml / docker-compose.test.yml / docker-compose.prod.yml`

建议检查：

- `docker compose ... config` 是否仍可展开
- `base + local` 是否仍可完成最小运行态验证
- 若改到生产口径，是否仍满足公开域名、证书和回调地址一致性

参考文档： [部署指南](/deployment/guide)

### 第 5 步：输出统一记录

至少落一份记录，推荐二选一：

- 只记结果：用 [回归结论记录模板](/guide/regression-result-template)
- 要给阶段汇总 / PR / 发布口径复用：用 [变更回归记录模板](/guide/change-regression-record-template)

当前已落一份真实记录：

- [首版 dev 总回归记录（2026-03-26）](/guide/dev-first-regression-record)

同时回写：

- 周志
- 当前规划页
- 状态矩阵

避免“实际已经通过，但规划页还停在待复核”再次出现。

## 4. 首版发布前最小检查单

### 当前判断（2026-03-26）

- 结论：`可发内部开发版`
- 判断依据：
  - `validate:baseline` 与 `validate:baseline:host` 已通过
  - 最新一次 `master` PR 的 `Repo Hygiene`、`Frontend Lint`、`Baseline Quick` 已全绿并成功合并
  - 首版范围内的业务、体验与最小工程门槛已在状态矩阵中收口为 `已完成`
  - 当前无已知阻塞主线的 `P0 / P1` 问题
- 不纳入本结论的后续项：
  - 真实外部反代域名、Auth 证书与 OIDC 回调链路联调记录
  - 更大范围上线前交付复核

### A. 工程门槛

- [x] `npm run validate:baseline` 通过
- [x] 如涉及宿主 / 配置 / `DbMigrate` / 部署链，`npm run validate:baseline:host` 通过
- [x] 最近一次 `master` PR 的 `Repo Hygiene`、`Frontend Lint`、`Baseline Quick` 正常
- [x] 当前没有已知阻塞主线的 `P0 / P1` 问题

### B. 当前已收口主线

- [x] 认证 / OIDC / Gateway 基础入口有有效通过记录
- [x] WebOS 桌面与应用容器有有效通过记录
- [x] 论坛基础有有效通过记录
- [x] 社区 `P0` 有有效通过记录
- [x] `Console V1` 有有效通过记录
- [x] `radish.client` 国风视觉基线 / 主题切换 / `i18n` 有有效通过记录
- [x] 通知中心有有效通过记录
- [x] Docker 镜像构建链与最小交付口径有有效通过记录

### C. 本轮如有触达则补验

> 当前内部开发版判断未新增这些专题改动，继续复用既有通过记录，不额外作为本轮阻塞项。

- [ ] 聊天室 `P1`
- [ ] 商城
- [ ] 文档应用 `A`
- [ ] 投票 / 问答 / 抽奖
- [ ] 萝卜坑
- [ ] 经验 / 等级 / 排行榜
- [ ] 附件 / 限流 / 多租户 / 其他专项

### D. 记录与结论

- [x] 已补总回归记录
- [x] 已更新周志或阶段汇总
- [x] 已更新规划页 / 状态矩阵
- [x] 当前结论可明确写成：`可发内部开发版` / `继续观察` / `仍有阻塞`

## 5. 推荐记录格式

如果本轮是“首版总回归”而不是某个单独专题，建议直接按下面结构写：

```md
## 首版 dev 总回归记录

- 记录日期：YYYY-MM-DD
- 记录人：<name>
- 范围：首版 dev 总回归

### 自动化执行

- `npm run validate:baseline`：通过 / 阻塞 / 未执行
- `npm run validate:baseline:host`：通过 / 阻塞 / 未执行
- `master` PR 最小 CI 门禁：正常 / 异常 / 未确认

### 直接引用的已有通过记录

- 2026-03-23：通知中心 Smoke
- 2026-03-23：认证 / OIDC / Gateway 基础入口 Smoke
- 2026-03-25：Docker 最小交付链 Smoke
- 2026-03-26：WebOS / 论坛基础 / 社区 P0 / Console / 体验主线 Smoke

### 本轮新增补验

- <如有，写专题与入口；如无，写“无”>

### 结论

- 可发内部开发版 / 继续观察 / 仍有阻塞

### 遗留项

- <none or remaining blockers>
```

## 6. 当前阶段的退出条件

当前首版 `dev` 可以考虑进入“可发内部开发版”判断，至少满足以下条件：

1. `状态矩阵` 中首版范围内的主线均已有明确结论，不再存在业务 / 体验主线层面的“待联调复核”。
2. `验证基线` 与最小 `CI` 门禁稳定，没有反复波动。
3. 当前没有阻塞主链路的已知 `P0 / P1` 问题。
4. 总回归记录、周志与规划页口径一致，不再依赖会话口头结论。

截至 `2026-03-26`，以上条件当前均已满足，因此本轮判断已更新为：`可发内部开发版`。

若以上任一项不满足，本页的结论应保持为“继续观察”或“仍有阻塞”，而不是提前下发布判断。
