# P3-9-E 发布候选 PR 前收口记录（2026-06-08）

> 本记录承接 `P3-9-E 发布候选路径总回归与收口结论`。
>
> 本批围绕访客公开访问、Flutter 登录移动用户和 Console 管理员排障三类主路径，收口人工复核、自动化验证和 `PR -> master` 前合并判断。

## 批次结论

`P3-9-A / B / C / D` 的首批发布候选路径已完成自动化总回归和人工复核。2026-06-08 复核确认此前 Flutter 登录态商城 / 钱包缺口已修复，商品详情余额展示、购买资格检查、单商品购买、订单详情、背包发放和胡萝卜资产 / 流水入口未暴露新增 `P0/P1`。

访客公开访问、公开分享、来源返回与 Console 管理员排障路径抽查未发现新增阻断。当前工程结论是：`P3-9` 可进入 `PR -> master` 合并准备，不需要恢复 `P3-8-D` 旧路径反复深挖。

## 变更摘要

| 项目 | 结果 |
| --- | --- |
| 当前规划入口 | 更新 `Docs/planning/current.md`，将下一顺位切到 PR 前扩大验证与合并准备 |
| P3-9 专题页 | 更新人工复核状态与 `P3-9-E` 批次状态 |
| PR 前记录 | 新增本收口记录，承载自动化、人工复核和合并判断 |

## 影响专题

| 专题 | 影响判断 |
| --- | --- |
| 访客公开访问 | 人工抽查通过，未新增代码改动 |
| Flutter 登录移动用户 | 人工复核通过，自动化补跑 `flutter test` / `flutter analyze` |
| Console 管理员排障 | 人工抽查通过，默认 baseline 覆盖 Console 权限链路扫描 |
| 身份语义 / LongId | `Docs/planning/current.md` 命中默认执行面文档 / 门禁资产，补跑 `validate:identity` |
| 宿主 / 部署 | 本轮未修改宿主运行、配置、数据库或部署链路，未执行 `validate:baseline:host` |

## 自动化执行

执行目录：仓库根目录 `/Users/luobo/Code/Radish`

```bash
npm run check:repo-hygiene:changed
git diff --check
npm run validate:ci
npm run validate:baseline
npm run validate:identity
```

执行目录：`Clients/radish.flutter`

```bash
flutter test
flutter analyze
```

结果：

- `npm run check:repo-hygiene:changed`：通过，未发现文本卫生问题。
- `git diff --check`：通过。
- `npm run validate:ci`：通过。
  - Repo Hygiene changed-only 通过。
  - Frontend changed-only Lint 通过，本次变更没有需要 lint 的前端脚本文件。
  - Baseline Quick 通过。
  - Identity Guard 判定命中 `Docs/planning/current.md`，原因类别为默认执行面文档 / 门禁资产。
  - 自动追加的 Identity Regression Validation 通过。
- `npm run validate:baseline`：通过。
  - 前端 TypeScript 类型检查通过：`@radish/ui`、`radish.client`、`radish.console`。
  - `radish.client` 最小 node 测试通过：`210` 个测试通过。
  - Console 权限链路扫描通过。
  - Repo Quality contract 自校验通过。
  - 身份语义 impact 判定自校验通过。
  - 身份语义防回归扫描通过。
  - 后端解决方案构建通过。
  - 后端测试通过：`405` 个测试通过。
- `npm run validate:identity`：通过。
  - 运行时 Claim 读取扫描通过。
  - 协议输出回退风险扫描通过。
  - 外部 LongId 字符串安全扫描通过。
  - 身份语义后端定向测试通过：`14` 个测试通过。
- `flutter test`：通过，`186` 个测试通过。
- `flutter analyze`：通过，`No issues found`。

## 人工验收

执行情况：已执行。

摘要：

- Flutter 登录态商品详情余额展示、购买资格检查、单商品购买、订单详情、背包发放和胡萝卜资产 / 流水入口复测通过，未暴露新增 `P0/P1`。
- 访客公开访问、公开分享和来源返回路径抽查通过，未暴露新增阻断。
- Console 管理员排障路径抽查通过，未暴露新增阻断。

## 故障归类 / 环境边界

- 归类：受限环境边界。
- 说明：首次在沙盒内执行 `npm run validate:baseline` 和 `npm run validate:ci` 时，后端测试阶段因 `dotnet test` 本地测试通信端口绑定被沙盒拒绝而中止；按验证提权规则重跑同一命令后通过。该失败不归类为代码回归。

## 结论

`P3-9` 发布候选主路径当前具备进入 `PR -> master` 合并准备的工程条件。后续若 PR 或扩大验证再暴露问题，只回拉主路径阻断、状态恢复、身份契约、公开链接、跨端回流或 Console 排障效率问题。

## 风险 / 后置项

- `validate:baseline:host` 未执行：本轮未触达宿主运行、配置、数据库或部署链路，后续进入发布 / 部署节点时再按 `M14 / M15` 口径执行。
- Flutter `发现 / 消息 / 更多 / 我的` 信息架构仍为后续产品设计评估项，不进入本轮发布候选合并前置。
