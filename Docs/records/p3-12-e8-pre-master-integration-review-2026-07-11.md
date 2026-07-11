# P3-12-E8 `dev -> master` 集成审阅记录

> 日期：2026-07-11（Asia/Shanghai）
> 状态：范围、安全阻断治理与批次级验证已完成；集成提交完成后等待推送与创建 PR
> 目标：只判断当前 P3-12-D 至 E8 / Q0 批次是否具备发起 `dev -> master` PR 的工程条件，不创建 tag、不部署。

## 1. 分支与提交边界

- 远程引用已执行 `git fetch --prune` 刷新。
- `origin/master` 与本地 `master` 均为 `5f93f332`；`origin/dev` 为 `09578693`，本地 `dev` 为 `c2d094b9`，本地已提交 Q0 批次比 `origin/dev` 领先 3 个提交。
- `master...dev` 当前计数为 `8 / 85`：`master` 独有的 8 个对象全部是历史 PR merge commit，`dev` 独有 85 个开发提交。
- 当前 merge base 为 `2312d630`，也是最新 `master` merge commit 的第二父提交；`2312d630..master` 树差异为空。因此 8 个主线独有对象只代表 GitHub merge ancestry，不代表 `dev` 缺少 8 批代码内容。
- 经典三方合并预演未发现冲突信号。由于主线独有对象只有 merge ancestry、树内容无额外差异，当前可以直接发起 `dev -> master` PR，不需要为“形式上最新”额外制造本地 merge commit，也不对 85 个提交做大规模 rebase。若 GitHub 实际报告冲突或 ruleset 要求分支必须最新，再把 `master` merge 到 `dev` 后重跑差异检查。

## 2. 已提交 PR 影响面

当前 `master...dev` 已提交范围共 337 个文件，约 `+27,625 / -7,801`：

| 范围 | 文件数 | 主要内容 |
| --- | ---: | --- |
| `Frontend/` | 150 | 正式 Web public / private / author 路径、WebOS 收束、Console 治理与移动任务流、共享 Markdown 安全能力 |
| `Docs/` | 96 | P3-12-D 至 E8 设计、规划、验收与发布工程口径 |
| `output/` | 27 | 已提交的阶段性 PC / mobile 验收截图证据 |
| 后端与测试 | 47 | API 暴露面删除、JWT audience、Auth transport / forwarded proto、通知与生产表面契约 |
| CI、依赖与仓库规则及根级文件 | 17 | Repo Quality、Dependency Security、ruleset 模板、依赖版本、验证脚本与协作入口 |

文件状态为新增 112、删除 17、修改 208。该范围属于阶段性集成批次，不适合再拆成按页面逐个进入 `master` 的小 PR；审阅应按以下四组理解：

1. P3-12-D 正式 Web / Console UI 与 Pencil 设计源阶段治理。
2. P3-12-E1 至 E8 产品成熟度、真实旅程和有限产品矩阵收口。
3. Q0-A 至 Q0-D 依赖安全、生产暴露面、身份 / transport 与 Markdown 协议安全。
4. 对应测试、运行态证据、协作规则与发布工程文档。

## 3. 本次集成提交范围

本次提交完整纳入 E8-B 收口与集成门禁：

- Discover 内容优先结构与 Pencil `P02`。
- 公开个人页关注、取消关注和受控登录回流。
- 公开 Docs `Published + Public + 未删除` 专用服务端契约。
- 公开帖子、快捷回复与评论举报入口，以及举报队列帖子导航查询修复。
- Console 移动低风险治理静态守卫、定向测试、E8-B / Q0 运行态记录和当前规划。
- 两份附件上传脚本的认证输入治理，以及高置信敏感字面量防回归门禁。

上述文件已按同一阶段性集成批次完成提交前检查；提交成功后可直接推送 `dev` 并创建内容完整的 `dev -> master` PR。

## 4. 风险审阅

### 4.1 高关注但已验证

