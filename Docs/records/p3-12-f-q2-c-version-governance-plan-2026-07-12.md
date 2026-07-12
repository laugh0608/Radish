# P3-12-F Q2-C 版本单一真值治理方案

> 状态：`Release Go 必要子集已完成`
>
> 日期：`2026-07-12`（Asia/Shanghai）
>
> 范围：产品版本、生成字段、客户端版本、Git tag、正式镜像和发布记录；不创建 tag、不推送镜像、不执行部署。

## 审计结论

- .NET、npm 根项目与 workspaces、Rust 长期停留在 `26.1.1`。
- `v26.3.2-release` 与 `v26.5.5-test` 所在提交的源版本仍为 `26.1.1`，tag 和源码没有自动关联。
- Docker workflow 只按 `v*-dev / test / release` 通配符分轨，过去未验证 tag 内产品版本，也未校验正式发布记录。
- Tauri 与 Flutter 仍使用初始化版本 `0.1.0`，无法直接表达其对应的 Radish 服务端版本。
- client / console 各自遗留一份 `0.0.0` 独立 package lock，与根 npm workspace lock 形成双重来源。
- 历史存在一枚缺少 `v` 前缀的 test tag；本批不改写历史，只阻止新增不规范 tag。

## 已确认决策

1. 根 `version.json` 是产品版本唯一人工真值。
2. 当前候选产品版本切换为 `26.7.1`，但本批不创建对应 tag。
3. .NET、npm、Rust、Tauri、Flutter 和正式镜像统一跟随 Radish 日历版本。
4. Flutter 构建号独立递增，当前为 `26.7.1+1`。
5. npm workspace 只保留根 `package-lock.json`。
6. tag 后缀只表达 `dev / test / release` 轨道；同日构建可增加 `DDXX`。
7. `-release` tag 必须在目标提交中找到唯一、机器可读且版本一致的发布记录。
8. 合并、tag、镜像发布和部署继续保持独立授权边界。

## 实现范围

- `Scripts/version-contract.mjs`：同步与只读校验所有版本字段。
- `Scripts/version-contract.test.mjs`：覆盖规范 tag、异常 tag 和发布记录 front matter。
- `Repo Quality / Version Contract` 与 Baseline Quick：常态阻断源码漂移。
- `Docker Images`：构建前校验 tag / 正式发布记录，镜像写入统一 OCI version label。
- 长期规则收口到 [产品版本与发布标识治理](/guide/version-governance)，部署指南和发布记录模板只保留执行入口。

## 退出条件

- 所有产品资产从 `version.json` 同步到 `26.7.1`，Flutter 为 `26.7.1+1`。
- 版本检查和规则自测通过，异常 tag / 漂移字段能够产生非零退出。
- PR 与镜像 workflow 均接入版本门禁。
- 正式发布记录模板提供机器可读字段。
- 文档明确历史不回写、本批不创建 tag 或部署。

## 实施与验证结论

- `version.json` 已成为唯一人工真值，全部产品资产同步为 `26.7.1`，Flutter 为 `26.7.1+1`。
- 两个 workspace 陈旧 lockfile 已删除，版本校验会阻止其被重新引入。
- 版本规则自测 `5/5` 通过，覆盖规范 / 异常 tag、字段漂移、陈旧 lockfile、正式发布记录缺失与匹配。
- `npm run validate:baseline:quick` 通过，四个 workspace 类型检查与 client `319` 项测试均在 `26.7.1` 下运行。
- `dotnet build Radish.slnx -c Debug --no-restore --disable-build-servers`：`0` Warning / `0` Error。
- `npm run validate:ci` 通过：后端 `618` 通过、`7` 个 PostgreSQL 环境用例跳过，身份定向测试 `29/29` 通过。
- `npm run check:dependency-security`：npm / NuGet High、Critical 均为 `0`。
- Cargo metadata 将 Tauri crate 识别为 `26.7.1`；Flutter analyze 无问题，Flutter test `204/204` 通过。
- `Version Contract` 在 PR 独立展示；现有必需检查 `Baseline Quick` 同时执行版本门禁，因此无需改变远程 ruleset 即可阻断源码漂移。
- Docker workflow 在构建前校验 tag；正式 tag 还会校验唯一发布记录，并把完整 tag 写入 OCI version label。
- 本批未创建 Git tag、未推送镜像、未修改远程 ruleset、未部署。
