# 首版 dev 总回归记录

> 本页用于沉淀当前首版 `dev` 收官阶段的统一回归结论。
>
> 关联文档：
>
> - [首版 dev 总回归与发布前检查单](/guide/dev-first-regression-checklist)
> - [首版 dev 功能矩阵状态表](/planning/dev-first-status-matrix)
> - [当前进行中](/planning/current)
> - [开发路线图总览](/development-plan)
> - [2026-03 / week4](/changelog/2026-03/week4)

## 首版 dev 总回归记录（2026-03-26）

- 记录日期：2026-03-26
- 记录人：项目协作记录
- 范围：首版 `dev` 总回归
- 触发原因：首版 `dev` 收官阶段需要把已通过的真实 Smoke、当前验证基线结果与工程判断收束成统一记录，避免继续依赖会话口头结论

### 变更摘要

- 汇总 `2026-03-23`、`2026-03-25`、`2026-03-26` 已通过的首版 Smoke 结果。
- 复跑 `npm run validate:baseline` 与 `npm run validate:baseline:host`，确认当前工程门槛是否已具备“可发内部开发版”判断基础。
- 补记最新一次 `master` PR 的最小 `CI` 门禁真实通过、PR 合并与 `dev` 回灌结果，避免继续停留在“门禁已接通但仍待确认闭环”的口径。
- 基于真实执行结果回写当前首版 `dev` 的工程结论，而不是继续沿用“应该差不多通过”的泛化口径。

### 影响范围

- 认证 / OIDC / Gateway 基础入口
- WebOS 桌面与应用容器
- 论坛基础
- 社区 `P0`
- `Console V1`
- `radish.client` 国风视觉基线 / 主题切换 / `i18n`
- 通知中心
- Docker 镜像构建链与最小交付口径
- 验证基线与最小 `CI` 门禁判断

### 自动化执行

- `npm run validate:baseline`：通过
  - 前端 `type-check`：通过
  - `radish.client` 最小 node 测试：通过，`6/6`
  - `Console` 权限链路扫描：通过
  - 身份语义防回归扫描：通过
  - 后端解决方案构建：通过
  - 后端测试：通过，`213/213`
- `npm run validate:baseline:host`：通过
  - 复用 `full` 验证结果并追加 `DbMigrate doctor / verify`
  - `DbMigrate doctor`：通过
  - `DbMigrate verify`：通过
- `master` Pull Request 最小 `CI` 门禁：通过
  - 最新一次用于合并的 `master` PR 已完成 `Repo Hygiene`、`Frontend Lint`、`Baseline Quick`
  - PR 已合并到 `master`：`37fe89c`
  - `dev` 已同步 `master` 合并结果：`cbd0f8a`

### 直接引用的已有通过记录

- `2026-03-23`：通知中心真实首版 Smoke 通过
- `2026-03-23`：认证 / OIDC / Gateway 基础入口真实首版 Smoke 通过
- `2026-03-25`：Docker 镜像构建链与最小交付口径 Smoke 通过
- `2026-03-26`：WebOS / 论坛基础 / 社区 `P0` / `Console V1` / 国风视觉基线 / 主题切换 / `i18n` 首版烟雾联调通过

### 本轮新增补验

- 首次复跑 `npm run validate:baseline` 时暴露 `2` 个 `PostControllerTest` 失败
- 补齐 `PostControllerTest` 对 `CommentService.QueryAsync(...)` 的严格 `Mock` 后，重新执行 `npm run validate:baseline`
- 继续执行 `npm run validate:baseline:host`
- 未新增额外 UI Smoke；本轮用户侧手工联调结论继续复用同日已确认通过的真实记录

### 已收口问题

- `Radish.Api.Tests.Controllers.PostControllerTest.GetById_Should_Return_Full_Poll_Detail_When_PostExists`
- `Radish.Api.Tests.Controllers.PostControllerTest.GetById_Should_Pass_AnswerSort_When_RequestContains_AnswerSort`
- 上述两个失败用例的根因是：
  - `PostController.GetById` 当前会在 `FillPostAvatarAndInteractorsAsync` 中新增调用 `IBaseService<Comment, CommentVo>.QueryAsync(...)`
  - 对应严格 `Mock` 尚未补齐该调用的 `setup`
- 当前处理结果：
  - 已在 `PostControllerTest` 中补齐 `commentServiceMock.QueryAsync(...)` 的空列表返回
  - 定向测试已恢复通过
  - `validate:baseline` 与 `validate:baseline:host` 已恢复全通过

### 当前结论

- 结果：可发内部开发版
- 工程判断：当前业务主线与体验主线 Smoke、`full` 验证基线，以及 `host` 只读自检均已完成当前批次收口
- 当前状态更准确的表述应为：`首版 dev 已具备可发内部开发版条件，当前转入内部开发版后观察与上线前交付复核阶段`

### 风险 / 后置项

- 最小 `CI` 门禁已完成真实合并闭环，但后续新增跨层改动仍需继续依赖 `master` PR 门禁作为默认质量入口
- 当前 `dotnet build/test` 仍存在较多警告，但本轮未表现为阻塞项；是否继续清理，后续按优先级单列，不并入当前首版门槛判断
- 本轮未重新组织新的业务侧总串联 Smoke，仍以 `2026-03-23 ~ 2026-03-26` 已沉淀记录为准
- 真实外部反代域名、Auth 证书与 OIDC 回调链路仍未在生产口径下补联调记录，但这属于上线前交付复核项，不阻塞当前内部开发版判断

### 下一动作

1. 维持当前 `master` PR 质量门禁与 `validate:baseline` 作为内部开发版默认回归入口。
2. 如近期再发生跨层改动，优先重新执行 `npm run validate:baseline`；涉及宿主 / 配置时再补 `npm run validate:baseline:host`。
3. 在准备更大范围对外部署或上线前，补记真实外部反代域名、Auth 证书与 OIDC 回调链路的联调结果。
