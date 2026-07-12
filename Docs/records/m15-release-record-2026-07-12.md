---
releaseTag: v26.7.1-release
productVersion: 26.7.1
imageTag: v26.7.1-release
---

# M15 发布记录（v26.7.1-release，2026-07-12）

> 本页在正式 tag 前随候选提交进入 `dev -> master` PR。当前只记录已经发生的候选事实；tag、镜像和生产部署结果在实际完成后回写。

## 记录信息

- 记录日期：2026-07-12
- 记录人：项目协作记录
- 发布类型：正式 Web 发布
- 当前状态：等待 `dev -> master` PR、tag、镜像和生产部署

## 发布标识

- 计划 Git tag：`v26.7.1-release`
- 产品版本：`26.7.1`
- 对应提交：待 PR 合并后，以 `v26.7.1-release` 指向的 `master` 提交为准
- 候选来源：`dev`
- 正式发布矩阵：Gateway、API、Auth、DbMigrate、client、Console
- 镜像 tag：
  - `radish-dbmigrate:v26.7.1-release`
  - `radish-frontend:v26.7.1-release`
  - `radish-api:v26.7.1-release`
  - `radish-auth:v26.7.1-release`
  - `radish-gateway:v26.7.1-release`

Flutter 为条件维护资产，Tauri 为冻结实验资产，二者不进入本次正式构建、镜像和部署矩阵。

## 候选验证结论

- Q0 安全与暴露面阻断、E8-B 有限产品矩阵已经完成。
- Q1 可靠任务、错误契约和文件访问令牌治理已经完成。
- Q2 时间语义、数据库演进、OpenIddict 迁移和版本单一真值已经完成。
- Q3 lint、warning-as-error、四 workspace 测试、候选卫生和镜像供应链门禁已经完成。
- 本地 SQLite 在备份后通过 `DbMigrate apply / verify`；生产相似 PostgreSQL / OpenIddict 升级和恢复演练已有独立记录。
- Gateway PC `1920x1080` 与移动 `390x844` CSS 视口覆盖公开、私域、作者态和 Console 代表路径。
- 候选验证最终结果：前端 `368` 项通过，后端 `625` 项通过、`7` 项 PostgreSQL 环境用例跳过，.NET 构建 `0 warning / 0 error`。
- npm / NuGet High、Critical 已知漏洞为 `0`。

关联记录：[P3-12-F Release Go 候选运行态验收记录](/records/p3-12-f-release-go-candidate-runtime-validation-2026-07-12)。

## 测试部署结论

- 部署情况：未部署
- 复核情况：未执行
- 关联记录：无
- 说明：本轮计划直接从通过门禁的正式 `master` 提交创建 release tag，不预写测试环境成功结论。

## 生产部署结论

- 部署情况：未部署
- 复核情况：未执行
- 关联记录：部署完成后补充 M14 部署后复核记录
- 说明：必须等待 tag 对应的五个镜像完成构建、SBOM、provenance 和 High / Critical 扫描后，再使用固定 `RADISH_IMAGE_TAG=v26.7.1-release` 部署。

## 回滚目标

- 测试环境优先回滚目标：`v26.3.2-test`
- 生产环境优先回滚目标：`v26.3.2-release`
- 数据库边界：部署前执行正式备份；应用回滚不能自动逆转已经提交的不可逆数据迁移，需按 DbMigrate 与发布记录判断前滚或数据恢复。

## 已知风险与发布后事项

- 内置浏览器无法设置 DPR；本轮移动证据只声明 `390x844` CSS 视口。
- 公开用户主页浏览器运行态正常，但初始 HTML 暂缺服务端 canonical、Open Graph、Twitter card 和 JSON-LD，登记为非阻断 P2。
- 发布后的真实使用观察负责收集激活、首次参与、回应后回流、核心任务失败和用户反馈，不再作为 tag 前置门禁。
- 部署后进入 [发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)，首个功能专题为商城商品效力与权益履约。
