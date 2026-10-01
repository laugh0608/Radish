# 2026-10-01 dev → master 批次回归

## 范围

- 日期：2026-10-01（Asia/Shanghai）；由项目所有者要求将近期成果通过 PR 集成到 `master`。
- 基线：`master a252f865`；原候选：`dev 8bd29aae`，34 个提交，243 个变更文件；追加本轮测试修正及回归记录。
- 内容：PostgreSQL 论坛互动 / 点赞标识符修复、部署持久目录归集、Hangfire 看板短期会话、Console 信息密度与登录过渡，以及日志 L1 契约 / 采集实验和 L2 已列明生成端治理。
- 影响判定使用完整 `origin/master...dev` 批次：后端宿主 / 服务 / 模型及门禁资产命中；身份运行时 Claim、外部 LongId 和门禁资产命中。工作区默认 changed-only 不代表本批次范围。

## 本轮提前发现并修正

1. `ProducerLoggingTests` 未传入 xUnit 测试取消令牌，触发 `xUnit1051`，使后端 `--warnaserror` 构建失败。改为 `TestContext.Current.CancellationToken`，不改业务行为。
2. PostgreSQL OpenIddict 迁移测试仍断言日志治理前的 `provider / applied` 文案。改为分别检查首次和重复 `apply` 后的实际迁移记录、数据库就绪状态与模型一致性，保留迁移幂等性验证。

## 自动化结果

| 检查 | 本轮结果 |
| --- | --- |
| `check:repo-quality:candidate` | 通过；92 个既有问题文件仍在审计预算内，无新增问题；文档卫生与本地链接通过 |
| 全量 `npm run lint` | 四个 Web workspace 通过，零 warning |
| Client / Console 显式 app / node 类型检查及 Console strict 检查 | 五条命令通过，补足默认聚合类型检查的已知覆盖限制 |
| Client / Console 生产 build | 通过；保留既有 chunk 体积提示 |
| `check:dependency-security` | 宿主机联网审计通过，npm / NuGet High 与 Critical 均为 0 |
| `check:logging-contract` | 27 通过 |
| `check:production-deploy` | 8 通过 |
| Compose `config --quiet` | 使用 `Deploy/.env.example` 通过，未启动部署服务 |
| Rust `cargo test` / `cargo build --locked --offline` | 7 测试通过，动态库构建通过；保留既有 unused import warning |
| `validate:baseline:host -- --warnings-as-errors` 的全部默认基线步骤 | 通过；后端构建 0 warning / error，1998 测试通过、0 失败、0 跳过，包含 PostgreSQL 与真实 Rust ABI |
| `DbMigrate doctor / verify` 只读自检 | doctor 返回 0 但报告两条本机 SQLite 版本引用异常；严格 verify 返回 1，故 host 聚合结果为 failed |
| `validate:identity` | 扫描与 35 项身份测试通过，0 跳过 |

实际使用 `.tmp/master-pr-baseline-20261001.md` 保存本轮 host 报告；通过 `RADISH_TEST_POSTGRES_CONNECTION_STRING` 与 `RADISH_TEST_NATIVE_LIBRARY` 指向本轮隔离测试资源。完整基线已执行与 `validate:backend` 相同的后端 `--warnaserror` 构建及全量测试步骤，未为入口名称重复执行同一回归。

本机 SQLite 中两条帖子 RevisionTag 指向不存在或跨租户的 Revision，属于现有本地数据前置问题；本轮未写入或自动修复该数据。该状态不构成 PR CI 源码失败，但当前不能据此宣称本机宿主 readiness 通过。CI PostgreSQL 空库迁移与幂等性回归已通过。真实 AuthFlow / 浏览器验收未执行；本批未修改 Auth 协议输出或官方 Token 解析。

## 环境与证据边界

- 沙盒拒绝前端合约测试监听回环端口、联网审计及 Docker socket；相关操作转到宿主机后取得真实结果，不把环境失败记作业务失败或跳过。
- 项目所有者单独授权复用已有 `postgres:17` 镜像启动 `radish-master-pr-pg-20261001`，只监听 `127.0.0.1:55439`，不挂载宿主目录。数据库回归只写隔离临时库；测试结束停止容器并删除其匿名卷。
- Rust 真实动态库来自本轮 `Lib/radish.lib/target/debug/libradish_lib.dylib`，只注入测试进程，不复制到产品目录。
- 未执行真实业务宿主启动、登录 / 浏览器 Smoke、生产部署或跨平台原生验收；Flutter 和 CI workflow 本批没有变化。

## 集成与后置边界

- 本地 PR 源码门禁通过，可创建 `dev → master` PR；宿主 readiness 因现有 SQLite 数据异常未通过。本轮不创建 tag、Release、镜像或部署；GitHub 聚合 `Candidate Quality` 仍须在 PR 上成功。
- 日志 L2 尚未整体关闭；`RadishLogging.Enabled` 继续为 false，正式传输 / 入库与后续 Console 告警不因本次集成取得完成结论。
- 旧部署切换持久目录前须按[部署指南](../deployment/guide.md)迁移原数据并保留 `.env`、证书和密钥；不能将空目录当作旧数据启动。Hangfire 权限登记按部署迁移入口更新。
- 合并建议采用 merge commit。项目所有者合并后，应先由后续收口执行人 fetch 最新 `origin/master`、按 [ADR 0001](../adr/0001-branch-and-pr-governance.md)快进或普通 merge 回灌并推送 `dev`，完成后再追加下一轮开发。
