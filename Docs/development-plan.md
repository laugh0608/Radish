# 开发路线图（总览）

> 本页只保留 **当前里程碑、下一主线入口、后置池**。
>
> 当前执行细节见 [当前进行中](/planning/current)，历史过程见 [开发日志](/changelog/)。

## 当前状态

- **当前里程碑**：`M15 最小交付与部署基线`
- **当前主线**：`M15 最小交付与部署基线`
- **当前阶段**：`M14` 已完成首轮真实闭环；`M15` 已完成单一入口、固定 tag 口径、测试环境真实回滚演练、生产环境最小回滚预案与首份真实发布记录。当前继续维持“发版 / 部署 / 发布后最小复核 / 回滚”的默认顺序，不扩张到自动回滚、workflow 改造或 Gateway & BFF。`

## 当前主线入口

- [M15 最小交付与部署基线](/guide/m15-delivery-baseline)
- [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist)
- [验证基线说明](/guide/validation-baseline)

## 下一主线入口

- `M15` 之后，当前唯一建议入口为 [M15 后发布质量基线与回归治理（第一批）](/guide/post-m15-quality-baseline)
- 该阶段不是新业务主线，而是把仓库从“首轮可发布、可部署”推进到“后续每次发布都可稳定验证、稳定留痕、稳定追溯”
- 该阶段默认复用现有 `baseline / host / ci / identity / deployment review / release record / rollback facts` 资产，不再另起并行体系

## 可转维护

- 身份语义 `Phase 4`
- `DbMigrate doctor / verify`
- 社区主链 MVP 与相关收口补丁
- 通知中心、认证 / OIDC / Gateway 基础入口
- 桌面壳层、主题切换、`radish.client` i18n 首轮覆盖

## 明确后置

- `P3-ext / P4-ext / P5-ext / Console-ext Phase 2+`
- `P2-ext Auto` 开源软件清单自动生成 / 发布物公告
- `Gateway & BFF` 深化
- 邮件通知系统
- 完整 E2E / Playwright 平台
- 完整可观测性平台、Tracing / Metrics 大阶段

## 阶段文档规则

- 阶段定义只以：
  - [当前进行中](/planning/current)
  - [开发路线图](/development-plan)
  - [M15 最小交付与部署基线](/guide/m15-delivery-baseline)
  - [M15 后发布质量基线与回归治理（第一批）](/guide/post-m15-quality-baseline)
  为准
- 其余模板、记录、样例与专项页面不再承担阶段定义职责
