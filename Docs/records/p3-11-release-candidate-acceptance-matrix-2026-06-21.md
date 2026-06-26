# P3-11 发布候选验收矩阵

> 记录日期：`2026-06-21`（Asia/Shanghai）
>
> 关联入口：[P3-11 发布候选整备与轻量复访补齐](/planning/p3-11-release-candidate-light-revisit)、[P3-10 阶段收束准备记录](/records/p3-10-stage-closure-prep-record-2026-06-21)、[验证基线说明](/guide/validation-baseline)
>
> 2026-06-22 更新：`P3-12-B6` 已确认上线前不维护历史发布脚本；本矩阵中涉及数据库结构的项按 `Radish.DbMigrate` 基线和正式数据库是否存在重新判断。

## 结论

`P3-11-A` 首批只建立发布候选验收矩阵，不创建 PR、不创建 tag、不进入 M15 测试 / 生产部署流程。

当前 `dev` 仍以 `46b9e8b2 docs(planning): 记录 P3-10 运行态复核` 为 HEAD；`master..dev` 为 `49` 个提交，覆盖 `242` 个文件，约 `18571` 行新增、`2169` 行删除。该范围包含 P3-10-D 后续安全治理、写操作可靠性、论坛内容发布可靠性、Flutter 承接和阶段收束文档。

若后续恢复 `dev -> master` PR 或发布候选整备，应先刷新 Git 范围；无新增代码影响面时，可以复用 P3-10 阶段收束准备记录中的验证结论，但仍需按下表判断哪些项目必须在恢复 PR / 发布候选前重跑。

## 验收矩阵

| 范围 | 当前依据 | 恢复 PR 前 | 发布候选前 | 剩余风险 |
| --- | --- | --- | --- | --- |
| Git 范围 | 本记录刷新到 `49` 个 `master..dev` 提交 | 必须重跑 `git log --oneline master..dev`、`git rev-list --count master..dev`、`git diff --stat master..dev` | 必须重跑 | 本地 P3-11 文档未提交前不计入提交范围 |
| 基础仓库门禁 | P3-10 记录中 `validate:ci -- --report` 通过 | 建议重跑 | 必须重跑 | changed-only 不能单独代表全范围后端影响 |
| 后端 / 前端完整基线 | P3-10 记录中 `validate:baseline` 通过 | 无新增代码时可复用，恢复 PR 前建议重跑 | 必须重跑 | 覆盖构建与测试，但不等于真实登录后 E2E |
| 身份语义与 LongId 守护 | P3-10 记录中 `validate:identity` 通过 | 建议重跑 | 必须重跑 | 当前范围命中身份语义和公开 ID 契约 |
| Flutter 论坛 / 商城承接 | `flutter analyze`、`flutter test` 已通过 | Flutter 无新增代码时可复用；恢复 PR 前建议重跑 | 必须重跑 | 不覆盖完整移动商城、转账或系统通知 |
| P3-10-D 公开入口治理 | P3-10-D 合并前记录与阶段收束记录 | 无页面代码变化时可复用 | 需要抽样复核 | 不覆盖登录后写操作、订单、背包或 Console 登录态深层页 |
| 支付口令与支付 / 转账幂等 | 后端定向测试、baseline 和治理文档 | 建议随 baseline 重跑 | 必须重跑，必要时补关键链路手测 | 不覆盖生产支付口令历史数据全量扫描 |
| WOG-1 至 WOG-6 写操作治理 | 后端测试、DbMigrate 结构补丁、Flutter 单商品购买幂等 | 建议随 baseline / Flutter 验证重跑 | 必须重跑；若存在正式数据库，再审核发布 SQL | 多实例并发、Redis 锁和完整资产风控仍后置 |
| 论坛内容发布可靠性 | 后端论坛定向测试、Web / Flutter `clientSubmissionId` 契约 | 建议随 baseline / Flutter 验证重跑 | 必须重跑 | 不覆盖 Flutter 子评论编辑和回答编辑 |
| 数据库结构 | P3-10 范围含多项 `DbMigrate` 结构补丁，历史发布脚本已按上线前口径清理 | PR 前审核 DbMigrate 自检结论和实体结构影响 | 若存在正式数据库，必须生成并演练发布 SQL；否则按当前基线重建目标库 | SQLite 与 PostgreSQL 行为仍需发布前分开确认 |
| Host runtime | P3-10 记录中 `check:host-runtime -- --details` 通过 | 只有用户确认前后端已启动后才重跑 | 必须重跑 | 不沿用历史服务启动状态 |
| Gateway PC / 移动页面 | P3-10 记录覆盖匿名公开入口、Console 登录回流和公开详情直链 | 无页面变化时可复用；恢复 PR 前可按风险抽样 | 发布候选前必须覆盖 PC / 移动 | 不覆盖生产部署链路、真实登录后全链路或完整 E2E |

## P3-11-B 只读盘点入口

发布候选验收矩阵完成后，下一步只读筛选轻量复访缺口，默认入口如下：

- 纯 Web：`/notifications`、`/messages`、`/me`、`/circle`、公开论坛详情。
- Flutter：论坛纯文本发帖、根评论 / 回复、问答回答、作者帖子正文编辑、作者根评论编辑、单商品购买重试。
- Console：只处理发布候选或真实验收阻断的权限、用户、订单、系统设置可见性问题。

本轮不启动真实页面 smoke。若后续需要 Gateway 页面复核，必须先要求用户明确前后端已经启动。

## 当前不做

- 不创建 `dev -> master` PR。
- 不创建发布 tag。
- 不把 P3-11-A 扩展成新功能实现。
- 不默认追加 P3-10-D 第五批链接语义扫尾。
- 不启动完整 Flutter 能力套件、完整移动商城、完整 E2E、完整可观测性或生产部署流程。
