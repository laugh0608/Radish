# P3-12-D12 radish.client 作者态任务面视觉实现记录

> 日期：2026-06-29（Asia/Shanghai）
>
> 范围：`radish.client` 论坛作者态 / Docs 作者态任务面视觉实现

## 背景

`P3-12-D11` 已完成圈子 / 宠物任务面。按 [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design)，第二批 `radish.client` 私域 / 作者态视觉实现剩余重点是论坛作者态和 Docs 作者态。

本批只处理作者入口的任务面、摘要和状态槽，不修改论坛发布器、`clientSubmissionId` 幂等、帖子编辑 / 回答 / 采纳逻辑、Docs 文档保存、修订读取、Markdown 编辑器、权限判断或后端契约。

## 实现范围

- `/forum/compose`
  - 发帖页增加作者任务摘要，展示登录状态、分类加载状态和提交幂等保护。
  - 移动端摘要卡改为单列任务流，保留现有 `PublishPostModal` 和分类加载状态。
- `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id`
  - Docs 作者台增加顶部作者任务摘要，展示目录节点、文档总数和当前作者入口状态。
  - 作者台状态面板改为复用共享 `WebStateSlot`，覆盖登录、权限、加载、空态和错误态。
  - 移动端摘要卡改为单列任务流，保留原有文档列表、编辑表单和修订查看布局。
- 国际化
  - 补充论坛发帖任务摘要的中英文文案键。

## 验证

```bash
npm run build --workspace=radish.client
```

结果：通过。

本批尚未执行仓库卫生检查、`git diff --check` 和真实 Gateway PC / mobile smoke。真实运行态复核需要用户先确认前后端已启动。

## 后续顺位

1. 补本批仓库卫生检查与 `git diff --check`。
2. 若继续视觉实现，可进入第二批收口检查：统一回看 D9-D12 私域 / 作者态页面的移动密度和状态槽一致性。
3. 阶段验收时提醒用户启动前后端，再补 Gateway PC / mobile 页面复核。
