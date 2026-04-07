# `PR -> master` 最小执行清单

> 本页只回答一个问题：**当一批改动准备从 `dev` 合并到 `master` 时，最少要做哪些检查和记录？**
>
> 关联入口：
>
> - [验证基线说明](/guide/validation-baseline)
> - [M15 最小交付与部署基线](/guide/m15-delivery-baseline)
> - [变更回归记录模板](/guide/change-regression-record-template)
> - [M15 发布记录模板](/guide/m15-release-record-template)

## 适用范围

适用于以下场景：

- 一批日常开发内容已经准备合并
- 需要从“本地连续提交”切换到“可合并批次”判断
- 需要明确本轮是否已经达到 `master` 合并前的最小工程门槛

不适用于：

- 开发中的本地中间提交
- 单纯保存点或尚未准备合并的实验性改动
- 已经进入真实发布 / 部署 / 回滚阶段的动作

## 一句话原则

本地连续提交只做必要验证；真正重留痕与完整合并前判断，收口到 `PR -> master` 这一层。

## 最小清单

### 1. 先确认这批改动是否真的应该面向 `master`

- 默认目标分支仍然是 `dev`
- 只有阶段性集成、发布准备、明确收口事项，才发起 `dev -> master`

### 2. 跑最小自动化

至少执行：

```bash
npm run validate:baseline:quick
```

如果本轮属于跨层改动、需要给出更强“可合并”结论，再执行：

```bash
npm run validate:baseline
```

如果本轮需要对齐当前 `Repo Quality` 最小门禁，再按需执行：

```bash
npm run validate:ci
```

如果需要把这一层结果直接贴进 PR 或批次级回归记录，可直接使用：

```bash
npm run validate:ci -- --report
```

如果希望脚本直接把报告落盘，再粘贴或沉淀到批次级记录，可使用：

```bash
npm run validate:ci -- --report-file .tmp/validate-ci-report.md
```

如果本轮还希望把自动化报告与身份影响面统一收成一份批次级记录，可继续执行：

```bash
npm run collect:change-regression-record -- --title "当前批次" --scope "当前 PR / 改动批次"
```

若还要把 baseline 或 `M14` 记录并入同一份批次级说明，请显式传入对应报告路径，避免误混入历史 `.tmp` 文件。

### 3. 只在命中条件时追加专题验证

- 命中身份语义影响面：补 `npm run validate:identity`
- 命中宿主 / 配置 / 数据库 / 部署链路：补 `npm run validate:baseline:host`
- 命中真实运行态确认：补 `npm run check:host-runtime`

不要因为普通前端或文档改动，就默认把所有专题验证全跑一遍。

### 4. 补一份批次级回归记录

这一层的记录对象是“这批准备合并的改动”，不是每一个 commit。

推荐直接复用：

- [变更回归记录模板](/guide/change-regression-record-template)

记录重点只保留：

- 本次改动范围
- 实际执行过的验证
- 若有失败或跳过，属于哪一类
- 当前结论是否可合并

### 5. 发起 `dev -> master` PR

PR 模板中应至少能回答：

- 为什么这批内容现在要进 `master`
- 本轮实际跑了哪些验证
- 是否需要补充专题验证或发布后动作

## 不该做的事

- 不要求把每次本地提交都写成回归记录
- 不要求每次普通改动都跑 `host`、部署和回滚链路
- 不要求把发布记录、部署记录提前到开发中间态去填写

## 与发布阶段的边界

`PR -> master` 只回答“这批内容是否具备合并条件”。

真正进入发布、部署、回滚时，再补：

- 发布记录
- 部署后最小复核记录
- 回滚事实或回滚预案

这些内容不前置到普通合并前检查里。
