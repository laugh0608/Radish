# P3-9-E 发布候选路径总回归与收口结论（2026-06-07）

> 本记录承接 `P3-9-E 发布候选路径总回归与收口结论`。
>
> 本批汇总 `P3-9-A / B / C / D` 的自动化证据、人工复核边界和后续回拉规则，用于判断 P3-9 首批真实使用主路径是否具备进入 `PR -> master` 前扩大验证与人工复核的条件。

## 批次结论

`P3-9-A / B / C / D` 已分别完成主路径验收入口、Flutter 登录移动用户路径、访客公开访问与分享、Console 管理员排障入口整备。`P3-9-E` 本轮没有继续扩展完整移动商城、通知中心、资产中心、创作器、WebOS / Tauri 新功能、PublicId 全量迁移或完整 E2E 平台。

自动化总回归结论为通过。当前可进入人工复核与 `PR -> master` 前扩大验证准备；若人工复核暴露问题，只回拉主路径阻断、状态恢复、身份契约、公开链接、跨端回流或 Console 排障效率缺口，不恢复 P3-8-D 逐项深挖节奏。

## 自动化覆盖

| 路径 | 已覆盖证据 | 结论 |
| --- | --- | --- |
| P3-9-A 验收入口 | 主路径验收矩阵、`realUsagePathContracts.test.ts`、登录回流和公开路由守护 | 通过 |
| P3-9-B 登录移动用户 | Flutter `flutter test`、`flutter analyze`，覆盖发现 / 论坛 / 文档 / 商城 / 我的主路径 | 通过 |
| P3-9-C 访客公开访问 | `radish.client` 测试、公开 head / share / JSON-LD / desktop handoff 守护、head smoke self-test | 通过 |
| P3-9-D Console 管理员 | `radish.console` 测试、Console URL helper、角色授权 helper、权限链路扫描 | 通过 |
| 身份契约 | `validate:identity`、LongId 字符串安全扫描、后端身份语义定向测试 | 通过 |
| 仓库基线 | `validate:baseline:quick`、`validate:baseline`、client / console build | 通过 |

## 验证记录

执行目录：仓库根目录，Flutter 命令执行目录为 `Clients/radish.flutter`。

```bash
npm run test --workspace=radish.client
npm run type-check --workspace=radish.client
npm run test --workspace=radish.console
npm run type-check --workspace=radish.console
npm run build --workspace=radish.client
npm run build --workspace=radish.console
flutter test
flutter analyze
npm run validate:baseline:quick
npm run validate:identity
node Scripts/check-public-head-smoke.mjs --self-test
npm run validate:baseline
npm run check:repo-hygiene:changed
node Scripts/check-repo-hygiene.mjs Docs/planning/current.md Docs/planning/p3-9-real-usage-release-candidate.md Docs/records/index.md Docs/records/p3-9-e-release-candidate-total-regression-record-2026-06-07.md
git diff --check
npm run lint:changed
```

结果：

- `npm run test --workspace=radish.client`：通过，`210` 项测试通过。
- `npm run type-check --workspace=radish.client`：通过。
- `npm run test --workspace=radish.console`：通过，`20` 项测试通过。
- `npm run type-check --workspace=radish.console`：通过。
- `npm run build --workspace=radish.client`：通过。
- `npm run build --workspace=radish.console`：通过。
- `flutter test`：通过，`183` 项测试通过。
- `flutter analyze`：通过，`No issues found`。
- `npm run validate:baseline:quick`：通过。
- `npm run validate:identity`：通过，LongId 字符串安全扫描未发现回归；后端身份语义定向测试 `14` 项通过，仍有项目既有 XML 注释 warning。
- `node Scripts/check-public-head-smoke.mjs --self-test`：通过。
- `npm run validate:baseline`：沙盒内首次在 `dotnet test` 阶段因 `vstest` 绑定本地 socket 被拒绝而中止；按沙盒规则提权重跑后通过，后端解决方案构建 `0` warning / `0` error，`Radish.Api.Tests` `405` 项通过。
- `npm run check:repo-hygiene:changed`：通过，检查 `3` 个已跟踪变更文件。
- `node Scripts/check-repo-hygiene.mjs ...`：通过，显式检查 `4` 个变更 / 新增文档文件。
- `git diff --check`：通过。
- `npm run lint:changed`：通过，本次变更没有需要 lint 的前端脚本文件。

未启动 Gateway / Auth / API / Vite dev server，未执行需要真实服务、测试账号和样本数据的人工复核，也未执行在线 public head smoke。

## 人工复核步骤

由用户在仓库根目录启动服务，AI 协作者不直接执行启动命令：

```bash
./start.sh
npm run dev --workspace=radish.client
npm run dev --workspace=radish.console
```

如果你使用 PowerShell，也可以用：

```bash
pwsh ./start.ps1
npm run dev --workspace=radish.client
npm run dev --workspace=radish.console
```

### 访客公开访问

1. 打开 `https://localhost:5000/`，确认普通浏览器根路径进入 `/discover`。
2. 用移动窄屏视口依次访问 `/discover`、`/forum`、`/docs`、`/shop`、`/leaderboard`、`/u/:id`。
3. 打开至少 1 条公开帖子、1 篇公开文档、1 个公开商品和 1 个公开个人页，确认返回来源、复制分享、canonical / OpenGraph / JSON-LD 语义一致。
4. 对至少一条详情执行在线 head smoke，示例：

```bash
node Scripts/check-public-head-smoke.mjs --base-url https://localhost:5000 --path /forum/post/<postPublicId> --path /docs/<slug> --path /shop/product/<productId>
```

### 登录移动用户

1. 在 Android 模拟器或真机启动 Flutter 客户端并登录测试账号。
2. 从发现页进入榜单、公开主页、论坛、文档、商城商品详情，再用 Android Back 或页面返回确认回到来源。
3. 发布纯文本帖子，提交评论或回复；提交失败场景需确认输入保留。
4. 从商品详情购买 1 件商品，进入订单详情，再查看背包发放和胡萝卜流水。
5. 查看通知、最近访问、胡萝卜资产、经验记录和个人资料编辑，确认返回“我的”后状态稳定。

### Console 管理员

1. 访问 `https://localhost:5000/console/` 并登录管理员账号。
2. 从用户详情查看最近订单和胡萝卜流水，进入订单详情后返回用户详情来源。
3. 从订单详情进入商品、用户和扣款流水，确认 `returnTo` 返回链路保留筛选和详情上下文。
4. 从商品详情进入相关订单，再返回商品详情或原订单来源。
5. 在角色授权页勾选资源树、查看接口预览并保存；加载中、保存中或无权限账号下不应触发保存或勾选修改。

## 后续规则

- 人工复核无新增阻断时，进入 `PR -> master` 前扩大验证和合并准备。
- 人工复核发现 `P0/P1` 时，按同类主路径缺口成组修复，并补定向测试和记录。
- `P2/P3` 体验项进入后续回拉池，不把完整商城、完整通知中心、完整资产中心、完整创作器、完整 PublicId 迁移或完整 E2E 平台作为当前批次前置。
