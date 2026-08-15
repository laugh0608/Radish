---
releaseTag: v26.8.1-release
productVersion: 26.8.1
imageTag: v26.8.1-release
---

# M15 Web 阶段收口发布记录（v26.8.1-release，2026-08-15）

> 本页随候选提交建立，并按实际执行回写 PR、tag 与镜像结果；生产部署及部署后复核仍保持未执行状态。

## 记录信息

- 记录日期：2026-08-15
- 发布类型：正式 Web 阶段收口发布
- 当前状态：PR required checks、合并、`master -> dev` 回灌、正式 tag 与五镜像发布已完成；生产部署尚未执行

## 发布标识

- Git tag：`v26.8.1-release`
- 产品版本：`26.8.1`
- 候选来源：`dev`，基于 `origin/master@e03416ac` 之后的阶段批次，通过 [PR #68](https://github.com/laugh0608/Radish/pull/68) 合入 `master`
- 最终对应提交：`e518a30694a3a72443d800f73c362dcadae0e2c0`
- 正式发布矩阵：DbMigrate、API、Auth、Gateway、Frontend
- 镜像 tag：五个正式镜像统一使用 `v26.8.1-release`

## 已发布制品

- Docker Images workflow：[run #31871626132](https://github.com/laugh0608/Radish/actions/runs/31871626132)，Candidate Quality 与五镜像任务全部成功。
- `ghcr.io/laugh0608/radish-dbmigrate:v26.8.1-release`：`sha256:7758a62cadaa94f79e7a10dbff43d3a772491cd6a93978bad6e1cc6653d1d397`
- `ghcr.io/laugh0608/radish-api:v26.8.1-release`：`sha256:0390272db8debf04894688b09df14e11290bf1833523b43c63a3096206a55025`
- `ghcr.io/laugh0608/radish-auth:v26.8.1-release`：`sha256:e9e816b48554c8322f4e79747adf7e59699f8f3d83367c04ff97c2771c11d054`
- `ghcr.io/laugh0608/radish-gateway:v26.8.1-release`：`sha256:a1b840abfede2aa621aa1cb2d51aab7ff1f353e3a5bee0acbaf8c8a36818f726`
- `ghcr.io/laugh0608/radish-frontend:v26.8.1-release`：`sha256:66af2747a48a32aace6a6a0cf667b6b16ad76437069b14972313b911c03858ef`

## 发布范围

- 收口 R3-F02 OIDC 回流信任、自服务权威状态、错误与路由边界，并复用已完成的 Gateway PC / mobile 成组验收。
- 收口 Web / Console 四主题语义、reduced-motion 静态门禁及四主题成组运行态验收，关闭当前 Web 主题退出门禁。
- 确认 Web / Flutter Native 两条产品线与 Flutter 产品化、UI 重构边界；Flutter 本轮只同步统一产品版本，不进入正式 Web 镜像、签名或分发矩阵。
- Tauri 保持弃用，WebOS 只保留历史兼容，不扩展当前发布范围。

## 候选验证

- 批次回归记录：[v26.8.1 Release Candidate 回归记录](/records/v26.8.1-release-candidate-regression-record-2026-08-15)
- 本地门禁：版本 tag 契约、Baseline Full、Repo Quality Local、Candidate Quality、后端 / 身份专题、LongId、依赖安全、repo hygiene 与 `git diff --check` 均通过。
- 测试摘要：前端 HTTP `48 / 48`、UI `32 / 32`、Client `557 / 557`、Console `138 / 138`；后端在 CI 同规格 PostgreSQL 17 下 `1320 passed / 0 skipped / 0 failed`，身份定向 `35 / 35`，warnings-as-errors 构建 `0 warning / 0 error`。
- PostgreSQL 候选：首次全量执行发现聊天已读游标并发创建后的单调推进竞争，修复为有界二次推进与权威状态复读，并将专项加强为 `8` 目标并发；定向测试 `4 / 4`、增强用例连续 `10` 轮及最终 Candidate Quality 均通过。临时数据库容器已清理，PR #68 的远程 required checks 已全部通过。
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

- 生产固定 tag 前滚与部署后最小复核尚未执行；发布制品完成不自动等同于生产部署完成。
- Flutter Native 新 UI 尚处 P0 路线确认状态，不属于本次发布完成声明；下一批从 P1 全页面事实审计开始。

## 发布门禁

- [x] 完成本地候选 build、test、身份语义、依赖安全、repo hygiene 与版本 tag 契约
- [x] `dev -> master` PR required checks 全部通过并合并
- [x] 最新 `origin/master` 回灌 `dev`
- [x] 在 `master` 合并提交创建并推送 `v26.8.1-release`
- [x] Docker Images 五镜像门禁成功
- [ ] 生产固定 tag 前滚与部署后复核完成
