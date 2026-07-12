---
releaseTag: v26.7.1.1201-release
productVersion: 26.7.1
imageTag: v26.7.1.1201-release
---

# M15 补发记录（v26.7.1.1201-release，2026-07-12）

> 本页记录 `v26.7.1-release` 被前端镜像漏洞门禁阻断后的正式补发。补发 tag 与五个镜像已经完成，生产部署结果仍在实际执行后回写。

## 记录信息

- 记录日期：2026-07-12
- 记录人：项目协作记录
- 发布类型：正式 Web 补发
- 当前状态：补发 tag 与五镜像门禁完成，等待生产部署与部署后复核

## 发布标识

- Git tag：`v26.7.1.1201-release`（已推送）
- 产品版本：`26.7.1`
- 对应提交：`6db3668ba32b5b05243ff1ae9f66fcc2b34a12f7`
- 候选来源：`master`，并已 fast-forward 回灌到 `dev`
- 正式发布矩阵：Gateway、API、Auth、DbMigrate、client、Console
- 镜像 tag：
  - `radish-dbmigrate:v26.7.1.1201-release`
  - `radish-frontend:v26.7.1.1201-release`
  - `radish-api:v26.7.1.1201-release`
  - `radish-auth:v26.7.1.1201-release`
  - `radish-gateway:v26.7.1.1201-release`

Flutter 为条件维护资产，Tauri 为冻结实验资产，二者不进入本次正式构建、镜像和部署矩阵。

## 补发原因与修复

- 原 `v26.7.1-release` 的 Candidate Quality 已通过，前端最终镜像在 Trivy High / Critical 阶段失败。
- 原运行层 `node:24-bookworm-slim` 包含运行时不需要的 Debian 与 npm 资产，命中 `20` 个 High / Critical 发现。
- 补发只把最终运行层调整为 `node:24-alpine`，并移除运行时不需要的 npm、npx、corepack 和 yarn 资产。
- 构建层、根 lockfile、前端依赖、静态服务脚本、动态运行配置、Client / Console 产物和路由契约均保持不变。
- 本地同口径 Trivy `0.69.3` 扫描结果为 High / Critical `0`。

## 修复验证

- `Frontend/Dockerfile` 使用 `--pull` 构建成功。
- 短时容器 `127.0.0.1:8088` 验证完成并已停止：
  - `/healthz`：`200`。
  - `/runtime-config.js`：`200`，保持 `no-store` 与动态环境配置。
  - Client 深链回退：`200`，返回 Client `index.html`。
  - `/console`：`308` 到 `/console/`。
  - Console 深链回退：`200`，返回 Console `index.html`。
- 补发提交已通过候选基线、版本契约、仓库卫生和 `git diff --check`。
- [Docker Images #17](https://github.com/laugh0608/Radish/actions/runs/29188581708) 的 Candidate Quality、Frontend、API、Auth、Gateway 与 DbMigrate job 全部成功。
- 五个镜像均已完成 High / Critical 扫描、`linux/amd64 + linux/arm64` 推送、SBOM 与 provenance。

## 生产部署结论

- 部署情况：未部署
- 复核情况：镜像供应链门禁已完成，部署环境复核未执行
- 关联记录：[Docker Images #17](https://github.com/laugh0608/Radish/actions/runs/29188581708)；部署后复核完成后补充 M14 记录
- 说明：部署时必须固定 `RADISH_IMAGE_TAG=v26.7.1.1201-release`，不得改用 `latest` 或原失败 tag。

## 回滚目标

- 测试环境优先回滚目标：`v26.3.2-test`
- 生产环境优先回滚目标：`v26.3.2-release`
- 数据库边界：部署前执行正式备份；应用回滚不能自动逆转已经提交的不可逆数据迁移，需按 DbMigrate 与发布记录判断前滚或数据恢复。

## 已知风险与发布后事项

- 内置浏览器无法设置 DPR；本轮移动证据只声明 `390x844` CSS 视口。
- 公开用户主页浏览器运行态正常，但初始 HTML 暂缺服务端 canonical、Open Graph、Twitter card 和 JSON-LD，登记为非阻断 P2。
- 部署后进入 [发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)，首个功能专题为商城商品效力与权益履约。
