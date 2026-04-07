# M15 后发布质量基线与回归治理（第一批）

> 本页定义 `M15` 之后的下一条主线入口。
>
> 关联入口：
>
> - [M15 最小交付与部署基线](/guide/m15-delivery-baseline)
> - [验证基线说明](/guide/validation-baseline)
> - [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist)
> - [M14 部署后最小复核记录模板](/guide/m14-deployment-review-record-template)
> - [M15 发布记录模板](/guide/m15-release-record-template)
> - [变更回归记录模板](/guide/change-regression-record-template)
> - [回归结论记录模板](/guide/regression-result-template)

## 一句话目标

把仓库从“首轮可发布、可部署”推进到“后续每次发布都可稳定验证、稳定留痕、稳定追溯”。

## 当前定位

- 这条主线不是继续补业务功能
- 这条主线也不是重开旧版 `M13`
- 它只负责把已经存在的验证、部署、发布、回滚与回归记录资产收成发布后的默认执行面

## 当前默认执行面

### 普通代码改动

默认执行：

```bash
npm run validate:baseline
```

如本轮命中身份语义影响面，再补：

```bash
npm run validate:identity
```

如本轮需要提交 / 合并前核对仓库门禁语义，再补：

```bash
npm run validate:ci
```

如本轮准备发起 `PR -> master`，并希望把自动化报告直接收成批次级记录，再补：

```bash
npm run collect:change-regression-record -- --title "当前批次" --scope "当前 PR / 改动批次"
```

### 宿主 / 配置 / 部署相关改动

默认执行：

```bash
npm run validate:baseline:host
npm run check:host-runtime -- --report-file .tmp/host-runtime-report.md
npm run collect:m14-host-record
```

如本轮涉及测试部署或生产部署，再补：

- `M14` 部署后最小复核记录

### 正式发布前后

发布前默认至少确认：

- `npm run validate:baseline:quick`
- 本次镜像 tag 已固定为明确版本
- 本次回滚目标是否明确

发布后默认至少保留：

- 一份 `M15` 发布记录
- 一份 `M14` 部署后最小复核记录
- 一份回滚事实或回滚预案说明

## 记录分层

- `validation-baseline`：回答“默认跑什么”
- `M14` 部署后最小复核：回答“部署后最小检查结果是什么”
- `M15` 发布记录：回答“本次发布事实是什么”
- `M15` 回滚记录 / 预案：回答“如果出问题怎么退、退到哪”
- 变更回归记录模板 / 回归结论记录模板：回答“本次改动整体做了哪些验证、当前能下什么结论”

## 第一批最小范围

本阶段第一批只收口三件事：

1. 把发布后默认执行面固定下来
2. 把发布、部署、回滚与回归记录的分层关系固定下来
3. 把第二次及后续 release 的追溯口径固定下来

## 当前边界

本阶段明确不做：

- `P3-ext / P4-ext / P5-ext / Console-ext Phase 2+`
- `Gateway & BFF` 深化
- 自动回滚、workflow 改造、蓝绿 / 金丝雀发布
- 完整 E2E / Playwright 平台
- 完整可观测性平台、Tracing / Metrics 大阶段

## 文档冻结规则

- 没有新的真实发布、真实部署、真实回滚或真实回归结论，不改本页
- 普通功能开发默认不改本页
- 只有在以下情况下才更新本页：
  - 默认执行面发生变化
  - 新的真实事实已经改变发布后默认动作
  - 下一主线正式切换
