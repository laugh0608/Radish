# F4 `dev -> master` 合并前回归记录

> 日期：2026-08-22（Asia/Shanghai）
>
> 范围：`origin/master` 的 `a182aec6` 之后、当前 `dev` 上的阶段性成果，包含 PR 质量门禁治理、Flutter Native P1 / P2 / P3、社区治理入口与长期协作入口收敛。

## 变更摘要

- 将 `PR -> master` 的远端质量门禁收敛为六个并行组件与单一 required check `Candidate Quality`，并同步 ruleset、验证脚本和排障文档。
- 完成 Flutter Native P1 全页面事实审计、P2 四主题与自适应技术基座，以及独立 `radish-flutter-native-ui-v1.pen` P3 代表设计源。
- 补齐 Issue / PR、贡献、安全与行为准则等社区治理入口，并收敛 `AGENTS.md` / `CLAUDE.md` 长期协作约束。
- 合并前预检清理 P2 记录中的一处尾随空格，避免 `git diff --check` 与仓库卫生门禁失败。

## 影响专题

- Flutter Native 主题、Shop 权益、compact / medium / expanded Shell、Discover 与 Forum Detail 代表实现。
- Repo Quality workflow、分支保护 contract、变更文件收集与文档治理。
- Agent 协作入口、社区治理入口和对应 `Docs/` 真相源。

## 后端 / API 影响面

- 命中情况：命中。
- 命中原因：后端门禁资产。
- 命中文件集中在 Repo Quality workflow、后端影响面规则、变更文件收集、后端回归入口和根 `package.json`；本批没有修改后端业务宿主、Service、Repository、模型、数据库迁移或 API 契约。

## 身份语义影响面

- 命中情况：命中。
- 命中原因：默认执行面文档 / 门禁资产，以及 Flutter 外部 LongId 字符串安全边界。
- 本批没有修改 Auth 协议输出或 Token 解析；Flutter 主题与页面改动仍由 LongId 扫描覆盖，避免外部对象 ID 被提前数值化。

## 自动化执行

- `npm run validate:candidate`：宿主机通过。
  - 全量仓库卫生预算与文档治理通过，Markdown 本地链接有效。
  - 四个 Web workspace 全量零 warning Lint 通过。
  - warning-as-error full baseline 通过；TypeScript、前端测试 `48 + 32 + 557 + 138`、`.NET` 构建和后端测试 `1280 passed / 41 skipped` 均正常。
  - npm / NuGet High 与 Critical 已知漏洞均为 `0`。
- `npm run validate:backend`：通过；warning-as-error 构建 `0 warning / 0 error`，后端测试 `1280 passed / 41 skipped`。
- `npm run validate:identity`：通过；运行时 Claim、协议输出、LongId 扫描无回归，身份语义后端定向测试 `35 / 35`。
- `flutter analyze --no-pub`：通过，零问题。
- `flutter test --no-pub`：通过，`228 / 228`。
- `git diff --check origin/master...HEAD`：在清理唯一尾随空格后通过。
- `npm run validate:baseline:host`：未执行；本批未命中宿主配置、数据库结构、种子或部署链路。

## 人工验收与运行态边界

- 本轮未启动服务、未执行 Gateway 浏览器 Smoke，也未进行部署或发布。
- Flutter P2 代码与 P3 Pencil 静态复核事实分别保留在对应专题记录；本轮只做阶段性合并前工程回归，不重复伪造运行态证据。
- P3 仍是独立代表设计源，后续 P4 实现差分不随本次 PR 提前进入运行时代码。

## 故障归类 / 环境边界

- 初次在受限沙盒运行候选 baseline 时，前端静态服务器合约测试因沙盒禁止绑定 `127.0.0.1` 返回 `listen EPERM`。
- 同一 `npm run validate:candidate` 随后在宿主机完整重跑并通过，因此该现象归类为受限环境边界，不是代码、contract 或默认执行面失败。
- 本地没有注入 PostgreSQL 17 测试连接，后端套件有 `41` 项 PostgreSQL 专题跳过；PR 的 `Backend Guard` 必须在远端 PostgreSQL 17 环境通过后才能合并。

## 结论

- 当前 `dev` 批次已满足本地 `PR -> master` 合并前工程门槛，可以发起阶段性 PR。
- 合并仍以远端聚合 `Candidate Quality` 成功为前提，不绕过 required check。
- PR 合入后立即将最新 `origin/master` 回灌 `dev`；本次 PR 不自动创建 tag、发布镜像或部署生产环境。

## 风险 / 后置项

- 等待 PR 远端 PostgreSQL 17 Backend Guard 与其余 Repo Quality 组件给出最终证据。
- 合并后先完成 `master -> dev` 拓扑回灌，再开始下一轮开发。
