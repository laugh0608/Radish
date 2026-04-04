# 身份语义防回归回归手册

> 本文用于回答一个执行问题：**当改动触达身份语义、Claim 读取、Auth 协议输出或官方协议消费者时，默认必须跑什么、怎么记、哪些适合上 CI？**
>
> 关联文档：
>
> - [身份语义收敛迁移计划](/guide/identity-claim-migration)
> - [身份语义收敛与 Claim 治理设计](/architecture/identity-claim-convergence)
> - [身份语义 Phase 4 协议消费者矩阵](/guide/identity-claim-protocol-consumers)
> - [身份语义 Phase 4 实施与回滚窗口](/guide/identity-claim-phase4-rollout-window)
> - [验证基线说明](/guide/validation-baseline)
> - [专题回归索引](/guide/regression-index)

## 1. 目的

`Phase 5` 的目标不是再补一份“应该注意什么”的泛化说明，而是把身份语义相关改动后的默认动作固化成同一套工程口径：

- 自动化入口有统一命令；
- 协议专题回归有明确顺序；
- 结果记录有固定模板；
- 本地 baseline 与 CI 的边界明确，不再靠口口相传。

## 2. 哪些改动命中本手册

以下任一场景，默认视为“身份语义相关改动”：

- 修改 `CurrentUser`、`ICurrentUserAccessor`、`ClaimsPrincipalNormalizer`、`UserClaimReader`、`IHttpContextUser`
- 修改 `UserClaimTypes`、`UserRoles`、`UserScopes`、`AuthorizationPolicies`
- 修改 `Radish.Auth` 中的 `AccountController`、`AuthorizationController`、`UserInfoController`
- 修改前端 Token 解析、OIDC 回调、用户名 / 租户 / 角色兜底逻辑
- 修改 `Radish.Api.AuthFlow.http`、`/connect/token`、`/connect/userinfo` 相关契约说明
- 修改身份语义回归规则、扫描白名单或相关 CI 门禁

## 3. 默认必须跑什么

### 3.1 最低要求

只要命中本手册范围，默认至少执行：

```bash
npm run validate:baseline
npm run validate:identity
```

职责分工：

- `validate:baseline`
  - 继续承担仓库默认跨层基线
  - 确认前端类型、最小测试、Console 权限扫描、身份语义总扫描、后端 build/test 无阻塞
- `validate:identity`
  - 只聚焦身份语义专题
  - 分别执行运行时散点 Claim 读取扫描与协议输出回退扫描
  - 执行身份语义后端定向测试：`ClaimsPrincipalNormalizerTests`、`HttpContextUserTests`、`AccountControllerTest`、`AuthorizationControllerTest`、`UserInfoControllerTest`

### 3.2 触达协议输出或协议消费者时

若本轮改动触达以下任一范围：

- `Radish.Auth` 协议输出侧
- `userinfo`
- 官方客户端 Token 解析
- `Radish.Api.AuthFlow.http`
- 协议消费者矩阵中的直接消费者

则在自动化之外，必须再补：

1. `Radish.Api.AuthFlow.http`
2. 官方顺序回归记录：`radish-client -> radish-console -> Radish.Api.AuthFlow.http -> radish-scalar`

原因：

- 自动化只能回答“代码层是否回退”；
- 无法替代真实授权码流程、`userinfo`、refresh token 与官方消费者闭环。

### 3.3 触达宿主 / 配置 / 种子时

若本轮还同时触达以下内容：

- 宿主配置
- `DbMigrate`
- OpenIddict 种子
- 部署 / 反向代理 / 运行时配置

则再追加：

```bash
npm run validate:baseline:host
```

## 4. 当前命令清单

```bash
npm run check:identity-claims
npm run check:identity-runtime
npm run check:identity-protocol-output
npm run validate:identity
```

含义：

- `check:identity-claims`
  - 兼容总入口
  - 一次性执行“运行时散点 Claim 读取 + 协议输出回退”双重守卫
- `check:identity-runtime`
  - 只扫描运行时代码是否重新长出 `FindFirst / FindAll / ClaimTypes / User.IsInRole / 原始 Claim 字符串`
