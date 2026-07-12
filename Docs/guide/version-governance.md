# 产品版本与发布标识治理

> 本页是 Radish 产品版本、客户端版本、Git tag、镜像 tag 与发布记录的一致性真相源；若旧的架构总规范或部署总指南中的历史版本示例与本页冲突，以本页为准。

## 核心结论

- 根目录 `version.json` 是唯一允许人工决定产品版本的位置。
- .NET、npm workspaces、Rust 扩展、Tauri 与 Flutter 均跟随同一个 Radish 日历版本。
- Flutter 的商店构建号独立递增，但展示版本仍跟随产品版本，例如 `26.7.1+1`。
- Git tag 的环境后缀只表示发布轨道，不改变产品版本。
- `Version Contract` 会阻止源码、lockfile、tag、镜像和正式发布记录不一致。
- 当前候选产品版本为 `26.7.1`；这不代表已经创建 tag 或完成发布。

## 单一真值

`version.json` 当前包含：

```json
{
  "schemaVersion": 1,
  "productVersion": "26.7.1",
  "flutterBuildNumber": 1
}
```

- `productVersion`：Radish 产品日历版本，格式固定为 `YY.M.RELEASE`。
- `flutterBuildNumber`：Flutter Android `versionCode` / iOS `CFBundleVersion` 使用的正整数构建号。
- 发布轨道、Git tag 和镜像 tag 不写入该文件，由 tag 后缀表达。

禁止把任一生成字段当成新的版本来源。修改版本时先改 `version.json`，再执行：

```bash
npm run version:sync
npm run check:version-contract
```

同步结果必须与 `version.json` 一并提交。

## 跟随范围

| 资产 | 版本字段 | 规则 |
|------|----------|------|
| .NET 全部项目 | `Directory.Build.props` 的 `Version / AssemblyVersion / FileVersion` | 跟随 `productVersion` |
| npm 根项目与四个 workspace | 各 `package.json` 和根 `package-lock.json` | 跟随 `productVersion` |
| Rust 原生扩展 | `Lib/radish.lib/Cargo.toml` | 跟随 `productVersion` |
| Tauri | `Cargo.toml`、`tauri.conf.json`、`Cargo.lock` | 跟随 `productVersion` |
| Flutter | `Clients/radish.flutter/pubspec.yaml` | `${productVersion}+${flutterBuildNumber}` |
| 正式 Web 发布矩阵镜像 | DbMigrate、API、Auth、Gateway、Frontend | 使用同一个完整 Git tag |

Tauri 与 Flutter 即使暂不属于本次正式 Web 发布矩阵，也不维护独立产品版本。它们复用 Radish 产品能力和服务端契约，因此展示版本统一；是否构建、签名、分发仍由各自发布矩阵决定。

npm workspace 只允许根 `package-lock.json`。`Frontend/radish.client` 与 `Frontend/radish.console` 下不得恢复独立 lockfile。

## Tag 规则

基础格式：

```text
vYY.M.RELEASE-(dev|test|release)
```

需要区分同日多次候选或热更新时，可以使用：

```text
vYY.M.RELEASE.DDXX-(dev|test|release)
```

- `DD` 为 `01-31`。
- `XX` 为 `01-99`。
- `DDXX` 只扩展 tag 和镜像标识；源码产品版本仍为三段式 `YY.M.RELEASE`。
- 不带 `v`、月份补零、未知轨道或产品版本不匹配的 tag 会被拒绝。

示例：

- `v26.7.1-test`
- `v26.7.1.1203-test`
- `v26.7.1-release`

本地创建 tag 前必须执行：

```bash
node Scripts/version-contract.mjs --tag v26.7.1-test
```

## 正式发布记录契约

`-release` tag 对应的提交必须已包含一份发布记录。记录文件顶部必须提供以下 front matter：

```yaml
---
releaseTag: v26.7.1-release
productVersion: 26.7.1
imageTag: v26.7.1-release
---
```

规则：

- `releaseTag` 必须等于即将创建的正式 Git tag。
- `productVersion` 必须等于 `version.json.productVersion`。
- `imageTag` 必须等于 `releaseTag`，五个正式镜像不得分别漂移。
- 同一个 `releaseTag` 必须且只能匹配一份发布记录。
- `dev / test` tag 不强制正式发布记录，但仍校验源码版本和 tag 格式。

发布记录应在候选提交中先写明“尚未部署”的真实状态；部署后再补实际结果，不能在 tag 前预写成功结论。

## 自动门禁

- `npm run check:version-contract`：只读检查所有生成版本字段和 lockfile 归属。
- `npm run check:version-contract:self-test`：验证 tag 格式与发布记录元数据解析规则。
- `npm run validate:baseline:quick`：包含以上两项。
- GitHub `Repo Quality / Version Contract`：PR 中独立展示版本结果；现有必需检查 `Baseline Quick` 同时执行相同门禁，因此无需修改远程 ruleset 也能阻断漂移。
- GitHub `Docker Images`：在登录 GHCR 和构建前执行 tag 校验；正式 tag 额外校验发布记录。
- Docker 镜像统一写入 `org.opencontainers.image.version=<完整 tag>`。

## 发布顺序

1. 在 `dev` 确认目标产品版本并修改 `version.json`。
2. 执行 `npm run version:sync`，提交全部生成字段。
3. 完成 Release Go 验证，并把正式发布记录随候选提交纳入 `dev -> master` PR。
4. 合并到 `master` 后，在 tag 目标提交执行 `node Scripts/version-contract.mjs --tag <tag>`。
5. 创建并推送 tag；Docker workflow 复核相同契约后才允许推送镜像。
6. 部署时用 `RADISH_IMAGE_TAG=<tag>` 固定五个镜像。
7. 部署后更新发布记录中的真实验证与回滚结论。

合并 `master`、创建 tag 和部署仍是三个独立决策。版本同步完成不自动授权创建 tag 或部署。

## 历史边界

- 不重写、不删除既有 Git tag 或已经发布的镜像。
- 历史 `26.1.1` 源版本与 `v26.3.x / v26.5.x` tag 的漂移作为既有事实保留。
- 新门禁只约束当前及未来提交，防止继续产生无法解释的版本组合。
