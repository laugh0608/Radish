# P3-12-D11 radish.client 圈子与宠物任务面视觉实现记录

> 日期：2026-06-29（Asia/Shanghai）
>
> 范围：`radish.client` 圈子 / 电子宠物私域任务面视觉实现

## 背景

`P3-12-D10` 已完成通知 / 消息任务面。按 [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design)，下一顺位继续处理圈子 / 宠物、论坛作者态和 Docs 作者态。

本批聚焦 `/circle` 与 `/pet` 的 Web 私域任务面，不修改关注 API、公开路由来源返回、宠物领取 / 照顾 / 保存资料接口、宠物动作幂等或后端数据模型。

## 实现范围

- `/circle`
  - 将圈子页头升级为私域任务摘要卡片，展示关注数、粉丝数和当前分区结果数。
  - 加载、空态、鉴权和错误状态统一进入入口级 `WebStateSlot` 容器。
  - 移动端摘要卡改为单列任务流，保留论坛 / 我的状态跳转和公开来源返回语义。
- `/pet`
  - 登录 / 初始化状态统一进入入口级 `WebStateSlot` 容器。
  - 宠物首页补成长值、近期流水和公开状态指标卡，增强日常复访任务面。
  - 移动端指标卡改为单列可触控布局，保留照顾动作、冷却、资料保存和流水逻辑。
- 国际化
  - 补充圈子空态 / 加载态和宠物指标卡中英文文案键。

## 验证

```bash
npm run build --workspace=radish.client
npm run check:repo-hygiene:changed
git diff --check
```

结果：均通过。

未执行真实 Gateway PC / mobile smoke；本轮未收到前后端已启动的明确确认。真实运行态复核继续放到阶段验收或用户明确要求时执行。

## 后续顺位

1. 继续 `radish.client` 第二批视觉实现：论坛作者态和 Docs 作者态。
2. 第二批私域 / 作者态页面完成后，再按阶段验收集中补 Gateway PC / mobile 页面复核。