- Auth / JWT / Gateway：audience、Forwarded Proto、transport security 与登录 / refresh 链路均命中 Identity Guard，并已完成自动化和真实协议回归。
- 生产 API 表面：删除 v2、Weather、性能、敏感配置、事务和测试写入口；反射契约、完整 API 测试与 Scalar v1 运行态共同固定退出条件。
- 公开内容与治理：公开 Docs 服务端谓词、关系复访、举报快照 / 审核 / 无处罚留痕已完成真实旅程。
- 大范围前端：正式 Web 与 Console 同时变化，但已按专题积累 Pencil、PC / mobile smoke、静态契约、类型检查、测试和构建证据。

### 4.2 合并与发布边界

- `Deploy/docker-compose.local.yaml`、Auth launch profile 和宿主配置有变化，PR 可合并判断已补 host baseline；生产相似 PostgreSQL / OpenIddict 升级演练仍属于 `P3-12-F` Release Go 门禁，不前移为本 PR 的部署动作。
- `package-lock.json` 大幅收敛来自 Q0-A 依赖治理，已由 npm / NuGet 安全审计和 workspace 验证覆盖。
- 本批包含 27 张既有验收截图，属于阶段证据；未新增运行生成目录或敏感数据文件。
- 合并到 `master`、创建 tag 与部署仍是三个独立决策；本记录只支持创建集成 PR。

### 4.3 集成安全复核补充发现

- `Radish.Api.Tests/HttpTest/test-attachment-upload.sh` 自历史提交 `2ec20505` 起保存了完整 JWT；同组 Bash / PowerShell 脚本还沿用默认账号密码、client secret 和 password grant。该 JWT 已过期且不是 refresh token，但活动树仍不得继续保留完整 Claims 与凭据字面量。
- 两份脚本已统一改为只读取当前进程 `RADISH_ACCESS_TOKEN`，缺失时在任何运行态请求前明确失败；不再自行使用账号密码换 Token。
- 新增高置信扫描，只阻断完整 JWT、硬编码 Bearer Token 与私钥 PEM 头，且只报告文件、行号和规则，不回显命中值。环境变量、模板变量、占位值与 `invalid_token` 测试值保持允许。
- 规则自测和全仓扫描已接入 `validate:baseline:quick`，因此本地 `validate:ci` 与远程 `Baseline Quick` 使用同一门禁；历史对象不做破坏性重写，当前树与后续提交必须保持零命中。

## 5. 批次级验证结果

- `npm run validate:ci -- --report-file .tmp/validate-ci-report.md`：通过；Repo Hygiene、changed-only lint、Baseline Quick、Backend Guard、Identity Guard 全部闭合。
- `npm run validate:baseline:host -- --report-file .tmp/validate-baseline-host-report.md`：通过；四个前端 workspace type-check、client 319 / 319、Console 权限扫描、解决方案构建、API 572 / 572、DbMigrate doctor / verify 全部通过。
- `npm run check:host-runtime -- --details --report-file .tmp/host-runtime-report.md`：Gateway / API / Auth 均为 `200`。
- `npm run check:dependency-security`：npm / NuGet High / Critical 均为 `0`。
- `npm run check:sensitive-literals:self-test`：3 / 3 通过。
- `npm run check:sensitive-literals`：覆盖 2229 个文本文件，零命中。
- E8-B 七项有限产品矩阵：全部通过，无接受后置项和已知 `P0/P1`。
- Q0 成组运行态：Scalar v1、授权码、受保护 API、UserInfo、refresh、audience 拒绝、Hub / Markdown 真实旅程均已覆盖。
- `git diff --check master...dev`、工作区 `git diff --check` 与仓库卫生：通过。

## 6. 集成结论与执行顺序

代码、产品矩阵、安全门禁和批次级验证已经具备创建 `dev -> master` PR 的工程条件；集成审阅发现的活动树凭据阻断也已关闭。集成提交完成后按以下顺序处理：

1. 复核 `git status`、`master...dev`、`git diff --check` 和关键报告结论，随后推送 `dev`。
2. 创建 `dev -> master` PR，使用仓库模板写明四组范围、实际验证、配置影响、风险和回滚边界。
3. 等待远程 `Repo Quality` 与首次真实 `Dependency Security` required check 全部上报并通过，再决定是否合并；不创建 tag，不部署。

在远程必需检查通过前不得把本地审计结果写成已合并结论；若 GitHub 要求分支更新，再按真实提示同步 `master`，不预先制造无内容收益的 merge commit。
