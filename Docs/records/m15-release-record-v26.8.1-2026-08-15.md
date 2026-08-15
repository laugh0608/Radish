---
releaseTag: v26.8.1-release
productVersion: 26.8.1
imageTag: v26.8.1-release
---

# M15 Web 阶段收口发布记录（v26.8.1-release，2026-08-15）

> 本页先随候选提交记录真实的未发布状态；PR、tag、镜像、生产部署与部署后复核只在实际完成后回写。

## 记录信息

- 记录日期：2026-08-15
- 发布类型：正式 Web 阶段收口发布
- 当前状态：Release Candidate 本地门禁通过，已达到 PR-ready；尚未推送、合并、创建 tag 或部署

## 发布标识

- Git tag：`v26.8.1-release`（尚未创建）
- 产品版本：`26.8.1`
- 候选来源：`dev`，基于 `origin/master@e03416ac` 之后的阶段批次
- 最终对应提交：待 `dev -> master` PR 合并后回写
- 正式发布矩阵：DbMigrate、API、Auth、Gateway、Frontend
- 镜像 tag：五个正式镜像统一使用 `v26.8.1-release`

## 发布范围

- 收口 R3-F02 OIDC 回流信任、自服务权威状态、错误与路由边界，并复用已完成的 Gateway PC / mobile 成组验收。
- 收口 Web / Console 四主题语义、reduced-motion 静态门禁及四主题成组运行态验收，关闭当前 Web 主题退出门禁。
- 确认 Web / Flutter Native 两条产品线与 Flutter 产品化、UI 重构边界；Flutter 本轮只同步统一产品版本，不进入正式 Web 镜像、签名或分发矩阵。
- Tauri 保持弃用，WebOS 只保留历史兼容，不扩展当前发布范围。

## 候选验证

- 批次回归记录：[v26.8.1 Release Candidate 回归记录](/records/v26.8.1-release-candidate-regression-record-2026-08-15)
- 本地门禁：版本 tag 契约、Baseline Full、Repo Quality Local、Candidate Quality、后端 / 身份专题、LongId、依赖安全、repo hygiene 与 `git diff --check` 均通过。
- 测试摘要：前端 HTTP `48 / 48`、UI `32 / 32`、Client `557 / 557`、Console `138 / 138`；后端在 CI 同规格 PostgreSQL 17 下 `1320 passed / 0 skipped / 0 failed`，身份定向 `35 / 35`，warnings-as-errors 构建 `0 warning / 0 error`。
- PostgreSQL 候选：首次全量执行发现聊天已读游标并发创建后的单调推进竞争，修复为有界二次推进与权威状态复读，并将专项加强为 `8` 目标并发；定向测试 `4 / 4`、增强用例连续 `10` 轮及最终 Candidate Quality 均通过。临时数据库容器已清理，远程 required checks 仍须独立通过。
- 运行态证据：复用 [R3-F02 成组运行态验收](/records/f4-r-r3-f02-grouped-runtime-acceptance-2026-08-15)与 [Web 四主题成组运行态验收](/records/f4-r-t03-web-four-theme-grouped-runtime-acceptance-2026-08-15)；本次版本与文档收口不重启服务、浏览器或主动生产证据采集。

## 测试部署结论

- 部署情况：未部署
- 最小复核：未执行
- 关联记录：无

## 生产部署结论

- 部署情况：未部署
- 最小复核：未执行
- 关联记录：无

## 回滚目标

- 测试环境优先回滚目标：`v26.7.1.1204-release`
- 生产环境优先回滚目标：`v26.7.1.1204-release`
- 说明：该 tag 是最近一次已知生产固定 tag，五镜像和 PostgreSQL 迁移链路已完成部署复核。

## 已知风险 / 后置项

- 远程 `dev -> master` PR required checks、合并与 `master -> dev` 回灌尚未执行。
- `v26.8.1-release` tag、五镜像构建、生产部署和部署后最小复核尚未执行。
- Flutter Native 新 UI 尚处 P0 路线确认状态，不属于本次发布完成声明；下一批从 P1 全页面事实审计开始。

## 发布门禁

- [x] 完成本地候选 build、test、身份语义、依赖安全、repo hygiene 与版本 tag 契约
- [ ] `dev -> master` PR required checks 全部通过并合并
- [ ] 最新 `origin/master` 回灌 `dev`
- [ ] 在 `master` 合并提交创建并推送 `v26.8.1-release`
- [ ] Docker Images 五镜像门禁成功
- [ ] 生产固定 tag 前滚与部署后复核完成