- `check:identity-protocol-output`
  - 只扫描 `Radish.Auth` 协议输出侧是否回退到历史双写承诺
- `validate:identity`
  - 身份语义专题聚合入口
  - 默认用于“改了身份语义就跑什么”的自动化答案

## 5. 专题回归顺序

若需要补协议闭环或官方消费者回归，继续沿用当前冻结顺序：

1. `radish-client`
2. `radish-console`
3. `Radish.Api.AuthFlow.http`
4. `radish-scalar`

其中 `Radish.Api.AuthFlow.http` 至少确认：

- 授权码可获取
- `/connect/token` 可换取 `access_token / refresh_token`
- `/connect/userinfo` 返回结构仍稳定
- refresh token 可换新 access token
- Bearer Token 可正常调用受保护 API

## 6. 结果怎么记

若本轮只是普通身份语义改动，最少在 PR 或评审说明中写清以下内容：

```md
## 身份语义回归记录

- 自动化：
  - `npm run validate:baseline`：通过 / 阻塞 / 未执行
  - `npm run validate:identity`：通过 / 阻塞 / 未执行
  - `npm run validate:baseline:host`：通过 / 阻塞 / 未执行
- 协议专题：
  - `Radish.Api.AuthFlow.http`：通过 / 阻塞 / 未执行
- 官方顺序回归：
  - `radish-client`：通过 / 阻塞 / 未执行
  - `radish-console`：通过 / 阻塞 / 未执行
  - `Radish.Api.AuthFlow.http`：通过 / 阻塞 / 未执行
  - `radish-scalar`：通过 / 阻塞 / 未执行
- 结论：
  - 可合并 / 可转维护 / 仍需观察 / 触发回滚窗口
```

若需要沉淀更完整记录，优先复用：

- [回归结论记录模板](/guide/regression-result-template)
- [变更回归记录模板](/guide/change-regression-record-template)

## 7. 哪些继续留在本地，哪些适合上 CI

### 7.1 适合继续留在本地专题层

- `Radish.Api.AuthFlow.http`
- `radish-client / radish-console / radish-scalar` 官方顺序回归
- 任何依赖浏览器、账号、Token、运行中宿主或人工观察的步骤

原因：

- 这些资产验证的是“真实协议闭环与官方消费者行为”；
- 当前不适合伪装成纯自动化门禁。

### 7.2 当前适合上 CI

- `check:identity-runtime`
- `check:identity-protocol-output`
- `validate:identity` 中的身份语义后端定向测试

原因：

- 稳定、可重复、无宿主依赖；
- 失败时可直接指向具体回归类型，不会淹没在 `Baseline Quick` 的综合输出里。

当前 `Repo Quality / Identity Guard` 已进一步收口为“按改动范围触发”：

- 若 PR 未命中身份语义影响面，则工作流会显式跳过 `validate:identity`
- 若 PR 命中 `CurrentUser / HttpContextTool / Auth 输出 / Token 解析 / AuthFlow / 身份语义文档与脚本` 等收口范围，则继续执行完整 `validate:identity`
- 这套判定当前继续复用仓库统一的变更文件收集脚本，而不是在 workflow 内重复维护多份 git diff 逻辑

本地 changed-only 自检也按同一条 collector 口径执行：

- 工作区未暂存改动：`npm run check:identity-impact`
- 只看 staged 内容：`npm run check:identity-impact:staged`
- 两者当前都先走 `Scripts/collect-changed-files.mjs`，再交给 `check-identity-impact.mjs` 判定

## 8. 当前结论

截至 `2026-04-04`，身份语义 `Phase 4` 的首轮实施、官方顺序真实回归与回滚窗口验证都已完成；`Phase 5` 当前首轮工程化落点收束为：

- 用 `validate:identity` 回答“默认必须跑什么”；
- 用 `check:identity-runtime / protocol-output` 明确回归类型；
- 用 `Radish.Api.AuthFlow.http` 与官方顺序回归维持协议消费者事实基线；
- 用按改动范围触发的独立 CI 门禁维持“身份语义保护是显式资产，而不是 baseline 的附属说明”。
