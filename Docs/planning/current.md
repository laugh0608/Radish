# 当前进行中

> 本页只维护 **当前主线、下一主线入口、并行维护项、近期不做项**。
>
> 详细历史事实请看 [开发日志](/changelog/)；模板、样例与专项记录请回到对应 `Docs/guide/` 页面，不再在本页重复展开。

## 当前主线

- **里程碑**：`M15 最小交付与部署基线`
- **当前主线**：`M15 最小交付与部署基线`
- **当前阶段**：`截至 2026-04-06，M14 的“启动前 validate:baseline:host -> 启动后 check:host-runtime -> 部署后最小复核”三层主路径已完成首轮真实闭环；M15 当前也已完成单一入口、固定 tag 口径、测试环境真实回滚演练、生产环境最小回滚预案与首份真实发布记录。当前主线继续只收口“发版 / 部署 / 发布后最小复核 / 回滚”的默认顺序，不扩张到 workflow 改造、自动回滚或 Gateway & BFF 专题。`
- **复核日期**：`2026-04-06`

## 当前执行入口

- [M15 最小交付与部署基线](/guide/m15-delivery-baseline)
- [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist)
- [验证基线说明](/guide/validation-baseline)
- [部署与容器指南](/deployment/guide)

## 当前已确认事实

- `M14` 默认主路径已真实闭环：`validate:baseline:host -> check:host-runtime -> 部署后最小复核`
- `M15` 单一入口已建立，测试与生产 `.env` 示例默认值已统一为明确版本 tag
- 测试环境已完成首轮真实最小回滚演练：`v26.3.2-r1-test -> v26.3.2-test`
- 生产环境当前已形成最小回滚预案，但尚未做真实回滚演练
- `v26.3.2-release` 的首份真实发布记录已落库，发布、部署与回滚事实已开始可追溯

## 下一主线入口

- `M15` 之后，当前下一主线入口固定为 [M15 后发布质量基线与回归治理（第一批）](/guide/post-m15-quality-baseline)
- 该入口只负责收口发布后默认执行面、回归记录与发布 / 部署 / 回滚事实之间的关系
- 不再直接沿用旧 `M13` 名义继续扩张阶段范围

## 并行维护项

- 身份语义 `Phase 4`：稳定维护；如部署形态、外部客户端或反代规则变化，再补事实确认
- `DbMigrate`：继续维持 `doctor / verify` 只读自检入口
- `Repo Quality / validate:ci / Identity Guard`：继续保持默认执行面同源
- 社区、通知、认证、桌面、Console 一期：当前均转入稳定维护，不再作为当前主线

## 文档冻结规则

- 没有新的真实发布、真实部署、真实回滚或真实回归结论，不改阶段文档
- 普通功能开发默认不改：
  - `planning/current.md`
  - `development-plan.md`
  - `guide/m15-delivery-baseline.md`
  - `guide/post-m15-quality-baseline.md`
- 阶段文档只在两种情况下允许更新：
  - 主线正式切换
  - 新的真实事实已经改变默认执行面

## 当前不做

- `P3-ext / P4-ext / P5-ext / Console-ext Phase 2+` 的非阻塞增强
- `Gateway & BFF` 深化
- 自动回滚、workflow 改造、蓝绿 / 金丝雀发布
- 完整 E2E / Playwright 平台
- 完整可观测性平台或大而全运维平台
